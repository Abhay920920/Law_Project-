using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class AppealRepository : IAppealRepository
    {
        private readonly DBHelper _db;

        public AppealRepository(DBHelper db)
        {
            _db = db;
        }

        public AppealViewModel GetAppealByCaseId(int caseId)
        {
            string query = "SELECT * FROM APPEAL_DETAILS WHERE CaseID = @CaseID";
            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            
            if (dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];
            var vm = new AppealViewModel
            {
                AppealID = (int)r["AppealID"],
                CaseID = (int)r["CaseID"],
                
                // 1. Feasibility
                FeasibilityReceived = r["FeasibilityReceived"] != DBNull.Value && (bool)r["FeasibilityReceived"],
                FeasibilityReceiptDate = r["FeasibilityReceiptDate"] != DBNull.Value ? (DateTime?)r["FeasibilityReceiptDate"] : null,
                InitialAction = r["InitialAction"]?.ToString(),
                InitialActionRemarks = r.Table.Columns.Contains("InitialActionRemarks") && r["InitialActionRemarks"] != DBNull.Value ? r["InitialActionRemarks"].ToString() : null,
                Opinion_CLO = r.Table.Columns.Contains("Opinion_CLO") && r["Opinion_CLO"] != DBNull.Value ? r["Opinion_CLO"].ToString() : null,
                ApprovalOutwardNo = r["ApprovalOutwardNo"]?.ToString(),
                ApprovalDate = r["ApprovalDate"] != DBNull.Value ? (DateTime?)r["ApprovalDate"] : null,
                ApprovalCopyPath = r.Table.Columns.Contains("ApprovalCopyPath") && r["ApprovalCopyPath"] != DBNull.Value ? r["ApprovalCopyPath"].ToString() : null,
                InitialActionPath1 = r.Table.Columns.Contains("InitialActionPath1") && r["InitialActionPath1"] != DBNull.Value ? r["InitialActionPath1"].ToString() : null,
                InitialActionPath2 = r.Table.Columns.Contains("InitialActionPath2") && r["InitialActionPath2"] != DBNull.Value ? r["InitialActionPath2"].ToString() : null,

                // Per-role action fields
                ActionTaken_LO = r.Table.Columns.Contains("ActionTaken_LO") && r["ActionTaken_LO"] != DBNull.Value ? r["ActionTaken_LO"].ToString() : null,
                ApprovalDate_LO = r.Table.Columns.Contains("ApprovalDate_LO") && r["ApprovalDate_LO"] != DBNull.Value ? (DateTime?)r["ApprovalDate_LO"] : null,
                Opinion_LO = r.Table.Columns.Contains("Opinion_LO") && r["Opinion_LO"] != DBNull.Value ? r["Opinion_LO"].ToString() : null,
                ActionTaken_DyCLO = r.Table.Columns.Contains("ActionTaken_DyCLO") && r["ActionTaken_DyCLO"] != DBNull.Value ? r["ActionTaken_DyCLO"].ToString() : null,
                ApprovalDate_DyCLO = r.Table.Columns.Contains("ApprovalDate_DyCLO") && r["ApprovalDate_DyCLO"] != DBNull.Value ? (DateTime?)r["ApprovalDate_DyCLO"] : null,
                Opinion_DyCLO = r.Table.Columns.Contains("Opinion_DyCLO") && r["Opinion_DyCLO"] != DBNull.Value ? r["Opinion_DyCLO"].ToString() : null,
                ActionTaken_CLO = r.Table.Columns.Contains("ActionTaken_CLO") && r["ActionTaken_CLO"] != DBNull.Value ? r["ActionTaken_CLO"].ToString() : null,
                ApprovalDate_CLO = r.Table.Columns.Contains("ApprovalDate_CLO") && r["ApprovalDate_CLO"] != DBNull.Value ? (DateTime?)r["ApprovalDate_CLO"] : null,
                ActionTaken_MD = r.Table.Columns.Contains("ActionTaken_MD") && r["ActionTaken_MD"] != DBNull.Value ? r["ActionTaken_MD"].ToString() : null,
                ApprovalDate_MD = r.Table.Columns.Contains("ApprovalDate_MD") && r["ApprovalDate_MD"] != DBNull.Value ? (DateTime?)r["ApprovalDate_MD"] : null,
                Opinion_MD = r.Table.Columns.Contains("Opinion_MD") && r["Opinion_MD"] != DBNull.Value ? r["Opinion_MD"].ToString() : null,

                // 2. Corp MFA
                CorpMFANumber = r["CorpMFANumber"]?.ToString(),
                CorpMFAYear = r["CorpMFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["CorpMFAYear"]) : null,
                HighCourtBench = r["HighCourtBench"]?.ToString(),
                OtherHighCourtBench = r.Table.Columns.Contains("OtherHighCourtBench") && r["OtherHighCourtBench"] != DBNull.Value ? r["OtherHighCourtBench"].ToString() : null,
                IsPendingForFiling = r.Table.Columns.Contains("IsPendingForFiling") && r["IsPendingForFiling"] != DBNull.Value && (bool)r["IsPendingForFiling"],
                CorpMFAEntrustmentNo = r["CorpMFAEntrustmentNo"]?.ToString(),
                CorpMFAEntrustmentDate = r["CorpMFAEntrustmentDate"] != DBNull.Value ? (DateTime?)r["CorpMFAEntrustmentDate"] : null,
                CorpMFAAdvocate = r["CorpMFAAdvocate"]?.ToString(),
                CorpMFACNRNumber = r.Table.Columns.Contains("CorpMFACNRNumber") ? r["CorpMFACNRNumber"]?.ToString() : null,
                CorpMFANextHearingDate = r.Table.Columns.Contains("CorpMFANextHearingDate") && r["CorpMFANextHearingDate"] != DBNull.Value ? (DateTime?)r["CorpMFANextHearingDate"] : null,
                CorpMFAStage = r.Table.Columns.Contains("CorpMFAStage") ? r["CorpMFAStage"]?.ToString() : null,

                // 3. Stay
                StayGranted = r["StayGranted"] != DBNull.Value && (bool)r["StayGranted"],
                StayComplianceOutwardNo = r["StayComplianceOutwardNo"]?.ToString(),
                StayComplianceDate = r["StayComplianceDate"] != DBNull.Value ? (DateTime?)r["StayComplianceDate"] : null,
                StayOrderPath1 = r.Table.Columns.Contains("StayOrderPath1") && r["StayOrderPath1"] != DBNull.Value ? r["StayOrderPath1"].ToString() : null,
                StayOrderPath2 = r.Table.Columns.Contains("StayOrderPath2") && r["StayOrderPath2"] != DBNull.Value ? r["StayOrderPath2"].ToString() : null,
                ComplianceLetterPath = r.Table.Columns.Contains("ComplianceLetterPath") && r["ComplianceLetterPath"] != DBNull.Value ? r["ComplianceLetterPath"].ToString() : null,

                // 4. MFA Status
                CorpMFAStatus = r["CorpMFAStatus"]?.ToString(),
                RestorationFiled = r["RestorationFiled"] != DBNull.Value && (bool)r["RestorationFiled"],
                RestorationDate = r["RestorationDate"] != DBNull.Value ? (DateTime?)r["RestorationDate"] : null,
                RestorationStatus = r["RestorationStatus"]?.ToString(),
                MFAJudgmentCopyPath = r.Table.Columns.Contains("MFAJudgmentCopyPath") && r["MFAJudgmentCopyPath"] != DBNull.Value ? r["MFAJudgmentCopyPath"].ToString() : null,

                // 5. Outcome
                CorpMFAOutcome = r["CorpMFAOutcome"]?.ToString(),
                CorpMFAActionTaken = r.Table.Columns.Contains("CorpMFAActionTaken") && r["CorpMFAActionTaken"] != DBNull.Value ? r["CorpMFAActionTaken"].ToString() : null,
                CorpMFAActionTakenPath = r.Table.Columns.Contains("CorpMFAActionTakenPath") && r["CorpMFAActionTakenPath"] != DBNull.Value ? r["CorpMFAActionTakenPath"].ToString() : null,
                ClosureOutwardNo = r["ClosureOutwardNo"]?.ToString(),
                ClosureDate = r["ClosureDate"] != DBNull.Value ? (DateTime?)r["ClosureDate"] : null,

                // 6. Corp SC
                CorpSCNumber = r.Table.Columns.Contains("CorpSCNumber") ? r["CorpSCNumber"]?.ToString() : null,
                CorpSCYear = r.Table.Columns.Contains("CorpSCYear") && r["CorpSCYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["CorpSCYear"]) : null,
                CorpSCEntrustmentNo = r.Table.Columns.Contains("CorpSCEntrustmentNo") ? r["CorpSCEntrustmentNo"]?.ToString() : null,
                CorpSCEntrustmentDate = r.Table.Columns.Contains("CorpSCEntrustmentDate") && r["CorpSCEntrustmentDate"] != DBNull.Value ? (DateTime?)r["CorpSCEntrustmentDate"] : null,
                CorpSCAdvocate = r.Table.Columns.Contains("CorpSCAdvocate") ? r["CorpSCAdvocate"]?.ToString() : null,
                CorpSCStatus = r.Table.Columns.Contains("CorpSCStatus") ? r["CorpSCStatus"]?.ToString() : null,

                // 7. Claimant MFA
                ClaimantDivisionID = r.Table.Columns.Contains("ClaimantDivisionID") && r["ClaimantDivisionID"] != DBNull.Value ? (int?)Convert.ToInt32(r["ClaimantDivisionID"]) : null,
                ClaimantMVCNumber = r.Table.Columns.Contains("ClaimantMVCNumber") ? r["ClaimantMVCNumber"]?.ToString() : null,
                ClaimantMVCYear = r.Table.Columns.Contains("ClaimantMVCYear") && r["ClaimantMVCYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["ClaimantMVCYear"]) : null,
                ClaimantMVCCurrentStatus = r.Table.Columns.Contains("ClaimantMVCCurrentStatus") ? r["ClaimantMVCCurrentStatus"]?.ToString() : null,
                
                ClaimantMFANumber = r.Table.Columns.Contains("ClaimantMFANumber") ? r["ClaimantMFANumber"]?.ToString() : null,
                ClaimantMFAYear = r.Table.Columns.Contains("ClaimantMFAYear") && r["ClaimantMFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["ClaimantMFAYear"]) : null,
                ClaimantMFACNRNumber = r.Table.Columns.Contains("ClaimantMFACNRNumber") ? r["ClaimantMFACNRNumber"]?.ToString() : null,
                ClaimantMFANextHearingDate = r.Table.Columns.Contains("ClaimantMFANextHearingDate") && r["ClaimantMFANextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["ClaimantMFANextHearingDate"]) : null,
                ClaimantMFAStage = r.Table.Columns.Contains("ClaimantMFAStage") ? r["ClaimantMFAStage"]?.ToString() : null,
                ClaimantMFAEntrustmentNo = r.Table.Columns.Contains("ClaimantMFAEntrustmentNo") ? r["ClaimantMFAEntrustmentNo"]?.ToString() : null,
                ClaimantMFAEntrustmentDate = r.Table.Columns.Contains("ClaimantMFAEntrustmentDate") && r["ClaimantMFAEntrustmentDate"] != DBNull.Value ? (DateTime?)r["ClaimantMFAEntrustmentDate"] : null,
                ClaimantMFAAdvocate = r.Table.Columns.Contains("ClaimantMFAAdvocate") ? r["ClaimantMFAAdvocate"]?.ToString() : null,
                ClaimantMFAStatus = r.Table.Columns.Contains("ClaimantMFAStatus") ? r["ClaimantMFAStatus"]?.ToString() : null,
                ClaimantMFADecision = r.Table.Columns.Contains("ClaimantMFADecision") ? r["ClaimantMFADecision"]?.ToString() : null,
                ClaimantActionTaken = r.Table.Columns.Contains("ClaimantActionTaken") ? r["ClaimantActionTaken"]?.ToString() : null,
                ClaimantApprovalNo = r.Table.Columns.Contains("ClaimantApprovalNo") ? r["ClaimantApprovalNo"]?.ToString() : null,
                ClaimantApprovalDate = r.Table.Columns.Contains("ClaimantApprovalDate") && r["ClaimantApprovalDate"] != DBNull.Value ? (DateTime?)r["ClaimantApprovalDate"] : null,
                ClaimantMFARemarks = r.Table.Columns.Contains("ClaimantMFARemarks") ? r["ClaimantMFARemarks"]?.ToString() : null,

                // 8. Claimant SC
                IsClaimantSCAppeal = r.Table.Columns.Contains("IsClaimantSCAppeal") && r["IsClaimantSCAppeal"] != DBNull.Value && (bool)r["IsClaimantSCAppeal"],
                IsClaimantSCPending = r.Table.Columns.Contains("IsClaimantSCPending") && r["IsClaimantSCPending"] != DBNull.Value && (bool)r["IsClaimantSCPending"],
                ClaimantSCNumber = r.Table.Columns.Contains("ClaimantSCNumber") ? r["ClaimantSCNumber"]?.ToString() : null,
                ClaimantSCDiaryNumber = r.Table.Columns.Contains("ClaimantSCDiaryNumber") && r["ClaimantSCDiaryNumber"] != DBNull.Value ? r["ClaimantSCDiaryNumber"].ToString() : null,
                ClaimantSCYear = r.Table.Columns.Contains("ClaimantSCYear") && r["ClaimantSCYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["ClaimantSCYear"]) : null,
                ClaimantSLPYear = r.Table.Columns.Contains("ClaimantSLPYear") && r["ClaimantSLPYear"] != DBNull.Value ? (int?)Convert.ToInt32(r["ClaimantSLPYear"]) : null,
                ClaimantSCFiledBy = r.Table.Columns.Contains("ClaimantSCFiledBy") ? r["ClaimantSCFiledBy"]?.ToString() : null,
                ClaimantSCEntrustmentNo = r.Table.Columns.Contains("ClaimantSCEntrustmentNo") ? r["ClaimantSCEntrustmentNo"]?.ToString() : null,
                ClaimantSCEntrustmentDate = r.Table.Columns.Contains("ClaimantSCEntrustmentDate") && r["ClaimantSCEntrustmentDate"] != DBNull.Value ? (DateTime?)r["ClaimantSCEntrustmentDate"] : null,
                ClaimantSCAdvocate = r.Table.Columns.Contains("ClaimantSCAdvocate") ? r["ClaimantSCAdvocate"]?.ToString() : null,
                ClaimantSCStatus = r.Table.Columns.Contains("ClaimantSCStatus") ? r["ClaimantSCStatus"]?.ToString() : null,
                ClaimantSCOutcome = r.Table.Columns.Contains("ClaimantSCOutcome") && r["ClaimantSCOutcome"] != DBNull.Value ? r["ClaimantSCOutcome"].ToString() : null,
                ClaimantSCActionTaken = r.Table.Columns.Contains("ClaimantSCActionTaken") && r["ClaimantSCActionTaken"] != DBNull.Value ? r["ClaimantSCActionTaken"].ToString() : null,
                ClaimantSCClosureNo = r.Table.Columns.Contains("ClaimantSCClosureNo") && r["ClaimantSCClosureNo"] != DBNull.Value ? r["ClaimantSCClosureNo"].ToString() : null,
                ClaimantSCClosureDate = r.Table.Columns.Contains("ClaimantSCClosureDate") && r["ClaimantSCClosureDate"] != DBNull.Value ? (DateTime?)r["ClaimantSCClosureDate"] : null,
                ClaimantSCJudgmentPath = r.Table.Columns.Contains("ClaimantSCJudgmentPath") && r["ClaimantSCJudgmentPath"] != DBNull.Value ? r["ClaimantSCJudgmentPath"].ToString() : null,

                // 9. Closure
                FinalComplianceStatus = r.Table.Columns.Contains("FinalComplianceStatus") ? r["FinalComplianceStatus"]?.ToString() : null,
                AmountDeposited = r.Table.Columns.Contains("AmountDeposited") && r["AmountDeposited"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["AmountDeposited"]) : null,
                FinalComplianceDate = r.Table.Columns.Contains("FinalComplianceDate") && r["FinalComplianceDate"] != DBNull.Value ? (DateTime?)r["FinalComplianceDate"] : null,
                FinalRemarks = r.Table.Columns.Contains("FinalRemarks") ? r["FinalRemarks"]?.ToString() : null,

            };

            // Fetch Connected Cases
            string connQuery = "SELECT * FROM APPEAL_CONNECTED WHERE CaseID = @CaseID";
            var dtConn = _db.ExecuteQuery(connQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach(DataRow row in dtConn.Rows)
            {
                vm.ConnectedCases.Add(new ConnectedAppeal
                {
                    ConnectedID = (int)row["ConnectedID"],
                    ConnectedMVCNo = row["ConnectedMVCNo"]?.ToString(),
                    FiledBy = row["FiledBy"]?.ToString(),
                    MFA_Number = row["MFA_Number"]?.ToString(),
                    Status = row["Status"]?.ToString()
                });
            }

            return vm;

        }

        public void SaveAppeal(AppealViewModel model)
        {
            if (model == null) return;
            using (var connection = new SqlConnection(_db.GetConnectionString()))
            {
                connection.Open();
                using (var trans = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Serialize Connected Cases to XML
                        string? xmlConnected = null;
                        if (model.ConnectedCases != null && model.ConnectedCases.Count > 0)
                        {
                            var xmlDoc = new System.Xml.XmlDocument();
                            var root = xmlDoc.CreateElement("ConnectedCases");
                            xmlDoc.AppendChild(root);
                            foreach (var caseItem in model.ConnectedCases)
                            {
                                var node = xmlDoc.CreateElement("Case");
                                node.SetAttribute("FiledBy", caseItem.FiledBy ?? "");
                                node.SetAttribute("ConnectedMVCNo", caseItem.ConnectedMVCNo ?? "");
                                node.SetAttribute("MFA_Number", caseItem.MFA_Number ?? "");
                                node.SetAttribute("Status", caseItem.Status ?? "");
                                root.AppendChild(node);
                            }
                            xmlConnected = xmlDoc.OuterXml;
                        }

                        // 2. Direct Update/Insert into APPEAL_DETAILS table (avoids SP parameter order mismatches)
                        string query = @"
                        IF EXISTS (SELECT 1 FROM APPEAL_DETAILS WHERE CaseID = @CaseID)
                        BEGIN
                            UPDATE APPEAL_DETAILS SET 
                                FeasibilityReceived = @FeasibilityReceived,
                                FeasibilityReceiptDate = @FeasibilityReceiptDate,
                                InitialAction = @InitialAction,
                                ApprovalOutwardNo = @ApprovalOutwardNo,
                                ApprovalDate = @ApprovalDate,
                                InitialActionRemarks = @InitialActionRemarks,
                                InitialActionPath1 = ISNULL(@InitialActionPath1, InitialActionPath1),
                                InitialActionPath2 = ISNULL(@InitialActionPath2, InitialActionPath2),
                                Opinion_CLO = ISNULL(@Opinion_CLO, Opinion_CLO),
                                ApprovalCopyPath = ISNULL(@ApprovalCopyPath, ApprovalCopyPath),
                                -- Per-role action fields (ISNULL preserves other roles' data)
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
                                CorpMFANumber = ISNULL(@CorpMFANumber, CorpMFANumber),
                                CorpMFAYear = ISNULL(@CorpMFAYear, CorpMFAYear),
                                HighCourtBench = ISNULL(@HighCourtBench, HighCourtBench),
                                OtherHighCourtBench = ISNULL(@OtherHighCourtBench, OtherHighCourtBench),
                                IsPendingForFiling = @IsPendingForFiling,
                                CorpMFAEntrustmentNo = ISNULL(@CorpMFAEntrustmentNo, CorpMFAEntrustmentNo),
                                CorpMFAEntrustmentDate = ISNULL(@CorpMFAEntrustmentDate, CorpMFAEntrustmentDate),
                                -- Use ISNULL(NULLIF) to preserve existing CNR/stage/hearing if form submits null or empty
                                CorpMFACNRNumber = ISNULL(NULLIF(@CorpMFACNRNumber, ''), CorpMFACNRNumber),
                                CorpMFANextHearingDate = ISNULL(@CorpMFANextHearingDate, CorpMFANextHearingDate),
                                CorpMFAStage = ISNULL(NULLIF(@CorpMFAStage, ''), CorpMFAStage),
                                StayGranted = @StayGranted,
                                StayComplianceOutwardNo = ISNULL(@StayComplianceOutwardNo, StayComplianceOutwardNo),
                                StayComplianceDate = ISNULL(@StayComplianceDate, StayComplianceDate),
                                StayOrderPath1 = ISNULL(@StayOrderPath1, StayOrderPath1),
                                StayOrderPath2 = ISNULL(@StayOrderPath2, StayOrderPath2),
                                ComplianceLetterPath = ISNULL(@ComplianceLetterPath, ComplianceLetterPath),
                                CorpMFAStatus = ISNULL(@CorpMFAStatus, CorpMFAStatus),
                                RestorationFiled = @RestorationFiled,
                                RestorationDate = ISNULL(@RestorationDate, RestorationDate),
                                RestorationStatus = ISNULL(@RestorationStatus, RestorationStatus),
                                MFAJudgmentCopyPath = ISNULL(@MFAJudgmentCopyPath, MFAJudgmentCopyPath),
                                CorpMFAOutcome = ISNULL(@CorpMFAOutcome, CorpMFAOutcome),
                                CorpMFAActionTaken = ISNULL(@CorpMFAActionTaken, CorpMFAActionTaken),
                                CorpMFAActionTakenPath = ISNULL(@CorpMFAActionTakenPath, CorpMFAActionTakenPath),
                                ClosureOutwardNo = ISNULL(@ClosureOutwardNo, ClosureOutwardNo),
                                ClosureDate = ISNULL(@ClosureDate, ClosureDate),
                                -- Corp SC fields are not in Manage.cshtml form; preserve existing DB values
                                CorpSCNumber = ISNULL(@CorpSCNumber, CorpSCNumber),
                                CorpSCYear = ISNULL(@CorpSCYear, CorpSCYear),
                                CorpSCEntrustmentNo = ISNULL(@CorpSCEntrustmentNo, CorpSCEntrustmentNo),
                                CorpSCEntrustmentDate = ISNULL(@CorpSCEntrustmentDate, CorpSCEntrustmentDate),
                                CorpSCAdvocate = ISNULL(@CorpSCAdvocate, CorpSCAdvocate),
                                CorpSCStatus = ISNULL(@CorpSCStatus, CorpSCStatus),
                                ClaimantDivisionID = ISNULL(@ClaimantDivisionID, ClaimantDivisionID),
                                ClaimantMVCNumber = ISNULL(@ClaimantMVCNumber, ClaimantMVCNumber),
                                ClaimantMVCYear = ISNULL(@ClaimantMVCYear, ClaimantMVCYear),
                                ClaimantMVCCurrentStatus = ISNULL(@ClaimantMVCCurrentStatus, ClaimantMVCCurrentStatus),
                                ClaimantMFANumber = ISNULL(@ClaimantMFANumber, ClaimantMFANumber),
                                ClaimantMFAYear = ISNULL(@ClaimantMFAYear, ClaimantMFAYear),
                                ClaimantMFACNRNumber = COALESCE(NULLIF(LTRIM(RTRIM(@ClaimantMFACNRNumber)), ''), ClaimantMFACNRNumber),
                                ClaimantMFAEntrustmentNo = ISNULL(@ClaimantMFAEntrustmentNo, ClaimantMFAEntrustmentNo),
                                ClaimantMFAEntrustmentDate = ISNULL(@ClaimantMFAEntrustmentDate, ClaimantMFAEntrustmentDate),
                                ClaimantMFAAdvocate = ISNULL(@ClaimantMFAAdvocate, ClaimantMFAAdvocate),
                                ClaimantMFAStatus = ISNULL(@ClaimantMFAStatus, ClaimantMFAStatus),
                                ClaimantMFARemarks = ISNULL(@ClaimantMFARemarks, ClaimantMFARemarks),
                                ClaimantMFADecision = ISNULL(@ClaimantMFADecision, ClaimantMFADecision),
                                ClaimantActionTaken = ISNULL(@ClaimantActionTaken, ClaimantActionTaken),
                                ClaimantApprovalNo = ISNULL(@ClaimantApprovalNo, ClaimantApprovalNo),
                                ClaimantApprovalDate = ISNULL(@ClaimantApprovalDate, ClaimantApprovalDate),
                                ClaimantSCNumber = ISNULL(@ClaimantSCNumber, ClaimantSCNumber),
                                ClaimantSCDiaryNumber = ISNULL(@ClaimantSCDiaryNumber, ClaimantSCDiaryNumber),
                                ClaimantSCYear = ISNULL(@ClaimantSCYear, ClaimantSCYear),
                                ClaimantSLPYear = ISNULL(@ClaimantSLPYear, ClaimantSLPYear),
                                ClaimantSCFiledBy = ISNULL(@ClaimantSCFiledBy, ClaimantSCFiledBy),
                                ClaimantSCEntrustmentNo = ISNULL(@ClaimantSCEntrustmentNo, ClaimantSCEntrustmentNo),
                                ClaimantSCEntrustmentDate = ISNULL(@ClaimantSCEntrustmentDate, ClaimantSCEntrustmentDate),
                                ClaimantSCAdvocate = ISNULL(@ClaimantSCAdvocate, ClaimantSCAdvocate),
                                ClaimantSCStatus = ISNULL(@ClaimantSCStatus, ClaimantSCStatus),
                                IsClaimantSCAppeal = @IsClaimantSCAppeal,
                                IsClaimantSCPending = @IsClaimantSCPending,
                                ClaimantSCOutcome = ISNULL(@ClaimantSCOutcome, ClaimantSCOutcome),
                                ClaimantSCActionTaken = ISNULL(@ClaimantSCActionTaken, ClaimantSCActionTaken),
                                ClaimantSCClosureNo = ISNULL(@ClaimantSCClosureNo, ClaimantSCClosureNo),
                                ClaimantSCClosureDate = ISNULL(@ClaimantSCClosureDate, ClaimantSCClosureDate),
                                ClaimantSCJudgmentPath = ISNULL(@ClaimantSCJudgmentPath, ClaimantSCJudgmentPath),
                                -- Compliance fields only updated via SaveCompliance endpoint; preserve existing
                                FinalComplianceStatus = ISNULL(@FinalComplianceStatus, FinalComplianceStatus),
                                AmountDeposited = ISNULL(@AmountDeposited, AmountDeposited),
                                FinalComplianceDate = ISNULL(@FinalComplianceDate, FinalComplianceDate),
                                FinalRemarks = ISNULL(@FinalRemarks, FinalRemarks),
                                ModifiedAt = GETDATE()
                            WHERE CaseID = @CaseID;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO APPEAL_DETAILS (
                                CaseID, FeasibilityReceived, FeasibilityReceiptDate, InitialAction, ApprovalOutwardNo, ApprovalDate, InitialActionRemarks,
                                InitialActionPath1, InitialActionPath2, Opinion_CLO, ApprovalCopyPath,
                                ActionTaken_LO, ApprovalDate_LO, Opinion_LO,
                                ActionTaken_DyCLO, ApprovalDate_DyCLO, Opinion_DyCLO,
                                ActionTaken_CLO, ApprovalDate_CLO,
                                ActionTaken_MD, ApprovalDate_MD, Opinion_MD,
                                CorpMFANumber, CorpMFAYear, HighCourtBench, OtherHighCourtBench, IsPendingForFiling, CorpMFAEntrustmentNo, CorpMFAEntrustmentDate, CorpMFAAdvocate,
                                CorpMFACNRNumber, CorpMFANextHearingDate, CorpMFAStage,
                                StayGranted, StayComplianceOutwardNo, StayComplianceDate, StayOrderPath1, StayOrderPath2, ComplianceLetterPath,
                                CorpMFAStatus, RestorationFiled, RestorationDate, RestorationStatus, MFAJudgmentCopyPath,
                                CorpMFAOutcome, CorpMFAActionTaken, CorpMFAActionTakenPath, ClosureOutwardNo, ClosureDate,
                                CorpSCNumber, CorpSCYear, CorpSCEntrustmentNo, CorpSCEntrustmentDate, CorpSCAdvocate, CorpSCStatus,
                                ClaimantDivisionID, ClaimantMVCNumber, ClaimantMVCYear, ClaimantMVCCurrentStatus,
                                ClaimantMFANumber, ClaimantMFAYear, ClaimantMFACNRNumber, ClaimantMFAEntrustmentNo, ClaimantMFAEntrustmentDate,
                                ClaimantMFAAdvocate, ClaimantMFAStatus, ClaimantMFARemarks, ClaimantMFADecision, ClaimantActionTaken, ClaimantApprovalNo, ClaimantApprovalDate,
                                ClaimantSCNumber, ClaimantSCDiaryNumber, ClaimantSCYear, ClaimantSLPYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus,
                                IsClaimantSCAppeal, IsClaimantSCPending, ClaimantSCOutcome, ClaimantSCActionTaken, ClaimantSCClosureNo, ClaimantSCClosureDate, ClaimantSCJudgmentPath,
                                FinalComplianceStatus, AmountDeposited, FinalComplianceDate, FinalRemarks, CreatedAt
                            )
                            VALUES (
                                @CaseID, @FeasibilityReceived, @FeasibilityReceiptDate, @InitialAction, @ApprovalOutwardNo, @ApprovalDate, @InitialActionRemarks,
                                @InitialActionPath1, @InitialActionPath2, @Opinion_CLO, @ApprovalCopyPath,
                                @ActionTaken_LO, @ApprovalDate_LO, @Opinion_LO,
                                @ActionTaken_DyCLO, @ApprovalDate_DyCLO, @Opinion_DyCLO,
                                @ActionTaken_CLO, @ApprovalDate_CLO,
                                @ActionTaken_MD, @ApprovalDate_MD, @Opinion_MD,
                                @CorpMFANumber, @CorpMFAYear, @HighCourtBench, @OtherHighCourtBench, @IsPendingForFiling, @CorpMFAEntrustmentNo, @CorpMFAEntrustmentDate, @CorpMFAAdvocate,
                                @CorpMFACNRNumber, @CorpMFANextHearingDate, @CorpMFAStage,
                                @StayGranted, @StayComplianceOutwardNo, @StayComplianceDate, @StayOrderPath1, @StayOrderPath2, @ComplianceLetterPath,
                                @CorpMFAStatus, @RestorationFiled, @RestorationDate, @RestorationStatus, @MFAJudgmentCopyPath,
                                @CorpMFAOutcome, @CorpMFAActionTaken, @CorpMFAActionTakenPath, @ClosureOutwardNo, @ClosureDate,
                                @CorpSCNumber, @CorpSCYear, @CorpSCEntrustmentNo, @CorpSCEntrustmentDate, @CorpSCAdvocate, @CorpSCStatus,
                                @ClaimantDivisionID, @ClaimantMVCNumber, @ClaimantMVCYear, @ClaimantMVCCurrentStatus,
                                @ClaimantMFANumber, @ClaimantMFAYear, @ClaimantMFACNRNumber, @ClaimantMFAEntrustmentNo, @ClaimantMFAEntrustmentDate,
                                @ClaimantMFAAdvocate, @ClaimantMFAStatus, @ClaimantMFARemarks, @ClaimantMFADecision, @ClaimantActionTaken, @ClaimantApprovalNo, @ClaimantApprovalDate,
                                @ClaimantSCNumber, @ClaimantSCDiaryNumber, @ClaimantSCYear, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus,
                                @IsClaimantSCAppeal, @IsClaimantSCPending, @ClaimantSCOutcome, @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate, @ClaimantSCJudgmentPath,
                                @FinalComplianceStatus, @AmountDeposited, @FinalComplianceDate, @FinalRemarks, GETDATE()
                            );
                        END";

                        using (var cmd = new SqlCommand(query, connection, trans))
                        {
                            cmd.CommandTimeout = 120;

                            void p(string n, SqlDbType t, object? v, int prec = 0, int sc = 0)
                            {
                                var param = new SqlParameter(n, t) { Value = v ?? DBNull.Value };
                                if (prec > 0) param.Precision = (byte)prec;
                                if (sc > 0) param.Scale = (byte)sc;
                                cmd.Parameters.Add(param);
                            }

                            p("@CaseID", SqlDbType.Int, model.CaseID);
                            p("@FeasibilityReceived", SqlDbType.Bit, model.FeasibilityReceived);
                            p("@FeasibilityReceiptDate", SqlDbType.Date, model.FeasibilityReceiptDate);
                            p("@InitialAction", SqlDbType.NVarChar, model.InitialAction);
                            p("@ApprovalOutwardNo", SqlDbType.NVarChar, model.ApprovalOutwardNo);
                            p("@ApprovalDate", SqlDbType.Date, model.ApprovalDate);
                            p("@InitialActionRemarks", SqlDbType.NVarChar, model.InitialActionRemarks);
                            p("@InitialActionPath1", SqlDbType.NVarChar, model.InitialActionPath1);
                            p("@InitialActionPath2", SqlDbType.NVarChar, model.InitialActionPath2);
                            p("@Opinion_CLO", SqlDbType.NVarChar, model.Opinion_CLO);
                            p("@ApprovalCopyPath", SqlDbType.NVarChar, model.ApprovalCopyPath);

                            // Per-role action parameters
                            p("@ActionTaken_LO", SqlDbType.NVarChar, model.ActionTaken_LO);
                            p("@ApprovalDate_LO", SqlDbType.Date, model.ApprovalDate_LO);
                            p("@Opinion_LO", SqlDbType.NVarChar, model.Opinion_LO);
                            p("@ActionTaken_DyCLO", SqlDbType.NVarChar, model.ActionTaken_DyCLO);
                            p("@ApprovalDate_DyCLO", SqlDbType.Date, model.ApprovalDate_DyCLO);
                            p("@Opinion_DyCLO", SqlDbType.NVarChar, model.Opinion_DyCLO);
                            p("@ActionTaken_CLO", SqlDbType.NVarChar, model.ActionTaken_CLO);
                            p("@ApprovalDate_CLO", SqlDbType.Date, model.ApprovalDate_CLO);
                            p("@ActionTaken_MD", SqlDbType.NVarChar, model.ActionTaken_MD);
                            p("@ApprovalDate_MD", SqlDbType.Date, model.ApprovalDate_MD);
                            p("@Opinion_MD", SqlDbType.NVarChar, model.Opinion_MD);

                            p("@CorpMFANumber", SqlDbType.NVarChar, model.CorpMFANumber);
                            p("@CorpMFAYear", SqlDbType.Int, model.CorpMFAYear);
                            p("@HighCourtBench", SqlDbType.NVarChar, model.HighCourtBench);
                            p("@OtherHighCourtBench", SqlDbType.NVarChar, model.OtherHighCourtBench);
                            p("@IsPendingForFiling", SqlDbType.Bit, model.IsPendingForFiling);
                            p("@CorpMFAEntrustmentNo", SqlDbType.NVarChar, model.CorpMFAEntrustmentNo);
                            p("@CorpMFAEntrustmentDate", SqlDbType.Date, model.CorpMFAEntrustmentDate);
                            p("@CorpMFAAdvocate", SqlDbType.NVarChar, model.CorpMFAAdvocate);
                            p("@CorpMFACNRNumber", SqlDbType.NVarChar, string.IsNullOrWhiteSpace(model.CorpMFACNRNumber) ? (object)DBNull.Value : model.CorpMFACNRNumber.Trim().ToUpperInvariant());
                            p("@CorpMFANextHearingDate", SqlDbType.Date, model.CorpMFANextHearingDate.HasValue ? (object)model.CorpMFANextHearingDate.Value : DBNull.Value);
                            p("@CorpMFAStage", SqlDbType.NVarChar, string.IsNullOrWhiteSpace(model.CorpMFAStage) ? (object)DBNull.Value : model.CorpMFAStage.Trim());

                            p("@StayGranted", SqlDbType.Bit, model.StayGranted);
                            p("@StayComplianceOutwardNo", SqlDbType.NVarChar, model.StayComplianceOutwardNo);
                            p("@StayComplianceDate", SqlDbType.Date, model.StayComplianceDate);
                            p("@StayOrderPath1", SqlDbType.NVarChar, model.StayOrderPath1);
                            p("@StayOrderPath2", SqlDbType.NVarChar, model.StayOrderPath2);
                            p("@ComplianceLetterPath", SqlDbType.NVarChar, model.ComplianceLetterPath);

                            p("@CorpMFAStatus", SqlDbType.NVarChar, model.CorpMFAStatus);
                            p("@RestorationFiled", SqlDbType.Bit, model.RestorationFiled);
                            p("@RestorationDate", SqlDbType.Date, model.RestorationDate);
                            p("@RestorationStatus", SqlDbType.NVarChar, model.RestorationStatus);
                            p("@MFAJudgmentCopyPath", SqlDbType.NVarChar, model.MFAJudgmentCopyPath);

                            p("@CorpMFAOutcome", SqlDbType.NVarChar, model.CorpMFAOutcome);
                            p("@CorpMFAActionTaken", SqlDbType.NVarChar, model.CorpMFAActionTaken);
                            p("@CorpMFAActionTakenPath", SqlDbType.NVarChar, model.CorpMFAActionTakenPath);
                            p("@ClosureOutwardNo", SqlDbType.NVarChar, model.ClosureOutwardNo);
                            p("@ClosureDate", SqlDbType.Date, model.ClosureDate);

                            p("@CorpSCNumber", SqlDbType.NVarChar, model.CorpSCNumber);
                            p("@CorpSCYear", SqlDbType.Int, model.CorpSCYear);
                            p("@CorpSCEntrustmentNo", SqlDbType.NVarChar, model.CorpSCEntrustmentNo);
                            p("@CorpSCEntrustmentDate", SqlDbType.Date, model.CorpSCEntrustmentDate);
                            p("@CorpSCAdvocate", SqlDbType.NVarChar, model.CorpSCAdvocate);
                            p("@CorpSCStatus", SqlDbType.NVarChar, model.CorpSCStatus);

                            p("@ClaimantDivisionID", SqlDbType.Int, model.ClaimantDivisionID);
                            p("@ClaimantMVCNumber", SqlDbType.NVarChar, model.ClaimantMVCNumber);
                            p("@ClaimantMVCYear", SqlDbType.Int, model.ClaimantMVCYear);
                            p("@ClaimantMVCCurrentStatus", SqlDbType.NVarChar, model.ClaimantMVCCurrentStatus);

                            p("@ClaimantMFANumber", SqlDbType.NVarChar, model.ClaimantMFANumber);
                            p("@ClaimantMFAYear", SqlDbType.Int, model.ClaimantMFAYear);
                            p("@ClaimantMFACNRNumber", SqlDbType.NVarChar, string.IsNullOrWhiteSpace(model.ClaimantMFACNRNumber) ? (object?)DBNull.Value : model.ClaimantMFACNRNumber.Trim().ToUpperInvariant());
                            p("@ClaimantMFAEntrustmentNo", SqlDbType.NVarChar, model.ClaimantMFAEntrustmentNo);
                            p("@ClaimantMFAEntrustmentDate", SqlDbType.Date, model.ClaimantMFAEntrustmentDate);
                            p("@ClaimantMFAAdvocate", SqlDbType.NVarChar, model.ClaimantMFAAdvocate);
                            p("@ClaimantMFAStatus", SqlDbType.NVarChar, model.ClaimantMFAStatus);
                            p("@ClaimantMFARemarks", SqlDbType.NVarChar, model.ClaimantMFARemarks);
                            p("@ClaimantMFADecision", SqlDbType.NVarChar, model.ClaimantMFADecision);
                            p("@ClaimantActionTaken", SqlDbType.NVarChar, model.ClaimantActionTaken);
                            p("@ClaimantApprovalNo", SqlDbType.NVarChar, model.ClaimantApprovalNo);
                            p("@ClaimantApprovalDate", SqlDbType.Date, model.ClaimantApprovalDate);

                            p("@ClaimantSCNumber", SqlDbType.NVarChar, model.ClaimantSCNumber);
                            p("@ClaimantSCDiaryNumber", SqlDbType.NVarChar, model.ClaimantSCDiaryNumber);
                            p("@ClaimantSCYear", SqlDbType.Int, model.ClaimantSCYear);
                            p("@ClaimantSLPYear", SqlDbType.Int, model.ClaimantSLPYear);
                            p("@ClaimantSCFiledBy", SqlDbType.NVarChar, model.ClaimantSCFiledBy);
                            p("@ClaimantSCEntrustmentNo", SqlDbType.NVarChar, model.ClaimantSCEntrustmentNo);
                            p("@ClaimantSCEntrustmentDate", SqlDbType.Date, model.ClaimantSCEntrustmentDate);
                            p("@ClaimantSCAdvocate", SqlDbType.NVarChar, model.ClaimantSCAdvocate);
                            p("@ClaimantSCStatus", SqlDbType.NVarChar, model.ClaimantSCStatus);
                            p("@IsClaimantSCAppeal", SqlDbType.Bit, model.IsClaimantSCAppeal);
                            p("@IsClaimantSCPending", SqlDbType.Bit, model.IsClaimantSCPending);
                            p("@ClaimantSCOutcome", SqlDbType.NVarChar, model.ClaimantSCOutcome);
                            p("@ClaimantSCActionTaken", SqlDbType.NVarChar, model.ClaimantSCActionTaken);
                            p("@ClaimantSCClosureNo", SqlDbType.NVarChar, model.ClaimantSCClosureNo);
                            p("@ClaimantSCClosureDate", SqlDbType.Date, model.ClaimantSCClosureDate);
                            p("@ClaimantSCJudgmentPath", SqlDbType.NVarChar, model.ClaimantSCJudgmentPath);

                            p("@FinalComplianceStatus", SqlDbType.NVarChar, model.FinalComplianceStatus);
                            p("@AmountDeposited", SqlDbType.Decimal, model.AmountDeposited, 18, 2);
                            p("@FinalComplianceDate", SqlDbType.Date, model.FinalComplianceDate);
                            p("@FinalRemarks", SqlDbType.NVarChar, model.FinalRemarks);

                            cmd.ExecuteNonQuery();
                        }

                        // 3. Save Connected Cases
                        string deleteConn = "DELETE FROM APPEAL_CONNECTED WHERE CaseID = @CaseID";
                        using (var delCmd = new SqlCommand(deleteConn, connection, trans))
                        {
                            delCmd.Parameters.Add(new SqlParameter("@CaseID", SqlDbType.Int) { Value = model.CaseID });
                            delCmd.ExecuteNonQuery();
                        }

                        if (!string.IsNullOrEmpty(xmlConnected))
                        {
                            string insertConn = @"
                            INSERT INTO APPEAL_CONNECTED (CaseID, FiledBy, ConnectedMVCNo, MFA_Number, Status)
                            SELECT @CaseID, 
                                   T.c.value('@FiledBy', 'NVARCHAR(50)'),
                                   T.c.value('@ConnectedMVCNo', 'NVARCHAR(50)'),
                                   T.c.value('@MFA_Number', 'NVARCHAR(50)'),
                                   T.c.value('@Status', 'NVARCHAR(100)')
                            FROM @ConnectedCasesXml.nodes('/ConnectedCases/Case') AS T(c);";

                            using (var insCmd = new SqlCommand(insertConn, connection, trans))
                            {
                                insCmd.Parameters.Add(new SqlParameter("@CaseID", SqlDbType.Int) { Value = model.CaseID });
                                insCmd.Parameters.Add(new SqlParameter("@ConnectedCasesXml", SqlDbType.Xml) { Value = xmlConnected });
                                insCmd.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        throw new Exception("Error saving appeal details: " + ex.Message, ex);
                    }
                }
            }
        }

        public IEnumerable<AppealViewModel> GetAllClaimantAppeals(int divisionId = 0)
        {
            var list = new List<AppealViewModel>();
            string query = @"
                SELECT ad.*, c.MVCNo as MVCNo_Main, c.DivisionID as DivisionID_Main, dm.DivisionNameEnglish, mm.MACTName
                FROM APPEAL_DETAILS ad
                JOIN MVC_CASES c ON ad.CaseID = c.CaseID
                LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                LEFT JOIN MACT_MASTER mm ON c.MACTID = mm.MACTID
                WHERE (ad.ClaimantMFANumber IS NOT NULL AND ad.ClaimantMFANumber <> '') 
                  AND (@DivisionID = 0 OR c.DivisionID = @DivisionID)
                ORDER BY ad.AppealID DESC";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@DivisionID", divisionId) });
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new AppealViewModel
                {
                    AppealID = (int)r["AppealID"],
                    CaseID = (int)r["CaseID"],
                    MVCNo = r["MVCNo_Main"]?.ToString() ?? "",
                    DivisionName = r["DivisionNameEnglish"]?.ToString() ?? "",
                    MACTName = r["MACTName"]?.ToString() ?? "",
                    ClaimantMFANumber = r["ClaimantMFANumber"]?.ToString(),
                    ClaimantMFAYear = r["ClaimantMFAYear"] != DBNull.Value ? (int?)r["ClaimantMFAYear"] : null,
                    ClaimantMFAStatus = r["ClaimantMFAStatus"]?.ToString()
                });
            }
            return list;
        }

        public AppealViewModel? GetAppealByMFADetails(string mfaNo, int mfaYear)
        {
            string query = @"
                SELECT ad.CaseID 
                FROM APPEAL_DETAILS ad
                WHERE (ad.ClaimantMFANumber = @MFANo AND ad.ClaimantMFAYear = @MFAYear)
                   OR (ad.CorpMFANumber = @MFANo AND ad.CorpMFAYear = @MFAYear)";
            
            var parameters = new[] {
                new SqlParameter("@MFANo", mfaNo),
                new SqlParameter("@MFAYear", mfaYear)
            };

            var dt = _db.ExecuteQuery(query, parameters);
            if (dt.Rows.Count == 0) return null;

            int caseId = Convert.ToInt32(dt.Rows[0]["CaseID"]);
            return GetAppealByCaseId(caseId);
        }
    }
}
