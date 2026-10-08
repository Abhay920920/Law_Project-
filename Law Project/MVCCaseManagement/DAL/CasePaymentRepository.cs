using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace MVCCaseManagement.DAL
{
    public class CasePaymentRepository : ICasePaymentRepository
    {
        private readonly string _connectionString;

        public CasePaymentRepository(DBHelper dbHelper)
        {
            _connectionString = dbHelper.GetConnectionString();
        }

        public List<CasePaymentViewModel> GetPaymentsByCaseId(int caseId)
        {
            var payments = new List<CasePaymentViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT PaymentID, CaseID, Amount, ChequeNumber, ChequeDate, 
                           PaymentType, Remarks, CreatedDate, CreatedBy
                    FROM MVC_CASE_PAYMENTS 
                    WHERE CaseID = @CaseID

                    UNION ALL

                    SELECT 
                        p.PaymentID * -1 as PaymentID, 
                        b.CaseID, 
                        p.Amount, 
                        p.ChequeNumber, 
                        p.ChequeDate, 
                        'Petty Bill' as PaymentType, 
                        p.Remarks, 
                        COALESCE(b.CreatedAt, b.BillDate) as CreatedDate, 
                        'Petty Bill System' as CreatedBy
                    FROM PETTY_BILL_PAYMENTS p
                    JOIN PETTY_BILLS b ON p.BillID = b.BillID
                    WHERE b.CaseID = @CaseID AND p.IsSelected = 1 AND p.IsCancelled = 0

                    ORDER BY CreatedDate DESC";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CaseID", caseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            payments.Add(new CasePaymentViewModel
                            {
                                PaymentID = reader.GetInt32(reader.GetOrdinal("PaymentID")),
                                CaseID = reader.GetInt32(reader.GetOrdinal("CaseID")),
                                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                                ChequeNumber = reader.IsDBNull(reader.GetOrdinal("ChequeNumber")) 
                                    ? null : reader.GetString(reader.GetOrdinal("ChequeNumber")),
                                ChequeDate = reader.IsDBNull(reader.GetOrdinal("ChequeDate")) 
                                    ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("ChequeDate")),
                                PaymentType = reader.IsDBNull(reader.GetOrdinal("PaymentType")) 
                                    ? null : reader.GetString(reader.GetOrdinal("PaymentType")),
                                Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) 
                                    ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                                CreatedDate = reader.IsDBNull(reader.GetOrdinal("CreatedDate"))
                                    ? DateTime.MinValue
                                    : reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) 
                                    ? null : reader.GetString(reader.GetOrdinal("CreatedBy"))
                            });
                        }
                    }
                }
            }

            return payments;
        }

        public int AddPayment(CasePaymentViewModel payment)
        {
            int paymentId = 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    INSERT INTO MVC_CASE_PAYMENTS 
                        (CaseID, Amount, ChequeNumber, ChequeDate, PaymentType, Remarks, CreatedBy, CreatedDate)
                    VALUES 
                        (@CaseID, @Amount, @ChequeNumber, @ChequeDate, @PaymentType, @Remarks, @CreatedBy, @CreatedDate);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CaseID", payment.CaseID);
                    cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                    cmd.Parameters.AddWithValue("@ChequeNumber", (object?)payment.ChequeNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ChequeDate", (object?)payment.ChequeDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PaymentType", (object?)payment.PaymentType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", (object?)payment.Remarks ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", (object?)payment.CreatedBy ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedDate", payment.CreatedDate);

                    paymentId = (int)cmd.ExecuteScalar();
                }
            }

            return paymentId;
        }

        public bool UpdatePayment(CasePaymentViewModel payment)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    UPDATE MVC_CASE_PAYMENTS 
                    SET Amount = @Amount,
                        ChequeNumber = @ChequeNumber,
                        ChequeDate = @ChequeDate,
                        PaymentType = @PaymentType,
                        Remarks = @Remarks
                    WHERE PaymentID = @PaymentID";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PaymentID", payment.PaymentID);
                    cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                    cmd.Parameters.AddWithValue("@ChequeNumber", (object?)payment.ChequeNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ChequeDate", (object?)payment.ChequeDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PaymentType", (object?)payment.PaymentType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", (object?)payment.Remarks ?? DBNull.Value);

                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public bool DeletePayment(int paymentId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "DELETE FROM MVC_CASE_PAYMENTS WHERE PaymentID = @PaymentID";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PaymentID", paymentId);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public decimal GetTotalPaymentsByCaseId(int caseId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT ISNULL(SUM(Amount), 0) 
                    FROM MVC_CASE_PAYMENTS 
                    WHERE CaseID = @CaseID";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CaseID", caseId);
                    var result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                }
            }
        }
    }
}
