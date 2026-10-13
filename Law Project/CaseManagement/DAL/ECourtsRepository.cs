using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MVCCaseManagement.DAL
{
    public class ECourtsRepository : IECourtsRepository
    {
        private readonly DBHelper _db;

        public ECourtsRepository(DBHelper db)
        {
            _db = db;
        }

        public async Task<int> SaveTrackedCaseAsync(TrackedCaseModel model)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                string sqlCheck = @"SELECT TOP 1 TrackedCaseID FROM TRACKED_CASES 
                                   WHERE (CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber != '')
                                      OR (EstCode = @EstCode AND CaseTypeCode = @CaseTypeCode AND RegNo = @RegNo AND RegYear = @RegYear)";
                int existingId = await conn.ExecuteScalarAsync<int>(sqlCheck, model, tran);

                if (existingId > 0)
                {
                    string sqlUpdate = @"
                        UPDATE TRACKED_CASES SET
                            CNRNumber = COALESCE(@CNRNumber, CNRNumber),
                            PetitionerName = COALESCE(@PetitionerName, PetitionerName),
                            RespondentName = COALESCE(@RespondentName, RespondentName),
                            CurrentStage = COALESCE(@CurrentStage, CurrentStage),
                            NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                            CourtNo = COALESCE(@CourtNo, CourtNo),
                            JudgeName = COALESCE(@JudgeName, JudgeName),
                            IsHighCourt = @IsHighCourt,
                            LastSyncedDate = GETDATE()
                        WHERE TrackedCaseID = @existingId";

                    await conn.ExecuteAsync(sqlUpdate, new {
                        model.CNRNumber,
                        model.PetitionerName,
                        model.RespondentName,
                        model.CurrentStage,
                        model.NextHearingDate,
                        model.CourtNo,
                        model.JudgeName,
                        model.IsHighCourt,
                        existingId
                    }, tran);

                    tran.Commit();
                    return existingId;
                }
                else
                {
                    string sqlInsert = @"
                        INSERT INTO TRACKED_CASES (
                            CNRNumber, EstCode, CaseTypeCode, RegNo, RegYear,
                            PetitionerName, RespondentName, CurrentStage, NextHearingDate,
                            CourtNo, JudgeName, IsHighCourt, LastSyncedDate, CreatedDate
                        ) VALUES (
                            @CNRNumber, @EstCode, @CaseTypeCode, @RegNo, @RegYear,
                            @PetitionerName, @RespondentName, @CurrentStage, @NextHearingDate,
                            @CourtNo, @JudgeName, @IsHighCourt, GETDATE(), GETDATE()
                        );
                        SELECT CAST(SCOPE_IDENTITY() as int);";

                    int newId = await conn.ExecuteScalarAsync<int>(sqlInsert, model, tran);
                    tran.Commit();
                    return newId;
                }
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public async Task<TrackedCaseModel?> GetTrackedCaseByCnrAsync(string cnrNumber)
        {
            using var conn = _db.GetConnection();
            string sql = "SELECT * FROM TRACKED_CASES WHERE CNRNumber = @cnrNumber";
            return await conn.QueryFirstOrDefaultAsync<TrackedCaseModel>(sql, new { cnrNumber });
        }

        public async Task<IEnumerable<TrackedCaseModel>> GetAllTrackedCasesAsync()
        {
            using var conn = _db.GetConnection();
            string sql = "SELECT * FROM TRACKED_CASES ORDER BY CreatedDate DESC";
            return await conn.QueryAsync<TrackedCaseModel>(sql);
        }

        public async Task<bool> UpdateCaseCnrLinkAsync(string table, string primaryKeyCol, int id, string cnrNumber)
        {
            // Allowed table whitelist to prevent SQL injection in table name
            var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MVC_CASES", "LABOUR_CASES", "LABOUR_SERVICE_MATTERS", "APPEAL_DETAILS"
            };

            if (!allowedTables.Contains(table))
            {
                throw new ArgumentException("Invalid table name specified for CNR linking.", nameof(table));
            }

            using var conn = _db.GetConnection();
            string sql = $"UPDATE [{table}] SET CNRNumber = @cnrNumber WHERE [{primaryKeyCol}] = @id";
            int rows = await conn.ExecuteAsync(sql, new { cnrNumber, id });
            return rows > 0;
        }

        public async Task<int> SaveCauselistAsync(CauselistHeaderModel header)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                string sqlCheck = @"
                    SELECT CauselistID FROM DAILY_CAUSELISTS 
                    WHERE EstCode = @EstCode AND CourtNo = @CourtNo 
                      AND CauselistDate = @CauselistDate AND CauselistType = @CauselistType";

                int causelistId = await conn.ExecuteScalarAsync<int>(sqlCheck, header, tran);

                if (causelistId > 0)
                {
                    // Delete existing items to replace with fresh list
                    await conn.ExecuteAsync("DELETE FROM CAUSELIST_ITEMS WHERE CauselistID = @causelistId", new { causelistId }, tran);
                    
                    string sqlUpdate = @"
                        UPDATE DAILY_CAUSELISTS SET
                            TotalCases = @TotalCases,
                            CorporationCasesCount = @CorporationCasesCount,
                            FetchedDate = GETDATE()
                        WHERE CauselistID = @causelistId";

                    await conn.ExecuteAsync(sqlUpdate, new { header.TotalCases, header.CorporationCasesCount, causelistId }, tran);
                }
                else
                {
                    string sqlInsert = @"
                        INSERT INTO DAILY_CAUSELISTS (
                            EstCode, CourtNo, CauselistDate, CauselistType, TotalCases, CorporationCasesCount, FetchedDate
                        ) VALUES (
                            @EstCode, @CourtNo, @CauselistDate, @CauselistType, @TotalCases, @CorporationCasesCount, GETDATE()
                        );
                        SELECT CAST(SCOPE_IDENTITY() as int);";

                    causelistId = await conn.ExecuteScalarAsync<int>(sqlInsert, header, tran);
                }

                if (header.Items != null && header.Items.Count > 0)
                {
                    string sqlItemInsert = @"
                        INSERT INTO CAUSELIST_ITEMS (
                            CauselistID, SrNo, CNRNumber, CaseNumber, PartyDetails, AdvocateDetails, Stage, IsCorporationCase, TrackedCaseID
                        ) VALUES (
                            @CauselistID, @SrNo, @CNRNumber, @CaseNumber, @PartyDetails, @AdvocateDetails, @Stage, @IsCorporationCase, @TrackedCaseID
                        );";

                    foreach (var item in header.Items)
                    {
                        item.CauselistID = causelistId;
                        await conn.ExecuteAsync(sqlItemInsert, item, tran);
                    }
                }

                tran.Commit();
                return causelistId;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public async Task<CauselistHeaderModel?> GetCauselistAsync(string estCode, string courtNo, DateTime date, string type = "civil")
        {
            using var conn = _db.GetConnection();
            string sqlHeader = @"
                SELECT * FROM DAILY_CAUSELISTS 
                WHERE EstCode = @estCode AND CourtNo = @courtNo 
                  AND CauselistDate = @date AND CauselistType = @type";

            var header = await conn.QueryFirstOrDefaultAsync<CauselistHeaderModel>(sqlHeader, new { estCode, courtNo, date, type });
            if (header != null)
            {
                string sqlItems = "SELECT * FROM CAUSELIST_ITEMS WHERE CauselistID = @CauselistID ORDER BY SrNo ASC";
                var items = await conn.QueryAsync<CauselistItemModel>(sqlItems, new { header.CauselistID });
                header.Items = new List<CauselistItemModel>(items);
            }

            return header;
        }

        public async Task<IEnumerable<MVCCaseManagement.Models.HighCourtCauseListItem>> GetHighCourtCauseListAsync()
        {
            using var conn = _db.GetConnection();
            string sql = @"
                SELECT 
                    a.AppealID,
                    a.CaseID,
                    'Corporation MFA' AS AppealType,
                    a.CorpMFANumber AS MFANumber,
                    a.CorpMFAYear AS MFAYear,
                    a.CorpMFACNRNumber AS CNRNumber,
                    ISNULL(NULLIF(a.HighCourtBench, ''), 'Dharwad') AS HighCourtBench,
                    NULLIF(a.CourtHall, '') AS CourtHall,
                    c.MVCNo AS ArisingMVCNo,
                    c.MVCYear AS ArisingMVCYear,
                    d.DivisionNameEnglish AS DivisionName,
                    m.MACTName,
                    a.CorpMFANextHearingDate AS NextHearingDate,
                    a.CorpMFAStage AS CaseStage,
                    ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant / Petitioner') AS PetitionerName,
                    ISNULL(NULLIF((SELECT TOP 1 RespondentName FROM MVC_CASE_RESPONDENTS WHERE CaseID = c.CaseID AND RespondentName IS NOT NULL AND RespondentName <> ''), ''), 'NWKRTC (Corporation)') AS RespondentName
                FROM APPEAL_DETAILS a
                INNER JOIN MVC_CASES c ON a.CaseID = c.CaseID
                LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE (a.CorpMFANumber IS NOT NULL AND a.CorpMFANumber <> '') OR (a.CorpMFACNRNumber IS NOT NULL AND a.CorpMFACNRNumber <> '')

                UNION ALL

                SELECT 
                    a.AppealID,
                    a.CaseID,
                    'Claimant MFA' AS AppealType,
                    a.ClaimantMFANumber AS MFANumber,
                    a.ClaimantMFAYear AS MFAYear,
                    a.ClaimantMFACNRNumber AS CNRNumber,
                    ISNULL(NULLIF(a.HighCourtBench, ''), 'Dharwad') AS HighCourtBench,
                    NULLIF(a.CourtHall, '') AS CourtHall,
                    ISNULL(a.ClaimantMVCNumber, c.MVCNo) AS ArisingMVCNo,
                    ISNULL(a.ClaimantMVCYear, c.MVCYear) AS ArisingMVCYear,
                    d.DivisionNameEnglish AS DivisionName,
                    m.MACTName,
                    a.ClaimantMFANextHearingDate AS NextHearingDate,
                    ISNULL(NULLIF(a.ClaimantMFAStage, ''), a.ClaimantMFAStatus) AS CaseStage,
                    ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant / Petitioner') AS PetitionerName,
                    ISNULL(NULLIF((SELECT TOP 1 RespondentName FROM MVC_CASE_RESPONDENTS WHERE CaseID = c.CaseID AND RespondentName IS NOT NULL AND RespondentName <> ''), ''), 'NWKRTC (Corporation)') AS RespondentName
                FROM APPEAL_DETAILS a
                LEFT JOIN MVC_CASES c ON a.CaseID = c.CaseID
                LEFT JOIN DIVISION_MASTER d ON ISNULL(a.ClaimantDivisionID, c.DivisionID) = d.DivisionID
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE (a.ClaimantMFANumber IS NOT NULL AND a.ClaimantMFANumber <> '') OR (a.ClaimantMFACNRNumber IS NOT NULL AND a.ClaimantMFACNRNumber <> '')
                ORDER BY NextHearingDate ASC, AppealID DESC";

            return await conn.QueryAsync<MVCCaseManagement.Models.HighCourtCauseListItem>(sql);
        }

        public async Task UpdateAppealLiveECourtsDataAsync(string? cnrNumber, string? courtHall, DateTime? nextHearingDate, string? stage, int? appealId = null)
        {
            using var conn = _db.GetConnection();

            if (appealId.HasValue && appealId.Value > 0)
            {
                string sqlById = @"
                    UPDATE APPEAL_DETAILS 
                    SET CourtHall = ISNULL(NULLIF(@courtHall, ''), CourtHall),
                        CorpMFANextHearingDate = ISNULL(@nextHearingDate, CorpMFANextHearingDate),
                        CorpMFAStage = ISNULL(NULLIF(@stage, ''), CorpMFAStage),
                        ClaimantMFANextHearingDate = ISNULL(@nextHearingDate, ClaimantMFANextHearingDate),
                        ClaimantMFAStage = ISNULL(NULLIF(@stage, ''), ClaimantMFAStage),
                        ClaimantMFAStatus = ISNULL(NULLIF(@stage, ''), ClaimantMFAStatus)
                    WHERE AppealID = @appealId";
                await conn.ExecuteAsync(sqlById, new { appealId = appealId.Value, courtHall, nextHearingDate, stage });
            }

            if (!string.IsNullOrWhiteSpace(cnrNumber))
            {
                string cleanCnr = cnrNumber.Trim();
                string sqlCorp = @"
                    UPDATE APPEAL_DETAILS 
                    SET CourtHall = ISNULL(NULLIF(@courtHall, ''), CourtHall),
                        CorpMFANextHearingDate = ISNULL(@nextHearingDate, CorpMFANextHearingDate),
                        CorpMFAStage = ISNULL(NULLIF(@stage, ''), CorpMFAStage)
                    WHERE CorpMFACNRNumber = @cleanCnr";
                await conn.ExecuteAsync(sqlCorp, new { cleanCnr, courtHall, nextHearingDate, stage });

                string sqlClaimant = @"
                    UPDATE APPEAL_DETAILS 
                    SET CourtHall = ISNULL(NULLIF(@courtHall, ''), CourtHall),
                        ClaimantMFANextHearingDate = ISNULL(@nextHearingDate, ClaimantMFANextHearingDate),
                        ClaimantMFAStage = ISNULL(NULLIF(@stage, ''), ClaimantMFAStage),
                        ClaimantMFAStatus = ISNULL(NULLIF(@stage, ''), ClaimantMFAStatus)
                    WHERE ClaimantMFACNRNumber = @cleanCnr";
                await conn.ExecuteAsync(sqlClaimant, new { cleanCnr, courtHall, nextHearingDate, stage });
            }
        }

        public async Task UpdateTrackedCaseStatusAsync(string cnrNumber, string? status, DateTime? nextHearingDate, string? stage)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return;
            using var conn = _db.GetConnection();
            string sql = @"
                UPDATE TRACKED_CASES SET
                    CurrentStage = COALESCE(@stage, CurrentStage),
                    NextHearingDate = COALESCE(@nextHearingDate, NextHearingDate),
                    LastSyncedDate = GETDATE()
                WHERE CNRNumber = @cnrNumber";
            await conn.ExecuteAsync(sql, new { cnrNumber, stage, nextHearingDate });
        }

        public async Task SyncLiveCaseDataAsync(string cnrNumber, DateTime? nextHearingDate, string? stage, string? courtHall, string? caseStatus, int? caseId = null, string? module = null, int? appealId = null, DateTime? decisionDate = null, string? estName = null)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return;
            string cleanCnr = cnrNumber.Trim();
            using var conn = _db.GetConnection();

            // 1. Update MVC_CASES
            if (string.IsNullOrEmpty(module) || module.Equals("MVC", StringComparison.OrdinalIgnoreCase))
            {
                string sqlMvc = @"
                    UPDATE MVC_CASES SET
                        CNRNumber = COALESCE(NULLIF(CNRNumber, ''), @cleanCnr),
                        NextHearingDate = COALESCE(@nextHearingDate, NextHearingDate),
                        CurrentStage = COALESCE(NULLIF(@stage, ''), CurrentStage),
                        ECourtsStage = COALESCE(NULLIF(@stage, ''), ECourtsStage),
                        CaseStatus = COALESCE(NULLIF(@caseStatus, ''), CaseStatus),
                        CourtHall = COALESCE(NULLIF(@courtHall, ''), CourtHall),
                        EstName = COALESCE(NULLIF(@estName, ''), EstName),
                        ClosureDate = COALESCE(ClosureDate, @decisionDate),
                        PendDispStatus = CASE WHEN @caseStatus = 'Disposed' OR @decisionDate IS NOT NULL THEN 'D' ELSE COALESCE(PendDispStatus, 'P') END
                    WHERE (CaseID = @caseId AND @caseId IS NOT NULL AND @caseId > 0)
                       OR (CNRNumber = @cleanCnr)";
                await conn.ExecuteAsync(sqlMvc, new { cleanCnr, nextHearingDate, stage, caseStatus, courtHall, caseId, decisionDate, estName });
            }

            // 2. Update APPEAL_DETAILS
            await UpdateAppealLiveECourtsDataAsync(cleanCnr, courtHall, nextHearingDate, stage, appealId);

            // 3. Update LABOUR_CASES
            if (string.IsNullOrEmpty(module) || module.Equals("Labour", StringComparison.OrdinalIgnoreCase))
            {
                string sqlLabour = @"
                    UPDATE LABOUR_CASES SET
                        CNRNumber = CASE WHEN @cleanCnr NOT LIKE 'KAHC%' THEN COALESCE(NULLIF(CNRNumber, ''), @cleanCnr) ELSE CNRNumber END,
                        CO_WP_Status = CASE WHEN CO_WP_CNRNumber = @cleanCnr THEN COALESCE(NULLIF(@caseStatus, ''), CO_WP_Status) ELSE CO_WP_Status END,
                        CO_WA_Status = CASE WHEN CO_WA_CNRNumber = @cleanCnr THEN COALESCE(NULLIF(@caseStatus, ''), CO_WA_Status) ELSE CO_WA_Status END,
                        CO_Claimant_CaseStatus = CASE WHEN CO_Claimant_CNRNumber = @cleanCnr THEN COALESCE(NULLIF(@caseStatus, ''), CO_Claimant_CaseStatus) ELSE CO_Claimant_CaseStatus END,
                        NextHearingDate = COALESCE(@nextHearingDate, NextHearingDate),
                        CurrentStage = COALESCE(NULLIF(@stage, ''), CurrentStage),
                        ECourtsStage = COALESCE(NULLIF(@stage, ''), ECourtsStage),
                        EstName = COALESCE(NULLIF(@estName, ''), EstName),
                        ECourtsCourtNo = COALESCE(NULLIF(@courtHall, ''), ECourtsCourtNo),
                        CaseStatus = COALESCE(NULLIF(@caseStatus, ''), CaseStatus),
                        DisposalDate = COALESCE(DisposalDate, @decisionDate),
                        PendDispStatus = CASE WHEN @caseStatus = 'Disposed' OR @decisionDate IS NOT NULL THEN 'D' ELSE COALESCE(PendDispStatus, 'P') END
                    WHERE (CaseID = @caseId AND @caseId IS NOT NULL AND @caseId > 0)
                       OR (CNRNumber = @cleanCnr)
                       OR (CO_WP_CNRNumber = @cleanCnr)
                       OR (CO_WA_CNRNumber = @cleanCnr)
                       OR (CO_Claimant_CNRNumber = @cleanCnr)";
                await conn.ExecuteAsync(sqlLabour, new { cleanCnr, nextHearingDate, stage, caseStatus, courtHall, caseId, decisionDate, estName });
            }

            // 4. Update TRACKED_CASES
            await UpdateTrackedCaseStatusAsync(cleanCnr, caseStatus, nextHearingDate, stage);
        }
    }
}
