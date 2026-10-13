using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class JudgementRepository : IJudgementRepository
    {
        private readonly DBHelper _db;

        public JudgementRepository(DBHelper db)
        {
            _db = db;
        }

        public IEnumerable<JudgementViewModel> GetAllJudgements()
        {
            var list = new List<JudgementViewModel>();
            string query = "SELECT * FROM JUDGEMENT_REPO ORDER BY UploadedDate DESC";
            DataTable dt = _db.ExecuteQuery(query);

            foreach (DataRow dr in dt.Rows)
            {
                list.Add(new JudgementViewModel
                {
                    JudgementID = Convert.ToInt32(dr["JudgementID"]),
                    Title = dr["Title"].ToString()!,
                    Court = dr["Court"]?.ToString(),
                    JudgementDate = dr["JudgementDate"] != DBNull.Value ? Convert.ToDateTime(dr["JudgementDate"]) : null,
                    Remarks = dr["Remarks"]?.ToString(),
                    Category = dr["Category"]?.ToString() ?? "Judgement",
                    FilePath = dr["FilePath"]?.ToString(),
                    UploadedBy = dr["UploadedBy"]?.ToString(),
                    UploadedDate = Convert.ToDateTime(dr["UploadedDate"])
                });
            }
            return list;
        }

        public JudgementViewModel? GetJudgementById(int id)
        {
            string query = "SELECT * FROM JUDGEMENT_REPO WHERE JudgementID = @ID";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ID", id) });

            if (dt.Rows.Count > 0)
            {
                DataRow dr = dt.Rows[0];
                return new JudgementViewModel
                {
                    JudgementID = Convert.ToInt32(dr["JudgementID"]),
                    Title = dr["Title"].ToString()!,
                    Court = dr["Court"]?.ToString(),
                    JudgementDate = dr["JudgementDate"] != DBNull.Value ? Convert.ToDateTime(dr["JudgementDate"]) : null,
                    Remarks = dr["Remarks"]?.ToString(),
                    Category = dr["Category"]?.ToString() ?? "Judgement",
                    FilePath = dr["FilePath"]?.ToString(),
                    UploadedBy = dr["UploadedBy"]?.ToString(),
                    UploadedDate = Convert.ToDateTime(dr["UploadedDate"])
                };
            }
            return null;
        }

        public void SaveJudgement(JudgementViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                string query = @"INSERT INTO JUDGEMENT_REPO (Title, Court, JudgementDate, Remarks, Category, FilePath, UploadedBy)
                               VALUES (@Title, @Court, @JDate, @Remarks, @Category, @FilePath, @UploadedBy)";
                
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", model.Title);
                    cmd.Parameters.AddWithValue("@Court", model.Court ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@JDate", model.JudgementDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Category", model.Category ?? "Judgement");
                    cmd.Parameters.AddWithValue("@FilePath", model.FilePath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@UploadedBy", model.UploadedBy ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateJudgement(JudgementViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                string query = @"UPDATE JUDGEMENT_REPO
                                 SET Title = @Title,
                                     Court = @Court,
                                     JudgementDate = @JDate,
                                     Remarks = @Remarks,
                                     Category = @Category,
                                     FilePath = @FilePath
                                 WHERE JudgementID = @JudgementID";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@JudgementID", model.JudgementID);
                    cmd.Parameters.AddWithValue("@Title", model.Title);
                    cmd.Parameters.AddWithValue("@Court", model.Court ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@JDate", model.JudgementDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Category", model.Category ?? "Judgement");
                    cmd.Parameters.AddWithValue("@FilePath", model.FilePath ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteJudgement(int id)
        {
            string query = "DELETE FROM JUDGEMENT_REPO WHERE JudgementID = @ID";
            _db.ExecuteQuery(query, new[] { new SqlParameter("@ID", id) });
        }
    }
}
