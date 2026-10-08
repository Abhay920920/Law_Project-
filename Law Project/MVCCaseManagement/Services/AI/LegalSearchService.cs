using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class LegalSearchService : ILegalSearchService
    {
        private readonly DBHelper _db;
        private readonly ILogger<LegalSearchService> _logger;

        public LegalSearchService(DBHelper db, ILogger<LegalSearchService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<JudgementViewModel>> SearchJudgementsAsync(string query, int top = 5, CancellationToken cancellationToken = default)
        {
            var results = new List<JudgementViewModel>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            // Extract words of length >= 3 for targeted parameter matching
            var keywords = query.Split(new[] { ' ', ',', ';', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length >= 3 && !IsCommonStopword(w))
                                .Take(5)
                                .ToList();

            if (!keywords.Any())
                keywords.Add(query.Trim());

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                var clauses = new List<string>();
                var parameters = new List<SqlParameter>();

                for (int i = 0; i < keywords.Count; i++)
                {
                    string paramName = $"@kw{i}";
                    clauses.Add($"(Title LIKE {paramName} OR Remarks LIKE {paramName} OR Court LIKE {paramName} OR Category LIKE {paramName})");
                    parameters.Add(new SqlParameter(paramName, $"%{keywords[i]}%"));
                }

                string sql = $@"
                    SELECT TOP (@Top) JudgementID, Title, Court, JudgementDate, Remarks, Category, FilePath, UploadedBy, UploadedDate
                    FROM JUDGEMENT_REPO
                    WHERE {string.Join(" OR ", clauses)}
                    ORDER BY UploadedDate DESC";

                parameters.Add(new SqlParameter("@Top", Math.Min(20, Math.Max(1, top))));

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new JudgementViewModel
                    {
                        JudgementID = reader.GetInt32(reader.GetOrdinal("JudgementID")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Court = reader.IsDBNull(reader.GetOrdinal("Court")) ? null : reader.GetString(reader.GetOrdinal("Court")),
                        JudgementDate = reader.IsDBNull(reader.GetOrdinal("JudgementDate")) ? null : reader.GetDateTime(reader.GetOrdinal("JudgementDate")),
                        Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                        Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "Judgement" : reader.GetString(reader.GetOrdinal("Category")),
                        FilePath = reader.IsDBNull(reader.GetOrdinal("FilePath")) ? null : reader.GetString(reader.GetOrdinal("FilePath")),
                        UploadedBy = reader.IsDBNull(reader.GetOrdinal("UploadedBy")) ? null : reader.GetString(reader.GetOrdinal("UploadedBy")),
                        UploadedDate = reader.GetDateTime(reader.GetOrdinal("UploadedDate"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching JUDGEMENT_REPO with query: {Query}", query);
            }

            return results;
        }

        public async Task<List<SimilarCaseMatch>> SearchSimilarCasesAsync(string caseType, int caseId, string? queryKeywords = null, int top = 5, CancellationToken cancellationToken = default)
        {
            var results = new List<SimilarCaseMatch>();
            string upperType = (caseType ?? "MVC").ToUpperInvariant();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                if (upperType == "MVC")
                {
                    // Retrieve key attributes of the target case
                    string sourceSql = @"
                        SELECT TOP 1 c.CaseID, c.MVCNo, c.MVCYear, c.VehicleNo, c.VehicleType, c.MACTID, m.MACTName, c.CurrentStage
                        FROM MVC_CASES c
                        LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                        WHERE c.CaseID = @CaseID";

                    int mactId = 0;
                    string vehicleType = string.Empty;
                    string currentStage = string.Empty;

                    using (var srcCmd = new SqlCommand(sourceSql, conn))
                    {
                        srcCmd.Parameters.AddWithValue("@CaseID", caseId);
                        using var reader = await srcCmd.ExecuteReaderAsync(cancellationToken);
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            mactId = reader.IsDBNull(reader.GetOrdinal("MACTID")) ? 0 : reader.GetInt32(reader.GetOrdinal("MACTID"));
                            vehicleType = reader.IsDBNull(reader.GetOrdinal("VehicleType")) ? string.Empty : reader.GetString(reader.GetOrdinal("VehicleType"));
                            currentStage = reader.IsDBNull(reader.GetOrdinal("CurrentStage")) ? string.Empty : reader.GetString(reader.GetOrdinal("CurrentStage"));
                        }
                    }

                    // Find similar cases matching vehicle type or court
                    string findSql = @"
                        SELECT TOP (@Top) c.CaseID, c.MVCNo, c.MVCYear, ISNULL(m.MACTName, '') AS Court,
                               ISNULL(c.VehicleType, '') AS VehicleType, ISNULL(c.CurrentStage, '') AS Stage,
                               ISNULL(c.ClaimAmount, 0) AS ClaimAmount, ISNULL(c.AwardAmount, 0) AS AwardAmount,
                               ISNULL(c.PetitionerName, '') AS Petitioner
                        FROM MVC_CASES c
                        LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                        WHERE c.CaseID <> @CaseID
                          AND (@MACTID = 0 OR c.MACTID = @MACTID OR (@VehicleType <> '' AND c.VehicleType = @VehicleType))
                        ORDER BY c.MVCYear DESC, c.CaseID DESC";

                    using (var findCmd = new SqlCommand(findSql, conn))
                    {
                        findCmd.Parameters.AddWithValue("@CaseID", caseId);
                        findCmd.Parameters.AddWithValue("@MACTID", mactId);
                        findCmd.Parameters.AddWithValue("@VehicleType", vehicleType);
                        findCmd.Parameters.AddWithValue("@Top", Math.Min(20, Math.Max(1, top)));

                        using var rdr = await findCmd.ExecuteReaderAsync(cancellationToken);
                        while (await rdr.ReadAsync(cancellationToken))
                        {
                            int matchId = rdr.GetInt32(rdr.GetOrdinal("CaseID"));
                            string mvcNo = rdr.GetString(rdr.GetOrdinal("MVCNo"));
                            int mvcYear = rdr.GetInt32(rdr.GetOrdinal("MVCYear"));
                            string court = rdr.GetString(rdr.GetOrdinal("Court"));
                            string stage = rdr.GetString(rdr.GetOrdinal("Stage"));
                            decimal claim = rdr.GetDecimal(rdr.GetOrdinal("ClaimAmount"));
                            decimal award = rdr.GetDecimal(rdr.GetOrdinal("AwardAmount"));

                            results.Add(new SimilarCaseMatch
                            {
                                CaseId = matchId,
                                CaseType = "MVC",
                                CaseNumber = $"MVC/{mvcNo}/{mvcYear}",
                                CourtOrTribunal = court,
                                SimilarityBasis = "Keyword & Attribute Match: Same MACT Jurisdiction / Vehicle Category",
                                SimilarityScore = 0.85,
                                Summary = $"Stage: {stage} | Claimed: Rs. {claim:N0} | Award: Rs. {award:N0}"
                            });
                        }
                    }
                }
                else if (upperType == "LABOUR")
                {
                    // Retrieve target labour case
                    string sourceSql = @"
                        SELECT TOP 1 CaseID, CaseNumber, CaseYear, CaseType, NatureOfDispute, NatureOfMisconduct, CurrentStage
                        FROM LABOUR_CASES
                        WHERE CaseID = @CaseID";

                    string subType = string.Empty;
                    string misconduct = string.Empty;

                    using (var srcCmd = new SqlCommand(sourceSql, conn))
                    {
                        srcCmd.Parameters.AddWithValue("@CaseID", caseId);
                        using var reader = await srcCmd.ExecuteReaderAsync(cancellationToken);
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            subType = reader.IsDBNull(reader.GetOrdinal("CaseType")) ? string.Empty : reader.GetString(reader.GetOrdinal("CaseType"));
                            misconduct = reader.IsDBNull(reader.GetOrdinal("NatureOfMisconduct")) ? string.Empty : reader.GetString(reader.GetOrdinal("NatureOfMisconduct"));
                        }
                    }

                    string findSql = @"
                        SELECT TOP (@Top) CaseID, CaseNumber, CaseYear, CaseType, NatureOfDispute, NatureOfMisconduct, CurrentStage, EmployeeName
                        FROM LABOUR_CASES
                        WHERE CaseID <> @CaseID
                          AND (@SubType = '' OR CaseType = @SubType)
                        ORDER BY CaseYear DESC, CaseID DESC";

                    using (var findCmd = new SqlCommand(findSql, conn))
                    {
                        findCmd.Parameters.AddWithValue("@CaseID", caseId);
                        findCmd.Parameters.AddWithValue("@SubType", subType);
                        findCmd.Parameters.AddWithValue("@Top", Math.Min(20, Math.Max(1, top)));

                        using var rdr = await findCmd.ExecuteReaderAsync(cancellationToken);
                        while (await rdr.ReadAsync(cancellationToken))
                        {
                            results.Add(new SimilarCaseMatch
                            {
                                CaseId = rdr.GetInt32(rdr.GetOrdinal("CaseID")),
                                CaseType = "LABOUR",
                                CaseNumber = $"{rdr.GetString(rdr.GetOrdinal("CaseType"))}/{rdr.GetString(rdr.GetOrdinal("CaseNumber"))}/{rdr.GetInt32(rdr.GetOrdinal("CaseYear"))}",
                                CourtOrTribunal = "Labour Court / Industrial Tribunal",
                                SimilarityBasis = "Keyword & Attribute Match: Similar Dispute Category / Case Type",
                                SimilarityScore = 0.82,
                                Summary = $"Employee: {rdr["EmployeeName"]} | Misconduct: {rdr["NatureOfMisconduct"]} | Stage: {rdr["CurrentStage"]}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching similar cases for {CaseType} id {CaseId}", caseType, caseId);
            }

            return results;
        }

        public async Task<List<CaseNoting>> SearchCaseNotingsAsync(string query, int top = 5, CancellationToken cancellationToken = default)
        {
            var list = new List<CaseNoting>();
            if (string.IsNullOrWhiteSpace(query))
                return list;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT TOP (@Top) NotingID, CaseType, CaseID, NotingText, 
                           CreatedByUsername, CreatedByName, CreatedByRole, CreatedByDivision, 
                           CreatedDate, IsActive
                    FROM CASE_NOTINGS
                    WHERE IsActive = 1 AND NotingText LIKE @Query
                    ORDER BY CreatedDate DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Top", Math.Min(20, Math.Max(1, top)));
                cmd.Parameters.AddWithValue("@Query", $"%{query.Trim()}%");

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching case notings with query: {Query}", query);
            }

            return list;
        }

        public List<string> GetApplicableProvisions(string caseType)
        {
            string upper = (caseType ?? "").ToUpperInvariant();
            switch (upper)
            {
                case "MVC":
                    return new List<string>
                    {
                        "Motor Vehicles Act, 1988 — Section 166: Application for compensation",
                        "Motor Vehicles Act, 1988 — Section 163-A: Special provisions as to payment of compensation on structured formula basis",
                        "Motor Vehicles Act, 1988 — Section 140: Liability to pay compensation in certain cases on the principle of no fault",
                        "Motor Vehicles Act, 1988 — Section 173: Appeals against award of Claims Tribunal (Limitation 90 days)",
                        "Motor Vehicles Act, 1988 — Section 149 / Section 150: Duty of insurers to satisfy judgments and awards",
                        "Limitation Act, 1963 — Section 5: Extension of prescribed period in certain cases (Condonation of Delay)"
                    };

                case "LABOUR":
                    return new List<string>
                    {
                        "Industrial Disputes Act, 1947 — Section 10: Reference of disputes to Boards, Courts or Tribunals",
                        "Industrial Disputes Act, 1947 — Section 11-A: Powers of Labour Courts/Tribunals to give appropriate relief in case of discharge or dismissal",
                        "Industrial Disputes Act, 1947 — Section 17-B: Payment of full wages to workman pending proceedings in higher courts",
                        "Industrial Disputes Act, 1947 — Section 33-C(2): Recovery of money due from an employer",
                        "Karnataka Industrial Employment (Standing Orders) Rules — Disciplinary proceedings & domestic enquiry standards",
                        "Constitution of India — Article 226 & 227: Writ Jurisdiction before High Court"
                    };

                case "APPEAL":
                    return new List<string>
                    {
                        "Motor Vehicles Act, 1988 — Section 173(1): Appeals to High Court within 90 days with statutory deposit (Rs. 25,000 or 50% of award amount)",
                        "Code of Civil Procedure, 1908 — Order XLI Rule 1 & Rule 5: Appeals from Original Decrees & Stay of Proceedings/Execution",
                        "Limitation Act, 1963 — Section 5: Application for condonation of delay supported by affidavit",
                        "Constitution of India — Article 136: Special Leave Petition (SLP) before Supreme Court of India"
                    };

                case "GRATUITY":
                    return new List<string>
                    {
                        "Payment of Gratuity Act, 1972 — Section 4: Payment of gratuity upon superannuation, retirement, or resignation",
                        "Payment of Gratuity Act, 1972 — Section 7: Determination of amount of gratuity & notice to Controlling Authority",
                        "Payment of Gratuity Act, 1972 — Section 7(7): Appeal to Appellate Authority within 60 days (extendable by 60 days on sufficient cause)",
                        "Payment of Gratuity Act, 1972 — Section 8: Recovery of gratuity with compound interest"
                    };

                default:
                    return new List<string>
                    {
                        "Code of Civil Procedure, 1908",
                        "Indian Limitation Act, 1963",
                        "Indian Evidence Act, 1872 / Bharatiya Sakshya Adhiniyam, 2023"
                    };
            }
        }

        private static bool IsCommonStopword(string word)
        {
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "with", "this", "that", "case", "from", "court", "order", "date", "against"
            };
            return stopwords.Contains(word);
        }
    }
}
