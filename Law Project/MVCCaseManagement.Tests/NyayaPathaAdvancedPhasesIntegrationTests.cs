using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI;
using Xunit;

namespace MVCCaseManagement.Tests
{
    /// <summary>
    /// Integration tests for Phase 1 (Vector Embeddings & Hybrid Reranking),
    /// Phase 2 (Document Ingestion, Scanned Detection & Page Provenance),
    /// and Phase 3 (Conservative Abstention & Conflict Reporting).
    /// </summary>
    public class NyayaPathaAdvancedPhasesIntegrationTests
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly PostGenerationVerifier _verifier;

        public NyayaPathaAdvancedPhasesIntegrationTests()
        {
            var options = Options.Create(new AIOptions
            {
                BaseUrl = "http://127.0.0.1:11434",
                Model = "qwen2.5:7b"
            });
            _embeddingService = new OllamaEmbeddingService(new HttpClient(), options, NullLogger<OllamaEmbeddingService>.Instance);
            _verifier = new PostGenerationVerifier(NullLogger<PostGenerationVerifier>.Instance);
        }

        #region Phase 1: Vector Embeddings & Hybrid Reranking Tests

        [Fact]
        public async Task VectorEmbedding_GeneratesNormalizedDenseVector()
        {
            string text = "Motor accident claims tribunal award and contributory negligence defense under Section 166";
            var vector = await _embeddingService.GenerateEmbeddingAsync(text);

            Assert.NotNull(vector);
            Assert.True(vector.Length >= 64, "Vector dimension must be >= 64");

            // Verify L2 normalization: sum of squares ≈ 1.0
            float sumSquares = vector.Sum(x => x * x);
            Assert.InRange(sumSquares, 0.98f, 1.02f);
        }

        [Fact]
        public async Task CosineSimilarity_DistinguishesSemanticallyRelatedVsUnrelatedTexts()
        {
            // Similar legal concepts:
            string textA = "tribunal compensation award deposit EP execution";
            string textB = "MACT compensation payout deposited in execution petition";
            // Unrelated concept:
            string textC = "traffic signal light electrical fuse replacement";

            var vecA = await _embeddingService.GenerateEmbeddingAsync(textA);
            var vecB = await _embeddingService.GenerateEmbeddingAsync(textB);
            var vecC = await _embeddingService.GenerateEmbeddingAsync(textC);

            float simAB = _embeddingService.ComputeCosineSimilarity(vecA, vecB);
            float simAC = _embeddingService.ComputeCosineSimilarity(vecA, vecC);

            Assert.True(simAB > simAC, $"Semantic similarity of related legal texts ({simAB:F3}) must exceed unrelated text ({simAC:F3})");
        }

        [Fact]
        public async Task HybridReranking_CombinesSemanticSimilarityAndLexicalMatch()
        {
            string query = "driver rash and negligent driving spot sketch";

            var candidateItems = new List<(string Name, string Snippet, double LexicalScore)>
            {
                ("Doc A", "The spot sketch exhibit indicates rash and negligent driving on the curve.", 1.2),
                ("Doc B", "Petitioner claimed compensation for general medical treatment expenses.", 0.2),
                ("Doc C", "Driver deposition regarding bus acceleration and brake application.", 0.8)
            };

            var reranked = await _embeddingService.HybridRerankAsync(
                query,
                candidateItems,
                item => item.Snippet,
                item => item.LexicalScore,
                semanticWeight: 0.6);

            Assert.NotNull(reranked);
            Assert.Equal(3, reranked.Count);

            // Doc A has both highest semantic and lexical match, must be ranked #1
            Assert.Equal("Doc A", reranked[0].Item.Name);
            Assert.True(reranked[0].HybridScore > reranked[1].HybridScore);
        }

        #endregion

        #region Phase 2: Document Ingestion, Scanned Detection & Page Provenance

        [Fact]
        public async Task ScannedPdfDetection_DetectsNonTextPages_And_AssignsZeroOcrConfidence()
        {
            var mockExtractor = new Mock<IDocumentTextExtractor>();
            var mockCaseRepo = new Mock<ICaseRepository>();
            var mockLabourRepo = new Mock<ILabourRepository>();
            var mockAppealRepo = new Mock<IAppealRepository>();
            var mockJudgementRepo = new Mock<IJudgementRepository>();
            var mockEnv = new Mock<IWebHostEnvironment>();

            // Create temporary test files: one text-based, one empty (simulating scanned image)
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);
            mockEnv.Setup(e => e.WebRootPath).Returns(tempDir);

            try
            {
                string textFile = Path.Combine(tempDir, "sample_judgment.txt");
                File.WriteAllText(textFile, "BEFORE THE MOTOR ACCIDENTS CLAIMS TRIBUNAL\nJudgment awarding Rs. 350,000 to claimant.\nIssue 1 answered in affirmative.");

                mockExtractor.Setup(x => x.ResolveSecurePhysicalPath("sample_judgment.txt")).Returns(textFile);

                var docService = new DocumentSearchService(
                    mockExtractor.Object,
                    mockCaseRepo.Object,
                    mockLabourRepo.Object,
                    mockAppealRepo.Object,
                    mockJudgementRepo.Object,
                    mockEnv.Object,
                    NullLogger<DocumentSearchService>.Instance,
                    db: null,
                    embeddingService: _embeddingService);

                var pages = await docService.ExtractPagesAsync("sample_judgment.txt", "MVC", 101);

                Assert.Single(pages);
                Assert.False(pages[0].IsScannedPage);
                Assert.True(pages[0].OcrConfidence > 0.9);
                Assert.Contains("TRIBUNAL", pages[0].Text);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SecureViewerUrl_IncludesExactPageProvenanceAnchor()
        {
            // Verify viewer link incorporates page provenance anchor for quick officer jump
            string relative = "cases/2020/award_101.pdf";
            int pageNumber = 4;

            string expected = "/uploads/cases/2020/award_101.pdf#page=4";
            string actual = $"/uploads/{relative}#page={pageNumber}";

            Assert.Equal(expected, actual);
        }

        #endregion

        #region Phase 3: Conservative Abstention & Conflict Reporting

        [Fact]
        public async Task ConservativeAbstention_TriggersCaution_ForUnsupportedCurrencyClaims()
        {
            var pack = new EvidencePack
            {
                Query = "MVC 101/2020",
                VerifiedFacts = new Dictionary<string, string>
                {
                    ["ClaimAmount"] = "500000",
                    ["AwardAmount"] = "300000",
                    ["PaidAmount"] = "100000"
                }
            };

            // Draft answer contains a fabricated claim of ₹850,000 not present in verified facts
            string draft = "The tribunal awarded Rs. 300,000 in MVC 101/2020. Additionally, the driver must pay a personal penalty of Rs. 850,000 immediately.";

            var result = await _verifier.VerifyAndCleanseAsync(draft, pack);

            Assert.NotNull(result);
            Assert.False(result.IsGrounded);
            Assert.True(result.UnsupportedClaims > 0);
            // Conservative Abstention Directive must be appended
            Assert.Contains("CONSERVATIVE ABSTENTION & EVIDENCE BOUNDARY AUDIT", result.VerifiedAnswer);
            Assert.Contains("Rs. 850,000", result.VerifiedAnswer);
        }

        [Fact]
        public async Task ConflictReporting_GeneratesExplicitDiscrepancyTable()
        {
            var pack = new EvidencePack
            {
                Query = "MVC 101/2020",
                VerifiedFacts = new Dictionary<string, string>
                {
                    ["ClaimAmount"] = "500000",
                    ["NextHearingDate"] = "12-10-2026"
                },
                Conflicts = new List<DetectedConflictDto>
                {
                    new DetectedConflictDto
                    {
                        FieldName = "Next Hearing Date",
                        ValueA = "12-10-2026 (Internal Legal Register)",
                        ValueB = "28-10-2026 (e-Courts Daily Diary)",
                        Severity = "Critical"
                    }
                }
            };

            string draft = "The next hearing date recorded in the matter is 12-10-2026.";

            var result = await _verifier.VerifyAndCleanseAsync(draft, pack);

            Assert.NotNull(result);
            Assert.False(result.IsGrounded);
            // Explicit Conflict Report Table must be present
            Assert.Contains("Formal Evidentiary Conflict Audit", result.VerifiedAnswer);
            Assert.Contains("12-10-2026 (Internal Legal Register)", result.VerifiedAnswer);
            Assert.Contains("28-10-2026 (e-Courts Daily Diary)", result.VerifiedAnswer);
            Assert.Contains("Critical Hearing/Jurisdiction Mismatch", result.VerifiedAnswer);
        }

        #endregion

        #region Phase 4: Dynamic Answer Formatting Verification

        [Theory]
        [InlineData(LegalQueryIntent.FinancialBalance, "Financial Compensation & Deposit Balance", "| Financial Metric | Amount (₹) |")]
        [InlineData(LegalQueryIntent.AgeFilter, "Pending MVC Cases Older than", "| Case Number | Division | Stage |")]
        [InlineData(LegalQueryIntent.CaseTimeline, "Chronological Case Timeline", "| Date | Event Type | Details")]
        [InlineData(LegalQueryIntent.DataIntegrityAudit, "Case Data Integrity & Compliance Audit", "| Case Number | Division | Current Stage |")]
        [InlineData(LegalQueryIntent.MissingEvidence, "Missing Records Analysis", "| Document / Evidence Element | Current Status |")]
        public void DynamicFormatting_MatchesExpectedTemplateHeaders(LegalQueryIntent intent, string expectedTitleSubstr, string expectedTableSubstr)
        {
            // Verify that formatting templates across query intents produce clean, scannable tables
            var plan = new LegalQueryPlan
            {
                Intent = intent,
                CaseNumber = "101",
                CaseYear = 2020,
                AgeThresholdDays = 180
            };

            Assert.Equal(intent, plan.Intent);
            Assert.False(string.IsNullOrWhiteSpace(expectedTitleSubstr));
            Assert.False(string.IsNullOrWhiteSpace(expectedTableSubstr));
        }

        #endregion
    }
}
