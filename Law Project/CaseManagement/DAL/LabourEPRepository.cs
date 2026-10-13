using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class LabourEPRepository : ILabourEPRepository
    {
        private readonly DBHelper _db;

        public LabourEPRepository(DBHelper db)
        {
            _db = db;
        }

        public IEnumerable<LabourEPViewModel> GetAllEPs(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            var eps = new List<LabourEPViewModel>();
            int offset = (pageNumber - 1) * pageSize;

            string query = @"
                SELECT ep.*, mc.CaseNumber, mc.CaseYear 
                FROM LABOUR_EP_DETAILS ep
                JOIN LABOUR_CASES mc ON ep.CaseID = mc.CaseID
                WHERE 1=1";
            
            var parameters = new List<SqlParameter>();
            
            if (divisionId > 0 && divisionId != 5)
            {
                query += " AND mc.DivisionID = @DivisionID";
                parameters.Add(new SqlParameter("@DivisionID", divisionId));
            }

            // CO Filter: If CO and not searching, show only Sent to CO
            bool isCentralOffice = divisionId == 0 || divisionId == 5;
            if (isCentralOffice && string.IsNullOrEmpty(search))
            {
                if (status == "all" || status == "SentToCO" || string.IsNullOrEmpty(status))
                {
                    query += " AND mc.SentToCO = 1";
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query += " AND (ep.ArisingFromCaseNo LIKE @Search OR ep.EPStatus LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "SentToCO")
                    query += " AND mc.SentToCO = 1";
                else if (status == "Favor")
                    query += " AND mc.DisposalResult = 'Favor'";
                else if (status == "Against")
                    query += " AND mc.DisposalResult = 'Against'";
                else
                {
                    query += " AND ep.EPStatus = @Status";
                    parameters.Add(new SqlParameter("@Status", status));
                }
            }

            query += " ORDER BY ep.CreatedDate DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            parameters.Add(new SqlParameter("@Offset", offset));
            parameters.Add(new SqlParameter("@PageSize", pageSize));

            DataTable dt = _db.ExecuteQuery(query, parameters.ToArray());
            foreach (DataRow row in dt.Rows)
            {
                eps.Add(MapRowToViewModel(row));
            }
            return eps;
        }

        public int GetTotalEPCount(int divisionId = 0, string? search = null, string? status = null)
        {
            string query = @"
                SELECT COUNT(*) 
                FROM LABOUR_EP_DETAILS ep
                JOIN LABOUR_CASES mc ON ep.CaseID = mc.CaseID
                WHERE 1=1";
            
            var parameters = new List<SqlParameter>();
            if (divisionId > 0 && divisionId != 5)
            {
                query += " AND mc.DivisionID = @DivisionID";
                parameters.Add(new SqlParameter("@DivisionID", divisionId));
            }

            bool isCentralOffice = divisionId == 0 || divisionId == 5;
            if (isCentralOffice && string.IsNullOrEmpty(search))
            {
                if (status == "all" || status == "SentToCO" || string.IsNullOrEmpty(status))
                {
                    query += " AND mc.SentToCO = 1";
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query += " AND (ep.ArisingFromCaseNo LIKE @Search OR ep.EPStatus LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "SentToCO")
                    query += " AND mc.SentToCO = 1";
                else if (status == "Favor")
                    query += " AND mc.DisposalResult = 'Favor'";
                else if (status == "Against")
                    query += " AND mc.DisposalResult = 'Against'";
                else
                {
                    query += " AND ep.EPStatus = @Status";
                    parameters.Add(new SqlParameter("@Status", status));
                }
            }

            return Convert.ToInt32(_db.ExecuteScalar(query, parameters.ToArray()));
        }

        public LabourEPViewModel? GetEPById(int epId)
        {
            string query = @"
                SELECT ep.*, mc.CaseNumber, mc.CaseYear 
                FROM LABOUR_EP_DETAILS ep
                JOIN LABOUR_CASES mc ON ep.CaseID = mc.CaseID
                WHERE ep.EPID = @EPID";
            
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@EPID", epId) });
            if (dt.Rows.Count == 0) return null;
            return MapRowToViewModel(dt.Rows[0]);
        }

        public int SaveEP(LabourEPViewModel model)
        {
            string query = @"
                INSERT INTO LABOUR_EP_DETAILS (
                    CaseID, ArisingFromCaseNo, ArisingFromCaseYear, ArisingFromCourt,
                    OriginalCaseStatus, EPStatus, NextHearingDate, AsPerECourts, IsSentToAccounts, DateSentToAccounts,
                    AwardAmount, InterestRate, LiabilityPercentage, PetitionDate,
                    PettyBillDate, PettyBillAmount, ChequeNumber, ChequeDate, AmountPaid,
                    ComplianceStatus, DateOfCompliance, Remarks, CreatedBy, DivisionID,
                    EPNumber, EPYear, EPCourt, EP_EntrustmentNo, EP_EntrustmentDate, AdvocateID, AdvocateName,
                    CaseOutcome, DisposalDate, DisposalRemarks, ClosureDate, CNRNumber, EstCode, CaseTypeCode
                )
                OUTPUT INSERTED.EPID
                VALUES (
                    @CaseID, @CaseNo, @CaseYearStr, @Court,
                    @OriginalStatus, @EPStatus, @NextHearingDate, @AsPerECourts, @SentToAcc, @DateSent,
                    @AwardAmt, @Interest, @Liability, @PetitionDate,
                    @PettyDate, @PettyAmount, @ChequeNo, @ChequeDate, @AmountPaid,
                    @Compliance, @ComplianceDate, @Remarks, @CreatedBy, @DivisionID,
                    @EPNumber, @EPYear, @EPCourt, @EPEntrustNo, @EPEntrustDate, @AdvocateID, @AdvocateName,
                    @CaseOutcome, @DisposalDate, @DisposalRemarks, @ClosureDate, @CNRNumber, @EstCode, @CaseTypeCode
                )";

            var parameters = new[]
            {
                new SqlParameter("@CaseID", model.CaseID),
                new SqlParameter("@EPNumber", model.EPNumber ?? (object)DBNull.Value),
                new SqlParameter("@EPYear", model.EPYear ?? (object)DBNull.Value),
                new SqlParameter("@EPCourt", model.EPCourt ?? (object)DBNull.Value),
                new SqlParameter("@EPEntrustNo", model.EP_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@EPEntrustDate", model.EP_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CNRNumber", model.CNRNumber ?? (object)DBNull.Value),
                new SqlParameter("@EstCode", model.EstCode ?? (object)DBNull.Value),
                new SqlParameter("@CaseTypeCode", model.CaseTypeCode ?? (object)DBNull.Value),
                new SqlParameter("@CaseNo", model.ArisingFromCaseNo ?? (object)DBNull.Value),
                new SqlParameter("@CaseYearStr", model.ArisingFromCaseYear ?? (object)DBNull.Value),
                new SqlParameter("@Court", model.ArisingFromCourt ?? (object)DBNull.Value),
                new SqlParameter("@OriginalStatus", model.OriginalCaseStatus ?? (object)DBNull.Value),
                new SqlParameter("@EPStatus", model.EPStatus ?? (object)DBNull.Value),
                new SqlParameter("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value),
                new SqlParameter("@AsPerECourts", model.AsPerECourts ?? (object)DBNull.Value),
                new SqlParameter("@SentToAcc", model.IsSentToAccounts),
                new SqlParameter("@DateSent", model.DateSentToAccounts ?? (object)DBNull.Value),
                new SqlParameter("@PettyDate", model.PettyBillDate ?? (object)DBNull.Value),
                new SqlParameter("@PettyAmount", model.PettyBillAmount ?? (object)DBNull.Value),
                new SqlParameter("@ChequeNo", model.ChequeNumber ?? (object)DBNull.Value),
                new SqlParameter("@ChequeDate", model.ChequeDate ?? (object)DBNull.Value),
                new SqlParameter("@AmountPaid", model.AmountPaid ?? (object)DBNull.Value),
                new SqlParameter("@Compliance", model.ComplianceStatus ?? (object)DBNull.Value),
                new SqlParameter("@ComplianceDate", model.DateOfCompliance ?? (object)DBNull.Value),
                new SqlParameter("@Remarks", model.Remarks ?? (object)DBNull.Value),
                new SqlParameter("@AwardAmt", model.AwardAmount ?? (object)DBNull.Value),
                new SqlParameter("@Interest", model.InterestRate ?? (object)DBNull.Value),
                new SqlParameter("@Liability", model.LiabilityPercentage ?? (object)DBNull.Value),
                new SqlParameter("@PetitionDate", model.PetitionDate ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CaseOutcome", model.CaseOutcome ?? (object)DBNull.Value),
                new SqlParameter("@DisposalDate", model.DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@DisposalRemarks", model.DisposalRemarks ?? (object)DBNull.Value),
                new SqlParameter("@ClosureDate", model.ClosureDate ?? (object)DBNull.Value),
                new SqlParameter("@CreatedBy", 1),
                new SqlParameter("@DivisionID", model.DivisionID)
            };

            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                object? result = _db.ExecuteScalar(query, parameters, connection, transaction);
                int epId = result != null ? Convert.ToInt32(result) : 0;

                // Save payments if any
                if (epId > 0 && model.Payments != null && model.Payments.Count > 0)
                {
                    foreach (var p in model.Payments.Where(x => x.Amount > 0))
                    {
                        string insertPayment = @"
                            INSERT INTO LABOUR_EP_PAYMENTS (EPID, Amount, PaymentDate, ChequeNumber, ChequeDate, Remarks)
                            VALUES (@EPID, @Amount, @Date, @Cheque, @CDate, @Rem)";
                        _db.ExecuteNonQuery(insertPayment, new[] {
                            new SqlParameter("@EPID", epId),
                            new SqlParameter("@Amount", p.Amount),
                            new SqlParameter("@Date", p.PaymentDate),
                            new SqlParameter("@Cheque", p.ChequeNumber ?? (object)DBNull.Value),
                            new SqlParameter("@CDate", p.ChequeDate ?? (object)DBNull.Value),
                            new SqlParameter("@Rem", p.Remarks ?? (object)DBNull.Value)
                        }, connection, transaction);
                    }
                }

                if (epId > 0 && model.CaseID > 0)
                {
                    string syncQuery = @"
                        UPDATE LABOUR_CASES SET IsEPFiled = 1 WHERE CaseID = @CaseID";
                    _db.ExecuteNonQuery(syncQuery, new[] { new SqlParameter("@CaseID", model.CaseID) }, connection, transaction);
                }

                transaction.Commit();
                return epId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool UpdateEP(LabourEPViewModel model)
        {
            string query = @"
                UPDATE LABOUR_EP_DETAILS SET
                    CaseID = CASE WHEN @CaseID > 0 THEN @CaseID ELSE CaseID END,
                    ArisingFromCaseNo = @CaseNo, 
                    ArisingFromCaseYear = @CaseYearStr, 
                    ArisingFromCourt = @Court,
                    OriginalCaseStatus = @OriginalStatus, 
                    EPStatus = @EPStatus, 
                    NextHearingDate = @NextHearingDate,
                    AsPerECourts = @AsPerECourts,
                    IsSentToAccounts = @SentToAcc, 
                    DateSentToAccounts = @DateSent,
                    AwardAmount = @AwardAmt,
                    InterestRate = @Interest,
                    LiabilityPercentage = @Liability,
                    PetitionDate = @PetitionDate,
                    PettyBillDate = @PettyDate, 
                    PettyBillAmount = @PettyAmount, 
                    ChequeNumber = @ChequeNo, 
                    ChequeDate = @ChequeDate, 
                    AmountPaid = @AmountPaid,
                    ComplianceStatus = @Compliance, 
                    DateOfCompliance = @ComplianceDate, 
                    Remarks = @Remarks,
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE(),
                    EPNumber = @EPNumber,
                    EPYear = @EPYear,
                    EPCourt = @EPCourt,
                    EP_EntrustmentNo = @EPEntrustNo,
                    EP_EntrustmentDate = @EPEntrustDate,
                    AdvocateID = @AdvocateID,
                    AdvocateName = @AdvocateName,
                    CaseOutcome = @CaseOutcome,
                    DisposalDate = @DisposalDate,
                    DisposalRemarks = @DisposalRemarks,
                    ClosureDate = @ClosureDate,
                    CNRNumber = @CNRNumber,
                    EstCode = @EstCode,
                    CaseTypeCode = @CaseTypeCode
                WHERE EPID = @EPID";

            var parameters = new[]
            {
                new SqlParameter("@CaseID", model.CaseID),
                new SqlParameter("@EPNumber", model.EPNumber ?? (object)DBNull.Value),
                new SqlParameter("@EPYear", model.EPYear ?? (object)DBNull.Value),
                new SqlParameter("@EPCourt", model.EPCourt ?? (object)DBNull.Value),
                new SqlParameter("@EPEntrustNo", model.EP_EntrustmentNo ?? (object)DBNull.Value),
                new SqlParameter("@EPEntrustDate", model.EP_EntrustmentDate ?? (object)DBNull.Value),
                new SqlParameter("@CNRNumber", model.CNRNumber ?? (object)DBNull.Value),
                new SqlParameter("@EstCode", model.EstCode ?? (object)DBNull.Value),
                new SqlParameter("@CaseTypeCode", model.CaseTypeCode ?? (object)DBNull.Value),
                new SqlParameter("@CaseNo", model.ArisingFromCaseNo ?? (object)DBNull.Value),
                new SqlParameter("@CaseYearStr", model.ArisingFromCaseYear ?? (object)DBNull.Value),
                new SqlParameter("@Court", model.ArisingFromCourt ?? (object)DBNull.Value),
                new SqlParameter("@OriginalStatus", model.OriginalCaseStatus ?? (object)DBNull.Value),
                new SqlParameter("@EPStatus", model.EPStatus ?? (object)DBNull.Value),
                new SqlParameter("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value),
                new SqlParameter("@AsPerECourts", model.AsPerECourts ?? (object)DBNull.Value),
                new SqlParameter("@SentToAcc", model.IsSentToAccounts),
                new SqlParameter("@DateSent", model.DateSentToAccounts ?? (object)DBNull.Value),
                new SqlParameter("@PettyDate", model.PettyBillDate ?? (object)DBNull.Value),
                new SqlParameter("@PettyAmount", model.PettyBillAmount ?? (object)DBNull.Value),
                new SqlParameter("@ChequeNo", model.ChequeNumber ?? (object)DBNull.Value),
                new SqlParameter("@ChequeDate", model.ChequeDate ?? (object)DBNull.Value),
                new SqlParameter("@AmountPaid", model.AmountPaid ?? (object)DBNull.Value),
                new SqlParameter("@Compliance", model.ComplianceStatus ?? (object)DBNull.Value),
                new SqlParameter("@ComplianceDate", model.DateOfCompliance ?? (object)DBNull.Value),
                new SqlParameter("@Remarks", model.Remarks ?? (object)DBNull.Value),
                new SqlParameter("@AwardAmt", model.AwardAmount ?? (object)DBNull.Value),
                new SqlParameter("@Interest", model.InterestRate ?? (object)DBNull.Value),
                new SqlParameter("@Liability", model.LiabilityPercentage ?? (object)DBNull.Value),
                new SqlParameter("@PetitionDate", model.PetitionDate ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value),
                new SqlParameter("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value),
                new SqlParameter("@CaseOutcome", model.CaseOutcome ?? (object)DBNull.Value),
                new SqlParameter("@DisposalDate", model.DisposalDate ?? (object)DBNull.Value),
                new SqlParameter("@DisposalRemarks", model.DisposalRemarks ?? (object)DBNull.Value),
                new SqlParameter("@ClosureDate", model.ClosureDate ?? (object)DBNull.Value),
                new SqlParameter("@ModifiedBy", 1),
                new SqlParameter("@DivisionID", model.DivisionID),
                new SqlParameter("@EPID", model.EPID)
            };

            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                _db.ExecuteNonQuery(query, parameters, connection, transaction);

                // Sync Payments (Delete and re-insert within transaction only if provided)
                if (model.Payments != null)
                {
                    string deletePayments = "DELETE FROM LABOUR_EP_PAYMENTS WHERE EPID = @EPID";
                    _db.ExecuteNonQuery(deletePayments, new[] { new SqlParameter("@EPID", model.EPID) }, connection, transaction);

                    if (model.Payments.Count > 0)
                    {
                        foreach (var p in model.Payments)
                        {
                            string insertPayment = @"
                                INSERT INTO LABOUR_EP_PAYMENTS (EPID, Amount, PaymentDate, ChequeNumber, ChequeDate, Remarks)
                                VALUES (@EPID, @Amount, @Date, @Cheque, @CDate, @Rem)";
                            _db.ExecuteNonQuery(insertPayment, new[] {
                                new SqlParameter("@EPID", model.EPID),
                                new SqlParameter("@Amount", p.Amount),
                                new SqlParameter("@Date", p.PaymentDate),
                                new SqlParameter("@Cheque", p.ChequeNumber ?? (object)DBNull.Value),
                                new SqlParameter("@CDate", p.ChequeDate ?? (object)DBNull.Value),
                                new SqlParameter("@Rem", p.Remarks ?? (object)DBNull.Value)
                            }, connection, transaction);
                        }
                    }
                }
                if (model.CaseID > 0)
                {
                    string syncQuery = @"UPDATE LABOUR_CASES SET IsEPFiled = 1 WHERE CaseID = @CaseID";
                    _db.ExecuteNonQuery(syncQuery, new[] { new SqlParameter("@CaseID", model.CaseID) }, connection, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public IEnumerable<LabourEPViewModel> GetEPsByCaseId(int caseId)
        {
            var eps = new List<LabourEPViewModel>();
            string query = @"
                SELECT ep.*, mc.CaseNumber, mc.CaseYear 
                FROM LABOUR_EP_DETAILS ep
                JOIN LABOUR_CASES mc ON ep.CaseID = mc.CaseID
                WHERE ep.CaseID = @ID
                ORDER BY ep.CreatedDate DESC";
            
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ID", caseId) });
            foreach (DataRow row in dt.Rows)
            {
                eps.Add(MapRowToViewModel(row));
            }
            return eps;
        }

        public IEnumerable<LabourEPViewModel> GetEPsByArisingApplication(int parentCaseId, string? arisingCaseNo)
        {
            var eps = new List<LabourEPViewModel>();
            string query = @"
                SELECT ep.*, mc.CaseNumber, mc.CaseYear 
                FROM LABOUR_EP_DETAILS ep
                LEFT JOIN LABOUR_CASES mc ON ep.CaseID = mc.CaseID
                WHERE (@ParentCaseID > 0 AND ep.CaseID = @ParentCaseID)
                   OR (@ArisingCaseNo IS NOT NULL AND @ArisingCaseNo <> '' AND ep.ArisingFromCaseNo LIKE '%' + @ArisingCaseNo + '%')
                ORDER BY ep.CreatedDate DESC";
            
            var parameters = new[] {
                new SqlParameter("@ParentCaseID", parentCaseId),
                new SqlParameter("@ArisingCaseNo", string.IsNullOrEmpty(arisingCaseNo) ? (object)DBNull.Value : arisingCaseNo.Trim())
            };

            DataTable dt = _db.ExecuteQuery(query, parameters);
            foreach (DataRow row in dt.Rows)
            {
                eps.Add(MapRowToViewModel(row));
            }
            return eps;
        }

        private LabourEPViewModel MapRowToViewModel(DataRow row)
        {
            return new LabourEPViewModel
            {
                EPID = Convert.ToInt32(row["EPID"]),
                CaseID = Convert.ToInt32(row["CaseID"]),
                EPNumber = row["EPNumber"]?.ToString(),
                EPYear = row["EPYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["EPYear"]) : null,
                EPCourt = row["EPCourt"]?.ToString(),
                EP_EntrustmentNo = row["EP_EntrustmentNo"]?.ToString(),
                EP_EntrustmentDate = row["EP_EntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["EP_EntrustmentDate"]) : null,
                ArisingFromCaseNo = row["ArisingFromCaseNo"]?.ToString() ?? row["CaseNumber"]?.ToString(),
                ArisingFromCaseYear = row["ArisingFromCaseYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["ArisingFromCaseYear"]) : (row["CaseYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["CaseYear"]) : null),
                ArisingFromCourt = row["ArisingFromCourt"]?.ToString(),
                OriginalCaseStatus = row["OriginalCaseStatus"]?.ToString(),
                EPStatus = row["EPStatus"]?.ToString(),
                NextHearingDate = row["NextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["NextHearingDate"]) : null,
                AsPerECourts = row["AsPerECourts"]?.ToString(),
                IsSentToAccounts = row["IsSentToAccounts"] != DBNull.Value && (bool)row["IsSentToAccounts"],
                DateSentToAccounts = row["DateSentToAccounts"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DateSentToAccounts"]) : null,
                PettyBillDate = row["PettyBillDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["PettyBillDate"]) : null,
                PettyBillAmount = row["PettyBillAmount"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["PettyBillAmount"]) : null,
                ChequeNumber = row["ChequeNumber"]?.ToString(),
                ChequeDate = row["ChequeDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ChequeDate"]) : null,
                AmountPaid = row["AmountPaid"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["AmountPaid"]) : null,
                ComplianceStatus = row["ComplianceStatus"]?.ToString(),
                DateOfCompliance = row["DateOfCompliance"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DateOfCompliance"]) : null,
                AwardAmount = row.Table.Columns.Contains("AwardAmount") && row["AwardAmount"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["AwardAmount"]) : null,
                InterestRate = row.Table.Columns.Contains("InterestRate") && row["InterestRate"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["InterestRate"]) : null,
                LiabilityPercentage = row.Table.Columns.Contains("LiabilityPercentage") && row["LiabilityPercentage"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["LiabilityPercentage"]) : null,
                PetitionDate = row.Table.Columns.Contains("PetitionDate") && row["PetitionDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["PetitionDate"]) : null,
                AdvocateID = row.Table.Columns.Contains("AdvocateID") && row["AdvocateID"] != DBNull.Value ? (int?)Convert.ToInt32(row["AdvocateID"]) : null,
                AdvocateName = row.Table.Columns.Contains("AdvocateName") ? row["AdvocateName"]?.ToString() : null,
                CaseOutcome = row.Table.Columns.Contains("CaseOutcome") ? row["CaseOutcome"]?.ToString() : null,
                DisposalDate = row.Table.Columns.Contains("DisposalDate") && row["DisposalDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DisposalDate"]) : null,
                DisposalRemarks = row.Table.Columns.Contains("DisposalRemarks") ? row["DisposalRemarks"]?.ToString() : null,
                ClosureDate = row.Table.Columns.Contains("ClosureDate") && row["ClosureDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ClosureDate"]) : null,
                CNRNumber = row.Table.Columns.Contains("CNRNumber") ? row["CNRNumber"]?.ToString() : null,
                EstCode = row.Table.Columns.Contains("EstCode") ? row["EstCode"]?.ToString() : null,
                CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") ? row["CaseTypeCode"]?.ToString() : null,
                DivisionID = row.Table.Columns.Contains("DivisionID") && row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                Payments = GetEPPayments(Convert.ToInt32(row["EPID"]))
            };
        }

        private List<LabourEPPaymentViewModel> GetEPPayments(int epId)
        {
            var payments = new List<LabourEPPaymentViewModel>();
            string query = "SELECT * FROM LABOUR_EP_PAYMENTS WHERE EPID = @EPID ORDER BY PaymentDate";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@EPID", epId) });
            foreach (DataRow row in dt.Rows)
            {
                payments.Add(new LabourEPPaymentViewModel
                {
                    PaymentID = (int)row["PaymentID"],
                    EPID = (int)row["EPID"],
                    Amount = (decimal)row["Amount"],
                    PaymentDate = (DateTime)row["PaymentDate"],
                    ChequeNumber = row["ChequeNumber"]?.ToString(),
                    ChequeDate = row["ChequeDate"] != DBNull.Value ? (DateTime?)row["ChequeDate"] : null,
                    Remarks = row["Remarks"]?.ToString()
                });
            }
            return payments;
        }
    }
}
