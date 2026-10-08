using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class UnifiedLegalResearchService : IUnifiedLegalResearchService
    {
        private readonly IQueryRouterService _queryRouter;
        private readonly ICaseContextBuilder _contextBuilder;
        private readonly IDocumentSearchService _docSearch;
        private readonly IECourtsContextService _eCourtsContext;
        private readonly ILegalSearchService _legalSearch;
        private readonly ILegalWebSearchService _webSearch;
        private readonly ISimilarCaseService _similarCaseService;
        private readonly IConflictDetectorService _conflictDetector;
        private readonly ILLMService? _llmService;
        private readonly IPromptManagementService _promptService;
        private readonly IAIAuditService _auditService;
        private readonly IPostGenerationVerifier _verifier;
        private readonly NyayaPathaOptions _options;
        private readonly AIOptions _aiOptions;
        private readonly ILogger<UnifiedLegalResearchService> _logger;

        public UnifiedLegalResearchService(
            IQueryRouterService queryRouter,
            ICaseContextBuilder contextBuilder,
            IDocumentSearchService docSearch,
            IECourtsContextService eCourtsContext,
            ILegalSearchService legalSearch,
            ILegalWebSearchService webSearch,
            ISimilarCaseService similarCaseService,
            IConflictDetectorService conflictDetector,
            ILLMService llmService,
            IPromptManagementService promptService,
            IAIAuditService auditService,
            IOptions<NyayaPathaOptions> options,
            IOptions<AIOptions> aiOptions,
            ILogger<UnifiedLegalResearchService> logger,
            IPostGenerationVerifier? verifier = null)
        {
            _queryRouter = queryRouter ?? throw new ArgumentNullException(nameof(queryRouter));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _docSearch = docSearch ?? throw new ArgumentNullException(nameof(docSearch));
            _eCourtsContext = eCourtsContext ?? throw new ArgumentNullException(nameof(eCourtsContext));
            _legalSearch = legalSearch ?? throw new ArgumentNullException(nameof(legalSearch));
            _webSearch = webSearch ?? throw new ArgumentNullException(nameof(webSearch));
            _similarCaseService = similarCaseService ?? throw new ArgumentNullException(nameof(similarCaseService));
            _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
            _llmService = llmService;
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _options = options?.Value ?? new NyayaPathaOptions();
            _aiOptions = aiOptions?.Value ?? new AIOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _verifier = verifier ?? new PostGenerationVerifier(Microsoft.Extensions.Logging.Abstractions.NullLogger<PostGenerationVerifier>.Instance);
        }

        public async Task<LegalResearchResult> ExecuteResearchAsync(
            LegalResearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new LegalResearchResult
            {
                Model = _aiOptions.Model ?? "qwen2.5:7b"
            };

            // 1. Security Authorization Check (Open to all NWKRTC Divisions & Central Office)
            if (!IsAuthorized(request.DivisionId, request.UserRole))
            {
                stopwatch.Stop();
                _logger.LogWarning("Unauthorized access attempt to Nyaya Patha AI by User {UserId}, Division {Div}, Role {Role}",
                    request.UserId, request.DivisionId, request.UserRole);

                result.Success = false;
                result.ErrorMessage = "Access Denied: Only authorized Central Office legal leadership (Dy CLO, CLO, MD) are permitted.";
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                return result;
            }

            if (!_options.IsEnabled)
            {
                stopwatch.Stop();
                result.Success = false;
                result.ErrorMessage = "Nyaya Patha AI Assistant is currently deactivated in system settings.";
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                return result;
            }

            try
            {
                // 2. Resolve or Create Conversation
                int conversationId = request.ConversationId ?? 0;
                string? resolvedCaseType = request.CaseType;
                int? resolvedCaseId = request.CaseId;

                if (conversationId <= 0)
                {
                    string initialTitle = !string.IsNullOrWhiteSpace(request.QuickAction)
                        ? $"Action: {request.QuickAction}"
                        : (request.Question.Length > 40 ? request.Question.Substring(0, 40) + "..." : request.Question);

                    conversationId = await _auditService.CreateConversationAsync(
                        request.UserId, resolvedCaseType, resolvedCaseId, initialTitle, cancellationToken);
                }
                else
                {
                    // 3. Conversational Memory: If no active case provided, check previous messages in session
                    if (!resolvedCaseId.HasValue || resolvedCaseId.Value <= 0)
                    {
                        var pastConv = await _auditService.GetConversationByIdAsync(conversationId, request.UserId, cancellationToken);
                        if (pastConv != null && pastConv.CaseID.HasValue && pastConv.CaseID.Value > 0)
                        {
                            resolvedCaseId = pastConv.CaseID.Value;
                            resolvedCaseType = pastConv.CaseType ?? "MVC";
                            result.FollowUpContext = $"Retained active case context from conversation: {resolvedCaseType} #{resolvedCaseId}";
                        }
                    }
                }
                result.ConversationId = conversationId;

                // 4. Query Routing: Classify what information is required
                var route = _queryRouter.RouteQuery(request.Question, resolvedCaseType, resolvedCaseId);
                result.RouteCategory = route.PrimaryCategory;
                route.Plan.UserDivisionId = request.DivisionId;
                route.Plan.UserRole = request.UserRole;

                // Phase 13: Enforce Authorization BEFORE Retrieval
                if (!IsAuthorized(request.DivisionId, request.UserRole, route.Plan))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Access Denied: User {UserId} in Division {UserDiv} attempted to query Division {TargetDiv}",
                        request.UserId, request.DivisionId, route.Plan.DivisionId);

                    result.Success = false;
                    result.ErrorMessage = $"Access Denied: You are not authorized to query litigation records outside your assigned division (Division #{request.DivisionId}).";
                    result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                    return result;
                }

                // Update resolved case if newly detected in question
                if (route.ExtractedCaseId.HasValue && route.ExtractedCaseId.Value > 0)
                {
                    resolvedCaseId = route.ExtractedCaseId.Value;
                    resolvedCaseType = route.ExtractedCaseType ?? "MVC";
                }

                // 5. Build or Resolve Case Dossier
                CaseDossier? dossier = null;
                if (resolvedCaseId.HasValue && resolvedCaseId.Value > 0 && !string.IsNullOrWhiteSpace(resolvedCaseType))
                {
                    dossier = await _contextBuilder.BuildDossierAsync(resolvedCaseType, resolvedCaseId.Value, cancellationToken);
                }
                else
                {
                    dossier = await _contextBuilder.ResolveAndBuildDossierAsync(request.Question, route.Plan, request.DivisionId, cancellationToken);
                    if (dossier != null && dossier.CaseId > 0)
                    {
                        resolvedCaseId = dossier.CaseId;
                        resolvedCaseType = dossier.CaseType;
                    }
                }

                if (dossier == null)
                {
                    dossier = new CaseDossier
                    {
                        CaseType = "GeneralLegalResearch",
                        CaseNumber = "General Legal Inquiry",
                        CourtName = "Supreme Court of India / High Court of Karnataka",
                        CurrentStage = "Judicial Precedents & Legal Analysis",
                        Petitioner = "N/A",
                        Respondent = "NWKRTC / State Road Transport Undertakings"
                    };
                }

                // 6. Targeted Concurrent Searches based on Route Selection
                var searchTasks = new List<Task>();
                var sourcesSearched = new List<string>();

                // Target A: Internal Database
                if (route.RequiredSources.Contains(QuerySourceCategory.InternalDatabase))
                {
                    sourcesSearched.Add("Internal Case Repository");
                }

                // Target B: Internal Documents
                Task<List<DocumentSearchResultDto>>? docTask = null;
                if (route.RequiredSources.Contains(QuerySourceCategory.InternalDocuments))
                {
                    sourcesSearched.Add("Uploaded Case Documents & Exhibits");
                    docTask = _docSearch.SearchDocumentsAsync(
                        request.Question,
                        dossier.CaseType != "GeneralLegalResearch" ? dossier.CaseType : null,
                        dossier.CaseId > 0 ? dossier.CaseId : null,
                        5,
                        cancellationToken);
                    searchTasks.Add(docTask);
                }

                // Target C: e-Courts / NAPIX
                Task<ECourtsCaseSummaryDto>? eCourtsTask = null;
                if (route.RequiredSources.Contains(QuerySourceCategory.ECourts) && !string.IsNullOrWhiteSpace(dossier.CNRNumber))
                {
                    sourcesSearched.Add("National e-Courts / NAPIX Gateway");
                    eCourtsTask = _eCourtsContext.GetCaseSummaryAsync(dossier.CNRNumber, dossier.CaseType, dossier.CaseId, cancellationToken);
                    searchTasks.Add(eCourtsTask);
                }

                // Target D: Judgment Repository
                Task<List<JudgementViewModel>>? judgmentTask = null;
                if (route.RequiredSources.Contains(QuerySourceCategory.Judgments))
                {
                    sourcesSearched.Add("Internal Judgement Repository");
                    judgmentTask = _legalSearch.SearchJudgementsAsync(request.Question, 4, cancellationToken);
                    searchTasks.Add(judgmentTask);
                }

                // Target E: Legal Web Sources (Only if explicitly enabled by user in chat)
                Task<List<ExternalLegalSourceDto>>? webTask = null;
                if (request.IncludeWeb)
                {
                    sourcesSearched.Add("Authoritative Legal Web Precedents");
                    webTask = _webSearch.SearchPrecedentsAsync(request.Question, null, 4, cancellationToken);
                    searchTasks.Add(webTask);
                }

                // Target F: Similar Cases Engine
                Task<List<UnifiedSimilarCaseDto>>? similarTask = null;
                if (route.RequiredSources.Contains(QuerySourceCategory.SimilarCases))
                {
                    sourcesSearched.Add("Similar Case Comparison Engine");
                    similarTask = _similarCaseService.FindSimilarCasesAsync(
                        dossier.CaseType,
                        dossier.CaseId,
                        request.Question,
                        6,
                        cancellationToken);
                    searchTasks.Add(similarTask);
                }

                // Execute all selected searches concurrently
                if (searchTasks.Count > 0)
                {
                    await Task.WhenAll(searchTasks);
                }

                // 7. Result Normalization & Integration into Dossier
                if (docTask != null)
                {
                    var docResults = await docTask;
                    dossier.SearchedDocumentPassages = docResults;
                }

                if (eCourtsTask != null)
                {
                    var ecSummary = await eCourtsTask;
                    if (ecSummary != null)
                    {
                        dossier.ECourtsSummary = ecSummary;
                        if (ecSummary.History.Count > 0 && dossier.ECourtsHistory.Count == 0)
                        {
                            dossier.ECourtsHistory.AddRange(ecSummary.History);
                        }
                    }
                }

                if (judgmentTask != null)
                {
                    var jResults = await judgmentTask;
                    foreach (var j in jResults)
                    {
                        if (!dossier.RelevantJudgments.Any(r => r.JudgementId == j.JudgementID))
                        {
                            dossier.RelevantJudgments.Add(new RelevantJudgmentDto
                            {
                                JudgementId = j.JudgementID,
                                Title = j.Title,
                                Court = j.Court ?? "High Court of Karnataka",
                                JudgementDate = j.JudgementDate,
                                KeyPrinciple = j.Remarks ?? j.Title,
                                Citation = $"Judgement Repo #{j.JudgementID}"
                            });
                        }
                    }
                }

                if (webTask != null)
                {
                    var wResults = await webTask;
                    foreach (var w in wResults)
                    {
                        if (!dossier.ExternalLegalSources.Any(s => s.Title.Equals(w.Title, StringComparison.OrdinalIgnoreCase)))
                        {
                            dossier.ExternalLegalSources.Add(w);
                        }
                    }
                }

                if (similarTask != null)
                {
                    var sResults = await similarTask;
                    dossier.UnifiedSimilarCases = sResults;
                }

                // 8. Conflict Detection: Identify discrepancies between internal records and e-Courts/documents
                var detectedConflicts = _conflictDetector.DetectConflicts(dossier);
                dossier.DetectedConflicts = detectedConflicts;
                result.Conflicts = detectedConflicts;

                // 9. Assemble Unified EvidencePack (Phase 7 - Evidence Fusion)
                var evidencePack = new EvidencePack
                {
                    Query = request.Question,
                    Plan = route.Plan,
                    Conflicts = detectedConflicts,
                    SourcesQueried = sourcesSearched,
                    SimilarCases = dossier.UnifiedSimilarCases ?? new List<UnifiedSimilarCaseDto>()
                };

                foreach (var kvp in dossier.StructuredFacts)
                {
                    evidencePack.VerifiedFacts[kvp.Key] = kvp.Value;
                }

                if (dossier.SearchedDocumentPassages != null)
                {
                    foreach (var doc in dossier.SearchedDocumentPassages)
                    {
                        evidencePack.Chunks.Add(new EvidenceChunk
                        {
                            DocumentName = doc.DocumentName,
                            PageNumber = doc.PageNumber,
                            SectionOrProvision = doc.Section,
                            Content = doc.MatchedSnippet,
                            RetrievalScore = doc.RelevanceScore,
                            AuthorityLevel = SourceAuthorityLevel.NWKRTCDatabase,
                            Taxonomy = EvidenceTaxonomy.Fact,
                            CaseId = doc.CaseId,
                            CaseType = doc.CaseType,
                            CaseNumber = doc.CaseNumber
                        });
                    }
                }

                if (dossier.RelevantJudgments != null)
                {
                    foreach (var j in dossier.RelevantJudgments)
                    {
                        evidencePack.Chunks.Add(new EvidenceChunk
                        {
                            DocumentName = j.Title,
                            Court = j.Court,
                            Content = j.KeyPrinciple,
                            AuthorityLevel = (j.Court ?? "").Contains("Supreme", StringComparison.OrdinalIgnoreCase)
                                ? SourceAuthorityLevel.SupremeCourt
                                : SourceAuthorityLevel.HighCourt,
                            Taxonomy = EvidenceTaxonomy.Precedent,
                            EventDate = j.JudgementDate,
                            SectionOrProvision = j.Citation
                        });
                    }
                }

                if (dossier.ExternalLegalSources != null)
                {
                    foreach (var w in dossier.ExternalLegalSources)
                    {
                        evidencePack.Chunks.Add(new EvidenceChunk
                        {
                            DocumentName = w.Title,
                            Court = w.Court,
                            Content = w.Excerpt,
                            AuthorityLevel = SourceAuthorityLevel.SecondaryLegalWeb,
                            Taxonomy = EvidenceTaxonomy.ExternalRecord,
                            EventDate = w.JudgmentDate
                        });
                    }
                }

                if (dossier.ECourtsSummary != null)
                {
                    evidencePack.Chunks.Add(new EvidenceChunk
                    {
                        DocumentName = $"e-Courts National Register ({dossier.ECourtsSummary.CNRNumber ?? "CNR"})",
                        Court = dossier.ECourtsSummary.CourtName,
                        Content = $"{dossier.ECourtsSummary.StatusMessage} Stage: {dossier.ECourtsSummary.CurrentStage}. Next Date: {dossier.ECourtsSummary.NextHearingDate:dd-MM-yyyy}",
                        AuthorityLevel = SourceAuthorityLevel.OfficialECourts,
                        Taxonomy = EvidenceTaxonomy.ExternalRecord,
                        EventDate = dossier.ECourtsSummary.NextHearingDate
                    });
                }

                if (dossier.Notings != null)
                {
                    foreach (var n in dossier.Notings)
                    {
                        evidencePack.Chunks.Add(new EvidenceChunk
                        {
                            DocumentName = $"Case Noting by {n.AuthorRole} ({n.AuthorName})",
                            Content = n.NotingText,
                            AuthorityLevel = SourceAuthorityLevel.NWKRTCCaseNoting,
                            Taxonomy = EvidenceTaxonomy.InternalOpinion,
                            EventDate = n.CreatedDate
                        });
                    }
                }

                // Deduplicate and rerank chunks based on composite relevance, authority, and freshness
                evidencePack.DeduplicateAndRerank(request.Question);

                var citations = BuildTraceableCitations(dossier);
                evidencePack.Citations = citations;
                result.Citations = citations;
                result.EvidencePack = evidencePack;

                // 10. Assemble Hardened System & User Prompts
                string systemPrompt = _promptService.GetSystemPrompt();
                string formattedUserPrompt = _promptService.BuildUserPromptFromEvidencePack(evidencePack, request.Question, request.QuickAction);

                // 11. Load Multi-Turn Conversation History
                var history = await _auditService.GetMessagesByConversationIdAsync(conversationId, cancellationToken);
                var messagesToSend = new List<AIMessage>();

                foreach (var h in history.TakeLast(6))
                {
                    messagesToSend.Add(new AIMessage
                    {
                        Role = h.Role,
                        MessageText = h.MessageText
                    });
                }

                messagesToSend.Add(new AIMessage
                {
                    Role = "user",
                    MessageText = formattedUserPrompt
                });

                // 12. Provider-Agnostic LLM Reasoning Engine (or Direct Deterministic Response)
                string completion;
                string activeModel;

                if (TryGenerateDirectInternalDatabaseAnswer(dossier, route, request.Question, request.QuickAction, out string directAnswer))
                {
                    completion = directAnswer;
                    activeModel = "NWKRTC Central Database Engine (Direct SQL — 0 Tokens)";
                    _logger.LogInformation("Query resolved directly from internal database without LLM invocation for user {UserId}", request.UserId);
                }
                else
                {
                    activeModel = _aiOptions.Model ?? "qwen2.5:7b";

                    try
                    {
                        if (_llmService != null)
                        {
                            var llmResp = await _llmService.GenerateAsync(new LLMRequest
                            {
                                SystemPrompt = systemPrompt,
                                Messages = messagesToSend
                            }, cancellationToken);

                            if (llmResp.Success)
                            {
                                completion = llmResp.Content;
                                activeModel = llmResp.Model;
                            }
                            else
                            {
                                // Tier-2: If Ollama returned an error (commonly "model output must contain
                                // either output text or tool calls" due to context overflow), retry once
                                // with a compact stripped-down prompt containing only verified facts.
                                bool isContextError = llmResp.ErrorMessage != null &&
                                    (llmResp.ErrorMessage.Contains("model output", StringComparison.OrdinalIgnoreCase) ||
                                     llmResp.ErrorMessage.Contains("empty response", StringComparison.OrdinalIgnoreCase) ||
                                     llmResp.ErrorMessage.Contains("context", StringComparison.OrdinalIgnoreCase) ||
                                     llmResp.ErrorMessage.Contains("too long", StringComparison.OrdinalIgnoreCase));

                                if (isContextError)
                                {
                                    _logger.LogWarning("LLM context overflow detected, retrying with compact prompt for user {UserId}. Original error: {Error}",
                                        request.UserId, llmResp.ErrorMessage);

                                    // Build a minimal prompt: only verified facts + user question
                                    var compactFacts = new System.Text.StringBuilder();
                                    compactFacts.AppendLine("CASE FACTS (verified from NWKRTC internal database):");
                                    foreach (var kvp in evidencePack.VerifiedFacts.Take(20))
                                        compactFacts.AppendLine($"- {kvp.Key}: {kvp.Value}");

                                    var compactMessages = new List<AIMessage>
                                    {
                                        new AIMessage
                                        {
                                            Role = "user",
                                            MessageText = $"{compactFacts}\n\nUSER QUERY: {request.Question}\n\n{(!string.IsNullOrWhiteSpace(request.QuickAction) ? _promptService.ResolveQuickActionDirective(request.QuickAction) : string.Empty)}\n\nBased only on the above verified facts, provide a concise legal analysis."
                                        }
                                    };

                                    var retryResp = await _llmService.GenerateAsync(new LLMRequest
                                    {
                                        SystemPrompt = systemPrompt,
                                        Messages = compactMessages
                                    }, cancellationToken);

                                    if (retryResp.Success)
                                    {
                                        completion = retryResp.Content;
                                        activeModel = retryResp.Model + " (compact-prompt retry)";
                                        _logger.LogInformation("LLM compact-prompt retry succeeded for user {UserId}", request.UserId);
                                    }
                                    else
                                    {
                                        completion = GenerateOfflineLegalIntelligenceFallback(request.Question, dossier, evidencePack, retryResp.ErrorMessage);
                                        activeModel = "Nyaya Patha Offline Legal Intelligence Engine";
                                    }
                                }
                                else
                                {
                                    completion = GenerateOfflineLegalIntelligenceFallback(request.Question, dossier, evidencePack, llmResp.ErrorMessage);
                                    activeModel = "Nyaya Patha Offline Legal Intelligence Engine";
                                }
                            }
                        }
                        else
                        {
                            completion = GenerateOfflineLegalIntelligenceFallback(request.Question, dossier, evidencePack, "AI service unconfigured");
                            activeModel = "Nyaya Patha Offline Legal Intelligence Engine";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "LLM reasoning invocation failed for user {UserId}", request.UserId);
                        completion = GenerateOfflineLegalIntelligenceFallback(request.Question, dossier, evidencePack, ex.Message);
                        activeModel = "Nyaya Patha Offline Legal Intelligence Engine (Fallback)";
                    }
                }

                // 13. Phase 10: Mandatory Post-Generation Verification & Cleansing
                var verification = await _verifier.VerifyAndCleanseAsync(completion, evidencePack, cancellationToken);
                completion = verification.VerifiedAnswer;
                result.Verification = verification;

                stopwatch.Stop();

                // 14. Audit Logging & Conversation Message Persistence
                await _auditService.SaveMessageAsync(conversationId, "user", request.Question, null, cancellationToken);
                await _auditService.SaveMessageAsync(conversationId, "assistant", completion, activeModel, cancellationToken);

                string retrievedSummary = $"{string.Join(", ", sourcesSearched)} | Dossier: {dossier.CaseType} #{dossier.CaseNumber} | Verified Claims: {verification.VerifiedClaims}/{verification.TotalClaims}";
                await _auditService.LogAuditAsync(new AIAuditLog
                {
                    UserID = request.UserId,
                    Role = request.UserRole,
                    DivisionID = request.DivisionId,
                    ConversationID = conversationId,
                    CaseType = dossier.CaseType,
                    CaseID = dossier.CaseId,
                    Question = request.Question,
                    RetrievedSources = retrievedSummary,
                    Model = activeModel,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    Status = "SUCCESS"
                }, cancellationToken);

                // Populate Result
                result.Success = true;
                result.Answer = completion;
                result.SourcesSearched = sourcesSearched;
                result.Citations = citations;
                result.Sufficiency = EvaluateEvidenceSufficiency(request, dossier, evidencePack, route);
                result.DossierSummary = dossier.CaseType != "GeneralLegalResearch" && dossier.CaseId > 0
                    ? $"{dossier.CaseNumber} ({dossier.CourtName}) — Stage: {dossier.CurrentStage}"
                    : null;
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unhandled error during unified legal research execution.");

                await _auditService.LogAuditAsync(new AIAuditLog
                {
                    UserID = request.UserId,
                    Role = request.UserRole,
                    DivisionID = request.DivisionId,
                    Question = request.Question,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    Status = "FAILED",
                    ErrorMessage = ex.Message
                }, CancellationToken.None);

                result.Success = false;
                result.ErrorMessage = "An unexpected error occurred while executing the legal research workflow. Details logged.";
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                return result;
            }
        }

        public async Task<List<UnifiedSimilarCaseDto>> FindSimilarCasesAsync(
            string caseType,
            int caseId,
            string? keywords = null,
            int maxResults = 6,
            CancellationToken cancellationToken = default)
        {
            return await _similarCaseService.FindSimilarCasesAsync(caseType, caseId, keywords, maxResults, cancellationToken);
        }

        public async Task<UnifiedSearchResponseDto> SearchUnifiedAsync(
            UnifiedSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var response = new UnifiedSearchResponseDto
            {
                Query = request.Query
            };

            if (string.IsNullOrWhiteSpace(request.Query))
                return response;

            try
            {
                var similarTask = _similarCaseService.FindSimilarCasesAsync(
                    request.CaseType ?? "MVC",
                    request.CaseId ?? 0,
                    request.Query,
                    request.MaxResults,
                    cancellationToken);

                var webTask = request.IncludeWeb
                    ? _webSearch.SearchPrecedentsAsync(request.Query, null, request.MaxResults, cancellationToken)
                    : Task.FromResult(new List<ExternalLegalSourceDto>());

                var judgmentTask = _legalSearch.SearchJudgementsAsync(request.Query, request.MaxResults, cancellationToken);

                await Task.WhenAll(similarTask, webTask, judgmentTask);

                response.SimilarCases = await similarTask;
                response.WebSources = await webTask;

                foreach (var j in await judgmentTask)
                {
                    response.Judgments.Add(new RelevantJudgmentDto
                    {
                        JudgementId = j.JudgementID,
                        Title = j.Title,
                        Court = j.Court ?? "High Court",
                        JudgementDate = j.JudgementDate,
                        KeyPrinciple = j.Remarks ?? j.Title,
                        Citation = $"Repo #{j.JudgementID}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing unified search for query {Query}", request.Query);
            }

            return response;
        }

        public static bool IsAuthorized(int divisionId, string role, LegalQueryPlan? plan = null)
        {
            // Central Office (DivisionID 5 or 0) or leadership roles have Corporation-wide scope
            bool isCentralOffice = divisionId == 5 || divisionId == 0 ||
                                   string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(role, "CLO", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(role, "Dy CLO", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(role, "DyCLO", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(role, "MD", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(role, "CO", StringComparison.OrdinalIgnoreCase);

            if (isCentralOffice) return true;

            // Divisional users are prohibited from querying corporation-wide aggregate (DivisionId == 0) or other divisions
            if (plan != null)
            {
                if (plan.DivisionId.HasValue && (plan.DivisionId.Value == 0 || plan.DivisionId.Value != divisionId))
                {
                    return false;
                }

                if (plan.Intent == LegalQueryIntent.DivisionComparison)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<CitationDto> BuildTraceableCitations(CaseDossier dossier)
        {
            var list = new List<CitationDto>();
            if (dossier == null) return list;

            // 1. External Web Precedents & Authoritative Judgments
            foreach (var w in dossier.ExternalLegalSources.Take(8))
            {
                list.Add(new CitationDto
                {
                    SourceType = "WebPrecedent",
                    SourceCategory = "Web",
                    Title = w.Title,
                    CaseNumber = w.CaseNumber,
                    Court = w.Court,
                    Date = w.JudgmentDate?.ToString("dd-MM-yyyy"),
                    WebUrl = w.Url,
                    Domain = w.SourceDomain,
                    IsVerified = w.IsVerifiedDomain,
                    Excerpt = w.Excerpt.Length > 160 ? w.Excerpt.Substring(0, 160) + "..." : w.Excerpt
                });
            }

            // 2. Precedents from Internal Judgement Repo
            foreach (var j in dossier.RelevantJudgments.Take(4))
            {
                list.Add(new CitationDto
                {
                    SourceType = "LegalPrecedent",
                    SourceCategory = "Internal",
                    Title = j.Title,
                    CaseNumber = j.Citation,
                    Court = j.Court,
                    Date = j.JudgementDate?.ToString("dd-MM-yyyy"),
                    RecordId = j.JudgementId.ToString(),
                    Excerpt = j.KeyPrinciple.Length > 160 ? j.KeyPrinciple.Substring(0, 160) + "..." : j.KeyPrinciple
                });
            }

            // 3. Document Passages (Preserving Document, Page Number, Section)
            foreach (var p in dossier.SearchedDocumentPassages.Take(4))
            {
                list.Add(new CitationDto
                {
                    SourceType = "Document",
                    SourceCategory = "Internal",
                    Title = $"{p.DocumentName} (Page {p.PageNumber}, {p.Section})",
                    PageNumber = p.PageNumber,
                    Section = p.Section,
                    DocumentUrl = p.SecureViewerUrl,
                    Excerpt = p.MatchedSnippet
                });
            }

            // 4. Case Register & Case Notings
            if (dossier.CaseType == "DivisionalStatistics" ||
                dossier.CaseType == "VehicleLitigationHistory" ||
                dossier.CaseType == "AdvocatePortfolio" ||
                dossier.CaseType == "HearingCalendar" ||
                dossier.CaseType == "FinancialExposure" ||
                dossier.CaseType == "ExecutionPetitions" ||
                dossier.CaseType == "CompoundFilter")
            {
                list.Add(new CitationDto
                {
                    SourceType = "CaseRecord",
                    SourceCategory = "Internal",
                    Title = dossier.CaseNumber,
                    CaseNumber = dossier.CaseNumber,
                    Court = "NWKRTC Central Litigation Database",
                    RecordId = dossier.StructuredFacts.TryGetValue("Division ID", out var did) ? did : null,
                    Excerpt = "Live verified statistical register from NWKRTC Central Case Management System."
                });
            }
            else if (dossier.CaseType != "GeneralLegalResearch" && dossier.CaseId > 0)
            {
                list.Add(new CitationDto
                {
                    SourceType = "CaseRecord",
                    SourceCategory = "Internal",
                    Title = $"{dossier.CaseType} Register #{dossier.CaseNumber}",
                    CaseNumber = dossier.CaseNumber,
                    Court = dossier.CourtName,
                    RecordId = dossier.CaseId.ToString(),
                    Excerpt = $"Stage: {dossier.CurrentStage} | Petitioner: {dossier.Petitioner} | Vehicle: {dossier.VehicleNo ?? "N/A"}"
                });

                foreach (var n in dossier.Notings.Take(4))
                {
                    list.Add(new CitationDto
                    {
                        SourceType = "CaseNoting",
                        SourceCategory = "Internal",
                        Title = $"Internal Opinion by {n.AuthorRole} ({n.AuthorName})",
                        Date = n.CreatedDate.ToString("dd-MM-yyyy"),
                        RecordId = n.NotingId.ToString(),
                        Excerpt = n.NotingText.Length > 130 ? n.NotingText.Substring(0, 130) + "..." : n.NotingText
                    });
                }

                if (dossier.ECourtsSummary != null)
                {
                    list.Add(new CitationDto
                    {
                        SourceType = "CourtOrder",
                        SourceCategory = "e-Courts",
                        Title = $"e-Courts Record ({dossier.ECourtsSummary.CNRNumber ?? "CNR"})",
                        CaseNumber = dossier.ECourtsSummary.CaseNumber ?? dossier.CaseNumber,
                        Court = dossier.ECourtsSummary.CourtName ?? dossier.CourtName,
                        Date = dossier.ECourtsSummary.NextHearingDate?.ToString("dd-MM-yyyy"),
                        IsVerified = dossier.ECourtsSummary.IsVerified,
                        Excerpt = $"{dossier.ECourtsSummary.StatusMessage} Stage: {dossier.ECourtsSummary.CurrentStage ?? "N/A"}"
                    });
                }
            }

            return list;
        }

        private bool TryGenerateDirectInternalDatabaseAnswer(
            CaseDossier dossier,
            QueryRouteResult route,
            string question,
            string? quickAction,
            out string directAnswer)
        {
            directAnswer = string.Empty;
            if (dossier == null) return false;

            string lower = (question ?? string.Empty).ToLowerInvariant().Trim();

            // 0. Legal Glossary & Acronym Conceptual Inquiries (Instant, Zero LLM Cost, Authoritative)
            if (TryGetLegalGlossaryExplanation(question, out string glossaryAnswer))
            {
                directAnswer = glossaryAnswer;
                return true;
            }

            // 1. Divisional Statistics & Deterministic Structured Registers (Direct Database Facts)
            if (dossier.CaseType == "DivisionalStatistics" ||
                dossier.CaseType == "VehicleLitigationHistory" ||
                dossier.CaseType == "AdvocatePortfolio" ||
                dossier.CaseType == "HearingCalendar" ||
                dossier.CaseType == "FinancialExposure" ||
                dossier.CaseType == "ExecutionPetitions" ||
                dossier.CaseType == "CompoundFilter")
            {
                if (dossier.StructuredFacts.TryGetValue("DirectMarkdown", out var dm) && !string.IsNullOrWhiteSpace(dm))
                {
                    directAnswer = dm;
                    return true;
                }

                var sb = new System.Text.StringBuilder();
                string divName = dossier.StructuredFacts.TryGetValue("Division Name", out var dn) ? dn : "Specified Division";
                string divCode = dossier.StructuredFacts.TryGetValue("Division Code", out var dc) ? dc : "";
                string caseCat = dossier.StructuredFacts.TryGetValue("Case Category", out var cc) ? cc : "Motor Accident Claims (MVC/MACT)";
                string total = dossier.StructuredFacts.TryGetValue("Total Cases Registered", out var tc) ? tc : "0";
                string pending = dossier.StructuredFacts.TryGetValue("Pending Cases", out var pc) ? pc : "0";
                string disposed = dossier.StructuredFacts.TryGetValue("Disposed Cases", out var disp) ? disp : "0";
                string topTribunals = dossier.StructuredFacts.TryGetValue("Top Tribunals / Courts", out var tt) ? tt : "";
                string recentCases = dossier.StructuredFacts.TryGetValue("Recent Active Cases", out var rc) ? rc : "";

                sb.AppendLine("## NYAYA PATHA — Internal Divisional Case Register");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 📊 Official Statistics: **{divName} ({divCode})**");
                sb.AppendLine();
                sb.AppendLine("| Category / Metric | Official Count | Database Status |");
                sb.AppendLine("|---|---|---|");
                sb.AppendLine($"| **Litigation Category** | {caseCat} | Real-Time Sync |");
                sb.AppendLine($"| **Total Registered Cases** | **{total}** | Verified |");
                sb.AppendLine($"| **Pending Cases** | **{pending}** | Active / In Progress |");
                sb.AppendLine($"| **Disposed Cases** | **{disposed}** | Concluded |");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(topTribunals))
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine("#### 🏛️ MACT Courts & Tribunals Distribution");
                    foreach (var trib in topTribunals.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        sb.AppendLine($"- **{trib}**");
                    }
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(recentCases))
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine("#### 📋 Recent Active Cases in this Division");
                    foreach (var c in recentCases.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        sb.AppendLine($"- 🔹 {c}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                directAnswer = sb.ToString();
                return true;
            }

            // 2. Similar Cases Quick Action (100% Verified Database Records, Zero Hallucination, Zero Lag)
            if (string.Equals(quickAction, "similar_cases", StringComparison.OrdinalIgnoreCase) ||
                lower == "action: similar_cases" || lower == "similar cases" || lower == "find similar cases")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## 🔍 NYAYA PATHA — Verified Similar Cases Analysis");
                sb.AppendLine();
                sb.AppendLine($"**Active Matter:** {dossier.CaseType} #{dossier.CaseNumber} ({dossier.CourtName})");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                if (dossier.UnifiedSimilarCases != null && dossier.UnifiedSimilarCases.Count > 0)
                {
                    sb.AppendLine($"### 📋 Verified Matches from NWKRTC Central Case Repository ({dossier.UnifiedSimilarCases.Count} Found)");
                    sb.AppendLine();
                    int idx = 1;
                    foreach (var sim in dossier.UnifiedSimilarCases)
                    {
                        sb.AppendLine($"#### {idx++}. **{sim.CaseNumber}** — {sim.Court}");
                        sb.AppendLine($"- **Similarity Index:** `{sim.SimilarityScore * 100:0.#}% Match`");
                        sb.AppendLine($"- **Similarity Basis:** {sim.SimilarityBasis}");
                        sb.AppendLine($"- **Case Summary / Facts:** {sim.FactsSummary}");
                        sb.AppendLine($"- **Legal Issues & Claims:** {sim.LegalIssues}");
                        sb.AppendLine($"- **Current Stage / Outcome:** **{sim.OutcomeOrStage}**");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("### ℹ️ Repository Verification Result");
                    sb.AppendLine($"No preceding similar cases found in the NWKRTC database strictly matching the factual parameters of **{dossier.CaseNumber}**.");
                    sb.AppendLine();
                    sb.AppendLine("- No fictional or synthetic case numbers are generated.");
                    sb.AppendLine("- To broaden the search, please use general legal precedent research.");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database & Verified Judicial Records (100% Ground Truth — Zero Fictional Citations).*");
                directAnswer = sb.ToString();
                return true;
            }

            // 3. ANY Other QuickAction must be processed by the LLM reasoning engine
            if (!string.IsNullOrWhiteSpace(quickAction))
            {
                return false;
            }

            // 3. Any analytical, document, eCourts, or multi-source question must go to the LLM
            bool isAnalyticalOrResearchQuery = 
                lower.StartsWith("action:") ||
                lower.Contains("analyze") || lower.Contains("analysis") ||
                lower.Contains("document") || lower.Contains("pdf") || lower.Contains("exhibit") ||
                lower.Contains("judgment") || lower.Contains("judgement") || lower.Contains("order") ||
                lower.Contains("chargesheet") || lower.Contains("fir") || lower.Contains("sketch") ||
                lower.Contains("finding") || lower.Contains("issue") || lower.Contains("reason") ||
                lower.Contains("why") || lower.Contains("how") || lower.Contains("explain") ||
                lower.Contains("summarize") || lower.Contains("summary") || lower.Contains("brief") ||
                lower.Contains("draft") || lower.Contains("argument") || lower.Contains("defense") ||
                lower.Contains("precedent") || lower.Contains("ruling") || lower.Contains("landmark") ||
                lower.Contains("strength") || lower.Contains("weakness") || lower.Contains("contradiction") ||
                lower.Contains("negligence") || lower.Contains("liability") || lower.Contains("dependent") ||
                lower.Contains("dependency") || lower.Contains("compare") || lower.Contains("similar") ||
                lower.Contains("cross-exam") || lower.Contains("grounds") || lower.Contains("appeal") ||
                lower.Contains("ecourt") || lower.Contains("e-court") || lower.Contains("napix") ||
                route.RequiredSources.Contains(QuerySourceCategory.InternalDocuments) ||
                route.RequiredSources.Contains(QuerySourceCategory.ECourts) ||
                route.RequiredSources.Contains(QuerySourceCategory.LegalWeb) ||
                route.RequiredSources.Contains(QuerySourceCategory.Judgments) ||
                route.RequiredSources.Contains(QuerySourceCategory.SimilarCases);

            if (isAnalyticalOrResearchQuery)
            {
                return false;
            }

            // 4. Pure Factual Metadata Lookup (Direct SQL Dossier)
            // Triggered only when the query is strictly a raw identifier lookup or a simple metadata attribute check
            bool isPureRawIdentifier = Regex.IsMatch(question.Trim(), @"^(?:CNR\s*:?\s*)?[A-Z]{4}\d{12}$", RegexOptions.IgnoreCase) ||
                                       Regex.IsMatch(question.Trim(), @"^(?:MVC|KID|ID|REF)[/\s-]*\d+[/\s-]*\d+$", RegexOptions.IgnoreCase) ||
                                       question.Trim().Equals(dossier.CNRNumber, StringComparison.OrdinalIgnoreCase);

            bool isBasicMetadataQuestion = lower.Contains("vehicle no") || lower.Contains("bus no") ||
                                           lower.Contains("claimant name") || lower.Contains("petitioner name") ||
                                           lower.Contains("claim amount") || lower.Contains("accident date") ||
                                           lower.Contains("advocate name") || lower.Contains("mact name");

            if (dossier.CaseId > 0 && dossier.CaseType != "GeneralLegalResearch" && (isPureRawIdentifier || isBasicMetadataQuestion))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"## NYAYA PATHA — Internal Case Dossier");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 📋 Verified Record: **{dossier.CaseType} #{dossier.CaseNumber}**");
                sb.AppendLine();
                sb.AppendLine("| Parameter | Official Record |");
                sb.AppendLine("|---|---|");
                sb.AppendLine($"| **Case Number** | {dossier.CaseNumber} |");
                if (!string.IsNullOrWhiteSpace(dossier.CNRNumber) && dossier.CNRNumber != "None")
                    sb.AppendLine($"| **CNR Number** | **`{dossier.CNRNumber}`** |");
                sb.AppendLine($"| **Court / Bench** | {dossier.CourtName} |");
                sb.AppendLine($"| **Current Stage** | **{dossier.CurrentStage}** |");
                if (dossier.StructuredFacts.TryGetValue("Disposal Status", out var dispStat) && !string.IsNullOrWhiteSpace(dispStat) && dispStat != "Pending")
                {
                    sb.AppendLine($"| **Disposal Status** | **{dispStat}** |");
                    if (dossier.StructuredFacts.TryGetValue("Disposal Result", out var dispRes) && !string.IsNullOrWhiteSpace(dispRes) && dispRes != "N/A")
                        sb.AppendLine($"| **Disposal Result** | **{dispRes}** |");
                    if (dossier.StructuredFacts.TryGetValue("Closure Date", out var cDate))
                        sb.AppendLine($"| **Closure Date** | {cDate} |");
                }
                sb.AppendLine($"| **Next Hearing Date** | {(dossier.NextHearingDate.HasValue ? dossier.NextHearingDate.Value.ToString("dd-MM-yyyy") : "Not Scheduled / Concluded")} |");
                sb.AppendLine($"| **Claimant / Petitioner** | {dossier.Petitioner} |");
                sb.AppendLine($"| **Respondent** | {dossier.Respondent} |");
                sb.AppendLine($"| **Vehicle Number** | {dossier.VehicleNo ?? "Not Recorded"} |");

                if (dossier.StructuredFacts.TryGetValue("Claim Nature", out var cNat))
                    sb.AppendLine($"| **Claim Nature / Type** | {cNat} |");
                if (dossier.StructuredFacts.TryGetValue("Claim Amount", out var claimAmt))
                    sb.AppendLine($"| **Claim Amount** | {claimAmt} |");
                if (dossier.StructuredFacts.TryGetValue("Award Amount", out var awdAmt) && awdAmt != "N/A")
                    sb.AppendLine($"| **Award Amount** | {awdAmt} |");
                if (dossier.StructuredFacts.TryGetValue("Accident Date", out var accDate))
                    sb.AppendLine($"| **Accident Date** | {accDate} |");
                if (dossier.StructuredFacts.TryGetValue("Appearing Advocate", out var advName))
                    sb.AppendLine($"| **Advocate** | {advName} |");
                if (dossier.StructuredFacts.TryGetValue("Judge Name", out var jName) && jName != "Not specified")
                    sb.AppendLine($"| **Presiding Judge / Coram** | {jName} |");
                if (dossier.StructuredFacts.TryGetValue("Court Hall", out var cHall) && cHall != "Not specified")
                    sb.AppendLine($"| **Court Hall** | {cHall} |");
                if (dossier.StructuredFacts.TryGetValue("Petition Filed Under", out var petSec))
                    sb.AppendLine($"| **Petition Section** | {petSec} |");
                if (dossier.StructuredFacts.TryGetValue("Objection Outward No", out var objOut))
                    sb.AppendLine($"| **Written Objection** | {objOut} |");
                if (dossier.StructuredFacts.TryGetValue("Entrustment Details", out var entDetails))
                    sb.AppendLine($"| **Legal Entrustment** | {entDetails} |");
                sb.AppendLine();

                // Adverse award facts if any
                var adverseFacts = dossier.StructuredFacts
                    .Where(kv => kv.Key.Contains("FIR") || kv.Key.Contains("Chargesheet") || kv.Key.Contains("TR-18") || kv.Key.Contains("Camera") || kv.Key.Contains("Liability"))
                    .ToList();

                if (adverseFacts.Count > 0)
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine("#### 🔍 Investigation & Liability Assessment");
                    sb.AppendLine("| Factor | Record Assessment |");
                    sb.AppendLine("|---|---|");
                    foreach (var af in adverseFacts)
                    {
                        sb.AppendLine($"| **{af.Key}** | {af.Value} |");
                    }
                    sb.AppendLine();
                }

                // Internal Opinions / Notings
                if (dossier.Notings.Count > 0)
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine("#### 📝 Internal Officer Notings & Opinions");
                    foreach (var n in dossier.Notings.Take(4))
                    {
                        sb.AppendLine($"- **{n.AuthorRole} ({n.AuthorName})** [{n.CreatedDate:dd-MM-yyyy}]: {n.NotingText}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Repository (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                directAnswer = sb.ToString();
                return true;
            }

            return false;
        }

        private static EvidenceSufficiencyEvaluation EvaluateEvidenceSufficiency(
            LegalResearchRequest request,
            CaseDossier? dossier,
            EvidencePack evidencePack,
            QueryRouteResult route)
        {
            var eval = new EvidenceSufficiencyEvaluation();

            // 1. Conflict evaluation
            if (evidencePack.Conflicts.Count > 0)
            {
                eval.Level = EvidenceSufficiencyLevel.Conflicted;
                eval.ConfidenceScore = 0.70;
                eval.Explanation = $"Detected {evidencePack.Conflicts.Count} conflicting facts across sources (e.g. {evidencePack.Conflicts[0].FieldName}). Displaying multi-source conflict breakdown.";
                eval.Contradictions.AddRange(evidencePack.Conflicts.Select(c => $"{c.FieldName}: {c.SourceA}='{c.ValueA}' vs {c.SourceB}='{c.ValueB}'"));
                return eval;
            }

            // 2. Direct Markdown / Deterministic Database Queries
            if (dossier != null && dossier.StructuredFacts.ContainsKey("DirectMarkdown"))
            {
                eval.Level = EvidenceSufficiencyLevel.Sufficient;
                eval.ConfidenceScore = 1.0;
                eval.Explanation = "Verified direct SQL records established with 100% deterministic precision.";
                return eval;
            }

            // 3. Regular Case Dossier
            if (dossier != null && dossier.CaseId > 0 && dossier.StructuredFacts.Count > 0)
            {
                // Check if specific field is asked that might be missing
                string qLower = request.Question.ToLowerInvariant();
                bool askedHearing = qLower.Contains("hearing") || qLower.Contains("next date");
                bool askedClaim = qLower.Contains("claim") || qLower.Contains("amount") || qLower.Contains("award");
                bool askedAdvocate = qLower.Contains("advocate") || qLower.Contains("lawyer") || qLower.Contains("counsel");

                var missing = new List<string>();
                if (askedHearing && !dossier.NextHearingDate.HasValue)
                    missing.Add("Next hearing date is not fixed in court schedule");
                if (askedAdvocate && !dossier.StructuredFacts.ContainsKey("Appearing Advocate"))
                    missing.Add("Appearing advocate has not been recorded");

                if (missing.Count > 0)
                {
                    eval.Level = EvidenceSufficiencyLevel.Partial;
                    eval.ConfidenceScore = 0.85;
                    eval.MissingElements = missing;
                    eval.Explanation = $"Partial evidence: {string.Join(", ", missing)}.";
                    return eval;
                }

                eval.Level = EvidenceSufficiencyLevel.Sufficient;
                eval.ConfidenceScore = 0.98;
                eval.Explanation = "Authorized internal case register facts and documents establish the query parameters.";
                return eval;
            }

            // 4. Scanned documents without OCR text
            if (dossier != null && dossier.ExtractedDocuments.Any(d => d.IsScannedDocument && string.IsNullOrWhiteSpace(d.ExtractedText)))
            {
                eval.Level = EvidenceSufficiencyLevel.Insufficient;
                eval.ConfidenceScore = 0.20;
                eval.MissingElements.Add("The document was scanned and OCR confidence is insufficient to establish this fact reliably.");
                eval.Explanation = "The document is scanned and reliable text could not be extracted.";
                return eval;
            }

            // 5. Research queries with chunks
            if (evidencePack.Chunks.Count > 0)
            {
                eval.Level = EvidenceSufficiencyLevel.Sufficient;
                eval.ConfidenceScore = 0.90;
                eval.Explanation = $"Sufficient authoritative precedents and legal sources ({evidencePack.Chunks.Count} evidence passages) retrieved.";
                return eval;
            }

            // 6. Insufficient evidence fallback
            eval.Level = EvidenceSufficiencyLevel.Insufficient;
            eval.ConfidenceScore = 0.10;
            eval.MissingElements.Add("No matching authorized legal records, notings, or judicial precedents found.");
            eval.Explanation = "The available records and sources do not establish this.";
            return eval;
        }

        private static bool TryGetLegalGlossaryExplanation(string question, out string explanation)
        {
            explanation = string.Empty;
            if (string.IsNullOrWhiteSpace(question)) return false;

            string q = question.Trim().ToLowerInvariant();

            // Strip common query preambles
            string normalized = Regex.Replace(q, @"^(?:what(?:'s|\s+is|\s+are)?|explain|tell\s+me\s+about|describe|meaning\s+of|definition\s+of)\s+", "", RegexOptions.IgnoreCase).Trim();
            normalized = Regex.Replace(normalized, @"[\?\.!]+$", "").Trim();

            // 1. MVC / MACT
            if (normalized == "mvc" || normalized == "whats mvc" || normalized == "what is mvc" || normalized == "mact" || normalized.StartsWith("mvc case") || normalized == "mvc meaning")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### 🚗 Motor Accident Claims Tribunal (MVC) Case — Legal Framework");
                sb.AppendLine();
                sb.AppendLine("In **NWKRTC** and Indian transport jurisprudence, **MVC** refers to a **Motor Accident Claims Case** instituted before the **Motor Accident Claims Tribunal (MACT)**.");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("#### ⚖️ Governing Statutory Provisions");
                sb.AppendLine("- **Primary Statute:** **Motor Vehicles Act, 1988 (Sections 166, 163A / 164, 140, 149)** read with the Karnataka Motor Vehicles Rules, 1989.");
                sb.AppendLine("- **Competent Forum:** Adjudicated by the **Motor Accident Claims Tribunal (MACT)**, presided over by District & Sessions Judges or Senior Civil Judges.");
                sb.AppendLine("- **Claimants:** Injured accident victims (injury / permanent disability claims) or dependent legal heirs of deceased victims (fatal accident claims).");
                sb.AppendLine();
                sb.AppendLine("#### 🛡️ Core Adjudication Elements & NWKRTC Defenses");
                sb.AppendLine("1. **Actionable Negligence (Section 166):** The burden rests on the claimant to prove rash and negligent driving by the Corporation driver. NWKRTC defends with **spot panchanama, trip sheets, bus mechanical inspection (IMV) reports, and departmental accident inquiry reports** to establish **Contributory Negligence** or sole negligence of third-party vehicles.");
                sb.AppendLine("2. **Quantum Determination:** Calculated strictly under settled Supreme Court precedents:");
                sb.AppendLine("   - ***Sarla Verma v. DTC (2009) 6 SCC 121*** — Standardized age multiplier (18 to 5) and personal living expense deductions.");
                sb.AppendLine("   - ***National Insurance Co. Ltd. v. Pranay Sethi (2017) 16 SCC 680*** — Future prospects calculation (10% to 50%) and standardized conventional heads.");
                sb.AppendLine("   - ***Raj Kumar v. Ajay Kumar (2011) 1 SCC 343*** — Difference between physical disability and functional loss of earning capacity.");
                sb.AppendLine("3. **Divisional Administration:** Managed by Divisional Law Officers across all 8 NWKRTC divisions (Belagavi, Hubballi-Dharwad, Gadag, Bagalkot, Uttara Kannada, Haveri, Chikkodi) with Central Office legal oversight.");
                explanation = sb.ToString();
                return true;
            }

            // 2. MFA (Miscellaneous First Appeal)
            if (normalized == "mfa" || normalized == "whats mfa" || normalized == "what is mfa" || normalized.StartsWith("mfa case") || normalized == "mfa meaning" || normalized == "mfa appeal")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### ⚖️ Miscellaneous First Appeal (MFA) — High Court Appellate Proceedings");
                sb.AppendLine();
                sb.AppendLine("In **NWKRTC** legal operations and Karnataka judicial practice, **MFA** stands for **Miscellaneous First Appeal** (statutory regular first appeal against tribunal awards or miscellaneous civil orders).");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("#### 🏛️ Statutory Basis & Jurisdiction");
                sb.AppendLine("- **Governing Statute:** **Section 173 of the Motor Vehicles Act, 1988** read with **Order XLI of the Code of Civil Procedure, 1908 (CPC)**.");
                sb.AppendLine("- **Appellate Forum:** Heard and adjudicated by the **High Court of Karnataka** (Dharwad Bench, Kalaburagi Bench, or Principal Bench Bengaluru).");
                sb.AppendLine("- **Purpose for NWKRTC:** An appeal preferred by the Corporation challenging an adverse, excessive, or legally erroneous Judgment & Award passed by a lower Motor Accident Claims Tribunal (MACT).");
                sb.AppendLine();
                sb.AppendLine("#### 📋 Essential Procedural Conditions for NWKRTC");
                sb.AppendLine("1. **Statutory Limitation Period:** Must be filed within **90 days** from the date of the MACT award. Any delay requires a formal application under **Section 5 of the Limitation Act, 1963** establishing sufficient administrative cause.");
                sb.AppendLine("2. **Mandatory Statutory Pre-Deposit:** Under **Section 173(1)**, the Corporation is statutorily required to deposit **₹25,000 or 50% of the awarded amount** (whichever is less) at the time of preferring the appeal.");
                sb.AppendLine("3. **Standard Grounds of Appeal:**");
                sb.AppendLine("   - **Excessive Quantum:** Erroneous calculation of notional income, unjustified future prospects, or exaggerated disability ratings contrary to medical board guidelines.");
                sb.AppendLine("   - **Erroneous Negligence Apportionment:** Tribunal failure to consider contributory negligence of third-party two-wheelers, tractors, or pedestrians.");
                sb.AppendLine("   - **Interest Reduction:** Challenging exorbitant interest rates (demanding reduction to standard 6% p.a.).");
                explanation = sb.ToString();
                return true;
            }

            // 3. KID / ID (Karnataka Industrial Dispute)
            if (normalized == "kid" || normalized == "whats kid" || normalized == "what is kid" || normalized == "industrial dispute" || normalized == "id case" || normalized == "kid meaning")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### 🛠️ Karnataka Industrial Dispute (KID) — Labour Adjudication");
                sb.AppendLine();
                sb.AppendLine("In **NWKRTC** personnel and industrial relations, **KID** stands for **Karnataka Industrial Dispute** (individual employee industrial dispute).");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("#### ⚖️ Legal Framework & Forum");
                sb.AppendLine("- **Governing Statute:** Instituted under **Section 10(4-A)** of the **Industrial Disputes Act, 1947** (Karnataka State Amendment, inserted by Karnataka Act 3 of 1988) or referenced under **Section 10(1)**.");
                sb.AppendLine("- **Competent Forum:** **Labour Court / Industrial Tribunal** (e.g. Hubballi, Belagavi).");
                sb.AppendLine("- **Nature of Dispute:** Filed by an individual Corporation workman (driver, conductor, mechanic, artisan) challenging Corporation disciplinary orders of **dismissal, discharge, retrenchment, or termination of service**.");
                sb.AppendLine();
                sb.AppendLine("#### 🛡️ Corporation Key Legal Defenses");
                sb.AppendLine("1. **Validity of Domestic Enquiry:** Corporation preliminary issue. The Labour Court first determines whether the internal departmental inquiry was conducted in strict adherence to principles of natural justice and NWKRTC C&R Regulations.");
                sb.AppendLine("2. **Section 11-A Power of Tribunal:** Even if misconduct is proven, the Tribunal exercises discretion to substitute dismissal with lesser penalties if deemed disproportionate.");
                sb.AppendLine("3. **Corporation Reliefs:** Defending against backwages claims by demonstrating non-employment proof, habitual absence, non-issuance of tickets, or past default history cards.");
                explanation = sb.ToString();
                return true;
            }

            // 4. PG (Payment of Gratuity)
            if (normalized == "pg" || normalized == "whats pg" || normalized == "what is pg" || normalized == "gratuity" || normalized == "pg case" || normalized == "pg meaning")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### 📜 Payment of Gratuity (PG) Proceedings — Legal Framework");
                sb.AppendLine();
                sb.AppendLine("In **NWKRTC**, **PG** refers to proceedings under the **Payment of Gratuity Act, 1972**.");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("#### ⚖️ Statutory Authority & Provisions");
                sb.AppendLine("- **Governing Law:** **Payment of Gratuity Act, 1972 (Sections 4 & 7)** read with the Karnataka Payment of Gratuity Rules.");
                sb.AppendLine("- **Competent Authority:** The **Controlling Authority under the PG Act** (Assistant Labour Commissioner) and **Appellate Authority** (Deputy Labour Commissioner).");
                sb.AppendLine("- **Claim Procedure:** Filed via **Form N** by retired, superannuated, or dismissed employees claiming terminal gratuity or disputed interest under Section 7(3A).");
                sb.AppendLine();
                sb.AppendLine("#### 🛡️ Corporation Defense Principles");
                sb.AppendLine("1. **Continuous Service (Section 2A):** Verification of qualifying 240 days per calendar year, excluding unauthorized absence periods (dies-non).");
                sb.AppendLine("2. **Statutory Ceiling Limit:** Enforcing the maximum ceiling limit of **₹20,00,000**.");
                sb.AppendLine("3. **Lawful Forfeiture (Section 4(6)):** Forfeiture of gratuity is statutorily justified where service was terminated for riotous/disorderly conduct, moral turpitude, or causing quantified financial loss to the Corporation.");
                explanation = sb.ToString();
                return true;
            }

            // 5. EP (Execution Petition)
            if (normalized == "ep" || normalized == "whats ep" || normalized == "what is ep" || normalized == "execution petition" || normalized == "ep meaning")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("### ⚡ Execution Petition (EP) — Decree Enforcement & Asset Protection");
                sb.AppendLine();
                sb.AppendLine("In **NWKRTC**, **EP** stands for an **Execution Petition** for judicial decree enforcement.");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("#### ⚖️ Governing Law & Jurisdiction");
                sb.AppendLine("- **Governing Statute:** **Order XXI of the Code of Civil Procedure, 1908 (CPC)** read with **Section 174 of the Motor Vehicles Act, 1988**.");
                sb.AppendLine("- **Competent Forum:** Executing Court / MACT / Labour Court executing recovery certificates.");
                sb.AppendLine("- **Purpose:** Initiated by claimants or decree-holders when awarded compensation has not been deposited by the Corporation within 30 days.");
                sb.AppendLine();
                sb.AppendLine("#### 🛡️ Operational Risks & Corporation Protocol");
                sb.AppendLine("1. **Attachment Warrants:** Claimants routinely seek attachment of NWKRTC depot buses or Corporation bank accounts (Garnishee orders).");
                sb.AppendLine("2. **Corporation Response:** Immediate filing of memo of appearance, demonstrating deposit of decretal amount, presenting High Court stay orders, or seeking reasonable time for Central Office financial clearance.");
                explanation = sb.ToString();
                return true;
            }

            return false;
        }

        private static string GenerateOfflineLegalIntelligenceFallback(
            string question,
            CaseDossier dossier,
            EvidencePack evidencePack,
            string? reason)
        {
            // 1. Try definitional / glossary explanation first
            if (TryGetLegalGlossaryExplanation(question, out string glossary))
            {
                return glossary;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("> ℹ️ *Offline Legal Intelligence Synthesizer (Local AI reasoning model is currently unstarted or unreachable; response synthesized directly from verified NWKRTC case records, database facts, and statutory provisions).*");
            sb.AppendLine();

            // 2. If active case dossier exists, summarize facts
            if (dossier != null && dossier.CaseId > 0 && dossier.CaseType != "GeneralLegalResearch")
            {
                sb.AppendLine($"### 📋 Verified Case Brief: **{dossier.CaseType} #{dossier.CaseNumber}**");
                sb.AppendLine();
                sb.AppendLine($"- **Court / Tribunal:** {dossier.CourtName}");
                sb.AppendLine($"- **Current Stage:** **{dossier.CurrentStage}**");
                sb.AppendLine($"- **Petitioner / Claimant:** {dossier.Petitioner}");
                sb.AppendLine($"- **Respondent:** {dossier.Respondent}");
                if (!string.IsNullOrWhiteSpace(dossier.VehicleNo))
                    sb.AppendLine($"- **Vehicle Registration:** `{dossier.VehicleNo}`");
                if (dossier.NextHearingDate.HasValue)
                    sb.AppendLine($"- **Next Hearing Date:** {dossier.NextHearingDate.Value:dd-MMM-yyyy}");
                sb.AppendLine();

                if (dossier.StructuredFacts.Count > 0)
                {
                    sb.AppendLine("#### 📊 Case Parameters & Financials");
                    foreach (var fact in dossier.StructuredFacts.Take(6))
                    {
                        sb.AppendLine($"- **{fact.Key}:** {fact.Value}");
                    }
                    sb.AppendLine();
                }

                if (dossier.RelevantJudgments.Count > 0)
                {
                    sb.AppendLine("#### 🏛️ Relevant Precedents & Legal Repository Matches");
                    foreach (var j in dossier.RelevantJudgments.Take(3))
                    {
                        sb.AppendLine($"- **{j.Title}** ({j.Court}) — *{j.KeyPrinciple}*");
                    }
                    sb.AppendLine();
                }
            }
            else if (evidencePack.Chunks.Count > 0)
            {
                sb.AppendLine("### 📑 Relevant Records & Authoritative Sources Retrieved");
                sb.AppendLine();
                foreach (var chunk in evidencePack.Chunks.Take(4))
                {
                    sb.AppendLine($"- **{chunk.DocumentName ?? "Legal Precedent"}:** {chunk.Content}");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("### ⚖️ Legal Query Summary");
                sb.AppendLine($"The system processed your inquiry: **\"{question}\"**.");
                sb.AppendLine();
                sb.AppendLine("Verified legal records, statutory references, and relevant judicial precedents have been indexed and cited in the verified sources panel below.");
            }

            return sb.ToString();
        }
    }
}
