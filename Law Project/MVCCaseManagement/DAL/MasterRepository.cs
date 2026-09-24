using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IMasterRepository
    {
        IEnumerable<Division> GetAllDivisions();
        IEnumerable<MACT> GetAllMACTs();
        MACT? GetMACTById(int id);
        void AddMACT(MACT mact);
        void UpdateMACT(MACT mact);
        void DeleteMACT(int id);

        IEnumerable<Advocate> GetAllAdvocates();
        Advocate? GetAdvocateById(int id);
        void AddAdvocate(Advocate advocate);
        void UpdateAdvocate(Advocate advocate);
        void DeleteAdvocate(int id);
        IEnumerable<Role> GetAllRoles();
        IEnumerable<HighCourtAdvocate> GetHighCourtAdvocates();
        HighCourtAdvocate? GetHighCourtAdvocateById(int id);
        void AddHighCourtAdvocate(HighCourtAdvocate advocate);
        void UpdateHighCourtAdvocate(HighCourtAdvocate advocate);
        void DeleteHighCourtAdvocate(int id);

        // Gratuity Masters
        IEnumerable<GratuityCourt> GetAllGratuityCourts();
        GratuityCourt? GetGratuityCourtById(int id);
        void AddGratuityCourt(GratuityCourt court);
        void UpdateGratuityCourt(GratuityCourt court);
        void DeleteGratuityCourt(int id);

        IEnumerable<GratuityAdvocate> GetAllGratuityAdvocates();
        GratuityAdvocate? GetGratuityAdvocateById(int id);
        void AddGratuityAdvocate(GratuityAdvocate advocate);
        void UpdateGratuityAdvocate(GratuityAdvocate advocate);
        void DeleteGratuityAdvocate(int id);
        
        // Labour Masters
        IEnumerable<LabourCourt> GetAllLabourCourts();
        LabourCourt? GetLabourCourtById(int id);
        void AddLabourCourt(LabourCourt court);
        void UpdateLabourCourt(LabourCourt court);
        void DeleteLabourCourt(int id);

        IEnumerable<LabourAdvocate> GetAllLabourAdvocates();
        LabourAdvocate? GetLabourAdvocateById(int id);
        void AddLabourAdvocate(LabourAdvocate advocate);
        void UpdateLabourAdvocate(LabourAdvocate advocate);
        void DeleteLabourAdvocate(int id);

        // Global Case Deletion
        int DeleteMVCRecord(string mvcNo, int mvcYear, int mactId);
        int DeleteGratuityRecord(string pgaNumber, int caseYear, string? courtName = null);
        int DeleteLabourRecord(string caseNumber, int caseYear, string? courtName = null);
        int DeleteCaseByNumberAndYear(string caseNumber, int caseYear);
    }

    public class MasterRepository : IMasterRepository
    {
        private readonly DBHelper _db;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache? _cache;

        public MasterRepository(DBHelper db, Microsoft.Extensions.Caching.Memory.IMemoryCache? cache = null)
        {
            _db = db;
            _cache = cache;
        }

        public IEnumerable<Division> GetAllDivisions()
        {
            const string cacheKey = "MASTER_DIVISIONS_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<Division>? cached) && cached != null)
            {
                return cached;
            }

            var divisions = new List<Division>();
            string query = "SELECT DivisionID, DivisionCode, DivisionNameEnglish, DivisionNameKannada, IsActive FROM DIVISION_MASTER WHERE IsActive = 1 ORDER BY DivisionNameEnglish";

            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                divisions.Add(new Division
                {
                    DivisionID = row["DivisionID"] != DBNull.Value ? (int)row["DivisionID"] : 0,
                    DivisionCode = row["DivisionCode"]?.ToString() ?? "",
                    DivisionNameEnglish = row["DivisionNameEnglish"]?.ToString() ?? "",
                    DivisionNameKannada = row["DivisionNameKannada"]?.ToString(),
                    IsActive = row["IsActive"] != DBNull.Value && (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, divisions, TimeSpan.FromMinutes(30));
            return divisions;
        }

        // MACT Methods
        public IEnumerable<MACT> GetAllMACTs()
        {
            const string cacheKey = "MASTER_MACTS_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<MACT>? cached) && cached != null)
            {
                return cached;
            }

            var macts = new List<MACT>();
            string query = "SELECT MACTID, MACTName, Location, IsActive FROM MACT_MASTER WHERE IsActive = 1 ORDER BY MACTName";

            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                macts.Add(new MACT
                {
                    MACTID = row["MACTID"] != DBNull.Value ? (int)row["MACTID"] : 0,
                    MACTName = row["MACTName"]?.ToString() ?? "",
                    Location = row["Location"]?.ToString(),
                    IsActive = row["IsActive"] != DBNull.Value && (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, macts, TimeSpan.FromMinutes(30));
            return macts;
        }

        public MACT? GetMACTById(int id)
        {
            string query = "SELECT * FROM MACT_MASTER WHERE MACTID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new MACT
            {
                MACTID = (int)row["MACTID"],
                MACTCode = row["MACTCode"]?.ToString() ?? "",
                MACTName = row["MACTName"]?.ToString() ?? "",
                Location = row["Location"]?.ToString(),
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddMACT(MACT mact)
        {
            // Auto-generate Code if not provided
            if (string.IsNullOrEmpty(mact.MACTCode)) mact.MACTCode = "M" + new Random().Next(1000, 9999);

            string query = "INSERT INTO MACT_MASTER (MACTCode, MACTName, Location, IsActive) VALUES (@Code, @Name, @Loc, 1)";
            var parameters = new[] {
                new SqlParameter("@Code", mact.MACTCode),
                new SqlParameter("@Name", mact.MACTName),
                new SqlParameter("@Loc", mact.Location ?? (object)DBNull.Value)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_MACTS_ACTIVE");
        }

        public void UpdateMACT(MACT mact)
        {
            string query = "UPDATE MACT_MASTER SET MACTName = @Name, Location = @Loc WHERE MACTID = @Id";
            var parameters = new[] {
                new SqlParameter("@Name", mact.MACTName),
                new SqlParameter("@Loc", mact.Location ?? (object)DBNull.Value),
                new SqlParameter("@Id", mact.MACTID)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_MACTS_ACTIVE");
        }

        public void DeleteMACT(int id)
        {
            string query = "UPDATE MACT_MASTER SET IsActive = 0 WHERE MACTID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_MACTS_ACTIVE");
        }

        // Advocate Methods
        public IEnumerable<Advocate> GetAllAdvocates()
        {
            const string cacheKey = "MASTER_ADVOCATES_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<Advocate>? cached) && cached != null)
            {
                return cached;
            }

            var advocates = new List<Advocate>();
            // Use MVC_DIVISION_ADVOCATES
            // Schema: Id, DivisionName, AdvocateName
            string query = "SELECT Id, AdvocateName, DivisionName, 1 as IsActive FROM MVC_DIVISION_ADVOCATES ORDER BY AdvocateName";

            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                advocates.Add(new Advocate
                {
                    AdvocateID = row["Id"] != DBNull.Value ? (int)row["Id"] : 0,
                    AdvocateName = row["AdvocateName"]?.ToString() ?? "",
                    Bench = row["DivisionName"]?.ToString(),
                    IsActive = true
                });
            }

            _cache?.Set(cacheKey, advocates, TimeSpan.FromMinutes(30));
            return advocates;
        }

        public Advocate? GetAdvocateById(int id)
        {
            string query = "SELECT Id, AdvocateName, DivisionName FROM MVC_DIVISION_ADVOCATES WHERE Id = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new Advocate
            {
                AdvocateID = (int)row["Id"],
                AdvocateName = row["AdvocateName"]?.ToString() ?? "",
                Bench = row["DivisionName"]?.ToString(),
                IsActive = true
            };
        }

        public void AddAdvocate(Advocate advocate)
        {
            // Default DivisionName if Bench is null
            string division = advocate.Bench ?? "Dharwad Division"; 
            string query = "INSERT INTO MVC_DIVISION_ADVOCATES (AdvocateName, DivisionName) VALUES (@Name, @Division)";
            var parameters = new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Division", division)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_ADVOCATES_ACTIVE");
        }

        public void UpdateAdvocate(Advocate advocate)
        {
            string query = "UPDATE MVC_DIVISION_ADVOCATES SET AdvocateName = @Name, DivisionName = @Division WHERE Id = @Id";
            var parameters = new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Division", advocate.Bench ?? (object)DBNull.Value),
                new SqlParameter("@Id", advocate.AdvocateID)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_ADVOCATES_ACTIVE");
        }

        public void DeleteAdvocate(int id)
        {
            // Hard delete since there is no IsActive column
            string query = "DELETE FROM MVC_DIVISION_ADVOCATES WHERE Id = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_ADVOCATES_ACTIVE");
        }

        public IEnumerable<Role> GetAllRoles()
        {
            var roles = new List<Role>();
            string query = "SELECT RoleID, RoleName FROM ROLE_MASTER WHERE IsActive = 1 ORDER BY RoleName"; // Assuming ROLE_MASTER has IsActive or just fetch all
            
            // Checking table definition for ROLE_MASTER in previous context, it has IsActive.
            
            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                roles.Add(new Role
                {
                    RoleID = (int)row["RoleID"],
                    RoleName = row["RoleName"].ToString() ?? ""
                });
            }
            return roles;
        }

        public IEnumerable<HighCourtAdvocate> GetHighCourtAdvocates()
        {
            const string cacheKey = "MASTER_HC_ADVOCATES_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<HighCourtAdvocate>? cached) && cached != null)
            {
                return cached;
            }

            var advocates = new List<HighCourtAdvocate>();
            string query = "SELECT AdvocateID, AdvocateName, Bench, IsActive FROM HIGH_COURT_ADVOCATES WHERE IsActive = 1 ORDER BY AdvocateName";

            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                advocates.Add(new HighCourtAdvocate
                {
                    AdvocateID = (int)row["AdvocateID"],
                    AdvocateName = row["AdvocateName"].ToString() ?? "",
                    Bench = row["Bench"]?.ToString(),
                    IsActive = (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, advocates, TimeSpan.FromMinutes(30));
            return advocates;
        }

        public HighCourtAdvocate? GetHighCourtAdvocateById(int id)
        {
            string query = "SELECT * FROM HIGH_COURT_ADVOCATES WHERE AdvocateID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new HighCourtAdvocate
            {
                AdvocateID = (int)row["AdvocateID"],
                AdvocateName = row["AdvocateName"].ToString() ?? "",
                Bench = row["Bench"]?.ToString(),
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddHighCourtAdvocate(HighCourtAdvocate advocate)
        {
            string query = "INSERT INTO HIGH_COURT_ADVOCATES (AdvocateName, Bench, IsActive) VALUES (@Name, @Bench, 1)";
            var parameters = new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Bench", advocate.Bench ?? (object)DBNull.Value)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_HC_ADVOCATES_ACTIVE");
        }

        public void UpdateHighCourtAdvocate(HighCourtAdvocate advocate)
        {
            string query = "UPDATE HIGH_COURT_ADVOCATES SET AdvocateName = @Name, Bench = @Bench WHERE AdvocateID = @Id";
            var parameters = new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Bench", advocate.Bench ?? (object)DBNull.Value),
                new SqlParameter("@Id", advocate.AdvocateID)
            };
            _db.ExecuteNonQuery(query, parameters);
            _cache?.Remove("MASTER_HC_ADVOCATES_ACTIVE");
        }

        public void DeleteHighCourtAdvocate(int id)
        {
            string query = "UPDATE HIGH_COURT_ADVOCATES SET IsActive = 0 WHERE AdvocateID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_HC_ADVOCATES_ACTIVE");
        }

        // --- Gratuity Court Methods ---
        public IEnumerable<GratuityCourt> GetAllGratuityCourts()
        {
            const string cacheKey = "MASTER_GRATUITY_COURTS_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<GratuityCourt>? cached) && cached != null)
            {
                return cached;
            }

            var courts = new List<GratuityCourt>();
            string query = "SELECT CourtID, CourtName, Location, IsActive FROM GRATUITY_COURT_MASTER WHERE IsActive = 1 ORDER BY CourtName";
            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                courts.Add(new GratuityCourt {
                    CourtID = (int)row["CourtID"],
                    CourtName = row["CourtName"].ToString() ?? "",
                    Location = row["Location"]?.ToString(),
                    IsActive = (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, courts, TimeSpan.FromMinutes(30));
            return courts;
        }

        public GratuityCourt? GetGratuityCourtById(int id)
        {
            string query = "SELECT * FROM GRATUITY_COURT_MASTER WHERE CourtID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];
            return new GratuityCourt {
                CourtID = (int)row["CourtID"],
                CourtName = row["CourtName"].ToString() ?? "",
                Location = row["Location"]?.ToString(),
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddGratuityCourt(GratuityCourt court)
        {
            string query = "INSERT INTO GRATUITY_COURT_MASTER (CourtName, Location, IsActive) VALUES (@Name, @Loc, 1)";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", court.CourtName),
                new SqlParameter("@Loc", court.Location ?? (object)DBNull.Value)
            });
            _cache?.Remove("MASTER_GRATUITY_COURTS_ACTIVE");
        }

        public void UpdateGratuityCourt(GratuityCourt court)
        {
            string query = "UPDATE GRATUITY_COURT_MASTER SET CourtName = @Name, Location = @Loc WHERE CourtID = @Id";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", court.CourtName),
                new SqlParameter("@Loc", court.Location ?? (object)DBNull.Value),
                new SqlParameter("@Id", court.CourtID)
            });
            _cache?.Remove("MASTER_GRATUITY_COURTS_ACTIVE");
        }

        public void DeleteGratuityCourt(int id)
        {
            string query = "UPDATE GRATUITY_COURT_MASTER SET IsActive = 0 WHERE CourtID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_GRATUITY_COURTS_ACTIVE");
        }

        // --- Gratuity Advocate Methods ---
        public IEnumerable<GratuityAdvocate> GetAllGratuityAdvocates()
        {
            const string cacheKey = "MASTER_GRATUITY_ADVOCATES_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<GratuityAdvocate>? cached) && cached != null)
            {
                return cached;
            }

            var advocates = new List<GratuityAdvocate>();
            string query = "SELECT AdvocateID, AdvocateName, Specialization, IsActive FROM GRATUITY_ADVOCATE_MASTER WHERE IsActive = 1 ORDER BY AdvocateName";
            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                advocates.Add(new GratuityAdvocate {
                    AdvocateID = (int)row["AdvocateID"],
                    AdvocateName = row["AdvocateName"].ToString() ?? "",
                    Specialization = row["Specialization"]?.ToString(),
                    IsActive = (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, advocates, TimeSpan.FromMinutes(30));
            return advocates;
        }

        public GratuityAdvocate? GetGratuityAdvocateById(int id)
        {
            string query = "SELECT * FROM GRATUITY_ADVOCATE_MASTER WHERE AdvocateID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];
            return new GratuityAdvocate {
                AdvocateID = (int)row["AdvocateID"],
                AdvocateName = row["AdvocateName"].ToString() ?? "",
                Specialization = row["Specialization"]?.ToString(),
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddGratuityAdvocate(GratuityAdvocate advocate)
        {
            string query = "INSERT INTO GRATUITY_ADVOCATE_MASTER (AdvocateName, Specialization, IsActive) VALUES (@Name, @Spec, 1)";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Spec", advocate.Specialization ?? (object)DBNull.Value)
            });
            _cache?.Remove("MASTER_GRATUITY_ADVOCATES_ACTIVE");
        }

        public void UpdateGratuityAdvocate(GratuityAdvocate advocate)
        {
            string query = "UPDATE GRATUITY_ADVOCATE_MASTER SET AdvocateName = @Name, Specialization = @Spec WHERE AdvocateID = @Id";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Spec", advocate.Specialization ?? (object)DBNull.Value),
                new SqlParameter("@Id", advocate.AdvocateID)
            });
            _cache?.Remove("MASTER_GRATUITY_ADVOCATES_ACTIVE");
        }

        public void DeleteGratuityAdvocate(int id)
        {
            string query = "UPDATE GRATUITY_ADVOCATE_MASTER SET IsActive = 0 WHERE AdvocateID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_GRATUITY_ADVOCATES_ACTIVE");
        }

        // --- Labour Court Methods ---
        public IEnumerable<LabourCourt> GetAllLabourCourts()
        {
            const string cacheKey = "MASTER_LABOUR_COURTS_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<LabourCourt>? cached) && cached != null)
            {
                return cached;
            }

            var list = new List<LabourCourt>();
            string query = "SELECT * FROM LABOUR_COURTS WHERE IsActive = 1 ORDER BY CourtName";
            DataTable dt = _db.ExecuteQuery(query);
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

            _cache?.Set(cacheKey, list, TimeSpan.FromMinutes(30));
            return list;
        }

        public LabourCourt? GetLabourCourtById(int id)
        {
            string query = "SELECT * FROM LABOUR_COURTS WHERE CourtID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];
            return new LabourCourt
            {
                CourtID = Convert.ToInt32(row["CourtID"]),
                CourtName = row["CourtName"].ToString()!,
                Location = row["Location"]?.ToString(),
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddLabourCourt(LabourCourt court)
        {
            string query = "INSERT INTO LABOUR_COURTS (CourtName, Location, IsActive) VALUES (@Name, @Loc, 1)";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", court.CourtName),
                new SqlParameter("@Loc", court.Location ?? (object)DBNull.Value)
            });
            _cache?.Remove("MASTER_LABOUR_COURTS_ACTIVE");
        }

        public void UpdateLabourCourt(LabourCourt court)
        {
            string query = "UPDATE LABOUR_COURTS SET CourtName = @Name, Location = @Loc WHERE CourtID = @Id";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", court.CourtName),
                new SqlParameter("@Loc", court.Location ?? (object)DBNull.Value),
                new SqlParameter("@Id", court.CourtID)
            });
            _cache?.Remove("MASTER_LABOUR_COURTS_ACTIVE");
        }

        public void DeleteLabourCourt(int id)
        {
            string query = "UPDATE LABOUR_COURTS SET IsActive = 0 WHERE CourtID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_LABOUR_COURTS_ACTIVE");
        }

        // --- Labour Advocate Methods ---
        public IEnumerable<LabourAdvocate> GetAllLabourAdvocates()
        {
            const string cacheKey = "MASTER_LABOUR_ADVOCATES_ACTIVE";
            if (_cache != null && _cache.TryGetValue(cacheKey, out List<LabourAdvocate>? cached) && cached != null)
            {
                return cached;
            }

            var list = new List<LabourAdvocate>();
            string query = "SELECT * FROM LABOUR_ADVOCATES WHERE IsActive = 1 ORDER BY AdvocateName";
            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LabourAdvocate
                {
                    AdvocateID = Convert.ToInt32(row["AdvocateID"]),
                    AdvocateName = row["AdvocateName"].ToString()!,
                    IsActive = (bool)row["IsActive"]
                });
            }

            _cache?.Set(cacheKey, list, TimeSpan.FromMinutes(30));
            return list;
        }

        public LabourAdvocate? GetLabourAdvocateById(int id)
        {
            string query = "SELECT * FROM LABOUR_ADVOCATES WHERE AdvocateID = @Id";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Id", id) });
            if (dt.Rows.Count == 0) return null;
            DataRow row = dt.Rows[0];
            return new LabourAdvocate
            {
                AdvocateID = Convert.ToInt32(row["AdvocateID"]),
                AdvocateName = row["AdvocateName"].ToString()!,
                IsActive = (bool)row["IsActive"]
            };
        }

        public void AddLabourAdvocate(LabourAdvocate advocate)
        {
            string query = "INSERT INTO LABOUR_ADVOCATES (AdvocateName, IsActive) VALUES (@Name, 1)";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", advocate.AdvocateName)
            });
            _cache?.Remove("MASTER_LABOUR_ADVOCATES_ACTIVE");
        }

        public void UpdateLabourAdvocate(LabourAdvocate advocate)
        {
            string query = "UPDATE LABOUR_ADVOCATES SET AdvocateName = @Name WHERE AdvocateID = @Id";
            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@Name", advocate.AdvocateName),
                new SqlParameter("@Id", advocate.AdvocateID)
            });
            _cache?.Remove("MASTER_LABOUR_ADVOCATES_ACTIVE");
        }

        public void DeleteLabourAdvocate(int id)
        {
            string query = "UPDATE LABOUR_ADVOCATES SET IsActive = 0 WHERE AdvocateID = @Id";
            _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", id) });
            _cache?.Remove("MASTER_LABOUR_ADVOCATES_ACTIVE");
        }

        public int DeleteMVCRecord(string mvcNo, int mvcYear, int mactId)
        {
            int totalDeleted = 0;
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string query = mactId > 0 
                            ? "SELECT CaseID FROM MVC_CASES WHERE MVCNo = @No AND MVCYear = @Year AND MACTID = @MactID"
                            : "SELECT CaseID FROM MVC_CASES WHERE MVCNo = @No AND MVCYear = @Year";
                        
                        var cmd = new SqlCommand(query, conn, trans);
                        cmd.Parameters.AddWithValue("@No", mvcNo);
                        cmd.Parameters.AddWithValue("@Year", mvcYear);
                        if (mactId > 0) cmd.Parameters.AddWithValue("@MactID", mactId);

                        var ids = new List<int>();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) ids.Add(reader.GetInt32(0));
                        }

                        foreach (int id in ids)
                        {
                            DeleteMVCChildRecords(id, conn, trans);
                            var delCmd = new SqlCommand("DELETE FROM MVC_CASES WHERE CaseID = @Id", conn, trans);
                            delCmd.Parameters.AddWithValue("@Id", id);
                            totalDeleted += delCmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
            return totalDeleted;
        }

        public int DeleteGratuityRecord(string pgaNumber, int caseYear, string? courtName = null)
        {
            int totalDeleted = 0;
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string query;
                        if (!string.IsNullOrEmpty(courtName))
                        {
                            query = @"SELECT CaseID FROM GRA_CASES 
                                     WHERE (PGANumber = @No OR PGANumber = @No + '/' + CAST(@Year AS VARCHAR) OR PGANumber LIKE @No + '%' + CAST(@Year AS VARCHAR)) 
                                     AND (CourtName LIKE @Court OR ControlingAuthority LIKE @Court)";
                        }
                        else
                        {
                            query = @"SELECT CaseID FROM GRA_CASES 
                                     WHERE (PGANumber = @No OR PGANumber = @No + '/' + CAST(@Year AS VARCHAR) OR PGANumber LIKE @No + '%' + CAST(@Year AS VARCHAR))";
                        }
                        
                        var cmd = new SqlCommand(query, conn, trans);
                        cmd.Parameters.AddWithValue("@No", pgaNumber);
                        cmd.Parameters.AddWithValue("@Year", caseYear);
                        if (!string.IsNullOrEmpty(courtName)) cmd.Parameters.AddWithValue("@Court", "%" + courtName + "%");

                        var ids = new List<int>();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) ids.Add(reader.GetInt32(0));
                        }

                        foreach (int id in ids)
                        {
                            DeleteGratuityChildRecords(id, conn, trans);
                            var delCmd = new SqlCommand("DELETE FROM GRA_CASES WHERE CaseID = @Id", conn, trans);
                            delCmd.Parameters.AddWithValue("@Id", id);
                            totalDeleted += delCmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
            return totalDeleted;
        }

        public int DeleteLabourRecord(string caseNumber, int caseYear, string? courtName = null)
        {
            int totalDeleted = 0;
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string query = !string.IsNullOrEmpty(courtName)
                            ? @"SELECT c.CaseID FROM LABOUR_CASES c 
                               LEFT JOIN LABOUR_COURTS ct ON c.CourtID = ct.CourtID 
                               WHERE (c.CaseNumber = @No OR c.SerialApp_CaseNumber = @No) AND c.CaseYear = @Year 
                               AND (ct.CourtName LIKE @Court OR c.OtherCourtDetails LIKE @Court)"
                            : "SELECT CaseID FROM LABOUR_CASES WHERE (CaseNumber = @No OR SerialApp_CaseNumber = @No) AND CaseYear = @Year";
                        
                        var cmd = new SqlCommand(query, conn, trans);
                        cmd.Parameters.AddWithValue("@No", caseNumber);
                        cmd.Parameters.AddWithValue("@Year", caseYear);
                        if (!string.IsNullOrEmpty(courtName)) cmd.Parameters.AddWithValue("@Court", "%" + courtName + "%");

                        var ids = new List<int>();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) ids.Add(reader.GetInt32(0));
                        }

                        foreach (int id in ids)
                        {
                            DeleteLabourChildRecords(id, conn, trans);
                            var delCmd = new SqlCommand("DELETE FROM LABOUR_CASES WHERE CaseID = @Id", conn, trans);
                            delCmd.Parameters.AddWithValue("@Id", id);
                            totalDeleted += delCmd.ExecuteNonQuery();
                        }

                        // Also delete from LABOUR_ARISING_APPLICATIONS
                        var delArisingCmd = new SqlCommand(@"DELETE FROM LABOUR_ARISING_APPLICATIONS 
                            WHERE (CaseNumber = @No OR ArisingNumber = @No) AND (CaseYear = @Year OR ArisingYear = @Year)", conn, trans);
                        delArisingCmd.Parameters.AddWithValue("@No", caseNumber);
                        delArisingCmd.Parameters.AddWithValue("@Year", caseYear);
                        totalDeleted += delArisingCmd.ExecuteNonQuery();

                        trans.Commit();
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
            return totalDeleted;
        }

        public int DeleteCaseByNumberAndYear(string caseNumber, int caseYear)
        {
            int totalDeleted = 0;
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. MVC
                        var cmd1 = new SqlCommand("SELECT CaseID FROM MVC_CASES WHERE MVCNo = @No AND MVCYear = @Year", conn, trans);
                        cmd1.Parameters.AddWithValue("@No", caseNumber);
                        cmd1.Parameters.AddWithValue("@Year", caseYear);
                        var ids1 = new List<int>();
                        using (var r = cmd1.ExecuteReader()) { while (r.Read()) ids1.Add(r.GetInt32(0)); }
                        foreach (int id in ids1) { DeleteMVCChildRecords(id, conn, trans); totalDeleted += new SqlCommand($"DELETE FROM MVC_CASES WHERE CaseID = {id}", conn, trans).ExecuteNonQuery(); }

                        // 2. Gratuity
                        var cmd2 = new SqlCommand("SELECT CaseID FROM GRA_CASES WHERE PGANumber = @No", conn, trans);
                        cmd2.Parameters.AddWithValue("@No", caseNumber);
                        var ids2 = new List<int>();
                        using (var r = cmd2.ExecuteReader()) { while (r.Read()) ids2.Add(r.GetInt32(0)); }
                        foreach (int id in ids2) { DeleteGratuityChildRecords(id, conn, trans); totalDeleted += new SqlCommand($"DELETE FROM GRA_CASES WHERE CaseID = {id}", conn, trans).ExecuteNonQuery(); }

                        // 3. Labour
                        var cmd3 = new SqlCommand("SELECT CaseID FROM LABOUR_CASES WHERE (CaseNumber = @No OR SerialApp_CaseNumber = @No) AND CaseYear = @Year", conn, trans);
                        cmd3.Parameters.AddWithValue("@No", caseNumber);
                        cmd3.Parameters.AddWithValue("@Year", caseYear);
                        var ids3 = new List<int>();
                        using (var r = cmd3.ExecuteReader()) { while (r.Read()) ids3.Add(r.GetInt32(0)); }
                        foreach (int id in ids3) { DeleteLabourChildRecords(id, conn, trans); totalDeleted += new SqlCommand($"DELETE FROM LABOUR_CASES WHERE CaseID = {id}", conn, trans).ExecuteNonQuery(); }

                        // 3b. Labour Arising Applications
                        var cmdArising = new SqlCommand(@"DELETE FROM LABOUR_ARISING_APPLICATIONS 
                            WHERE (CaseNumber = @No OR ArisingNumber = @No) AND (CaseYear = @Year OR ArisingYear = @Year)", conn, trans);
                        cmdArising.Parameters.AddWithValue("@No", caseNumber);
                        cmdArising.Parameters.AddWithValue("@Year", caseYear);
                        totalDeleted += cmdArising.ExecuteNonQuery();

                        trans.Commit();
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
            return totalDeleted;
        }

        private void DeleteMVCChildRecords(int id, SqlConnection conn, SqlTransaction trans)
        {
            string[] tables = { "MVC_CASE_PAYMENTS", "MVC_CASE_ADVERSE_DOCS", "MVC_EP_DETAILS", "APPEAL_CONNECTED", "APPEAL_DETAILS", "CASE_VIEW_TRACKING", "MVC_CASE_ADVERSE_CONNECTED", "MVC_CASE_ADVERSE_DETAILS", "MVC_CASE_ADVERSE_PW", "MVC_CASE_ADVERSE_RW", "MVC_CASE_CONNECTED", "MVC_CASE_PETITIONERS" };
            foreach (var tbl in tables)
            {
                var cmd = new SqlCommand($"DELETE FROM {tbl} WHERE CaseID = @Id", conn, trans);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private void DeleteGratuityChildRecords(int id, SqlConnection conn, SqlTransaction trans)
        {
            string[] tables = { "GRA_INTEREST_PAYMENTS", "GRA_PAYMENTS" };
            foreach (var tbl in tables)
            {
                var cmd = new SqlCommand($"DELETE FROM {tbl} WHERE CaseID = @Id", conn, trans);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private void DeleteLabourChildRecords(int id, SqlConnection conn, SqlTransaction trans)
        {
            string[] tables = { "LABOUR_CASE_HISTORY", "LABOUR_CASE_EVIDENCE", "LABOUR_SERVICE_MATTERS", "LABOUR_CONNECTED_CASES", "LABOUR_CASE_VIEW_TRACKING", "LABOUR_REINSTATED_DOCUMENTS" };
            foreach (var tbl in tables)
            {
                var cmd = new SqlCommand($"DELETE FROM {tbl} WHERE CaseID = @Id", conn, trans);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            var arisingCmd = new SqlCommand("DELETE FROM LABOUR_ARISING_APPLICATIONS WHERE ParentCaseID = @Id", conn, trans);
            arisingCmd.Parameters.AddWithValue("@Id", id);
            arisingCmd.ExecuteNonQuery();
        }
    }
}
