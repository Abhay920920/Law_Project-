using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dapper;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI.Catalog;

namespace MVCCaseManagement.Services.AI.Compiler
{
    public class CompiledQueryPlan
    {
        public string Sql { get; set; } = string.Empty;
        public DynamicParameters Parameters { get; set; } = new();
        public string IntentTitle { get; set; } = "Database Intelligence Query";
        public string Module { get; set; } = "MVC";
        public bool IsAggregation { get; set; }
        public string PrimaryTable { get; set; } = "MVC_CASES";
        public List<string> ColumnDisplayOrder { get; set; } = new();
    }

    public static class DynamicSqlCompiler
    {
        private static readonly Dictionary<string, int> DivisionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hubballi"] = 1, ["hubli"] = 1, ["hbl"] = 1,
            ["belagavi"] = 2, ["belgaum"] = 2, ["bgm"] = 2,
            ["gadag"] = 3, ["gdg"] = 3,
            ["dharwad"] = 4, ["dwd"] = 4,
            ["central office"] = 5, ["head office"] = 5, ["co"] = 5, ["ho"] = 5,
            ["bagalkot"] = 6, ["bgk"] = 6,
            ["haveri"] = 7, ["hvr"] = 7,
            ["uttara kannada"] = 8, ["sirsi"] = 8, ["uk"] = 8,
            ["chikkodi"] = 9, ["ckd"] = 9
        };

        public static CompiledQueryPlan CompileQuery(
            string naturalQuery,
            int userDivisionId,
            bool isCentralOffice,
            LegalQueryPlan? plan = null)
        {
            string raw = (naturalQuery ?? string.Empty).Trim();
            string lower = raw.ToLowerInvariant();

            var compiled = new CompiledQueryPlan();
            var parameters = new DynamicParameters();

            // 1. Identify Target Division
            int? queryDivisionId = null;
            string? queryDivisionName = null;

            if (plan?.DivisionId.HasValue == true && plan.DivisionId.Value > 0)
            {
                queryDivisionId = plan.DivisionId.Value;
                queryDivisionName = plan.DivisionName ?? plan.DivisionCode;
            }
            else
            {
                foreach (var kvp in DivisionMap)
                {
                    if (lower.Contains(kvp.Key))
                    {
                        queryDivisionId = kvp.Value;
                        queryDivisionName = kvp.Key;
                        break;
                    }
                }
            }

            // Division tenancy rule: Divisional user cannot view other divisions
            if (!isCentralOffice && userDivisionId != 0 && userDivisionId != 5)
            {
                queryDivisionId = userDivisionId;
            }

            // 2. Identify Target Domain / Module
            bool isLabour = lower.Contains("labour") || lower.Contains("industrial dispute") ||
                            lower.Contains("reinstat") || lower.Contains("misconduct") ||
                            lower.Contains("workman") || lower.Contains("id act");

            bool isGratuity = lower.Contains("gratuity") || lower.Contains("pga") ||
                              lower.Contains("controlling authority") || lower.Contains("pga case");

            bool isPettyBill = lower.Contains("petty bill") || lower.Contains("pettybill") ||
                               lower.Contains("voucher") || lower.Contains("cancelled cheque") ||
                               lower.Contains("tds") || lower.Contains("net payable");

            bool isAppeal = (lower.Contains("appeal") || lower.Contains("mfa") || lower.Contains("stay order") || lower.Contains("stay granted")) &&
                            !isLabour && !isGratuity;

            // 3. Extract Common Entities (Vehicles, Case Nos, Years)
            var vehicleMatch = Regex.Match(raw, @"\b(KA[- ]?\d{2}[- ]?[A-Z]{1,3}[- ]?\d{1,4})\b", RegexOptions.IgnoreCase);
            var yearMatch = Regex.Match(raw, @"\b(20\d{2})\b");
            var caseNoMatch = Regex.Match(raw, @"\b(?:mvc|id|pga|mfa|pb)?\s*(\d+)[ /_-]+(20\d{2})\b", RegexOptions.IgnoreCase);

            // =========================================================================
            // QUERY PATTERN 1: PETTY BILLS & CANCELLED CHEQUES
            // =========================================================================
            if (isPettyBill || lower.Contains("cancelled cheque") || lower.Contains("voucher"))
            {
                compiled.Module = "PettyBill";
                compiled.PrimaryTable = "PETTY_BILLS";

                if (lower.Contains("cancel") || lower.Contains("cancelled cheque"))
                {
                    compiled.IntentTitle = "Petty Bills with Cancelled / Dishonoured Cheques";
                    compiled.Sql = @"
                        SELECT TOP 100 
                            b.PettyBillID, 
                            b.MVCNo, 
                            b.MVCYear, 
                            ISNULL(b.PetitionerName, 'N/A') AS PetitionerName, 
                            ISNULL(b.DivisionName, 'N/A') AS DivisionName,
                            ISNULL(b.NetPayable, 0) AS NetPayable, 
                            p.ChequeNumber, 
                            p.ChequeDate, 
                            ISNULL(p.Amount, 0) AS PaymentAmount, 
                            ISNULL(p.CancellationReason, 'Cheque Dishonoured / Cancelled') AS CancellationReason
                        FROM PETTY_BILLS b
                        INNER JOIN PETTY_BILL_PAYMENTS p ON b.PettyBillID = p.PettyBillID
                        WHERE p.IsCancelled = 1";

                    if (queryDivisionId.HasValue && queryDivisionId.Value != 0 && queryDivisionId.Value != 5)
                    {
                        compiled.Sql += " AND b.CaseID IN (SELECT c.CaseID FROM MVC_CASES c WHERE c.DivisionID = @TargetDivId)";
                        parameters.Add("@TargetDivId", queryDivisionId.Value);
                    }

                    compiled.Sql += " ORDER BY p.ChequeDate DESC, b.PettyBillID DESC";
                }
                else
                {
                    compiled.IntentTitle = "Petty Bill Settlement & Voucher Details";
                    compiled.Sql = @"
                        SELECT TOP 100 
                            b.PettyBillID, 
                            b.MVCNo, 
                            b.MVCYear, 
                            ISNULL(b.PetitionerName, 'N/A') AS PetitionerName, 
                            ISNULL(b.DivisionName, 'N/A') AS DivisionName,
                            ISNULL(b.BaseAwardAmount, 0) AS BaseAward, 
                            ISNULL(b.TDSAmount, 0) AS TDSAmount, 
                            ISNULL(b.NetPayable, 0) AS NetPayable, 
                            ISNULL(b.GrossPayable, 0) AS GrossPayable, 
                            ISNULL(b.ApprovalLetterNo, 'Pending') AS ApprovalLetterNo, 
                            b.BillDate
                        FROM PETTY_BILLS b
                        WHERE 1=1";

                    if (queryDivisionId.HasValue && queryDivisionId.Value != 0 && queryDivisionId.Value != 5)
                    {
                        compiled.Sql += " AND b.CaseID IN (SELECT c.CaseID FROM MVC_CASES c WHERE c.DivisionID = @TargetDivId)";
                        parameters.Add("@TargetDivId", queryDivisionId.Value);
                    }

                    compiled.Sql += " ORDER BY b.BillDate DESC, b.PettyBillID DESC";
                }

                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 2: LABOUR CASES (Reinstatement, Misconduct, Service Matters)
            // =========================================================================
            if (isLabour)
            {
                compiled.Module = "Labour";
                compiled.PrimaryTable = "LABOUR_CASES";

                if (lower.Contains("reinstat"))
                {
                    compiled.IntentTitle = queryDivisionName != null
                        ? $"Labour Cases with Reinstatement Orders in {queryDivisionName.ToUpperInvariant()}"
                        : "Labour Cases with Workman Reinstatement Orders";

                    compiled.Sql = @"
                        SELECT TOP 100 
                            l.CaseID, 
                            l.CaseNumber, 
                            l.CaseYear, 
                            ISNULL(l.PetitionerName, 'N/A') AS WorkmanName, 
                            ISNULL(l.EmployeeNo, 'N/A') AS EmployeeNo, 
                            ISNULL(l.Designation, 'Driver/Conductor') AS Designation, 
                            ISNULL(l.NatureOfMisconduct, 'N/A') AS Misconduct, 
                            l.CO_IsWorkmanReinstated AS Reinstated, 
                            ISNULL(l.CO_WP_StayGranted, 'No Stay') AS HighCourtStay, 
                            ISNULL(l.CurrentStage, 'Active') AS CurrentStage, 
                            l.NextHearingDate
                        FROM LABOUR_CASES l
                        WHERE (l.CO_IsWorkmanReinstated = 1 OR l.AwardDetails LIKE '%reinstat%' OR l.NatureOfCase LIKE '%reinstat%')";

                    if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                    {
                        compiled.Sql += " AND l.DivisionID = @DivId";
                        parameters.Add("@DivId", queryDivisionId.Value);
                    }

                    compiled.Sql += " ORDER BY l.NextHearingDate ASC, l.CaseYear DESC";
                }
                else if (lower.Contains("depot") || lower.Contains("distribution") || lower.Contains("statistic"))
                {
                    compiled.IsAggregation = true;
                    compiled.IntentTitle = "Depot-wise & Division-wise Active Labour Disputes";
                    compiled.Sql = @"
                        SELECT TOP 100 
                            ISNULL(d.DivisionNameEnglish, 'Unknown Division') AS DivisionName, 
                            ISNULL(l.WorkingStatus, 'Active') AS WorkingStatus, 
                            COUNT(l.CaseID) AS ActiveDisputes,
                            SUM(CASE WHEN l.CO_IsWorkmanReinstated = 1 THEN 1 ELSE 0 END) AS ReinstatementsOrdered
                        FROM LABOUR_CASES l
                        LEFT JOIN DIVISION_MASTER d ON l.DivisionID = d.DivisionID
                        WHERE (l.CaseStatus = 'Pending' OR l.CaseStatus IS NULL)";

                    if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                    {
                        compiled.Sql += " AND l.DivisionID = @DivId";
                        parameters.Add("@DivId", queryDivisionId.Value);
                    }

                    compiled.Sql += " GROUP BY d.DivisionNameEnglish, l.WorkingStatus ORDER BY ActiveDisputes DESC";
                }
                else
                {
                    compiled.IntentTitle = queryDivisionName != null
                        ? $"Active Labour Disputes in {queryDivisionName.ToUpperInvariant()}"
                        : "Active Industrial Disputes & Labour Cases";

                    compiled.Sql = @"
                        SELECT TOP 100 
                            l.CaseID, 
                            l.CaseNumber, 
                            l.CaseYear, 
                            ISNULL(l.PetitionerName, 'N/A') AS WorkmanName, 
                            ISNULL(l.Designation, 'Staff') AS Designation, 
                            ISNULL(l.NatureOfMisconduct, 'N/A') AS NatureOfMisconduct, 
                            ISNULL(l.AdvocateName, 'Panel Counsel') AS AdvocateName, 
                            ISNULL(l.CurrentStage, 'Proceedings') AS CurrentStage, 
                            l.NextHearingDate
                        FROM LABOUR_CASES l
                        WHERE 1=1";

                    if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                    {
                        compiled.Sql += " AND l.DivisionID = @DivId";
                        parameters.Add("@DivId", queryDivisionId.Value);
                    }

                    if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int yr))
                    {
                        compiled.Sql += " AND l.CaseYear = @CaseYear";
                        parameters.Add("@CaseYear", yr);
                    }

                    compiled.Sql += " ORDER BY l.NextHearingDate ASC, l.CaseID DESC";
                }

                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 3: GRATUITY CLAIMS (GRA_CASES / Controlling Authority)
            // =========================================================================
            if (isGratuity)
            {
                compiled.Module = "Gratuity";
                compiled.PrimaryTable = "GRA_CASES";

                if (lower.Contains("unpaid") || lower.Contains("outstanding") || lower.Contains("total") || lower.Contains("balance"))
                {
                    compiled.IsAggregation = true;
                    compiled.IntentTitle = "Gratuity Liabilities & Outstanding Balance Summary";
                    compiled.Sql = @"
                        SELECT TOP 100 
                            ISNULL(d.DivisionNameEnglish, 'Statewide / Central') AS DivisionName, 
                            COUNT(g.CaseID) AS TotalGratuityClaims, 
                            ISNULL(SUM(g.AmountClaimed), 0) AS TotalAmountClaimed, 
                            ISNULL(SUM(g.OrderedAmount_CA), 0) AS TotalCAOrdered, 
                            ISNULL(SUM(g.ActualPaidAmount), 0) AS TotalPaidByCorp, 
                            ISNULL(SUM(ISNULL(g.OrderedAmount_CA, 0) - ISNULL(g.ActualPaidAmount, 0)), 0) AS OutstandingLiability
                        FROM GRA_CASES g
                        LEFT JOIN DIVISION_MASTER d ON (g.DivisionCode = CAST(d.DivisionID AS VARCHAR(10)) OR g.DivisionCode = d.DivisionCode)
                        WHERE 1=1";

                    if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                    {
                        compiled.Sql += " AND (g.DivisionCode = CAST(@DivId AS VARCHAR(10)) OR g.DivisionCode = @DivCode)";
                        parameters.Add("@DivId", queryDivisionId.Value);
                        parameters.Add("@DivCode", queryDivisionId.Value.ToString());
                    }

                    compiled.Sql += " GROUP BY d.DivisionNameEnglish ORDER BY OutstandingLiability DESC";
                }
                else
                {
                    compiled.IntentTitle = "Gratuity Claims & CA Orders Register";
                    compiled.Sql = @"
                        SELECT TOP 100 
                            g.CaseID, 
                            g.PGANumber, 
                            ISNULL(g.ClaimantName, 'N/A') AS ClaimantName, 
                            ISNULL(g.ClaimantDesignation, 'Retired') AS Designation, 
                            ISNULL(g.AmountClaimed, 0) AS ClaimedAmount, 
                            ISNULL(g.OrderedAmount_CA, 0) AS CAOrderedAmount, 
                            ISNULL(g.ActualPaidAmount, 0) AS PaidAmount, 
                            ISNULL(g.CurrentStage, 'Hearing') AS CurrentStage, 
                            g.NextHearingDate
                        FROM GRA_CASES g
                        WHERE 1=1";

                    if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                    {
                        compiled.Sql += " AND (g.DivisionCode = CAST(@DivId AS VARCHAR(10)) OR g.DivisionCode = @DivCode)";
                        parameters.Add("@DivId", queryDivisionId.Value);
                        parameters.Add("@DivCode", queryDivisionId.Value.ToString());
                    }

                    compiled.Sql += " ORDER BY g.NextHearingDate ASC, g.CaseID DESC";
                }

                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 4: HIGH COURT APPEALS & STAY ORDERS (APPEAL_DETAILS)
            // =========================================================================
            if (isAppeal)
            {
                compiled.Module = "Appeal";
                compiled.PrimaryTable = "APPEAL_DETAILS";
                compiled.IntentTitle = "High Court MFA Appeals & Interim Stay Orders";

                compiled.Sql = @"
                    SELECT TOP 100 
                        c.CaseID, 
                        c.MVCNo, 
                        c.MVCYear, 
                        ISNULL(d.DivisionNameEnglish, 'N/A') AS DivisionName, 
                        ISNULL(a.CorpMFANumber, 'Pending Filing') AS MFANumber, 
                        a.CorpMFAYear, 
                        ISNULL(a.HighCourtBench, 'Dharwad Bench') AS HighCourtBench, 
                        ISNULL(a.StayGranted, 'Pending') AS StayGranted, 
                        a.StayDate, 
                        ISNULL(a.StayNature, 'Interim Stay on Award') AS StayNature, 
                        ISNULL(a.CorpMFAStatus, 'Pending') AS MFAStatus
                    FROM APPEAL_DETAILS a
                    INNER JOIN MVC_CASES c ON a.CaseID = c.CaseID
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    WHERE (a.StayGranted LIKE '%yes%' OR a.StayGranted LIKE '%grant%' OR a.StayNature IS NOT NULL)";

                if (lower.Contains("dharwad"))
                {
                    compiled.Sql += " AND a.HighCourtBench LIKE '%Dharwad%'";
                }
                else if (lower.Contains("kalaburagi") || lower.Contains("gulbarga"))
                {
                    compiled.Sql += " AND a.HighCourtBench LIKE '%Kalaburagi%'";
                }

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += " ORDER BY a.StayDate DESC, a.AppealID DESC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 5: ADVOCATE PORTFOLIO & CASELOAD RANKING
            // =========================================================================
            if (lower.Contains("advocate") || lower.Contains("lawyer") || lower.Contains("counsel") || lower.Contains("caseload"))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IsAggregation = true;
                compiled.IntentTitle = queryDivisionName != null
                    ? $"Top Advocates by Active Caseload in {queryDivisionName.ToUpperInvariant()}"
                    : "Top Empanelled Advocates by Active Caseload";

                compiled.Sql = @"
                    SELECT TOP 5 
                        LTRIM(RTRIM(ISNULL(c.AdvocateName, 'Unassigned'))) AS AdvocateName, 
                        COUNT(c.CaseID) AS ActiveCases, 
                        ISNULL(SUM(c.ClaimAmount), 0) AS TotalClaimExposure, 
                        COUNT(CASE WHEN c.NextHearingDate IS NOT NULL AND c.NextHearingDate <= DATEADD(day, 14, GETDATE()) THEN 1 END) AS UpcomingHearingsIn14Days
                    FROM MVC_CASES c
                    WHERE (c.DisposalStatus = 'Pending' OR c.DisposalStatus IS NULL)";

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += @"
                    GROUP BY LTRIM(RTRIM(ISNULL(c.AdvocateName, 'Unassigned')))
                    HAVING LTRIM(RTRIM(ISNULL(c.AdvocateName, 'Unassigned'))) NOT IN ('', 'N/A')
                    ORDER BY ActiveCases DESC";

                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 6: VEHICLE FLEET LITIGATION HISTORY
            // =========================================================================
            if (vehicleMatch.Success)
            {
                string rawVeh = vehicleMatch.Groups[1].Value;
                string cleanVeh = Regex.Replace(rawVeh, @"[\s-]", "");

                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IntentTitle = $"Fleet Accident & Litigation Dossier for Bus {rawVeh.ToUpperInvariant()}";

                compiled.Sql = @"
                    SELECT TOP 100 
                        c.CaseID, 
                        c.MVCNo, 
                        c.MVCYear, 
                        c.AccidentDate, 
                        c.VehicleNo, 
                        ISNULL(c.ClaimType, 'Injury') AS ClaimType, 
                        ISNULL(c.ClaimAmount, 0) AS ClaimAmount, 
                        ISNULL(ad.AwardAmount, 0) AS AwardAmount, 
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage, 
                        ISNULL(c.DisposalStatus, 'Pending') AS DisposalStatus
                    FROM MVC_CASES c
                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON c.CaseID = ad.CaseID
                    WHERE REPLACE(REPLACE(c.VehicleNo, '-', ''), ' ', '') = @CleanVehicleNo";

                parameters.Add("@CleanVehicleNo", cleanVeh);

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += " ORDER BY c.AccidentDate DESC, c.CaseYear DESC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 7: OVERDUE JUDICIAL HEARINGS (> 30 DAYS OVERDUE)
            // =========================================================================
            if (lower.Contains("overdue") || lower.Contains("delayed hearing") || (lower.Contains("hearing") && lower.Contains("30 day")))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IntentTitle = "Cases with Overdue Next Hearing Dates (> 30 Days Unlisted)";

                compiled.Sql = @"
                    SELECT TOP 100 
                        c.CaseID, 
                        c.MVCNo, 
                        c.MVCYear, 
                        ISNULL(c.VehicleNo, 'N/A') AS VehicleNo, 
                        c.NextHearingDate, 
                        DATEDIFF(day, c.NextHearingDate, GETDATE()) AS DaysOverdue, 
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage, 
                        ISNULL(c.AdvocateName, 'Panel Counsel') AS AdvocateName
                    FROM MVC_CASES c
                    WHERE c.NextHearingDate < DATEADD(day, -30, GETDATE())
                      AND (c.DisposalStatus = 'Pending' OR c.DisposalStatus IS NULL)";

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += " ORDER BY c.NextHearingDate ASC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 8: MISSING EVIDENCE AUDIT
            // =========================================================================
            if (lower.Contains("missing evidence") || lower.Contains("evidentiary gap") || lower.Contains("missing document"))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IntentTitle = "Pre-Briefing Evidentiary Documentation Audit";

                compiled.Sql = @"
                    SELECT TOP 100 
                        c.CaseID, 
                        c.MVCNo, 
                        c.MVCYear, 
                        ISNULL(c.VehicleNo, 'N/A') AS VehicleNo, 
                        c.AccidentDate, 
                        c.IsObjectionFiled, 
                        c.IsEvidenceFiled, 
                        ISNULL(ad.IsFIRFiled, 0) AS FIRFiled, 
                        ISNULL(ad.IsChargeSheetFiled, 0) AS ChargeSheetFiled, 
                        ISNULL(ad.IsBusCameraInstalled, 0) AS CameraInstalled, 
                        ISNULL(c.CurrentStage, 'Trial') AS CurrentStage
                    FROM MVC_CASES c
                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON c.CaseID = ad.CaseID
                    WHERE (c.IsEvidenceFiled = 0 OR c.IsObjectionFiled = 0 OR ISNULL(ad.IsFIRFiled, 0) = 0)
                      AND (c.DisposalStatus = 'Pending' OR c.DisposalStatus IS NULL)";

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += " ORDER BY c.CaseYear DESC, c.CaseID DESC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 9: FINANCIAL COMPENSATION AWARDED VS DEPOSITED
            // =========================================================================
            if (lower.Contains("award") && (lower.Contains("deposit") || lower.Contains("compensation") || lower.Contains("total liability")))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IsAggregation = true;
                compiled.IntentTitle = queryDivisionName != null
                    ? $"Compensation Awarded vs Deposited in {queryDivisionName.ToUpperInvariant()}"
                    : "Corporation Total Compensation Awarded vs EP Deposited";

                compiled.Sql = @"
                    SELECT TOP 100 
                        ISNULL(d.DivisionNameEnglish, 'Statewide / Central') AS DivisionName, 
                        COUNT(c.CaseID) AS AdverseCasesCount, 
                        ISNULL(SUM(ad.AwardAmount), 0) AS TotalAwardedAmount, 
                        ISNULL(SUM(ad.EPDepositedAmount), 0) AS TotalEPDepositedAmount, 
                        ISNULL(SUM(ad.AwardAmount) - SUM(ad.EPDepositedAmount), 0) AS OutstandingAwardBalance
                    FROM MVC_CASES c
                    INNER JOIN MVC_CASE_ADVERSE_DETAILS ad ON c.CaseID = ad.CaseID
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    WHERE 1=1";

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                compiled.Sql += " GROUP BY d.DivisionNameEnglish ORDER BY OutstandingAwardBalance DESC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 10: FATAL ACCIDENT CLAIMS IN SPECIFIC YEAR / DIVISION
            // =========================================================================
            if (lower.Contains("fatal") || lower.Contains("death") || lower.Contains("fatality"))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IntentTitle = queryDivisionName != null
                    ? $"Fatal MVC Accident Claims in {queryDivisionName.ToUpperInvariant()}"
                    : "Fatal Motor Accident Claims (MVC)";

                compiled.Sql = @"
                    SELECT TOP 100 
                        c.CaseID, 
                        c.MVCNo, 
                        c.MVCYear, 
                        c.AccidentDate, 
                        ISNULL(c.VehicleNo, 'N/A') AS VehicleNo, 
                        ISNULL(c.ClaimAmount, 0) AS ClaimAmount, 
                        ISNULL(c.CurrentStage, 'Trial') AS CurrentStage, 
                        ISNULL(c.AdvocateName, 'Panel Counsel') AS AdvocateName, 
                        ISNULL(c.DisposalStatus, 'Pending') AS DisposalStatus
                    FROM MVC_CASES c
                    WHERE (c.ClaimType LIKE '%Fatal%' OR c.ClaimType LIKE '%Death%')";

                if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
                {
                    compiled.Sql += " AND c.DivisionID = @DivId";
                    parameters.Add("@DivId", queryDivisionId.Value);
                }

                if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int yr))
                {
                    compiled.Sql += " AND c.MVCYear = @CaseYear";
                    parameters.Add("@CaseYear", yr);
                }

                compiled.Sql += " ORDER BY c.AccidentDate DESC, c.CaseYear DESC";
                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // QUERY PATTERN 11: DIVISION COMPARISON / STATEWIDE REGISTER
            // =========================================================================
            if (lower.Contains("compare") || lower.Contains("statewide") || lower.Contains("all divisions") || lower.Contains("across divisions"))
            {
                compiled.Module = "MVC";
                compiled.PrimaryTable = "MVC_CASES";
                compiled.IsAggregation = true;
                compiled.IntentTitle = "Statewide Divisional Litigation & Pending Claims Comparison";

                compiled.Sql = @"
                    SELECT TOP 100 
                        d.DivisionNameEnglish AS DivisionName, 
                        COUNT(c.CaseID) AS TotalCases, 
                        COUNT(CASE WHEN c.DisposalStatus = 'Pending' OR c.DisposalStatus IS NULL THEN 1 END) AS PendingCases, 
                        COUNT(CASE WHEN c.DisposalStatus = 'Disposed' THEN 1 END) AS DisposedCases, 
                        ISNULL(SUM(c.ClaimAmount), 0) AS TotalClaimExposure
                    FROM DIVISION_MASTER d
                    LEFT JOIN MVC_CASES c ON d.DivisionID = c.DivisionID
                    WHERE d.IsActive = 1
                    GROUP BY d.DivisionNameEnglish
                    ORDER BY TotalCases DESC";

                compiled.Parameters = parameters;
                return compiled;
            }

            // =========================================================================
            // DEFAULT FALLBACK: ACTIVE CASE REGISTER IN TARGET DOMAIN / DIVISION
            // =========================================================================
            compiled.Module = "MVC";
            compiled.PrimaryTable = "MVC_CASES";
            compiled.IntentTitle = queryDivisionName != null
                ? $"Verified Case Records in {queryDivisionName.ToUpperInvariant()}"
                : "Verified Active Legal Case Register";

            compiled.Sql = @"
                SELECT TOP 100 
                    c.CaseID, 
                    c.MVCNo, 
                    c.MVCYear, 
                    c.AccidentDate, 
                    ISNULL(c.VehicleNo, 'N/A') AS VehicleNo, 
                    ISNULL(c.ClaimAmount, 0) AS ClaimAmount, 
                    ISNULL(c.CurrentStage, 'Pending') AS CurrentStage, 
                    c.NextHearingDate, 
                    ISNULL(c.AdvocateName, 'Panel Counsel') AS AdvocateName, 
                    ISNULL(c.DisposalStatus, 'Pending') AS DisposalStatus
                FROM MVC_CASES c
                WHERE 1=1";

            if (queryDivisionId.HasValue && queryDivisionId.Value > 0)
            {
                compiled.Sql += " AND c.DivisionID = @DivId";
                parameters.Add("@DivId", queryDivisionId.Value);
            }

            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int fallbackYr))
            {
                compiled.Sql += " AND c.MVCYear = @CaseYear";
                parameters.Add("@CaseYear", fallbackYr);
            }

            compiled.Sql += " ORDER BY c.NextHearingDate ASC, c.CaseYear DESC";
            compiled.Parameters = parameters;
            return compiled;
        }
    }
}

