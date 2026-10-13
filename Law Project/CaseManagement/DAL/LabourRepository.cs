using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;
using MVCCaseManagement.Common;

namespace MVCCaseManagement.DAL
{
    public interface ILabourRepository
    {
        IEnumerable<LabourCase> GetCases(int divisionID, string? status = null);
        LabourDashboardStats GetDashboardStats(int divisionID);
        int SaveCase(LabourCase model);
        LabourCase? GetCaseById(int id);
        void UpdateCase(LabourCase model);
        IEnumerable<LabourCase> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null);
        int GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null);
        IEnumerable<LabourCourt> GetAllCourts();
        IEnumerable<LabourAdvocate> GetAllAdvocates();
        void MarkCaseAsViewed(int caseId);
        LabourCase? GetCaseByNumber(string caseNumber, int year, string? court = null, string? caseType = null);
        List<LabourCase> GetCasesByNumber(string caseNumber, int year);
        IEnumerable<LabourCase> GetConnectedHighCourtCases(int divisionID);
        bool TransferCase(int caseId, int toDivisionId, string remarks);
        IEnumerable<LabourCase> GetRecentTransfers(int divisionId);
        void MarkTransferAsViewed(int caseId);
        IEnumerable<LabourCase> GetReinstatementNotifications(int divisionId);
        void MarkReinstatementAsViewed(int caseId);
        IEnumerable<LabourCase> GetCasesByHearingDate(DateTime date, int divisionId, DateTime? endDate = null);
        void MarkAsViewedByCO(int caseId);
        
        // --- Service Matter Dedicated Methods ---
        IEnumerable<LabourCase> GetServiceMatters(int divisionId);
        int SaveServiceMatter(LabourCase model);
        void UpdateServiceMatter(LabourCase model);
        LabourCase? GetServiceMatterById(int id);
        IEnumerable<LabourConnectedCase> GetConnectedCasesByCaseId(int caseId);
        IEnumerable<LabourReinstatementDocument> GetReinstatedDocuments(int caseId);
        void AddReinstatedDocument(LabourReinstatementDocument doc);
        bool UpdateCNR(int caseId, string? cnrNumber, string? estCode, int modifiedBy);
        bool UpdateAppealCNR(int caseId, string appealType, string? cnrNumber, int modifiedBy);
        bool UpdateLiveSyncInfo(int caseId, DateTime? nextHearingDate, string? stage, string? courtHall, int modifiedBy);
        bool UpdateServiceMatterRoleAction(int serviceId, string role, string actionTaken, DateTime? approvalDate, string opinion, int? modifiedBy);
    }

    public class LabourRepository : ILabourRepository
    {
        private readonly DBHelper _db;

        public LabourRepository(DBHelper db)
        {
            _db = db;
        }

        public IEnumerable<LabourCourt> GetAllCourts()
        {
            var list = new List<LabourCourt>();
            string query = "SELECT * FROM LABOUR_COURTS WHERE IsActive = 1";
            var dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LabourCourt
                {
                    CourtID = Convert.ToInt32(row["CourtID"]),
                    CourtName = row["CourtName"].ToString()!,
                    Location = row["Location"]?.ToString(),
                    IsActive = (bool)row["IsActive"]
                });
            }
            return list;
        }

        public IEnumerable<LabourAdvocate> GetAllAdvocates()
        {
            var list = new List<LabourAdvocate>();
            string query = "SELECT AdvocateID, AdvocateName, IsActive FROM LABOUR_ADVOCATES WHERE IsActive = 1 ORDER BY AdvocateName";
            
            try 
            {
                var dt = _db.ExecuteQuery(query);
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new LabourAdvocate
                    {
                        AdvocateID = Convert.ToInt32(row["AdvocateID"]),
                        AdvocateName = row["AdvocateName"].ToString()!,
                        IsActive = (bool)row["IsActive"]
                    });
                }
            }
            catch (Exception)
            {
                // Fallback or log if needed while DB is initializing
            }
            return list;
        }

        public LabourDashboardStats GetDashboardStats(int divisionID)
        {
            var stats = new LabourDashboardStats();
            // Refined query guaranteed to count each case exactly once without Cartesian explosion
            string query = @"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN c.CaseStatus = 'Pending' THEN 1 ELSE 0 END) as Pending,
                    SUM(CASE WHEN c.CaseStatus IN ('Disposed', 'DNP', 'Ex-parte') THEN 1 ELSE 0 END) as Disposed,
                    SUM(CASE WHEN c.DisposalResult = 'Against' THEN 1 ELSE 0 END) as Against,
                    SUM(CASE WHEN c.SentToCO = 1 THEN 1 ELSE 0 END) as SentToCO,
                    SUM(CASE WHEN (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%AT%CA%') THEN 1 ELSE 0 END) as PendingCompetentAuthority,
                    SUM(CASE WHEN (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%FOR%ACTION%') THEN 1 ELSE 0 END) as PendingCLOLO,
                    SUM(CASE WHEN (c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%')) OR a.HasPendingWp = 1 OR a.HasPendingWa = 1 THEN 1 ELSE 0 END) as HighCourtAppeals,
                    SUM(CASE WHEN c.IsArisingApplication = 1 THEN 1 ELSE 0 END) as ArisingApplications,
                    SUM(CASE WHEN c.SentToCO = 1 AND (c.CO_ActionTaken IS NULL OR c.CO_ActionTaken = '') THEN 1 ELSE 0 END) as NoActionTaken,
                    SUM(CASE WHEN c.CO_IsWorkmanReinstated = 1 AND c.CO_ReinstatementApprovalDate IS NOT NULL AND c.CO_Reinstatement_ApprovalNo IS NOT NULL AND c.CO_Reinstatement_ApprovalNo <> '' AND c.CO_Reinstatement_ApprovalCopyPath IS NOT NULL AND c.CO_Reinstatement_ApprovalCopyPath <> '' THEN 1 ELSE 0 END) as Reinstatements,
                    SUM(CASE WHEN c.CreatedDate >= DATEADD(month, DATEDIFF(month, 0, GETDATE()), 0) THEN 1 ELSE 0 END) as CasesThisMonth,
                    SUM(CASE WHEN c.IsClaimantSCPending = 1 OR c.ClaimantSCStatus = 'Pending' THEN 1 ELSE 0 END) as SLPPending,
                    SUM(CASE WHEN c.CaseType IN ('Claimant Writ Appeal', 'Claimant WA', 'Writ Appeal', 'WA') OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '' AND (c.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR a.HasPendingWa = 1 THEN 1 ELSE 0 END) as WritAppealsClaimant,
                    SUM(CASE WHEN (c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Service_WPNumber IS NOT NULL AND c.CO_Service_WPNumber <> '' AND (c.CO_Service_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Service_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND c.CO_WP_CaseStatus_Option <> '' AND UPPER(c.CO_WP_CaseStatus_Option) NOT IN ('NONE', 'NOT FILED') AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_WP_StayGranted = 1 AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%' OR c.CO_ActionTaken LIKE '%WP%') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CaseType IN ('Claimant Appeal', 'Claimant Writ Petition', 'Claimant WP', 'Writ Petition', 'WP', 'Service Matter') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR a.HasPendingWp = 1 THEN 1 ELSE 0 END) as WritPetitionClaimant
                FROM LABOUR_CASES c
                OUTER APPLY (
                    SELECT 
                        MAX(CASE WHEN ((a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '') OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WP%') OR (a.CO_WP_CaseStatus_Option IS NOT NULL AND a.CO_WP_CaseStatus_Option <> '' AND UPPER(a.CO_WP_CaseStatus_Option) LIKE '%PENDING%')) AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED')) THEN 1 ELSE 0 END) as HasPendingWp,
                        MAX(CASE WHEN ((a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '') OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WA%') OR (a.CO_WA_CaseStatus_Option IS NOT NULL AND a.CO_WA_CaseStatus_Option <> '' AND UPPER(a.CO_WA_CaseStatus_Option) LIKE '%PENDING%')) AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED')) THEN 1 ELSE 0 END) as HasPendingWa
                    FROM LABOUR_ARISING_APPLICATIONS a
                    WHERE (a.ParentCaseID > 0 AND a.ParentCaseID = c.CaseID) 
                       OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))
                ) a
                WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionID) });
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                stats.TotalCases = row["Total"] != DBNull.Value ? Convert.ToInt32(row["Total"]) : 0;
                stats.PendingCases = row["Pending"] != DBNull.Value ? Convert.ToInt32(row["Pending"]) : 0;
                stats.DisposedCases = row["Disposed"] != DBNull.Value ? Convert.ToInt32(row["Disposed"]) : 0;
                stats.AgainstCases = row["Against"] != DBNull.Value ? Convert.ToInt32(row["Against"]) : 0;
                stats.SentToCOCount = row["SentToCO"] != DBNull.Value ? Convert.ToInt32(row["SentToCO"]) : 0;
                stats.PendingCompetentAuthorityCount = row["PendingCompetentAuthority"] != DBNull.Value ? Convert.ToInt32(row["PendingCompetentAuthority"]) : 0;
                stats.PendingCLOLOCount = row["PendingCLOLO"] != DBNull.Value ? Convert.ToInt32(row["PendingCLOLO"]) : 0;
                stats.HighCourtAppealCount = row["HighCourtAppeals"] != DBNull.Value ? Convert.ToInt32(row["HighCourtAppeals"]) : 0;
                stats.ArisingApplicationCount = row["ArisingApplications"] != DBNull.Value ? Convert.ToInt32(row["ArisingApplications"]) : 0;
                stats.NoActionTakenCount = row["NoActionTaken"] != DBNull.Value ? Convert.ToInt32(row["NoActionTaken"]) : 0;
                stats.ReinstatementCount = row["Reinstatements"] != DBNull.Value ? Convert.ToInt32(row["Reinstatements"]) : 0;
                stats.CasesThisMonth = row["CasesThisMonth"] != DBNull.Value ? Convert.ToInt32(row["CasesThisMonth"]) : 0;
                stats.SLPPendingCount = row["SLPPending"] != DBNull.Value ? Convert.ToInt32(row["SLPPending"]) : 0;
                stats.WritAppealsClaimantCount = row["WritAppealsClaimant"] != DBNull.Value ? Convert.ToInt32(row["WritAppealsClaimant"]) : 0;
                stats.WritPetitionClaimantCount = row["WritPetitionClaimant"] != DBNull.Value ? Convert.ToInt32(row["WritPetitionClaimant"]) : 0;
                stats.FinancialExposure = 0; // Placeholder as no financial fields exist yet
            }

            // Fetch Service/Commercial Matter Count
            string svcQuery = "SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID OR DivisionID = 0 OR DivisionID IS NULL)";
            var dtSvc = _db.ExecuteQuery(svcQuery, new[] { new SqlParameter("@DivisionID", divisionID) });
            if (dtSvc.Rows.Count > 0)
            {
                stats.ServiceMatterCount = Convert.ToInt32(dtSvc.Rows[0][0]);
            }

            // Fetch Arising Application Count (authoritative count from LABOUR_ARISING_APPLICATIONS)
            string arisingQuery = "SELECT COUNT(*) FROM LABOUR_ARISING_APPLICATIONS WHERE (@DivisionID = 0 AND SentToCO = 1) OR (@DivisionID > 0 AND DivisionID = @DivisionID)";
            try 
            {
                var dtArising = _db.ExecuteQuery(arisingQuery, new[] { new SqlParameter("@DivisionID", divisionID) });
                if (dtArising.Rows.Count > 0)
                {
                    stats.ArisingApplicationCount = Convert.ToInt32(dtArising.Rows[0][0]);
                }
            } 
            catch { /* Keep fallback count from LABOUR_CASES if table doesn't exist yet */ }

            return stats;
        }

        public IEnumerable<LabourCase> GetCases(int divisionID, string? status = null)
        {
            var list = new List<LabourCase>();
            string query = @"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                  AND (@Status IS NULL OR c.CaseStatus = @Status)
                ORDER BY c.CreatedDate DESC";

            var parameters = new[] {
                new SqlParameter("@DivisionID", divisionID),
                new SqlParameter("@Status", status ?? (object)DBNull.Value)
            };

            var dt = _db.ExecuteQuery(query, parameters);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapToModel(row));
            }
            return list;
        }

        public IEnumerable<LabourCase> GetConnectedHighCourtCases(int divisionId)
        {
            var list = new List<LabourCase>();
            string query = @"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                  AND (
                       (c.CO_WP_CNRNumber IS NOT NULL AND c.CO_WP_CNRNumber <> '') 
                    OR (c.CO_WA_CNRNumber IS NOT NULL AND c.CO_WA_CNRNumber <> '') 
                    OR (c.CO_Claimant_CNRNumber IS NOT NULL AND c.CO_Claimant_CNRNumber <> '')
                    OR (c.CNRNumber IS NOT NULL AND c.CNRNumber LIKE 'KAHC%')
                    OR (c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '')
                    OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '')
                    OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '')
                    OR (c.CO_Service_WPNumber IS NOT NULL AND c.CO_Service_WPNumber <> '')
                  )
                ORDER BY c.CaseID DESC";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionId) });
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapToModel(row));
            }
            return list;
        }

        public IEnumerable<LabourCase> GetCasesByHearingDate(DateTime date, int divisionId, DateTime? endDate = null)
        {
            var list = new List<LabourCase>();
            string query;
            List<SqlParameter> parameters = new List<SqlParameter>();

            if (endDate.HasValue)
            {
                query = @"
                    SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                    FROM LABOUR_CASES c
                    LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                    LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                    WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                      AND CAST(c.NextHearingDate AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(c.NextHearingDate AS DATE) <= CAST(@EndDate AS DATE)
                    ORDER BY c.NextHearingDate ASC";
                
                parameters.Add(new SqlParameter("@StartDate", date));
                parameters.Add(new SqlParameter("@EndDate", endDate.Value));
            }
            else
            {
                query = @"
                    SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                    FROM LABOUR_CASES c
                    LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                    LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                    WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                      AND CAST(c.NextHearingDate AS DATE) = CAST(@Date AS DATE)
                    ORDER BY c.NextHearingDate ASC";
                
                parameters.Add(new SqlParameter("@Date", date));
            }

            parameters.Add(new SqlParameter("@DivisionID", divisionId));

            var dt = _db.ExecuteQuery(query, parameters.ToArray());
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapToModel(row));
            }
            return list;
        }

        public IEnumerable<LabourCase> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            var list = new List<LabourCase>();
            int offset = (pageNumber - 1) * pageSize;

            var paramsList = new List<SqlParameter> {
                new SqlParameter("@DivisionID", divisionId),
                new SqlParameter("@Status", (object)status ?? "all"),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize)
            };

            string searchCondition = @"(
                        @Search IS NULL OR @Search = '' OR 
                        c.CaseNumber LIKE @Search OR 
                        c.PetitionerName LIKE @Search OR
                        c.EmployeeNo LIKE @Search OR
                        c.CO_WP_CaseNumber LIKE @Search OR
                        CAST(c.CO_WP_Year AS VARCHAR) LIKE @Search OR
                        c.CO_WA_CaseNumber LIKE @Search OR
                        CAST(c.CO_WA_Year AS VARCHAR) LIKE @Search OR
                        CAST(c.CaseID AS VARCHAR) = REPLACE(REPLACE(@Search, '%', ''), '#', '')
                   )";

            if (!string.IsNullOrEmpty(search) && search.Contains("/"))
            {
                var parts = search.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int year))
                {
                    searchCondition = "(c.CaseNumber = @SNo AND c.CaseYear = @SYear)";
                    paramsList.Add(new SqlParameter("@SNo", parts[0].Trim()));
                    paramsList.Add(new SqlParameter("@SYear", year));
                }
            }
            paramsList.Add(new SqlParameter("@Search", string.IsNullOrEmpty(search) ? (object)DBNull.Value : "%" + search.Trim() + "%"));

            string query = $@"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName,
                       la.AdvocateName as LA_AdvocateName,
                       hca.AdvocateName as HCA_AdvocateName,
                       am.AdvocateName as AM_AdvocateName,
                       (SELECT TOP 1 a.CO_WP_CaseNumber FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WP_CaseNumber,
                       (SELECT TOP 1 a.CO_WP_Year FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WP_Year,
                       (SELECT TOP 1 a.CO_WP_Status FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WP_Status,
                       (SELECT TOP 1 a.CO_WA_CaseNumber FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WA_CaseNumber,
                       (SELECT TOP 1 a.CO_WA_Year FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WA_Year,
                       (SELECT TOP 1 a.CO_WA_Status FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '' ORDER BY a.ArisingID DESC) as Arising_CO_WA_Status
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                LEFT JOIN LABOUR_CASE_VIEW_TRACKING vt ON c.CaseID = vt.CaseID
                LEFT JOIN LABOUR_ADVOCATES la ON c.AdvocateID = la.AdvocateID
                LEFT JOIN HIGH_COURT_ADVOCATES hca ON c.AdvocateID = hca.AdvocateID
                LEFT JOIN ADVOCATE_MASTER am ON c.AdvocateID = am.AdvocateID
                WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                  AND {searchCondition}
                  AND (
                        (@Status = 'all') OR
                        (@Status = 'SentToCO' AND c.SentToCO = 1 AND (@DivisionID > 0 OR IsViewedByCO = 0)) OR
                        (@Status = 'PendingCompetentAuthority' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%AT%CA%')) OR
                        (@Status = 'PendingDecision' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%FOR%ACTION%')) OR
                        (@Status = 'HighCourtAppeal' AND ((c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '' AND (c.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(c.CO_WA_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%')) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '') AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR ((a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '') AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (a.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WA_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status = 'ArisingApplication' AND c.IsArisingApplication = 1) OR
                        (@Status = 'Favor' AND (c.DisposalResult = 'Favor' OR c.DisposalResult = 'Partially Favor')) OR
                        (@Status = 'Against' AND (c.DisposalResult = 'Against' OR c.DisposalResult = 'Dismissed')) OR
                        (@Status = 'InterimStayCompliance' AND (c.CO_WP_StayGranted = 1 OR c.CO_Reinstatement_StayGranted = 1)) OR
                        (@Status = 'NoActionTaken' AND c.SentToCO = 1 AND (c.CO_ActionTaken IS NULL OR c.CO_ActionTaken = '')) OR
                        (@Status = 'Reinstatement' AND c.CO_IsWorkmanReinstated = 1 AND c.CO_ReinstatementApprovalDate IS NOT NULL AND c.CO_Reinstatement_ApprovalNo IS NOT NULL AND c.CO_Reinstatement_ApprovalNo <> '' AND c.CO_Reinstatement_ApprovalCopyPath IS NOT NULL AND c.CO_Reinstatement_ApprovalCopyPath <> '') OR
                        (@Status = 'SLPPending' AND (c.IsClaimantSCPending = 1 OR c.ClaimantSCStatus = 'Pending')) OR
                        (@Status = 'WritAppealsClaimant' AND (c.CaseType IN ('Claimant Writ Appeal', 'Claimant WA', 'Writ Appeal', 'WA') OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '' AND (c.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '') AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WA%' AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WA_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status = 'WritPetitionClaimant' AND ((c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Service_WPNumber IS NOT NULL AND c.CO_Service_WPNumber <> '' AND (c.CO_Service_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Service_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND c.CO_WP_CaseStatus_Option <> '' AND UPPER(c.CO_WP_CaseStatus_Option) NOT IN ('NONE', 'NOT FILED') AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_WP_StayGranted = 1 AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%' OR c.CO_ActionTaken LIKE '%WP%') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CaseType IN ('Claimant Appeal', 'Claimant Writ Petition', 'Claimant WP', 'Writ Petition', 'WP', 'Service Matter') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '') AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WP%' AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WP_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status NOT IN ('all', 'SentToCO', 'PendingCompetentAuthority', 'PendingDecision', 'HighCourtAppeal', 'ArisingApplication', 'Favor', 'Against', 'InterimStayCompliance', 'NoActionTaken', 'Reinstatement', 'SLPPending', 'WritAppealsClaimant', 'WritPetitionClaimant') AND c.CaseStatus = @Status)
                  )
                ORDER BY c.CreatedDate DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            DataTable dt;
            try 
            {
                dt = _db.ExecuteQuery(query, paramsList.ToArray());
            }
            catch (Exception)
            {
                // Fallback: If table is missing, run without tracking logic
                string fallbackQuery = $@"
                    SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                    FROM LABOUR_CASES c
                    LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                    LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                    WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                      AND {searchCondition}
                      AND (
                            @Status = 'all' OR 
                            (@Status = 'SentToCO' AND c.SentToCO = 1 AND (@DivisionID > 0 OR IsViewedByCO = 0)) OR
                            (@Status = 'PendingCompetentAuthority' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%AT%CA%')) OR
                            (@Status = 'Favor' AND (c.DisposalResult = 'Favor' OR c.DisposalResult = 'Partially Favor')) OR
                            (@Status = 'Against' AND (c.DisposalResult = 'Against' OR c.DisposalResult = 'Dismissed')) OR
                            (@Status = 'NoActionTaken' AND c.SentToCO = 1 AND (c.CO_ActionTaken IS NULL OR c.CO_ActionTaken = '')) OR
                            (@Status <> 'all' AND @Status <> 'SentToCO' AND @Status <> 'Favor' AND @Status <> 'Against' AND @Status <> 'NoActionTaken' AND c.CaseStatus = @Status)
                      )
                    ORDER BY c.CreatedDate DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                dt = _db.ExecuteQuery(fallbackQuery, paramsList.ToArray());
            }

            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapToModel(row));
            }
            return list;
        }

        public int GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null)
        {
            var paramsList = new List<SqlParameter> {
                new SqlParameter("@DivisionID", divisionId),
                new SqlParameter("@Status", (object)status ?? "all")
            };

            string searchCondition = @"(
                        @Search IS NULL OR @Search = '' OR 
                        c.CaseNumber LIKE @Search OR 
                        c.PetitionerName LIKE @Search OR
                        c.EmployeeNo LIKE @Search OR
                        c.CO_WP_CaseNumber LIKE @Search OR
                        CAST(c.CO_WP_Year AS VARCHAR) LIKE @Search OR
                        c.CO_WA_CaseNumber LIKE @Search OR
                        CAST(c.CO_WA_Year AS VARCHAR) LIKE @Search OR
                        CAST(c.CaseID AS VARCHAR) = REPLACE(REPLACE(@Search, '%', ''), '#', '')
                   )";

            if (!string.IsNullOrEmpty(search) && search.Contains("/"))
            {
                var parts = search.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int year))
                {
                    searchCondition = "(c.CaseNumber = @SNo AND c.CaseYear = @SYear)";
                    paramsList.Add(new SqlParameter("@SNo", parts[0].Trim()));
                    paramsList.Add(new SqlParameter("@SYear", year));
                }
            }
            paramsList.Add(new SqlParameter("@Search", string.IsNullOrEmpty(search) ? (object)DBNull.Value : "%" + search.Trim() + "%"));

            string query = $@"
                SELECT COUNT(*) 
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_CASE_VIEW_TRACKING vt ON c.CaseID = vt.CaseID
                WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                  AND {searchCondition}
                  AND (
                        (@Status = 'all') OR
                        (@Status = 'SentToCO' AND c.SentToCO = 1 AND (@DivisionID > 0 OR IsViewedByCO = 0)) OR
                        (@Status = 'PendingCompetentAuthority' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%AT%CA%')) OR
                        (@Status = 'PendingDecision' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%FOR%ACTION%')) OR
                        (@Status = 'HighCourtAppeal' AND ((c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '' AND (c.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(c.CO_WA_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%')) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '') AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR ((a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '') AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (a.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WA_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status = 'ArisingApplication' AND c.IsArisingApplication = 1) OR
                        (@Status = 'Favor' AND (c.DisposalResult = 'Favor' OR c.DisposalResult = 'Partially Favor')) OR
                        (@Status = 'Against' AND (c.DisposalResult = 'Against' OR c.DisposalResult = 'Dismissed')) OR
                        (@Status = 'InterimStayCompliance' AND (c.CO_WP_StayGranted = 1 OR c.CO_Reinstatement_StayGranted = 1)) OR
                        (@Status = 'NoActionTaken' AND c.SentToCO = 1 AND (c.CO_ActionTaken IS NULL OR c.CO_ActionTaken = '')) OR
                        (@Status = 'Reinstatement' AND c.CO_IsWorkmanReinstated = 1 AND c.CO_ReinstatementApprovalDate IS NOT NULL AND c.CO_Reinstatement_ApprovalNo IS NOT NULL AND c.CO_Reinstatement_ApprovalNo <> '' AND c.CO_Reinstatement_ApprovalCopyPath IS NOT NULL AND c.CO_Reinstatement_ApprovalCopyPath <> '') OR
                        (@Status = 'SLPPending' AND (c.IsClaimantSCPending = 1 OR c.ClaimantSCStatus = 'Pending')) OR
                        (@Status = 'WritAppealsClaimant' AND (c.CaseType IN ('Claimant Writ Appeal', 'Claimant WA', 'Writ Appeal', 'WA') OR (c.CO_WA_CaseNumber IS NOT NULL AND c.CO_WA_CaseNumber <> '' AND (c.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WA_CaseNumber IS NOT NULL AND a.CO_WA_CaseNumber <> '') AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WA%' AND (a.CO_WA_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WA_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WA_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WA_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status = 'WritPetitionClaimant' AND ((c.CO_WP_CaseNumber IS NOT NULL AND c.CO_WP_CaseNumber <> '' AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Service_WPNumber IS NOT NULL AND c.CO_Service_WPNumber <> '' AND (c.CO_Service_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Service_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_WP_CaseStatus_Option IS NOT NULL AND c.CO_WP_CaseStatus_Option <> '' AND UPPER(c.CO_WP_CaseStatus_Option) NOT IN ('NONE', 'NOT FILED') AND UPPER(c.CO_WP_CaseStatus_Option) LIKE '%PENDING%') OR (c.CO_WP_StayGranted = 1 AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_ActionTaken IS NOT NULL AND (c.CO_ActionTaken LIKE '%Writ Petition%' OR c.CO_ActionTaken LIKE '%File WP%' OR c.CO_ActionTaken LIKE '%WP%') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CaseType IN ('Claimant Appeal', 'Claimant Writ Petition', 'Claimant WP', 'Writ Petition', 'WP', 'Service Matter') AND (c.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(c.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (c.CO_Claimant_CaseNumber IS NOT NULL AND c.CO_Claimant_CaseNumber <> '' AND (c.CO_Claimant_CaseStatus IS NULL OR UPPER(LTRIM(RTRIM(c.CO_Claimant_CaseStatus))) NOT IN ('DISPOSED', 'CLOSED'))) OR EXISTS (SELECT 1 FROM LABOUR_ARISING_APPLICATIONS a WHERE (a.ParentCaseID = c.CaseID OR (a.Parent_CaseNumber IS NOT NULL AND a.Parent_CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(c.CaseNumber)) AND (a.Parent_CaseYear IS NULL OR a.Parent_CaseYear = 0 OR a.Parent_CaseYear = c.CaseYear))) AND (((a.CO_WP_CaseNumber IS NOT NULL AND a.CO_WP_CaseNumber <> '') AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_ActionTaken IS NOT NULL AND a.CO_ActionTaken LIKE '%WP%' AND (a.CO_WP_Status IS NULL OR UPPER(LTRIM(RTRIM(a.CO_WP_Status))) NOT IN ('DISPOSED', 'CLOSED'))) OR (a.CO_WP_CaseStatus_Option IS NOT NULL AND UPPER(a.CO_WP_CaseStatus_Option) LIKE '%PENDING%'))))) OR
                        (@Status NOT IN ('all', 'SentToCO', 'PendingCompetentAuthority', 'PendingDecision', 'HighCourtAppeal', 'ArisingApplication', 'Favor', 'Against', 'InterimStayCompliance', 'NoActionTaken', 'Reinstatement', 'SLPPending', 'WritAppealsClaimant', 'WritPetitionClaimant') AND c.CaseStatus = @Status)
                  )";

            DataTable dt;
            try 
            {
                dt = _db.ExecuteQuery(query, paramsList.ToArray());
            }
            catch (Exception)
            {
                // Fallback: If table is missing
                string fallbackQuery = $@"
                    SELECT COUNT(*) 
                    FROM LABOUR_CASES c
                    WHERE (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                      AND {searchCondition}
                      AND (
                            @Status = 'all' OR 
                            (@Status = 'SentToCO' AND c.SentToCO = 1 AND (@DivisionID > 0 OR IsViewedByCO = 0)) OR
                            (@Status = 'PendingCompetentAuthority' AND (UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(c.CO_ActionTaken))) LIKE '%PENDING%AT%CA%')) OR
                            (@Status = 'Favor' AND (c.DisposalResult = 'Favor' OR c.DisposalResult = 'Partially Favor')) OR
                            (@Status = 'Against' AND (c.DisposalResult = 'Against' OR c.DisposalResult = 'Dismissed')) OR
                            (@Status = 'NoActionTaken' AND c.SentToCO = 1 AND (c.CO_ActionTaken IS NULL OR c.CO_ActionTaken = '')) OR
                            (@Status <> 'all' AND @Status <> 'SentToCO' AND @Status <> 'Favor' AND @Status <> 'Against' AND @Status <> 'NoActionTaken' AND c.CaseStatus = @Status)
                      )";
                dt = _db.ExecuteQuery(fallbackQuery, paramsList.ToArray());
            }
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        public LabourCase? GetCaseById(int id)
        {
            string query = @"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName,
                       la.AdvocateName as LA_AdvocateName,
                       hca.AdvocateName as HCA_AdvocateName,
                       am.AdvocateName as AM_AdvocateName
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                LEFT JOIN LABOUR_ADVOCATES la ON c.AdvocateID = la.AdvocateID
                LEFT JOIN HIGH_COURT_ADVOCATES hca ON c.AdvocateID = hca.AdvocateID
                LEFT JOIN ADVOCATE_MASTER am ON c.AdvocateID = am.AdvocateID
                WHERE c.CaseID = @ID";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ID", id) });
            if (dt.Rows.Count > 0)
            {
                var model = MapToModel(dt.Rows[0]);
                // try
                // {
                //     model.EvidenceList = GetEvidenceByCaseId(id).ToList();
                // }
                // catch { /* Ignore evidence errors if table missing/empty */ }

                try
                {
                    model.HistoryList = GetCaseHistory(id).ToList();
                }
                catch { /* Ignore */ }

                try
                {
                    var allConnected = GetConnectedCasesByCaseId(id).ToList();
                    model.ConnectedCases = allConnected.Where(c => c.FiledBy != "CLAIMANT APPEAL").ToList();
                    model.ClaimantConnectedCases = allConnected.Where(c => c.FiledBy == "CLAIMANT APPEAL").ToList();
                }
                catch { /* Ignore connected cases errors */ }

                try
                {
                    model.ReinstatedDocuments = GetReinstatedDocuments(id).ToList();
                }
                catch { /* Ignore */ }

                model.EnclosedDocuments = GetEnclosedDocs(id);

                return model;
            }
            return null;
        }

        public int SaveCase(LabourCase model)
        {
            string query = @"
                INSERT INTO LABOUR_CASES (
                    ServiceID, DivisionID, CaseType, CaseStatus, CaseNumber, CaseYear, CourtID, OtherCourtDetails, CNRNumber, EstCode, CaseTypeCode,
                    PetitionerName, EmployeeNo, PFNumber, Designation, WorkingStatus, LegalRepresentativeName, LRRelationship, IsWorkman, IsWorkmanRemark, NatureOfCase, NatureOfMisconduct,
                    EntrustmentNo, EntrustmentDate, AdvocateID, AdvocateName, IsDoubleClaim, DoubleClaimRemarks,
                    CurrentStage, SerialApp_CaseNumber, SerialApp_CurrentStage, CaseHistory, NextHearingDate, IsCOApprovalRequired, COApproval_OutwardNo, COApproval_OutwardDate,
                    IsDocumentSent, DocumentSent_OutwardNo, DocumentSent_OutwardDate,
                    IsObjectionFiled, ObjectionFiled_OutwardNo, ObjectionFiled_OutwardDate, 
                    DisposalMode, DisposalDate, DisposalResult, FavorRemark,
                    LokAdalat_COApprovalRequired, LokAdalat_OutwardNo, LokAdalat_Date, LokAdalatDocumentPath,
                    Against_CaseCategory, Against_BriefFacts, Against_PunishmentImposed, Against_PunishmentNo, Against_PunishmentDate,
                    DE_HistorySheet, DE_HistorySheetPath, DE_ObjectionsFiled, DE_ObjectionsRemark, DE_DocumentsMarked, DE_DocumentsRemark, DE_Order, ChargesStatus,
                    TerminalBenefitsPaid, SerialApplicationDetails, IsRepeatDismissal, RepeatDismissalRemark, HasAppealDetails, AppealDetailsRemark,
                    DE_EO_Name, DE_EO_Designation, DE_EO_IsBasedOnDocuments, 
            DE_Reporter_Name, DE_Reporter_Designation, DE_Reporter_IsBasedOnDocuments,
            DE_Other_Name, DE_Other_Designation, DE_Other_IsBasedOnDocuments,
            IsEnquiryOfficerEvidence, IsReporterEvidence, IsOtherEvidence,
            CC_CaseDisposedDate, CC_PublicationDate, CC_AppliedDate, CC_IssuedDate, CC_DeliveredDate, CC_ReceivedDate, CC_Remarks,
                    AwardDetails, Opinion_Advocate, Opinion_LO, Opinion_DC,
                    SentToCO, CO_OutwardNo, CO_OutwardDate, CO_Remarks,
                    CO_FeasibilityReceived, CO_FeasibilityDate, CO_ActionTaken, CO_ApprovalOutwardNo, CO_ApprovalDate, CO_ClosedDocumentPath,
                    CO_WP_CNRNumber, CO_WP_CaseStatus_Option, CO_WP_CaseNumber, CO_WP_Year, CO_WP_HighCourtBench, CO_WP_EntrustmentNo, CO_WP_EntrustmentDate, CO_WP_AdvocateName, CO_WP_StayGranted, CO_WP_StayApprovalNo, CO_WP_StayNature, CO_WP_StayDate,
                    CO_WP_StayOrderPath, CO_WP_StayRemark,
                     CO_WA_CNRNumber, CO_WA_CaseStatus_Option, CO_WA_CaseNumber, CO_WA_Year, CO_WA_HighCourtBench, CO_WA_EntrustmentNo, CO_WA_EntrustmentDate, CO_WA_AdvocateName, CO_WA_StayGranted, CO_WA_StayApprovalNo, CO_WA_StayNature, CO_WA_StayDate, CO_WA_StayOrderPath, CO_WA_StayRemark, CO_WA_Status, CO_WA_Outcome, CO_WA_OutcomeRemark, CO_WA_OutcomeOutwardNo, CO_WA_OutcomeOutwardDate, CO_WA_ActionTaken,
                     CO_IsWorkmanReinstated, CO_ReinstatedSubjectToWP, CO_ReinstatementApprovalIssued, CO_ReinstatementApprovalDate,
                     CO_WP_Status, CO_OverallCaseStatus,
                     CO_WP_Outcome, CO_WP_OutcomeRemark, CO_WP_OutcomeOutwardNo, CO_WP_OutcomeOutwardDate, CO_WP_JudgmentCopyPath,
                    CO_Disposal_Nature, CO_Disposal_CommSentToDivision, CO_Disposal_OutwardNo, CO_Disposal_Date, CO_Disposal_Decision, CO_Disposal_ApprovalOutwardNo, CO_Disposal_ApprovalDate,
                    CO_FurtherAppeal_Status_Option, CO_FurtherAppeal_CaseNumber, CO_FurtherAppeal_Year, CO_FurtherAppeal_EntrustmentNo, CO_FurtherAppeal_EntrustmentDate, CO_FurtherAppeal_AdvocateName, CO_FurtherAppeal_CaseStatus, CO_FurtherAppeal_DisposalOutwardNo, CO_FurtherAppeal_DisposalDate,
                    CO_Claimant_CNRNumber, CO_Claimant_DivisionName, CO_Claimant_ArisingOutOf, CO_Claimant_Court, CO_Claimant_CaseNumber, CO_Claimant_CaseYear, CO_Claimant_HighCourtBench, CO_Claimant_OriginalCaseStatus, CO_Claimant_IsConnected, CO_Claimant_EntrustmentNo, CO_Claimant_EntrustmentDate, CO_Claimant_AdvocateName, CO_Claimant_CaseStatus, CO_Claimant_PetitionCopyPath,
                    IsClaimantSCPending, ClaimantSCDiaryNumber, ClaimantSCYear, ClaimantSCNumber, ClaimantSLPYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus, ClaimantSCOutcome, ClaimantSCActionTaken, ClaimantSCClosureNo, ClaimantSCClosureDate,
                    CO_Service_Division, CO_Service_WPNumber, CO_Service_WPYear, CO_Service_PetitionerName, CO_Service_CaseNature, CO_Service_Prayer, CO_Service_PetitionCopyPath, CO_Service_IsEmployee, CO_Service_StayGranted, CO_Service_StayVacateFiled, CO_Service_StayCompliance, CO_Service_ApprovalOutwardNo, CO_Service_ApprovalDate, CO_Service_ApprovalCopyPath, CO_Service_Status, CO_Service_DisposalDate, CO_Service_ActionTaken, CO_Service_ApprovalSentDetails, CO_Service_OutwardNo, CO_Service_OutwardDate, CO_Service_AppealFiledBefore, CO_Service_AppealType, CO_Service_AppealEntrustmentDate, CO_Service_AppealAdvocate, CO_Service_AppealStatus, CO_Service_EntrustmentNo, CO_Service_EntrustmentDate,
                    JudgmentCopyPath, JudgmentCopyPath2,
                    IsArisingApplication, Arising_OriginalCaseNumber, Arising_OriginalCaseYear, Arising_OriginalCourt, 
                    Arising_CurrentStatus, Arising_ApplicationStatus,
                    IsFiledWithinLimitation, LimitationRemark, IsDelayCondoned, DelayCondonationRemark,
            CO_StayComplianceRemark, CO_StayComplianceFilePath, CO_StayComplianceDate,
            Against_PunishmentCopyPath, ClaimPetitionPath, ClaimFiledOn, DelayInFiling, ClaimDetails,
            IsViewedByCO, FavorOutwardDate,
            CreatedDate, CreatedBy
                ) VALUES (
                    @ServiceID, @DivisionID, @CaseType, @CaseStatus, @CaseNumber, @CaseYear, @CourtID, @OtherCourtDetails, @CNRNumber, @EstCode, @CaseTypeCode,
                    @PetitionerName, @EmployeeNo, @PFNumber, @Designation, @WorkingStatus, @LegalRepresentativeName, @LRRelationship, @IsWorkman, @IsWorkmanRemark, @NatureOfCase, @NatureOfMisconduct,
                    @EntrustmentNo, @EntrustmentDate, @AdvocateID, @AdvocateName, @IsDoubleClaim, @DoubleClaimRemarks,
                    @CurrentStage, @SerialApp_CaseNumber, @SerialApp_CurrentStage, @CaseHistory, @NextHearingDate, @IsCOApprovalRequired, @COApproval_OutwardNo, @COApproval_OutwardDate,
                    @IsDocumentSent, @DocumentSent_OutwardNo, @DocumentSent_OutwardDate,
                    @IsObjectionFiled, @ObjectionFiled_OutwardNo, @ObjectionFiled_OutwardDate,
                    @DisposalMode, @DisposalDate, @DisposalResult, @FavorRemark,
                    @LokAdalat_COApprovalRequired, @LokAdalat_OutwardNo, @LokAdalat_Date, @LokAdalatDocumentPath,
                    @Against_CaseCategory, @Against_BriefFacts, @Against_PunishmentImposed, @Against_PunishmentNo, @Against_PunishmentDate,
                    @DE_HistorySheet, @DE_HistorySheetPath, @DE_ObjectionsFiled, @DE_ObjectionsRemark, @DE_DocumentsMarked, @DE_DocumentsRemark, @DE_Order, @ChargesStatus,
                    @TerminalBenefitsPaid, @SerialApplicationDetails, @IsRepeatDismissal, @RepeatDismissalRemark, @HasAppealDetails, @AppealDetailsRemark,
                    @DE_EO_Name, @DE_EO_Designation, @DE_EO_IsBasedOnDocuments, 
            @DE_Reporter_Name, @DE_Reporter_Designation, @DE_Reporter_IsBasedOnDocuments,
            @DE_Other_Name, @DE_Other_Designation, @DE_Other_IsBasedOnDocuments,
            @IsEnquiryOfficerEvidence, @IsReporterEvidence, @IsOtherEvidence,
            @CC_CaseDisposedDate, @CC_PublicationDate, @CC_AppliedDate, @CC_IssuedDate, @CC_DeliveredDate, @CC_ReceivedDate, @CC_Remarks,
                    @AwardDetails, @Opinion_Advocate, @Opinion_LO, @Opinion_DC,
                    @SentToCO, @CO_OutwardNo, @CO_OutwardDate, @CO_Remarks,
                    @CO_FeasibilityReceived, @CO_FeasibilityDate, @CO_ActionTaken, @CO_ApprovalOutwardNo, @CO_ApprovalDate, @CO_ClosedDocumentPath,
                    @CO_WP_CNRNumber, @CO_WP_CaseStatus_Option, @CO_WP_CaseNumber, @CO_WP_Year, @CO_WP_HighCourtBench, @CO_WP_EntrustmentNo, @CO_WP_EntrustmentDate, @CO_WP_AdvocateName, @CO_WP_StayGranted, @CO_WP_StayApprovalNo, @CO_WP_StayNature, @CO_WP_StayDate,
                     @CO_WP_StayOrderPath, @CO_WP_StayRemark,
                     @CO_WA_CNRNumber, @CO_WA_CaseStatus_Option, @CO_WA_CaseNumber, @CO_WA_Year, @CO_WA_HighCourtBench, @CO_WA_EntrustmentNo, @CO_WA_EntrustmentDate, @CO_WA_AdvocateName, @CO_WA_StayGranted, @CO_WA_StayApprovalNo, @CO_WA_StayNature, @CO_WA_StayDate, @CO_WA_StayOrderPath, @CO_WA_StayRemark, @CO_WA_Status, @CO_WA_Outcome, @CO_WA_OutcomeRemark, @CO_WA_OutcomeOutwardNo, @CO_WA_OutcomeOutwardDate, @CO_WA_ActionTaken,
                     @CO_IsWorkmanReinstated, @CO_ReinstatedSubjectToWP, @CO_ReinstatementApprovalIssued, @CO_ReinstatementApprovalDate,
                     @CO_WP_Status, @CO_OverallCaseStatus,
                     @CO_WP_Outcome, @CO_WP_OutcomeRemark, @CO_WP_OutcomeOutwardNo, @CO_WP_OutcomeOutwardDate, @CO_WP_JudgmentCopyPath,
                    @CO_Disposal_Nature, @CO_Disposal_CommSentToDivision, @CO_Disposal_OutwardNo, @CO_Disposal_Date, @CO_Disposal_Decision, @CO_Disposal_ApprovalOutwardNo, @CO_Disposal_ApprovalDate,
                    @CO_FurtherAppeal_Status_Option, @CO_FurtherAppeal_CaseNumber, @CO_FurtherAppeal_Year, @CO_FurtherAppeal_EntrustmentNo, @CO_FurtherAppeal_EntrustmentDate, @CO_FurtherAppeal_AdvocateName, @CO_FurtherAppeal_CaseStatus, @CO_FurtherAppeal_DisposalOutwardNo, @CO_FurtherAppeal_DisposalDate,
                    @CO_Claimant_CNRNumber, @CO_Claimant_DivisionName, @CO_Claimant_ArisingOutOf, @CO_Claimant_Court, @CO_Claimant_CaseNumber, @CO_Claimant_CaseYear, @CO_Claimant_HighCourtBench, @CO_Claimant_OriginalCaseStatus, @CO_Claimant_IsConnected, @CO_Claimant_EntrustmentNo, @CO_Claimant_EntrustmentDate, @CO_Claimant_AdvocateName, @CO_Claimant_CaseStatus, @CO_Claimant_PetitionCopyPath,
                    @IsClaimantSCPending, @ClaimantSCDiaryNumber, @ClaimantSCYear, @ClaimantSCNumber, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus, @ClaimantSCOutcome, @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate,
                    @CO_Service_Division, @CO_Service_WPNumber, @CO_Service_WPYear, @CO_Service_PetitionerName, @CO_Service_CaseNature, @CO_Service_Prayer, @CO_Service_PetitionCopyPath, @CO_Service_IsEmployee, @CO_Service_StayGranted, @CO_Service_StayVacateFiled, @CO_Service_StayCompliance, @CO_Service_ApprovalOutwardNo, @CO_Service_ApprovalDate, @CO_Service_ApprovalCopyPath, @CO_Service_Status, @CO_Service_DisposalDate, @CO_Service_ActionTaken, @CO_Service_ApprovalSentDetails, @CO_Service_OutwardNo, @CO_Service_OutwardDate, @CO_Service_AppealFiledBefore, @CO_Service_AppealType, @CO_Service_AppealEntrustmentDate, @CO_Service_AppealAdvocate, @CO_Service_AppealStatus, @CO_Service_EntrustmentNo, @CO_Service_EntrustmentDate,
                    @JudgmentCopyPath, @JudgmentCopyPath2,
                    @IsArisingApplication, @Arising_OriginalCaseNumber, @Arising_OriginalCaseYear, @Arising_OriginalCourt, 
                    @Arising_CurrentStatus, @Arising_ApplicationStatus,
                    @IsFiledWithinLimitation, @LimitationRemark, @IsDelayCondoned, @DelayCondonationRemark,
                    @CO_StayComplianceRemark, @CO_StayComplianceFilePath, @CO_StayComplianceDate,
                    @Against_PunishmentCopyPath, @ClaimPetitionPath, @ClaimFiledOn, @DelayInFiling, @ClaimDetails,
                    @IsViewedByCO, @FavorOutwardDate,
                    GETDATE(), @CreatedBy
                );
                SELECT CAST(SCOPE_IDENTITY() as int)";

            var parameters = GetParameters(model);
            int newId;
            
            using (var connection = _db.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        newId = Convert.ToInt32(_db.ExecuteScalar(query, parameters.ToArray(), connection, transaction));

                        // Save Connected Cases
                        if (model.ConnectedCases != null && model.ConnectedCases.Any())
                        {
                            foreach (var cc in model.ConnectedCases)
                            {
                                cc.CaseID = newId;
                                AddConnectedCase(cc, connection, transaction);
                            }
                        }
                        
                        // Save Case History
                        if (model.HistoryList != null && model.HistoryList.Any())
                        {
                            foreach (var hist in model.HistoryList)
                            {
                                hist.CaseID = newId;
                                AddCaseHistory(hist, connection, transaction);
                            }
                        }

                        // Save Enclosed Documents
                        SaveEnclosedDocs(newId, model.EnclosedDocuments, connection, transaction);
                        
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return newId;
        }

        public void UpdateCase(LabourCase model)
        {
            string query = @"
                UPDATE LABOUR_CASES SET
                    ServiceID = @ServiceID, DivisionID = @DivisionID, CaseType = @CaseType, CaseStatus = @CaseStatus, CaseNumber = @CaseNumber, CaseYear = @CaseYear, 
                    CourtID = @CourtID, OtherCourtDetails = @OtherCourtDetails, CNRNumber = @CNRNumber, EstCode = @EstCode, CaseTypeCode = @CaseTypeCode,
                    PetitionerName = @PetitionerName, EmployeeNo = @EmployeeNo, PFNumber = @PFNumber, Designation = @Designation, WorkingStatus = @WorkingStatus, LegalRepresentativeName = @LegalRepresentativeName, LRRelationship = @LRRelationship, IsWorkman = @IsWorkman, IsWorkmanRemark = @IsWorkmanRemark, 
                    NatureOfCase = @NatureOfCase, NatureOfMisconduct = @NatureOfMisconduct,
                    EntrustmentNo = @EntrustmentNo, EntrustmentDate = @EntrustmentDate, AdvocateID = @AdvocateID, AdvocateName = @AdvocateName, 
                    IsDoubleClaim = @IsDoubleClaim, DoubleClaimRemarks = @DoubleClaimRemarks,
                    CurrentStage = @CurrentStage, SerialApp_CaseNumber = @SerialApp_CaseNumber, SerialApp_CurrentStage = @SerialApp_CurrentStage, CaseHistory = @CaseHistory, NextHearingDate = @NextHearingDate,
                    IsCOApprovalRequired = @IsCOApprovalRequired, COApproval_OutwardNo = @COApproval_OutwardNo, COApproval_OutwardDate = @COApproval_OutwardDate,
                    IsDocumentSent = @IsDocumentSent, DocumentSent_OutwardNo = @DocumentSent_OutwardNo, DocumentSent_OutwardDate = @DocumentSent_OutwardDate,
                    IsObjectionFiled = @IsObjectionFiled, ObjectionFiled_OutwardNo = @ObjectionFiled_OutwardNo, ObjectionFiled_OutwardDate = @ObjectionFiled_OutwardDate,
                    DisposalMode = @DisposalMode, DisposalDate = @DisposalDate, DisposalResult = @DisposalResult, FavorRemark = @FavorRemark, FavorOutwardDate = @FavorOutwardDate,
                    LokAdalat_COApprovalRequired = @LokAdalat_COApprovalRequired, LokAdalat_OutwardNo = @LokAdalat_OutwardNo, LokAdalat_Date = @LokAdalat_Date, LokAdalatDocumentPath = COALESCE(@LokAdalatDocumentPath, LokAdalatDocumentPath),
                    Against_CaseCategory = @Against_CaseCategory, Against_BriefFacts = @Against_BriefFacts, Against_PunishmentImposed = @Against_PunishmentImposed, 
                    Against_PunishmentNo = @Against_PunishmentNo, Against_PunishmentDate = @Against_PunishmentDate,
                    DE_HistorySheet = @DE_HistorySheet, DE_HistorySheetPath = COALESCE(@DE_HistorySheetPath, DE_HistorySheetPath), DE_ObjectionsFiled = @DE_ObjectionsFiled, DE_ObjectionsRemark = @DE_ObjectionsRemark,
                    DE_DocumentsMarked = @DE_DocumentsMarked, DE_DocumentsRemark = @DE_DocumentsRemark,
                    DE_Order = @DE_Order, ChargesStatus = @ChargesStatus,
                    TerminalBenefitsPaid = @TerminalBenefitsPaid, SerialApplicationDetails = @SerialApplicationDetails, 
                    IsRepeatDismissal = @IsRepeatDismissal, RepeatDismissalRemark = @RepeatDismissalRemark,
                    HasAppealDetails = @HasAppealDetails, AppealDetailsRemark = @AppealDetailsRemark,
                    DE_EO_Name = @DE_EO_Name, DE_EO_Designation = @DE_EO_Designation, DE_EO_IsBasedOnDocuments = @DE_EO_IsBasedOnDocuments, DE_EO_BasedOnDocsRemark = @DE_EO_BasedOnDocsRemark,
            DE_Reporter_Name = @DE_Reporter_Name, DE_Reporter_Designation = @DE_Reporter_Designation, DE_Reporter_IsBasedOnDocuments = @DE_Reporter_IsBasedOnDocuments, DE_Reporter_BasedOnDocsRemark = @DE_Reporter_BasedOnDocsRemark,
            DE_Other_Name = @DE_Other_Name, DE_Other_Designation = @DE_Other_Designation, DE_Other_IsBasedOnDocuments = @DE_Other_IsBasedOnDocuments, DE_Other_BasedOnDocsRemark = @DE_Other_BasedOnDocsRemark,
            IsEnquiryOfficerEvidence = @IsEnquiryOfficerEvidence, IsReporterEvidence = @IsReporterEvidence, IsOtherEvidence = @IsOtherEvidence,
            CC_CaseDisposedDate = @CC_CaseDisposedDate, CC_PublicationDate = @CC_PublicationDate, CC_AppliedDate = @CC_AppliedDate, CC_IssuedDate = @CC_IssuedDate, CC_DeliveredDate = @CC_DeliveredDate, CC_ReceivedDate = @CC_ReceivedDate, CC_Remarks = @CC_Remarks,
                    AwardDetails = @AwardDetails, Opinion_Advocate = @Opinion_Advocate, Opinion_DC = @Opinion_DC, Opinion_CLO = ISNULL(NULLIF(@Opinion_CLO, ''), Opinion_CLO),
                    -- Per-role action fields (ISNULL preserves data from other roles)
                    ActionTaken_LO = ISNULL(NULLIF(@ActionTaken_LO, ''), ActionTaken_LO),
                    ApprovalDate_LO = ISNULL(@ApprovalDate_LO, ApprovalDate_LO),
                    Opinion_LO = ISNULL(NULLIF(@Opinion_LO, ''), Opinion_LO),
                    ActionTaken_DyCLO = ISNULL(NULLIF(@ActionTaken_DyCLO, ''), ActionTaken_DyCLO),
                    ApprovalDate_DyCLO = ISNULL(@ApprovalDate_DyCLO, ApprovalDate_DyCLO),
                    Opinion_DyCLO = ISNULL(NULLIF(@Opinion_DyCLO, ''), Opinion_DyCLO),
                    ActionTaken_CLO = ISNULL(NULLIF(@ActionTaken_CLO, ''), ActionTaken_CLO),
                    ApprovalDate_CLO = ISNULL(@ApprovalDate_CLO, ApprovalDate_CLO),
                    ActionTaken_MD = ISNULL(NULLIF(@ActionTaken_MD, ''), ActionTaken_MD),
                    ApprovalDate_MD = ISNULL(@ApprovalDate_MD, ApprovalDate_MD),
                    Opinion_MD = ISNULL(NULLIF(@Opinion_MD, ''), Opinion_MD),
                    SentToCO = @SentToCO, CO_OutwardNo = @CO_OutwardNo, CO_OutwardDate = @CO_OutwardDate, CO_Remarks = @CO_Remarks,
                    
                    CO_FeasibilityReceived = @CO_FeasibilityReceived, CO_FeasibilityDate = @CO_FeasibilityDate, CO_ActionTaken = @CO_ActionTaken, 
                    CO_ApprovalOutwardNo = @CO_ApprovalOutwardNo, CO_ApprovalDate = @CO_ApprovalDate, CO_ClosedDocumentPath = COALESCE(@CO_ClosedDocumentPath, CO_ClosedDocumentPath),

                    CO_WP_CNRNumber = @CO_WP_CNRNumber, CO_WP_CaseStatus_Option = @CO_WP_CaseStatus_Option, CO_WP_CaseNumber = @CO_WP_CaseNumber, CO_WP_Year = @CO_WP_Year, 
                    CO_WP_HighCourtBench = @CO_WP_HighCourtBench, CO_WP_EntrustmentNo = @CO_WP_EntrustmentNo, CO_WP_EntrustmentDate = @CO_WP_EntrustmentDate, 
                    CO_WP_AdvocateName = @CO_WP_AdvocateName, CO_WP_StayGranted = @CO_WP_StayGranted, CO_WP_StayApprovalNo = @CO_WP_StayApprovalNo, 
                    CO_WP_StayNature = @CO_WP_StayNature, CO_WP_StayDate = @CO_WP_StayDate,
                    CO_WP_StayOrderPath = COALESCE(@CO_WP_StayOrderPath, CO_WP_StayOrderPath), CO_WP_StayRemark = @CO_WP_StayRemark,

                    CO_WA_CNRNumber = @CO_WA_CNRNumber, CO_WA_CaseStatus_Option = @CO_WA_CaseStatus_Option, CO_WA_CaseNumber = @CO_WA_CaseNumber, CO_WA_Year = @CO_WA_Year, 
                    CO_WA_HighCourtBench = @CO_WA_HighCourtBench, CO_WA_EntrustmentNo = @CO_WA_EntrustmentNo, CO_WA_EntrustmentDate = @CO_WA_EntrustmentDate, 
                    CO_WA_AdvocateName = @CO_WA_AdvocateName, CO_WA_StayGranted = @CO_WA_StayGranted, CO_WA_StayApprovalNo = @CO_WA_StayApprovalNo, 
                    CO_WA_StayNature = @CO_WA_StayNature, CO_WA_StayDate = @CO_WA_StayDate,
                    CO_WA_StayOrderPath = COALESCE(@CO_WA_StayOrderPath, CO_WA_StayOrderPath), CO_WA_StayRemark = @CO_WA_StayRemark,
                    CO_WA_Status = @CO_WA_Status, CO_WA_Outcome = @CO_WA_Outcome, CO_WA_OutcomeRemark = @CO_WA_OutcomeRemark,
                    CO_WA_OutcomeOutwardNo = @CO_WA_OutcomeOutwardNo, CO_WA_OutcomeOutwardDate = @CO_WA_OutcomeOutwardDate,
                    CO_WA_ActionTaken = @CO_WA_ActionTaken,
                    CO_StayComplianceRemark = @CO_StayComplianceRemark, CO_StayComplianceFilePath = COALESCE(@CO_StayComplianceFilePath, CO_StayComplianceFilePath), CO_StayComplianceDate = @CO_StayComplianceDate,

                    CO_IsWorkmanReinstated = @CO_IsWorkmanReinstated, CO_ReinstatedSubjectToWP = @CO_ReinstatedSubjectToWP, 
                CO_Reinstatement_StayGranted = @CO_Reinstatement_StayGranted, CO_Reinstatement_StayApprovalNo = @CO_Reinstatement_StayApprovalNo, 
                CO_Reinstatement_StayNature = @CO_Reinstatement_StayNature, CO_Reinstatement_StayDate = @CO_Reinstatement_StayDate, 
                CO_Reinstatement_StayOrderPath = COALESCE(@CO_Reinstatement_StayOrderPath, CO_Reinstatement_StayOrderPath), CO_Reinstatement_StayRemark = @CO_Reinstatement_StayRemark,
                CO_ReinstatementApprovalIssued = @CO_ReinstatementApprovalIssued, CO_ReinstatementApprovalDate = @CO_ReinstatementApprovalDate,
                CO_Reinstatement_ApprovalNo = @CO_Reinstatement_ApprovalNo, CO_Reinstatement_ApprovalCopyPath = COALESCE(@CO_Reinstatement_ApprovalCopyPath, CO_Reinstatement_ApprovalCopyPath),

                    CO_WP_Status = @CO_WP_Status, CO_OverallCaseStatus = @CO_OverallCaseStatus,
                    CO_WP_Outcome = @CO_WP_Outcome, CO_WP_OutcomeRemark = @CO_WP_OutcomeRemark, 
                    CO_WP_OutcomeOutwardNo = @CO_WP_OutcomeOutwardNo, CO_WP_OutcomeOutwardDate = @CO_WP_OutcomeOutwardDate,
                    CO_WP_ActionTaken = @CO_WP_ActionTaken, CO_WP_JudgmentCopyPath = COALESCE(@CO_WP_JudgmentCopyPath, CO_WP_JudgmentCopyPath),

                    CO_Disposal_Nature = @CO_Disposal_Nature, CO_Disposal_CommSentToDivision = @CO_Disposal_CommSentToDivision, 
                    CO_Disposal_OutwardNo = @CO_Disposal_OutwardNo, CO_Disposal_Date = @CO_Disposal_Date, CO_Disposal_Decision = @CO_Disposal_Decision, 
                    CO_Disposal_ApprovalOutwardNo = @CO_Disposal_ApprovalOutwardNo, CO_Disposal_ApprovalDate = @CO_Disposal_ApprovalDate,

                    CO_FurtherAppeal_Status_Option = @CO_FurtherAppeal_Status_Option, CO_FurtherAppeal_CaseNumber = @CO_FurtherAppeal_CaseNumber, 
                    CO_FurtherAppeal_Year = @CO_FurtherAppeal_Year, CO_FurtherAppeal_EntrustmentNo = @CO_FurtherAppeal_EntrustmentNo, 
                    CO_FurtherAppeal_EntrustmentDate = @CO_FurtherAppeal_EntrustmentDate, CO_FurtherAppeal_AdvocateName = @CO_FurtherAppeal_AdvocateName, 
                    CO_FurtherAppeal_CaseStatus = @CO_FurtherAppeal_CaseStatus, 
                    CO_FurtherAppeal_DisposalOutwardNo = @CO_FurtherAppeal_DisposalOutwardNo,
                    CO_FurtherAppeal_DisposalDate = @CO_FurtherAppeal_DisposalDate,
                    
                    CO_Claimant_CNRNumber = @CO_Claimant_CNRNumber, CO_Claimant_DivisionName = @CO_Claimant_DivisionName, CO_Claimant_ArisingOutOf = @CO_Claimant_ArisingOutOf, 
                    CO_Claimant_Court = @CO_Claimant_Court, CO_Claimant_CaseNumber = @CO_Claimant_CaseNumber, 
                    CO_Claimant_CaseYear = @CO_Claimant_CaseYear,
                    CO_Claimant_HighCourtBench = @CO_Claimant_HighCourtBench, CO_Claimant_OriginalCaseStatus = @CO_Claimant_OriginalCaseStatus, 
                    CO_Claimant_IsConnected = @CO_Claimant_IsConnected, 
                    CO_Claimant_EntrustmentNo = @CO_Claimant_EntrustmentNo, CO_Claimant_EntrustmentDate = @CO_Claimant_EntrustmentDate, CO_Claimant_AdvocateName = @CO_Claimant_AdvocateName, 
                    CO_Claimant_CaseStatus = @CO_Claimant_CaseStatus, CO_Claimant_PetitionCopyPath = COALESCE(@CO_Claimant_PetitionCopyPath, CO_Claimant_PetitionCopyPath),

                    IsClaimantSCPending = @IsClaimantSCPending, ClaimantSCDiaryNumber = @ClaimantSCDiaryNumber, 
                    ClaimantSCYear = @ClaimantSCYear, ClaimantSCNumber = @ClaimantSCNumber, ClaimantSLPYear = @ClaimantSLPYear, 
                    ClaimantSCFiledBy = @ClaimantSCFiledBy, ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo, 
                    ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate, ClaimantSCAdvocate = @ClaimantSCAdvocate, 
                    ClaimantSCStatus = @ClaimantSCStatus, ClaimantSCOutcome = @ClaimantSCOutcome, 
                    ClaimantSCActionTaken = @ClaimantSCActionTaken, ClaimantSCClosureNo = @ClaimantSCClosureNo, 
                    ClaimantSCClosureDate = @ClaimantSCClosureDate,

                    CO_Service_Division = @CO_Service_Division, CO_Service_WPNumber = @CO_Service_WPNumber, CO_Service_WPYear = @CO_Service_WPYear,
                    CO_Service_PetitionerName = @CO_Service_PetitionerName, CO_Service_CaseNature = @CO_Service_CaseNature, 
                    CO_Service_Prayer = @CO_Service_Prayer, CO_Service_PetitionCopyPath = COALESCE(@CO_Service_PetitionCopyPath, CO_Service_PetitionCopyPath),
                    CO_Service_IsEmployee = @CO_Service_IsEmployee,
                    CO_Service_StayGranted = @CO_Service_StayGranted, 
                    CO_Service_StayVacateFiled = @CO_Service_StayVacateFiled, 
                    CO_Service_StayCompliance = @CO_Service_StayCompliance,
                    CO_Service_ApprovalOutwardNo = @CO_Service_ApprovalOutwardNo,
                    CO_Service_ApprovalDate = @CO_Service_ApprovalDate,
                    CO_Service_ApprovalCopyPath = COALESCE(@CO_Service_ApprovalCopyPath, CO_Service_ApprovalCopyPath),
                    CO_Service_Status = @CO_Service_Status, 
                    CO_Service_DisposalDate = @CO_Service_DisposalDate, CO_Service_ActionTaken = @CO_Service_ActionTaken, 
                    CO_Service_ApprovalSentDetails = @CO_Service_ApprovalSentDetails, CO_Service_OutwardNo = @CO_Service_OutwardNo, 
                    CO_Service_OutwardDate = @CO_Service_OutwardDate,
                    CO_Service_AppealFiledBefore = @CO_Service_AppealFiledBefore, CO_Service_AppealType = @CO_Service_AppealType, 
                    CO_Service_AppealEntrustmentDate = @CO_Service_AppealEntrustmentDate, CO_Service_AppealAdvocate = @CO_Service_AppealAdvocate, 
                    CO_Service_AppealStatus = @CO_Service_AppealStatus,
                    CO_Service_EntrustmentNo = @CO_Service_EntrustmentNo,
                    CO_Service_EntrustmentDate = @CO_Service_EntrustmentDate,

                    JudgmentCopyPath = COALESCE(@JudgmentCopyPath, JudgmentCopyPath), JudgmentCopyPath2 = COALESCE(@JudgmentCopyPath2, JudgmentCopyPath2),
                    IsArisingApplication = @IsArisingApplication, Arising_OriginalCaseNumber = @Arising_OriginalCaseNumber, 
                    Arising_OriginalCaseYear = @Arising_OriginalCaseYear, Arising_OriginalCourt = @Arising_OriginalCourt,
                    Arising_CurrentStatus = @Arising_CurrentStatus, Arising_ApplicationStatus = @Arising_ApplicationStatus,
                    IsFiledWithinLimitation = @IsFiledWithinLimitation, LimitationRemark = @LimitationRemark,
                    IsDelayCondoned = @IsDelayCondoned, DelayCondonationRemark = @DelayCondonationRemark,
                    Against_PunishmentCopyPath = COALESCE(@Against_PunishmentCopyPath, Against_PunishmentCopyPath),
                    ClaimPetitionPath = COALESCE(@ClaimPetitionPath, ClaimPetitionPath),
                    ClaimFiledOn = @ClaimFiledOn,
                    DelayInFiling = @DelayInFiling,
                    ClaimDetails = @ClaimDetails,
                    IsViewedByCO = @IsViewedByCO,
                    ModifiedDate = GETDATE(), ModifiedBy = @ModifiedBy
                WHERE CaseID = @CaseID";

            var parameters = GetParameters(model);
            parameters.Add(new SqlParameter("@CaseID", model.CaseID));

            using (var connection = _db.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        _db.ExecuteNonQuery(query, parameters.ToArray(), connection, transaction);

                        // Save Enclosed Documents (only if provided in payload)
                        if (model.EnclosedDocuments != null)
                        {
                            SaveEnclosedDocs(model.CaseID, model.EnclosedDocuments, connection, transaction);
                        }

                        // Update Connected Cases: Delete all existing and re-insert only if either collection is submitted
                        if (model.ConnectedCases != null || model.ClaimantConnectedCases != null)
                        {
                            string deleteCCQuery = "DELETE FROM LABOUR_CONNECTED_CASES WHERE CaseID = @CaseID";
                            _db.ExecuteNonQuery(deleteCCQuery, new[] { new SqlParameter("@CaseID", model.CaseID) }, connection, transaction);

                            if (model.ConnectedCases != null && model.ConnectedCases.Any())
                            {
                                foreach (var cc in model.ConnectedCases)
                                {
                                    cc.CaseID = model.CaseID;
                                    AddConnectedCase(cc, connection, transaction);
                                }
                            }

                            if (model.ClaimantConnectedCases != null && model.ClaimantConnectedCases.Any())
                            {
                                foreach (var cc in model.ClaimantConnectedCases)
                                {
                                    cc.CaseID = model.CaseID;
                                    cc.FiledBy = "CLAIMANT APPEAL";
                                    AddConnectedCase(cc, connection, transaction);
                                }
                            }
                        }

                        // Update Case History: Delete all and re-insert only if HistoryList is submitted
                        if (model.HistoryList != null)
                        {
                            string deleteHistQuery = "DELETE FROM LABOUR_CASE_HISTORY WHERE CaseID = @CaseID";
                            _db.ExecuteNonQuery(deleteHistQuery, new[] { new SqlParameter("@CaseID", model.CaseID) }, connection, transaction);

                            if (model.HistoryList.Any())
                            {
                                foreach (var hist in model.HistoryList)
                                {
                                    hist.CaseID = model.CaseID;
                                    AddCaseHistory(hist, connection, transaction);
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private List<SqlParameter> GetParameters(LabourCase model)
        {
            return new List<SqlParameter>
            {
                new SqlParameter("@ServiceID", model.ServiceID > 0 ? (object)model.ServiceID : DBNull.Value),
                new SqlParameter("@DivisionID", model.DivisionID),
                new SqlParameter("@CaseType", (object?)model.CaseType?.Trim() ?? string.Empty),
                new SqlParameter("@CaseStatus", (object?)model.CaseStatus?.Trim() ?? "Pending"),
                new SqlParameter("@CaseNumber", (object?)model.CaseNumber?.Trim() ?? string.Empty),
                new SqlParameter("@CaseYear", (object?)model.CaseYear ?? 0), // Default to 0 if null, assuming INT NOT NULL column
                new SqlParameter("@CourtID", model.CourtID ?? (object)DBNull.Value),
                new SqlParameter("@OtherCourtDetails", model.OtherCourtDetails ?? (object)DBNull.Value),
                new SqlParameter("@CNRNumber", !string.IsNullOrWhiteSpace(model.CNRNumber) ? model.CNRNumber.Trim() : (object)DBNull.Value),
                new SqlParameter("@EstCode", (object?)model.EstCode ?? DBNull.Value),
                new SqlParameter("@CaseTypeCode", (object?)model.CaseTypeCode ?? DBNull.Value),
                new SqlParameter("@PetitionerName", (object?)model.PetitionerName?.Trim() ?? string.Empty),
                new SqlParameter("@EmployeeNo", model.EmployeeNo ?? (object)DBNull.Value),
                new SqlParameter("@PFNumber", model.PFNumber ?? (object)DBNull.Value),
                new SqlParameter("@Designation", model.Designation ?? (object)DBNull.Value),
                new SqlParameter("@WorkingStatus", model.WorkingStatus ?? (object)DBNull.Value),
                new SqlParameter("@LegalRepresentativeName", model.LegalRepresentativeName ?? (object)DBNull.Value),
                new SqlParameter("@LRRelationship", model.LRRelationship ?? (object)DBNull.Value),
                new SqlParameter("@IsWorkman", model.IsWorkman),
                new SqlParameter("@IsWorkmanRemark", model.IsWorkmanRemark ?? (object)DBNull.Value),
                new SqlParameter("@NatureOfCase", model.NatureOfCase ?? (object)DBNull.Value),
                new SqlParameter("@NatureOfMisconduct", model.NatureOfMisconduct ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentNo", model.EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentDate", model.EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@IsDoubleClaim", model.IsDoubleClaim),
                new SqlParameter("@DoubleClaimRemarks", model.DoubleClaimRemarks ?? (object)DBNull.Value),
                new SqlParameter("@CurrentStage", model.CurrentStage ?? (object)DBNull.Value),
                new SqlParameter("@SerialApp_CaseNumber", model.SerialApp_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@SerialApp_CurrentStage", model.SerialApp_CurrentStage ?? (object)DBNull.Value),
                new SqlParameter("@CaseHistory", model.CaseHistory ?? (object)DBNull.Value),
                new SqlParameter("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value),
                new SqlParameter("@IsCOApprovalRequired", model.IsCOApprovalRequired),
                new SqlParameter("@COApproval_OutwardNo", model.COApproval_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@COApproval_OutwardDate", model.COApproval_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@IsDocumentSent", model.IsDocumentSent),
                new SqlParameter("@DocumentSent_OutwardNo", model.DocumentSent_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@DocumentSent_OutwardDate", model.DocumentSent_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@IsObjectionFiled", model.IsObjectionFiled),
                new SqlParameter("@ObjectionFiled_OutwardNo", model.ObjectionFiled_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@ObjectionFiled_OutwardDate", model.ObjectionFiled_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@DisposalMode", model.DisposalMode ?? (object)DBNull.Value),
                new SqlParameter("@DisposalDate", model.DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@DisposalResult", model.DisposalResult ?? (object)DBNull.Value),
                new SqlParameter("@FavorRemark", model.FavorRemark ?? (object)DBNull.Value),
                new SqlParameter("@FavorOutwardDate", model.FavorOutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@LokAdalat_COApprovalRequired", model.LokAdalat_COApprovalRequired),
                new SqlParameter("@LokAdalat_OutwardNo", model.LokAdalat_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@LokAdalat_Date", model.LokAdalat_Date ?? (object)DBNull.Value),
                new SqlParameter("@LokAdalatDocumentPath", model.LokAdalatDocumentPath ?? (object)DBNull.Value),
                new SqlParameter("@Against_CaseCategory", model.Against_CaseCategory ?? (object)DBNull.Value),
                new SqlParameter("@Against_BriefFacts", model.Against_BriefFacts ?? (object)DBNull.Value),
                new SqlParameter("@Against_PunishmentImposed", model.Against_PunishmentImposed ?? (object)DBNull.Value),
                new SqlParameter("@Against_PunishmentNo", model.Against_PunishmentNo ?? (object)DBNull.Value),
                new SqlParameter("@Against_PunishmentDate", model.Against_PunishmentDate ?? (object)DBNull.Value),
                new SqlParameter("@Against_PunishmentCopyPath", model.Against_PunishmentCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@ClaimPetitionPath", model.ClaimPetitionPath ?? (object)DBNull.Value),
                new SqlParameter("@ClaimFiledOn", model.ClaimFiledOn ?? (object)DBNull.Value),
                new SqlParameter("@DelayInFiling", model.DelayInFiling ?? (object)DBNull.Value),
                new SqlParameter("@ClaimDetails", model.ClaimDetails ?? (object)DBNull.Value),
                new SqlParameter("@IsViewedByCO", model.IsViewedByCO),
                new SqlParameter("@DE_HistorySheet", model.DE_HistorySheet),
                new SqlParameter("@DE_HistorySheetPath", model.DE_HistorySheetPath ?? (object)DBNull.Value),
                new SqlParameter("@DE_ObjectionsFiled", model.DE_ObjectionsFiled),
                new SqlParameter("@DE_ObjectionsRemark", model.DE_ObjectionsRemark ?? (object)DBNull.Value),
                new SqlParameter("@DE_DocumentsMarked", model.DE_DocumentsMarked),
                new SqlParameter("@DE_DocumentsRemark", model.DE_DocumentsRemark ?? (object)DBNull.Value),
                new SqlParameter("@DE_Order", model.DE_Order ?? (object)DBNull.Value),
                new SqlParameter("@ChargesStatus", model.ChargesStatus ?? (object)DBNull.Value),
                new SqlParameter("@TerminalBenefitsPaid", model.TerminalBenefitsPaid ?? (object)DBNull.Value),
                new SqlParameter("@SerialApplicationDetails", model.SerialApplicationDetails ?? (object)DBNull.Value),
                new SqlParameter("@IsRepeatDismissal", model.IsRepeatDismissal),
                new SqlParameter("@RepeatDismissalRemark", model.RepeatDismissalRemark ?? (object)DBNull.Value),
                new SqlParameter("@HasAppealDetails", model.HasAppealDetails),
                new SqlParameter("@AppealDetailsRemark", model.AppealDetailsRemark ?? (object)DBNull.Value),
                new SqlParameter("@DE_EO_Name", model.DE_EO_Name ?? (object)DBNull.Value),
                new SqlParameter("@DE_EO_Designation", model.DE_EO_Designation ?? (object)DBNull.Value),
                new SqlParameter("@DE_EO_IsBasedOnDocuments", model.DE_EO_IsBasedOnDocuments),
                new SqlParameter("@DE_EO_BasedOnDocsRemark", model.DE_EO_BasedOnDocsRemark ?? (object)DBNull.Value),
                new SqlParameter("@DE_Reporter_Name", model.DE_Reporter_Name ?? (object)DBNull.Value),
                new SqlParameter("@DE_Reporter_Designation", model.DE_Reporter_Designation ?? (object)DBNull.Value),
                new SqlParameter("@DE_Reporter_IsBasedOnDocuments", model.DE_Reporter_IsBasedOnDocuments),
                new SqlParameter("@DE_Reporter_BasedOnDocsRemark", model.DE_Reporter_BasedOnDocsRemark ?? (object)DBNull.Value),
                new SqlParameter("@DE_Other_Name", model.DE_Other_Name ?? (object)DBNull.Value),
                new SqlParameter("@DE_Other_Designation", model.DE_Other_Designation ?? (object)DBNull.Value),
                new SqlParameter("@DE_Other_IsBasedOnDocuments", model.DE_Other_IsBasedOnDocuments),
                new SqlParameter("@DE_Other_BasedOnDocsRemark", model.DE_Other_BasedOnDocsRemark ?? (object)DBNull.Value),
        new SqlParameter("@IsEnquiryOfficerEvidence", model.IsEnquiryOfficerEvidence),
        new SqlParameter("@IsReporterEvidence", model.IsReporterEvidence),
        new SqlParameter("@IsOtherEvidence", model.IsOtherEvidence),
                new SqlParameter("@CC_CaseDisposedDate", model.CC_CaseDisposedDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_PublicationDate", model.CC_PublicationDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_AppliedDate", model.CC_AppliedDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_IssuedDate", model.CC_IssuedDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_DeliveredDate", model.CC_DeliveredDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_ReceivedDate", model.CC_ReceivedDate ?? (object)DBNull.Value),
                new SqlParameter("@CC_Remarks", model.CC_Remarks ?? (object)DBNull.Value),
                new SqlParameter("@AwardDetails", model.AwardDetails ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_Advocate", model.Opinion_Advocate ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_LO", model.Opinion_LO ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_DC", model.Opinion_DC ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_CLO", model.Opinion_CLO ?? (object)DBNull.Value),
                // Per-role action parameters
                new SqlParameter("@ActionTaken_LO", model.ActionTaken_LO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_LO", model.ApprovalDate_LO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_DyCLO", model.ActionTaken_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_DyCLO", model.ApprovalDate_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_DyCLO", model.Opinion_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_CLO", model.ActionTaken_CLO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_CLO", model.ApprovalDate_CLO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_MD", model.ActionTaken_MD ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_MD", model.ApprovalDate_MD ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_MD", model.Opinion_MD ?? (object)DBNull.Value),
                new SqlParameter("@SentToCO", (object?)model.SentToCO ?? 0), // Default to false (0) if null
                new SqlParameter("@CO_OutwardNo", model.CO_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_OutwardDate", model.CO_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Remarks", model.CO_Remarks ?? (object)DBNull.Value),
                new SqlParameter("@JudgmentCopyPath", model.JudgmentCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@JudgmentCopyPath2", model.JudgmentCopyPath2 ?? (object)DBNull.Value),
                new SqlParameter("@IsArisingApplication", model.IsArisingApplication),
                new SqlParameter("@Arising_OriginalCaseNumber", model.Arising_OriginalCaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@Arising_OriginalCaseYear", model.Arising_OriginalCaseYear ?? (object)DBNull.Value),
                new SqlParameter("@Arising_OriginalCourt", model.Arising_OriginalCourt ?? (object)DBNull.Value),
                new SqlParameter("@Arising_CurrentStatus", model.Arising_CurrentStatus ?? (object)DBNull.Value),
                new SqlParameter("@Arising_ApplicationStatus", model.Arising_ApplicationStatus ?? (object)DBNull.Value),
                new SqlParameter("@CreatedBy", model.CreatedBy ?? (object)DBNull.Value),
                new SqlParameter("@ModifiedBy", model.ModifiedBy ?? (object)DBNull.Value),
                
                // CO Fields
                new SqlParameter("@CO_FeasibilityReceived", model.CO_FeasibilityReceived ?? (object)DBNull.Value),
                new SqlParameter("@CO_FeasibilityDate", model.CO_FeasibilityDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_ActionTaken", model.CO_ActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@CO_ApprovalOutwardNo", model.CO_ApprovalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_ApprovalDate", model.CO_ApprovalDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_ClosedDocumentPath", model.CO_ClosedDocumentPath ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_WP_CaseStatus_Option", model.CO_WP_CaseStatus_Option ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_CNRNumber", model.CO_WP_CNRNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_CaseNumber", model.CO_WP_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_Year", model.CO_WP_Year ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_HighCourtBench", model.CO_WP_HighCourtBench ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_EntrustmentNo", model.CO_WP_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_EntrustmentDate", model.CO_WP_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_AdvocateName", model.CO_WP_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayGranted", model.CO_WP_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayApprovalNo", model.CO_WP_StayApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayNature", model.CO_WP_StayNature ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayDate", model.CO_WP_StayDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayOrderPath", model.CO_WP_StayOrderPath ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_StayRemark", model.CO_WP_StayRemark ?? (object)DBNull.Value),
                
                // WA
                new SqlParameter("@CO_WA_CaseStatus_Option", model.CO_WA_CaseStatus_Option ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_CNRNumber", model.CO_WA_CNRNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_CaseNumber", model.CO_WA_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_Year", model.CO_WA_Year ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_HighCourtBench", model.CO_WA_HighCourtBench ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_EntrustmentNo", model.CO_WA_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_EntrustmentDate", model.CO_WA_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_AdvocateName", model.CO_WA_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayGranted", model.CO_WA_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayApprovalNo", model.CO_WA_StayApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayNature", model.CO_WA_StayNature ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayDate", model.CO_WA_StayDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayOrderPath", model.CO_WA_StayOrderPath ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_StayRemark", model.CO_WA_StayRemark ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_Status", model.CO_WA_Status ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_Outcome", model.CO_WA_Outcome ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeRemark", model.CO_WA_OutcomeRemark ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeOutwardNo", model.CO_WA_OutcomeOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeOutwardDate", model.CO_WA_OutcomeOutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WA_ActionTaken", model.CO_WA_ActionTaken ?? (object)DBNull.Value),

                new SqlParameter("@CO_IsWorkmanReinstated", model.CO_IsWorkmanReinstated),
                
                new SqlParameter("@CO_ReinstatedSubjectToWP", model.CO_ReinstatedSubjectToWP ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_Reinstatement_StayGranted", model.CO_Reinstatement_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayApprovalNo", model.CO_Reinstatement_StayApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayNature", model.CO_Reinstatement_StayNature ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayDate", model.CO_Reinstatement_StayDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayOrderPath", model.CO_Reinstatement_StayOrderPath ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayRemark", model.CO_Reinstatement_StayRemark ?? (object)DBNull.Value),
                
                 new SqlParameter("@CO_ReinstatementApprovalIssued", model.CO_ReinstatementApprovalIssued ?? (object)DBNull.Value),
                new SqlParameter("@CO_ReinstatementApprovalDate", model.CO_ReinstatementApprovalDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_ApprovalNo", model.CO_Reinstatement_ApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Reinstatement_ApprovalCopyPath", model.CO_Reinstatement_ApprovalCopyPath ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_WP_Status", model.CO_WP_Status ?? (object)DBNull.Value),
                new SqlParameter("@CO_OverallCaseStatus", model.CO_OverallCaseStatus ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_WP_Outcome", model.CO_WP_Outcome ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeRemark", model.CO_WP_OutcomeRemark ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeOutwardNo", model.CO_WP_OutcomeOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeOutwardDate", model.CO_WP_OutcomeOutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_ActionTaken", model.CO_WP_ActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@CO_WP_JudgmentCopyPath", model.CO_WP_JudgmentCopyPath ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_Disposal_Nature", model.CO_Disposal_Nature ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_CommSentToDivision", model.CO_Disposal_CommSentToDivision ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_OutwardNo", model.CO_Disposal_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_Date", model.CO_Disposal_Date ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_Decision", model.CO_Disposal_Decision ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_ApprovalOutwardNo", model.CO_Disposal_ApprovalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Disposal_ApprovalDate", model.CO_Disposal_ApprovalDate ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_FurtherAppeal_Status_Option", model.CO_FurtherAppeal_Status_Option ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_CaseNumber", model.CO_FurtherAppeal_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_Year", model.CO_FurtherAppeal_Year ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_EntrustmentNo", model.CO_FurtherAppeal_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_EntrustmentDate", model.CO_FurtherAppeal_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_AdvocateName", model.CO_FurtherAppeal_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_CaseStatus", model.CO_FurtherAppeal_CaseStatus ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_DisposalOutwardNo", model.CO_FurtherAppeal_DisposalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_DisposalDate", model.CO_FurtherAppeal_DisposalDate ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_Claimant_DivisionName", model.CO_Claimant_DivisionName ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_ArisingOutOf", model.CO_Claimant_ArisingOutOf ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_Court", model.CO_Claimant_Court ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_CNRNumber", model.CO_Claimant_CNRNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_CaseNumber", model.CO_Claimant_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_CaseYear", model.CO_Claimant_CaseYear ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_HighCourtBench", model.CO_Claimant_HighCourtBench ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_OriginalCaseStatus", model.CO_Claimant_OriginalCaseStatus ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_IsConnected", model.CO_Claimant_IsConnected ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_EntrustmentNo", model.CO_Claimant_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_EntrustmentDate", model.CO_Claimant_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_AdvocateName", model.CO_Claimant_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_CaseStatus", model.CO_Claimant_CaseStatus ?? (object)DBNull.Value),
                new SqlParameter("@CO_Claimant_PetitionCopyPath", model.CO_Claimant_PetitionCopyPath ?? (object)DBNull.Value),

                new SqlParameter("@IsClaimantSCPending", model.IsClaimantSCPending),
                new SqlParameter("@ClaimantSCDiaryNumber", model.ClaimantSCDiaryNumber ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCYear", model.ClaimantSCYear ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCNumber", model.ClaimantSCNumber ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSLPYear", model.ClaimantSLPYear ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCFiledBy", model.ClaimantSCFiledBy ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCEntrustmentNo", model.ClaimantSCEntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCEntrustmentDate", model.ClaimantSCEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCAdvocate", model.ClaimantSCAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCStatus", model.ClaimantSCStatus ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCOutcome", model.ClaimantSCOutcome ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCActionTaken", model.ClaimantSCActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCClosureNo", model.ClaimantSCClosureNo ?? (object)DBNull.Value),
                new SqlParameter("@ClaimantSCClosureDate", model.ClaimantSCClosureDate ?? (object)DBNull.Value),

                new SqlParameter("@CO_Service_Division", model.CO_Service_Division ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_WPNumber", model.CO_Service_WPNumber ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_WPYear", model.CO_Service_WPYear ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_PetitionerName", model.CO_Service_PetitionerName ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_CaseNature", model.CO_Service_CaseNature ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_Prayer", model.CO_Service_Prayer ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_PetitionCopyPath", model.CO_Service_PetitionCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_IsEmployee", model.CO_Service_IsEmployee ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_StayGranted", model.CO_Service_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_StayVacateFiled", model.CO_Service_StayVacateFiled ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_StayCompliance", model.CO_Service_StayCompliance ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_ApprovalOutwardNo", model.CO_Service_ApprovalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_ApprovalDate", model.CO_Service_ApprovalDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_ApprovalCopyPath", model.CO_Service_ApprovalCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_Status", model.CO_Service_Status ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_DisposalDate", model.CO_Service_DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_ActionTaken", model.CO_Service_ActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_ApprovalSentDetails", model.CO_Service_ApprovalSentDetails ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_OutwardNo", model.CO_Service_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_OutwardDate", model.CO_Service_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_AppealFiledBefore", model.CO_Service_AppealFiledBefore ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_AppealType", model.CO_Service_AppealType ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_AppealEntrustmentDate", model.CO_Service_AppealEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_AppealAdvocate", model.CO_Service_AppealAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_AppealStatus", model.CO_Service_AppealStatus ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_EntrustmentNo", model.CO_Service_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@CO_Service_EntrustmentDate", model.CO_Service_EntrustmentDate ?? (object)DBNull.Value),
                
                new SqlParameter("@CO_StayComplianceRemark", model.CO_StayComplianceRemark ?? (object)DBNull.Value),
                new SqlParameter("@CO_StayComplianceFilePath", model.CO_StayComplianceFilePath ?? (object)DBNull.Value),
                new SqlParameter("@CO_StayComplianceDate", model.CO_StayComplianceDate ?? (object)DBNull.Value),
                new SqlParameter("@IsFiledWithinLimitation", model.IsFiledWithinLimitation ?? (object)DBNull.Value),
                new SqlParameter("@LimitationRemark", model.LimitationRemark ?? (object)DBNull.Value),
                new SqlParameter("@IsDelayCondoned", model.IsDelayCondoned ?? (object)DBNull.Value),
                new SqlParameter("@DelayCondonationRemark", model.DelayCondonationRemark ?? (object)DBNull.Value),
                new SqlParameter("@IsReinstatementViewed", model.IsReinstatementViewed)
            };
        }

        public void AddEvidence(LabourCaseEvidence evidence)
        {
            string query = "INSERT INTO LABOUR_CASE_EVIDENCE (CaseID, EvidenceType, OtherDetails) VALUES (@CaseID, @Type, @Details)";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@CaseID", evidence.CaseID),
                new SqlParameter("@Type", evidence.EvidenceType ?? (object)DBNull.Value),
                new SqlParameter("@Details", evidence.OtherDetails ?? (object)DBNull.Value)
            });
        }

        public void AddConnectedCase(LabourConnectedCase cc, SqlConnection? conn = null, SqlTransaction? trans = null)
        {
            string query = "INSERT INTO LABOUR_CONNECTED_CASES (CaseID, CaseDetails, CaseType, FiledBy, CurrentStatus) VALUES (@CaseID, @Details, @CaseType, @FiledBy, @Status)";
            var prms = new[] {
                new SqlParameter("@CaseID", cc.CaseID),
                new SqlParameter("@Details", cc.CaseDetails ?? (object)DBNull.Value),
                new SqlParameter("@CaseType", cc.CaseType ?? (object)DBNull.Value),
                new SqlParameter("@FiledBy", cc.FiledBy ?? (object)DBNull.Value),
                new SqlParameter("@Status", cc.CurrentStatus ?? (object)DBNull.Value)
            };

            if (conn != null && trans != null) _db.ExecuteNonQuery(query, prms, conn, trans);
            else _db.ExecuteNonQuery(query, prms);
        }

        public IEnumerable<LabourConnectedCase> GetConnectedCasesByCaseId(int caseId)
        {
            var list = new List<LabourConnectedCase>();
            string query = "SELECT * FROM LABOUR_CONNECTED_CASES WHERE CaseID = @CaseID";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LabourConnectedCase
                {
                    ConnectedCaseID = (int)row["ConnectedCaseID"],
                    CaseID = (int)row["CaseID"],
                    CaseDetails = row["CaseDetails"]?.ToString(),
                    CaseType = row.Table.Columns.Contains("CaseType") ? row["CaseType"]?.ToString() : null,
                    FiledBy = row["FiledBy"]?.ToString(),
                    CurrentStatus = row["CurrentStatus"]?.ToString()
                });
            }
            return list;
        }

        public IEnumerable<LabourCaseEvidence> GetEvidenceByCaseId(int caseId)
        {
            var list = new List<LabourCaseEvidence>();
            string query = "SELECT * FROM LABOUR_CASE_EVIDENCE WHERE CaseID = @CaseID";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            foreach(DataRow row in dt.Rows)
            {
                list.Add(new LabourCaseEvidence {
                    EvidenceID = (int)row["EvidenceID"],
                    CaseID = (int)row["CaseID"],
                    EvidenceType = row["EvidenceType"]?.ToString(),
                    OtherDetails = row["OtherDetails"]?.ToString()
                });
            }
            return list;
        }

        private LabourCase MapToModel(DataRow row, bool includeChildLists = false)
        {
            var model = new LabourCase
            {
                CaseID = GetInt(row, "CaseID") ?? 0,
                ServiceID = GetInt(row, "ServiceID"),
                DivisionID = GetInt(row, "DivisionID") ?? 0,
                
                // Helper to check column existence before access
                DivisionName = GetString(row, "DivisionName"),
                CaseType = GetString(row, "CaseType") ?? "",
                CaseStatus = GetString(row, "CaseStatus") ?? "Pending",
                CaseNumber = GetString(row, "CaseNumber") ?? "",
                CaseYear = GetInt(row, "CaseYear") ?? DateTime.Now.Year,
                
                CourtID = GetInt(row, "CourtID"),
                CourtName = GetString(row, "CourtName"),
                OtherCourtDetails = GetString(row, "OtherCourtDetails"),
                CNRNumber = GetString(row, "CNRNumber"),
                EstCode = GetString(row, "EstCode"),
                CaseTypeCode = GetString(row, "CaseTypeCode"),
                LastNapixSyncAt = GetDate(row, "LastNapixSyncAt"),
                LastNapixSyncStatus = GetString(row, "LastNapixSyncStatus"),
                LastNapixSyncError = GetString(row, "LastNapixSyncError"),
                PendDispStatus = GetString(row, "PendDispStatus"),
                EstName = GetString(row, "EstName"),
                ECourtsStage = GetString(row, "ECourtsStage"),
                ECourtsCourtNo = GetString(row, "ECourtsCourtNo"),
                ECourtsJudge = GetString(row, "ECourtsJudge"),
                
                PetitionerName = GetString(row, "PetitionerName") ?? "",
                EmployeeNo = GetString(row, "EmployeeNo"),
                PFNumber = GetString(row, "PFNumber"),
                Designation = GetString(row, "Designation"),
                WorkingStatus = GetString(row, "WorkingStatus"),
                LegalRepresentativeName = GetString(row, "LegalRepresentativeName"),
                LRRelationship = GetString(row, "LRRelationship"),
                IsWorkman = GetBool(row, "IsWorkman"),
                IsWorkmanRemark = GetString(row, "IsWorkmanRemark"),
                
                NatureOfCase = GetString(row, "NatureOfCase"),
                NatureOfMisconduct = GetString(row, "NatureOfMisconduct"),
                
                EntrustmentNo = GetString(row, "EntrustmentNo"),
                EntrustmentDate = GetDate(row, "EntrustmentDate"),
                
                AdvocateID = GetInt(row, "AdvocateID"),
                AdvocateName = !string.IsNullOrWhiteSpace(GetString(row, "AdvocateName")) 
                    ? GetString(row, "AdvocateName") 
                    : (!string.IsNullOrWhiteSpace(GetString(row, "LA_AdvocateName"))
                        ? GetString(row, "LA_AdvocateName")
                        : (!string.IsNullOrWhiteSpace(GetString(row, "HCA_AdvocateName")) 
                            ? GetString(row, "HCA_AdvocateName") 
                            : GetString(row, "AM_AdvocateName"))),
                
                IsDoubleClaim = GetBool(row, "IsDoubleClaim"),
                DoubleClaimRemarks = GetString(row, "DoubleClaimRemarks"),
                
                CurrentStage = GetString(row, "CurrentStage"),
                SerialApp_CaseNumber = GetString(row, "SerialApp_CaseNumber"),
                SerialApp_CurrentStage = GetString(row, "SerialApp_CurrentStage"),
                CaseHistory = GetString(row, "CaseHistory"),
                NextHearingDate = GetDate(row, "NextHearingDate"),
                
                IsCOApprovalRequired = GetBool(row, "IsCOApprovalRequired"),
                COApproval_OutwardNo = GetString(row, "COApproval_OutwardNo"),
                COApproval_OutwardDate = GetDate(row, "COApproval_OutwardDate"),
                
                IsDocumentSent = GetBool(row, "IsDocumentSent"),
                DocumentSent_OutwardNo = GetString(row, "DocumentSent_OutwardNo"),
                DocumentSent_OutwardDate = GetDate(row, "DocumentSent_OutwardDate"),
                
                IsObjectionFiled = GetBool(row, "IsObjectionFiled"),
                ObjectionFiled_OutwardNo = GetString(row, "ObjectionFiled_OutwardNo"),
                ObjectionFiled_OutwardDate = GetDate(row, "ObjectionFiled_OutwardDate"),
                
                DisposalMode = GetString(row, "DisposalMode"),
                DisposalDate = GetDate(row, "DisposalDate"),
                DisposalResult = GetString(row, "DisposalResult"),
                FavorRemark = GetString(row, "FavorRemark"),
                FavorOutwardDate = GetDate(row, "FavorOutwardDate"),
                
                LokAdalat_COApprovalRequired = GetBool(row, "LokAdalat_COApprovalRequired"),
                LokAdalat_OutwardNo = GetString(row, "LokAdalat_OutwardNo"),
                LokAdalat_Date = GetDate(row, "LokAdalat_Date"),
                LokAdalatDocumentPath = GetString(row, "LokAdalatDocumentPath"),
                
                Against_CaseCategory = GetString(row, "Against_CaseCategory"),
                Against_BriefFacts = GetString(row, "Against_BriefFacts"),
                Against_PunishmentImposed = GetString(row, "Against_PunishmentImposed"),
                Against_PunishmentNo = GetString(row, "Against_PunishmentNo"),
                Against_PunishmentDate = GetDate(row, "Against_PunishmentDate"),
                Against_PunishmentCopyPath = GetString(row, "Against_PunishmentCopyPath"),
                ClaimPetitionPath = GetString(row, "ClaimPetitionPath"),
                ClaimFiledOn = GetString(row, "ClaimFiledOn"),
                DelayInFiling = GetString(row, "DelayInFiling"),
                ClaimDetails = GetString(row, "ClaimDetails"),
                
                DE_HistorySheet = GetBool(row, "DE_HistorySheet"),
                DE_HistorySheetPath = GetString(row, "DE_HistorySheetPath"),
                DE_ObjectionsFiled = GetBool(row, "DE_ObjectionsFiled"),
                DE_ObjectionsRemark = GetString(row, "DE_ObjectionsRemark"),
                DE_DocumentsMarked = GetBool(row, "DE_DocumentsMarked"),
                DE_DocumentsRemark = GetString(row, "DE_DocumentsRemark"),
                DE_Order = GetString(row, "DE_Order"),
                ChargesStatus = GetString(row, "ChargesStatus"),
                TerminalBenefitsPaid = GetString(row, "TerminalBenefitsPaid"),
                SerialApplicationDetails = GetString(row, "SerialApplicationDetails"),
                IsRepeatDismissal = GetBool(row, "IsRepeatDismissal"),
                RepeatDismissalRemark = GetString(row, "RepeatDismissalRemark"),
                HasAppealDetails = GetBool(row, "HasAppealDetails"),
                AppealDetailsRemark = GetString(row, "AppealDetailsRemark"),
                DE_EO_Name = GetString(row, "DE_EO_Name"),
                DE_EO_Designation = GetString(row, "DE_EO_Designation"),
                DE_EO_IsBasedOnDocuments = GetBool(row, "DE_EO_IsBasedOnDocuments"),
                DE_EO_BasedOnDocsRemark = GetString(row, "DE_EO_BasedOnDocsRemark"),
                DE_Reporter_Name = GetString(row, "DE_Reporter_Name"),
                DE_Reporter_Designation = GetString(row, "DE_Reporter_Designation"),
                DE_Reporter_IsBasedOnDocuments = GetBool(row, "DE_Reporter_IsBasedOnDocuments"),
                DE_Reporter_BasedOnDocsRemark = GetString(row, "DE_Reporter_BasedOnDocsRemark"),
                DE_Other_Name = GetString(row, "DE_Other_Name"),
                DE_Other_Designation = GetString(row, "DE_Other_Designation"),
                DE_Other_IsBasedOnDocuments = GetBool(row, "DE_Other_IsBasedOnDocuments"),
                DE_Other_BasedOnDocsRemark = GetString(row, "DE_Other_BasedOnDocsRemark"),
        IsEnquiryOfficerEvidence = GetBool(row, "IsEnquiryOfficerEvidence"),
        IsReporterEvidence = GetBool(row, "IsReporterEvidence"),
        IsOtherEvidence = GetBool(row, "IsOtherEvidence"),
                
                CC_CaseDisposedDate = GetDate(row, "CC_CaseDisposedDate"),
                CC_PublicationDate = GetDate(row, "CC_PublicationDate"),
                CC_AppliedDate = GetDate(row, "CC_AppliedDate"),
                CC_IssuedDate = GetDate(row, "CC_IssuedDate"),
                CC_DeliveredDate = GetDate(row, "CC_DeliveredDate"),
                CC_ReceivedDate = GetDate(row, "CC_ReceivedDate"),
                CC_Remarks = GetString(row, "CC_Remarks"),
                
                AwardDetails = GetString(row, "AwardDetails"),
                Opinion_Advocate = GetString(row, "Opinion_Advocate"),
                Opinion_LO = GetString(row, "Opinion_LO"),
                Opinion_DC = GetString(row, "Opinion_DC"),
                Opinion_CLO = GetString(row, "Opinion_CLO"),

                // Per-role action fields
                ActionTaken_LO = GetString(row, "ActionTaken_LO"),
                ApprovalDate_LO = GetDate(row, "ApprovalDate_LO"),
                ActionTaken_DyCLO = GetString(row, "ActionTaken_DyCLO"),
                ApprovalDate_DyCLO = GetDate(row, "ApprovalDate_DyCLO"),
                Opinion_DyCLO = GetString(row, "Opinion_DyCLO"),
                ActionTaken_CLO = GetString(row, "ActionTaken_CLO"),
                ApprovalDate_CLO = GetDate(row, "ApprovalDate_CLO"),
                ActionTaken_MD = GetString(row, "ActionTaken_MD"),
                ApprovalDate_MD = GetDate(row, "ApprovalDate_MD"),
                Opinion_MD = GetString(row, "Opinion_MD"),
                
                SentToCO = GetBool(row, "SentToCO"),
                CO_OutwardNo = GetString(row, "CO_OutwardNo"),
                CO_OutwardDate = GetDate(row, "CO_OutwardDate"),
                CO_Remarks = GetString(row, "CO_Remarks"),
                
                CO_FeasibilityReceived = GetBoolNullable(row, "CO_FeasibilityReceived"),
                CO_FeasibilityDate = GetDate(row, "CO_FeasibilityDate"),
                CO_ActionTaken = GetString(row, "CO_ActionTaken"),
                CO_ApprovalOutwardNo = GetString(row, "CO_ApprovalOutwardNo"),
                CO_ApprovalDate = GetDate(row, "CO_ApprovalDate"),
                CO_ClosedDocumentPath = GetString(row, "CO_ClosedDocumentPath"),
                
                CO_WP_CaseStatus_Option = GetString(row, "CO_WP_CaseStatus_Option"),
                CO_WP_CNRNumber = GetString(row, "CO_WP_CNRNumber"),
                CO_WP_CaseNumber = GetString(row, "CO_WP_CaseNumber"),
                CO_WP_Year = GetInt(row, "CO_WP_Year"),
                CO_WP_HighCourtBench = GetString(row, "CO_WP_HighCourtBench"),
                CO_WP_EntrustmentNo = GetString(row, "CO_WP_EntrustmentNo"),
                CO_WP_EntrustmentDate = GetDate(row, "CO_WP_EntrustmentDate"),
                CO_WP_AdvocateName = GetString(row, "CO_WP_AdvocateName"),
                CO_WP_StayGranted = GetBoolNullable(row, "CO_WP_StayGranted"),
                CO_WP_StayApprovalNo = GetString(row, "CO_WP_StayApprovalNo"),
                CO_WP_StayNature = GetString(row, "CO_WP_StayNature"),
                CO_WP_StayDate = GetDate(row, "CO_WP_StayDate"),
                CO_WP_StayOrderPath = GetString(row, "CO_WP_StayOrderPath"),
                CO_WP_StayRemark = GetString(row, "CO_WP_StayRemark"),
                
                CO_IsWorkmanReinstated = GetBool(row, "CO_IsWorkmanReinstated"),
                CO_ReinstatedSubjectToWP = GetString(row, "CO_ReinstatedSubjectToWP"),
                
                CO_Reinstatement_StayGranted = GetBoolNullable(row, "CO_Reinstatement_StayGranted"),
                CO_Reinstatement_StayApprovalNo = GetString(row, "CO_Reinstatement_StayApprovalNo"),
                CO_Reinstatement_StayNature = GetString(row, "CO_Reinstatement_StayNature"),
                CO_Reinstatement_StayDate = GetDate(row, "CO_Reinstatement_StayDate"),
                CO_Reinstatement_StayOrderPath = GetString(row, "CO_Reinstatement_StayOrderPath"),
                CO_Reinstatement_StayRemark = GetString(row, "CO_Reinstatement_StayRemark"),
                
                 CO_ReinstatementApprovalIssued = GetBoolNullable(row, "CO_ReinstatementApprovalIssued"),
                 CO_ReinstatementApprovalDate = GetDate(row, "CO_ReinstatementApprovalDate"),
                 CO_Reinstatement_ApprovalNo = GetString(row, "CO_Reinstatement_ApprovalNo"),
                 CO_Reinstatement_ApprovalCopyPath = GetString(row, "CO_Reinstatement_ApprovalCopyPath"),
                
                CO_WP_Status = GetString(row, "CO_WP_Status"),
                CO_OverallCaseStatus = GetString(row, "CO_OverallCaseStatus"),
                
                CO_WP_Outcome = GetString(row, "CO_WP_Outcome"),
                CO_WP_OutcomeRemark = GetString(row, "CO_WP_OutcomeRemark"),
                CO_WP_OutcomeOutwardNo = GetString(row, "CO_WP_OutcomeOutwardNo"),
                CO_WP_OutcomeOutwardDate = GetDate(row, "CO_WP_OutcomeOutwardDate"),
                CO_WP_ActionTaken = GetString(row, "CO_WP_ActionTaken"),
                CO_WP_JudgmentCopyPath = GetString(row, "CO_WP_JudgmentCopyPath"),

                // WA
                CO_WA_CaseStatus_Option = GetString(row, "CO_WA_CaseStatus_Option"),
                CO_WA_CNRNumber = GetString(row, "CO_WA_CNRNumber"),
                CO_WA_CaseNumber = GetString(row, "CO_WA_CaseNumber"),
                CO_WA_Year = GetInt(row, "CO_WA_Year"),
                CO_WA_HighCourtBench = GetString(row, "CO_WA_HighCourtBench"),
                CO_WA_EntrustmentNo = GetString(row, "CO_WA_EntrustmentNo"),
                CO_WA_EntrustmentDate = GetDate(row, "CO_WA_EntrustmentDate"),
                CO_WA_AdvocateName = GetString(row, "CO_WA_AdvocateName"),
                CO_WA_StayGranted = GetBoolNullable(row, "CO_WA_StayGranted"),
                CO_WA_StayApprovalNo = GetString(row, "CO_WA_StayApprovalNo"),
                CO_WA_StayNature = GetString(row, "CO_WA_StayNature"),
                CO_WA_StayDate = GetDate(row, "CO_WA_StayDate"),
                CO_WA_StayOrderPath = GetString(row, "CO_WA_StayOrderPath"),
                CO_WA_StayRemark = GetString(row, "CO_WA_StayRemark"),
                CO_WA_Status = GetString(row, "CO_WA_Status"),
                CO_WA_Outcome = GetString(row, "CO_WA_Outcome"),
                CO_WA_OutcomeRemark = GetString(row, "CO_WA_OutcomeRemark"),
                CO_WA_OutcomeOutwardNo = GetString(row, "CO_WA_OutcomeOutwardNo"),
                CO_WA_OutcomeOutwardDate = GetDate(row, "CO_WA_OutcomeOutwardDate"),
                CO_WA_ActionTaken = GetString(row, "CO_WA_ActionTaken"),
                
                CO_Disposal_Nature = GetString(row, "CO_Disposal_Nature"),
                CO_Disposal_CommSentToDivision = GetBoolNullable(row, "CO_Disposal_CommSentToDivision"),
                CO_Disposal_OutwardNo = GetString(row, "CO_Disposal_OutwardNo"),
                CO_Disposal_Date = GetDate(row, "CO_Disposal_Date"),
                CO_Disposal_Decision = GetString(row, "CO_Disposal_Decision"),
                CO_Disposal_ApprovalOutwardNo = GetString(row, "CO_Disposal_ApprovalOutwardNo"),
                CO_Disposal_ApprovalDate = GetDate(row, "CO_Disposal_ApprovalDate"),
                
                CO_FurtherAppeal_Status_Option = GetString(row, "CO_FurtherAppeal_Status_Option"),
                CO_FurtherAppeal_CaseNumber = GetString(row, "CO_FurtherAppeal_CaseNumber"),
                CO_FurtherAppeal_Year = GetInt(row, "CO_FurtherAppeal_Year"),
                CO_FurtherAppeal_EntrustmentNo = GetString(row, "CO_FurtherAppeal_EntrustmentNo"),
                CO_FurtherAppeal_EntrustmentDate = GetDate(row, "CO_FurtherAppeal_EntrustmentDate"),
                CO_FurtherAppeal_AdvocateName = GetString(row, "CO_FurtherAppeal_AdvocateName"),
                CO_FurtherAppeal_CaseStatus = GetString(row, "CO_FurtherAppeal_CaseStatus"),
                CO_FurtherAppeal_DisposalOutwardNo = GetString(row, "CO_FurtherAppeal_DisposalOutwardNo"),
                CO_FurtherAppeal_DisposalDate = GetDate(row, "CO_FurtherAppeal_DisposalDate"),
                
                CO_Claimant_DivisionName = GetString(row, "CO_Claimant_DivisionName"),
                CO_Claimant_ArisingOutOf = GetString(row, "CO_Claimant_ArisingOutOf"),
                CO_Claimant_Court = GetString(row, "CO_Claimant_Court"),
                CO_Claimant_CNRNumber = GetString(row, "CO_Claimant_CNRNumber"),
                CO_Claimant_CaseNumber = GetString(row, "CO_Claimant_CaseNumber"),
                CO_Claimant_CaseYear = GetInt(row, "CO_Claimant_CaseYear"),
                CO_Claimant_HighCourtBench = GetString(row, "CO_Claimant_HighCourtBench"),
                CO_Claimant_OriginalCaseStatus = GetString(row, "CO_Claimant_OriginalCaseStatus"),
                CO_Claimant_IsConnected = GetBoolNullable(row, "CO_Claimant_IsConnected"),
                CO_Claimant_EntrustmentNo = GetString(row, "CO_Claimant_EntrustmentNo"),
                CO_Claimant_EntrustmentDate = GetDate(row, "CO_Claimant_EntrustmentDate"),
                CO_Claimant_AdvocateName = GetString(row, "CO_Claimant_AdvocateName"),
                CO_Claimant_CaseStatus = GetString(row, "CO_Claimant_CaseStatus"),
                CO_Claimant_PetitionCopyPath = GetString(row, "CO_Claimant_PetitionCopyPath"),

                // SC Appeal
                IsClaimantSCPending = GetBool(row, "IsClaimantSCPending"),
                ClaimantSCDiaryNumber = GetString(row, "ClaimantSCDiaryNumber"),
                ClaimantSCYear = GetInt(row, "ClaimantSCYear"),
                ClaimantSCNumber = GetString(row, "ClaimantSCNumber"),
                ClaimantSLPYear = GetInt(row, "ClaimantSLPYear"),
                ClaimantSCFiledBy = GetString(row, "ClaimantSCFiledBy"),
                ClaimantSCEntrustmentNo = GetString(row, "ClaimantSCEntrustmentNo"),
                ClaimantSCEntrustmentDate = GetDate(row, "ClaimantSCEntrustmentDate"),
                ClaimantSCAdvocate = GetString(row, "ClaimantSCAdvocate"),
                ClaimantSCStatus = GetString(row, "ClaimantSCStatus"),
                ClaimantSCOutcome = GetString(row, "ClaimantSCOutcome"),
                ClaimantSCActionTaken = GetString(row, "ClaimantSCActionTaken"),
                ClaimantSCClosureNo = GetString(row, "ClaimantSCClosureNo"),
                ClaimantSCClosureDate = GetDate(row, "ClaimantSCClosureDate"),
                
                CO_Service_Division = GetString(row, "CO_Service_Division"),
                CO_Service_WPNumber = GetString(row, "CO_Service_WPNumber"),
                CO_Service_WPYear = GetInt(row, "CO_Service_WPYear"),
                CO_Service_PetitionerName = GetString(row, "CO_Service_PetitionerName"),
                CO_Service_CaseNature = GetString(row, "CO_Service_CaseNature"),
                CO_Service_Prayer = GetString(row, "CO_Service_Prayer"),
                CO_Service_PetitionCopyPath = GetString(row, "CO_Service_PetitionCopyPath"),
                CO_Service_IsEmployee = GetBoolNullable(row, "CO_Service_IsEmployee"),
                CO_Service_StayGranted = GetBoolNullable(row, "CO_Service_StayGranted"),
                CO_Service_StayVacateFiled = GetBoolNullable(row, "CO_Service_StayVacateFiled"),
                CO_Service_StayCompliance = GetBoolNullable(row, "CO_Service_StayCompliance"),
                CO_Service_ApprovalOutwardNo = GetString(row, "CO_Service_ApprovalOutwardNo"),
                CO_Service_ApprovalDate = GetDate(row, "CO_Service_ApprovalDate"),
                CO_Service_ApprovalCopyPath = GetString(row, "CO_Service_ApprovalCopyPath"),
                CO_Service_Status = GetString(row, "CO_Service_Status"),
                CO_Service_DisposalDate = GetDate(row, "CO_Service_DisposalDate"),
                CO_Service_ActionTaken = GetString(row, "CO_Service_ActionTaken"),
                CO_Service_ApprovalSentDetails = GetString(row, "CO_Service_ApprovalSentDetails"),
                CO_Service_OutwardNo = GetString(row, "CO_Service_OutwardNo"),
                CO_Service_OutwardDate = GetDate(row, "CO_Service_OutwardDate"),
                CO_Service_AppealFiledBefore = GetString(row, "CO_Service_AppealFiledBefore"),
                CO_Service_AppealType = GetString(row, "CO_Service_AppealType"),
                CO_Service_AppealEntrustmentDate = GetDate(row, "CO_Service_AppealEntrustmentDate"),
                CO_Service_AppealAdvocate = GetString(row, "CO_Service_AppealAdvocate"),
                CO_Service_AppealStatus = GetString(row, "CO_Service_AppealStatus"),
                CO_Service_EntrustmentNo = GetString(row, "CO_Service_EntrustmentNo"),
                CO_Service_EntrustmentDate = GetDate(row, "CO_Service_EntrustmentDate"),
                
                JudgmentCopyPath = GetString(row, "JudgmentCopyPath"),
                JudgmentCopyPath2 = GetString(row, "JudgmentCopyPath2"),
                
                IsArisingApplication = GetBool(row, "IsArisingApplication"),
                Arising_OriginalCaseNumber = GetString(row, "Arising_OriginalCaseNumber"),
                Arising_OriginalCaseYear = GetInt(row, "Arising_OriginalCaseYear"),
                Arising_OriginalCourt = GetString(row, "Arising_OriginalCourt"),
                Arising_CurrentStatus = GetString(row, "Arising_CurrentStatus"),
                Arising_ApplicationStatus = GetString(row, "Arising_ApplicationStatus"),
                CreatedDate = GetDate(row, "CreatedDate") ?? DateTime.MinValue,
                CreatedBy = GetInt(row, "CreatedBy"),
                ModifiedDate = GetDate(row, "ModifiedDate"),
                ModifiedBy = GetInt(row, "ModifiedBy"),
                CO_StayComplianceDate = GetDate(row, "CO_StayComplianceDate"),
                CO_StayComplianceRemark = GetString(row, "CO_StayComplianceRemark"),
                CO_StayComplianceFilePath = GetString(row, "CO_StayComplianceFilePath"),
                
                TransferredFromDivisionID = GetInt(row, "TransferredFromDivisionID"),
                TransferDate = GetDate(row, "TransferDate"),
                IsTransferViewed = GetBool(row, "IsTransferViewed"),
                IsFiledWithinLimitation = GetBoolNullable(row, "IsFiledWithinLimitation"),
                LimitationRemark = GetString(row, "LimitationRemark"),
                IsDelayCondoned = GetBoolNullable(row, "IsDelayCondoned"),
                DelayCondonationRemark = GetString(row, "DelayCondonationRemark"),
                IsViewedByCO = GetBool(row, "IsViewedByCO"),
                EnclosedDocuments = includeChildLists ? GetEnclosedDocs(GetInt(row, "CaseID") ?? 0) : new List<EnclosedDocument>(),
                ReinstatedDocuments = includeChildLists ? GetReinstatedDocuments(GetInt(row, "CaseID") ?? 0).ToList() : new List<LabourReinstatementDocument>(),

                Arising_CO_WP_CaseNumber = GetString(row, "Arising_CO_WP_CaseNumber"),
                Arising_CO_WP_Year = GetInt(row, "Arising_CO_WP_Year"),
                Arising_CO_WP_Status = GetString(row, "Arising_CO_WP_Status"),
                Arising_CO_WA_CaseNumber = GetString(row, "Arising_CO_WA_CaseNumber"),
                Arising_CO_WA_Year = GetInt(row, "Arising_CO_WA_Year"),
                Arising_CO_WA_Status = GetString(row, "Arising_CO_WA_Status")
            };
            return model;
        }

        // --- Safe Helpers (Delegated to Common.DataRowExtensions) ---
        private string? GetString(DataRow row, string colName) => row.GetString(colName);
        private int? GetInt(DataRow row, string colName) => row.GetInt(colName);
        private DateTime? GetDate(DataRow row, string colName) => row.GetDate(colName);
        private bool GetBool(DataRow row, string colName) => row.GetBool(colName);
        private bool? GetBoolNullable(DataRow row, string colName) => row.GetBoolNullable(colName);
        
        public void MarkCaseAsViewed(int caseId)
        {
            try 
            {
                string checkQuery = "SELECT COUNT(*) FROM LABOUR_CASE_VIEW_TRACKING WHERE CaseID = @CaseID";
                int existingCount = (int)_db.ExecuteScalar(checkQuery, new[] { new SqlParameter("@CaseID", caseId) })!;
                
                if (existingCount == 0)
                {
                    string insertQuery = "INSERT INTO LABOUR_CASE_VIEW_TRACKING (CaseID, ViewedAt) VALUES (@CaseID, GETDATE())";
                    _db.ExecuteNonQuery(insertQuery, new[] { new SqlParameter("@CaseID", caseId) });
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                Console.WriteLine("Error in MarkCaseAsViewed: " + ex.Message);
            }
        }

        public LabourCase? GetCaseByNumber(string caseNumber, int year, string? court = null, string? caseType = null)
        {
            if (string.IsNullOrWhiteSpace(caseNumber)) return null;

            string cleanNo = caseNumber.Replace("WP", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace("WA", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace(" ", "")
                                       .Replace(".", "")
                                       .Replace("/", "")
                                       .Trim();
            if (string.IsNullOrEmpty(cleanNo)) cleanNo = caseNumber.Trim();

            string cleanCourt = "";
            if (!string.IsNullOrWhiteSpace(court))
            {
                cleanCourt = court.Split('(')[0].Trim();
            }

            string query = @"
                SELECT TOP 1 c.*, d.DivisionNameEnglish as DivisionName, ct.CourtName
                FROM LABOUR_CASES c
                JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                LEFT JOIN LABOUR_COURTS ct ON c.CourtID = ct.CourtID
                WHERE (
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CO_WP_CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CO_Claimant_CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.Arising_OriginalCaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%'
                )
                AND (
                    @Year = 0 OR
                    c.CaseYear = @Year OR
                    c.CO_WP_Year = @Year OR
                    c.CO_Claimant_CaseYear = @Year OR
                    c.Arising_OriginalCaseYear = @Year
                )";

            var parameters = new List<SqlParameter> {
                new SqlParameter("@CleanNo", cleanNo),
                new SqlParameter("@Year", year)
            };

            if (!string.IsNullOrEmpty(cleanCourt))
            {
                query += @" AND (
                    ct.CourtName LIKE @Court OR 
                    c.OtherCourtDetails LIKE @Court OR
                    c.CO_WP_HighCourtBench LIKE @Court OR
                    c.CO_Claimant_HighCourtBench LIKE @Court OR
                    c.CO_Claimant_Court LIKE @Court OR
                    c.CO_WA_HighCourtBench LIKE @Court OR
                    c.Arising_OriginalCourt LIKE @Court
                )";
                parameters.Add(new SqlParameter("@Court", "%" + cleanCourt + "%"));
            }

            if (!string.IsNullOrEmpty(caseType))
            {
                query += " AND (c.CaseType = @CaseType OR c.CaseType LIKE '%' + @CaseType + '%')";
                parameters.Add(new SqlParameter("@CaseType", caseType));
            }

            query += " ORDER BY c.CaseID DESC";

            DataTable dt = _db.ExecuteQuery(query, parameters.ToArray());
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return MapToModel(row);
        }

        public List<LabourCase> GetCasesByNumber(string caseNumber, int year)
        {
            if (string.IsNullOrWhiteSpace(caseNumber)) return new List<LabourCase>();

            string cleanNo = caseNumber.Replace("WP", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace("WA", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace(" ", "")
                                       .Replace(".", "")
                                       .Replace("/", "")
                                       .Trim();
            if (string.IsNullOrEmpty(cleanNo)) cleanNo = caseNumber.Trim();

            string query = @"
                SELECT c.*, d.DivisionNameEnglish as DivisionName, ct.CourtName
                FROM LABOUR_CASES c
                JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                LEFT JOIN LABOUR_COURTS ct ON c.CourtID = ct.CourtID
                WHERE (
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CO_WP_CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.CO_Claimant_CaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%' OR
                    REPLACE(REPLACE(REPLACE(REPLACE(c.Arising_OriginalCaseNumber, 'WP', ''), ' ', ''), '.', ''), '/', '') LIKE '%' + @CleanNo + '%'
                )
                AND (
                    @Year = 0 OR
                    c.CaseYear = @Year OR
                    c.CO_WP_Year = @Year OR
                    c.CO_Claimant_CaseYear = @Year OR
                    c.Arising_OriginalCaseYear = @Year
                )
                ORDER BY c.CaseID DESC";

            var parameters = new List<SqlParameter> {
                new SqlParameter("@CleanNo", cleanNo),
                new SqlParameter("@Year", year)
            };

            DataTable dt = _db.ExecuteQuery(query, parameters.ToArray());
            var list = new List<LabourCase>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapToModel(row));
            }
            return list;
        }

        public bool TransferCase(int caseId, int toDivisionId, string remarks)
        {
            // First get the current division to record where it came from
            int fromDivId = 0;
            try { fromDivId = (int?)_db.ExecuteScalar("SELECT DivisionID FROM LABOUR_CASES WHERE CaseID = @ID", new[] { new SqlParameter("@ID", caseId) }) ?? 0; } catch {}

            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string updateQuery = @"
                            UPDATE LABOUR_CASES 
                            SET DivisionID = @ToDivID,
                                TransferredFromDivisionID = @FromDivID,
                                TransferDate = GETDATE(),
                                IsTransferViewed = 0
                            WHERE CaseID = @CaseID";

                        using var cmd = new SqlCommand(updateQuery, conn, trans);
                        cmd.Parameters.AddWithValue("@ToDivID", toDivisionId);
                        cmd.Parameters.AddWithValue("@FromDivID", fromDivId);
                        cmd.Parameters.AddWithValue("@CaseID", caseId);
                        cmd.ExecuteNonQuery();

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        return false;
                    }
                }
            }
        }

        public IEnumerable<LabourCase> GetRecentTransfers(int divisionId)
        {
            var list = new List<LabourCase>();
            // Logic: Get cases transferred to this division that haven't been viewed yet
            // OR were transferred in the last 7 days (optional backup logic, but sticking to IsTransferViewed flag for notification behavior)
            string query = @"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName, fromDiv.DivisionNameEnglish as FromDivisionName
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                LEFT JOIN DIVISION_MASTER fromDiv ON c.TransferredFromDivisionID = fromDiv.DivisionID
                WHERE c.DivisionID = @DivisionID 
                  AND c.IsTransferViewed = 0
                  AND c.TransferredFromDivisionID IS NOT NULL
                ORDER BY c.TransferDate DESC";

            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionId) });
                foreach (DataRow row in dt.Rows)
                {
                    var model = MapToModel(row);
                    model.TransferredFromDivisionID = row["TransferredFromDivisionID"] != DBNull.Value ? Convert.ToInt32(row["TransferredFromDivisionID"]) : 0;
                    model.TransferredFromDivisionName = row["FromDivisionName"] != DBNull.Value ? row["FromDivisionName"].ToString() : "Unknown Division";
                    list.Add(model);
                }
            }
            catch 
            {
               // Ignore if columns missing (before migration run)
            }
            return list;
        }

        public void MarkTransferAsViewed(int caseId)
        {
            string query = "UPDATE LABOUR_CASES SET IsTransferViewed = 1 WHERE CaseID = @CaseID";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
        }
        
        public IEnumerable<LabourCase> GetReinstatementNotifications(int divisionId)
        {
            var list = new List<LabourCase>();
            string query = @"
                SELECT c.*, lc.CourtName, dm.DivisionNameEnglish as DivisionName
                FROM LABOUR_CASES c
                LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                WHERE c.DivisionID = @DivisionID 
                  AND (c.IsReinstatementViewed = 0 OR c.IsReinstatementViewed IS NULL)
                  AND c.CO_IsWorkmanReinstated = 1 
                  AND c.CO_ReinstatementApprovalDate IS NOT NULL 
                  AND c.CO_Reinstatement_ApprovalNo IS NOT NULL 
                  AND c.CO_Reinstatement_ApprovalNo <> '' 
                  AND c.CO_Reinstatement_ApprovalCopyPath IS NOT NULL 
                  AND c.CO_Reinstatement_ApprovalCopyPath <> ''
                ORDER BY c.CO_ReinstatementApprovalDate DESC";

            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionId) });
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(MapToModel(row));
                }
            }
            catch { }
            return list;
        }

        public void MarkReinstatementAsViewed(int caseId)
        {
            string query = "UPDATE LABOUR_CASES SET IsReinstatementViewed = 1 WHERE CaseID = @CaseID";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
        }

        public void MarkAsViewedByCO(int caseId)
        {
            string query = "UPDATE LABOUR_CASES SET IsViewedByCO = 1 WHERE CaseID = @CaseID";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
        }

        private bool ToBool(object? value)
        {
            if (value == null || value == DBNull.Value) return false;
            if (value is bool b) return b;
            
            string s = value.ToString()?.Trim() ?? "";
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            return false;
        }
        public void AddCaseHistory(LabourCaseHistory history, SqlConnection? conn = null, SqlTransaction? trans = null)
        {
            string query = "INSERT INTO LABOUR_CASE_HISTORY (CaseID, CurrentStage, NextHearingDate, Remarks, CreatedDate) VALUES (@CaseID, @Stage, @NextDate, @Remarks, @CreatedDate)";
            var prms = new[] {
                new SqlParameter("@CaseID", history.CaseID),
                new SqlParameter("@Stage", history.CurrentStage ?? (object)DBNull.Value),
                new SqlParameter("@NextDate", history.NextHearingDate ?? (object)DBNull.Value),
                new SqlParameter("@Remarks", history.Remarks ?? (object)DBNull.Value),
                new SqlParameter("@CreatedDate", history.CreatedDate)
            };

            if (conn != null && trans != null) _db.ExecuteNonQuery(query, prms, conn, trans);
            else _db.ExecuteNonQuery(query, prms);
        }

        public IEnumerable<LabourCaseHistory> GetCaseHistory(int caseId)
        {
            var list = new List<LabourCaseHistory>();
            string query = "SELECT * FROM LABOUR_CASE_HISTORY WHERE CaseID = @CaseID ORDER BY CreatedDate DESC";
            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new LabourCaseHistory
                    {
                        HistoryID = Convert.ToInt32(row["HistoryID"]),
                        CaseID = Convert.ToInt32(row["CaseID"]),
                        CurrentStage = row["CurrentStage"].ToString(),
                        NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                        Remarks = row["Remarks"].ToString(),
                        CreatedDate = Convert.ToDateTime(row["CreatedDate"])
                    });
                }
            }
            catch { }
            return list;
        }

        public IEnumerable<LabourReinstatementDocument> GetReinstatedDocuments(int caseId)
        {
            var list = new List<LabourReinstatementDocument>();
            string query = "SELECT * FROM LABOUR_REINSTATED_DOCUMENTS WHERE CaseID = @CaseID ORDER BY UploadedDate DESC";
            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new LabourReinstatementDocument
                    {
                        DocumentID = Convert.ToInt32(row["DocumentID"]),
                        CaseID = Convert.ToInt32(row["CaseID"]),
                        DocumentName = row["DocumentName"].ToString() ?? "",
                        DocumentPath = row["DocumentPath"].ToString() ?? "",
                        UploadedDate = Convert.ToDateTime(row["UploadedDate"])
                    });
                }
            }
            catch { }
            return list;
        }

        public void AddReinstatedDocument(LabourReinstatementDocument doc)
        {
            string query = "INSERT INTO LABOUR_REINSTATED_DOCUMENTS (CaseID, DocumentName, DocumentPath, UploadedDate) VALUES (@CaseID, @DocumentName, @DocumentPath, GETDATE())";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@CaseID", doc.CaseID),
                new SqlParameter("@DocumentName", doc.DocumentName),
                new SqlParameter("@DocumentPath", doc.DocumentPath)
            });
        }

        public IEnumerable<LabourCase> GetServiceMatters(int divisionId)
        {
            var list = new List<LabourCase>();
            string query = @"
                SELECT s.*, d.DivisionNameEnglish as DivisionName 
                FROM LABOUR_SERVICE_MATTERS s 
                LEFT JOIN DIVISION_MASTER d ON s.DivisionID = d.DivisionID
                WHERE (@DivisionID = 0 OR @DivisionID = 5 OR s.DivisionID = @DivisionID OR s.DivisionID = 0 OR s.DivisionID IS NULL)
                ORDER BY s.CreatedDate DESC";
            
            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionId) });
                foreach (DataRow row in dt.Rows)
                {
                    try
                    {
                        list.Add(MapServiceToModel(row));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error mapping service matter row: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying service matters: {ex.Message}");
            }
            return list;
        }

        public int SaveServiceMatter(LabourCase model)
        {
            string query = @"
                INSERT INTO LABOUR_SERVICE_MATTERS (
                    CaseID, DivisionID, WPNumber, PetitionerName, CaseNature, Prayer, PetitionCopyPath,
                    StayGranted, StayVacateFiled, StayCompliance, ApprovalOutwardNo, ApprovalDate, ApprovalCopyPath,
                    Status, DisposalDate, ActionTaken, ApprovalSentDetails, OutwardNo, OutwardDate,
                    EntrustmentNo, EntrustmentDate,
                    AppealFiledBefore, AppealType, AppealEntrustmentDate, AppealAdvocate, AppealStatus,
                    WA_CaseNumber, WA_Year, WA_HighCourtBench, WA_EntrustmentNo, WA_EntrustmentDate, WA_AdvocateName,
                    WA_StayGranted, WA_StayApprovalNo, WA_StayNature, WA_StayDate, WA_StayOrderPath, WA_StayRemark,
                    WA_Status, WA_Outcome, WA_OutcomeRemark, WA_OutcomeOutwardNo, WA_OutcomeOutwardDate, WA_ActionTaken,
                    SC_Pending, SC_DiaryNumber, SC_Year, SC_Number, SC_SLPYear, SC_FiledBy, SC_EntrustmentNo, SC_EntrustmentDate, 
                    SC_Advocate, SC_Status, SC_Outcome, SC_ActionTaken, SC_ClosureNo, SC_ClosureDate,
                    WPYear, IsEmployee, CNRNumber,
                    CreatedDate, CreatedBy
                ) VALUES (
                    @CaseID, @DivisionID, @WPNumber, @PetitionerName, @CaseNature, @Prayer, @PetitionCopyPath,
                    @StayGranted, @StayVacateFiled, @StayCompliance, @ApprovalOutwardNo, @ApprovalDate, @ApprovalCopyPath,
                    @Status, @DisposalDate, @ActionTaken, @ApprovalSentDetails, @OutwardNo, @OutwardDate,
                    @EntrustmentNo, @EntrustmentDate,
                    @AppealFiledBefore, @AppealType, @AppealEntrustmentDate, @AppealAdvocate, @AppealStatus,
                    @WA_CaseNumber, @WA_Year, @WA_HighCourtBench, @WA_EntrustmentNo, @WA_EntrustmentDate, @WA_AdvocateName,
                    @WA_StayGranted, @WA_StayApprovalNo, @WA_StayNature, @WA_StayDate, @WA_StayOrderPath, @WA_StayRemark,
                    @WA_Status, @WA_Outcome, @WA_OutcomeRemark, @WA_OutcomeOutwardNo, @WA_OutcomeOutwardDate, @WA_ActionTaken,
                    @SC_Pending, @SC_DiaryNumber, @SC_Year, @SC_Number, @SC_SLPYear, @SC_FiledBy, @SC_EntrustmentNo, @SC_EntrustmentDate,
                    @SC_Advocate, @SC_Status, @SC_Outcome, @SC_ActionTaken, @SC_ClosureNo, @SC_ClosureDate,
                    @WPYear, @IsEmployee, @CNRNumber,
                    GETDATE(), @CreatedBy
                );
                SELECT CAST(SCOPE_IDENTITY() as int)";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@CaseID", model.CaseID > 0 ? (object)model.CaseID : DBNull.Value),
                new SqlParameter("@DivisionID", model.DivisionID),
                new SqlParameter("@WPNumber", model.CO_Service_WPNumber ?? (object)DBNull.Value),
                new SqlParameter("@PetitionerName", model.CO_Service_PetitionerName ?? (object)DBNull.Value),
                new SqlParameter("@CaseNature", model.CO_Service_CaseNature ?? (object)DBNull.Value),
                new SqlParameter("@Prayer", model.CO_Service_Prayer ?? (object)DBNull.Value),
                new SqlParameter("@PetitionCopyPath", model.CO_Service_PetitionCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@StayGranted", model.CO_Service_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@StayVacateFiled", model.CO_Service_StayVacateFiled ?? (object)DBNull.Value),
                new SqlParameter("@StayCompliance", model.CO_Service_StayCompliance ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalOutwardNo", model.CO_Service_ApprovalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate", model.CO_Service_ApprovalDate ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalCopyPath", model.CO_Service_ApprovalCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@Status", model.CO_Service_Status ?? (object)DBNull.Value),
                new SqlParameter("@DisposalDate", model.CO_Service_DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken", model.CO_Service_ActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalSentDetails", model.CO_Service_ApprovalSentDetails ?? (object)DBNull.Value),
                new SqlParameter("@OutwardNo", model.CO_Service_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@OutwardDate", model.CO_Service_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentNo", model.CO_Service_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentDate", model.CO_Service_EntrustmentDate ?? (object)DBNull.Value),
                
                new SqlParameter("@AppealFiledBefore", model.CO_Service_AppealFiledBefore ?? (object)DBNull.Value),
                new SqlParameter("@AppealType", model.CO_Service_AppealType ?? (object)DBNull.Value),
                new SqlParameter("@AppealEntrustmentDate", model.CO_Service_AppealEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@AppealAdvocate", model.CO_Service_AppealAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@AppealStatus", model.CO_Service_AppealStatus ?? (object)DBNull.Value),
                
                new SqlParameter("@WA_CaseNumber", model.CO_WA_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@WA_Year", model.CO_WA_Year ?? (object)DBNull.Value),
                new SqlParameter("@WA_HighCourtBench", model.CO_WA_HighCourtBench ?? (object)DBNull.Value),
                new SqlParameter("@WA_EntrustmentNo", model.CO_WA_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_EntrustmentDate", model.CO_WA_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_AdvocateName", model.CO_WA_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayGranted", model.CO_WA_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayApprovalNo", model.CO_WA_StayApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayNature", model.CO_WA_StayNature ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayDate", model.CO_WA_StayDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayOrderPath", model.CO_WA_StayOrderPath ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayRemark", model.CO_WA_StayRemark ?? (object)DBNull.Value),
                new SqlParameter("@WA_Status", model.CO_WA_Status ?? (object)DBNull.Value),
                new SqlParameter("@WA_Outcome", model.CO_WA_Outcome ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeRemark", model.CO_WA_OutcomeRemark ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeOutwardNo", model.CO_WA_OutcomeOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeOutwardDate", model.CO_WA_OutcomeOutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_ActionTaken", model.CO_WA_ActionTaken ?? (object)DBNull.Value),

                new SqlParameter("@SC_Pending", model.IsClaimantSCPending  ? 1 : 0),
                new SqlParameter("@SC_DiaryNumber", model.ClaimantSCDiaryNumber ?? (object)DBNull.Value),
                new SqlParameter("@SC_Year", model.ClaimantSCYear ?? (object)DBNull.Value),
                new SqlParameter("@SC_Number", model.ClaimantSCNumber ?? (object)DBNull.Value),
                new SqlParameter("@SC_SLPYear", model.ClaimantSLPYear ?? (object)DBNull.Value),
                new SqlParameter("@SC_FiledBy", model.ClaimantSCFiledBy ?? (object)DBNull.Value),
                new SqlParameter("@SC_EntrustmentNo", model.ClaimantSCEntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@SC_EntrustmentDate", model.ClaimantSCEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@SC_Advocate", model.ClaimantSCAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@SC_Status", model.ClaimantSCStatus ?? (object)DBNull.Value),
                new SqlParameter("@SC_Outcome", model.ClaimantSCOutcome ?? (object)DBNull.Value),
                new SqlParameter("@SC_ActionTaken", model.ClaimantSCActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@SC_ClosureNo", model.ClaimantSCClosureNo ?? (object)DBNull.Value),
                new SqlParameter("@SC_ClosureDate", model.ClaimantSCClosureDate ?? (object)DBNull.Value),
                new SqlParameter("@WPYear", model.CO_Service_WPYear ?? (object)DBNull.Value),
                new SqlParameter("@IsEmployee", model.CO_Service_IsEmployee ?? (object)DBNull.Value),
                new SqlParameter("@CNRNumber", model.CNRNumber ?? (object)DBNull.Value),
                
                new SqlParameter("@CreatedBy", model.CreatedBy ?? (object)DBNull.Value)
            };

            return Convert.ToInt32(_db.ExecuteScalar(query, parameters.ToArray()));
        }

        public void UpdateServiceMatter(LabourCase model)
        {
            string query = @"
                UPDATE LABOUR_SERVICE_MATTERS SET
                    DivisionID = @DivisionID,
                    WPNumber = @WPNumber,
                    PetitionerName = @PetitionerName,
                    CaseNature = @CaseNature,
                    Prayer = @Prayer,
                    PetitionCopyPath = COALESCE(@PetitionCopyPath, PetitionCopyPath),
                    StayGranted = @StayGranted,
                    StayVacateFiled = @StayVacateFiled,
                    StayCompliance = @StayCompliance,
                    ApprovalOutwardNo = @ApprovalOutwardNo,
                    ApprovalDate = @ApprovalDate,
                    ApprovalCopyPath = COALESCE(@ApprovalCopyPath, ApprovalCopyPath),
                    Status = @Status,
                    DisposalDate = @DisposalDate,
                    ActionTaken = @ActionTaken,
                    ApprovalSentDetails = @ApprovalSentDetails,
                    OutwardNo = @OutwardNo,
                    OutwardDate = @OutwardDate,
                    EntrustmentNo = @EntrustmentNo,
                    EntrustmentDate = @EntrustmentDate,
                    
                    AppealFiledBefore = @AppealFiledBefore,
                    AppealType = @AppealType,
                    AppealEntrustmentDate = @AppealEntrustmentDate,
                    AppealAdvocate = @AppealAdvocate,
                    AppealStatus = @AppealStatus,
                    
                    WA_CaseNumber = @WA_CaseNumber,
                    WA_Year = @WA_Year,
                    WA_HighCourtBench = @WA_HighCourtBench,
                    WA_EntrustmentNo = @WA_EntrustmentNo,
                    WA_EntrustmentDate = @WA_EntrustmentDate,
                    WA_AdvocateName = @WA_AdvocateName,
                    WA_StayGranted = @WA_StayGranted,
                    WA_StayApprovalNo = @WA_StayApprovalNo,
                    WA_StayNature = @WA_StayNature,
                    WA_StayDate = @WA_StayDate,
                    WA_StayOrderPath = COALESCE(@WA_StayOrderPath, WA_StayOrderPath),
                    WA_StayRemark = @WA_StayRemark,
                    WA_Status = @WA_Status,
                    WA_Outcome = @WA_Outcome,
                    WA_OutcomeRemark = @WA_OutcomeRemark,
                    WA_OutcomeOutwardNo = @WA_OutcomeOutwardNo,
                    WA_OutcomeOutwardDate = @WA_OutcomeOutwardDate,
                    WA_ActionTaken = @WA_ActionTaken,
                    SC_Pending = @SC_Pending,
                    SC_DiaryNumber = @SC_DiaryNumber,
                    SC_Year = @SC_Year,
                    SC_Number = @SC_Number,
                    SC_SLPYear = @SC_SLPYear,
                    SC_FiledBy = @SC_FiledBy,
                    SC_EntrustmentNo = @SC_EntrustmentNo,
                    SC_EntrustmentDate = @SC_EntrustmentDate,
                    SC_Advocate = @SC_Advocate,
                    SC_Status = @SC_Status,
                    SC_Outcome = @SC_Outcome,
                    SC_ActionTaken = @SC_ActionTaken,
                    SC_ClosureNo = @SC_ClosureNo,
                    SC_ClosureDate = @SC_ClosureDate,
                    WPYear = @WPYear,
                    IsEmployee = @IsEmployee,
                    CNRNumber = @CNRNumber,
                    -- Per-role action fields (ISNULL preserves data from other roles)
                    ActionTaken_LO = ISNULL(NULLIF(@ActionTaken_LO, ''), ActionTaken_LO),
                    ApprovalDate_LO = ISNULL(@ApprovalDate_LO, ApprovalDate_LO),
                    Opinion_LO = ISNULL(NULLIF(@Opinion_LO, ''), Opinion_LO),
                    ActionTaken_DyCLO = ISNULL(NULLIF(@ActionTaken_DyCLO, ''), ActionTaken_DyCLO),
                    ApprovalDate_DyCLO = ISNULL(@ApprovalDate_DyCLO, ApprovalDate_DyCLO),
                    Opinion_DyCLO = ISNULL(NULLIF(@Opinion_DyCLO, ''), Opinion_DyCLO),
                    ActionTaken_CLO = ISNULL(NULLIF(@ActionTaken_CLO, ''), ActionTaken_CLO),
                    ApprovalDate_CLO = ISNULL(@ApprovalDate_CLO, ApprovalDate_CLO),
                    Opinion_CLO = ISNULL(NULLIF(@Opinion_CLO, ''), Opinion_CLO),
                    ActionTaken_MD = ISNULL(NULLIF(@ActionTaken_MD, ''), ActionTaken_MD),
                    ApprovalDate_MD = ISNULL(@ApprovalDate_MD, ApprovalDate_MD),
                    Opinion_MD = ISNULL(NULLIF(@Opinion_MD, ''), Opinion_MD),
                    ModifiedDate = GETDATE(),
                    ModifiedBy = @CreatedBy
                WHERE ServiceID = @ServiceID";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@ServiceID", model.ServiceID),
                new SqlParameter("@DivisionID", model.DivisionID),
                new SqlParameter("@WPNumber", model.CO_Service_WPNumber ?? (object)DBNull.Value),
                new SqlParameter("@PetitionerName", model.CO_Service_PetitionerName ?? (object)DBNull.Value),
                new SqlParameter("@CaseNature", model.CO_Service_CaseNature ?? (object)DBNull.Value),
                new SqlParameter("@Prayer", model.CO_Service_Prayer ?? (object)DBNull.Value),
                new SqlParameter("@PetitionCopyPath", model.CO_Service_PetitionCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@StayGranted", model.CO_Service_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@StayVacateFiled", model.CO_Service_StayVacateFiled ?? (object)DBNull.Value),
                new SqlParameter("@StayCompliance", model.CO_Service_StayCompliance ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalOutwardNo", model.CO_Service_ApprovalOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate", model.CO_Service_ApprovalDate ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalCopyPath", model.CO_Service_ApprovalCopyPath ?? (object)DBNull.Value),
                new SqlParameter("@Status", model.CO_Service_Status ?? (object)DBNull.Value),
                new SqlParameter("@DisposalDate", model.CO_Service_DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken", model.CO_Service_ActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalSentDetails", model.CO_Service_ApprovalSentDetails ?? (object)DBNull.Value),
                new SqlParameter("@OutwardNo", model.CO_Service_OutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@OutwardDate", model.CO_Service_OutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentNo", model.CO_Service_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@EntrustmentDate", model.CO_Service_EntrustmentDate ?? (object)DBNull.Value),
                
                new SqlParameter("@AppealFiledBefore", model.CO_Service_AppealFiledBefore ?? (object)DBNull.Value),
                new SqlParameter("@AppealType", model.CO_Service_AppealType ?? (object)DBNull.Value),
                new SqlParameter("@AppealEntrustmentDate", model.CO_Service_AppealEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@AppealAdvocate", model.CO_Service_AppealAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@AppealStatus", model.CO_Service_AppealStatus ?? (object)DBNull.Value),
                
                new SqlParameter("@WA_CaseNumber", model.CO_WA_CaseNumber ?? (object)DBNull.Value),
                new SqlParameter("@WA_Year", model.CO_WA_Year ?? (object)DBNull.Value),
                new SqlParameter("@WA_HighCourtBench", model.CO_WA_HighCourtBench ?? (object)DBNull.Value),
                new SqlParameter("@WA_EntrustmentNo", model.CO_WA_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_EntrustmentDate", model.CO_WA_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_AdvocateName", model.CO_WA_AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayGranted", model.CO_WA_StayGranted ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayApprovalNo", model.CO_WA_StayApprovalNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayNature", model.CO_WA_StayNature ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayDate", model.CO_WA_StayDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayOrderPath", model.CO_WA_StayOrderPath ?? (object)DBNull.Value),
                new SqlParameter("@WA_StayRemark", model.CO_WA_StayRemark ?? (object)DBNull.Value),
                new SqlParameter("@WA_Status", model.CO_WA_Status ?? (object)DBNull.Value),
                new SqlParameter("@WA_Outcome", model.CO_WA_Outcome ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeRemark", model.CO_WA_OutcomeRemark ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeOutwardNo", model.CO_WA_OutcomeOutwardNo ?? (object)DBNull.Value),
                new SqlParameter("@WA_OutcomeOutwardDate", model.CO_WA_OutcomeOutwardDate ?? (object)DBNull.Value),
                new SqlParameter("@WA_ActionTaken", model.CO_WA_ActionTaken ?? (object)DBNull.Value),

                new SqlParameter("@SC_Pending", model.IsClaimantSCPending ? 1 : 0),
                new SqlParameter("@SC_DiaryNumber", model.ClaimantSCDiaryNumber ?? (object)DBNull.Value),
                new SqlParameter("@SC_Year", model.ClaimantSCYear ?? (object)DBNull.Value),
                new SqlParameter("@SC_Number", model.ClaimantSCNumber ?? (object)DBNull.Value),
                new SqlParameter("@SC_SLPYear", model.ClaimantSLPYear ?? (object)DBNull.Value),
                new SqlParameter("@SC_FiledBy", model.ClaimantSCFiledBy ?? (object)DBNull.Value),
                new SqlParameter("@SC_EntrustmentNo", model.ClaimantSCEntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@SC_EntrustmentDate", model.ClaimantSCEntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@SC_Advocate", model.ClaimantSCAdvocate ?? (object)DBNull.Value),
                new SqlParameter("@SC_Status", model.ClaimantSCStatus ?? (object)DBNull.Value),
                new SqlParameter("@SC_Outcome", model.ClaimantSCOutcome ?? (object)DBNull.Value),
                new SqlParameter("@SC_ActionTaken", model.ClaimantSCActionTaken ?? (object)DBNull.Value),
                new SqlParameter("@SC_ClosureNo", model.ClaimantSCClosureNo ?? (object)DBNull.Value),
                new SqlParameter("@SC_ClosureDate", model.ClaimantSCClosureDate ?? (object)DBNull.Value),
                new SqlParameter("@WPYear", model.CO_Service_WPYear ?? (object)DBNull.Value),
                new SqlParameter("@IsEmployee", model.CO_Service_IsEmployee ?? (object)DBNull.Value),
                new SqlParameter("@CNRNumber", model.CNRNumber ?? (object)DBNull.Value),

                // Per-role action parameters
                new SqlParameter("@ActionTaken_LO", model.ActionTaken_LO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_LO", model.ApprovalDate_LO ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_LO", model.Opinion_LO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_DyCLO", model.ActionTaken_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_DyCLO", model.ApprovalDate_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_DyCLO", model.Opinion_DyCLO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_CLO", model.ActionTaken_CLO ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_CLO", model.ApprovalDate_CLO ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_CLO", model.Opinion_CLO ?? (object)DBNull.Value),
                new SqlParameter("@ActionTaken_MD", model.ActionTaken_MD ?? (object)DBNull.Value),
                new SqlParameter("@ApprovalDate_MD", model.ApprovalDate_MD ?? (object)DBNull.Value),
                new SqlParameter("@Opinion_MD", model.Opinion_MD ?? (object)DBNull.Value),

                new SqlParameter("@CreatedBy", model.ModifiedBy ?? model.CreatedBy ?? (object)DBNull.Value)
            };
            
            _db.ExecuteNonQuery(query, parameters.ToArray());
        }

        public bool UpdateServiceMatterRoleAction(int serviceId, string role, string actionTaken, DateTime? approvalDate, string opinion, int? modifiedBy)
        {
            string colAction = "";
            string colDate = "";
            string colOpinion = "";
            string extraUpdate = "";

            switch (role?.Trim().ToUpper())
            {
                case "LO":
                    colAction = "ActionTaken_LO";
                    colDate = "ApprovalDate_LO";
                    colOpinion = "Opinion_LO";
                    break;
                case "DY CLO":
                case "DYCLO":
                    colAction = "ActionTaken_DyCLO";
                    colDate = "ApprovalDate_DyCLO";
                    colOpinion = "Opinion_DyCLO";
                    break;
                case "CLO":
                    colAction = "ActionTaken_CLO";
                    colDate = "ApprovalDate_CLO";
                    colOpinion = "Opinion_CLO";
                    extraUpdate = @", ActionTaken = CASE WHEN @ActionTaken = 'Approved' THEN 'Pending before Competent Authority' WHEN @ActionTaken = 'Rejected' THEN 'CLOSED' ELSE ActionTaken END,
                                     ApprovalDate = @ApprovalDate";
                    break;
                case "MD":
                    colAction = "ActionTaken_MD";
                    colDate = "ApprovalDate_MD";
                    colOpinion = "Opinion_MD";
                    extraUpdate = @", ActionTaken = CASE WHEN @ActionTaken = 'Approved' THEN 'APPROVED' WHEN @ActionTaken = 'Rejected' THEN 'REJECTED' ELSE ActionTaken END,
                                     ApprovalDate = @ApprovalDate";
                    break;
                default:
                    return false;
            }

            string query = $@"
                UPDATE LABOUR_SERVICE_MATTERS SET
                    {colAction} = @ActionTaken,
                    {colDate} = @ApprovalDate,
                    {colOpinion} = @Opinion,
                    ModifiedDate = GETDATE(),
                    ModifiedBy = @ModifiedBy
                    {extraUpdate}
                WHERE ServiceID = @ServiceID";

            var parameters = new[]
            {
                new SqlParameter("@ServiceID", serviceId),
                new SqlParameter("@ActionTaken", (object?)actionTaken ?? DBNull.Value),
                new SqlParameter("@ApprovalDate", (object?)approvalDate ?? DBNull.Value),
                new SqlParameter("@Opinion", (object?)opinion ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", (object?)modifiedBy ?? DBNull.Value)
            };

            int rows = _db.ExecuteNonQuery(query, parameters);
            return rows > 0;
        }

        public bool UpdateCNR(int caseId, string? cnrNumber, string? estCode, int modifiedBy)
        {
            string query = @"
                UPDATE LABOUR_CASES 
                SET CNRNumber = @CNRNumber, 
                    EstCode = COALESCE(@EstCode, EstCode),
                    CO_WP_CNRNumber = CASE WHEN CO_WP_CaseNumber IS NOT NULL AND (CO_WP_CNRNumber IS NULL OR CO_WP_CNRNumber = '') THEN @CNRNumber ELSE CO_WP_CNRNumber END,
                    ModifiedDate = GETDATE(),
                    ModifiedBy = @ModifiedBy
                WHERE CaseID = @CaseID;

                UPDATE s
                SET s.CNRNumber = @CNRNumber,
                    s.EstCode = COALESCE(@EstCode, s.EstCode),
                    s.ModifiedDate = GETDATE(),
                    s.ModifiedBy = @ModifiedBy
                FROM LABOUR_SERVICE_MATTERS s
                WHERE s.ServiceID = @CaseID OR s.CaseID = @CaseID;";

            var sqlParams = new[]
            {
                new SqlParameter("@CaseID", caseId),
                new SqlParameter("@CNRNumber", (object?)cnrNumber ?? DBNull.Value),
                new SqlParameter("@EstCode", (object?)estCode ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", modifiedBy)
            };

            return _db.ExecuteNonQuery(query, sqlParams) > 0;
        }

        public bool UpdateAppealCNR(int caseId, string appealType, string? cnrNumber, int modifiedBy)
        {
            string normalized = (appealType ?? "").Trim().ToUpperInvariant();
            string col = normalized switch
            {
                "CORP_WA" or "WA" or "CO_WA" => "CO_WA_CNRNumber",
                "CLAIMANT_WP" or "CLAIMANT" or "CO_CLAIMANT" => "CO_Claimant_CNRNumber",
                _ => "CO_WP_CNRNumber"
            };

            string query = $@"
                UPDATE LABOUR_CASES 
                SET {col} = @CNRNumber, 
                    ModifiedDate = GETDATE(),
                    ModifiedBy = @ModifiedBy
                WHERE CaseID = @CaseID;";

            var sqlParams = new[]
            {
                new SqlParameter("@CaseID", caseId),
                new SqlParameter("@CNRNumber", (object?)cnrNumber ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", modifiedBy)
            };

            return _db.ExecuteNonQuery(query, sqlParams) > 0;
        }

        public bool UpdateLiveSyncInfo(int caseId, DateTime? nextHearingDate, string? stage, string? courtHall, int modifiedBy)
        {
            try
            {
                string query = @"
                    UPDATE LABOUR_CASES 
                    SET NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                        CurrentStage = COALESCE(NULLIF(@Stage, ''), CurrentStage),
                        CaseStatus = CASE 
                            WHEN @Stage IS NOT NULL AND UPPER(LTRIM(RTRIM(@Stage))) IN ('DISPOSED', 'DISMISSED', 'CLOSED', 'DECIDED') THEN 'Disposed'
                            WHEN CaseStatus NOT IN ('Pending', 'Disposed', 'DNP', 'Ex-parte') OR CaseStatus IS NULL OR CaseStatus = '' THEN 'Pending'
                            ELSE CaseStatus 
                        END,
                        OtherCourtDetails = COALESCE(NULLIF(@CourtHall, ''), OtherCourtDetails),
                        ModifiedDate = GETDATE(),
                        ModifiedBy = @ModifiedBy
                    WHERE CaseID = @CaseID;

                    UPDATE LABOUR_SERVICE_MATTERS
                    SET NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                        Stage = CASE WHEN @Stage IS NOT NULL AND @Stage <> '' AND @Stage <> 'Pending Sync...' THEN @Stage ELSE Stage END,
                        CourtHall = COALESCE(NULLIF(@CourtHall, ''), CourtHall),
                        ModifiedDate = GETDATE(),
                        ModifiedBy = @ModifiedBy
                    WHERE ServiceID = @CaseID OR CaseID = @CaseID;";

                var parameters = new[]
                {
                    new SqlParameter("@CaseID", caseId),
                    new SqlParameter("@NextHearingDate", (object?)nextHearingDate ?? DBNull.Value),
                    new SqlParameter("@Stage", (object?)stage ?? DBNull.Value),
                    new SqlParameter("@CourtHall", (object?)courtHall ?? DBNull.Value),
                    new SqlParameter("@ModifiedBy", modifiedBy)
                };

                return _db.ExecuteNonQuery(query, parameters) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateLiveSyncInfo Error: {ex.Message}");
                return false;
            }
        }

        public LabourCase? GetServiceMatterById(int id)
        {
            string query = @"
                SELECT s.*, d.DivisionNameEnglish as DivisionName
                FROM LABOUR_SERVICE_MATTERS s 
                LEFT JOIN DIVISION_MASTER d ON s.DivisionID = d.DivisionID
                WHERE s.ServiceID = @ServiceID";
                
            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ServiceID", id) });
            if (dt.Rows.Count > 0)
            {
                return MapServiceToModel(dt.Rows[0]);
            }
            return null;
        }

        private LabourCase MapServiceToModel(DataRow row)
        {
            var status = GetString(row, "Status");
            var disposalDate = GetDate(row, "DisposalDate");
            var actionTaken = GetString(row, "ActionTaken");
            var petitioner = GetString(row, "PetitionerName");
            var advocate = GetString(row, "AppealAdvocate");
            if (string.IsNullOrEmpty(advocate)) advocate = GetString(row, "WA_AdvocateName");
            if (string.IsNullOrEmpty(advocate)) advocate = GetString(row, "SC_Advocate");
            
            var entrustmentNo = GetString(row, "EntrustmentNo");
            var entrustmentDate = GetDate(row, "EntrustmentDate");

            var model = new LabourCase
            {
                ServiceID = GetInt(row, "ServiceID") ?? 0,
                CaseID = GetInt(row, "CaseID") ?? 0,
                DivisionID = GetInt(row, "DivisionID") ?? 0,
                DivisionName = GetString(row, "DivisionName"),
                CaseType = "Service Matter",
                CaseNumber = GetString(row, "WPNumber") ?? string.Empty,
                CaseYear = GetInt(row, "WPYear") ?? 0,
                CNRNumber = GetString(row, "CNRNumber"),
                EstCode = GetString(row, "EstCode"),
                CaseTypeCode = GetString(row, "CaseTypeCode"),
                NextHearingDate = GetDate(row, "NextHearingDate"),
                
                // Standard properties mapped for UI summary cards & Details views
                PetitionerName = petitioner,
                CaseStatus = !string.IsNullOrWhiteSpace(status) ? status : "Pending",
                DisposalDate = disposalDate,
                DisposalResult = !string.IsNullOrWhiteSpace(actionTaken) ? actionTaken : status,
                AdvocateName = advocate,
                EntrustmentNo = entrustmentNo,
                EntrustmentDate = entrustmentDate,
                CourtName = !string.IsNullOrWhiteSpace(GetString(row, "CourtHall")) 
                            ? GetString(row, "CourtHall") 
                            : (GetString(row, "AppealFiledBefore") ?? "High Court"),
                CO_OverallCaseStatus = status,
                CO_ActionTaken = actionTaken,
                AwardDetails = GetString(row, "Prayer"),
                FavorRemark = GetString(row, "CaseNature"),
                
                CO_Service_WPNumber = GetString(row, "WPNumber") ?? string.Empty,
                CO_Service_WPYear = GetInt(row, "WPYear"),
                CO_Service_PetitionerName = petitioner,
                CO_Service_CaseNature = GetString(row, "CaseNature"),
                CO_Service_Prayer = GetString(row, "Prayer"),
                CO_Service_PetitionCopyPath = GetString(row, "PetitionCopyPath"),
                CO_Service_IsEmployee = GetBoolNullable(row, "IsEmployee"),
                CO_Service_StayGranted = GetBool(row, "StayGranted"),
                CO_Service_StayVacateFiled = GetBool(row, "StayVacateFiled"),
                CO_Service_StayCompliance = GetBool(row, "StayCompliance"),
                CO_Service_ApprovalOutwardNo = GetString(row, "ApprovalOutwardNo"),
                CO_Service_ApprovalDate = GetDate(row, "ApprovalDate"),
                CO_Service_ApprovalCopyPath = GetString(row, "ApprovalCopyPath"),
                CO_Service_Status = status,
                CO_Service_DisposalDate = disposalDate,
                CO_Service_ActionTaken = actionTaken,
                CO_Service_ApprovalSentDetails = GetString(row, "ApprovalSentDetails"),
                CO_Service_OutwardNo = GetString(row, "OutwardNo"),
                CO_Service_OutwardDate = GetDate(row, "OutwardDate"),
                CO_Service_EntrustmentNo = entrustmentNo,
                CO_Service_EntrustmentDate = entrustmentDate,
                
                CO_Service_AppealFiledBefore = GetString(row, "AppealFiledBefore"),
                CO_Service_AppealType = GetString(row, "AppealType"),
                CO_Service_AppealEntrustmentDate = GetDate(row, "AppealEntrustmentDate"),
                CO_Service_AppealAdvocate = GetString(row, "AppealAdvocate"),
                CO_Service_AppealStatus = GetString(row, "AppealStatus"),
                
                CO_WA_CaseNumber = GetString(row, "WA_CaseNumber"),
                CO_WA_Year = GetInt(row, "WA_Year"),
                CO_WA_HighCourtBench = GetString(row, "WA_HighCourtBench"),
                CO_WA_EntrustmentNo = GetString(row, "WA_EntrustmentNo"),
                CO_WA_EntrustmentDate = GetDate(row, "WA_EntrustmentDate"),
                CO_WA_AdvocateName = GetString(row, "WA_AdvocateName"),
                CO_WA_StayGranted = GetBool(row, "WA_StayGranted"),
                CO_WA_StayApprovalNo = GetString(row, "WA_StayApprovalNo"),
                CO_WA_StayNature = GetString(row, "WA_StayNature"),
                CO_WA_StayDate = GetDate(row, "WA_StayDate"),
                CO_WA_StayOrderPath = GetString(row, "WA_StayOrderPath"),
                CO_WA_StayRemark = GetString(row, "WA_StayRemark"),
                CO_WA_Status = GetString(row, "WA_Status"),
                CO_WA_Outcome = GetString(row, "WA_Outcome"),
                CO_WA_OutcomeRemark = GetString(row, "WA_OutcomeRemark"),
                CO_WA_OutcomeOutwardNo = GetString(row, "WA_OutcomeOutwardNo"),
                CO_WA_OutcomeOutwardDate = GetDate(row, "WA_OutcomeOutwardDate"),
                CO_WA_ActionTaken = GetString(row, "WA_ActionTaken"),

                IsClaimantSCPending = GetBool(row, "SC_Pending"),
                ClaimantSCDiaryNumber = GetString(row, "SC_DiaryNumber"),
                ClaimantSCYear = GetInt(row, "SC_Year"),
                ClaimantSCNumber = GetString(row, "SC_Number"),
                ClaimantSLPYear = GetInt(row, "SC_SLPYear"),
                ClaimantSCFiledBy = GetString(row, "SC_FiledBy"),
                ClaimantSCEntrustmentNo = GetString(row, "SC_EntrustmentNo"),
                ClaimantSCEntrustmentDate = GetDate(row, "SC_EntrustmentDate"),
                ClaimantSCAdvocate = GetString(row, "SC_Advocate"),
                ClaimantSCStatus = GetString(row, "SC_Status"),
                ClaimantSCOutcome = GetString(row, "SC_Outcome"),
                ClaimantSCActionTaken = GetString(row, "SC_ActionTaken"),
                ClaimantSCClosureNo = GetString(row, "SC_ClosureNo"),
                ClaimantSCClosureDate = GetDate(row, "SC_ClosureDate"),

                // Per-role action fields
                ActionTaken_LO = GetString(row, "ActionTaken_LO"),
                ApprovalDate_LO = GetDate(row, "ApprovalDate_LO"),
                Opinion_LO = GetString(row, "Opinion_LO"),

                ActionTaken_DyCLO = GetString(row, "ActionTaken_DyCLO"),
                ApprovalDate_DyCLO = GetDate(row, "ApprovalDate_DyCLO"),
                Opinion_DyCLO = GetString(row, "Opinion_DyCLO"),

                ActionTaken_CLO = GetString(row, "ActionTaken_CLO"),
                ApprovalDate_CLO = GetDate(row, "ApprovalDate_CLO"),
                Opinion_CLO = GetString(row, "Opinion_CLO"),

                ActionTaken_MD = GetString(row, "ActionTaken_MD"),
                ApprovalDate_MD = GetDate(row, "ApprovalDate_MD"),
                Opinion_MD = GetString(row, "Opinion_MD"),
                
                CreatedDate = GetDate(row, "CreatedDate") ?? DateTime.MinValue
            };
            return model;
        }
        private void SaveEnclosedDocs(int caseId, List<EnclosedDocument> docs, SqlConnection conn, SqlTransaction trans)
        {
            if (docs == null) return;

            // Clear existing
            _db.ExecuteNonQuery("DELETE FROM LABOUR_ENCLOSED_DOCS WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", caseId) }, conn, trans);

            if (docs != null && docs.Any())
            {
                foreach (var doc in docs)
                {
                    if (!string.IsNullOrEmpty(doc.DocName))
                    {
                        string q = "INSERT INTO LABOUR_ENCLOSED_DOCS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)";
                        _db.ExecuteNonQuery(q, new[] {
                            new SqlParameter("@CaseID", caseId),
                            new SqlParameter("@DocName", doc.DocName),
                            new SqlParameter("@PageCount", (object?)doc.PageCount ?? DBNull.Value)
                        }, conn, trans);
                    }
                }
            }
        }

        private List<EnclosedDocument> GetEnclosedDocs(int caseId)
        {
            var list = new List<EnclosedDocument>();
            string query = "SELECT DocName, PageCount FROM LABOUR_ENCLOSED_DOCS WHERE CaseID = @CaseID";
            try
            {
                var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new EnclosedDocument
                    {
                        DocName = row["DocName"]?.ToString(),
                        PageCount = row["PageCount"] != DBNull.Value ? Convert.ToInt32(row["PageCount"]) : null
                    });
                }
            }
            catch { }
            return list;
        }
    }
}

