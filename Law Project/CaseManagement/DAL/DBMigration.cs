using Microsoft.Data.SqlClient;

namespace MVCCaseManagement.DAL
{
    /// <summary>
    /// ARCHITECTURAL NOTICE:
    /// DBMigration represents the frozen V1.0.0 Baseline Schema Hardening.
    /// It is invoked solely by DatabaseMigrationRunner.cs as the initial baseline step (MigrationId: "V1_0_0_Baseline_Schema_Hardening").
    /// Once the baseline is recorded in SCHEMA_MIGRATIONS, this class is intentionally bypassed.
    /// 
    /// DO NOT add new schema migrations or index changes here.
    /// ALL NEW MIGRATIONS MUST be added as versioned migrations (V1_X_X) in DatabaseMigrationRunner.cs
    /// to benefit from distributed application locking (sp_getapplock), atomic transactions, and execution audit logging.
    /// </summary>
    public static class DBMigration
    {
        public static void EnsureAll(DBHelper db)
        {
            EnsureLabourColumns(db);
            EnsureMvcColumns(db);
            EnsureMvcAdverseColumns(db);
            EnsureMvcEpColumns(db);
            EnsureGratuitySchema(db);
            EnsureLabourEPTable(db);
            EnsureOtpTable(db);
            EnsureArisingApplicationsTable(db);
            EnsureServiceMattersTable(db);
            EnsureCustomCompensationColumns(db);
            EnsureAppealColumns(db);
            EnsureMvcOppositeVehiclesTable(db);
            EnsureOtherCasesTable(db);
            EnsureOtherCaseRespondentsTable(db);
            EnsureOtherCaseDocsTable(db);
            EnsureOtherCaseEvidenceTables(db);
            EnsureMvcRegistrationTables(db);
            EnsureRemindBackRegistrationTables(db);
            EnsureNotificationsTable(db);
            EnsureECourtsSchema(db);
            EnsureSirsiMactCourtUpdate(db);
            EnsureHighCourtCourtHallColumns(db);
            EnsureMactEstablishmentMapping(db);
            EnsureRoleActionColumns(db);
        }

        /// <summary>
        /// Adds per-role action columns (LO, Dy CLO, CLO, MD) to APPEAL_DETAILS and LABOUR_CASES tables.
        /// </summary>
        public static void EnsureRoleActionColumns(DBHelper db)
        {
            try
            {
                // --- APPEAL_DETAILS: per-role action columns ---
                var appealColumns = new[]
                {
                    "ActionTaken_LO NVARCHAR(100) NULL",
                    "ApprovalDate_LO DATE NULL",
                    "Opinion_LO NVARCHAR(MAX) NULL",
                    "ActionTaken_DyCLO NVARCHAR(100) NULL",
                    "ApprovalDate_DyCLO DATE NULL",
                    "Opinion_DyCLO NVARCHAR(MAX) NULL",
                    "ActionTaken_CLO NVARCHAR(100) NULL",
                    "ApprovalDate_CLO DATE NULL",
                    "ActionTaken_MD NVARCHAR(100) NULL",
                    "ApprovalDate_MD DATE NULL",
                    "Opinion_MD NVARCHAR(MAX) NULL"
                };

                foreach (var colDef in appealColumns)
                {
                    var colName = colDef.Split(' ')[0];
                    try
                    {
                        string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = '{colName}'";
                        if ((int)(db.ExecuteScalar(check) ?? 0) == 0)
                        {
                            db.ExecuteNonQuery($"ALTER TABLE APPEAL_DETAILS ADD {colDef}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Migration Error for APPEAL_DETAILS.{colName}: {ex.Message}");
                    }
                }

                // --- LABOUR_CASES: per-role action columns ---
                var labourColumns = new[]
                {
                    "ActionTaken_LO NVARCHAR(100) NULL",
                    "ApprovalDate_LO DATE NULL",
                    "ActionTaken_DyCLO NVARCHAR(100) NULL",
                    "ApprovalDate_DyCLO DATE NULL",
                    "Opinion_DyCLO NVARCHAR(MAX) NULL",
                    "ActionTaken_CLO NVARCHAR(100) NULL",
                    "ApprovalDate_CLO DATE NULL",
                    "ActionTaken_MD NVARCHAR(100) NULL",
                    "ApprovalDate_MD DATE NULL",
                    "Opinion_MD NVARCHAR(MAX) NULL"
                };

                foreach (var colDef in labourColumns)
                {
                    var colName = colDef.Split(' ')[0];
                    try
                    {
                        string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = '{colName}'";
                        if ((int)(db.ExecuteScalar(check) ?? 0) == 0)
                        {
                            db.ExecuteNonQuery($"ALTER TABLE LABOUR_CASES ADD {colDef}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Migration Error for LABOUR_CASES.{colName}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for Role Action Columns: {ex.Message}");
            }
        }

        public static void EnsureMvcEpColumns(DBHelper db)
        {
            try
            {
                var columns = new[]
                {
                    "EP_EntrustmentNo NVARCHAR(100) NULL",
                    "EP_EntrustmentDate DATE NULL",
                    "AdvocateID INT NULL",
                    "AdvocateName NVARCHAR(200) NULL",
                    "CaseOutcome NVARCHAR(50) NULL",
                    "DisposalDate DATE NULL",
                    "ClosureDate DATE NULL",
                    "DisposalRemarks NVARCHAR(MAX) NULL",
                    "CNRNumber NVARCHAR(16) NULL",
                    "EstCode NVARCHAR(50) NULL",
                    "CaseTypeCode NVARCHAR(50) NULL",
                    "RealizationDate DATE NULL",
                    "CalculationMethod NVARCHAR(50) NULL"
                };

                foreach (var colDef in columns)
                {
                    var colName = colDef.Split(' ')[0];
                    string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_EP_DETAILS' AND COLUMN_NAME = '{colName}'";
                    try
                    {
                        int count = (int)(db.ExecuteScalar(check) ?? 0);
                        if (count == 0)
                        {
                            string alter = $"ALTER TABLE MVC_EP_DETAILS ADD {colDef}";
                            db.ExecuteNonQuery(alter);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Migration Error for MVC_EP_DETAILS.{colName}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for MVC_EP_DETAILS schema: {ex.Message}");
            }
        }

        public static void EnsureHighCourtCourtHallColumns(DBHelper db)
        {
            try
            {
                string checkCol = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'CourtHall'";
                if ((int)(db.ExecuteScalar(checkCol) ?? 0) == 0)
                {
                    db.ExecuteNonQuery("ALTER TABLE APPEAL_DETAILS ADD CourtHall NVARCHAR(100) NULL");
                }
                else
                {
                    db.ExecuteNonQuery("UPDATE APPEAL_DETAILS SET CourtHall = NULL WHERE CourtHall = 'Court Hall 1'");
                }
            }
            catch { }
        }

        public static void EnsureECourtsSchema(DBHelper db)
        {
            try
            {
                // Safely add eCourts columns to existing tables if missing
                string[] tablesToEnhance = new[] { "MVC_CASES", "LABOUR_CASES", "LABOUR_SERVICE_MATTERS", "APPEAL_DETAILS", "MVC_REMIND_BACK_CASES" };
                foreach (var table in tablesToEnhance)
                {
                    string checkColCNR = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CNRNumber'";
                    if ((int)(db.ExecuteScalar(checkColCNR) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {table} ADD CNRNumber NVARCHAR(16) NULL");
                    }

                    string checkColEst = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'EstCode'";
                    if ((int)(db.ExecuteScalar(checkColEst) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {table} ADD EstCode NVARCHAR(50) NULL");
                    }

                    string checkColType = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CaseTypeCode'";
                    if ((int)(db.ExecuteScalar(checkColType) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {table} ADD CaseTypeCode NVARCHAR(50) NULL");
                    }

                    string checkColStatus = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CaseStatus'";
                    if ((int)(db.ExecuteScalar(checkColStatus) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {table} ADD CaseStatus NVARCHAR(100) NULL");
                    }

                    string checkColHall = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CourtHall'";
                    if ((int)(db.ExecuteScalar(checkColHall) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {table} ADD CourtHall NVARCHAR(150) NULL");
                    }
                }

                // Create TRACKED_CASES table if not exists
                string checkTracked = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TRACKED_CASES'";
                if ((int)(db.ExecuteScalar(checkTracked) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE TRACKED_CASES (
                            TrackedCaseID INT PRIMARY KEY IDENTITY(1,1),
                            CNRNumber NVARCHAR(16) UNIQUE NULL,
                            EstCode NVARCHAR(50) NOT NULL,
                            CaseTypeCode NVARCHAR(50) NOT NULL,
                            RegNo NVARCHAR(50) NOT NULL,
                            RegYear INT NOT NULL,
                            PetitionerName NVARCHAR(255) NULL,
                            RespondentName NVARCHAR(255) NULL,
                            CurrentStage NVARCHAR(100) NULL,
                            NextHearingDate DATE NULL,
                            CourtNo NVARCHAR(50) NULL,
                            JudgeName NVARCHAR(200) NULL,
                            IsHighCourt BIT DEFAULT 0,
                            LastSyncedDate DATETIME DEFAULT GETDATE(),
                            CreatedDate DATETIME DEFAULT GETDATE()
                        );
                        CREATE INDEX IX_TRACKED_CASES_CNR ON TRACKED_CASES(CNRNumber);
                        CREATE INDEX IX_TRACKED_CASES_SEARCH ON TRACKED_CASES(EstCode, CaseTypeCode, RegNo, RegYear);
                    ");
                }

                // Create DAILY_CAUSELISTS table if not exists
                string checkCauselist = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DAILY_CAUSELISTS'";
                if ((int)(db.ExecuteScalar(checkCauselist) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE DAILY_CAUSELISTS (
                            CauselistID INT PRIMARY KEY IDENTITY(1,1),
                            EstCode NVARCHAR(50) NOT NULL,
                            CourtNo NVARCHAR(50) NOT NULL,
                            CauselistDate DATE NOT NULL,
                            CauselistType NVARCHAR(20) DEFAULT 'civil',
                            TotalCases INT DEFAULT 0,
                            CorporationCasesCount INT DEFAULT 0,
                            FetchedDate DATETIME DEFAULT GETDATE(),
                            CONSTRAINT UQ_CAUSELIST UNIQUE(EstCode, CourtNo, CauselistDate, CauselistType)
                        );
                    ");
                }

                // Create CAUSELIST_ITEMS table if not exists
                string checkItems = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CAUSELIST_ITEMS'";
                if ((int)(db.ExecuteScalar(checkItems) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE CAUSELIST_ITEMS (
                            ItemID INT PRIMARY KEY IDENTITY(1,1),
                            CauselistID INT FOREIGN KEY REFERENCES DAILY_CAUSELISTS(CauselistID) ON DELETE CASCADE,
                            SrNo INT NOT NULL,
                            CNRNumber NVARCHAR(16) NULL,
                            CaseNumber NVARCHAR(100) NULL,
                            PartyDetails NVARCHAR(MAX) NULL,
                            AdvocateDetails NVARCHAR(MAX) NULL,
                            Stage NVARCHAR(100) NULL,
                            IsCorporationCase BIT DEFAULT 0,
                            TrackedCaseID INT NULL FOREIGN KEY REFERENCES TRACKED_CASES(TrackedCaseID)
                        );
                    ");
                }

                // Create CASE_ORDERS table if not exists
                string checkOrders = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CASE_ORDERS'";
                if ((int)(db.ExecuteScalar(checkOrders) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE CASE_ORDERS (
                            OrderID INT PRIMARY KEY IDENTITY(1,1),
                            CNRNumber NVARCHAR(16) NOT NULL,
                            OrderDate DATE NOT NULL,
                            OrderType NVARCHAR(100) NULL,
                            OrderPdfUrl NVARCHAR(500) NULL,
                            LocalPdfPath NVARCHAR(500) NULL,
                            FetchedDate DATETIME DEFAULT GETDATE()
                        );
                    ");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for eCourts schema: {ex.Message}");
            }
        }

        public static void EnsureNotificationsTable(DBHelper db)
        {
            try
            {
                string check = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SYSTEM_NOTIFICATIONS'";
                if ((int)(db.ExecuteScalar(check) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE SYSTEM_NOTIFICATIONS (
                            NotificationID INT PRIMARY KEY IDENTITY(1,1),
                            UserID INT NULL,
                            DivisionID INT NULL,
                            Title NVARCHAR(200) NOT NULL,
                            Message NVARCHAR(MAX) NOT NULL,
                            RelatedCaseType NVARCHAR(100) NULL,
                            RelatedCaseID INT NULL,
                            LinkUrl NVARCHAR(500) NULL,
                            IsRead BIT NOT NULL DEFAULT 0,
                            CreatedDate DATETIME DEFAULT GETDATE()
                        )");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for SYSTEM_NOTIFICATIONS: {ex.Message}");
            }
        }

        public static void EnsureLabourColumns(DBHelper db)
        {
            var columns = new[]
            {
                "CO_WA_CaseStatus_Option NVARCHAR(100)",
                "CO_WA_CaseNumber NVARCHAR(100)",
                "CO_WA_Year INT",
                "CO_WA_HighCourtBench NVARCHAR(100)",
                "CO_WA_EntrustmentNo NVARCHAR(100)",
                "CO_WA_EntrustmentDate DATE",
                "CO_WA_AdvocateName NVARCHAR(255)",
                "CO_WA_StayGranted BIT",
                "CO_WA_StayApprovalNo NVARCHAR(100)",
                "CO_WA_StayNature NVARCHAR(255)",
                "CO_WA_StayDate DATE",
                "CO_WA_StayOrderPath NVARCHAR(MAX)",
                "CO_WA_StayRemark NVARCHAR(MAX)",
                "CO_WA_Status NVARCHAR(100)",
                "CO_WA_Outcome NVARCHAR(100)",
                "CO_WA_OutcomeRemark NVARCHAR(MAX)",
                "CO_WA_OutcomeOutwardNo NVARCHAR(100)",
                "CO_WA_OutcomeOutwardDate DATE",
                "CO_WA_ActionTaken NVARCHAR(100)",
                
                // Claimant Appeal
                "CO_Claimant_CaseYear INT",

                // Claimant SC Appeal
                "IsClaimantSCPending BIT",
                "ClaimantSCDiaryNumber NVARCHAR(50)",
                "ClaimantSCYear INT",
                "ClaimantSCNumber NVARCHAR(50)",
                "ClaimantSLPYear INT",
                "ClaimantSCFiledBy NVARCHAR(50)",
                "ClaimantSCEntrustmentNo NVARCHAR(50)",
                "ClaimantSCEntrustmentDate DATE",
                "ClaimantSCAdvocate NVARCHAR(200)",
                "ClaimantSCStatus NVARCHAR(50)",
                "ClaimantSCOutcome NVARCHAR(100)",
                "ClaimantSCActionTaken NVARCHAR(100)",
                "ClaimantSCClosureNo NVARCHAR(50)",
                "ClaimantSCClosureDate DATE",

                // Added from LabourRepository
                "LokAdalatDocumentPath NVARCHAR(500)",
                "Opinion_CLO NVARCHAR(MAX)",
                "IsFiledWithinLimitation BIT",
                "LimitationRemark NVARCHAR(MAX)",
                "IsDelayCondoned BIT",
                "DelayCondonationRemark NVARCHAR(MAX)",
                "CO_StayComplianceRemark NVARCHAR(MAX)",
                "CO_StayComplianceFilePath NVARCHAR(MAX)",
                "CO_StayComplianceDate DATETIME",
                "CO_ClosedDocumentPath NVARCHAR(MAX)",
                
                // Service Matter Stay Compliance
                "CO_Service_StayCompliance BIT",
                "CO_Service_ApprovalOutwardNo NVARCHAR(100)",
                "CO_Service_ApprovalDate DATE",
                "CO_Service_ApprovalCopyPath NVARCHAR(MAX)",
                "CO_Service_EntrustmentNo NVARCHAR(100)",
                "CO_Service_EntrustmentDate DATE",
                
                // Missing core columns
                "PFNumber NVARCHAR(50)",
                "IsWorkman BIT DEFAULT 0",
                "IsWorkmanRemark NVARCHAR(MAX)",
                "SerialApp_CaseNumber NVARCHAR(100)",
                "SerialApp_CurrentStage NVARCHAR(200)",
                "Against_PunishmentCopyPath NVARCHAR(MAX)",
                "CC_CaseDisposedDate DATE",
                "CC_PublicationDate DATE",
                "CC_AppliedDate DATE",
                "CC_IssuedDate DATE",
                "CC_DeliveredDate DATE",
                "CC_ReceivedDate DATE",
                "CC_Remarks NVARCHAR(MAX)",
                "CO_Reinstatement_ApprovalNo NVARCHAR(100)",
                "CO_Reinstatement_ApprovalCopyPath NVARCHAR(MAX)",
                "ClaimPetitionPath NVARCHAR(MAX)",
                "ClaimFiledOn NVARCHAR(200)",
                "DelayInFiling NVARCHAR(200)",
                "ClaimDetails NVARCHAR(MAX)",
                "LRRelationship NVARCHAR(100)",
                "IsViewedByCO BIT DEFAULT 0",
                "DE_EO_BasedOnDocsRemark NVARCHAR(MAX)",
                "DE_Reporter_BasedOnDocsRemark NVARCHAR(MAX)",
                "DE_Other_BasedOnDocsRemark NVARCHAR(MAX)",
                "ChargesStatus NVARCHAR(50)",
                "TerminalBenefitsPaid NVARCHAR(MAX)",
                "SerialApplicationDetails NVARCHAR(MAX)",
                "IsRepeatDismissal BIT DEFAULT 0",
                "RepeatDismissalRemark NVARCHAR(MAX)",
                "HasAppealDetails BIT DEFAULT 0",
                "AppealDetailsRemark NVARCHAR(MAX)",
                "LegalRepresentativeName NVARCHAR(200)",
                "JudgmentCopyPath2 NVARCHAR(500)",
                "ServiceID INT NULL",
                "CO_Service_Division NVARCHAR(200)",
                "CO_Service_WPNumber NVARCHAR(100)",
                "CO_Service_WPYear INT",
                "CO_Service_PetitionerName NVARCHAR(200)",
                "CO_Service_CaseNature NVARCHAR(100)",
                "CO_Service_Prayer NVARCHAR(MAX)",
                "CO_Service_PetitionCopyPath NVARCHAR(MAX)",
                "CO_Service_IsEmployee BIT DEFAULT 0",
                "CO_Service_StayGranted BIT",
                "CO_Service_StayVacateFiled BIT",
                "CO_Service_Status NVARCHAR(50)",
                "CO_Service_DisposalDate DATE",
                "CO_Service_ActionTaken NVARCHAR(100)",
                "CO_Service_ApprovalSentDetails NVARCHAR(MAX)",
                "CO_Service_OutwardNo NVARCHAR(100)",
                "CO_Service_OutwardDate DATE",
                "CO_Service_AppealFiledBefore NVARCHAR(100)",
                "CO_Service_AppealType NVARCHAR(50)",
                "CO_Service_AppealEntrustmentDate DATE",
                "CO_Service_AppealAdvocate NVARCHAR(200)",
                "CO_Service_AppealStatus NVARCHAR(50)",
                "FavorOutwardDate DATE",
                "CO_Reinstatement_StayGranted BIT",
                "CO_Reinstatement_StayApprovalNo NVARCHAR(100)",
                "CO_Reinstatement_StayNature NVARCHAR(100)",
                "CO_Reinstatement_StayDate DATE",
                "CO_Reinstatement_StayOrderPath NVARCHAR(MAX)",
                "CO_Reinstatement_StayRemark NVARCHAR(MAX)",
                "CO_WP_ActionTaken NVARCHAR(100)",
                "IsReinstatementViewed BIT DEFAULT 0",
                "Against_CaseCategory NVARCHAR(200)",
                "Against_BriefFacts NVARCHAR(MAX)",
                "Against_PunishmentImposed NVARCHAR(200)",
                "Against_PunishmentNo NVARCHAR(100)",
                "Against_PunishmentDate DATE",
                "IsEnquiryOfficerEvidence BIT DEFAULT 0",
                "IsReporterEvidence BIT DEFAULT 0",
                "IsOtherEvidence BIT DEFAULT 0",
                "CO_OverallCaseStatus NVARCHAR(100)",
                "CO_Disposal_Nature NVARCHAR(100)",
                "CO_Disposal_CommSentToDivision BIT DEFAULT 0",
                "CO_Disposal_OutwardNo NVARCHAR(100)",
                "CO_Disposal_Date DATE",
                "CO_Disposal_Decision NVARCHAR(100)",
                "CO_Disposal_ApprovalOutwardNo NVARCHAR(100)",
                "CO_Disposal_ApprovalDate DATE",
                "CO_FurtherAppeal_Status_Option NVARCHAR(100)",
                "CO_FurtherAppeal_CaseNumber NVARCHAR(100)",
                "CO_FurtherAppeal_Year INT",
                "CO_FurtherAppeal_EntrustmentNo NVARCHAR(100)",
                "CO_FurtherAppeal_EntrustmentDate DATE",
                "CO_FurtherAppeal_AdvocateName NVARCHAR(200)",
                "CO_FurtherAppeal_CaseStatus NVARCHAR(100)",
                "CO_FurtherAppeal_DisposalOutwardNo NVARCHAR(100)",
                "CO_FurtherAppeal_DisposalDate DATE",
                "CO_Claimant_DivisionName NVARCHAR(200)",
                "CO_Claimant_ArisingOutOf NVARCHAR(100)",
                "CO_Claimant_Court NVARCHAR(200)",
                "CO_Claimant_CaseNumber NVARCHAR(100)",
                "CO_Claimant_HighCourtBench NVARCHAR(100)",
                "CO_Claimant_OriginalCaseStatus NVARCHAR(100)",
                "CO_Claimant_IsConnected BIT DEFAULT 0",
                "CO_Claimant_EntrustmentNo NVARCHAR(100)",
                "CO_Claimant_EntrustmentDate DATE",
                "CO_Claimant_AdvocateName NVARCHAR(200)",
                "CO_Claimant_CaseStatus NVARCHAR(100)",
                "CO_Claimant_PetitionCopyPath NVARCHAR(MAX)",
                "IsEPFiled BIT NOT NULL DEFAULT 0",
                "CO_WP_CaseStatus_Option NVARCHAR(100)",
                "CO_WP_CaseNumber NVARCHAR(100)",
                "CO_WP_Year INT",
                "CO_WP_HighCourtBench NVARCHAR(100)",
                "CO_WP_EntrustmentNo NVARCHAR(100)",
                "CO_WP_EntrustmentDate DATE",
                "CO_WP_AdvocateName NVARCHAR(200)",
                "CO_WP_StayGranted BIT",
                "CO_WP_StayApprovalNo NVARCHAR(100)",
                "CO_WP_StayNature NVARCHAR(100)",
                "CO_WP_StayDate DATE",
                "CO_WP_StayOrderPath NVARCHAR(MAX)",
                "CO_WP_StayRemark NVARCHAR(MAX)",
                "CO_WP_Status NVARCHAR(100)",
                "CO_WP_Outcome NVARCHAR(100)",
                "CO_WP_OutcomeRemark NVARCHAR(MAX)",
                "CO_WP_OutcomeOutwardNo NVARCHAR(100)",
                "CO_WP_OutcomeOutwardDate DATE",
                "CO_WP_JudgmentCopyPath NVARCHAR(MAX)",
                "NatureOfCase NVARCHAR(100)"
            };

            foreach (var colDef in columns)
            {
                var colName = colDef.Split(' ')[0];
                string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = '{colName}'";
                
                try
                {
                    int count = (int)(db.ExecuteScalar(check) ?? 0);
                    if (count == 0)
                    {
                        string alter = $"ALTER TABLE LABOUR_CASES ADD {colDef}";
                        db.ExecuteNonQuery(alter);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Migration Error for LABOUR_CASES.{colName}: {ex.Message}");
                }
            }
        }

        public static void EnsureAppealColumns(DBHelper db)
        {
            // Appeal Columns
            try
            {
                var columns = new[]
                {
                    "Opinion_CLO NVARCHAR(MAX) NULL",
                    "InitialActionRemarks NVARCHAR(MAX) NULL",
                    "ApprovalCopyPath NVARCHAR(MAX) NULL",
                    "InitialActionPath1 NVARCHAR(MAX) NULL",
                    "InitialActionPath2 NVARCHAR(MAX) NULL",
                    "OtherHighCourtBench NVARCHAR(100) NULL",
                    "IsPendingForFiling BIT NOT NULL DEFAULT 0",
                    "StayOrderPath1 NVARCHAR(MAX) NULL",
                    "StayOrderPath2 NVARCHAR(MAX) NULL",
                    "ComplianceLetterPath NVARCHAR(MAX) NULL",
                    "MFAJudgmentCopyPath NVARCHAR(MAX) NULL",
                    "CorpMFAActionTaken NVARCHAR(100) NULL",
                    "CorpMFAActionTakenPath NVARCHAR(MAX) NULL",
                    "ClaimantMFARemarks NVARCHAR(MAX) NULL",
                    "ClaimantSCJudgmentPath NVARCHAR(MAX) NULL",
                    "CorpMFACNRNumber NVARCHAR(50) NULL",
                    "CorpMFANextHearingDate DATE NULL",
                    "CorpMFAStage NVARCHAR(100) NULL",
                    "ClaimantMFACNRNumber NVARCHAR(50) NULL",
                    "ClaimantMFANextHearingDate DATE NULL",
                    "ClaimantMFAStage NVARCHAR(100) NULL"
                };

                foreach (var colDef in columns)
                {
                    var colName = colDef.Split(' ')[0];
                    string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = '{colName}'";
                    if ((int)(db.ExecuteScalar(check) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE APPEAL_DETAILS ADD {colDef}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for APPEAL_DETAILS columns: {ex.Message}");
            }
        }

        public static void EnsureMvcColumns(DBHelper db)
        {
            var columns = new[]
            {
                "TransferredFromDivisionID INT NULL",
                "TransferDate DATETIME NULL",
                "IsTransferViewed BIT NOT NULL DEFAULT 0",
                "PetitionFiledFor NVARCHAR(100) NULL",
                "DoubleClaimFlag BIT NOT NULL DEFAULT 0",
                "IsObjectionFiled BIT NOT NULL DEFAULT 0",
                "ObjectionFiledDate DATE NULL",
                "ObjectionOutwardNo NVARCHAR(50) NULL",
                "DisposalStatus NVARCHAR(50) NULL",
                "DisposalResult NVARCHAR(50) NULL",
                "DisposalRemarks NVARCHAR(500) NULL",
                "IsSTPassenger BIT NOT NULL DEFAULT 0",
                "IsMedicalExpensesPaid BIT NOT NULL DEFAULT 0",
                "MedicalPaidAmount DECIMAL(18,2) NULL",
                "MedicalPaidRemarks NVARCHAR(MAX) NULL",
                "IsARFAmountPaid BIT NOT NULL DEFAULT 0",
                "ARFPaidAmount DECIMAL(18,2) NULL",
                "ARFPaidRemarks NVARCHAR(MAX) NULL",
                "VehicleType NVARCHAR(20) DEFAULT 'Corporation'",
                "AdvocateName NVARCHAR(255) NULL",
                "IsWithinLimitation BIT NOT NULL DEFAULT 0",
                "LimitationRemark NVARCHAR(MAX) NULL",
                "IsDelayCondoned BIT NULL",
                "DelayRemark NVARCHAR(MAX) NULL",
                "ClosureDate DATE NULL",
                "IsOppositeVehicleInmate BIT NOT NULL DEFAULT 0",
                "ThirdPartyFlag BIT NOT NULL DEFAULT 0",
                "ClaimRemark NVARCHAR(MAX) NULL",
                "ClaimPetitionDate DATE NULL",
                "HasInterimOrder BIT NOT NULL DEFAULT 0",
                "InterimOrderFilePath NVARCHAR(MAX) NULL"
            };

            foreach (var colDef in columns)
            {
                var colName = colDef.Split(' ')[0];
                string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = '{colName}'";
                
                try
                {
                    int count = (int)(db.ExecuteScalar(check) ?? 0);
                    if (count == 0)
                    {
                        string alter = $"ALTER TABLE MVC_CASES ADD {colDef}";
                        db.ExecuteNonQuery(alter);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Migration Error for MVC_CASES.{colName}: {ex.Message}");
                }
            }

            // Migrate 'Fatal' to 'Death' for ClaimType and InjuryType
            try
            {
                db.ExecuteNonQuery("UPDATE MVC_CASES SET ClaimType = 'Death' WHERE ClaimType = 'Fatal'");
                db.ExecuteNonQuery("UPDATE MVC_CASE_ADVERSE_DETAILS SET InjuryType = 'Death' WHERE InjuryType = 'Fatal'");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for ClaimType/InjuryType update: {ex.Message}");
            }
        }

        public static void EnsureCustomCompensationColumns(DBHelper db)
        {
            var tables = new[] { "MVC_CASE_ADVERSE_DETAILS", "MVC_REMIND_BACK_ADVERSE_DETAILS" };
            foreach (var table in tables)
            {
                string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'CustomCompensation'";
                try
                {
                    int count = (int)(db.ExecuteScalar(check) ?? 0);
                    if (count == 0)
                    {
                        string alter = $"ALTER TABLE {table} ADD CustomCompensation NVARCHAR(MAX) NULL";
                        db.ExecuteNonQuery(alter);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Migration Error for {table}.CustomCompensation: {ex.Message}");
                }
            }
        }

    public static void EnsureMvcAdverseColumns(DBHelper db)
    {
        var tables = new[] { "MVC_CASE_ADVERSE_DETAILS", "MVC_REMIND_BACK_ADVERSE_DETAILS" };
        var columns = new[]
        {
            "IsMedicalInsuranceClaimed BIT NOT NULL DEFAULT 0",
            "IncomePeriod VARCHAR(20) DEFAULT 'monthly'",
            "IsMedicalBillsVerified BIT NULL",
            "IsFIRFiled BIT NULL",
            "IsChargeSheetFiled BIT NULL",
            "DisabilityPercentage DECIMAL(18,2) NULL",
            "PunishmentRemarks NVARCHAR(500) NULL",
            "InterimCompAmount DECIMAL(18,2) NULL",
            "InterimCompDeducted BIT NOT NULL DEFAULT 0",
            "IsBusCameraInstalled BIT NULL",
            "IsCameraFootageProduced BIT NULL",
            "IsPhotographProduced BIT NULL",
            "PhotographNotProducedReason NVARCHAR(MAX) NULL",
            "PoliceSketchExhibitNo NVARCHAR(100) NULL",
            "IsPoliceSketchEnclosed BIT NULL",
            "IsEvidenceBasedOnSecurityReport BIT NULL",
            "SecurityReportNoEvidenceReason NVARCHAR(MAX) NULL",
            "IsImpleadingAppFiled BIT NULL",
            "ImpleadingAppNotFiledReason NVARCHAR(MAX) NULL",
            "IsVictimSalaried BIT NULL",
            "IsIncomeCrossVerified BIT NULL",
            "DoesIncomeTallyWithDocuments BIT NULL",
            "IsAmountDepositedInEP BIT NOT NULL DEFAULT 0",
            "IsEPFiled BIT NOT NULL DEFAULT 0",
            "EPDepositedAmount DECIMAL(18,2) NULL",
            "FutureProspectsPercentage DECIMAL(18,2) NULL",
            "PersonalExpensesDeduction DECIMAL(18,2) NULL",
            "Multiplier DECIMAL(18,2) NULL",
            "LossOfDependency DECIMAL(18,2) NULL",
            "LossOfConsortium DECIMAL(18,2) NULL",
            "LossOfEstate DECIMAL(18,2) NULL",
            "FuneralExpenses DECIMAL(18,2) NULL",
            "LossOfLoveAffection DECIMAL(18,2) NULL",
            "MedicalExpenseOther DECIMAL(18,2) NULL",
            "PainSufferings DECIMAL(18,2) NULL",
            "ConveyanceAttendant DECIMAL(18,2) NULL",
            "LossOfFutureIncome DECIMAL(18,2) NULL",
            "LossOfIncomeLaidUp DECIMAL(18,2) NULL",
            "LossOfAmenities DECIMAL(18,2) NULL",
            "FutureMedicalExpenses DECIMAL(18,2) NULL",
            "InjuryOtherExpense DECIMAL(18,2) NULL",
            "EPNumber NVARCHAR(100) NULL",
            "EPCourt NVARCHAR(255) NULL",
            "EPStage NVARCHAR(100) NULL",
            "EPNextHearingDate DATE NULL",
            "AgeProofUploadPath NVARCHAR(MAX) NULL",
            "FutureProspectus NVARCHAR(MAX) NULL",
            "IsSTPassenger BIT NOT NULL DEFAULT 0",
            "IsMedicalExpensesPaid BIT NOT NULL DEFAULT 0",
            "MedicalPaidAmount DECIMAL(18,2) NULL",
            "MedicalPaidRemarks NVARCHAR(MAX) NULL",
            "IsARFAmountPaid BIT NOT NULL DEFAULT 0",
            "ARFPaidAmount DECIMAL(18,2) NULL",
            "ARFPaidRemarks NVARCHAR(MAX) NULL",
            "IsDeceasedInTR18 BIT NOT NULL DEFAULT 0",
            "TreatedDocFlag BIT NOT NULL DEFAULT 0",
            "MannerOfAccidentRO NVARCHAR(MAX) NULL",
            "AdverseDoubleClaimFlag BIT NOT NULL DEFAULT 0",
            "DoubleClaimDetails NVARCHAR(MAX) NULL",
            "DisposedOnDate DATE NULL",
            "CopyAppliedDate DATE NULL",
            "CertifiedCopyRemarks NVARCHAR(500) NULL",
            "CopyIssuedDate DATE NULL",
            "CopyReceivedDate DATE NULL",
            "CopyDeliveredDate DATE NULL",
            "OutwardNumber NVARCHAR(100) NULL",
            "OutwardDate DATE NULL",
            "ClosureRemarks NVARCHAR(MAX) NULL",
            "AdvocateOpinion NVARCHAR(MAX) NULL",
            "LOOpinion NVARCHAR(MAX) NULL",
            "DCOpinion NVARCHAR(MAX) NULL",
            "IsCorpLiable BIT NOT NULL DEFAULT 0",
            "LiabilityPercentage DECIMAL(18,2) NULL",
            "LiabilityRemarks NVARCHAR(MAX) NULL",
            "AdverseJudgmentUploadPath NVARCHAR(MAX) NULL",
            "InjuryType NVARCHAR(100) NULL",
            "AsPerECourts NVARCHAR(MAX) NULL",
            // M3-P1: RoundOff field for UI award summary card
            "RoundOffAmount DECIMAL(18,2) NULL",
            "BusInsuranceDetails NVARCHAR(MAX) NULL",
            "IsBusInsured BIT NOT NULL DEFAULT 0",
            "ClaimPetitionDate DATE NULL",
            "AwardDate DATE NULL",
            "MannerOfAccident NVARCHAR(MAX) NULL",
            "ObjectionFiled BIT NOT NULL DEFAULT 0",
            "ObjectionRemarks NVARCHAR(MAX) NULL",
            "RWType NVARCHAR(100) NULL",
            "TR18Remarks NVARCHAR(MAX) NULL",
            "IsAllegedAccident BIT NOT NULL DEFAULT 0",
            "SecurityRequired BIT NOT NULL DEFAULT 0",
            "SecurityUploadPath NVARCHAR(MAX) NULL",
            "GovIDProofUploadPath NVARCHAR(MAX) NULL",
            "AwardAmount DECIMAL(18,2) NULL",
            "InterestRate DECIMAL(18,2) NULL",
            "VictimAge INT NULL",
            "Occupation NVARCHAR(255) NULL",
            "IncomeConsidered DECIMAL(18,2) NULL",
            "InjuryDetails NVARCHAR(MAX) NULL",
            "DriverPunishmentStatus NVARCHAR(100) NULL",
            "PunishmentOrderUploadPath NVARCHAR(MAX) NULL",
            "ForwardingStatus NVARCHAR(100) NULL",
            "IsDelayApplicationFiled BIT NOT NULL DEFAULT 0",
            "DelayApplicationPath NVARCHAR(MAX) NULL",
            "IsDelayCondonedAdverse BIT NULL",
            "DelayCondonedOrderPath NVARCHAR(MAX) NULL"
        };

        foreach (var table in tables)
        {
            foreach (var colDef in columns)
            {
                var colName = colDef.Split(' ')[0];
                string check = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{colName}'";
                try
                {
                    int count = (int)(db.ExecuteScalar(check) ?? 0);
                    if (count == 0)
                    {
                        string alter = $"ALTER TABLE {table} ADD {colDef}";
                        db.ExecuteNonQuery(alter);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Migration Error for {table}.{colName}: {ex.Message}");
                }
            }
        }
    }

        public static void EnsureGratuitySchema(DBHelper db)
        {
            try
            {
                // GRA_INTEREST_PAYMENTS
                string checkInterestTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_INTEREST_PAYMENTS'";
                if ((int)(db.ExecuteScalar(checkInterestTable) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE GRA_INTEREST_PAYMENTS (
                            InterestPaymentID INT PRIMARY KEY IDENTITY(1,1),
                            CaseID INT NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            ChequeNumber NVARCHAR(100) NULL,
                            ChequeDate DATETIME NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            CONSTRAINT FK_GRA_INTEREST_PAYMENTS_CaseID FOREIGN KEY (CaseID) REFERENCES GRA_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // GRA_PAYMENTS
                string checkPaymentTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_PAYMENTS'";
                if ((int)(db.ExecuteScalar(checkPaymentTable) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE GRA_PAYMENTS (
                            PaymentID INT PRIMARY KEY IDENTITY(1,1),
                            CaseID INT NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            ChequeNumber NVARCHAR(100) NULL,
                            ChequeDate DATETIME NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            CONSTRAINT FK_GRA_PAYMENTS_CaseID FOREIGN KEY (CaseID) REFERENCES GRA_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // CLOOpinion column in GRA_CASES
                string checkCLOColumn = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GRA_CASES' AND COLUMN_NAME = 'CLOOpinion'";
                if ((int)(db.ExecuteScalar(checkCLOColumn) ?? 0) == 0)
                {
                    db.ExecuteNonQuery("ALTER TABLE GRA_CASES ADD CLOOpinion NVARCHAR(MAX) NULL");
                }

                // Missing Interest Columns
                var interestColumns = new[]
                {
                    "IsInterestPayable BIT NULL",
                    "InterestRate DECIMAL(18,2) NULL",
                    "InterestRemarks NVARCHAR(MAX) NULL"
                };

                foreach (var colDef in interestColumns)
                {
                    var colName = colDef.Split(' ')[0];
                    string checkCol = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GRA_CASES' AND COLUMN_NAME = '{colName}'";
                    if ((int)(db.ExecuteScalar(checkCol) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE GRA_CASES ADD {colDef}");
                    }
                }

                // Appeal Forwarding columns
                var appealForwardingColumns = new[]
                {
                    "ForwardingStatus_Appeal NVARCHAR(200) NULL",
                    "OutwardNumber_Appeal NVARCHAR(100) NULL",
                    "OutwardDate_Appeal DATE NULL"
                };

                foreach (var colDef in appealForwardingColumns)
                {
                    var colName = colDef.Split(' ')[0];
                    string checkCol = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GRA_CASES' AND COLUMN_NAME = '{colName}'";
                    if ((int)(db.ExecuteScalar(checkCol) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE GRA_CASES ADD {colDef}");
                    }
                }

                // New missing Appeal and Evidence columns for PGA Appeal entry
                var missingColumns = new[]
                {
                    "HighCourtBench NVARCHAR(100) NULL",
                    "OtherHighCourtBench NVARCHAR(100) NULL",
                    "StayGranted BIT NULL",
                    "StayComplianceOutwardNo NVARCHAR(100) NULL",
                    "StayComplianceDate DATE NULL",
                    "StayOrderPath1 NVARCHAR(MAX) NULL",
                    "StayOrderPath2 NVARCHAR(MAX) NULL",
                    "InitialActionRemarks NVARCHAR(MAX) NULL",
                    "InitialActionPath1 NVARCHAR(MAX) NULL",
                    "InitialActionPath2 NVARCHAR(MAX) NULL",
                    "FinalRemarks NVARCHAR(MAX) NULL",
                    "AppealActionOutwardNo NVARCHAR(100) NULL",
                    "AppealActionDate DATE NULL",
                    "CorpWPNumber NVARCHAR(100) NULL",
                    "CorpWPYear INT NULL",
                    "CorpWPAdvocate NVARCHAR(200) NULL",
                    "CorpWPEntrustmentNo NVARCHAR(100) NULL",
                    "CorpWPEntrustmentDate DATE NULL",
                    "CorpWPStatus NVARCHAR(100) NULL",
                    "RestorationFiled BIT NULL",
                    "RestorationDate DATE NULL",
                    "RestorationStatus NVARCHAR(100) NULL",
                    "CorpWPOutcome NVARCHAR(100) NULL",
                    "CorpWPActionTaken NVARCHAR(100) NULL",
                    "ClosureOutwardNo NVARCHAR(100) NULL",
                    "IsClaimantSCAppeal BIT NULL",
                    "IsClaimantSCPending BIT NULL",
                    "ClaimantSCDiaryNumber NVARCHAR(100) NULL",
                    "ClaimantSCYear INT NULL",
                    "ClaimantSCNumber NVARCHAR(100) NULL",
                    "ClaimantSLPYear INT NULL",
                    "ClaimantSCFiledBy NVARCHAR(100) NULL",
                    "ClaimantSCEntrustmentNo NVARCHAR(100) NULL",
                    "ClaimantSCEntrustmentDate DATE NULL",
                    "ClaimantSCAdvocate NVARCHAR(200) NULL",
                    "ClaimantSCStatus NVARCHAR(100) NULL",
                    "ClaimantSCOutcome NVARCHAR(100) NULL",
                    "ClaimantSCActionTaken NVARCHAR(100) NULL",
                    "ClaimantSCClosureNo NVARCHAR(100) NULL",
                    "ClaimantSCClosureDate DATE NULL",
                    "ClaimantDivisionID INT NULL",
                    "ClaimantArisingWPNumber NVARCHAR(100) NULL",
                    "ClaimantArisingWPYear INT NULL",
                    "ClaimantMVCCurrentStatus NVARCHAR(100) NULL",
                    "ClaimantWPNumber NVARCHAR(100) NULL",
                    "ClaimantWPYear INT NULL",
                    "ClaimantHighCourtBench NVARCHAR(100) NULL",
                    "ClaimantOtherHighCourtBench NVARCHAR(100) NULL",
                    "ClaimantWPEntrustmentNo NVARCHAR(100) NULL",
                    "ClaimantWPEntrustmentDate DATE NULL",
                    "ClaimantWPAdvocate NVARCHAR(200) NULL",
                    "ClaimantWPStatus NVARCHAR(100) NULL",
                    "ClaimantWPDecision NVARCHAR(100) NULL",
                    "ClaimantActionTaken NVARCHAR(100) NULL",
                    "ClaimantApprovalNo NVARCHAR(100) NULL",
                    "ClaimantApprovalDate DATE NULL",
                    "AppealCopyAppliedDate DATE NULL",
                    "AppealCopyReadyDate DATE NULL",
                    "AppealCopyDeliveredDate DATE NULL",
                    "AppealCopyReceivedDate DATE NULL",
                    "AppealDelayRemarks NVARCHAR(MAX) NULL",
                    "AppealAdvocateOpinion NVARCHAR(MAX) NULL",
                    "AppealLOOpinion NVARCHAR(MAX) NULL",
                    "AppealDCOpinion NVARCHAR(MAX) NULL",
                    "EvidenceRemarks NVARCHAR(MAX) NULL",
                    "EvidenceWitnessName NVARCHAR(200) NULL",
                    "EvidenceWitnessDesignation NVARCHAR(100) NULL",
                    "AppealComplianceAmount DECIMAL(18,2) NULL",
                    "AppealComplianceChequeNumber NVARCHAR(100) NULL",
                    "AppealComplianceChequeDate DATE NULL",
                    "AppealNumber NVARCHAR(100) NULL",
                    "AppealYear INT NULL",
                    "AppealArisingNo NVARCHAR(100) NULL",
                    "AppealArisingYear INT NULL",
                    "AppealCourt NVARCHAR(200) NULL",
                    "AppealEntrustmentNo NVARCHAR(100) NULL",
                    "AppealEntrustmentDate DATE NULL",
                    "AppealAdvocate NVARCHAR(200) NULL",
                    "AppealAwardDetails NVARCHAR(MAX) NULL",
                    "AppealDisposalDate DATE NULL",
                    "AppealJudgmentPath NVARCHAR(MAX) NULL",
                    "FinalAmount DECIMAL(18,2) NULL",
                    "ComplianceStatus NVARCHAR(100) NULL",
                    "Remarks NVARCHAR(MAX) NULL",
                    "AppealRemarks NVARCHAR(MAX) NULL",
                    "AppealDate DATE NULL"
                };

                foreach (var colDef in missingColumns)
                {
                    var colName = colDef.Split(' ')[0];
                    string checkCol = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GRA_CASES' AND COLUMN_NAME = '{colName}'";
                    if ((int)(db.ExecuteScalar(checkCol) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE GRA_CASES ADD {colDef}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for Gratuity: {ex.Message}");
            }
        }

        public static void EnsureLabourEPTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_EP_DETAILS'";
                if ((int)(db.ExecuteScalar(checkTable) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE LABOUR_EP_DETAILS (
                            EPID INT PRIMARY KEY IDENTITY(1,1),
                            CaseID INT NOT NULL,
                            DivisionID INT NOT NULL DEFAULT(0),
                            EPNumber NVARCHAR(50) NULL,
                            EPYear INT NULL,
                            EPCourt NVARCHAR(200) NULL,
                            EP_EntrustmentNo NVARCHAR(100) NULL,
                            EP_EntrustmentDate DATE NULL,
                            ArisingFromCaseNo NVARCHAR(100) NULL,
                            ArisingFromCaseYear INT NULL,
                            ArisingFromCourt NVARCHAR(200) NULL,
                            OriginalCaseStatus NVARCHAR(50) NULL,
                            EPStatus NVARCHAR(50) NULL,
                            NextHearingDate DATE NULL,
                            AsPerECourts NVARCHAR(200) NULL,
                            IsSentToAccounts BIT NOT NULL DEFAULT 0,
                            DateSentToAccounts DATE NULL,
                            AwardAmount DECIMAL(18,2) NULL,
                            InterestRate DECIMAL(5,2) NULL,
                            LiabilityPercentage DECIMAL(5,2) NULL,
                            PetitionDate DATE NULL,
                            PettyBillDate DATE NULL,
                            PettyBillAmount DECIMAL(18,2) NULL,
                            ChequeNumber NVARCHAR(50) NULL,
                            ChequeDate DATE NULL,
                            AmountPaid DECIMAL(18,2) NULL,
                            ComplianceStatus NVARCHAR(50) NULL,
                            DateOfCompliance DATE NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            CreatedBy NVARCHAR(100) NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            ModifiedBy NVARCHAR(100) NULL,
                            ModifiedDate DATETIME NULL
                        )");
                }
                else
                {
                    // Ensure EP_EntrustmentNo and Date if table exists but they are missing
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EP_EntrustmentNo", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EP_EntrustmentDate", "DATE NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "DivisionID", "INT NOT NULL DEFAULT 0");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EPNumber", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EPYear", "INT NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EPCourt", "NVARCHAR(200) NULL");
                }

                string checkPayments = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_EP_PAYMENTS'";
                if ((int)(db.ExecuteScalar(checkPayments) ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE LABOUR_EP_PAYMENTS (
                            PaymentID INT PRIMARY KEY IDENTITY(1,1),
                            EPID INT NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            PaymentDate DATE NOT NULL,
                            ChequeNumber NVARCHAR(50) NULL,
                            ChequeDate DATE NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_LABOUR_EP_PAYMENTS_EPID FOREIGN KEY (EPID) REFERENCES LABOUR_EP_DETAILS(EPID) ON DELETE CASCADE
                        )");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Labour EP Migration Error: " + ex.Message);
            }
        }

        public static void EnsureOtpTable(DBHelper db)
        {
            string check = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'USER_OTP_VERIFICATIONS'";
            try
            {
                int count = (int)(db.ExecuteScalar(check) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE USER_OTP_VERIFICATIONS (
                            VerificationID INT PRIMARY KEY IDENTITY(1,1),
                            UserID INT NOT NULL,
                            OTP VARCHAR(10) NOT NULL,
                            ExpiryTime DATETIME NOT NULL,
                            IsVerified BIT DEFAULT 0,
                            CreatedAt DATETIME DEFAULT GETDATE(),
                            FOREIGN KEY (UserID) REFERENCES USERS(UserID)
                        );
                        CREATE INDEX IX_USER_OTP_VERIFICATIONS_UserID ON USER_OTP_VERIFICATIONS(UserID);
                        CREATE INDEX IX_USER_OTP_VERIFICATIONS_OTP ON USER_OTP_VERIFICATIONS(OTP);";
                    db.ExecuteNonQuery(create);
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for USER_OTP_VERIFICATIONS: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the LABOUR_ARISING_APPLICATIONS table if it does not exist.
        /// Separate table for Arising Applications with FK to LABOUR_CASES.
        /// </summary>
        public static void EnsureArisingApplicationsTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_ARISING_APPLICATIONS'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE LABOUR_ARISING_APPLICATIONS (
                            ArisingID INT IDENTITY(1,1) PRIMARY KEY,
                            ParentCaseID INT NULL,
                            Parent_CaseNumber NVARCHAR(100) NULL,
                            Parent_CaseYear INT NULL,
                            Parent_CourtID INT NULL,
                            Parent_CourtName NVARCHAR(200) NULL,
                            Parent_CaseType NVARCHAR(50) NULL,
                            Parent_CaseStatus NVARCHAR(50) NULL,
                            Parent_PetitionerName NVARCHAR(200) NULL,
                            DivisionID INT NOT NULL,
                            CaseNumber NVARCHAR(100) NOT NULL,
                            CaseYear INT NULL,
                            CourtID INT NULL,
                            OtherCourtDetails NVARCHAR(200) NULL,
                            CaseType NVARCHAR(50) DEFAULT 'Arising Application',
                            CaseStatus NVARCHAR(50) DEFAULT 'Pending',
                            PetitionerName NVARCHAR(200) NULL,
                            EntrustmentNo NVARCHAR(100) NULL,
                            EntrustmentDate DATE NULL,
                            AdvocateID INT NULL,
                            AdvocateName NVARCHAR(200) NULL,
                            IsDocumentSent BIT DEFAULT 0,
                            DocumentSent_OutwardNo NVARCHAR(50) NULL,
                            DocumentSent_OutwardDate DATE NULL,
                            IsObjectionFiled BIT DEFAULT 0,
                            ObjectionFiled_OutwardNo NVARCHAR(50) NULL,
                            ObjectionFiled_OutwardDate DATE NULL,
                            IsEvidenceFiled BIT DEFAULT 0,
                            CurrentStage NVARCHAR(200) NULL,
                            NextHearingDate DATE NULL,
                            DisposalMode NVARCHAR(100) NULL,
                            DisposalDate DATE NULL,
                            DisposalResult NVARCHAR(100) NULL,
                            FavorRemark NVARCHAR(MAX) NULL,
                            FavorOutwardDate DATE NULL,
                            JudgmentCopyPath NVARCHAR(MAX) NULL,
                            LokAdalat_COApprovalRequired BIT DEFAULT 0,
                            LokAdalat_OutwardNo NVARCHAR(100) NULL,
                            LokAdalat_Date DATE NULL,
                            LokAdalatDocumentPath NVARCHAR(MAX) NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            CreatedBy INT NULL,
                            ModifiedDate DATETIME NULL,
                            ModifiedBy INT NULL,
                            DE_HistorySheet BIT DEFAULT 0,
                            DE_HistorySheetPath NVARCHAR(500) NULL,
                            DE_ObjectionsFiled BIT DEFAULT 0,
                            DE_ObjectionsRemark NVARCHAR(MAX) NULL,
                            DE_DocumentsMarked BIT DEFAULT 0,
                            DE_DocumentsRemark NVARCHAR(MAX) NULL,
                            DE_Order NVARCHAR(50) NULL,
                            DE_EO_IsBasedOnDocuments BIT DEFAULT 0,
                            DE_EO_Name NVARCHAR(200) NULL,
                            DE_EO_Designation NVARCHAR(100) NULL,
                            DE_Reporter_IsBasedOnDocuments BIT DEFAULT 0,
                            DE_Reporter_Name NVARCHAR(200) NULL,
                            DE_Reporter_Designation NVARCHAR(100) NULL,
                            DE_Other_IsBasedOnDocuments BIT DEFAULT 0,
                            DE_Other_Name NVARCHAR(200) NULL,
                            DE_Other_Designation NVARCHAR(100) NULL,
                            CC_CaseDisposedDate DATE NULL,
                            CC_PublicationDate DATE NULL,
                            CC_AppliedDate DATE NULL,
                            CC_IssuedDate DATE NULL,
                            CC_DeliveredDate DATE NULL,
                            CC_ReceivedDate DATE NULL,
                            CC_Remarks NVARCHAR(MAX) NULL,
                            AwardDetails NVARCHAR(MAX) NULL,
                            Opinion_Advocate NVARCHAR(MAX) NULL,
                            Opinion_LO NVARCHAR(MAX) NULL,
                            Opinion_DC NVARCHAR(MAX) NULL,
                            SentToCO BIT DEFAULT 0,
                            CO_OutwardNo NVARCHAR(100) NULL,
                            CO_OutwardDate DATE NULL,
                            CO_Remarks NVARCHAR(MAX) NULL,
                            Opinion_CLO NVARCHAR(MAX) NULL,
                            EmployeeNo NVARCHAR(50) NULL,
                            PFNumber NVARCHAR(50) NULL,
                            Designation NVARCHAR(100) NULL,
                            WorkingStatus NVARCHAR(50) NULL,
                            LegalRepresentativeName NVARCHAR(200) NULL,
                            LRRelationship NVARCHAR(100) NULL,
                            IsWorkman BIT DEFAULT 0,
                            IsWorkmanRemark NVARCHAR(MAX) NULL,
                            NatureOfCase NVARCHAR(100) NULL,
                            NatureOfMisconduct NVARCHAR(MAX) NULL,
                            ClaimFiledOn NVARCHAR(200) NULL,
                            DelayInFiling NVARCHAR(200) NULL,
                            ClaimDetails NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_ArisingApp_ParentCase 
                                FOREIGN KEY (ParentCaseID) REFERENCES LABOUR_CASES(CaseID)
                                ON DELETE SET NULL ON UPDATE CASCADE
                        );
                        CREATE NONCLUSTERED INDEX IX_ArisingApp_ParentCaseID ON LABOUR_ARISING_APPLICATIONS(ParentCaseID);
                        CREATE NONCLUSTERED INDEX IX_ArisingApp_CaseNumber_Year ON LABOUR_ARISING_APPLICATIONS(CaseNumber, CaseYear);
                        CREATE NONCLUSTERED INDEX IX_ArisingApp_DivisionID ON LABOUR_ARISING_APPLICATIONS(DivisionID);
                    ";
                    db.ExecuteNonQuery(create);
                    System.Console.WriteLine("Migration: LABOUR_ARISING_APPLICATIONS table created.");
                }
                else
                {
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "EmployeeNo", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "PFNumber", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "Designation", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "WorkingStatus", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LegalRepresentativeName", "NVARCHAR(200) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LRRelationship", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "IsWorkman", "BIT DEFAULT 0");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "IsWorkmanRemark", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "NatureOfCase", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "NatureOfMisconduct", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "ClaimFiledOn", "NVARCHAR(200) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "DelayInFiling", "NVARCHAR(200) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "ClaimDetails", "NVARCHAR(MAX) NULL");

                    // Extra core fields that were missing
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "DisposalMode", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "DisposalDate", "DATE NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "DisposalResult", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "FavorRemark", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "FavorOutwardDate", "DATE NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "JudgmentCopyPath", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LokAdalat_COApprovalRequired", "BIT DEFAULT 0");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LokAdalat_OutwardNo", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LokAdalat_Date", "DATE NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "LokAdalatDocumentPath", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "Opinion_CLO", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "CO_WP_JudgmentCopyPath", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "ClaimPetitionPath", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "AwardAmount", "DECIMAL(18,2) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "CO_WP_CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "CO_WA_CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "EstCode", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", "CaseTypeCode", "NVARCHAR(20) NULL");

                    db.EnsureColumn("LABOUR_EP_DETAILS", "CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "EstCode", "NVARCHAR(50) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "CaseTypeCode", "NVARCHAR(20) NULL");

                    db.EnsureColumn("LABOUR_CASES", "CO_WP_CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_CASES", "CO_WA_CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("LABOUR_CASES", "CO_Claimant_CNRNumber", "NVARCHAR(16) NULL");

                    // Ensure Performance Indexes across Entire Legal System
                    try
                    {
                        db.ExecuteNonQuery(@"
                            -- Labour Module Indexes
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_PERF')
                                CREATE INDEX IX_LABOUR_CASES_PERF ON LABOUR_CASES(DivisionID, CaseStatus, CreatedDate);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_CNR')
                                CREATE INDEX IX_LABOUR_CASES_CNR ON LABOUR_CASES(CNRNumber);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_CASENO')
                                CREATE INDEX IX_LABOUR_CASES_CASENO ON LABOUR_CASES(CaseNumber, CaseYear);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_ARISING_PARENT')
                                CREATE INDEX IX_LABOUR_ARISING_PARENT ON LABOUR_ARISING_APPLICATIONS(ParentCaseID);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_ARISING_CNR')
                                CREATE INDEX IX_LABOUR_ARISING_CNR ON LABOUR_ARISING_APPLICATIONS(CNRNumber);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_EP_CASEID')
                                CREATE INDEX IX_LABOUR_EP_CASEID ON LABOUR_EP_DETAILS(CaseID);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_EP_CNR')
                                CREATE INDEX IX_LABOUR_EP_CNR ON LABOUR_EP_DETAILS(CNRNumber);

                            -- MVC & Main Cases Indexes
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_DIV_STATUS')
                                CREATE INDEX IX_MVC_CASES_DIV_STATUS ON MVC_CASES(DivisionID, StatusID, CreatedAt);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_CNR')
                                CREATE INDEX IX_MVC_CASES_CNR ON MVC_CASES(CNRNumber);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_NO_YEAR')
                                CREATE INDEX IX_MVC_CASES_NO_YEAR ON MVC_CASES(MVCNo, MVCYear);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_HEARINGS_CASEID')
                                CREATE INDEX IX_MVC_HEARINGS_CASEID ON MVC_CASE_HEARINGS(CaseID, HearingDate);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_PETITIONERS_CASEID')
                                CREATE INDEX IX_MVC_PETITIONERS_CASEID ON MVC_CASE_PETITIONERS(CaseID);
                            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_RESPONDENTS_CASEID')
                                CREATE INDEX IX_MVC_RESPONDENTS_CASEID ON MVC_CASE_RESPONDENTS(CaseID);

                            -- Appeals & Gratuity Indexes
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'APPEAL_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_APPEAL_CASES_DIV')
                                CREATE INDEX IX_APPEAL_CASES_DIV ON APPEAL_CASES(DivisionID, CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_CASES_DIV_STATUS')
                            BEGIN
                                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'DivisionCode')
                                    CREATE INDEX IX_GRA_CASES_DIV_STATUS ON GRA_CASES(DivisionCode, CaseStatus);
                                ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'DivisionID')
                                    CREATE INDEX IX_GRA_CASES_DIV_STATUS ON GRA_CASES(DivisionID, CaseStatus);
                            END
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_CASES_HEARING')
                                CREATE INDEX IX_GRA_CASES_HEARING ON GRA_CASES(NextHearingDate);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OTHER_CASES_DIV_TYPE')
                                CREATE INDEX IX_OTHER_CASES_DIV_TYPE ON OTHER_CASES(DivisionID, CaseType, LitigantType);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OTHER_CASES_CNR')
                                CREATE INDEX IX_OTHER_CASES_CNR ON OTHER_CASES(CNRNumber) WHERE CNRNumber IS NOT NULL;
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PETTY_BILLS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PETTY_BILLS_DIV')
                                CREATE INDEX IX_PETTY_BILLS_DIV ON PETTY_BILLS(DivisionID, Status);

                            -- Appeal Details, Adverse Cases, View Tracking & Child Collections Performance Indexes
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'APPEAL_DETAILS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_APPEAL_DETAILS_CASEID')
                                CREATE INDEX IX_APPEAL_DETAILS_CASEID ON APPEAL_DETAILS(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_DETAILS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_ADVERSE_CASEID')
                                CREATE INDEX IX_MVC_ADVERSE_CASEID ON MVC_CASE_ADVERSE_DETAILS(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CASE_VIEW_TRACKING') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CASE_VIEW_TRACKING_CASEID')
                                CREATE INDEX IX_CASE_VIEW_TRACKING_CASEID ON CASE_VIEW_TRACKING(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_CONNECTED') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASE_CONNECTED_CASEID')
                                CREATE INDEX IX_MVC_CASE_CONNECTED_CASEID ON MVC_CASE_CONNECTED(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_PAYMENTS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_PAYMENTS_CASEID')
                                CREATE INDEX IX_GRA_PAYMENTS_CASEID ON GRA_PAYMENTS(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_INTEREST_PAYMENTS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_INTEREST_CASEID')
                                CREATE INDEX IX_GRA_INTEREST_CASEID ON GRA_INTEREST_PAYMENTS(CaseID);
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CAUSELIST_ITEMS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CAUSELIST_ITEMS_LISTID')
                                CREATE INDEX IX_CAUSELIST_ITEMS_LISTID ON CAUSELIST_ITEMS(CauselistID);
                        ");
                    }
                    catch { /* Indexes optimization guard */ }

                    string checkArisingPayments = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_ARISING_PAYMENTS'";
                    if ((int)(db.ExecuteScalar(checkArisingPayments) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery(@"
                            CREATE TABLE LABOUR_ARISING_PAYMENTS (
                                PaymentID INT PRIMARY KEY IDENTITY(1,1),
                                ArisingID INT NOT NULL,
                                Amount DECIMAL(18,2) NOT NULL,
                                PaymentDate DATE NOT NULL,
                                ChequeNumber NVARCHAR(50) NULL,
                                ChequeDate DATE NULL,
                                Remarks NVARCHAR(MAX) NULL,
                                CreatedDate DATETIME DEFAULT GETDATE(),
                                CONSTRAINT FK_LABOUR_ARISING_PAYMENTS_ARISINGID FOREIGN KEY (ArisingID) REFERENCES LABOUR_ARISING_APPLICATIONS(ArisingID) ON DELETE CASCADE
                            )");
                    }

                    string checkArisingDocs = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_ARISING_ENCLOSED_DOCS'";
                    if ((int)(db.ExecuteScalar(checkArisingDocs) ?? 0) == 0)
                    {
                        db.ExecuteNonQuery(@"
                            CREATE TABLE LABOUR_ARISING_ENCLOSED_DOCS (
                                DocID INT PRIMARY KEY IDENTITY(1,1),
                                ArisingID INT NOT NULL,
                                DocName NVARCHAR(500) NULL,
                                PageCount INT NULL,
                                CONSTRAINT FK_LABOUR_ARISING_ENCLOSED_DOCS_ARISINGID FOREIGN KEY (ArisingID) REFERENCES LABOUR_ARISING_APPLICATIONS(ArisingID) ON DELETE CASCADE
                            )");
                    }

                    db.EnsureColumn("LABOUR_EP_DETAILS", "AdvocateID", "INT NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "AdvocateName", "NVARCHAR(200) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "CaseOutcome", "NVARCHAR(100) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "DisposalDate", "DATE NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "DisposalRemarks", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("LABOUR_EP_DETAILS", "ClosureDate", "DATE NULL");

                    // Central Office Action Fields
                    var coColumns = new[]
                    {
                        "CO_FeasibilityReceived BIT NULL",
                        "CO_FeasibilityDate DATE NULL",
                        "CO_ActionTaken NVARCHAR(100) NULL",
                        "CO_ApprovalOutwardNo NVARCHAR(100) NULL",
                        "CO_ApprovalDate DATE NULL",
                        "CO_ClosedDocumentPath NVARCHAR(MAX) NULL",
                        
                        "CO_WP_CaseStatus_Option NVARCHAR(100) NULL",
                        "CO_WP_CaseNumber NVARCHAR(100) NULL",
                        "CO_WP_Year INT NULL",
                        "CO_WP_HighCourtBench NVARCHAR(100) NULL",
                        "CO_WP_EntrustmentNo NVARCHAR(100) NULL",
                        "CO_WP_EntrustmentDate DATE NULL",
                        "CO_WP_AdvocateName NVARCHAR(255) NULL",
                        "CO_WP_StayGranted BIT NULL",
                        "CO_WP_StayApprovalNo NVARCHAR(100) NULL",
                        "CO_WP_StayNature NVARCHAR(255) NULL",
                        "CO_WP_StayDate DATE NULL",
                        "CO_WP_StayOrderPath NVARCHAR(MAX) NULL",
                        "CO_WP_StayRemark NVARCHAR(MAX) NULL",
                        "CO_WP_Status NVARCHAR(100) NULL",
                        "CO_WP_Outcome NVARCHAR(100) NULL",
                        "CO_WP_OutcomeRemark NVARCHAR(MAX) NULL",
                        "CO_WP_OutcomeOutwardNo NVARCHAR(100) NULL",
                        "CO_WP_OutcomeOutwardDate DATE NULL",
                        "CO_WP_ActionTaken NVARCHAR(100) NULL",

                        "CO_IsWorkmanReinstated BIT NULL",
                        "CO_ReinstatedSubjectToWP NVARCHAR(100) NULL",
                        "CO_Reinstatement_StayGranted BIT NULL",
                        "CO_Reinstatement_StayApprovalNo NVARCHAR(100) NULL",
                        "CO_Reinstatement_StayNature NVARCHAR(100) NULL",
                        "CO_Reinstatement_StayDate DATE NULL",
                        "CO_Reinstatement_StayOrderPath NVARCHAR(MAX) NULL",
                        "CO_Reinstatement_StayRemark NVARCHAR(MAX) NULL",
                        "CO_ReinstatementApprovalIssued BIT NULL",
                        "CO_ReinstatementApprovalDate DATE NULL",
                        "CO_Reinstatement_ApprovalNo NVARCHAR(100) NULL",
                        "CO_Reinstatement_ApprovalCopyPath NVARCHAR(MAX) NULL",

                        "CO_WA_CaseStatus_Option NVARCHAR(100) NULL",
                        "CO_WA_CaseNumber NVARCHAR(100) NULL",
                        "CO_WA_Year INT NULL",
                        "CO_WA_HighCourtBench NVARCHAR(100) NULL",
                        "CO_WA_EntrustmentNo NVARCHAR(100) NULL",
                        "CO_WA_EntrustmentDate DATE NULL",
                        "CO_WA_AdvocateName NVARCHAR(255) NULL",
                        "CO_WA_StayGranted BIT NULL",
                        "CO_WA_StayApprovalNo NVARCHAR(100) NULL",
                        "CO_WA_StayNature NVARCHAR(255) NULL",
                        "CO_WA_StayDate DATE NULL",
                        "CO_WA_StayOrderPath NVARCHAR(MAX) NULL",
                        "CO_WA_StayRemark NVARCHAR(MAX) NULL",
                        "CO_WA_Status NVARCHAR(100) NULL",
                        "CO_WA_Outcome NVARCHAR(100) NULL",
                        "CO_WA_OutcomeRemark NVARCHAR(MAX) NULL",
                        "CO_WA_OutcomeOutwardNo NVARCHAR(100) NULL",
                        "CO_WA_OutcomeOutwardDate DATE NULL",
                        "CO_WA_ActionTaken NVARCHAR(100) NULL",

                        "CO_FurtherAppeal_Status_Option NVARCHAR(100) NULL",
                        "CO_FurtherAppeal_CaseNumber NVARCHAR(100) NULL",
                        "CO_FurtherAppeal_Year INT NULL",
                        "CO_FurtherAppeal_EntrustmentNo NVARCHAR(100) NULL",
                        "CO_FurtherAppeal_EntrustmentDate DATE NULL",
                        "CO_FurtherAppeal_AdvocateName NVARCHAR(255) NULL",
                        "CO_FurtherAppeal_CaseStatus NVARCHAR(100) NULL",
                        "CO_FurtherAppeal_DisposalOutwardNo NVARCHAR(100) NULL",
                        "CO_FurtherAppeal_DisposalDate DATE NULL",

                        "CO_Disposal_Nature NVARCHAR(100) NULL",
                        "CO_Disposal_CommSentToDivision BIT NULL",
                        "CO_Disposal_OutwardNo NVARCHAR(100) NULL",
                        "CO_Disposal_Date DATE NULL",
                        "CO_Disposal_Decision NVARCHAR(100) NULL",
                        "CO_Disposal_ApprovalOutwardNo NVARCHAR(100) NULL",
                        "CO_Disposal_ApprovalDate DATE NULL"
                    };

                    foreach (var colDef in coColumns)
                    {
                        var colName = colDef.Split(' ')[0];
                        db.EnsureColumn("LABOUR_ARISING_APPLICATIONS", colName, colDef.Substring(colName.Length).Trim());
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for LABOUR_ARISING_APPLICATIONS: {ex.Message}");
            }
        }
        public static void EnsureServiceMattersTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LABOUR_SERVICE_MATTERS'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE LABOUR_SERVICE_MATTERS (
                            ServiceID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NULL,
                            DivisionID INT NOT NULL,
                            WPNumber NVARCHAR(100) NULL,
                            PetitionerName NVARCHAR(200) NULL,
                            CaseNature NVARCHAR(100) NULL,
                            Prayer NVARCHAR(MAX) NULL,
                            PetitionCopyPath NVARCHAR(500) NULL,
                            StayGranted BIT NULL,
                            StayVacateFiled BIT NULL,
                            StayCompliance BIT NULL,
                            ApprovalOutwardNo NVARCHAR(100) NULL,
                            ApprovalDate DATE NULL,
                            ApprovalCopyPath NVARCHAR(500) NULL,
                            Status NVARCHAR(50) NULL,
                            DisposalDate DATE NULL,
                            ActionTaken NVARCHAR(100) NULL,
                            ApprovalSentDetails NVARCHAR(MAX) NULL,
                            OutwardNo NVARCHAR(100) NULL,
                            OutwardDate DATE NULL,
                            EntrustmentNo NVARCHAR(100) NULL,
                            EntrustmentDate DATE NULL,
                            
                            -- Appeal details (Division Bench / WA)
                            AppealFiledBefore NVARCHAR(100) NULL,
                            AppealType NVARCHAR(50) NULL,
                            AppealEntrustmentDate DATE NULL,
                            AppealAdvocate NVARCHAR(200) NULL,
                            AppealStatus NVARCHAR(50) NULL,
                            
                            WA_CaseNumber NVARCHAR(100) NULL,
                            WA_Year INT NULL,
                            WA_HighCourtBench NVARCHAR(100) NULL,
                            WA_EntrustmentNo NVARCHAR(100) NULL,
                            WA_EntrustmentDate DATE NULL,
                            WA_AdvocateName NVARCHAR(200) NULL,
                            WA_StayGranted BIT NULL,
                            WA_StayApprovalNo NVARCHAR(100) NULL,
                            WA_StayNature NVARCHAR(100) NULL,
                            WA_StayDate DATE NULL,
                            WA_StayOrderPath NVARCHAR(500) NULL,
                            WA_StayRemark NVARCHAR(MAX) NULL,
                            WA_Status NVARCHAR(50) NULL,
                            WA_Outcome NVARCHAR(50) NULL,
                            WA_OutcomeRemark NVARCHAR(MAX) NULL,
                            WA_OutcomeOutwardNo NVARCHAR(100) NULL,
                            WA_OutcomeOutwardDate DATE NULL,
                            WA_ActionTaken NVARCHAR(50) NULL,

                            -- Supreme Court Fields
                            SC_Pending BIT NULL DEFAULT 0,
                            SC_DiaryNumber NVARCHAR(50) NULL,
                            SC_Year INT NULL,
                            SC_Number NVARCHAR(50) NULL,
                            SC_SLPYear INT NULL,
                            SC_FiledBy NVARCHAR(50) NULL,
                            SC_EntrustmentNo NVARCHAR(50) NULL,
                            SC_EntrustmentDate DATE NULL,
                            SC_Advocate NVARCHAR(200) NULL,
                            SC_Status NVARCHAR(50) NULL,
                            SC_Outcome NVARCHAR(100) NULL,
                            SC_ActionTaken NVARCHAR(100) NULL,
                            SC_ClosureNo NVARCHAR(50) NULL,
                            SC_ClosureDate DATE NULL,

                            CreatedDate DATETIME DEFAULT GETDATE(),
                            CreatedBy INT NULL,
                            ModifiedDate DATETIME NULL,
                            ModifiedBy INT NULL,
                            WPYear INT NULL,
                            CONSTRAINT FK_ServiceMatter_Case FOREIGN KEY (CaseID) REFERENCES LABOUR_CASES(CaseID) ON DELETE CASCADE
                        );
                        CREATE NONCLUSTERED INDEX IX_ServiceMatter_CaseID ON LABOUR_SERVICE_MATTERS(CaseID);
                        CREATE NONCLUSTERED INDEX IX_ServiceMatter_DivisionID ON LABOUR_SERVICE_MATTERS(DivisionID);
                    ";
                    db.ExecuteNonQuery(create);
                    System.Console.WriteLine("Migration: LABOUR_SERVICE_MATTERS table created.");
                }

                // Add missing columns if they don't exist
                var columns = new[]
                {
                    "WPYear INT NULL",
                    "IsEmployee BIT NULL DEFAULT 0",
                    "CNRNumber NVARCHAR(16) NULL",
                    "EstCode NVARCHAR(50) NULL",
                    "CaseTypeCode NVARCHAR(50) NULL",
                    "NextHearingDate DATE NULL",
                    "Stage NVARCHAR(150) NULL",
                    "CourtHall NVARCHAR(150) NULL"
                };

                foreach (var colDef in columns)
                {
                    var colName = colDef.Split(' ')[0];
                    string checkCol = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_SERVICE_MATTERS' AND COLUMN_NAME = '{colName}'";
                    int colCount = (int)(db.ExecuteScalar(checkCol) ?? 0);
                    if (colCount == 0)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE LABOUR_SERVICE_MATTERS ADD {colDef}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for LABOUR_SERVICE_MATTERS: {ex.Message}");
            }
        }
        public static void EnsureMvcOppositeVehiclesTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_OPPOSITE_VEHICLES'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE MVC_CASE_OPPOSITE_VEHICLES (
                            OppositeVehicleID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            VehicleNo NVARCHAR(50) NOT NULL,
                            CONSTRAINT FK_MVC_OppositeVehicles_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        );
                        CREATE NONCLUSTERED INDEX IX_MVC_OppositeVehicles_CaseID ON MVC_CASE_OPPOSITE_VEHICLES(CaseID);
                    ";
                    db.ExecuteNonQuery(create);
                    System.Console.WriteLine("Migration: MVC_CASE_OPPOSITE_VEHICLES table created.");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for MVC_CASE_OPPOSITE_VEHICLES: {ex.Message}");
            }
        }

        public static void EnsureOtherCasesTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASES'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE OTHER_CASES (
                            CaseID INT IDENTITY(1,1) PRIMARY KEY,
                            DivisionID INT NOT NULL,
                            CaseType NVARCHAR(50) NOT NULL, -- OS, PSC, CC, ECA, Consumer, LAC
                            LitigantType NVARCHAR(50) NOT NULL, -- Corporation, Claimant
                            IsPendingForFiling BIT DEFAULT 0,
                            CaseNumber NVARCHAR(100) NULL,
                            CaseYear INT NULL,
                            Court NVARCHAR(200) NULL,
                            CaseNature NVARCHAR(100) NULL,
                            ClaimDetails NVARCHAR(MAX) NULL,
                            RespondentName NVARCHAR(500) NULL,
                            Remark NVARCHAR(MAX) NULL,
                            EntrustmentNumber NVARCHAR(100) NULL,
                            EntrustmentDate DATE NULL,
                            AdvocateName NVARCHAR(200) NULL,
                            EvidenceFiled NVARCHAR(50) NULL, -- Yes, No
                            CaseStatus NVARCHAR(100) NULL,
                            NextDateOfHearing DATE NULL,
                            InterimOrder NVARCHAR(50) NULL, -- Yes, No
                            InterimOrderFilePath NVARCHAR(MAX) NULL,
                            AwardDetails NVARCHAR(MAX) NULL,
                            Result NVARCHAR(100) NULL, -- DISPOSED, SENT TO CENTRAL OFFICE, CLOSED AT DIVISION LEVEL
                            ClosureDate DATE NULL,
                            ClosureRemark NVARCHAR(MAX) NULL,
                            OutwardNumber NVARCHAR(100) NULL,
                            OutwardDate DATE NULL,
                            CreatedBy INT NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            ModifiedBy INT NULL,
                            ModifiedDate DATETIME NULL,
                            CONSTRAINT FK_OtherCases_Division FOREIGN KEY (DivisionID) REFERENCES DIVISION_MASTER(DivisionID)
                        );
                        CREATE NONCLUSTERED INDEX IX_OtherCases_DivisionID ON OTHER_CASES(DivisionID);
                        CREATE NONCLUSTERED INDEX IX_OtherCases_CaseType ON OTHER_CASES(CaseType);
                    ";
                    db.ExecuteNonQuery(create);
                    System.Console.WriteLine("Migration: OTHER_CASES table created.");
                }
                else
                {
                    // Ensure ClaimDetails if table exists
                    db.EnsureColumn("OTHER_CASES", "ClaimDetails", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "RespondentName", "NVARCHAR(500) NULL");
                    db.EnsureColumn("OTHER_CASES", "Remark", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "DisposalStatus", "NVARCHAR(100) NULL");
                    
                    // CC Details
                    db.EnsureColumn("OTHER_CASES", "CopyDisposedOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CopyAppliedOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CopyReadyOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CopyDeliveredOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CopyReceivedAtDivision", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CertifiedCopyRemarks", "NVARCHAR(MAX) NULL");
                    
                    // Opinions & Final Status
                    db.EnsureColumn("OTHER_CASES", "AdvocateOpinion", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "LawOfficerOpinion", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "DCFinalDecision", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "FinalForwardingStatus", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "FinalJudgmentFilePath", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "ClosureNumber", "NVARCHAR(50) NULL");
                    db.EnsureColumn("OTHER_CASES", "PetitionerName", "NVARCHAR(500) NULL");
                    db.EnsureColumn("OTHER_CASES", "PetitionerRelationship", "NVARCHAR(500) NULL");
                    db.EnsureColumn("OTHER_CASES", "ClaimType", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "DateOfClaimPetition", "DATETIME NULL");
                    db.EnsureColumn("OTHER_CASES", "CaseStage", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "DocumentSent", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "ObjectionVerified", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "IsObjectionFiled", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "ObjectionPending", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "ObjectionRemarks", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "ObjectionOutwardNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "ObjectionFiledDate", "DATETIME NULL");
                    db.EnsureColumn("OTHER_CASES", "InitialOutwardNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "InitialOutwardDate", "DATETIME NULL");
                    db.EnsureColumn("OTHER_CASES", "VehicleNumber", "NVARCHAR(50) NULL");
                    db.EnsureColumn("OTHER_CASES", "DateOfAccident", "DATETIME NULL");
                    db.EnsureColumn("OTHER_CASES", "ClaimAmount", "DECIMAL(18,2) NULL");
                    db.EnsureColumn("OTHER_CASES", "IsVehicleInvolved", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "VehicleType", "NVARCHAR(50) NULL");
                    db.EnsureColumn("OTHER_CASES", "IsCorporationEmployee", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "MannerOfIncident", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "ClaimRemarks", "NVARCHAR(MAX) NULL");

                    // Appeal Columns
                    db.EnsureColumn("OTHER_CASES", "AppealNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealYear", "INT NULL");
                    db.EnsureColumn("OTHER_CASES", "ArisingOutOSNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "OSYear", "INT NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealEntrustmentNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealEntrustmentDate", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "CourtAppellateAuth", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealAdvocateName", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealComplianceAmount", "DECIMAL(18,2) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealChequeNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealChequeDate", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealAwardDetails", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealCaseDisposedOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealCopyAppliedOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealCopyReadyOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealCopyDeliveredOn", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealCopyReceivedAtDivision", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealDelayRemarks", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealAdvocateOpinion", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealALOOpinion", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealDCOpinion", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealForwardingStatus", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealOutwardNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealOutwardDate", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealClosureNumber", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealClosureDate", "DATE NULL");
                    db.EnsureColumn("OTHER_CASES", "AppealJudgmentCopyPath", "NVARCHAR(MAX) NULL");

                    // e-Courts Gateway Integration Columns
                    db.EnsureColumn("OTHER_CASES", "CNRNumber", "NVARCHAR(16) NULL");
                    db.EnsureColumn("OTHER_CASES", "EstCode", "NVARCHAR(50) NULL");
                    db.EnsureColumn("OTHER_CASES", "CaseTypeCode", "NVARCHAR(20) NULL");
                    db.EnsureColumn("OTHER_CASES", "OtherCourtDetails", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "LastNapixSyncAt", "DATETIME2 NULL");
                    db.EnsureColumn("OTHER_CASES", "LastNapixSyncStatus", "NVARCHAR(100) NULL");
                    db.EnsureColumn("OTHER_CASES", "LastNapixSyncError", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("OTHER_CASES", "NapixSyncAttemptCount", "INT NOT NULL DEFAULT 0");
                    db.EnsureColumn("OTHER_CASES", "NapixDataHash", "NVARCHAR(64) NULL");
                    db.EnsureColumn("OTHER_CASES", "PendDispStatus", "NVARCHAR(10) NULL");
                    db.EnsureColumn("OTHER_CASES", "EstName", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "ECourtsStage", "NVARCHAR(200) NULL");
                    db.EnsureColumn("OTHER_CASES", "ECourtsCourtNo", "NVARCHAR(50) NULL");
                    db.EnsureColumn("OTHER_CASES", "ECourtsJudge", "NVARCHAR(200) NULL");
                }

                db.EnsureColumn("OTHER_CASES", "OutwardNumber", "NVARCHAR(100) NULL");
                db.EnsureColumn("OTHER_CASES", "OutwardDate", "DATE NULL");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for OTHER_CASES: {ex.Message}");
            }
        }

        public static void EnsureOtherCaseDocsTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASE_DOCUMENTS'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE OTHER_CASE_DOCUMENTS (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            DocName NVARCHAR(500) NULL,
                            PageCount INT NULL,
                            CONSTRAINT FK_OtherCaseDocs_Case FOREIGN KEY (CaseID) REFERENCES OTHER_CASES(CaseID) ON DELETE CASCADE
                        );");
                    System.Console.WriteLine("Migration: OTHER_CASE_DOCUMENTS table created.");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for OTHER_CASE_DOCUMENTS: {ex.Message}");
            }
        }

        public static void EnsureOtherCasePetitionersTable(DBHelper db)
        {
            try
            {
                string checkPetTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASE_PETITIONERS'";
                int count = (int)(db.ExecuteScalar(checkPetTable) ?? 0);
                if (count == 0)
                {
                    db.ExecuteNonQuery(@"CREATE TABLE OTHER_CASE_PETITIONERS (
                        PetitionerID INT PRIMARY KEY IDENTITY(1,1),
                        CaseID INT NOT NULL,
                        PetitionerName NVARCHAR(250),
                        PetitionerRemark NVARCHAR(MAX),
                        FOREIGN KEY (CaseID) REFERENCES OTHER_CASES(CaseID) ON DELETE CASCADE
                    )");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for OTHER_CASES: {ex.Message}");
            }
        }

        public static void EnsureOtherCaseRespondentsTable(DBHelper db)
        {
            try
            {
                string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASE_RESPONDENTS'";
                int count = (int)(db.ExecuteScalar(checkTable) ?? 0);
                if (count == 0)
                {
                    string create = @"
                        CREATE TABLE OTHER_CASE_RESPONDENTS (
                            RespondentID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            RespondentName NVARCHAR(500) NOT NULL,
                            RespondentRemark NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_OtherCaseRespondents_Case FOREIGN KEY (CaseID) REFERENCES OTHER_CASES(CaseID) ON DELETE CASCADE
                        );
                        CREATE NONCLUSTERED INDEX IX_OtherCaseRespondents_CaseID ON OTHER_CASE_RESPONDENTS(CaseID);
                    ";
                    db.ExecuteNonQuery(create);
                    System.Console.WriteLine("Migration: OTHER_CASE_RESPONDENTS table created.");
                }
                else
                {
                    db.EnsureColumn("OTHER_CASE_RESPONDENTS", "RespondentRemark", "NVARCHAR(MAX) NULL");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for OTHER_CASE_RESPONDENTS: {ex.Message}");
            }
        }

        public static void EnsureOtherCaseEvidenceTables(DBHelper db)
        {
            try
            {
                // Respondent Evidence table
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASE_EVIDENCE_RESPONDENT'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE OTHER_CASE_EVIDENCE_RESPONDENT (
                            EvidenceID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            Name NVARCHAR(500) NOT NULL,
                            Remark NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_OtherEvidenceResp_Case FOREIGN KEY (CaseID) REFERENCES OTHER_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // Corporation Evidence table
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OTHER_CASE_EVIDENCE_CORP'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE OTHER_CASE_EVIDENCE_CORP (
                            EvidenceID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            Name NVARCHAR(500) NOT NULL,
                            Designation NVARCHAR(500) NULL,
                            CONSTRAINT FK_OtherEvidenceCorp_Case FOREIGN KEY (CaseID) REFERENCES OTHER_CASES(CaseID) ON DELETE CASCADE
                        )");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for OTHER_CASE_EVIDENCE: {ex.Message}");
            }
        }

        public static void EnsureMvcRegistrationTables(DBHelper db)
        {
            try
            {
                // 1. MVC_CASE_PETITIONERS
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_PETITIONERS'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_PETITIONERS (
                            PetitionerID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            PetitionerName NVARCHAR(200) NOT NULL,
                            Relationship NVARCHAR(50) NULL,
                            CONSTRAINT FK_MVC_Petitioners_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // 2. MVC_CASE_RESPONDENTS
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_RESPONDENTS'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_RESPONDENTS (
                            RespondentID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            RespondentName NVARCHAR(255) NOT NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_MVC_Respondents_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // 3. MVC_CASE_CONNECTED
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_CONNECTED'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_CONNECTED (
                            ConnID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            ConnectedMVCNo NVARCHAR(50) NOT NULL,
                            ConnectedYear INT NULL,
                            Remarks NVARCHAR(500) NULL,
                            CONSTRAINT FK_MVC_Connected_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // 4. MVC_CASE_ADVERSE_DOCS
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_DOCS'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_ADVERSE_DOCS (
                            DocID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            DocName NVARCHAR(MAX) NOT NULL,
                            PageCount INT NULL,
                            CONSTRAINT FK_MVC_AdverseDocs_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // 5. MVC_CASE_ADVERSE_PW
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_PW'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_ADVERSE_PW (
                            PWID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            PWName NVARCHAR(255) NOT NULL,
                            Type NVARCHAR(50) NULL,
                            Designation NVARCHAR(100) NULL,
                            Remark NVARCHAR(MAX) NULL,
                            IsTreated BIT NULL,
                            CONSTRAINT FK_MVC_AdversePW_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }
                else
                {
                    db.EnsureColumn("MVC_CASE_ADVERSE_PW", "Type", "NVARCHAR(50) NULL");
                    db.EnsureColumn("MVC_CASE_ADVERSE_PW", "Designation", "NVARCHAR(100) NULL");
                    db.EnsureColumn("MVC_CASE_ADVERSE_PW", "Remark", "NVARCHAR(MAX) NULL");
                    db.EnsureColumn("MVC_CASE_ADVERSE_PW", "IsTreated", "BIT NULL");
                }

                // 6. MVC_CASE_ADVERSE_RW
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_RW'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_ADVERSE_RW (
                            RWID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            RWName NVARCHAR(255) NOT NULL,
                            CONSTRAINT FK_MVC_AdverseRW_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }

                // 7. MVC_CASE_ADVERSE_CONNECTED
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_CONNECTED'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_CASE_ADVERSE_CONNECTED (
                            ConnID INT IDENTITY(1,1) PRIMARY KEY,
                            CaseID INT NOT NULL,
                            ConnectedMVCNo NVARCHAR(50) NOT NULL,
                            ConnectedYear INT NULL,
                            Remarks NVARCHAR(500) NULL,
                            CONSTRAINT FK_MVC_AdverseConnected_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
                        )");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for MVC Registration Tables: {ex.Message}");
            }
        }

        public static void EnsureRemindBackRegistrationTables(DBHelper db)
        {
            try
            {
                // 1. MVC_REMIND_BACK_RESPONDENTS
                if ((int)(db.ExecuteScalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_REMIND_BACK_RESPONDENTS'") ?? 0) == 0)
                {
                    db.ExecuteNonQuery(@"
                        CREATE TABLE MVC_REMIND_BACK_RESPONDENTS (
                            RespondentID INT IDENTITY(1,1) PRIMARY KEY,
                            RemindBackID INT NOT NULL,
                            RespondentName NVARCHAR(255) NOT NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            CONSTRAINT FK_MVC_RemindBack_Respondents FOREIGN KEY (RemindBackID) REFERENCES MVC_REMIND_BACK_CASES(RemindBackID) ON DELETE CASCADE
                        );
                        CREATE NONCLUSTERED INDEX IX_MVC_RemindBack_Respondents_RBID ON MVC_REMIND_BACK_RESPONDENTS(RemindBackID);");
                }

                // 2. Ensure MVC_REMIND_BACK_ADVERSE_PW has Type, Designation, Remark, IsTreated
                db.EnsureColumn("MVC_REMIND_BACK_ADVERSE_PW", "Type", "NVARCHAR(50) NULL");
                db.EnsureColumn("MVC_REMIND_BACK_ADVERSE_PW", "Designation", "NVARCHAR(100) NULL");
                db.EnsureColumn("MVC_REMIND_BACK_ADVERSE_PW", "Remark", "NVARCHAR(MAX) NULL");
                db.EnsureColumn("MVC_REMIND_BACK_ADVERSE_PW", "IsTreated", "BIT NULL");

                // 3. Ensure MVC_REMIND_BACK_ADVERSE_RW has Designation
                db.EnsureColumn("MVC_REMIND_BACK_ADVERSE_RW", "Designation", "NVARCHAR(100) NULL");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for Remind Back Registration Tables: {ex.Message}");
            }
        }

        public static void EnsureSirsiMactCourtUpdate(DBHelper db)
        {
            try
            {
                // Update MACT_MASTER entry for Sirsi MACT Court
                string findMactSql = "SELECT MACTID FROM MACT_MASTER WHERE MACTName LIKE '%Sirsi%' OR MACTName LIKE '%SIRSI%'";
                var existingId = db.ExecuteScalar(findMactSql);
                int mactId = 0;

                if (existingId != null && existingId != DBNull.Value)
                {
                    mactId = Convert.ToInt32(existingId);
                    db.ExecuteNonQuery("UPDATE MACT_MASTER SET MACTName = 'SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI' WHERE MACTID = @mactId",
                        new[] { new SqlParameter("@mactId", mactId) });
                }
                else
                {
                    mactId = Convert.ToInt32(db.ExecuteScalar(@"
                        INSERT INTO MACT_MASTER (MACTCode, MACTName, Location, IsActive) 
                        VALUES ('KAUKA2', 'SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI', 'SIRSI', 1); 
                        SELECT CAST(SCOPE_IDENTITY() as int);"));
                }

                // Update MVC Case 466/2017 to point to this MACT Court & EstCode 'KAUKA2'
                db.ExecuteNonQuery(@"
                    UPDATE MVC_CASES 
                    SET MACTID = @mactId, 
                        EstCode = 'KAUKA2' 
                    WHERE MVCNo = '466' AND MVCYear = 2017",
                    new[] { new SqlParameter("@mactId", mactId) });

                // Clean up stray bracket character in APPEAL_DETAILS table
                db.ExecuteNonQuery(@"
                    UPDATE APPEAL_DETAILS 
                    SET ClaimantMFADecision = NULL 
                    WHERE ClaimantMFADecision = '}' OR ClaimantMFADecision = '{'");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for Sirsi MACT Update: {ex.Message}");
            }
        }

        public static void EnsureMactEstablishmentMapping(DBHelper db)
        {
            try
            {
                // 1. Ensure MACTCode column exists in MACT_MASTER
                string checkCol = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MACT_MASTER' AND COLUMN_NAME = 'MACTCode'";
                if ((int)(db.ExecuteScalar(checkCol) ?? 0) == 0)
                {
                    db.ExecuteNonQuery("ALTER TABLE MACT_MASTER ADD MACTCode NVARCHAR(50) NULL");
                }

                // 2. Define standard establishment mappings
                var estList = new[]
                {
                    new { Code = "KADW01", Name = "Principal District & Sessions Court, Dharwad [KADW01]", Location = "Dharwad" },
                    new { Code = "KADW02", Name = "Addl Senior Civil Judge & JMFC, Hubballi [KADW02]", Location = "Hubballi" },
                    new { Code = "KABG01", Name = "Principal District & Sessions Court, Belagavi [KABG01]", Location = "Belagavi" },
                    new { Code = "KAUK01", Name = "Principal District & Sessions Court, Karwar [KAUK01]", Location = "Karwar" },
                    new { Code = "KAUKA2", Name = "SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI", Location = "SIRSI" },
                    new { Code = "KAGD01", Name = "Principal District & Sessions Court, Gadag [KAGD01]", Location = "Gadag" },
                    new { Code = "KAHA01", Name = "Principal District & Sessions Court, Haveri [KAHA01]", Location = "Haveri" },
                    new { Code = "KABJ01", Name = "Principal District & Sessions Court, Vijayapura [KABJ01]", Location = "Vijayapura" },
                    new { Code = "KABK01", Name = "Principal District & Sessions Court, Bagalkot [KABK01]", Location = "Bagalkot" },
                    new { Code = "KAHC01", Name = "High Court of Karnataka, Principal Bench Bengaluru [KAHC01]", Location = "Bengaluru" },
                    new { Code = "KAHC02", Name = "High Court of Karnataka, Dharwad Bench [KAHC02]", Location = "Dharwad" },
                    new { Code = "KAHC03", Name = "High Court of Karnataka, Kalaburagi Bench [KAHC03]", Location = "Kalaburagi" }
                };

                foreach (var est in estList)
                {
                    string checkSql = "SELECT MACTID FROM MACT_MASTER WHERE MACTCode = @Code OR MACTName LIKE @NameSearch";
                    var mactIdObj = db.ExecuteScalar(checkSql, new[] {
                        new SqlParameter("@Code", est.Code),
                        new SqlParameter("@NameSearch", "%" + est.Location + "%")
                    });

                    if (mactIdObj != null && mactIdObj != DBNull.Value)
                    {
                        int mactId = Convert.ToInt32(mactIdObj);
                        db.ExecuteNonQuery("UPDATE MACT_MASTER SET MACTCode = @Code, MACTName = @Name, Location = @Loc WHERE MACTID = @Id", new[] {
                            new SqlParameter("@Code", est.Code),
                            new SqlParameter("@Name", est.Name),
                            new SqlParameter("@Loc", est.Location),
                            new SqlParameter("@Id", mactId)
                        });
                    }
                    else
                    {
                        db.ExecuteNonQuery("INSERT INTO MACT_MASTER (MACTCode, MACTName, Location, IsActive) VALUES (@Code, @Name, @Loc, 1)", new[] {
                            new SqlParameter("@Code", est.Code),
                            new SqlParameter("@Name", est.Name),
                            new SqlParameter("@Loc", est.Location)
                        });
                    }
                }

                // 3. Auto-populate missing EstCode in MVC_CASES for 16-digit CNR Number cases
                db.ExecuteNonQuery(@"
                    UPDATE MVC_CASES
                    SET EstCode = UPPER(SUBSTRING(CNRNumber, 1, 6))
                    WHERE (EstCode IS NULL OR EstCode = '')
                      AND CNRNumber IS NOT NULL
                      AND LEN(LTRIM(RTRIM(CNRNumber))) = 16");

                // 4. Migrate MVC_CASES.MACTID where EstCode matches MACT_MASTER.MACTCode
                db.ExecuteNonQuery(@"
                    UPDATE c
                    SET c.MACTID = m.MACTID
                    FROM MVC_CASES c
                    INNER JOIN MACT_MASTER m ON UPPER(c.EstCode) = UPPER(m.MACTCode)
                    WHERE c.EstCode IS NOT NULL AND c.EstCode <> '' AND c.MACTID <> m.MACTID");

                // 5. Correct case 852 / 2025 (Hubballi Rural Division) to point to Hubballi Court (KADW02)
                db.ExecuteNonQuery(@"
                    UPDATE c
                    SET c.EstCode = 'KADW02',
                        c.MACTID = (SELECT TOP 1 MACTID FROM MACT_MASTER WHERE MACTCode = 'KADW02')
                    FROM MVC_CASES c
                    WHERE (c.MVCNo = '852' OR c.MVCNo = '852/2025') AND c.MVCYear = 2025");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Migration Error for MACT Establishment Mapping: {ex.Message}");
            }
        }
    }
}
