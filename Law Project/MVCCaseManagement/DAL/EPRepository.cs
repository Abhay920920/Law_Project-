using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class EPRepository : IEPRepository
    {
        private readonly DBHelper _db;

        public EPRepository(DBHelper db)
        {
            _db = db;
        }

        public IEnumerable<EPViewModel> GetAllEPs(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            var eps = new List<EPViewModel>();
            int offset = (pageNumber - 1) * pageSize;

            string query = @"
                SELECT ep.*, mc.MVCNo, mc.MVCYear 
                FROM MVC_EP_DETAILS ep
                JOIN MVC_CASES mc ON ep.CaseID = mc.CaseID
                LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON mc.CaseID = adv.CaseID
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
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query += " AND (ep.ArisingFromMVCNo LIKE @Search OR ep.VehicleNo LIKE @Search OR ep.EPStatus LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "SentToCO")
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
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
                FROM MVC_EP_DETAILS ep
                JOIN MVC_CASES mc ON ep.CaseID = mc.CaseID
                LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON mc.CaseID = adv.CaseID
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
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query += " AND (ep.ArisingFromMVCNo LIKE @Search OR ep.VehicleNo LIKE @Search OR ep.EPStatus LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "SentToCO")
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
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

        public EPViewModel? GetEPById(int epId)
        {
            string query = @"
                SELECT ep.*, mc.MVCNo, mc.MVCYear 
                FROM MVC_EP_DETAILS ep
                JOIN MVC_CASES mc ON ep.CaseID = mc.CaseID
                WHERE ep.EPID = @EPID";
            
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@EPID", epId) });
            if (dt.Rows.Count == 0) return null;
            return MapRowToViewModel(dt.Rows[0]);
        }

        public int SaveEP(EPViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Ensure summary fields from payments if present
                        if (model.Payments != null && model.Payments.Count > 0)
                        {
                            var validPayments = model.Payments.Where(p => p.Amount > 0).ToList();
                            decimal totalPaid = validPayments.Sum(p => p.Amount);
                            if (totalPaid > 0)
                            {
                                model.AmountPaid = totalPaid;
                            }
                            var latest = validPayments.OrderByDescending(p => p.ChequeDate ?? p.PaymentDate).FirstOrDefault();
                            if (latest != null)
                            {
                                if (string.IsNullOrEmpty(model.ChequeNumber))
                                    model.ChequeNumber = latest.ChequeNumber;
                                if (!model.ChequeDate.HasValue)
                                    model.ChequeDate = latest.ChequeDate;
                            }
                            decimal targetAmount = (model.AwardAmount ?? 0) * ((model.LiabilityPercentage ?? 100) / 100m);
                            if (targetAmount > 0 && (model.AmountPaid ?? 0) >= targetAmount)
                            {
                                model.ComplianceStatus = "Paid";
                                model.DateOfCompliance ??= model.ChequeDate ?? DateTime.Today;
                            }
                            else if ((model.AmountPaid ?? 0) > 0)
                            {
                                model.ComplianceStatus = "Partially Paid";
                            }
                        }

                        string effectiveEPNo = !string.IsNullOrWhiteSpace(model.EPNumber)
                            ? model.EPNumber
                            : $"EP/{model.ArisingFromMVCNo}/{model.ArisingFromMVCYear ?? DateTime.Today.Year}";

                        string query = @"
                            INSERT INTO MVC_EP_DETAILS (
                                CaseID, ArisingFromMVCNo, ArisingFromMVCYear, ArisingFromMACT,
                                OriginalMVCStatus, EPStatus, NextHearingDate, AsPerECourts, IsSentToAccounts, DateSentToAccounts,
                                AwardAmount, InterestRate, LiabilityPercentage, PetitionDate, RealizationDate, CalculationMethod,
                                PettyBillDate, PettyBillAmount, ChequeNumber, ChequeDate, AmountPaid,
                                ComplianceStatus, DateOfCompliance, Remarks, CreatedBy, DivisionID,
                                VehicleNo, AccidentDate, EPNumber, EPYear, EPCourt,
                                EP_EntrustmentNo, EP_EntrustmentDate, AdvocateID, AdvocateName,
                                CaseOutcome, DisposalDate, ClosureDate, DisposalRemarks,
                                CNRNumber, EstCode, CaseTypeCode
                            )
                            OUTPUT INSERTED.EPID
                            VALUES (
                                @CaseID, @MVCNo, @MVCYear, @MACT,
                                @OriginalStatus, @EPStatus, @NextHearingDate, @AsPerECourts, @SentToAcc, @DateSent,
                                @AwardAmt, @Interest, @Liability, @PetitionDate, @RealizationDate, @CalculationMethod,
                                @PettyDate, @PettyAmount, @ChequeNo, @ChequeDate, @AmountPaid,
                                @Compliance, @ComplianceDate, @Remarks, @CreatedBy, @DivisionID,
                                @VehicleNo, @AccidentDate, @EPNumber, @EPYear, @EPCourt,
                                @EPEntrustmentNo, @EPEntrustmentDate, @AdvocateID, @AdvocateName,
                                @CaseOutcome, @DisposalDate, @ClosureDate, @DisposalRemarks,
                                @CNRNumber, @EstCode, @CaseTypeCode
                            )";

                        var cmd = new SqlCommand(query, conn, trans);
                        cmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                        cmd.Parameters.AddWithValue("@EPNumber", effectiveEPNo);
                        cmd.Parameters.AddWithValue("@EPYear", (object?)model.EPYear ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EPCourt", (object?)model.EPCourt ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MVCNo", (object?)model.ArisingFromMVCNo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MVCYear", (object?)model.ArisingFromMVCYear ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MACT", (object?)model.ArisingFromMACT ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@OriginalStatus", (object?)model.OriginalMVCStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EPStatus", (object?)model.EPStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@NextHearingDate", (object?)model.NextHearingDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AsPerECourts", (object?)model.AsPerECourts ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SentToAcc", model.IsSentToAccounts);
                        cmd.Parameters.AddWithValue("@DateSent", (object?)model.DateSentToAccounts ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PettyDate", (object?)model.PettyBillDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PettyAmount", (object?)model.PettyBillAmount ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ChequeNo", (object?)model.ChequeNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ChequeDate", (object?)model.ChequeDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AmountPaid", (object?)model.AmountPaid ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Compliance", (object?)model.ComplianceStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ComplianceDate", (object?)model.DateOfCompliance ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Remarks", (object?)model.Remarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AwardAmt", (object?)model.AwardAmount ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Interest", (object?)model.InterestRate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Liability", (object?)model.LiabilityPercentage ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PetitionDate", (object?)model.PetitionDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RealizationDate", (object?)model.RealizationDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CalculationMethod", (object?)model.CalculationMethod ?? "InterestDeduction");
                        cmd.Parameters.AddWithValue("@CreatedBy", 1);
                        cmd.Parameters.AddWithValue("@DivisionID", model.DivisionID);
                        cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AccidentDate", (object?)model.AccidentDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EPEntrustmentNo", (object?)model.EP_EntrustmentNo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EPEntrustmentDate", (object?)model.EP_EntrustmentDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateID", (object?)model.AdvocateID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateName", (object?)model.AdvocateName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseOutcome", (object?)model.CaseOutcome ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalDate", (object?)model.DisposalDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClosureDate", (object?)model.ClosureDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalRemarks", (object?)model.DisposalRemarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CNRNumber", (object?)model.CNRNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EstCode", (object?)model.EstCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseTypeCode", (object?)model.CaseTypeCode ?? DBNull.Value);

                        int epId = Convert.ToInt32(cmd.ExecuteScalar());

                        // Save payments if any
                        if (epId > 0 && model.Payments != null && model.Payments.Count > 0)
                        {
                            foreach (var p in model.Payments.Where(x => x.Amount > 0))
                            {
                                string insertPayment = @"
                                    INSERT INTO MVC_EP_PAYMENTS (EPID, Amount, PaymentDate, ChequeNumber, ChequeDate, Remarks)
                                    VALUES (@EPID, @Amount, @Date, @Cheque, @CDate, @Rem)";
                                using (var pCmd = new SqlCommand(insertPayment, conn, trans))
                                {
                                    pCmd.Parameters.AddWithValue("@EPID", epId);
                                    pCmd.Parameters.AddWithValue("@Amount", p.Amount);
                                    DateTime pDate = p.ChequeDate ?? (p.PaymentDate != default ? p.PaymentDate : DateTime.Today);
                                    pCmd.Parameters.AddWithValue("@Date", pDate);
                                    pCmd.Parameters.AddWithValue("@Cheque", (object?)p.ChequeNumber ?? DBNull.Value);
                                    pCmd.Parameters.AddWithValue("@CDate", (object?)p.ChequeDate ?? DBNull.Value);
                                    pCmd.Parameters.AddWithValue("@Rem", (object?)p.Remarks ?? DBNull.Value);
                                    pCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        if (epId > 0 && model.CaseID > 0)
                        {
                            string syncQuery = @"
                                IF EXISTS (SELECT 1 FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @CaseID)
                                BEGIN
                                    UPDATE MVC_CASE_ADVERSE_DETAILS 
                                    SET IsEPFiled = 1,
                                        EPNumber = COALESCE(@EPNumber, EPNumber),
                                        EPCourt = COALESCE(@EPCourt, EPCourt),
                                        EPStage = COALESCE(@EPStatus, EPStage),
                                        EPNextHearingDate = COALESCE(@NextHearingDate, EPNextHearingDate)
                                    WHERE CaseID = @CaseID
                                END
                                ELSE
                                BEGIN
                                    INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, IsEPFiled, EPNumber, EPCourt, EPStage, EPNextHearingDate) 
                                    VALUES (@CaseID, 1, @EPNumber, @EPCourt, @EPStatus, @NextHearingDate)
                                END";
                            var syncCmd = new SqlCommand(syncQuery, conn, trans);
                            syncCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            syncCmd.Parameters.AddWithValue("@EPNumber", effectiveEPNo);
                            syncCmd.Parameters.AddWithValue("@EPCourt", (object?)model.EPCourt ?? DBNull.Value);
                            syncCmd.Parameters.AddWithValue("@EPStatus", (object?)model.EPStatus ?? DBNull.Value);
                            syncCmd.Parameters.AddWithValue("@NextHearingDate", (object?)model.NextHearingDate ?? DBNull.Value);
                            syncCmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return epId;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool UpdateEP(EPViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Ensure summary fields from payments if present
                        if (model.Payments != null && model.Payments.Count > 0)
                        {
                            var validPayments = model.Payments.Where(p => p.Amount > 0).ToList();
                            decimal totalPaid = validPayments.Sum(p => p.Amount);
                            model.AmountPaid = totalPaid;
                            
                            var latest = validPayments.OrderByDescending(p => p.ChequeDate ?? p.PaymentDate).FirstOrDefault();
                            if (latest != null)
                            {
                                if (!string.IsNullOrEmpty(latest.ChequeNumber))
                                    model.ChequeNumber = latest.ChequeNumber;
                                if (latest.ChequeDate.HasValue)
                                    model.ChequeDate = latest.ChequeDate;
                            }
                            decimal targetAmount = (model.AwardAmount ?? 0) * ((model.LiabilityPercentage ?? 100) / 100m);
                            if (targetAmount > 0 && totalPaid >= targetAmount)
                            {
                                model.ComplianceStatus = "Paid";
                                model.DateOfCompliance ??= model.ChequeDate ?? DateTime.Today;
                            }
                            else if (totalPaid > 0)
                            {
                                model.ComplianceStatus = "Partially Paid";
                            }
                            else
                            {
                                model.ComplianceStatus = "Pending";
                            }
                        }

                        string query = @"
                            UPDATE MVC_EP_DETAILS SET
                                ArisingFromMVCNo = @MVCNo, 
                                ArisingFromMVCYear = @MVCYear, 
                                ArisingFromMACT = @MACT,
                                OriginalMVCStatus = @OriginalStatus, 
                                EPStatus = @EPStatus, 
                                NextHearingDate = @NextHearingDate,
                                AsPerECourts = @AsPerECourts,
                                IsSentToAccounts = @SentToAcc, 
                                DateSentToAccounts = @DateSent,
                                AwardAmount = @AwardAmt,
                                InterestRate = @Interest,
                                LiabilityPercentage = @Liability,
                                PetitionDate = @PetitionDate,
                                RealizationDate = @RealizationDate,
                                CalculationMethod = @CalculationMethod,
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
                                VehicleNo = @VehicleNo,
                                AccidentDate = @AccidentDate,
                                EPNumber = COALESCE(NULLIF(@EPNumber, ''), EPNumber, 'EP/' + CAST(@EPID as varchar)),
                                EPYear = @EPYear,
                                EPCourt = @EPCourt,
                                EP_EntrustmentNo = @EPEntrustmentNo,
                                EP_EntrustmentDate = @EPEntrustmentDate,
                                AdvocateID = @AdvocateID,
                                AdvocateName = @AdvocateName,
                                CaseOutcome = @CaseOutcome,
                                DisposalDate = @DisposalDate,
                                ClosureDate = @ClosureDate,
                                DisposalRemarks = @DisposalRemarks,
                                CNRNumber = @CNRNumber,
                                EstCode = @EstCode,
                                CaseTypeCode = @CaseTypeCode
                            WHERE EPID = @EPID";

                        using (var cmd = new SqlCommand(query, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@EPNumber", (object?)model.EPNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EPYear", (object?)model.EPYear ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EPCourt", (object?)model.EPCourt ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MVCNo", (object?)model.ArisingFromMVCNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MVCYear", (object?)model.ArisingFromMVCYear ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MACT", (object?)model.ArisingFromMACT ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@OriginalStatus", (object?)model.OriginalMVCStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EPStatus", (object?)model.EPStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NextHearingDate", (object?)model.NextHearingDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AsPerECourts", (object?)model.AsPerECourts ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SentToAcc", model.IsSentToAccounts);
                            cmd.Parameters.AddWithValue("@DateSent", (object?)model.DateSentToAccounts ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PettyDate", (object?)model.PettyBillDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PettyAmount", (object?)model.PettyBillAmount ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ChequeNo", (object?)model.ChequeNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ChequeDate", (object?)model.ChequeDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AmountPaid", (object?)model.AmountPaid ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Compliance", (object?)model.ComplianceStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ComplianceDate", (object?)model.DateOfCompliance ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object?)model.Remarks ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AwardAmt", (object?)model.AwardAmount ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Interest", (object?)model.InterestRate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Liability", (object?)model.LiabilityPercentage ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PetitionDate", (object?)model.PetitionDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RealizationDate", (object?)model.RealizationDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CalculationMethod", (object?)model.CalculationMethod ?? "InterestDeduction");
                            cmd.Parameters.AddWithValue("@ModifiedBy", 1);
                            cmd.Parameters.AddWithValue("@DivisionID", model.DivisionID);
                            cmd.Parameters.AddWithValue("@EPID", model.EPID);
                            cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AccidentDate", (object?)model.AccidentDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EPEntrustmentNo", (object?)model.EP_EntrustmentNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EPEntrustmentDate", (object?)model.EP_EntrustmentDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AdvocateID", (object?)model.AdvocateID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AdvocateName", (object?)model.AdvocateName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CaseOutcome", (object?)model.CaseOutcome ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DisposalDate", (object?)model.DisposalDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ClosureDate", (object?)model.ClosureDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DisposalRemarks", (object?)model.DisposalRemarks ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CNRNumber", (object?)model.CNRNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EstCode", (object?)model.EstCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CaseTypeCode", (object?)model.CaseTypeCode ?? DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }

                        // Sync Payments atomically
                        string deletePayments = "DELETE FROM MVC_EP_PAYMENTS WHERE EPID = @EPID";
                        using (var delCmd = new SqlCommand(deletePayments, conn, trans))
                        {
                            delCmd.Parameters.AddWithValue("@EPID", model.EPID);
                            delCmd.ExecuteNonQuery();
                        }

                        if (model.Payments != null && model.Payments.Count > 0)
                        {
                            foreach (var p in model.Payments.Where(x => x.Amount > 0))
                            {
                                string insertPayment = @"
                                    INSERT INTO MVC_EP_PAYMENTS (EPID, Amount, PaymentDate, ChequeNumber, ChequeDate, Remarks)
                                    VALUES (@EPID, @Amount, @Date, @Cheque, @CDate, @Rem)";
                                using (var pCmd = new SqlCommand(insertPayment, conn, trans))
                                {
                                    pCmd.Parameters.AddWithValue("@EPID", model.EPID);
                                    pCmd.Parameters.AddWithValue("@Amount", p.Amount);
                                    DateTime pDate = p.ChequeDate ?? (p.PaymentDate != default ? p.PaymentDate : DateTime.Today);
                                    pCmd.Parameters.AddWithValue("@Date", pDate);
                                    pCmd.Parameters.AddWithValue("@Cheque", (object?)p.ChequeNumber ?? DBNull.Value);
                                    pCmd.Parameters.AddWithValue("@CDate", (object?)p.ChequeDate ?? DBNull.Value);
                                    pCmd.Parameters.AddWithValue("@Rem", (object?)p.Remarks ?? DBNull.Value);
                                    pCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        if (model.CaseID > 0)
                        {
                            string syncQuery = @"
                                IF EXISTS (SELECT 1 FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @CaseID)
                                BEGIN
                                    UPDATE MVC_CASE_ADVERSE_DETAILS SET IsEPFiled = 1 WHERE CaseID = @CaseID
                                END
                                ELSE
                                BEGIN
                                    INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, IsEPFiled) VALUES (@CaseID, 1)
                                END";
                            using (var syncCmd = new SqlCommand(syncQuery, conn, trans))
                            {
                                syncCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                syncCmd.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        Console.WriteLine($"UpdateEP Error: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        private EPViewModel MapRowToViewModel(DataRow row)
        {
            return new EPViewModel
            {
                EPID = Convert.ToInt32(row["EPID"]),
                CaseID = Convert.ToInt32(row["CaseID"]),
                EPNumber = row["EPNumber"]?.ToString(),
                EPYear = row["EPYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["EPYear"]) : null,
                EPCourt = row["EPCourt"]?.ToString(),
                ArisingFromMVCNo = row["ArisingFromMVCNo"]?.ToString() ?? row["MVCNo"]?.ToString(),
                ArisingFromMVCYear = row["ArisingFromMVCYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["ArisingFromMVCYear"]) : (row["MVCYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["MVCYear"]) : null),
                ArisingFromMACT = row["ArisingFromMACT"]?.ToString(),
                OriginalMVCStatus = row["OriginalMVCStatus"]?.ToString(),
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
                RealizationDate = row.Table.Columns.Contains("RealizationDate") && row["RealizationDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["RealizationDate"]) : null,
                CalculationMethod = row.Table.Columns.Contains("CalculationMethod") && row["CalculationMethod"] != DBNull.Value ? row["CalculationMethod"]?.ToString() ?? "InterestDeduction" : "InterestDeduction",
                VehicleNo = row.Table.Columns.Contains("VehicleNo") ? row["VehicleNo"]?.ToString() : null,
                AccidentDate = row.Table.Columns.Contains("AccidentDate") && row["AccidentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["AccidentDate"]) : null,
                Remarks = row["Remarks"]?.ToString(),
                DivisionID = row.Table.Columns.Contains("DivisionID") && row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                EP_EntrustmentNo = row.Table.Columns.Contains("EP_EntrustmentNo") ? row["EP_EntrustmentNo"]?.ToString() : null,
                EP_EntrustmentDate = row.Table.Columns.Contains("EP_EntrustmentDate") && row["EP_EntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["EP_EntrustmentDate"]) : null,
                AdvocateID = row.Table.Columns.Contains("AdvocateID") && row["AdvocateID"] != DBNull.Value ? (int?)Convert.ToInt32(row["AdvocateID"]) : null,
                AdvocateName = row.Table.Columns.Contains("AdvocateName") ? row["AdvocateName"]?.ToString() : null,
                CaseOutcome = row.Table.Columns.Contains("CaseOutcome") ? row["CaseOutcome"]?.ToString() : null,
                DisposalDate = row.Table.Columns.Contains("DisposalDate") && row["DisposalDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DisposalDate"]) : null,
                ClosureDate = row.Table.Columns.Contains("ClosureDate") && row["ClosureDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ClosureDate"]) : null,
                DisposalRemarks = row.Table.Columns.Contains("DisposalRemarks") ? row["DisposalRemarks"]?.ToString() : null,
                CNRNumber = row.Table.Columns.Contains("CNRNumber") ? row["CNRNumber"]?.ToString() : null,
                EstCode = row.Table.Columns.Contains("EstCode") ? row["EstCode"]?.ToString() : null,
                CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") ? row["CaseTypeCode"]?.ToString() : null,
                Payments = GetEPPayments(Convert.ToInt32(row["EPID"]))
            };
        }

        private List<EPPaymentViewModel> GetEPPayments(int epId)
        {
            var payments = new List<EPPaymentViewModel>();
            string query = "SELECT * FROM MVC_EP_PAYMENTS WHERE EPID = @EPID ORDER BY PaymentDate, PaymentID";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@EPID", epId) });
            foreach (DataRow row in dt.Rows)
            {
                payments.Add(new EPPaymentViewModel
                {
                    PaymentID = (int)row["PaymentID"],
                    EPID = (int)row["EPID"],
                    Amount = (decimal)row["Amount"],
                    PaymentDate = row["PaymentDate"] != DBNull.Value ? (DateTime)row["PaymentDate"] : DateTime.Today,
                    ChequeNumber = row["ChequeNumber"]?.ToString(),
                    ChequeDate = row["ChequeDate"] != DBNull.Value ? (DateTime?)row["ChequeDate"] : null,
                    Remarks = row["Remarks"]?.ToString()
                });
            }
            return payments;
        }
    }
}
