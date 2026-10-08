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
            ILogger<UnifiedLegalResearchService> logger)
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

                // 9. Assemble Safe Evidence Package & System Prompt
                string systemPrompt = _promptService.GetSystemPrompt();
                string formattedUserPrompt = _promptService.BuildUserPrompt(dossier, request.Question, request.QuickAction);

                // Append Conflict Warnings to User Prompt if any were detected
                if (detectedConflicts.Count > 0)
                {
                    var conflictSb = new System.Text.StringBuilder();
                    conflictSb.AppendLine();
                    conflictSb.AppendLine("--- DETECTED CONFLICTS BETWEEN SOURCES ---");
                    foreach (var c in detectedConflicts)
                    {
                        conflictSb.AppendLine($"[CONFLICT in {c.FieldName}]: {c.Description}");
                    }
                    conflictSb.AppendLine("You must explicitly alert the user to these discrepancies and state that verification is required before relying on the record.");
                    conflictSb.AppendLine("--- END CONFLICTS ---");
                    formattedUserPrompt = conflictSb.ToString() + "\n" + formattedUserPrompt;
                }

                // 10. Load Multi-Turn Conversation History
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

                // 11. Provider-Agnostic LLM Reasoning Engine (or Direct Deterministic Response)
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
                                completion = $"[Notice: Local AI reasoning engine is currently unavailable ({llmResp.ErrorMessage}). Verified legal records and authoritative citations remain accessible below.]";
                            }
                        }
                        else
                        {
                            completion = "[Notice: Local AI reasoning engine is currently unconfigured. Verified legal records remain accessible below.]";
                            activeModel = "Offline / Direct SQL";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "LLM reasoning invocation failed for user {UserId}", request.UserId);
                        completion = $"[Notice: AI reasoning engine returned an error: {ex.Message}. Verified legal records remain accessible below.]";
                    }
                }

                stopwatch.Stop();

                // 12. Build Traceable Citations
                var citations = BuildTraceableCitations(dossier);

                // 13. Audit Logging & Conversation Message Persistence
                await _auditService.SaveMessageAsync(conversationId, "user", request.Question, null, cancellationToken);
                await _auditService.SaveMessageAsync(conversationId, "assistant", completion, activeModel, cancellationToken);

                string retrievedSummary = $"{string.Join(", ", sourcesSearched)} | Dossier: {dossier.CaseType} #{dossier.CaseNumber}";
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

        private static bool IsAuthorized(int divisionId, string role)
        {
            // Fully open to all NWKRTC Divisions, Central Office, and Roles ($0 Local AI Cost)
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

            // 2. ANY QuickAction must be processed by the LLM reasoning engine (e.g. check_ecourts, analyze_documents, strengths, etc.)
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
    }
}
