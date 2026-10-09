using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI;

namespace MVCCaseManagement.Controllers
{
    [Authorize(Policy = "NyayaPathaAIAccess")]
    public class NyayaPathaController : Controller
    {
        private readonly ICaseContextBuilder _contextBuilder;
        private readonly IAIAuditService _auditService;
        private readonly IECourtsContextService _eCourtsContext;
        private readonly ILegalWebSearchService _webSearch;
        private readonly IUnifiedLegalResearchService _unifiedResearch;
        private readonly ISimilarCaseService _similarCaseService;
        private readonly ILLMService _llmService;
        private readonly NyayaPathaOptions _options;
        private readonly AIOptions _aiOptions;
        private readonly ILogger<NyayaPathaController> _logger;

        public NyayaPathaController(
            ICaseContextBuilder contextBuilder,
            IAIAuditService auditService,
            IECourtsContextService eCourtsContext,
            ILegalWebSearchService webSearch,
            IUnifiedLegalResearchService unifiedResearch,
            ISimilarCaseService similarCaseService,
            ILLMService llmService,
            IOptions<NyayaPathaOptions> options,
            IOptions<AIOptions> aiOptions,
            ILogger<NyayaPathaController> logger)
        {
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _eCourtsContext = eCourtsContext ?? throw new ArgumentNullException(nameof(eCourtsContext));
            _webSearch = webSearch ?? throw new ArgumentNullException(nameof(webSearch));
            _unifiedResearch = unifiedResearch ?? throw new ArgumentNullException(nameof(unifiedResearch));
            _similarCaseService = similarCaseService ?? throw new ArgumentNullException(nameof(similarCaseService));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _options = options?.Value ?? new NyayaPathaOptions();
            _aiOptions = aiOptions?.Value ?? new AIOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst("UserID") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            return 0;
        }

        private int GetCurrentDivisionId()
        {
            var claim = User.FindFirst("DivisionID");
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            return 5; // Default Central Office
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Officer";
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Health(CancellationToken cancellationToken)
        {
            if (_llmService != null)
            {
                var health = await _llmService.CheckHealthAsync(cancellationToken);
                int statusCode = health.Status == "Unhealthy" ? 503 : 200;
                return StatusCode(statusCode, health);
            }

            bool enabled = _options.IsEnabled && _aiOptions.Enabled;
            return Ok(new
            {
                status = enabled ? "Healthy" : "Disabled",
                isEnabled = enabled,
                provider = _aiOptions.Provider ?? "Ollama",
                configuredModel = _aiOptions.Model ?? "qwen2.5:14b",
                allowExternalProviders = _aiOptions.AllowExternalProviders,
                message = enabled ? "Nyaya Patha AI operational." : "Nyaya Patha AI is deactivated (Kill switch engaged)."
            });
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? caseType, int? caseId)
        {
            _logger.LogWarning(">>> [NyayaPatha] Index invoked. CaseType={CaseType}, CaseId={CaseId}, User={User}", 
                caseType, caseId, User.Identity?.Name);

            if (!_options.IsEnabled || !_aiOptions.Enabled)
            {
                ViewBag.DisabledMessage = "Nyaya Patha AI Assistant is currently disabled by system administrator.";
                return View("Disabled");
            }

            int userId = GetCurrentUserId();
            _logger.LogWarning(">>> [NyayaPatha] UserId resolved to {UserId}. Loading conversations...", userId);

            List<AIConversation> conversations = new();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                conversations = await _auditService.GetConversationsByUserAsync(userId, top: 5, cts.Token);
                _logger.LogWarning(">>> [NyayaPatha] Conversations loaded. Count={Count}", conversations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ">>> [NyayaPatha] Timed out or failed to load initial AI conversation history for user {UserId}", userId);
            }
            ViewBag.Conversations = conversations;

            CaseDossier? initialDossier = null;
            if (caseId.HasValue && !string.IsNullOrWhiteSpace(caseType))
            {
                _logger.LogWarning(">>> [NyayaPatha] Building initial dossier for {CaseType} #{CaseId}...", caseType, caseId.Value);
                initialDossier = await _contextBuilder.BuildDossierAsync(caseType, caseId.Value, deepEnrich: false);
                _logger.LogWarning(">>> [NyayaPatha] Initial dossier built.");
            }

            ViewBag.InitialDossier = initialDossier;
            ViewBag.UserRole = GetCurrentUserRole();
            ViewBag.ModelName = _aiOptions.Model ?? "qwen2.5:7b";

            _logger.LogWarning(">>> [NyayaPatha] Returning View...");
            return View(initialDossier);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Chat([FromBody] NyayaPathaChatRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (!_options.IsEnabled || !_aiOptions.Enabled)
                {
                    return Json(new NyayaPathaChatResponse
                    {
                        Success = false,
                        ErrorMessage = "Nyaya Patha AI is currently disabled in system settings."
                    });
                }

                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                {
                    return Json(new NyayaPathaChatResponse
                    {
                        Success = false,
                        ErrorMessage = "Question or prompt cannot be empty."
                    });
                }

                if (_aiOptions.MaxInputLength > 0 && request.Message.Length > _aiOptions.MaxInputLength)
                {
                    return Json(new NyayaPathaChatResponse
                    {
                        Success = false,
                        ErrorMessage = $"Question exceeds the maximum allowable length of {_aiOptions.MaxInputLength} characters."
                    });
                }

                int userId = GetCurrentUserId();
                int divisionId = GetCurrentDivisionId();
                string userRole = GetCurrentUserRole();

                var researchResult = await _unifiedResearch.ExecuteResearchAsync(new LegalResearchRequest
                {
                    Question = request.Message,
                    ConversationId = request.ConversationId,
                    CaseType = request.CaseType,
                    CaseId = request.CaseId,
                    QuickAction = request.QuickAction,
                    UserId = userId,
                    DivisionId = divisionId,
                    UserRole = userRole,
                    IncludeWeb = request.IncludeWeb
                }, cancellationToken);

                return Json(new NyayaPathaChatResponse
                {
                    Success = researchResult.Success,
                    ConversationId = researchResult.ConversationId,
                    Reply = researchResult.Answer,
                    Citations = researchResult.Citations,
                    SourcesSearched = researchResult.SourcesSearched,
                    Conflicts = researchResult.Conflicts,
                    RouteCategory = researchResult.RouteCategory,
                    FollowUpContext = researchResult.FollowUpContext,
                    DossierSummary = researchResult.DossierSummary,
                    ErrorMessage = researchResult.ErrorMessage,
                    Model = researchResult.Model,
                    ExecutionTimeMs = researchResult.ExecutionTimeMs,
                    Verification = researchResult.Verification
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in NyayaPatha Chat action.");
                return Json(new NyayaPathaChatResponse
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred while communicating with the AI service: " + ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchCases(string q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Json(new List<CaseSearchItemDto>());

            int userDivisionId = GetCurrentDivisionId();
            var results = await _contextBuilder.SearchCasesAsync(q.Trim(), 15, userDivisionId, cancellationToken);
            return Json(results);
        }

        [HttpGet]
        public async Task<IActionResult> GetDossier(string caseType, int caseId, CancellationToken cancellationToken)
        {
            var dossier = await _contextBuilder.BuildDossierAsync(caseType, caseId, cancellationToken);
            if (dossier == null)
                return NotFound(new { message = "Case record not found in system." });

            int userDivisionId = GetCurrentDivisionId();
            if (userDivisionId != 5 && dossier.DivisionId.HasValue && dossier.DivisionId.Value != userDivisionId)
            {
                _logger.LogWarning("Security Block: User with division {UserDiv} attempted to access dossier for case {CaseType} #{CaseId} in division {CaseDiv}",
                    userDivisionId, caseType, caseId, dossier.DivisionId);
                return Forbid();
            }

            return Json(new
            {
                caseId = dossier.CaseId,
                caseType = dossier.CaseType,
                caseNumber = dossier.CaseNumber,
                court = dossier.CourtName,
                stage = dossier.CurrentStage,
                petitioner = dossier.Petitioner,
                respondent = dossier.Respondent,
                vehicleNo = dossier.VehicleNo,
                cnrNumber = dossier.CNRNumber,
                notingsCount = dossier.Notings.Count,
                documentsCount = dossier.ExtractedDocuments.Count,
                judgmentsCount = dossier.RelevantJudgments.Count,
                similarCasesCount = dossier.SimilarCases.Count,
                provisions = dossier.ApplicableProvisions,
                citations = BuildCitations(dossier)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Conversation(int id, CancellationToken cancellationToken)
        {
            int userId = GetCurrentUserId();
            var conv = await _auditService.GetConversationByIdAsync(id, userId, cancellationToken);
            if (conv == null)
                return NotFound();

            var messages = await _auditService.GetMessagesByConversationIdAsync(id, cancellationToken);
            return Json(new
            {
                conversationId = conv.ConversationID,
                title = conv.Title,
                caseType = conv.CaseType,
                caseId = conv.CaseID,
                messages = messages.Select(m => new
                {
                    id = m.MessageID,
                    role = m.Role,
                    text = m.MessageText,
                    createdAt = m.CreatedAt.ToString("g")
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
        {
            int userId = GetCurrentUserId();
            var convs = await _auditService.GetConversationsByUserAsync(userId, top: 30, cancellationToken);
            return Json(convs.Select(c => new
            {
                conversationId = c.ConversationID,
                title = c.Title,
                caseType = c.CaseType,
                caseId = c.CaseID,
                updatedAt = c.UpdatedAt.ToString("dd MMM yyyy, HH:mm"),
                updatedDateShort = c.UpdatedAt.ToString("dd MMM")
            }));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RenameConversation(int id, string title, CancellationToken cancellationToken)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(title))
                return Json(new { success = false, message = "Invalid parameters." });

            int userId = GetCurrentUserId();
            bool updated = await _auditService.UpdateConversationTitleAsync(id, userId, title, cancellationToken);
            return Json(new { success = updated });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteConversation(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
                return Json(new { success = false, message = "Invalid session identifier." });

            int userId = GetCurrentUserId();
            bool deleted = await _auditService.DeleteConversationAsync(id, userId, cancellationToken);
            return Json(new { success = deleted });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ClearAllConversations(CancellationToken cancellationToken)
        {
            int userId = GetCurrentUserId();
            bool cleared = await _auditService.ClearAllConversationsAsync(userId, cancellationToken);
            return Json(new { success = cleared });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CheckECourts(string caseType, int caseId, CancellationToken cancellationToken)
        {
            if (caseId <= 0) return BadRequest(new { message = "Invalid case ID." });
            var dossier = await _contextBuilder.BuildDossierAsync(caseType, caseId, cancellationToken);
            if (dossier == null) return NotFound(new { message = "Case record not found." });

            var summary = dossier.ECourtsSummary ?? await _eCourtsContext.GetCaseSummaryAsync(dossier.CNRNumber, caseType, caseId, cancellationToken);
            return Json(summary);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SearchSimilarCases(string caseType, int caseId, string? q, CancellationToken cancellationToken)
        {
            var similar = await _similarCaseService.FindSimilarCasesAsync(caseType, caseId, q, 6, cancellationToken);
            return Json(similar);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SearchLatestJudgments(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<ExternalLegalSourceDto>());
            var results = await _webSearch.SearchRecentJudgmentsAsync(query, null, 5, cancellationToken);
            return Json(results);
        }

        private List<CitationDto> BuildCitations(CaseDossier? dossier)
        {
            var list = new List<CitationDto>();
            if (dossier == null) return list;

            // 1. External Web Precedents & Authoritative Judgments (Prioritized for Legal Research)
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

            // 3. If a specific case is loaded, include internal case records and documents
            if (dossier.CaseType != "GeneralLegalResearch" && dossier.CaseId > 0)
            {
                list.Add(new CitationDto
                {
                    SourceType = "CaseRecord",
                    SourceCategory = "Internal",
                    Title = $"{dossier.CaseType} Case Register #{dossier.CaseNumber}",
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
                        Title = $"Internal Noting by {n.AuthorRole} ({n.AuthorName})",
                        Date = n.CreatedDate.ToString("dd-MM-yyyy"),
                        RecordId = n.NotingId.ToString(),
                        Excerpt = n.NotingText.Length > 130 ? n.NotingText.Substring(0, 130) + "..." : n.NotingText
                    });
                }

                foreach (var d in dossier.ExtractedDocuments)
                {
                    list.Add(new CitationDto
                    {
                        SourceType = "Document",
                        SourceCategory = "Internal",
                        Title = d.DocumentName,
                        DocumentUrl = Url.Action("GetUploadFile", "Uploads", new { filePath = d.SourceFilePath }),
                        Excerpt = $"Secure server-extracted document ({d.PageCount} pages verified)."
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
                        Excerpt = $"{dossier.ECourtsSummary.StatusMessage} Stage: {dossier.ECourtsSummary.CurrentStage ?? "N/A"} | Coram: {dossier.ECourtsSummary.JudgeName ?? "N/A"}"
                    });

                    foreach (var o in dossier.ECourtsSummary.Orders.Take(2))
                    {
                        list.Add(new CitationDto
                        {
                            SourceType = o.IsJudgment ? "FinalJudgment" : "CourtOrder",
                            SourceCategory = "e-Courts",
                            Title = $"e-Courts Order #{o.OrderNumber} ({o.OrderType})",
                            Date = o.OrderDate?.ToString("dd-MM-yyyy"),
                            Court = dossier.ECourtsSummary.CourtName,
                            Excerpt = o.Details.Length > 130 ? o.Details.Substring(0, 130) + "..." : o.Details
                        });
                    }
                }
                else if (dossier.ECourtsHistory.Count > 0)
                {
                    var h = dossier.ECourtsHistory.First();
                    list.Add(new CitationDto
                    {
                        SourceType = "CourtOrder",
                        SourceCategory = "e-Courts",
                        Title = $"e-Courts Synced Hearing ({h.Stage})",
                        Date = h.EventDate.ToString("dd-MM-yyyy"),
                        Court = h.CourtHall,
                        Excerpt = h.OrderDetails
                    });
                }
            }

            return list;
        }
    }
}
