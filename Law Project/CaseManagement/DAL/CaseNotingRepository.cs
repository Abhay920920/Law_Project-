using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.DAL
{
    public class CaseNotingRepository : ICaseNotingRepository
    {
        private readonly string _connectionString;

        public CaseNotingRepository(DBHelper dbHelper)
        {
            _connectionString = dbHelper.GetConnectionString();
        }

        public List<CaseNoting> GetNotings(string caseType, int caseId)
        {
            var list = new List<CaseNoting>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT NotingID, CaseType, CaseID, NotingText, 
                           CreatedByUsername, CreatedByName, CreatedByRole, CreatedByDivision, 
                           CreatedDate, IsActive
                    FROM CASE_NOTINGS
                    WHERE CaseType = @CaseType AND CaseID = @CaseID AND IsActive = 1
                    ORDER BY CreatedDate ASC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@CaseType", caseType.ToUpperInvariant());
                    cmd.Parameters.AddWithValue("@CaseID", caseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CaseNoting
                            {
                                NotingID = reader.GetInt32(reader.GetOrdinal("NotingID")),
                                CaseType = reader.GetString(reader.GetOrdinal("CaseType")),
                                CaseID = reader.GetInt32(reader.GetOrdinal("CaseID")),
                                NotingText = reader.GetString(reader.GetOrdinal("NotingText")),
                                CreatedByUsername = reader.GetString(reader.GetOrdinal("CreatedByUsername")),
                                CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName")) ? null : reader.GetString(reader.GetOrdinal("CreatedByName")),
                                CreatedByRole = reader.IsDBNull(reader.GetOrdinal("CreatedByRole")) ? null : reader.GetString(reader.GetOrdinal("CreatedByRole")),
                                CreatedByDivision = reader.IsDBNull(reader.GetOrdinal("CreatedByDivision")) ? null : reader.GetString(reader.GetOrdinal("CreatedByDivision")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        public int AddNoting(CaseNoting noting)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO CASE_NOTINGS 
                    (CaseType, CaseID, NotingText, CreatedByUsername, CreatedByName, CreatedByRole, CreatedByDivision, CreatedDate, IsActive)
                    VALUES 
                    (@CaseType, @CaseID, @NotingText, @CreatedByUsername, @CreatedByName, @CreatedByRole, @CreatedByDivision, GETDATE(), 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@CaseType", (noting.CaseType ?? "MVC").ToUpperInvariant());
                    cmd.Parameters.AddWithValue("@CaseID", noting.CaseID);
                    cmd.Parameters.AddWithValue("@NotingText", noting.NotingText ?? string.Empty);
                    cmd.Parameters.AddWithValue("@CreatedByUsername", noting.CreatedByUsername ?? "Unknown");
                    cmd.Parameters.AddWithValue("@CreatedByName", (object?)noting.CreatedByName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedByRole", (object?)noting.CreatedByRole ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedByDivision", (object?)noting.CreatedByDivision ?? DBNull.Value);

                    var result = cmd.ExecuteScalar();
                    return result != null && int.TryParse(result.ToString(), out int id) ? id : 0;
                }
            }
        }

        public CaseNoting? GetNotingById(int notingId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT NotingID, CaseType, CaseID, NotingText, 
                           CreatedByUsername, CreatedByName, CreatedByRole, CreatedByDivision, 
                           CreatedDate, IsActive
                    FROM CASE_NOTINGS
                    WHERE NotingID = @NotingID";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@NotingID", notingId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new CaseNoting
                            {
                                NotingID = reader.GetInt32(reader.GetOrdinal("NotingID")),
                                CaseType = reader.GetString(reader.GetOrdinal("CaseType")),
                                CaseID = reader.GetInt32(reader.GetOrdinal("CaseID")),
                                NotingText = reader.GetString(reader.GetOrdinal("NotingText")),
                                CreatedByUsername = reader.GetString(reader.GetOrdinal("CreatedByUsername")),
                                CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName")) ? null : reader.GetString(reader.GetOrdinal("CreatedByName")),
                                CreatedByRole = reader.IsDBNull(reader.GetOrdinal("CreatedByRole")) ? null : reader.GetString(reader.GetOrdinal("CreatedByRole")),
                                CreatedByDivision = reader.IsDBNull(reader.GetOrdinal("CreatedByDivision")) ? null : reader.GetString(reader.GetOrdinal("CreatedByDivision")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };
                        }
                    }
                }
            }
            return null;
        }

        public bool DeleteNoting(int notingId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE CASE_NOTINGS SET IsActive = 0 WHERE NotingID = @NotingID";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@NotingID", notingId);
                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
        }
    }
}
