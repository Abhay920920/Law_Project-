using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Services.AI
{
    public class ECourtsContextService : IECourtsContextService
    {
        private readonly IECourtsRepository _eCourtsRepo;
        private readonly IECourtsNapixService _napixService;
        private readonly NyayaPathaOptions _options;
        private readonly ILogger<ECourtsContextService> _logger;

        public ECourtsContextService(
            IECourtsRepository eCourtsRepo,
            IECourtsNapixService napixService,
            IOptions<NyayaPathaOptions> options,
            ILogger<ECourtsContextService> logger)
        {
            _eCourtsRepo = eCourtsRepo ?? throw new ArgumentNullException(nameof(eCourtsRepo));
            _napixService = napixService ?? throw new ArgumentNullException(nameof(napixService));
            _options = options?.Value ?? new NyayaPathaOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ECourtsCaseSummaryDto> GetCaseSummaryAsync(
            string? cnrNumber, 
            string caseType, 
            int caseId, 
            CancellationToken cancellationToken = default)
        {
            var summary = new ECourtsCaseSummaryDto
            {
                CNRNumber = cnrNumber?.Trim()
            };

            if (string.IsNullOrWhiteSpace(cnrNumber))
            {
                summary.IsVerified = false;
                summary.StatusMessage = "e-Courts verification unavailable: Case does not have a CNR Number recorded in NWKRTC records.";
                return summary;
            }

            string cleanCnr = cnrNumber.Trim().ToUpperInvariant();
            summary.CNRNumber = cleanCnr;
            bool isHighCourt = cleanCnr.StartsWith("HC", StringComparison.OrdinalIgnoreCase) || 
                               cleanCnr.Contains("HC") ||
                               string.Equals(caseType, "APPEAL", StringComparison.OrdinalIgnoreCase);

            // 1. Check local tracked database first (instant & reliable)
            TrackedCaseModel? localTracked = null;
            try
            {
                localTracked = await _eCourtsRepo.GetTrackedCaseByCnrAsync(cleanCnr);
                if (localTracked != null)
                {
                    summary.CaseNumber = $"{localTracked.CaseTypeCode}/{localTracked.RegNo}/{localTracked.RegYear}";
                    summary.CurrentStage = localTracked.CurrentStage;
                    summary.NextHearingDate = localTracked.NextHearingDate;
                    summary.CourtName = localTracked.CourtNo ?? (localTracked.IsHighCourt ? "High Court of Karnataka" : "District / Subordinate Court");
                    summary.JudgeName = localTracked.JudgeName;
                    summary.Petitioner = localTracked.PetitionerName;
                    summary.Respondent = localTracked.RespondentName;
                    summary.IsVerified = true;
                    summary.StatusMessage = $"Verified from local e-Courts repository (last synced: {localTracked.LastSyncedDate:dd-MM-yyyy}).";

                    summary.History.Add(new ECourtsHistoryItemDto
                    {
                        EventDate = localTracked.NextHearingDate ?? localTracked.LastSyncedDate,
                        Stage = localTracked.CurrentStage ?? "Tracked",
                        CourtHall = localTracked.CourtNo,
                        Judge = localTracked.JudgeName,
                        OrderDetails = $"Locally verified case: {summary.CaseNumber} (Est: {localTracked.EstCode})"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading local tracked e-Courts data for CNR {CNR}", cleanCnr);
            }

            // 2. Query live e-Courts NAPIX if enabled
            if (_options.EnableECourtsIntegration)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.ECourtsTimeoutSeconds)));

                try
                {
                    string module = string.Equals(caseType, "LABOUR", StringComparison.OrdinalIgnoreCase) ? "Labour" : "MVC";
                    var cnrDetails = await _napixService.GetCnrDetailsAsync(cleanCnr, isHighCourt, module);

                    if (cnrDetails.HasValue && cnrDetails.Value.ValueKind == JsonValueKind.Object)
                    {
                        var root = cnrDetails.Value;
                        summary.IsVerified = true;
                        summary.StatusMessage = "Verified live via National e-Courts NAPIX Gateway.";

                        // Parse Case Details
                        if (root.TryGetProperty("case_details", out var cDetails) && cDetails.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in cDetails.EnumerateArray())
                            {
                                if (item.TryGetProperty("case_no", out var cn)) summary.CaseNumber = cn.GetString();
                                if (item.TryGetProperty("court_name", out var crt)) summary.CourtName = crt.GetString();
                                if (item.TryGetProperty("purpose_next", out var stg)) summary.CurrentStage = stg.GetString();
                                if (item.TryGetProperty("coram", out var coram)) summary.JudgeName = coram.GetString();
                                if (item.TryGetProperty("pet_name", out var pet)) summary.Petitioner = pet.GetString();
                                if (item.TryGetProperty("res_name", out var res)) summary.Respondent = res.GetString();
                                if (item.TryGetProperty("dt_regis", out var regDt) && DateTime.TryParse(regDt.GetString(), out var parsedReg))
                                    summary.RegistrationDate = parsedReg;
                                if (item.TryGetProperty("date_next_list", out var nextDt) && DateTime.TryParse(nextDt.GetString(), out var parsedNext))
                                    summary.NextHearingDate = parsedNext;
                                break;
                            }
                        }

                        // Parse History of Hearings
                        if (root.TryGetProperty("history_of_case_hearing", out var histArray) && histArray.ValueKind == JsonValueKind.Array)
                        {
                            summary.History.Clear();
                            foreach (var h in histArray.EnumerateArray())
                            {
                                DateTime eventDate = DateTime.MinValue;
                                if (h.TryGetProperty("hearing_date", out var hd) && DateTime.TryParse(hd.GetString(), out var dt))
                                    eventDate = dt;

                                string stage = h.TryGetProperty("purpose_of_hearing", out var poh) ? (poh.GetString() ?? "") : "";
                                string hall = h.TryGetProperty("court_no", out var cNo) ? (cNo.GetString() ?? "") : "";
                                string judge = h.TryGetProperty("judge_name", out var jn) ? (jn.GetString() ?? "") : "";
                                string order = h.TryGetProperty("business", out var bsn) ? (bsn.GetString() ?? "") : "Hearing listed";

                                if (eventDate != DateTime.MinValue || !string.IsNullOrWhiteSpace(stage))
                                {
                                    summary.History.Add(new ECourtsHistoryItemDto
                                    {
                                        EventDate = eventDate != DateTime.MinValue ? eventDate : DateTime.Now,
                                        Stage = stage,
                                        CourtHall = hall,
                                        Judge = judge,
                                        OrderDetails = order
                                    });
                                }
                            }
                        }

                        // Parse Orders & Judgments
                        var orders = await GetOrdersAndJudgmentsAsync(cleanCnr, isHighCourt, cts.Token);
                        if (orders.Count > 0)
                        {
                            summary.Orders.AddRange(orders);
                        }
                    }
                    else
                    {
                        string? lastError = _napixService.GetLastModuleError(module);
                        if (localTracked != null)
                        {
                            summary.StatusMessage = $"Verified from local e-Courts repository (Live NAPIX response: {lastError ?? "Cached records in use"}).";
                        }
                        else
                        {
                            summary.IsVerified = false;
                            summary.StatusMessage = $"e-Courts records for CNR {cleanCnr} could not be resolved live ({lastError ?? "Record not found on gateway"}).";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Timeout connecting to live eCourts NAPIX for CNR {CNR}", cleanCnr);
                    if (localTracked == null)
                    {
                        summary.IsVerified = false;
                        summary.StatusMessage = "e-Courts live lookup timed out. Internal NWKRTC records will be prioritized.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error querying live eCourts for CNR {CNR}", cleanCnr);
                    if (localTracked == null)
                    {
                        summary.IsVerified = false;
                        summary.StatusMessage = "e-Courts gateway currently unavailable. Analysis grounded in verified internal records.";
                    }
                }
            }

            return summary;
        }

        public async Task<List<ECourtsOrderDto>> GetOrdersAndJudgmentsAsync(
            string cnrNumber, 
            bool isHighCourt = false, 
            CancellationToken cancellationToken = default)
        {
            var list = new List<ECourtsOrderDto>();
            if (string.IsNullOrWhiteSpace(cnrNumber)) return list;

            try
            {
                var ordersJson = await _napixService.GetOrdersAsync(cnrNumber.Trim(), isHighCourt);
                if (ordersJson.HasValue && ordersJson.Value.ValueKind == JsonValueKind.Object)
                {
                    var root = ordersJson.Value;
                    if (root.TryGetProperty("order_details", out var oArray) && oArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var o in oArray.EnumerateArray())
                        {
                            string orderNo = o.TryGetProperty("order_no", out var on) ? (on.GetString() ?? "") : "";
                            string orderDateStr = o.TryGetProperty("order_date", out var od) ? (od.GetString() ?? "") : "";
                            DateTime? orderDate = DateTime.TryParse(orderDateStr, out var parsed) ? parsed : null;
                            string judge = o.TryGetProperty("judge_name", out var jn) ? (jn.GetString() ?? "") : "";
                            string details = o.TryGetProperty("order_details", out var dtl) ? (dtl.GetString() ?? "") : "Court Order Record";
                            bool isJudgment = details.Contains("judgment", StringComparison.OrdinalIgnoreCase) || 
                                              details.Contains("disposed", StringComparison.OrdinalIgnoreCase) ||
                                              details.Contains("award", StringComparison.OrdinalIgnoreCase);

                            list.Add(new ECourtsOrderDto
                            {
                                OrderNumber = orderNo,
                                OrderDate = orderDate,
                                OrderType = isJudgment ? "Final Judgment / Award" : "Court Interim Order",
                                Judge = judge,
                                Details = details,
                                IsJudgment = isJudgment
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing e-Courts orders for CNR {CNR}", cnrNumber);
            }

            return list;
        }
    }
}
