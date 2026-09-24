using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace MVCCaseManagement.DAL
{
    public class PettyBillRepository : IPettyBillRepository
    {
        private readonly string _connectionString;

        public PettyBillRepository(DBHelper dbHelper)
        {
            _connectionString = dbHelper.GetConnectionString();
        }

        public int SavePettyBill(PettyBillViewModel model)
        {
            int billId = 0;
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert main bill record
                        string insertBillQuery = @"
                            INSERT INTO PETTY_BILLS (
                                CaseID, MVCNo, MVCYear, MACTName, VehicleNo, AccidentDate,
                                PetitionerName, AppealNumber, DivisionName, EnhancedAmount,
                                CourtCost, TDSPercentage, TDSAmount, FileRefNo, ApprovalLetterNo,
                                ApprovalDate, BillDate, NetPayable, GrossPayable, TotalDeductions,
                                AwardType, BaseAwardAmount, InterestFrom, InterestTo,
                                IsRelaxationApplied, RelaxationFrom, RelaxationTo
                            ) VALUES (
                                @CaseID, @MVCNo, @MVCYear, @MACTName, @VehicleNo, @AccidentDate,
                                @PetitionerName, @AppealNumber, @DivisionName, @EnhancedAmount,
                                @CourtCost, @TDSPercentage, @TDSAmount, @FileRefNo, @ApprovalLetterNo,
                                @ApprovalDate, @BillDate, @NetPayable, @GrossPayable, @TotalDeductions,
                                @AwardType, @BaseAwardAmount, @InterestFrom, @InterestTo,
                                @IsRelaxationApplied, @RelaxationFrom, @RelaxationTo
                            );
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var cmd = new SqlCommand(insertBillQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CaseID", (object?)model.CaseID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MVCNo", model.MVCNo);
                            cmd.Parameters.AddWithValue("@MVCYear", (object?)model.MVCYear ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MACTName", (object?)model.MACTName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AccidentDate", (object?)model.AccidentDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PetitionerName", (object?)model.PetitionerName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AppealNumber", (object?)model.AppealNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DivisionName", (object?)model.DivisionName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EnhancedAmount", model.EnhancedAmount);
                            cmd.Parameters.AddWithValue("@CourtCost", model.CourtCost);
                            cmd.Parameters.AddWithValue("@TDSPercentage", model.TDSPercentage);
                            cmd.Parameters.AddWithValue("@TDSAmount", model.TDSAmount);
                            cmd.Parameters.AddWithValue("@FileRefNo", (object?)model.FileRefNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ApprovalLetterNo", (object?)model.ApprovalLetterNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ApprovalDate", (object?)model.ApprovalDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BillDate", (object?)model.BillDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NetPayable", model.NetPayable);
                            cmd.Parameters.AddWithValue("@GrossPayable", model.GrossPayable);
                            cmd.Parameters.AddWithValue("@TotalDeductions", model.TotalDeductions);
                            cmd.Parameters.AddWithValue("@AwardType", model.AwardType ?? "Lower Court");
                            cmd.Parameters.AddWithValue("@BaseAwardAmount", model.BaseAwardAmount);
                            cmd.Parameters.AddWithValue("@InterestFrom", (object?)model.InterestFrom ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@InterestTo", (object?)model.InterestTo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsRelaxationApplied", model.IsRelaxationApplied);
                            cmd.Parameters.AddWithValue("@RelaxationFrom", (object?)model.RelaxationFrom ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RelaxationTo", (object?)model.RelaxationTo ?? DBNull.Value);

                            billId = (int)cmd.ExecuteScalar();
                        }

                        // Insert interest entries
                        if (model.InterestEntries != null && model.InterestEntries.Count > 0)
                        {
                            string insertInterestQuery = @"
                                INSERT INTO PETTY_BILL_INTEREST (BillID, FromDate, ToDate, Amount)
                                VALUES (@BillID, @FromDate, @ToDate, @Amount)";

                            foreach (var interest in model.InterestEntries)
                            {
                                using (var cmd = new SqlCommand(insertInterestQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BillID", billId);
                                    cmd.Parameters.AddWithValue("@FromDate", interest.FromDate);
                                    cmd.Parameters.AddWithValue("@ToDate", interest.ToDate);
                                    cmd.Parameters.AddWithValue("@Amount", interest.Amount);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Insert payment entries
                        if (model.PreviousPayments != null && model.PreviousPayments.Count > 0)
                        {
                            string insertPaymentQuery = @"
                                INSERT INTO PETTY_BILL_PAYMENTS (BillID, ChequeNumber, ChequeDate, Amount, IsSelected, Remarks, IsCancelled)
                                VALUES (@BillID, @ChequeNumber, @ChequeDate, @Amount, @IsSelected, @Remarks, @IsCancelled)";

                            foreach (var payment in model.PreviousPayments)
                            {
                                using (var cmd = new SqlCommand(insertPaymentQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BillID", billId);
                                    cmd.Parameters.AddWithValue("@ChequeNumber", (object)payment.ChequeNumber ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ChequeDate", (object?)payment.ChequeDate ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                                    cmd.Parameters.AddWithValue("@IsSelected", payment.IsSelected);
                                    cmd.Parameters.AddWithValue("@Remarks", (object?)payment.Remarks ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IsCancelled", payment.IsCancelled);
                                    cmd.ExecuteNonQuery();
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

            return billId;
        }

        public PettyBillViewModel? GetPettyBillById(int billId)
        {
            PettyBillViewModel? model = null;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Get main bill record
                string getBillQuery = @"
                    SELECT * FROM PETTY_BILLS WHERE BillID = @BillID";

                using (var cmd = new SqlCommand(getBillQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@BillID", billId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model = new PettyBillViewModel
                            {
                                BillID = reader.GetInt32(reader.GetOrdinal("BillID")),
                                CaseID = reader.IsDBNull(reader.GetOrdinal("CaseID")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("CaseID")),
                                MVCNo = reader.IsDBNull(reader.GetOrdinal("MVCNo")) ? string.Empty : reader.GetString(reader.GetOrdinal("MVCNo")),
                                MVCYear = reader.IsDBNull(reader.GetOrdinal("MVCYear")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MVCYear")),
                                MACTName = reader.IsDBNull(reader.GetOrdinal("MACTName")) ? null : reader.GetString(reader.GetOrdinal("MACTName")),
                                VehicleNo = reader.IsDBNull(reader.GetOrdinal("VehicleNo")) ? null : reader.GetString(reader.GetOrdinal("VehicleNo")),
                                AccidentDate = reader.IsDBNull(reader.GetOrdinal("AccidentDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AccidentDate")),
                                PetitionerName = reader.IsDBNull(reader.GetOrdinal("PetitionerName")) ? null : reader.GetString(reader.GetOrdinal("PetitionerName")),
                                AppealNumber = reader.IsDBNull(reader.GetOrdinal("AppealNumber")) ? null : reader.GetString(reader.GetOrdinal("AppealNumber")),
                                DivisionName = reader.IsDBNull(reader.GetOrdinal("DivisionName")) ? null : reader.GetString(reader.GetOrdinal("DivisionName")),
                                EnhancedAmount = reader.GetDecimal(reader.GetOrdinal("EnhancedAmount")),
                                CourtCost = reader.GetDecimal(reader.GetOrdinal("CourtCost")),
                                TDSPercentage = reader.GetDecimal(reader.GetOrdinal("TDSPercentage")),
                                TDSAmount = reader.GetDecimal(reader.GetOrdinal("TDSAmount")),
                                FileRefNo = reader.IsDBNull(reader.GetOrdinal("FileRefNo")) ? null : reader.GetString(reader.GetOrdinal("FileRefNo")),
                                ApprovalLetterNo = reader.IsDBNull(reader.GetOrdinal("ApprovalLetterNo")) ? null : reader.GetString(reader.GetOrdinal("ApprovalLetterNo")),
                                ApprovalDate = reader.IsDBNull(reader.GetOrdinal("ApprovalDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("ApprovalDate")),
                                BillDate = reader.IsDBNull(reader.GetOrdinal("BillDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BillDate")),
                                NetPayable = reader.GetDecimal(reader.GetOrdinal("NetPayable")),
                                GrossPayable = reader.GetDecimal(reader.GetOrdinal("GrossPayable")),
                                TotalDeductions = reader.GetDecimal(reader.GetOrdinal("TotalDeductions")),
                                AwardType = reader.IsDBNull(reader.GetOrdinal("AwardType")) ? "Lower Court" : reader.GetString(reader.GetOrdinal("AwardType")),
                                BaseAwardAmount = reader.IsDBNull(reader.GetOrdinal("BaseAwardAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("BaseAwardAmount")),
                                InterestFrom = reader.IsDBNull(reader.GetOrdinal("InterestFrom")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("InterestFrom")),
                                InterestTo = reader.IsDBNull(reader.GetOrdinal("InterestTo")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("InterestTo")),
                                IsRelaxationApplied = !reader.IsDBNull(reader.GetOrdinal("IsRelaxationApplied")) && reader.GetBoolean(reader.GetOrdinal("IsRelaxationApplied")),
                                RelaxationFrom = reader.IsDBNull(reader.GetOrdinal("RelaxationFrom")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("RelaxationFrom")),
                                RelaxationTo = reader.IsDBNull(reader.GetOrdinal("RelaxationTo")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("RelaxationTo")),
                                InterestEntries = new List<InterestEntry>(),
                                PreviousPayments = new List<PreviousPayment>()
                            };
                        }
                    }
                }

                if (model != null)
                {
                    // Get interest entries
                    string getInterestQuery = @"
                        SELECT * FROM PETTY_BILL_INTEREST WHERE BillID = @BillID ORDER BY FromDate";

                    using (var cmd = new SqlCommand(getInterestQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@BillID", billId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                model.InterestEntries.Add(new InterestEntry
                                {
                                    FromDate = reader.GetDateTime(reader.GetOrdinal("FromDate")),
                                    ToDate = reader.GetDateTime(reader.GetOrdinal("ToDate")),
                                    Amount = reader.GetDecimal(reader.GetOrdinal("Amount"))
                                });
                            }
                        }
                    }

                    // Get payment entries
                    string getPaymentsQuery = @"
                        SELECT PaymentID, ChequeNumber, ChequeDate, Amount, IsSelected, IsCancelled, Remarks 
                        FROM PETTY_BILL_PAYMENTS WHERE BillID = @BillID ORDER BY ChequeDate";

                    using (var cmd = new SqlCommand(getPaymentsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@BillID", billId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                model.PreviousPayments.Add(new PreviousPayment
                                {
                                    PaymentID = reader.GetInt32(reader.GetOrdinal("PaymentID")),
                                    ChequeNumber = reader.IsDBNull(reader.GetOrdinal("ChequeNumber")) ? string.Empty : reader.GetString(reader.GetOrdinal("ChequeNumber")),
                                    ChequeDate = reader.IsDBNull(reader.GetOrdinal("ChequeDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("ChequeDate")),
                                    Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                                    IsSelected = reader.GetBoolean(reader.GetOrdinal("IsSelected")),
                                    IsCancelled = reader.IsDBNull(reader.GetOrdinal("IsCancelled")) ? false : reader.GetBoolean(reader.GetOrdinal("IsCancelled")),
                                    Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks"))
                                });
                            }
                        }
                    }
                }
            }

            return model;
        }

        public List<PettyBillViewModel> GetPettyBillsByCaseId(int caseId)
        {
            var bills = new List<PettyBillViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT BillID, MVCNo, MVCYear, BillDate, NetPayable, CreatedAt, UpdatedAt
                    FROM PETTY_BILLS 
                    WHERE CaseID = @CaseID
                    ORDER BY CreatedAt DESC";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CaseID", caseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bills.Add(new PettyBillViewModel
                            {
                                BillID = reader.GetInt32(reader.GetOrdinal("BillID")),
                                MVCNo = reader.IsDBNull(reader.GetOrdinal("MVCNo")) ? string.Empty : reader.GetString(reader.GetOrdinal("MVCNo")),
                                MVCYear = reader.IsDBNull(reader.GetOrdinal("MVCYear")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MVCYear")),
                                BillDate = reader.IsDBNull(reader.GetOrdinal("BillDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BillDate")),
                                NetPayable = reader.GetDecimal(reader.GetOrdinal("NetPayable"))
                            });
                        }
                    }
                }
            }

            return bills;
        }

        public List<PettyBillViewModel> GetPettyBillsByMVC(string mvcNo, int mvcYear)
        {
            var bills = new List<PettyBillViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT BillID, MVCNo, MVCYear, BillDate, NetPayable, CreatedAt, UpdatedAt
                    FROM PETTY_BILLS 
                    WHERE MVCNo = @MVCNo AND MVCYear = @MVCYear
                    ORDER BY CreatedAt DESC";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@MVCNo", mvcNo);
                    cmd.Parameters.AddWithValue("@MVCYear", mvcYear);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bills.Add(new PettyBillViewModel
                            {
                                BillID = reader.GetInt32(reader.GetOrdinal("BillID")),
                                MVCNo = reader.IsDBNull(reader.GetOrdinal("MVCNo")) ? string.Empty : reader.GetString(reader.GetOrdinal("MVCNo")),
                                MVCYear = reader.IsDBNull(reader.GetOrdinal("MVCYear")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MVCYear")),
                                BillDate = reader.IsDBNull(reader.GetOrdinal("BillDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BillDate")),
                                NetPayable = reader.GetDecimal(reader.GetOrdinal("NetPayable"))
                            });
                        }
                    }
                }
            }

            return bills;
        }

        public List<PreviousPayment> GetAllBillPaymentsByCaseId(int caseId)
        {
            var payments = new List<PreviousPayment>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT PaymentID, ChequeNumber, ChequeDate, Amount, Remarks
                    FROM PETTY_BILL_PAYMENTS p
                    INNER JOIN PETTY_BILLS b ON p.BillID = b.BillID
                    WHERE b.CaseID = @CaseID
                    ORDER BY p.ChequeDate";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CaseID", caseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            payments.Add(new PreviousPayment
                            {
                                PaymentID = reader.GetInt32(reader.GetOrdinal("PaymentID")),
                                ChequeNumber = reader.IsDBNull(reader.GetOrdinal("ChequeNumber")) ? string.Empty : reader.GetString(reader.GetOrdinal("ChequeNumber")),
                                ChequeDate = reader.IsDBNull(reader.GetOrdinal("ChequeDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("ChequeDate")),
                                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                                IsSelected = true,
                                Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? "Manual Entry in Petty Bill" : reader.GetString(reader.GetOrdinal("Remarks"))
                            });
                        }
                    }
                }
            }
            return payments;
        }

        public bool UpdatePettyBill(PettyBillViewModel model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Update main bill record
                        string updateBillQuery = @"
                            UPDATE PETTY_BILLS SET
                                CaseID = @CaseID, MVCNo = @MVCNo, MVCYear = @MVCYear, MACTName = @MACTName,
                                VehicleNo = @VehicleNo, AccidentDate = @AccidentDate, PetitionerName = @PetitionerName,
                                AppealNumber = @AppealNumber, DivisionName = @DivisionName, EnhancedAmount = @EnhancedAmount,
                                CourtCost = @CourtCost, TDSPercentage = @TDSPercentage, TDSAmount = @TDSAmount,
                                FileRefNo = @FileRefNo, ApprovalLetterNo = @ApprovalLetterNo, ApprovalDate = @ApprovalDate,
                                BillDate = @BillDate, NetPayable = @NetPayable, GrossPayable = @GrossPayable,
                                TotalDeductions = @TotalDeductions, AwardType = @AwardType, BaseAwardAmount = @BaseAwardAmount,
                                InterestFrom = @InterestFrom, InterestTo = @InterestTo,
                                IsRelaxationApplied = @IsRelaxationApplied, RelaxationFrom = @RelaxationFrom, RelaxationTo = @RelaxationTo,
                                UpdatedAt = GETDATE()
                            WHERE BillID = @BillID";

                        using (var cmd = new SqlCommand(updateBillQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BillID", model.BillID);
                            cmd.Parameters.AddWithValue("@CaseID", (object?)model.CaseID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MVCNo", (object?)model.MVCNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MVCYear", (object?)model.MVCYear ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MACTName", (object?)model.MACTName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AccidentDate", (object?)model.AccidentDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PetitionerName", (object?)model.PetitionerName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AppealNumber", (object?)model.AppealNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DivisionName", (object?)model.DivisionName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EnhancedAmount", model.EnhancedAmount);
                            cmd.Parameters.AddWithValue("@CourtCost", model.CourtCost);
                            cmd.Parameters.AddWithValue("@TDSPercentage", model.TDSPercentage);
                            cmd.Parameters.AddWithValue("@TDSAmount", model.TDSAmount);
                            cmd.Parameters.AddWithValue("@FileRefNo", (object?)model.FileRefNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ApprovalLetterNo", (object?)model.ApprovalLetterNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ApprovalDate", (object?)model.ApprovalDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BillDate", (object?)model.BillDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NetPayable", model.NetPayable);
                            cmd.Parameters.AddWithValue("@GrossPayable", model.GrossPayable);
                            cmd.Parameters.AddWithValue("@TotalDeductions", model.TotalDeductions);
                            cmd.Parameters.AddWithValue("@AwardType", model.AwardType ?? "Lower Court");
                            cmd.Parameters.AddWithValue("@BaseAwardAmount", model.BaseAwardAmount);
                            cmd.Parameters.AddWithValue("@InterestFrom", (object?)model.InterestFrom ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@InterestTo", (object?)model.InterestTo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsRelaxationApplied", model.IsRelaxationApplied);
                            cmd.Parameters.AddWithValue("@RelaxationFrom", (object?)model.RelaxationFrom ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RelaxationTo", (object?)model.RelaxationTo ?? DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }

                        // Delete existing interest and payment entries
                        string deleteInterestQuery = "DELETE FROM PETTY_BILL_INTEREST WHERE BillID = @BillID";
                        string deletePaymentsQuery = "DELETE FROM PETTY_BILL_PAYMENTS WHERE BillID = @BillID";

                        using (var cmd = new SqlCommand(deleteInterestQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BillID", model.BillID);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = new SqlCommand(deletePaymentsQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BillID", model.BillID);
                            cmd.ExecuteNonQuery();
                        }

                        // Re-insert interest entries
                        if (model.InterestEntries != null && model.InterestEntries.Count > 0)
                        {
                            string insertInterestQuery = @"
                                INSERT INTO PETTY_BILL_INTEREST (BillID, FromDate, ToDate, Amount)
                                VALUES (@BillID, @FromDate, @ToDate, @Amount)";

                            foreach (var interest in model.InterestEntries)
                            {
                                using (var cmd = new SqlCommand(insertInterestQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BillID", model.BillID);
                                    cmd.Parameters.AddWithValue("@FromDate", interest.FromDate);
                                    cmd.Parameters.AddWithValue("@ToDate", interest.ToDate);
                                    cmd.Parameters.AddWithValue("@Amount", interest.Amount);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Re-insert payment entries
                        if (model.PreviousPayments != null && model.PreviousPayments.Count > 0)
                        {
                            string insertPaymentQuery = @"
                                INSERT INTO PETTY_BILL_PAYMENTS (BillID, ChequeNumber, ChequeDate, Amount, IsSelected, Remarks, IsCancelled)
                                VALUES (@BillID, @ChequeNumber, @ChequeDate, @Amount, @IsSelected, @Remarks, @IsCancelled)";

                            foreach (var payment in model.PreviousPayments)
                            {
                                using (var cmd = new SqlCommand(insertPaymentQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BillID", model.BillID);
                                    cmd.Parameters.AddWithValue("@ChequeNumber", (object?)payment.ChequeNumber ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ChequeDate", (object?)payment.ChequeDate ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                                    cmd.Parameters.AddWithValue("@IsSelected", payment.IsSelected);
                                    cmd.Parameters.AddWithValue("@Remarks", (object?)payment.Remarks ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IsCancelled", payment.IsCancelled);
                                    cmd.ExecuteNonQuery();
                                }
                            }
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
            }
        }

        public bool DeletePettyBill(int billId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "DELETE FROM PETTY_BILLS WHERE BillID = @BillID";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@BillID", billId);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public bool DeletePettyBillPayment(int paymentId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "DELETE FROM PETTY_BILL_PAYMENTS WHERE PaymentID = @PaymentID";
                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PaymentID", paymentId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}
