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
            ViewBag.MACTList = _masterRepo.GetAllMACTs();
            return View();
        }

        [HttpGet]
        public IActionResult CentralOffice(int? month, int? year, string? module, string? caseType, string? view)
        {
            var divId = User.FindFirstValue("DivisionID");
            if (!(divId == "0" || divId == "5" || string.IsNullOrEmpty(divId) || User.IsInRole("Admin") || User.IsInRole("CentralOffice") || User.IsInRole("MD") || User.IsInRole("CLO")))
            {
                TempData["ErrorMessage"] = "Access Denied: Only Central Office can access this report.";
                return RedirectToAction("Index", "Home");
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
                        if (selectedCaseType.Equals("KID", StringComparison.OrdinalIgnoreCase))
                        {
                            caseTypeFilter = " AND (UPPER(c.CaseType) = 'KID') ";
                        }
                        else if (selectedCaseType.Equals("ID", StringComparison.OrdinalIgnoreCase))
                        {
                            caseTypeFilter = " AND (UPPER(c.CaseType) = 'ID') ";
                        }
                        else if (selectedCaseType.Equals("ArisingApplication", StringComparison.OrdinalIgnoreCase) || selectedCaseType.Equals("Arising Application", StringComparison.OrdinalIgnoreCase))
                        {
                            caseTypeFilter = " AND (c.IsArisingApplication = 1 OR UPPER(ISNULL(c.CaseType,'')) LIKE '%ARISING%') ";
                        }

                        // 2. Fetch Labour Case counts per division
                        string labourStatsQuery = $@"
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
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'PARTIALLY FAVOR'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) 
                                         OR (c.DisposalDate IS NULL AND c.CaseStatus = 'Disposed' AND c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                SUM(CASE WHEN 
                                        (c.TransferredFromDivisionID IS NOT NULL AND c.TransferredFromDivisionID > 0)
                                        AND ((c.TransferDate >= @StartDate AND c.TransferDate <= @EndDate) OR (c.TransferDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate))
                                    THEN 1 ELSE 0 END) as ReceivedFromOtherDivisionCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.CaseStatus,'')) = 'TRANSFERRED')
                                    THEN 1 ELSE 0 END) as TransferredCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                        AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate)
                                        AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed' OR c.ModifiedDate > @EndDate)
                                    THEN 1 ELSE 0 END) as EndingPending
                            FROM DIVISION_MASTER d
                            LEFT JOIN LABOUR_CASES c ON c.DivisionID = d.DivisionID {caseTypeFilter}
                            WHERE d.IsActive = 1 AND d.DivisionID NOT IN (0, 5)
                            GROUP BY d.DivisionID";

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
                                        divData.EndingPending = Convert.ToInt32(reader["EndingPending"]);
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
                            LEFT JOIN GRA_CASES c ON (CAST(c.DivisionCode AS NVARCHAR(50)) = CAST(d.DivisionID AS NVARCHAR(50)) OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionNameEnglish OR TRY_CAST(c.DivisionCode AS INT) = d.DivisionID) {caseTypeFilter}
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
                                        divData.EndingPending = Convert.ToInt32(reader["EndingPending"]);
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
                                JOIN GRA_CASES c ON (CAST(c.DivisionCode AS NVARCHAR(50)) = CAST(d.DivisionID AS NVARCHAR(50)) OR CAST(c.DivisionCode AS NVARCHAR(50)) = d.DivisionNameEnglish OR TRY_CAST(c.DivisionCode AS INT) = d.DivisionID) {caseTypeFilter}
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
                                        AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed' OR c.DisposalStatus <> 'CLOSED AT DIVISION LEVEL' OR c.ModifiedDate >= @StartDate)
                                    THEN 1 ELSE 0 END) as BeginningPending,
                                    
                                SUM(CASE WHEN 
                                        (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                        OR (c.EntrustmentDate IS NULL AND c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)
                                    THEN 1 ELSE 0 END) as EntrustedCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate) OR (c.ModifiedDate >= @StartDate AND c.ModifiedDate <= @EndDate))
                                        AND (UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedDate <= @EndDate))
                                        AND (c.ClosureDate IS NULL OR c.ClosureDate > @EndDate)
                                        AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed' OR c.ModifiedDate > @EndDate)
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
                                        divData.EndingPending = Convert.ToInt32(reader["EndingPending"]);
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
                                    SUM(BeginningPending) as BeginningPending,
                                    SUM(EntrustedCount) as EntrustedCount,
                                    SUM(DisposedFavorCount) as DisposedFavorCount,
                                    SUM(DisposedAgainstCount) as DisposedAgainstCount
                                FROM (
                                    -- MVC Supreme Court SLP
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedAt < @StartDate)) AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM MVC_CASES c
                                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                                    WHERE ((c.CorpSLPNo IS NOT NULL AND RTRIM(LTRIM(c.CorpSLPNo)) <> '') OR (c.ClaimantSLPNo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSLPNo)) <> '') OR UPPER(ISNULL(c.CurrentStatus,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Labour Supreme Court SLP
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE ((c.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Gratuity Supreme Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Others Supreme Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')
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
                        catch { }

                        // 2. HIGH COURT
                        try
                        {
                            string hcQuery = @"
                                SELECT 
                                    SUM(BeginningPending) as BeginningPending,
                                    SUM(EntrustedCount) as EntrustedCount,
                                    SUM(DisposedFavorCount) as DisposedFavorCount,
                                    SUM(DisposedAgainstCount) as DisposedAgainstCount
                                FROM (
                                    -- MVC High Court MFA
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedAt < @StartDate)) AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM MVC_CASES c
                                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                                    WHERE ((c.CorpMFANo IS NOT NULL AND RTRIM(LTRIM(c.CorpMFANo)) <> '') OR (c.ClaimantMFANo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantMFANo)) <> '') OR (c.MFAEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(c.MFAEntrustmentNo)) <> '') OR UPPER(ISNULL(c.CurrentStatus,'')) LIKE '%MFA%')
                                      AND NOT ((c.CorpSLPNo IS NOT NULL AND RTRIM(LTRIM(c.CorpSLPNo)) <> '') OR (c.ClaimantSLPNo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSLPNo)) <> ''))

                                    UNION ALL

                                    -- Labour High Court WP / WA
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE (UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR c.HighCourtBench IS NOT NULL)
                                      AND NOT ((c.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Gratuity High Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WP%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WA%')
                                      AND NOT (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Others High Court
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE (UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%')
                                      AND NOT (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')
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
                        catch { }

                        // 3. OTHER COURTS (District / MACT / Labour Court / CA / Consumer / LAC etc.)
                        try
                        {
                            string ocQuery = @"
                                SELECT 
                                    SUM(BeginningPending) as BeginningPending,
                                    SUM(EntrustedCount) as EntrustedCount,
                                    SUM(DisposedFavorCount) as DisposedFavorCount,
                                    SUM(DisposedAgainstCount) as DisposedAgainstCount
                                FROM (
                                    -- MVC District / MACT
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedAt < @StartDate)) AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM MVC_CASES c
                                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                                    WHERE NOT ((c.CorpSLPNo IS NOT NULL AND RTRIM(LTRIM(c.CorpSLPNo)) <> '') OR (c.ClaimantSLPNo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSLPNo)) <> '') OR UPPER(ISNULL(c.CurrentStatus,'')) LIKE '%SLP%')
                                      AND NOT ((c.CorpMFANo IS NOT NULL AND RTRIM(LTRIM(c.CorpMFANo)) <> '') OR (c.ClaimantMFANo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantMFANo)) <> '') OR (c.MFAEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(c.MFAEntrustmentNo)) <> '') OR UPPER(ISNULL(c.CurrentStatus,'')) LIKE '%MFA%')

                                    UNION ALL

                                    -- Labour Trial / Industrial Court (KID/ID/Arising)
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM LABOUR_CASES c
                                    WHERE NOT (UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR c.HighCourtBench IS NOT NULL OR (c.ClaimantSCNumber IS NOT NULL AND RTRIM(LTRIM(c.ClaimantSCNumber)) <> '') OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%')

                                    UNION ALL

                                    -- Gratuity Controlling / Appellate Authority
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM GRA_CASES c
                                    WHERE NOT (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%SLP%' OR UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WP%' OR UPPER(ISNULL(c.AppealNumber,'')) LIKE '%WA%')

                                    UNION ALL

                                    -- Others (OS, Consumer, LAC, ECA, etc.)
                                    SELECT 
                                        SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.ClosureDate IS NULL OR c.ClosureDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                                        SUM(CASE WHEN ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCount,
                                        SUM(CASE WHEN c.ClosureDate >= @StartDate AND c.ClosureDate <= @EndDate AND UPPER(ISNULL(c.Result,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE') THEN 1 ELSE 0 END) as DisposedAgainstCount
                                    FROM OTHER_CASES c
                                    WHERE NOT (UPPER(ISNULL(c.CourtType,'')) LIKE '%SUPREME%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%SLP%' OR UPPER(ISNULL(c.CourtType,'')) LIKE '%HIGH%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%')
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
                        catch { }
                    }
                    else
                    {
                        // MVC CASES (Existing logic)

                        // 2. Fetch Case counts per division
                        string caseStatsQuery = @"
                            SELECT 
                                d.DivisionID,
                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedAt < @StartDate))
                                        AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate >= @StartDate)
                                        AND (c.DisposalStatus IS NULL OR c.DisposalStatus <> 'CLOSED' OR c.UpdatedAt >= @StartDate)
                                    THEN 1 ELSE 0 END) as BeginningPending,
                                    
                                SUM(CASE WHEN 
                                        (c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate)
                                        OR (c.EntrustmentDate IS NULL AND c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate)
                                    THEN 1 ELSE 0 END) as EntrustedCount,
                                    
                                SUM(CASE WHEN 
                                        ((ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate) 
                                         OR (ad.DisposedOnDate IS NULL AND c.DisposalStatus = 'DISPOSED' AND c.UpdatedAt >= @StartDate AND c.UpdatedAt <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED', 'EX-PARTE', 'FAVOR (SETTLED)'))
                                    THEN 1 ELSE 0 END) as DisposedFavorCount,
                                    
                                SUM(CASE WHEN 
                                        ((ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate) 
                                         OR (ad.DisposedOnDate IS NULL AND c.DisposalStatus = 'DISPOSED' AND c.UpdatedAt >= @StartDate AND c.UpdatedAt <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE'))
                                    THEN 1 ELSE 0 END) as DisposedAgainstCount,
                                    
                                SUM(CASE WHEN 
                                        ((ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate) OR (c.UpdatedAt >= @StartDate AND c.UpdatedAt <= @EndDate))
                                        AND (UPPER(ISNULL(c.DisposalResult,'')) = 'TRANSFERRED' OR UPPER(ISNULL(c.DisposalStatus,'')) = 'TRANSFERRED')
                                    THEN 1 ELSE 0 END) as TransferredCount,

                                SUM(CASE WHEN 
                                        ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.EntrustmentDate IS NULL AND c.CreatedAt <= @EndDate))
                                        AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate > @EndDate)
                                        AND (c.DisposalStatus IS NULL OR c.DisposalStatus <> 'DISPOSED' OR c.UpdatedAt > @EndDate)
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
                                        divData.EndingPending = Convert.ToInt32(reader["EndingPending"]);
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

                        // Direct MVC_CASE_PAYMENTS fallback
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
                                            if (divData.CompensationPaidAmount == 0)
                                            {
                                                divData.CompensationPaidAmount = extraAmt;
                                                divData.CompensationPaidCaseCount = extraCnt;
                                            }
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
                                SELECT b.CaseID, SUM(p.Amount) as PaidTotal 
                                FROM PETTY_BILL_PAYMENTS p 
                                JOIN PETTY_BILLS b ON p.BillID = b.BillID 
                                WHERE (p.IsCancelled IS NULL OR p.IsCancelled = 0) 
                                GROUP BY b.CaseID
                            ) paid ON paid.CaseID = c.CaseID
                            WHERE (c.DisposalStatus IS NULL OR c.DisposalStatus <> 'CLOSED')
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
                                    ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.EntrustmentDate IS NULL AND c.CreatedAt < @StartDate))
                                    AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate >= @StartDate)
                                THEN 1 ELSE 0 END) as BeginningPending,
                                
                            SUM(CASE WHEN (c.CorpMFANo IS NOT NULL AND RTRIM(LTRIM(c.CorpMFANo)) <> '') AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate))
                                THEN 1 ELSE 0 END) as EntrustedCorp,

                            SUM(CASE WHEN (c.ClaimantMFANo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantMFANo)) <> '') AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt >= @StartDate AND c.CreatedAt <= @EndDate))
                                THEN 1 ELSE 0 END) as EntrustedClaimant,

                            SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate
                                THEN 1 ELSE 0 END) as TotalDisposed,

                            SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED')
                                THEN 1 ELSE 0 END) as DisposedFavorCorp,

                            SUM(CASE WHEN ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED', 'ADVERSE')
                                THEN 1 ELSE 0 END) as DisposedFavorClaimant,

                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.CreatedAt <= @EndDate))
                                    AND (ad.DisposedOnDate IS NULL OR ad.DisposedOnDate > @EndDate)
                                THEN 1 ELSE 0 END) as EndingPending
                        FROM MVC_CASES c
                        LEFT JOIN MVC_CASE_ADVERSE_DETAILS ad ON ad.CaseID = c.CaseID
                        WHERE (c.CorpMFANo IS NOT NULL OR c.ClaimantMFANo IS NOT NULL OR c.MFAEntrustmentNo IS NOT NULL OR UPPER(ISNULL(c.CurrentStatus,'')) LIKE '%MFA%')";

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
                                viewModel.HighCourtData.MfaCases.TotalDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.MfaCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.MfaCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                viewModel.HighCourtData.MfaCases.EndingPending = reader["EndingPending"] != DBNull.Value ? Convert.ToInt32(reader["EndingPending"]) : 0;
                            }
                        }
                    }
                }
                catch { }

                // 2. LABOUR W.P. CASES
                try
                {
                    string wpQuery = @"
                        SELECT 
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                            SUM(CASE WHEN UPPER(ISNULL(c.FiledBy,'CORPORATION')) LIKE '%CORP%' AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCorp,
                            SUM(CASE WHEN UPPER(ISNULL(c.FiledBy,'')) LIKE '%CLAIMANT%' AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedClaimant,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate THEN 1 ELSE 0 END) as TotalDisposed,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCorp,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED') THEN 1 ELSE 0 END) as DisposedFavorClaimant,
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate <= @EndDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate) THEN 1 ELSE 0 END) as EndingPending
                        FROM LABOUR_CASES c
                        WHERE (UPPER(ISNULL(c.CaseType,'')) LIKE '%WP%' OR c.IsArisingApplication = 0 OR c.HighCourtBench IS NOT NULL)";

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
                                viewModel.HighCourtData.LabourWpCases.TotalDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                viewModel.HighCourtData.LabourWpCases.EndingPending = reader["EndingPending"] != DBNull.Value ? Convert.ToInt32(reader["EndingPending"]) : 0;
                            }
                        }
                    }
                }
                catch { }

                // 3. LABOUR W.A. CASES
                try
                {
                    string waQuery = @"
                        SELECT 
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate < @StartDate) OR (c.CreatedDate < @StartDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate >= @StartDate) THEN 1 ELSE 0 END) as BeginningPending,
                            SUM(CASE WHEN UPPER(ISNULL(c.FiledBy,'CORPORATION')) LIKE '%CORP%' AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedCorp,
                            SUM(CASE WHEN UPPER(ISNULL(c.FiledBy,'')) LIKE '%CLAIMANT%' AND ((c.EntrustmentDate >= @StartDate AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate >= @StartDate AND c.CreatedDate <= @EndDate)) THEN 1 ELSE 0 END) as EntrustedClaimant,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate THEN 1 ELSE 0 END) as TotalDisposed,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('FAVOR', 'FAVOUR', 'DISMISSED') THEN 1 ELSE 0 END) as DisposedFavorCorp,
                            SUM(CASE WHEN c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate AND UPPER(ISNULL(c.DisposalResult,'')) IN ('AGAINST', 'ALLOWED') THEN 1 ELSE 0 END) as DisposedFavorClaimant,
                            SUM(CASE WHEN ((c.EntrustmentDate IS NOT NULL AND c.EntrustmentDate <= @EndDate) OR (c.CreatedDate <= @EndDate)) AND (c.DisposalDate IS NULL OR c.DisposalDate > @EndDate) THEN 1 ELSE 0 END) as EndingPending
                        FROM LABOUR_CASES c
                        WHERE (UPPER(ISNULL(c.CaseType,'')) LIKE '%WA%' OR c.HighCourtBench = 'Division Bench')";

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
                                viewModel.HighCourtData.LabourWaCases.TotalDisposed = reader["TotalDisposed"] != DBNull.Value ? Convert.ToInt32(reader["TotalDisposed"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.DisposedFavorCorp = reader["DisposedFavorCorp"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorCorp"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.DisposedFavorClaimant = reader["DisposedFavorClaimant"] != DBNull.Value ? Convert.ToInt32(reader["DisposedFavorClaimant"]) : 0;
                                viewModel.HighCourtData.LabourWaCases.EndingPending = reader["EndingPending"] != DBNull.Value ? Convert.ToInt32(reader["EndingPending"]) : 0;
                            }
                        }
                    }
                }
                catch { }

                // 5. SLP CASES (Supreme Court)
                try
                {
                    string slpMvcQuery = "SELECT COUNT(*) FROM MVC_CASES WHERE (CorpSLPNo IS NOT NULL OR ClaimantSLPNo IS NOT NULL OR UPPER(ISNULL(CurrentStatus,'')) LIKE '%SLP%')";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(slpMvcQuery, conn))
                    {
                        viewModel.HighCourtData.SlpMvcCasesCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                catch { }

                try
                {
                    string slpLabourQuery = "SELECT COUNT(*) FROM LABOUR_CASES WHERE (ClaimantSCNumber IS NOT NULL OR UPPER(ISNULL(CaseType,'')) LIKE '%SLP%')";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(slpLabourQuery, conn))
                    {
                        viewModel.HighCourtData.SlpLabourCasesCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                catch { }
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

                // Get EP payments if exists
                var epList = _epRepo.GetAllEPs(0);
                var epDetails = epList.FirstOrDefault(ep => ep.CaseID == caseDetails.CaseID);
                
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateBill(PettyBillViewModel model)
        {
            return View("PrintBill", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveBill([FromBody] PettyBillViewModel model)
        {
            try
            {
                int billId;
                
                if (model.BillID > 0)
                {
                    // Update existing bill
                    bool success = _pettyBillRepo.UpdatePettyBill(model);
                    billId = model.BillID;
                    
                    return Json(new { success = true, billId = billId, message = "Bill updated successfully!" });
                }
                else
                {
                    // Save new bill
                    billId = _pettyBillRepo.SavePettyBill(model);
                    
                    return Json(new { success = true, billId = billId, message = "Bill saved successfully!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving petty bill for CaseID {CaseID}", model.CaseID);
                return Json(new { success = false, message = "Error saving bill. Please try again." });
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
        [ValidateAntiForgeryToken]
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
                return Json(new { success = false, message = "Error deleting bill. Please try again." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                return Json(new { success = false, message = "Error deleting payment. Please try again." });
            }
        }
    }
}
