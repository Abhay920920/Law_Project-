using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly ICaseRepository _caseRepo;
        private readonly IEPRepository _epRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly IPettyBillRepository _pettyBillRepo;
        private readonly ICasePaymentRepository _casePaymentRepo;
        private readonly ILogger<ReportController> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public ReportController(ICaseRepository caseRepo, IEPRepository epRepo, IMasterRepository masterRepo, IPettyBillRepository pettyBillRepo, ICasePaymentRepository casePaymentRepo, ILogger<ReportController> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _caseRepo = caseRepo;
            _epRepo = epRepo;
            _masterRepo = masterRepo;
            _pettyBillRepo = pettyBillRepo;
            _casePaymentRepo = casePaymentRepo;
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            if (Request.Path.Value != null && 
                (Request.Path.Value.Equals("/Report", StringComparison.OrdinalIgnoreCase) || 
                 Request.Path.Value.Equals("/Report/Index", StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect("/pettybill" + Request.QueryString);
            }

            int divisionId = int.Parse(User.FindFirstValue("DivisionID") ?? "0");
            ViewBag.MACTList = _masterRepo.GetAllMACTs();
            try
            {
                ViewBag.AuditedCases = _epRepo.GetAuditedCasesForBilling(divisionId, 15);
            }
            catch (Exception exAudited)
            {
                _logger.LogWarning(exAudited, "Could not load audited cases for billing queue");
                ViewBag.AuditedCases = new List<EPViewModel>();
            }
            return View();
        }

        [HttpGet]
        public IActionResult CentralOffice(int? month, int? year, string? module, string? caseType, string? view)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            int selectedMonth = month.HasValue && month >= 1 && month <= 12 ? month.Value : DateTime.Now.Month;
            int selectedYear = year.HasValue && year >= 2000 && year <= 2050 ? year.Value : DateTime.Now.Year;
            string selectedModule = "MVC";
            if (!string.IsNullOrEmpty(module))
            {
                if (module.Equals("Labour", StringComparison.OrdinalIgnoreCase)) selectedModule = "Labour";
                else if (module.Equals("Gratuity", StringComparison.OrdinalIgnoreCase)) selectedModule = "Gratuity";
                else if (module.Equals("Others", StringComparison.OrdinalIgnoreCase)) selectedModule = "Others";
                else if (module.Equals("MMR-ST2", StringComparison.OrdinalIgnoreCase) || module.Equals("MMRST2", StringComparison.OrdinalIgnoreCase)) selectedModule = "MMR-ST2";
            }
            string selectedCaseType = !string.IsNullOrEmpty(caseType) ? caseType.Trim() : "ALL";
            string selectedView = !string.IsNullOrEmpty(view) ? view.Trim() : "MonthlyStatement";

            DateTime startDate = new DateTime(selectedYear, selectedMonth, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

            var viewModel = new MvcMonthlyStatementViewModel
            {
                SelectedMonth = selectedMonth,
                SelectedYear = selectedYear,
                SelectedModule = selectedModule,
                SelectedCaseType = selectedCaseType,
                SelectedView = selectedView
            };

            string connectionString = _configuration.GetConnectionString("MVCCaseDB");

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                conn.Open();

                // 1. Fetch active divisions
                string divQuery = @"
                    SELECT DivisionID, ISNULL(DivisionCode, DivisionNameEnglish) as DivisionCode, DivisionNameEnglish as DivisionName 
                    FROM DIVISION_MASTER 
                    WHERE IsActive = 1 AND DivisionID NOT IN (0, 5) 
                    ORDER BY DivisionID";

                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(divQuery, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            viewModel.Divisions.Add(new DivisionColumnData
                            {
                                DivisionID = Convert.ToInt32(reader["DivisionID"]),
                                DivisionCode = reader["DivisionCode"]?.ToString() ?? "",
                                DivisionName = reader["DivisionName"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                if (viewModel.Divisions.Any())
                {
                    var divDict = viewModel.Divisions.ToDictionary(d => d.DivisionID);

                    if (selectedModule == "Labour")
                    {
                        // Build CaseType filter clause for Labour Cases
                        string caseTypeFilter = "";
                        bool isArising = selectedCaseType.Equals("ArisingApplication", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("Arising Application", StringComparison.OrdinalIgnoreCase);

                        string labourStatsQuery;
                        if (isArising)
                        {
                            labourStatsQuery = @"
                                WITH CombinedLabourCases AS (
                                    SELECT c.DivisionID, c.EntrustmentDate, c.CreatedDate, c.ModifiedDate, c.DisposalDate, c.DisposalResult, c.CaseStatus, c.TransferredFromDivisionID, c.TransferDate
                                    FROM LABOUR_CASES c
                                    WHERE (c.IsArisingApplication = 1 OR UPPER(ISNULL(c.CaseType,'')) LIKE '%ARISING%')
                                    UNION ALL
                                    SELECT a.DivisionID, a.EntrustmentDate, a.CreatedDate, a.ModifiedDate, a.DisposalDate, a.DisposalResult, a.CaseStatus, CAST(NULL AS INT) as TransferredFromDivisionID, CAST(NULL AS DATE) as TransferDate
                                    FROM LABOUR_ARISING_APPLICATIONS a
                                )
                                SELECT 
                                    d.DivisionID,
                                    SUM(CASE WHEN 
                                            ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate < @StartDate))
                                            AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate)
                                            AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR c.ModifiedDate >= @StartDate)
                                        THEN 1 ELSE 0 END) as BeginningPending,
                                        
                                    SUM(CASE WHEN 
                                            (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                            OR (c.EntrustmentDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)
                                        THEN 1 ELSE 0 END) as EntrustedCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                             OR (c.DisposalDate IS NULL AND UPPER(c.CaseStatus) = 'DISPOSED' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'PARTIALLY FAVOR', 'SETTLED', 'FAVOR (SETTLED)'))
                                        THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                             OR (c.DisposalDate IS NULL AND UPPER(c.CaseStatus) = 'DISPOSED' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED'))
                                        THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                    SUM(CASE WHEN 
                                            (c.TransferredFromDivisionID IS NOT NULL AND c.TransferredFromDivisionID > 0)
                                            AND ((c.TransferDate >= @StartDate AND c.TransferDate <= @EndDate) OR (c.TransferDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate))
                                        THEN 1 ELSE 0 END) as ReceivedFromOtherDivisionCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.TransferDate >= @StartDate AND c.TransferDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.CaseStatus,'')) = 'TRANSFERRED')
                                        THEN 1 ELSE 0 END) as TransferredCount,

                                    SUM(CASE WHEN 
                                            ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                            AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate)
                                            AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR c.ModifiedDate > @EndDate)
                                        THEN 1 ELSE 0 END) as EndingPending
                                FROM DIVISION_MASTER d
                                LEFT JOIN CombinedLabourCases c ON c.DivisionID = d.DivisionID
                                WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                                GROUP BY d.DivisionID";
                        }
                        else
                        {
                            if (selectedCaseType.Equals("KID", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%KID%') ";
                            }
                            else if (selectedCaseType.Equals("ID", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND ((UPPER(c.CaseType) = 'ID' OR UPPER(c.CaseType) LIKE '%I.D.%' OR UPPER(c.CaseType) = 'REF.NO' OR UPPER(c.CaseType) LIKE '%REF%') AND UPPER(c.CaseType) NOT LIKE '%KID%') ";
                            }

                            // 2. Fetch Labour Case counts per division
                            labourStatsQuery = $@"
                                SELECT 
                                    d.DivisionID,
                                    SUM(CASE WHEN 
                                            ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate < @StartDate))
                                            AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate)
                                            AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR c.ModifiedDate >= @StartDate)
                                        THEN 1 ELSE 0 END) as BeginningPending,
                                        
                                    SUM(CASE WHEN 
                                            (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                            OR (c.EntrustmentDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)
                                        THEN 1 ELSE 0 END) as EntrustedCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                             OR (c.DisposalDate IS NULL AND UPPER(c.CaseStatus) = 'DISPOSED' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'PARTIALLY FAVOR', 'SETTLED', 'FAVOR (SETTLED)'))
                                        THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                             OR (c.DisposalDate IS NULL AND UPPER(c.CaseStatus) = 'DISPOSED' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED'))
                                        THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                    SUM(CASE WHEN 
                                            (c.TransferredFromDivisionID IS NOT NULL AND c.TransferredFromDivisionID > 0)
                                            AND ((c.TransferDate >= @StartDate AND c.TransferDate <= @EndDate) OR (c.TransferDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate))
                                        THEN 1 ELSE 0 END) as ReceivedFromOtherDivisionCount,
                                        
                                    SUM(CASE WHEN 
                                            ((c.TransferDate >= @StartDate AND c.TransferDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                            AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.CaseStatus,'')) = 'TRANSFERRED')
                                        THEN 1 ELSE 0 END) as TransferredCount,

                                    SUM(CASE WHEN 
                                            ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                            AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate)
                                            AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR c.ModifiedDate > @EndDate)
                                        THEN 1 ELSE 0 END) as EndingPending
                                FROM DIVISION_MASTER d
                                LEFT JOIN LABOUR_CASES c ON c.DivisionID = d.DivisionID {caseTypeFilter}
                                WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                                GROUP BY d.DivisionID";
                        }

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(labourStatsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        divData.BeginningPending = Convert.ToInt32(reader["BeginningPending"]);
                                        divData.EntrustedCount = Convert.ToInt32(reader["EntrustedCount"]);
                                        divData.DisposedFavorCount = Convert.ToInt32(reader["DisposedFavorCount"]);
                                        divData.DisposedAgainstCount = Convert.ToInt32(reader["DisposedAgainstCount"]);
                                        divData.ReceivedFromOtherDivisionCount = Convert.ToInt32(reader["ReceivedFromOtherDivisionCount"]);
                                        divData.TransferredCount = Convert.ToInt32(reader["TransferredCount"]);
                                        // Enforce 100% mathematical integrity across every division column:
                                        // Ending = Beginning + Entrusted + Received - DisposedFavor - DisposedAgainst - Transferred
                                        divData.EndingPending = Math.Max(0, divData.CalculatedEndingPending);
                                    }
                                }
                            }
                        }

                        // 3. Outstanding balance pending at Accounts (Count of pending EP/Accounts cases for Labour)
                        try
                        {
                            string accountsPendingQuery = $@"
                                SELECT 
                                    d.DivisionID,
                                    COUNT(DISTINCT ep.EPID) as PendingAccountsCases
                                FROM DIVISION_MASTER d
                                JOIN LABOUR_CASES c ON c.DivisionID = d.DivisionID {caseTypeFilter}
                                JOIN LABOUR_EP_DETAILS ep ON ep.CaseID = c.CaseID
                                WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                                  AND (ep.IsSentToAccounts = 1 OR c.SentToCO = 1)
                                  AND (ep.ComplianceStatus IS NULL OR UPPER(ep.ComplianceStatus) <> 'COMPLIED')
                                GROUP BY d.DivisionID";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(accountsPendingQuery, conn))
                            {
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int dId = Convert.ToInt32(reader["DivisionID"]);
                                        if (divDict.TryGetValue(dId, out var divData))
                                        {
                                            divData.AccountsPendingCasesCount = Convert.ToInt32(reader["PendingAccountsCases"]);
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Table optional or empty */ }

                        // Fallback check for Accounts pending cases in LABOUR_CASES directly if EP table count is 0
                        foreach (var divData in viewModel.Divisions)
                        {
                            if (divData.AccountsPendingCasesCount == 0)
                            {
                                try
                                {
                                    string fallbackAccQuery = $@"
                                        SELECT COUNT(*) 
                                        FROM LABOUR_CASES c
                                        WHERE c.DivisionID = @DivID {caseTypeFilter}
                                          AND (c.SentToCO = 1 OR c.IsEPFiled = 1)
                                          AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed')";

                                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(fallbackAccQuery, conn))
                                    {
                                        cmd.Parameters.AddWithValue("@DivID", divData.DivisionID);
                                        divData.AccountsPendingCasesCount = Convert.ToInt32(cmd.ExecuteScalar());
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    else if (selectedModule == "Gratuity")
                    {
                        // Build CaseType filter clause for Gratuity Cases
                        string caseTypeFilter = "";
                        if (selectedCaseType.Equals("PGACR", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("PGA/CR", StringComparison.OrdinalIgnoreCase))
                        {
                            caseTypeFilter = " AND ((c.CourtType = 'Controlling Authority' OR c.CourtType IS NULL) AND (c.AppealNumber IS NULL OR RTRIM(LTRIM(c.AppealNumber)) = '')) ";
                        }
                        else if (selectedCaseType.Equals("PGAApplCR", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("PGA/Appeal/CR Number", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("PGA/Appeal/CR", StringComparison.OrdinalIgnoreCase))
                        {
                            caseTypeFilter = " AND ((c.AppealNumber IS NOT NULL AND RTRIM(LTRIM(c.AppealNumber)) <> '') OR UPPER(ISNULL(c.CourtType,'')) LIKE '%APPELLATE%' OR UPPER(ISNULL(c.CourtType,'')) LIKE '%APPEAL%') ";
                        }

                        // 2. Fetch Gratuity Case counts per division
                        string gratuityStatsQuery = $@"
                            SELECT 
                                d.DivisionID,
                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate < @StartDate))
                                        AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate)
                                        AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed' OR c.ModifiedDate >= @StartDate)
                                    THEN 1 ELSE 0 END) as BeginningPending,
                                    
                                SUM(CASE WHEN 
                                        (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                        OR (c.EntrustmentDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)
                                    THEN 1 ELSE 0 END) as EntrustedCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                         OR (c.DisposalDate IS NULL AND c.CaseStatus = 'Disposed' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                         OR (c.DisposalDate IS NULL AND c.CaseStatus = 'Disposed' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.ForwardingStatus,'')) LIKE '%RECEIVED%' OR UPPER(ISNULL(c.Remarks,'')) LIKE '%RECEIVED%')
                                    THEN 1 ELSE 0 END) as ReceivedFromOtherDivisionCount,

                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.CaseStatus,'')) = 'TRANSFERRED')
                                    THEN 1 ELSE 0 END) as TransferredCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                        AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate)
                                        AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed' OR c.ModifiedDate > @EndDate)
                                    THEN 1 ELSE 0 END) as EndingPending,

                                SUM(CASE WHEN (c.ComplianceStatus IS NULL OR UPPER(c.ComplianceStatus) <> 'COMPLIED')
                                    THEN ISNULL(c.OrderedAmount_CA, ISNULL(c.FinalAmount, ISNULL(c.GratuityAmount_CA_Act, ISNULL(c.GratuityAmount_Corp_Act, 0))))
                                    ELSE 0 END) as GratuityAmountDue
                            FROM DIVISION_MASTER d
                            LEFT JOIN GRA_CASES c ON (c.DivisionCode = d.DivisionID OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionCode OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionNameEnglish) {caseTypeFilter}
                            WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                            GROUP BY d.DivisionID";

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(gratuityStatsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        divData.BeginningPending = Convert.ToInt32(reader["BeginningPending"]);
                                        divData.EntrustedCount = Convert.ToInt32(reader["EntrustedCount"]);
                                        divData.DisposedFavorCount = Convert.ToInt32(reader["DisposedFavorCount"]);
                                        divData.DisposedAgainstCount = Convert.ToInt32(reader["DisposedAgainstCount"]);
                                        divData.ReceivedFromOtherDivisionCount = Convert.ToInt32(reader["ReceivedFromOtherDivisionCount"]);
                                        divData.TransferredCount = Convert.ToInt32(reader["TransferredCount"]);
                                        // Enforce 100% mathematical integrity: Ending = Beginning + Entrusted + Received - DisposedFavor - DisposedAgainst - Transferred
                                        divData.EndingPending = Math.Max(0, divData.CalculatedEndingPending);
                                        divData.GratuityAmountDue = Convert.ToDecimal(reader["GratuityAmountDue"]);
                                    }
                                }
                            }
                        }

                        // Outstanding accounts cases for Gratuity
                        try
                        {
                            string gratuityAccQuery = $@"
                                SELECT 
                                    d.DivisionID,
                                    COUNT(DISTINCT c.CaseID) as PendingAccountsCases
                                FROM DIVISION_MASTER d
                                JOIN GRA_CASES c ON (c.DivisionCode = d.DivisionID OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionCode OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionNameEnglish) {caseTypeFilter}
                                WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                                  AND (c.ForwardingStatus LIKE '%Central Office%' OR c.ActionTaken IS NOT NULL)
                                  AND (c.ComplianceStatus IS NULL OR UPPER(c.ComplianceStatus) <> 'COMPLIED')
                                GROUP BY d.DivisionID";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(gratuityAccQuery, conn))
                            {
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int dId = Convert.ToInt32(reader["DivisionID"]);
                                        if (divDict.TryGetValue(dId, out var divData))
                                        {
                                            divData.AccountsPendingCasesCount = Convert.ToInt32(reader["PendingAccountsCases"]);
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    else if (selectedModule == "Others")
                    {
                        // Build CaseType filter clause for Others Cases
                        string caseTypeFilter = "";
                        if (!string.IsNullOrEmpty(selectedCaseType) && !selectedCaseType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                            if (selectedCaseType.Equals("Consumer", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%CONSUMER%') ";
                            }
                            else if (selectedCaseType.Equals("LAC", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%LAC%') ";
                            }
                            else if (selectedCaseType.Equals("OS", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%OS%') ";
                            }
                            else if (selectedCaseType.Equals("PSC", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("P&SC", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%PSC%' OR UPPER(c.CaseType) LIKE '%P&SC%' OR UPPER(c.CaseType) LIKE '%P & SC%') ";
                            }
                            else if (selectedCaseType.Equals("CC", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%CC%') ";
                            }
                            else if (selectedCaseType.Equals("ECA", StringComparison.OrdinalIgnoreCase))
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) LIKE '%ECA%') ";
                            }
                            else
                            {
                                caseTypeFilter = " AND (UPPER(c.CaseType) = @CaseTypeFilter) ";
                            }
                        }

                        string othersStatsQuery = $@"
                            SELECT 
                                d.DivisionID,
                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate < @StartDate))
                                        AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate)
                                        AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR UPPER(ISNULL(c.DisposalStatus,'')) NOT IN ('DISPOSED', 'CLOSED', 'CLOSED AT DIVISION LEVEL', 'DNP') OR c.ClosureDate >= @StartDate OR c.ModifiedDate >= @StartDate)
                                    THEN 1 ELSE 0 END) as BeginningPending,
                                    
                                SUM(CASE WHEN 
                                        (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                        OR (c.EntrustmentDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)
                                    THEN 1 ELSE 0 END) as EntrustedCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)', 'SETTLED'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                        AND (c.ClosureDate IS NULL OR c.ClosureDate > @EndDate)
                                        AND (c.CaseStatus IS NULL OR UPPER(c.CaseStatus) <> 'DISPOSED' OR c.ModifiedDate > @EndDate)
                                    THEN 1 ELSE 0 END) as EndingPending
                            FROM DIVISION_MASTER d
                            LEFT JOIN OTHER_CASES c ON c.DivisionID = d.DivisionID {caseTypeFilter}
                            WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                            GROUP BY d.DivisionID";

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(othersStatsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);
                            if (!string.IsNullOrEmpty(caseTypeFilter) && caseTypeFilter.Contains("@CaseTypeFilter"))
                            {
                                cmd.Parameters.AddWithValue("@CaseTypeFilter", selectedCaseType.ToUpper());
                            }

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        divData.BeginningPending = Convert.ToInt32(reader["BeginningPending"]);
                                        divData.EntrustedCount = Convert.ToInt32(reader["EntrustedCount"]);
                                        divData.DisposedFavorCount = Convert.ToInt32(reader["DisposedFavorCount"]);
                                        divData.DisposedAgainstCount = Convert.ToInt32(reader["DisposedAgainstCount"]);
                                        // Enforce 100% mathematical integrity: Ending = Beginning + Entrusted - DisposedFavor - DisposedAgainst
                                        divData.EndingPending = Math.Max(0, divData.CalculatedEndingPending);
                                    }
                                }
                            }
                        }
                    }
                    else if (selectedModule == "MMR-ST2")
                    {
                        // MMR-ST2 Statement Calculation across all modules (MVC, Labour, Gratuity, Other Courts)
                        
                        // 1. SUPREME COURT
                        try
                        {
                            string scQuery = @"
                                SELECT 
                                    ISNULL(SUM(BeginningPending), 0) as BeginningPending,
                                    ISNULL(SUM(EntrustedCount), 0) as EntrustedCount,
                                    ISNULL(SUM(DisposedFavorCount), 0) as DisposedFavorCount,
                                    ISNULL(SUM(DisposedAgainstCount), 0) as DisposedAgainstCount
                                FROM (
                                    -- MVC Supreme Court SLP
                                    SELECT 
                                        SUM(CASE WHEN ((ap.CorpSCEntrustmentDate IS NOT NULL AND ap.CorpSCEntrustmentDate < @StartDate) OR (ap.ClaimantSCEntrustmentDate IS NOT NULL AND ap.ClaimantSCEntrustmentDate < @StartDate) OR (c.CreatedAt < @StartDate)) AND (ap.ClaimantSCClosureDate IS NULL OR ap.ClaimantSCClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((ap.CorpSCEntrustmentDate >= @StartDate AND ap.CorpSCEntrustmentDate <= @EndDate) OR (ap.ClaimantSCEntrustmentDate >= @StartDate AND ap.ClaimantSCEntrustmentDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN ap.ClaimantSCClosureDate >= @StartDate AND ap.ClaimantSCClosureDate <= @EndDate AND UPPER(ISNULL(ap.ClaimantSCOutcome,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'SETTLED', 'CLOSED IN FAVOR') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN ap.ClaimantSCClosureDate >= @StartDate AND ap.ClaimantSCClosureDate <= @EndDate AND UPPER(ISNULL(ap.ClaimantSCOutcome,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM APPEAL_DETAILS ap
                                    JOIN MVC_CASES c ON c.CaseID = ap.CaseID
                                    WHERE ((ap.CorpSCNumber IS NOT NULL AND RTRIM(LTRIM(ap.CorpSCNumber)) <> '') OR (ap.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(ap.ClaimantSCNumber)) <> ''))

                                    UNION ALL

                                    -- Labour Supreme Court SLP
                                    SELECT 
                                        SUM(CASE WHEN ((c.ClaimantSCEntrustmentDate IS NOT NULL AND c.ClaimantSCEntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClaimantSCClosureDate IS NULL OR c.ClaimantSCClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN (c.ClaimantSCEntrustmentDate >= @StartDate AND c.ClaimantSCEntrustmentDate <= @EndDate) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClaimantSCClosureDate >= @StartDate AND c.ClaimantSCClosureDate <= @EndDate AND UPPER(ISNULL(c.ClaimantSCOutcome,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClaimantSCClosureDate >= @StartDate AND c.ClaimantSCClosureDate <= @EndDate AND UPPER(ISNULL(c.ClaimantSCOutcome,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE ((c.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCNumber)) <> '') OR (c.ClaimantSCDiaryNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCDiaryNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Gratuity Supreme Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealCourt,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Others Supreme Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE (UPPER(ISNULL(c.Court,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')
                                ) sc_all";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(scQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@StartDate", startDate);
                                cmd.Parameters.AddWithValue("@EndDate", endDate);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        viewModel.MmrSt2Data.SupremeCourt.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                        viewModel.MmrSt2Data.SupremeCourt.EntrustedCount = reader["EntrustedCount"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCount"]) : 0;
                                        viewModel.MmrSt2Data.SupremeCourt.DisposedFavorCount = reader["DisposedFavorCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCount"]) : 0;
                                        viewModel.MmrSt2Data.SupremeCourt.DisposedAgainstCount = reader["DisposedAgainstCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedAgainstCount"]) : 0;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MMR-ST2 SupremeCourt Error] {ex.Message}");
                        }

                        // 2. HIGH COURT
                        try
                        {
                            string hcQuery = @"
                                SELECT 
                                    ISNULL(SUM(BeginningPending), 0) as BeginningPending,
                                    ISNULL(SUM(EntrustedCount), 0) as EntrustedCount,
                                    ISNULL(SUM(DisposedFavorCount), 0) as DisposedFavorCount,
                                    ISNULL(SUM(DisposedAgainstCount), 0) as DisposedAgainstCount
                                FROM (
                                    -- MVC High Court MFA
                                    SELECT 
                                        SUM(CASE WHEN ((COALESCE(ap.CorpMFAEntrustmentDate, ap.ClaimantMFAEntrustmentDate, c.EntrustmentDate, c.CreatedAt) < @StartDate)) AND (ap.ClosureDate IS NULL OR ap.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((ap.CorpMFAEntrustmentDate >= @StartDate AND ap.CorpMFAEntrustmentDate <= @EndDate) OR (ap.ClaimantMFAEntrustmentDate >= @StartDate AND ap.ClaimantMFAEntrustmentDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN ap.ClosureDate >= @StartDate AND ap.ClosureDate <= @EndDate AND UPPER(ISNULL(ap.CorpMFAOutcome, ISNULL(c.DisposalResult,''))) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)', 'SETTLED', 'CLOSED IN FAVOR') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN ap.ClosureDate >= @StartDate AND ap.ClosureDate <= @EndDate AND UPPER(ISNULL(ap.CorpMFAOutcome, ISNULL(c.DisposalResult,''))) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM APPEAL_DETAILS ap
                                    JOIN MVC_CASES c ON c.CaseID = ap.CaseID
                                    WHERE ((ap.CorpMFANumber IS NOT NULL AND RTRIM(LTRIM(ap.CorpMFANumber)) <> '') OR (ap.ClaimantMFANumber IS NOT NULL AND RTRIM(LTRIM(ap.ClaimantMFANumber)) <> '') OR (ap.CorpMFAEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(ap.CorpMFAEntrustmentNo)) <> ''))

                                    UNION ALL

                                    -- Labour High Court WP / WA
                                    SELECT 
                                        SUM(CASE WHEN ((COALESCE(c.CO_WP_EntrustmentDate, c.CO_WA_EntrustmentDate, c.EntrustmentDate, c.CreatedDate) < @StartDate)) AND (COALESCE(c.CO_Disposal_Date, c.DisposalDate) IS NULL OR COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.CO_WP_EntrustmentDate >= @StartDate AND c.CO_WP_EntrustmentDate <= @EndDate) OR (c.CO_WA_EntrustmentDate >= @StartDate AND c.CO_WA_EntrustmentDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WP_Outcome, c.CO_WA_Outcome, c.DisposalResult),'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WP_Outcome, c.CO_WA_Outcome, c.DisposalResult),'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE ((c.CO_WP_CaseNumber IS NOT NULL AND RTRIM(LTRIM(c.CO_WP_CaseNumber)) <> '') OR (c.CO_WA_CaseNumber IS NOT NULL AND RTRIM(LTRIM(c.CO_WA_CaseNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR c.CO_WP_HighCourtBench IS NOT NULL OR c.CO_WA_HighCourtBench IS NOT NULL)

                                    UNION ALL

                                    -- Gratuity High Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealCourt,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WP%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WA%')

                                    UNION ALL

                                    -- Others High Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE (UPPER(ISNULL(c.Court,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%')
                                ) hc_all";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(hcQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@StartDate", startDate);
                                cmd.Parameters.AddWithValue("@EndDate", endDate);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        viewModel.MmrSt2Data.HighCourt.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                        viewModel.MmrSt2Data.HighCourt.EntrustedCount = reader["EntrustedCount"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCount"]) : 0;
                                        viewModel.MmrSt2Data.HighCourt.DisposedFavorCount = reader["DisposedFavorCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCount"]) : 0;
                                        viewModel.MmrSt2Data.HighCourt.DisposedAgainstCount = reader["DisposedAgainstCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedAgainstCount"]) : 0;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MMR-ST2 HighCourt Error] {ex.Message}");
                        }

                        // 3. OTHER COURTS (District / MACT / Labour Court / CA / Consumer / LAC etc.)
                        try
                        {
                            string ocQuery = @"
                                SELECT 
                                    ISNULL(SUM(BeginningPending), 0) as BeginningPending,
                                    ISNULL(SUM(EntrustedCount), 0) as EntrustedCount,
                                    ISNULL(SUM(DisposedFavorCount), 0) as DisposedFavorCount,
                                    ISNULL(SUM(DisposedAgainstCount), 0) as DisposedAgainstCount
                                FROM (
                                    -- MVC District / MACT
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedAt < @StartDate)) AND (COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) IS NULL OR COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate AND COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)', 'SETTLED', 'CLOSED IN FAVOR') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate AND COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM MVC_CASES c
                                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID

                                    UNION ALL

                                    -- Labour Trial / Industrial Court (KID/ID/Arising)
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE NOT ((c.CO_WP_CaseNumber IS NOT NULL AND RTRIM(LTRIM(c.CO_WP_CaseNumber)) <> '') OR (c.CO_WA_CaseNumber IS NOT NULL AND RTRIM(LTRIM(c.CO_WA_CaseNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR (c.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SL%')

                                    UNION ALL

                                    -- Gratuity Controlling / Appellate Authority
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE NOT (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealCourt,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%SLP%' OR UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealCourt,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WP%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WA%')

                                    UNION ALL

                                    -- Others (OS, Consumer, LAC, ECA, etc.)
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE NOT (UPPER(ISNULL(c.Court,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%' OR UPPER(ISNULL(c.Court,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%')
                                ) oc_all";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(ocQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@StartDate", startDate);
                                cmd.Parameters.AddWithValue("@EndDate", endDate);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        viewModel.MmrSt2Data.OtherCourts.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                        viewModel.MmrSt2Data.OtherCourts.EntrustedCount = reader["EntrustedCount"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCount"]) : 0;
                                        viewModel.MmrSt2Data.OtherCourts.DisposedFavorCount = reader["DisposedFavorCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCount"]) : 0;
                                        viewModel.MmrSt2Data.OtherCourts.DisposedAgainstCount = reader["DisposedAgainstCount"] != DBNull.Value ? Convert.ToInt32(reader["DisposedAgainstCount"]) : 0;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MMR-ST2 OtherCourts Error] {ex.Message}");
                        }
                    }
                    else
                    {
                        // MVC CASES (Existing logic)

                        // 2. Fetch Case counts per division
                        string caseStatsQuery = @"
                            SELECT 
                                d.DivisionID,
                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND COALESCE(c.CreatedAt, c.ModifiedDate) < @StartDate))
                                        AND (COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) IS NULL OR COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) >= @StartDate)
                                        AND (c.DisposalStatus IS NULL OR UPPER(c.DisposalStatus) NOT IN ('DISPOSED', 'CLOSED') OR COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate)
                                    THEN 1 ELSE 0 END) as BeginningPending,
                                    
                                SUM(CASE WHEN 
                                        (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                        OR (c.EntrustmentDate IS NULL AND COALESCE(c.CreatedAt, c.ModifiedDate) >= @StartDate AND COALESCE(c.CreatedAt, c.ModifiedDate) <= @EndDate)
                                    THEN 1 ELSE 0 END) as EntrustedCount,
                                    
                                SUM(CASE WHEN 
                                        ((COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate AND COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) <= @EndDate) 
                                         OR (c.ClosureDate IS NOT NULL AND c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)', 'SETTLED', 'CLOSED IN FAVOR'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate AND COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) <= @EndDate) 
                                         OR (ad.AwardDate IS NOT NULL AND ad.AwardDate >= @StartDate AND ad.AwardDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED', 'AWARDED'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,
                                    
                                SUM(CASE WHEN 
                                        ((COALESCE(c.TransferDate, c.ModifiedDate, c.UpdatedAt) >= @StartDate AND COALESCE(c.TransferDate, c.ModifiedDate, c.UpdatedAt) <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.DisposalStatus,'')) = 'TRANSFERRED')
                                    THEN 1 ELSE 0 END) as TransferredCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedAt <= @EndDate))
                                        AND (COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) IS NULL OR COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate) > @EndDate)
                                        AND (c.DisposalStatus IS NULL OR UPPER(c.DisposalStatus) NOT IN ('DISPOSED', 'CLOSED') OR COALESCE(ad.DisposedOnDate, c.ClosureDate, ad.AwardDate, c.ModifiedDate, c.UpdatedAt) > @EndDate)
                                    THEN 1 ELSE 0 END) as EndingPending
                            FROM DIVISION_MASTER d
                            LEFT JOIN MVC_CASES c ON c.DivisionID = d.DivisionID
                            LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                            WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                            GROUP BY d.DivisionID";

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(caseStatsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        divData.BeginningPending = Convert.ToInt32(reader["BeginningPending"]);
                                        divData.EntrustedCount = Convert.ToInt32(reader["EntrustedCount"]);
                                        divData.DisposedFavorCount = Convert.ToInt32(reader["DisposedFavorCount"]);
                                        divData.DisposedAgainstCount = Convert.ToInt32(reader["DisposedAgainstCount"]);
                                        divData.TransferredCount = Convert.ToInt32(reader["TransferredCount"]);
                                        // Enforce 100% mathematical integrity: Ending = Beginning + Entrusted - DisposedFavor - DisposedAgainst - Transferred
                                        divData.EndingPending = Math.Max(0, divData.CalculatedEndingPending);
                                    }
                                }
                            }
                        }

                        // 3. Payments made in selected month
                        string paymentsQuery = @"
                            SELECT 
                                ISNULL(c.DivisionID, d.DivisionID) as DivisionID,
                                SUM(ISNULL(p.Amount, 0)) as TotalAmountPaid,
                                COUNT(DISTINCT b.CaseID) as PaidCasesCount
                            FROM PETTY_BILL_PAYMENTS p
                            JOIN PETTY_BILLS b ON p.BillID = b.BillID
                            LEFT JOIN MVC_CASES c ON b.CaseID = c.CaseID
                            LEFT JOIN DIVISION_MASTER d ON b.DivisionName = d.DivisionNameEnglish
                            WHERE p.ChequeDate >= @StartDate AND p.ChequeDate <= @EndDate
                              AND (p.IsCancelled IS NULL OR p.IsCancelled = 0)
                              AND ISNULL(c.DivisionID, d.DivisionID) IS NOT NULL
                              AND ISNULL(c.DivisionID, d.DivisionID) NOT IN (0, 5)
                            GROUP BY ISNULL(c.DivisionID, d.DivisionID)";

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(paymentsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        divData.CompensationPaidAmount = Convert.ToDecimal(reader["TotalAmountPaid"]);
                                        divData.CompensationPaidCaseCount = Convert.ToInt32(reader["PaidCasesCount"]);
                                    }
                                }
                            }
                        }

                        // Direct MVC_CASE_PAYMENTS integration
                        try
                        {
                            string directPayQuery = @"
                                SELECT 
                                    c.DivisionID,
                                    SUM(ISNULL(p.Amount, 0)) as TotalAmountPaid,
                                    COUNT(DISTINCT c.CaseID) as PaidCasesCount
                                FROM MVC_CASE_PAYMENTS p
                                JOIN MVC_CASES c ON p.CaseID = c.CaseID
                                WHERE p.ChequeDate >= @StartDate AND p.ChequeDate <= @EndDate
                                  AND c.DivisionID NOT IN (0, 5)
                                GROUP BY c.DivisionID";

                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(directPayQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@StartDate", startDate);
                                cmd.Parameters.AddWithValue("@EndDate", endDate);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int dId = Convert.ToInt32(reader["DivisionID"]);
                                        if (divDict.TryGetValue(dId, out var divData))
                                        {
                                            decimal extraAmt = Convert.ToDecimal(reader["TotalAmountPaid"]);
                                            int extraCnt = Convert.ToInt32(reader["PaidCasesCount"]);
                                            divData.CompensationPaidAmount += extraAmt;
                                            divData.CompensationPaidCaseCount += extraCnt;
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* table optional */ }

                        // 4. Outstanding balance pending at Accounts
                        string outstandingQuery = @"
                            SELECT 
                                c.DivisionID,
                                SUM(ISNULL(ad.AwardAmount, 0) - ISNULL(paid.PaidTotal, 0)) as OutstandingBalance
                            FROM MVC_CASES c
                            JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                            LEFT JOIN (
                                SELECT CaseID, SUM(Amount) as PaidTotal
                                FROM (
                                    SELECT b.CaseID, p.Amount 
                                    FROM PETTY_BILL_PAYMENTS p 
                                    JOIN PETTY_BILLS b ON p.BillID = b.BillID 
                                    WHERE (p.IsCancelled IS NULL OR p.IsCancelled = 0)
                                    UNION ALL
                                    SELECT p2.CaseID, p2.Amount
                                    FROM MVC_CASE_PAYMENTS p2
                                ) allPaid
                                GROUP BY CaseID
                            ) paid ON paid.CaseID = c.CaseID
                            WHERE (c.DisposalStatus IS NULL OR UPPER(c.DisposalStatus) <> 'CLOSED')
                              AND c.DivisionID NOT IN (0, 5)
                              AND ad.AwardAmount IS NOT NULL AND ad.AwardAmount > 0
                            GROUP BY c.DivisionID";

                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(outstandingQuery, conn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int dId = Convert.ToInt32(reader["DivisionID"]);
                                    if (divDict.TryGetValue(dId, out var divData))
                                    {
                                        decimal bal = Convert.ToDecimal(reader["OutstandingBalance"]);
                                        divData.OutstandingBalanceAmount = bal > 0 ? bal : 0;
                                    }
                                }
                            }
                        }
                    }
                // Fetch High Court & Supreme Court Data for Report
                try
                {
                    // 1. MFA CASES (MVC Appeals)
                    string mfaQuery = @"
                        SELECT 
                            SUM(CASE WHEN 
                                    ((COALESCE(ap.CorpMFAEntrustmentDate, ap.ClaimantMFAEntrustmentDate, c.EntrustmentDate, c.CreatedAt) < @StartDate))
                                    AND (ap.ClosureDate IS NULL OR ap.ClosureDate >= @StartDate)
                                THEN 1 ELSE 0 END) as BeginningPending,
                                
                            SUM(CASE WHEN (ap.CorpMFANumber IS NOT NULL AND RTRIM(LTRIM(ap.CorpMFANumber)) <> '') AND ((ap.CorpMFAEntrustmentDate >= @StartDate AND ap.CorpMFAEntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate))
                                THEN 1 ELSE 0 END) as EntrustedCorp,

                            SUM(CASE WHEN (ap.ClaimantMFANumber IS NOT NULL AND RTRIM(LTRIM(ap.ClaimantMFANumber)) <> '') AND ((ap.ClaimantMFAEntrustmentDate >= @StartDate AND ap.ClaimantMFAEntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate))
                                THEN 1 ELSE 0 END) as EntrustedClaimant,

                            SUM(CASE WHEN ap.ClosureDate >= @StartDate AND ap.ClosureDate <= @EndDate
                                THEN 1 ELSE 0 END) as TotalDisposed,

                            SUM(CASE WHEN ap.ClosureDate >= @StartDate AND ap.ClosureDate <= @EndDate AND UPPER(ISNULL(ap.CorpMFAOutcome,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'SETTLED', 'CLOSED IN FAVOR')
                                THEN 1 ELSE 0 END) as DisposedFavorCorp,

                            SUM(CASE WHEN ap.ClosureDate >= @StartDate AND ap.ClosureDate <= @EndDate AND UPPER(ISNULL(ap.CorpMFAOutcome,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE', 'PARTIALLY ALLOWED')
                                THEN 1 ELSE 0 END) as DisposedFavorClaimant,

                            SUM(CASE WHEN ((COALESCE(ap.CorpMFAEntrustmentDate, ap.ClaimantMFAEntrustmentDate, c.EntrustmentDate, c.CreatedAt) <= @EndDate))
                                    AND (ap.ClosureDate IS NULL OR ap.ClosureDate > @EndDate)
                                THEN 1 ELSE 0 END) as EndingPending
                        FROM APPEAL_DETAILS ap
                        JOIN MVC_CASES c ON c.CaseID = ap.CaseID
                        WHERE (ap.CorpMFANumber IS NOT NULL OR ap.ClaimantMFANumber IS NOT NULL OR ap.CorpMFAEntrustmentNo IS NOT NULL OR UPPER(ISNULL(ap.CorpMFAStatus,'')) LIKE '%MFA%')";

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(mfaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                viewModel.HighCourtData.MfaCases.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                viewModel.HighCourtData.MfaCases.EntrustedCorp = reader["EntrustedCorp"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCorp"]) : 0;
                                viewModel.HighCourtData.MfaCases.EntrustedClaimant = reader["EntrustedClaimant"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedClaimant"]) : 0;
                                viewModel.HighCourtData.MfaCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.MfaCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                int rawDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.MfaCases.TotalDisposed = Math.Max(rawDisposed, viewModel.HighCourtData.MfaCases.DisposedFavorCorp + viewModel.HighCourtData.MfaCases.DisposedFavorClaimant);
                                // Ensure 100% mathematical audit balance: Ending = Beginning + EntrustedCorp + EntrustedClaimant - TotalDisposed
                                viewModel.HighCourtData.MfaCases.EndingPending = Math.Max(0, viewModel.HighCourtData.MfaCases.BeginningPending + viewModel.HighCourtData.MfaCases.EntrustedCorp + viewModel.HighCourtData.MfaCases.EntrustedClaimant - viewModel.HighCourtData.MfaCases.TotalDisposed);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HighCourt MFA Error] {ex.Message}");
                }

                // 2. LABOUR W.P. CASES
                try
                {
                    string wpQuery = @"
                        SELECT 
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                            SUM(CASE WHEN (c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '') AND ((c.CO_WP_EntrustmentDate >= @StartDate AND c.CO_WP_EntrustmentDate <= @EndDate) OR (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCorp,
                            SUM(CASE WHEN (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '') AND ((c.CO_Claimant_EntrustmentDate >= @StartDate AND c.CO_Claimant_EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedClaimant,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate THEN 1 ELSE 0 END) as TotalDisposed,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WP_Outcome, c.DisposalResult),'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCorp,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WP_Outcome, c.DisposalResult),'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedFavorClaimant,
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate <= @EndDate)) AND (COALESCE(c.CO_Disposal_Date, c.DisposalDate) IS NULL OR COALESCE(c.CO_Disposal_Date, c.DisposalDate) > @EndDate) THEN 1 ELSE 0 END) as EndingPending
                        FROM LABOUR_CASES c
                        WHERE (UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%W.P%' OR (c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '') OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '') OR c.CO_WP_HighCourtBench IS NOT NULL OR c.CO_Claimant_HighCourtBench IS NOT NULL)";

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(wpQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                viewModel.HighCourtData.LabourWpCases.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.EntrustedCorp = reader["EntrustedCorp"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCorp"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.EntrustedClaimant = reader["EntrustedClaimant"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedClaimant"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                int rawDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.TotalDisposed = Math.Max(rawDisposed, viewModel.HighCourtData.LabourWpCases.DisposedFavorCorp + viewModel.HighCourtData.LabourWpCases.DisposedFavorClaimant);
                                // Ensure 100% mathematical audit balance: Ending = Beginning + EntrustedCorp + EntrustedClaimant - TotalDisposed
                                viewModel.HighCourtData.LabourWpCases.EndingPending = Math.Max(0, viewModel.HighCourtData.LabourWpCases.BeginningPending + viewModel.HighCourtData.LabourWpCases.EntrustedCorp + viewModel.HighCourtData.LabourWpCases.EntrustedClaimant - viewModel.HighCourtData.LabourWpCases.TotalDisposed);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HighCourt WP Error] {ex.Message}");
                }

                // 3. LABOUR W.A. CASES
                try
                {
                    string waQuery = @"
                        SELECT 
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                            SUM(CASE WHEN (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '') AND ((c.CO_WA_EntrustmentDate >= @StartDate AND c.CO_WA_EntrustmentDate <= @EndDate) OR (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCorp,
                            0 as EntrustedClaimant,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate THEN 1 ELSE 0 END) as TotalDisposed,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WA_Outcome, c.DisposalResult),'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCorp,
                            SUM(CASE WHEN COALESCE(c.CO_Disposal_Date, c.DisposalDate) >= @StartDate AND COALESCE(c.CO_Disposal_Date, c.DisposalDate) <= @EndDate AND UPPER(ISNULL(COALESCE(c.CO_WA_Outcome, c.DisposalResult),'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedFavorClaimant,
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate <= @EndDate)) AND (COALESCE(c.CO_Disposal_Date, c.DisposalDate) IS NULL OR COALESCE(c.CO_Disposal_Date, c.DisposalDate) > @EndDate) THEN 1 ELSE 0 END) as EndingPending
                        FROM LABOUR_CASES c
                        WHERE (UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%W.A%' OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '') OR c.CO_WA_HighCourtBench IS NOT NULL)";

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(waQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                viewModel.HighCourtData.LabourWaCases.BeginningPending = reader["BeginningPending"] != DBNull.Value ? Convert.ToInt32(reader["BeginningPending"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.EntrustedCorp = reader["EntrustedCorp"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedCorp"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.EntrustedClaimant = reader["EntrustedClaimant"] != DBNull.Value ? Convert.ToInt32(reader["EntrustedClaimant"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                int rawDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.TotalDisposed = Math.Max(rawDisposed, viewModel.HighCourtData.LabourWaCases.DisposedFavorCorp + viewModel.HighCourtData.LabourWaCases.DisposedFavorClaimant);
                                // Ensure 100% mathematical audit balance: Ending = Beginning + EntrustedCorp + EntrustedClaimant - TotalDisposed
                                viewModel.HighCourtData.LabourWaCases.EndingPending = Math.Max(0, viewModel.HighCourtData.LabourWaCases.BeginningPending + viewModel.HighCourtData.LabourWaCases.EntrustedCorp + viewModel.HighCourtData.LabourWaCases.EntrustedClaimant - viewModel.HighCourtData.LabourWaCases.TotalDisposed);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HighCourt WA Error] {ex.Message}");
                }

                // 5. SLP CASES (Supreme Court)
                try
                {
                    string slpMvcQuery = "SELECT COUNT(*) FROM APPEAL_DETAILS WHERE (CorpSCNumber IS NOT NULL OR ClaimantSCNumber IS NOT NULL OR UPPER(ISNULL(CorpSCStatus,'')) LIKE '%SLP%' OR UPPER(ISNULL(ClaimantSCStatus,'')) LIKE '%SLP%')";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(slpMvcQuery, conn))
                    {
                        viewModel.HighCourtData.SlpMvcCasesCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HighCourt SLP MVC Error] {ex.Message}");
                }

                try
                {
                    string slpLabourQuery = "SELECT COUNT(*) FROM LABOUR_CASES WHERE (ClaimantSCNumber IS NOT NULL OR ClaimantSCDiaryNumber IS NOT NULL OR UPPER(ISNULL(CaseType,'')) LIKE '%SLP%')";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(slpLabourQuery, conn))
                    {
                        viewModel.HighCourtData.SlpLabourCasesCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HighCourt SLP Labour Error] {ex.Message}");
                }
            }
        }

        return View(viewModel);
    }

        [HttpGet]
        public JsonResult GetCaseDetails(string mvcNo, int year, string mactName = "")
        {
            try
            {
                // Get User Division
                var divIdString = User.FindFirstValue("DivisionID");
                int userDivisionId = int.TryParse(divIdString, out int id) ? id : 0;

                // Pass search parameter to bypass Central Office filters
                string searchTerm = $"{mvcNo}/{year}";
                
                var allCases = _caseRepo.GetAllCases(divisionId: 0, pageNumber: 1, pageSize: 100000, search: searchTerm);
                
                // 1. Filter by MVC year
                var candidates = allCases.Where(c => c.MVCYear == year).ToList();

                // Exact match on MVC No
                var matches = candidates.Where(c => c.MVCNo.Equals(mvcNo, StringComparison.OrdinalIgnoreCase)).ToList();
                
                // If no exact match, try fuzzy match (leading zeros)
                if (!matches.Any())
                {
                    var mvcNoTrimmed = mvcNo.TrimStart('0');
                    matches = candidates.Where(c => c.MVCNo.TrimStart('0').Equals(mvcNoTrimmed, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!matches.Any())
                {
                    var casesThisYear = candidates.Take(5).Select(c => c.MVCNo).ToList();
                    var suggestion = casesThisYear.Any() 
                        ? $" Available cases in {year}: {string.Join(", ", casesThisYear)}" 
                        : $" No cases found for year {year}";
                    return Json(new { success = false, message = $"Case {mvcNo}/{year} not found.{suggestion}" });
                }

                // 2. If MACT Name is provided, filter by it
                MVCCaseViewModel caseDetails = null;
                if (!string.IsNullOrWhiteSpace(mactName))
                {
                    caseDetails = matches.FirstOrDefault(c => c.MACTName.Contains(mactName, StringComparison.OrdinalIgnoreCase));
                    if (caseDetails == null)
                    {
                         return Json(new { success = false, message = $"Case found, but MACT '{mactName}' does not match. Found MACT: {matches.First().MACTName}" });
                    }
                }
                else
                {
                    if (userDivisionId != 0 && userDivisionId != 5)
                    {
                        var divisionMatch = matches.FirstOrDefault(c => c.DivisionID == userDivisionId);
                        if (divisionMatch != null) caseDetails = divisionMatch;
                    }
                    if (caseDetails == null) caseDetails = matches.First();
                }

                // Get full case details
                var fullCase = _caseRepo.GetCaseById(caseDetails.CaseID);
                _logger.LogInformation("GetCaseDetails: CaseID={CaseID}, MVC={MVCNo}/{MVCYear}", fullCase.CaseID, fullCase.MVCNo, fullCase.MVCYear);

                // Get EP / Audit details and verified payments
                var epDetails = _epRepo.GetEPByCaseId(fullCase.CaseID);
                
                List<PreviousPayment> payments = new();
                if (epDetails != null && epDetails.Payments != null)
                {
                    payments.AddRange(epDetails.Payments.Select(p => new PreviousPayment
                    {
                        ChequeNumber = p.ChequeNumber ?? "",
                        ChequeDate = p.PaymentDate,
                        Amount = p.Amount,
                        IsSelected = true,
                        Remarks = "EP Payment"
                    }));
                }

                // Get Advance Payments
                var advancePayments = _casePaymentRepo.GetPaymentsByCaseId(fullCase.CaseID);
                if (advancePayments.Any())
                {
                    payments.AddRange(advancePayments.Select(p => new PreviousPayment
                    {
                        ChequeNumber = p.ChequeNumber ?? "",
                        ChequeDate = p.ChequeDate ?? p.CreatedDate,
                        Amount = p.Amount,
                        IsSelected = true,
                        Remarks = $"{p.PaymentType ?? "Advance"} Payment"
                    }));
                }

                // Get Compliance Payment
                if (fullCase.LinkedAppeal != null && fullCase.LinkedAppeal.FinalComplianceStatus == "Complied" && fullCase.LinkedAppeal.AmountDeposited.HasValue)
                {
                    Console.WriteLine($"[DEBUG] Compliance Payment found: ₹{fullCase.LinkedAppeal.AmountDeposited.Value}");
                    // Extract Cheque Number from Remarks using Regex
                    // Matches "Cheque No: 123", "Ref No: ABC", "Cheque/Ref No: 123", etc.
                    string chqNo = "";
                    if (!string.IsNullOrEmpty(fullCase.LinkedAppeal.FinalRemarks))
                    {
                         var match = System.Text.RegularExpressions.Regex.Match(
                             fullCase.LinkedAppeal.FinalRemarks, 
                             @"(?:Cheque|Ref|DD)(?:[\/\s\w]*)(?:No\.?|Number)?[\s:\-]+([A-Za-z0-9]+)", 
                             System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                         if (match.Success) chqNo = match.Groups[1].Value;
                    }

                    payments.Add(new PreviousPayment
                    {
                        Amount = fullCase.LinkedAppeal.AmountDeposited.Value,
                        ChequeDate = fullCase.LinkedAppeal.FinalComplianceDate,
                        ChequeNumber = chqNo,
                        IsSelected = true,
                        Remarks = $"Compliance Payment: {fullCase.LinkedAppeal.FinalRemarks}"
                    });
                }
                else
                {
                    // No compliance payment - normal state, no log needed
                }

                // Get Payments from previously saved Petty Bills
                var billPayments = _pettyBillRepo.GetAllBillPaymentsByCaseId(fullCase.CaseID);
                if (billPayments != null)
                {
                    foreach (var bp in billPayments)
                    {
                        // Better duplicate check: 
                        // If same Amount AND same Cheque Number (trimmed) -> Duplicate.
                        // If no Cheque Number, same Amount AND Date (within 2 days) -> Duplicate.
                        bool exists = payments.Any(p => {
                            bool amountMatch = Math.Abs(p.Amount - bp.Amount) < 0.01m;
                            string pChq = (p.ChequeNumber ?? "").Trim();
                            string bpChq = (bp.ChequeNumber ?? "").Trim();
                            
                            bool exactChqMatch = !string.IsNullOrEmpty(pChq) && !string.IsNullOrEmpty(bpChq) && 
                                               string.Equals(pChq, bpChq, StringComparison.OrdinalIgnoreCase);
                            
                            bool dateMatch = p.ChequeDate.HasValue && bp.ChequeDate.HasValue && 
                                           Math.Abs((p.ChequeDate.Value - bp.ChequeDate.Value).TotalDays) <= 2;

                            if (amountMatch) {
                                if (exactChqMatch) return true;
                                if (string.IsNullOrEmpty(pChq) && string.IsNullOrEmpty(bpChq) && dateMatch) return true;
                            }
                            return false;
                        });

                        if (!exists)
                        {
                            // Add consistent remark if missing
                            if (string.IsNullOrEmpty(bp.Remarks)) bp.Remarks = "From Previous Bill";
                            else if (!bp.Remarks.Contains("From Previous Bill") && !bp.Remarks.Contains("Payment")) bp.Remarks += " (From Bill)";

                            payments.Add(bp);
                        }
                    }
                }
                
                // Sort by date
                payments = payments.OrderBy(p => p.ChequeDate).ToList();

                var result = new
                {
                    success = true,
                    caseID = fullCase.CaseID,
                    mvcNo = fullCase.MVCNo,
                    mvcYear = fullCase.MVCYear,
                    mactName = fullCase.MACTName,
                    divisionName = fullCase.DivisionName,
                    vehicleNo = fullCase.VehicleNo,
                    accidentDate = fullCase.AccidentDate?.ToString("dd-MM-yyyy"),
                    petitionDate = fullCase.AdverseAward?.ClaimPetitionDate?.ToString("dd-MM-yyyy") ?? "",
                    petitionerName = fullCase.Petitioners.FirstOrDefault()?.PetitionerName ?? "",
                    appealNumber = fullCase.LinkedAppeal?.CorpMFANumber ?? "",
                    awardAmount = fullCase.AdverseAward?.AwardAmount ?? 0,
                    liableAmount = fullCase.AdverseAward?.LiableAmount ?? 0,
                    isAppeal = !string.IsNullOrEmpty(fullCase.LinkedAppeal?.CorpMFANumber) || !string.IsNullOrEmpty(fullCase.LinkedAppeal?.ClaimantMFANumber),
                    calculationMethod = epDetails?.CalculationMethod ?? "PrincipalDeduction",
                    realizationDate = epDetails?.RealizationDate?.ToString("yyyy-MM-dd") ?? "",
                    interestRate = epDetails?.InterestRate ?? fullCase.AdverseAward?.InterestRate ?? 6.0m,
                    epId = epDetails?.EPID ?? 0,
                    auditComplianceStatus = epDetails?.ComplianceStatus ?? "Pending",
                    auditTotalPaid = epDetails?.AmountPaid ?? 0,
                    previousPayments = payments
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCaseDetails for MVC {MVCNo}/{Year}", mvcNo, year);
                return Json(new { success = false, message = "An error occurred while retrieving case details." });
            }
        }

        [HttpGet]
        public IActionResult PrintBill(int id)
        {
            var model = _pettyBillRepo.GetPettyBillById(id);
            if (model == null) return NotFound();
            return View("PrintBill", model);
        }

        [HttpGet]
        public JsonResult GetFullBillsByCase(int caseId, string mvcNo, int mvcYear)
        {
            try
            {
                var bills = _pettyBillRepo.GetFullPettyBillsByCaseIdOrMVC(caseId, mvcNo, mvcYear);
                return Json(new { success = true, bills = bills });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving full petty bills for CaseID {CaseID} / {MVCNo}/{Year}", caseId, mvcNo, mvcYear);
                return Json(new { success = false, message = "Error loading petty bills." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateBill(PettyBillViewModel model)
        {
            return View("PrintBill", model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public JsonResult SaveBill([FromBody] PettyBillViewModel? model)
        {
            if (model == null)
            {
                _logger.LogWarning("SaveBill invoked with null model payload.");
                return Json(new { success = false, message = "Invalid bill data received. Please refresh and try again." });
            }

            try
            {
                int billId;
                
                if (model.BillID > 0)
                {
                    // Update existing bill
                    bool success = _pettyBillRepo.UpdatePettyBill(model);
                    billId = model.BillID;
                }
                else
                {
                    // Save new bill
                    billId = _pettyBillRepo.SavePettyBill(model);
                }

                // Link bill to EP/Audit record
                if (model.CaseID.HasValue && model.CaseID.Value > 0)
                {
                    try
                    {
                        _epRepo.UpdatePettyBillLink(model.CaseID.Value, model.BillDate ?? DateTime.Today, model.NetPayable);
                    }
                    catch (Exception exLink)
                    {
                        _logger.LogWarning(exLink, "Could not update EP PettyBillLink for CaseID {CaseID}", model.CaseID.Value);
                    }
                }

                return Json(new { success = true, billId = billId, message = model.BillID > 0 ? "Bill updated successfully!" : "Bill saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving petty bill for CaseID {CaseID}", model?.CaseID);
                return Json(new { success = false, message = $"Error saving bill: {ex.Message}" });
            }
        }

        [HttpGet]
        public JsonResult GetBill(int billId)
        {
            try
            {
                var bill = _pettyBillRepo.GetPettyBillById(billId);
                
                if (bill == null)
                {
                    return Json(new { success = false, message = "Bill not found" });
                }
                
                return Json(new { success = true, bill = bill });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bill {BillID}", billId);
                return Json(new { success = false, message = "Error retrieving bill. Please try again." });
            }
        }

        [HttpGet]
        public JsonResult GetBillsByMVC(string mvcNo, int mvcYear)
        {
            try
            {
                var bills = _pettyBillRepo.GetPettyBillsByMVC(mvcNo, mvcYear);
                
                return Json(new { success = true, bills = bills });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bills for MVC {MVCNo}/{MVCYear}", mvcNo, mvcYear);
                return Json(new { success = false, message = "Error retrieving bills. Please try again." });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public JsonResult DeleteBill(int billId)
        {
            try
            {
                bool success = _pettyBillRepo.DeletePettyBill(billId);
                
                if (success)
                {
                    return Json(new { success = true, message = "Bill deleted successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Bill not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bill {BillID}", billId);
                return Json(new { success = false, message = $"Error deleting bill: {ex.Message}" });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public JsonResult DeletePaymentRow(int paymentId)
        {
            try
            {
                // This only works for payments stored in PETTY_BILL_PAYMENTS (PaymentID > 0)
                if (paymentId <= 0)
                {
                    return Json(new { success = false, message = "Only manual Petty Bill entries can be deleted from database here." });
                }

                bool success = _pettyBillRepo.DeletePettyBillPayment(paymentId);
                return Json(new { success = success, message = success ? "Payment record deleted from database." : "Record not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment row {PaymentID}", paymentId);
                return Json(new { success = false, message = $"Error deleting payment: {ex.Message}" });
            }
        }
    }
}
