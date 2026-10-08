using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class PostGenerationVerifier : IPostGenerationVerifier
    {
        private readonly ILogger<PostGenerationVerifier> _logger;

        public PostGenerationVerifier(ILogger<PostGenerationVerifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<VerificationResult> VerifyAndCleanseAsync(
            string draftAnswer,
            EvidencePack evidencePack,
            CancellationToken cancellationToken = default)
        {
            var result = new VerificationResult
            {
                VerifiedAnswer = draftAnswer ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(draftAnswer) || evidencePack == null)
            {
                return Task.FromResult(result);
            }

            var claims = new List<ExtractedClaim>();
            var contradictions = new List<string>();

            // Extract sentences from draft
            var sentences = SplitIntoSentences(draftAnswer);
            result.TotalClaims = sentences.Count;

            // Build corpus of all verified evidence text
            var evidenceCorpus = new StringBuilder();
            foreach (var kvp in evidencePack.VerifiedFacts)
            {
                evidenceCorpus.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            foreach (var chunk in evidencePack.Chunks)
            {
                evidenceCorpus.AppendLine(chunk.Content);
            }
            foreach (var cit in evidencePack.Citations)
            {
                evidenceCorpus.AppendLine($"{cit.Title} {cit.CaseNumber} {cit.Court} {cit.Date} {cit.Excerpt}");
            }
            string allEvidence = evidenceCorpus.ToString();

            int verifiedCount = 0;
            int unsupportedCount = 0;

            foreach (var sentence in sentences)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string trimmed = sentence.Trim();
                if (trimmed.Length < 10) continue;

                // Check for factual indicators: dates, currency, case numbers, statutory sections
                bool hasNumbers = Regex.IsMatch(trimmed, @"\b\d+[\d,./-]*\b");
                bool hasStatute = Regex.IsMatch(trimmed, @"\b(?:Section|Sec\.?|Order|Article)\s+\d+", RegexOptions.IgnoreCase);
                bool hasCaseRef = Regex.IsMatch(trimmed, @"\b(?:MVC|KID|MFA|PGA|ID|WP|WA|EP|CNR)\b", RegexOptions.IgnoreCase);

                bool isMaterialClaim = hasNumbers || hasStatute || hasCaseRef;

                if (!isMaterialClaim)
                {
                    // General explanatory prose
                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        Taxonomy = EvidenceTaxonomy.Inference,
                        IsSupported = true,
                        Confidence = 0.95
                    });
                    verifiedCount++;
                    continue;
                }

                // Verify against evidence pack
                bool isSupported = VerifySentenceAgainstEvidence(trimmed, allEvidence, evidencePack);

                if (isSupported)
                {
                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        EvidenceSnippet = "Corresponds with verified evidence pack.",
                        Taxonomy = hasStatute ? EvidenceTaxonomy.LegalRule : EvidenceTaxonomy.Fact,
                        IsSupported = true,
                        Confidence = 0.95
                    });
                    verifiedCount++;
                }
                else
                {
                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        EvidenceSnippet = "Unsubstantiated in provided records.",
                        Taxonomy = EvidenceTaxonomy.Inference,
                        IsSupported = false,
                        VerificationNote = "Fact or figure not explicitly substantiated in verified database or retrieved exhibits.",
                        Confidence = 0.40
                    });
                    unsupportedCount++;
                }
            }

            // Contradiction Check against detected conflicts
            if (evidencePack.Conflicts != null && evidencePack.Conflicts.Count > 0)
            {
                foreach (var conflict in evidencePack.Conflicts)
                {
                    // If draft failed to mention the discrepancy, record a flagged contradiction
                    bool mentionsConflict = draftAnswer.Contains("discrepancy", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains(conflict.ValueB, StringComparison.OrdinalIgnoreCase);

                    if (!mentionsConflict && conflict.IsCritical)
                    {
                        contradictions.Add($"Critical Discrepancy in [{conflict.FieldName}]: Internal record has '{conflict.ValueA}' but e-Courts has '{conflict.ValueB}'. Draft answer did not explicitly caution the user.");
                    }
                }
            }

            result.VerifiedClaims = verifiedCount;
            result.UnsupportedClaims = unsupportedCount;
            result.Claims = claims;
            result.FlaggedContradictions = contradictions;
            result.IsGrounded = unsupportedCount == 0 && contradictions.Count == 0;

            // If critical contradictions were unaddressed in the draft, prepend an authoritative disclaimer banner
            if (contradictions.Count > 0)
            {
                var bannerSb = new StringBuilder();
                bannerSb.AppendLine("> [!WARNING]");
                bannerSb.AppendLine("> **MANDATORY VERIFICATION ALERT — SOURCE CONFLICT DETECTED**");
                foreach (var c in contradictions)
                {
                    bannerSb.AppendLine($"> - {c}");
                }
                bannerSb.AppendLine();
                result.VerifiedAnswer = bannerSb.ToString() + result.VerifiedAnswer;
            }

            // If unsupported claims exceed 35% of material claims, append a conservative caveat
            if (unsupportedCount > 0 && ((double)unsupportedCount / Math.Max(1, verifiedCount + unsupportedCount)) > 0.35)
            {
                var caveatSb = new StringBuilder();
                caveatSb.AppendLine();
                caveatSb.AppendLine("---");
                caveatSb.AppendLine("*Notice: Certain specific figures or assertions in the reasoning above could not be 100% matched to verified case exhibits on disk. Please cross-verify with official court records before relying in judicial proceedings.*");
                result.VerifiedAnswer += caveatSb.ToString();
            }

            _logger.LogInformation("Post-generation verification complete. Verified: {Verified}, Unsupported: {Unsupported}, Contradictions: {Contradictions}",
                verifiedCount, unsupportedCount, contradictions.Count);

            return Task.FromResult(result);
        }

        private static bool VerifySentenceAgainstEvidence(string sentence, string evidenceCorpus, EvidencePack pack)
        {
            // 1. Extract significant keywords
            var words = sentence.Split(new[] { ' ', ',', ';', ':', '(', ')', '[', ']', '"' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length >= 4 && !IsStopword(w))
                                .ToList();

            if (words.Count == 0) return true;

            int matchCount = 0;
            foreach (var word in words)
            {
                if (evidenceCorpus.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
            }

            double ratio = (double)matchCount / words.Count;
            return ratio >= 0.35; // At least 35% of content words exist in verified evidence
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            var rawSentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-Z0-9])|\n+");
            foreach (var s in rawSentences)
            {
                string clean = s.Trim();
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    list.Add(clean);
                }
            }
            return list;
        }

        private static bool IsStopword(string word)
        {
            var stops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "that", "this", "with", "from", "have", "been", "were", "what",
                "which", "their", "there", "about", "would", "could", "should",
                "these", "those", "under", "after", "before", "during", "court",
                "honourable", "corporation", "claimant", "petitioner", "respondent"
            };
            return stops.Contains(word);
        }
    }
}
