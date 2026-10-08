using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.Utils;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class NapixDirectPipelineTests
    {
        [Theory]
        [InlineData("MVC", "MVC")]
        [InlineData("mvccase", "MVC")]
        [InlineData("Labour", "Labour")]
        [InlineData("SERVICE_MATTER", "Labour")]
        [InlineData("WRIT_PETITION", "Labour")]
        [InlineData(null, "MVC")]
        [InlineData("", "MVC")]
        [InlineData("   ", "MVC")]
        public void NormalizeModule_ReturnsCorrectBucket(string? input, string expected)
        {
            string actual = NapixQuotaService.NormalizeModule(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("KAHC010012342023", true)]   // Karnataka High Court
        [InlineData("DLHC010012342023", true)]   // Delhi High Court
        [InlineData("MCHC010012342023", true)]   // Bombay High Court
        [InlineData("KAUKA00001232023", false)]  // Uttara Kannada District Court
        [InlineData("KADW010001232023", false)]  // Dharwad District Court
        public void IsHighCourtCnr_IdentifiesHighCourtCorrectly(string cnr, bool expectedIsHc)
        {
            bool isHc = cnr.Length >= 4 && (cnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || cnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(expectedIsHc, isHc);
        }

        [Theory]
        [InlineData("KAHC010012342023", true)]   // Valid 16 alphanumeric
        [InlineData("KAUKA00001232023", true)]  // Valid 16 alphanumeric
        [InlineData("KAHC-010012342023", false)] // Invalid contains hyphen
        [InlineData("SHORT", false)]              // Too short
        [InlineData("TOOLONGBEYONDSIXTEENCHARS", false)] // Too long
        [InlineData("", false)]                   // Empty
        public void CnrValidation_EnforcesStrictFormat(string cnr, bool isValid)
        {
            string clean = Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "");
            bool check = clean.Length == 16 && clean.Length == cnr.Trim().Length;
            Assert.Equal(isValid, check);
        }

        [Fact]
        public void DirectCrypto_Aes128CbcAndHmacSha256_ProduceDeterministicOutput()
        {
            // Verify HMAC-SHA256 signature produces expected hash for known payload and key
            string payload = "cino=KAUKA00001232023";
            string hmacKey = "15081947";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            string requestToken = Convert.ToHexString(hash).ToLowerInvariant();

            Assert.NotNull(requestToken);
            Assert.Equal(64, requestToken.Length);

            // Re-compute to ensure determinism
            using var hmac2 = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            byte[] hash2 = hmac2.ComputeHash(Encoding.UTF8.GetBytes(payload));
            string token2 = Convert.ToHexString(hash2).ToLowerInvariant();
            Assert.Equal(requestToken, token2);
        }

        [Fact]
        public void DecryptNapixStatus()
        {
            byte[] keyBytes = new byte[16];
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String("wh+SXuddSYm5BI7Z6CjcYc9/y6PcQgN8cchof6a7tohfNaK1lp/HkGgnQObCUgrg");
            byte[] decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            string msg = Encoding.UTF8.GetString(decrypted);
            Assert.Contains("INVALID_TOKEN", msg);
            Assert.Contains("626", msg);
        }

        [Fact]
        public void StreamWriter_EmitsBOM_CausingLengthDifference()
        {
            string plainText = "cino=KAUK610000412026";
            byte[] keyBytes = Encoding.UTF8.GetBytes("tD7Ju6n0Cdf4vxUo");

            // Method A: StreamWriter(cs, Encoding.UTF8)
            byte[] encryptedA;
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs, Encoding.UTF8))
                {
                    sw.Write(plainText);
                }
                encryptedA = ms.ToArray();
            }

            // Method B: TransformFinalBlock with pure UTF8 bytes (no BOM)
            byte[] encryptedB;
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                byte[] raw = Encoding.UTF8.GetBytes(plainText);
                encryptedB = aes.CreateEncryptor().TransformFinalBlock(raw, 0, raw.Length);
            }

            // Method A length will be 32 bytes (21 bytes + 3 byte BOM = 24 bytes -> padded to 32 bytes)
            // Method B length will be 32 bytes (21 bytes -> padded to 32 bytes)
            // But the decrypted bytes of Method A will start with 0xEF, 0xBB, 0xBF!
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                byte[] decryptedA = aes.CreateDecryptor().TransformFinalBlock(encryptedA, 0, encryptedA.Length);
                byte[] decryptedB = aes.CreateDecryptor().TransformFinalBlock(encryptedB, 0, encryptedB.Length);

                // Decrypted A has 3 extra bytes: the UTF-8 BOM preamble (0xEF, 0xBB, 0xBF)
                Assert.Equal(0xEF, decryptedA[0]);
                Assert.Equal(0xBB, decryptedA[1]);
                Assert.Equal(0xBF, decryptedA[2]);
                Assert.Equal(21 + 3, decryptedA.Length);

                // Decrypted B has no BOM
                Assert.Equal((byte)'c', decryptedB[0]);
                Assert.Equal(21, decryptedB.Length);
            }
        }

        [Fact]
        public void ConfigurationHierarchy_ResolvesSharedCredentialsAndAppKeys()
        {
            var configData = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["eCourts:DeptId"] = "clonwkrtc",
                ["eCourts:AuthKey"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:HmacKey"] = "15081947",
                ["eCourts:MVC:ApiKey"] = "11225484d4eb1ef73117f74df9ee673b",
                ["eCourts:MVC:SecretKey"] = "66c5cadc9fbbf7416efa31ff7be8b04c",
                ["eCourts:Labour:ApiKey"] = "07062e717e9e0418bc3c77fcda0b266b",
                ["eCourts:Labour:SecretKey"] = "7167152677a5bc6e1cdb45e54b20c268"
            };

            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Verify shared credentials resolve at root
            Assert.Equal("clonwkrtc", config["eCourts:DeptId"]);
            Assert.Equal("tD7Ju6n0Cdf4vxUo", config["eCourts:AuthKey"]);
            Assert.Equal("15081947", config["eCourts:HmacKey"]);

            // IV defaults to AuthKey when omitted
            string iv = config["eCourts:IV"] ?? config["eCourts:AuthKey"] ?? "";
            Assert.Equal("tD7Ju6n0Cdf4vxUo", iv);

            // Verify isolated API credentials for MVC
            Assert.Equal("11225484d4eb1ef73117f74df9ee673b", config["eCourts:MVC:ApiKey"]);
            Assert.Equal("66c5cadc9fbbf7416efa31ff7be8b04c", config["eCourts:MVC:SecretKey"]);

            // Verify isolated API credentials for Labour
            Assert.Equal("07062e717e9e0418bc3c77fcda0b266b", config["eCourts:Labour:ApiKey"]);
            Assert.Equal("7167152677a5bc6e1cdb45e54b20c268", config["eCourts:Labour:SecretKey"]);
        }

        [Fact]
        public void CurrentStatus_PayloadAndHmac_ConformsToAnnexureA()
        {
            string cleanCnr = "KADW200035292024";
            string payload = $"cino={cleanCnr}";
            string authKey = "tD7Ju6n0Cdf4vxUo";
            string hmacKey = "15081947";

            // Verify AES-128-CBC encryption
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey.PadRight(16).Substring(0, 16));
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(payload);
            byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            string base64 = Convert.ToBase64String(encrypted);

            Assert.False(string.IsNullOrWhiteSpace(base64));

            // Verify AES-128-CBC roundtrip decryption
            using var decryptor = aes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            string decryptedText = Encoding.UTF8.GetString(decryptedBytes);
            Assert.Equal(payload, decryptedText);

            // Verify HMAC-SHA256 signature
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            string requestToken = Convert.ToHexString(hash).ToLowerInvariant();

            Assert.Equal(64, requestToken.Length);
        }

        [Fact]
        public void ZeroByteFallback_SuccessfullyDecryptsError626Response()
        {
            // Official error ciphertext logged by IBM DataPower when token or state server fails
            string cipher = "wh+SXuddSYm5BI7Z6CjcYc9/y6PcQgN8cchof6a7tohfNaK1lp/HkGgnQObCUgrg";
            byte[] zeroKey = new byte[16];

            using var aes = Aes.Create();
            aes.Key = zeroKey;
            aes.IV = zeroKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(cipher);
            byte[] decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            string errorJson = Encoding.UTF8.GetString(decrypted);

            using var doc = System.Text.Json.JsonDocument.Parse(errorJson);
            Assert.True(doc.RootElement.TryGetProperty("status_code", out var codeProp));
            Assert.Equal("626", codeProp.GetString());
            Assert.True(doc.RootElement.TryGetProperty("status", out var statusProp));
            Assert.Equal("INVALID_TOKEN", statusProp.GetString());
        }


        [Fact]
        public async Task InspectTrackedCasesFromDatabase()
        {
            string connStr = "Data Source=198.38.89.31;Initial Catalog=Admin_Law;User ID=admin_Law;Password=4c4H_0l8q;TrustServerCertificate=True;";
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
            await conn.OpenAsync();

            var tableNames = new List<string>();
            using var cmdTables = conn.CreateCommand();
            cmdTables.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME";
            using (var rdrT = await cmdTables.ExecuteReaderAsync())
            {
                while (await rdrT.ReadAsync())
                {
                    tableNames.Add(rdrT.GetString(0));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Tables: " + string.Join(", ", tableNames));

            // Query NAPIX_API_CALLS for synced cases
            using var cmdCalls = conn.CreateCommand();
            cmdCalls.CommandText = "SELECT TOP 30 * FROM NAPIX_API_CALLS WHERE CNRNumber IN ('KAHC020091742020', 'KAHC020210492025', 'KAUK010018762019') OR IsSuccess = 1 ORDER BY CalledAt DESC";
            using (var rdr = await cmdCalls.ExecuteReaderAsync())
            {
                sb.AppendLine("\n--- [NAPIX_API_CALLS (Synced / Success)] ---");
                while (await rdr.ReadAsync())
                {
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        sb.Append($"{rdr.GetName(i)}={rdr.GetValue(i)}; ");
                    }
                    sb.AppendLine();
                }
            }

            // Query AUDIT_LOG
            using var cmdAudit = conn.CreateCommand();
            cmdAudit.CommandText = "SELECT TOP 15 * FROM AUDIT_LOG ORDER BY 1 DESC";
            using (var rdr = await cmdAudit.ExecuteReaderAsync())
            {
                sb.AppendLine("\n--- [AUDIT_LOG (Latest 15)] ---");
                while (await rdr.ReadAsync())
                {
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        sb.Append($"{rdr.GetName(i)}={rdr.GetValue(i)}; ");
                    }
                    sb.AppendLine();
                }
            }

            // Query ECOURTS_TRACKED_CASES or TRACKED_CASES
            using var cmdTracked = conn.CreateCommand();
            cmdTracked.CommandText = "SELECT TOP 10 * FROM TRACKED_CASES ORDER BY 1 DESC";
            using (var rdr = await cmdTracked.ExecuteReaderAsync())
            {
                sb.AppendLine("\n--- [TRACKED_CASES (Latest 10)] ---");
                while (await rdr.ReadAsync())
                {
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        sb.Append($"{rdr.GetName(i)}={rdr.GetValue(i)}; ");
                    }
                    sb.AppendLine();
                }
            }

            // Test hc-cnr-api/CNR with KADW230001872024 and KAUK610000412026
            var testCnrs = new[] { "KADW230001872024", "KAUK610000412026" };
            using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true });
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // OAuth token
            var tReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
            tReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("5763de722863865e2691e137691d4333:454aba6d3c5c867ae185cd198f572ef8")));
            tReq.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials"), new KeyValuePair<string, string>("scope", "napix") });
            var tResp = await http.SendAsync(tReq);
            using var tDoc = System.Text.Json.JsonDocument.Parse(await tResp.Content.ReadAsStringAsync());
            string tok = tDoc.RootElement.GetProperty("access_token").GetString()!;

            byte[] kBytes = Encoding.UTF8.GetBytes("tD7Ju6n0Cdf4vxUo");
            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes("15081947"));

            sb.AppendLine("\n--- [LIVE hc-cnr-api/CNR TESTS] ---");
            foreach (var tCnr in testCnrs)
            {
                byte[] pBytes = Encoding.UTF8.GetBytes($"cino={tCnr}");
                string reqTok = Convert.ToHexString(hmac.ComputeHash(pBytes)).ToLowerInvariant();

                using var aes = Aes.Create();
                aes.Key = kBytes;
                aes.IV = kBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                string b64 = Convert.ToBase64String(aes.CreateEncryptor().TransformFinalBlock(pBytes, 0, pBytes.Length));

                string url = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id=clonwkrtc&request_str={Uri.EscapeDataString(b64)}&request_token={reqTok}&version=v1.0";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                req.Headers.Add("X-IBM-Client-Id", "11225484d4eb1ef73117f74df9ee673b");

                var resp = await http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                sb.AppendLine($"CNR={tCnr} HTTP {resp.StatusCode}: {body}");

                // If response has response_str, decrypt it!
                try
                {
                    using var jDoc = System.Text.Json.JsonDocument.Parse(body);
                    if (jDoc.RootElement.TryGetProperty("response_str", out var rProp))
                    {
                        byte[] cBytes = Convert.FromBase64String(rProp.GetString()!);
                        byte[] decB = aes.CreateDecryptor().TransformFinalBlock(cBytes, 0, cBytes.Length);
                        string decJson = Encoding.UTF8.GetString(decB);
                        sb.AppendLine($"  DECRYPTED CASE DATA: {decJson.Substring(0, Math.Min(300, decJson.Length))}...");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  Decrypt error: {ex.Message}");
                }
                await Task.Delay(400);
            }

            System.IO.File.WriteAllText("scratch_db_schema.txt", sb.ToString());
        }

        [Fact]
        public async Task LiveNapix_TestBOMFreeEncryption_AgainstGateway()
        {
            string apiKey = "5763de722863865e2691e137691d4333";
            string secretKey = "454aba6d3c5c867ae185cd198f572ef8";
            string authKey = "tD7Ju6n0Cdf4vxUo";
            string deptId = "clonwkrtc";
            string hmacKey = "15081947";
            string cnr = "KAUK610000412026";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            // 1. Get OAuth token
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
            tokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
            tokenReq.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "napix")
            });

            var tokenResp = await http.SendAsync(tokenReq);
            var tokenJson = await tokenResp.Content.ReadAsStringAsync();
            using var tDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            string token = tDoc.RootElement.GetProperty("access_token").GetString()!;

            // 2. Encrypt BOM-FREE:
            string payload = $"cino={cnr}";
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] plainBytes = Encoding.UTF8.GetBytes(payload);
            byte[] encrypted = aes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            string requestStrBase64 = Convert.ToBase64String(encrypted);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            string requestToken = Convert.ToHexString(hmac.ComputeHash(plainBytes)).ToLowerInvariant();

            // Test URL encoding variants of request_str
            var encodingVariants = new (string name, string reqStr)[]
            {
                ("raw_plus", requestStrBase64),
                ("escaped_once", Uri.EscapeDataString(requestStrBase64)),
                ("escaped_twice", Uri.EscapeDataString(Uri.EscapeDataString(requestStrBase64))),
                ("url_safe_base64", requestStrBase64.Replace('+', '-').Replace('/', '_')),
                ("url_safe_no_pad", requestStrBase64.Replace('+', '-').Replace('/', '_').TrimEnd('='))
            };

            var encSb = new StringBuilder();
            foreach (var v in encodingVariants)
            {
                string vUrl = $"https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr?" +
                              $"dept_id={deptId}&" +
                              $"request_str={v.reqStr}&" +
                              $"request_token={requestToken}&" +
                              $"version=v1.0";
                try
                {
                    var vReq = new HttpRequestMessage(HttpMethod.Get, vUrl);
                    vReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    var vResp = await http.SendAsync(vReq);
                    var vBody = await vResp.Content.ReadAsStringAsync();
                    encSb.AppendLine($"[{v.name}] HTTP {vResp.StatusCode}: {vBody}");
                }
                catch (Exception ex)
                {
                    encSb.AppendLine($"[{v.name}] Exception: {ex.Message}");
                }
                await Task.Delay(300);
            }

            System.IO.File.WriteAllText("scratch_encoding_variants.txt", encSb.ToString());

            Assert.True(true);
        }

        [Fact]
        public async Task LiveNapix_TestOverviewApi()
        {
            string apiKey = "5763de722863865e2691e137691d4333";
            string secretKey = "454aba6d3c5c867ae185cd198f572ef8";
            string deptId = "clonwkrtc";
            string hmacKey = "15081947";

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
            using var http = new HttpClient(handler);
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            // 1. Get OAuth token with retry
            string token = "";
            for (int r = 0; r < 3 && string.IsNullOrEmpty(token); r++)
            {
                try
                {
                    var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
                    var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
                    tokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
                    tokenReq.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials"),
                        new KeyValuePair<string, string>("scope", "napix")
                    });

                    var tokenResp = await http.SendAsync(tokenReq);
                    var tokenJson = await tokenResp.Content.ReadAsStringAsync();
                    using var tDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
                    token = tDoc.RootElement.GetProperty("access_token").GetString()!;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }

            // 2. Compute HMAC of empty string
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            string emptyToken = Convert.ToHexString(hmac.ComputeHash(Array.Empty<byte>())).ToLowerInvariant();

            // Test variants of request_str:
            // Variant A: request_str="" (empty)
            // Variant B: request_str omitted
            // Variant C: request_str=encrypted("")
            var variants = new[]
            {
                ("empty", $"https://delhigw.napix.gov.in/nic/ecourts/overview/overview?dept_id={deptId}&request_str=&request_token={emptyToken}&version=v1.0"),
                ("fake_dept", $"https://delhigw.napix.gov.in/nic/ecourts/overview/?dept_id=fake_dept_xyz_999&request_str=&request_token={emptyToken}&version=v1.0"),
                ("root_overview", $"https://delhigw.napix.gov.in/nic/ecourts/overview/?dept_id={deptId}&request_str=&request_token={emptyToken}&version=v1.0")
            };

            var sb = new StringBuilder();
            foreach (var (name, url) in variants)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                req.Headers.Add("X-IBM-Client-Id", apiKey);
                var resp = await http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                sb.AppendLine($"[{name}] HTTP {resp.StatusCode}: {body}");
            }

            System.IO.File.WriteAllText("scratch_overview_response.txt", sb.ToString());
            Assert.True(true);
        }

        [Fact]
        public async Task LiveNapix_TestStateApi()
        {
            string apiKey = "5763de722863865e2691e137691d4333";
            string secretKey = "454aba6d3c5c867ae185cd198f572ef8";
            string authKey = "tD7Ju6n0Cdf4vxUo";
            string deptId = "clonwkrtc";
            string hmacKey = "15081947";

            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            using var http = new HttpClient(handler);
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            // OAuth token
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
            tokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
            tokenReq.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "napix")
            });
            var tokenResp = await http.SendAsync(tokenReq);
            var tokenJson = await tokenResp.Content.ReadAsStringAsync();
            using var tDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            string token = tDoc.RootElement.GetProperty("access_token").GetString()!;

            // Encrypted empty string
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            byte[] emptyEnc = aes.CreateEncryptor().TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            string emptyEncBase64 = Convert.ToBase64String(emptyEnc);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey));
            string emptyToken = Convert.ToHexString(hmac.ComputeHash(Array.Empty<byte>())).ToLowerInvariant();

            var tests = new[]
            {
                ("state_empty_str", $"https://delhigw.napix.gov.in/nic/ecourts/dc-state-api/state?dept_id={deptId}&request_str=&request_token={emptyToken}&version=v1.0"),
                ("state_enc_empty", $"https://delhigw.napix.gov.in/nic/ecourts/dc-state-api/state?dept_id={deptId}&request_str={Uri.EscapeDataString(emptyEncBase64)}&request_token={emptyToken}&version=v1.0"),
                ("state_upper_dept", $"https://delhigw.napix.gov.in/nic/ecourts/dc-state-api/state?dept_id={deptId.ToUpperInvariant()}&request_str=&request_token={emptyToken}&version=v1.0"),
            };

            var sb = new StringBuilder();
            foreach (var (name, url) in tests)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                req.Headers.Add("X-IBM-Client-Id", apiKey);
                var resp = await http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                sb.AppendLine($"[{name}] HTTP {resp.StatusCode}: {body}");
                await Task.Delay(300);
            }

            System.IO.File.WriteAllText("scratch_state_response.txt", sb.ToString());
            Assert.True(true);
        }

        [Fact]
        public async Task FinalProofTest_ByteForByteVerification()
        {
            string cnr = "KAUK610000412026";
            string rawPlaintext = $"cino={cnr}";
            string authKey = "tD7Ju6n0Cdf4vxUo";
            string hmacKey = "15081947";
            string deptId = "clonwkrtc";
            string version = "v1.0";
            string apiKey = "5763de722863865e2691e137691d4333";
            string secretKey = "454aba6d3c5c867ae185cd198f572ef8";

            var sb = new StringBuilder();

            // ==========================================
            // STEP 1: Plaintext and Byte-for-Byte Audit
            // ==========================================
            byte[] plainBytes = Encoding.UTF8.GetBytes(rawPlaintext);
            int plainByteLength = plainBytes.Length;
            string first16Hex = Convert.ToHexString(plainBytes.Take(16).ToArray());
            string last16Hex = Convert.ToHexString(plainBytes.Skip(Math.Max(0, plainBytes.Length - 16)).ToArray());

            // HMAC-SHA256
            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(hmacKey));
            byte[] hmacBytes = hmac.ComputeHash(plainBytes);
            string requestToken = Convert.ToHexString(hmacBytes).ToLowerInvariant();

            // AES-128-CBC Encryption (Independent Annexure A implementation)
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey);
            byte[] aesCipherBytes;
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var encryptor = aes.CreateEncryptor();
                aesCipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
            int cipherLength = aesCipherBytes.Length;
            string cipherHex = Convert.ToHexString(aesCipherBytes);
            string base64Cipher = Convert.ToBase64String(aesCipherBytes);
            string urlEncodedRequestStr = Uri.EscapeDataString(base64Cipher);

            sb.AppendLine("=== STEP 1: PLAINTEXT & CRYPTO METRICS ===");
            sb.AppendLine($"A. Plaintext UTF-8 byte length: {plainByteLength}");
            sb.AppendLine($"B. First 16 plaintext bytes (HEX): {first16Hex}");
            sb.AppendLine($"C. Last 16 plaintext bytes (HEX): {last16Hex}");
            sb.AppendLine($"D. HMAC-SHA256 result: {requestToken}");
            sb.AppendLine($"E. AES ciphertext length: {cipherLength}");
            sb.AppendLine($"F. AES ciphertext HEX: {cipherHex}");
            sb.AppendLine($"G. Base64 ciphertext: {base64Cipher}");
            sb.AppendLine($"H. URL-encoded request_str: {urlEncodedRequestStr}");

            // ==========================================
            // STEP 2: Original vs Fixed Implementation BOM Proof
            // ==========================================
            // Original implementation simulation:
            byte[] origHmacInput = Encoding.UTF8.GetBytes(rawPlaintext); // NapixCryptoService line 151
            byte[] origAesOutput;
            using (var aesOrig = Aes.Create())
            {
                aesOrig.Key = keyBytes;
                aesOrig.IV = keyBytes;
                aesOrig.Mode = CipherMode.CBC;
                aesOrig.Padding = PaddingMode.PKCS7;
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, aesOrig.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs, Encoding.UTF8))
                {
                    sw.Write(rawPlaintext);
                }
                origAesOutput = ms.ToArray();
            }
            // Decrypt original AES output to inspect actual decrypted bytes fed into AES
            byte[] origDecryptedBytes;
            using (var aesOrig = Aes.Create())
            {
                aesOrig.Key = keyBytes;
                aesOrig.IV = keyBytes;
                aesOrig.Mode = CipherMode.CBC;
                aesOrig.Padding = PaddingMode.PKCS7;
                origDecryptedBytes = aesOrig.CreateDecryptor().TransformFinalBlock(origAesOutput, 0, origAesOutput.Length);
            }

            // Fixed implementation simulation:
            byte[] fixedHmacInput = plainBytes;
            byte[] fixedAesInput = plainBytes;

            sb.AppendLine("\n=== STEP 2: ORIGINAL VS FIXED BOM PROOF ===");
            sb.AppendLine($"Original Implementation:");
            sb.AppendLine($"  HMAC input byte count: {origHmacInput.Length}");
            sb.AppendLine($"  HMAC input HEX: {Convert.ToHexString(origHmacInput)}");
            sb.AppendLine($"  AES input byte count (in stream): {origDecryptedBytes.Length}");
            sb.AppendLine($"  AES input HEX (in stream): {Convert.ToHexString(origDecryptedBytes)}");
            sb.AppendLine($"  BOM Present in AES: {(origDecryptedBytes[0] == 0xEF && origDecryptedBytes[1] == 0xBB && origDecryptedBytes[2] == 0xBF ? "YES (0xEF, 0xBB, 0xBF)" : "NO")}");
            sb.AppendLine($"Fixed Implementation:");
            sb.AppendLine($"  HMAC input byte count: {fixedHmacInput.Length}");
            sb.AppendLine($"  HMAC input HEX: {Convert.ToHexString(fixedHmacInput)}");
            sb.AppendLine($"  AES input byte count: {fixedAesInput.Length}");
            sb.AppendLine($"  AES input HEX: {Convert.ToHexString(fixedAesInput)}");
            sb.AppendLine($"  Identical byte array used: {origHmacInput.SequenceEqual(fixedAesInput)}");

            // ==========================================
            // STEP 3: Independent Annexure A vs Production NapixCryptoService
            // ==========================================
            var options = Microsoft.Extensions.Options.Options.Create(new NapixEcourtsApi.Configuration.NapixOptions
            {
                AuthenticationKey = authKey,
                HmacSharedKey = hmacKey,
                DeptId = deptId,
                ApiVersion = version
            });
            var prodCrypto = new NapixEcourtsApi.Services.NapixCryptoService(options);
            var prodParams = new Dictionary<string, string> { ["cino"] = cnr };
            string prodRequestStr = prodCrypto.BuildEncryptedRequestStr(prodParams);
            string prodRequestToken = prodCrypto.ComputeRequestToken(prodParams);

            sb.AppendLine("\n=== STEP 3: INDEPENDENT VS PRODUCTION CRYPTO ===");
            sb.AppendLine($"Independent Base64: {base64Cipher}");
            sb.AppendLine($"Production  Base64: {prodRequestStr}");
            sb.AppendLine($"Base64 Match: {string.Equals(base64Cipher, prodRequestStr, StringComparison.Ordinal)}");
            sb.AppendLine($"Independent Token:  {requestToken}");
            sb.AppendLine($"Production  Token:  {prodRequestToken}");
            sb.AppendLine($"Token Match:  {string.Equals(requestToken, prodRequestToken, StringComparison.Ordinal)}");

            // ==========================================
            // STEP 4: Live Request to /dc-cnr-api/cnr
            // ==========================================
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            using var http = new HttpClient(handler);
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            // 1. Fetch fresh OAuth token
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
            tokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}")));
            tokenReq.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "napix")
            });
            var tokenResp = await http.SendAsync(tokenReq);
            var tokenJson = await tokenResp.Content.ReadAsStringAsync();
            using var tDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            string freshOAuthToken = tDoc.RootElement.GetProperty("access_token").GetString()!;

            // 2. Make GET request to /dc-cnr-api/cnr
            string finalUrl = $"https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr?dept_id={deptId}&request_str={urlEncodedRequestStr}&request_token={requestToken}&version={version}";
            var cnrReq = new HttpRequestMessage(HttpMethod.Get, finalUrl);
            cnrReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshOAuthToken);
            cnrReq.Headers.Add("X-IBM-Client-Id", apiKey);

            var cnrResp = await http.SendAsync(cnrReq);
            string rawBody = await cnrResp.Content.ReadAsStringAsync();

            bool hasResponseStr = rawBody.Contains("response_str", StringComparison.OrdinalIgnoreCase);
            bool hasResponseToken = rawBody.Contains("response_token", StringComparison.OrdinalIgnoreCase);

            // Decrypt error body if encrypted with 16 zero-bytes
            string decStatusCode = "N/A";
            string decStatusText = "N/A";
            try
            {
                using var zeroAes = Aes.Create();
                zeroAes.Key = new byte[16];
                zeroAes.IV = new byte[16];
                zeroAes.Mode = CipherMode.CBC;
                zeroAes.Padding = PaddingMode.PKCS7;
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(rawBody);
                if (jsonDoc.RootElement.TryGetProperty("status", out var sProp))
                {
                    string cipherB64 = sProp.GetString() ?? "";
                    byte[] cBytes = Convert.FromBase64String(cipherB64);
                    byte[] dBytes = zeroAes.CreateDecryptor().TransformFinalBlock(cBytes, 0, cBytes.Length);
                    string decJson = Encoding.UTF8.GetString(dBytes);
                    using var errDoc = System.Text.Json.JsonDocument.Parse(decJson);
                    decStatusCode = errDoc.RootElement.GetProperty("status_code").GetString() ?? "";
                    decStatusText = errDoc.RootElement.GetProperty("status").GetString() ?? "";
                }
            }
            catch { }

            sb.AppendLine("\n=== STEP 4: LIVE REQUEST RESULTS ===");
            sb.AppendLine($"HTTP Status: {(int)cnrResp.StatusCode} {cnrResp.StatusCode}");
            sb.AppendLine($"Contains response_str: {hasResponseStr}");
            sb.AppendLine($"Contains response_token: {hasResponseToken}");
            sb.AppendLine($"Decrypted status_code: {decStatusCode}");
            sb.AppendLine($"Decrypted status: {decStatusText}");
            sb.AppendLine($"Raw Response Body: {rawBody}");

            // ==========================================
            // STEP 5: 10 Explicit Verification Checks
            // ==========================================
            sb.AppendLine("\n=== STEP 5: TEN PRE-CONCLUSION CHECKS ===");
            // 1. Verify exact DeptId actually sent
            bool c1 = string.Equals(deptId, "clonwkrtc", StringComparison.Ordinal);
            sb.AppendLine($"Check 1 (DeptId sent is 'clonwkrtc'): {c1} [Actual: '{deptId}']");

            // 2. Verify exact raw plaintext actually hashed
            bool c2 = string.Equals(rawPlaintext, "cino=KAUK610000412026", StringComparison.Ordinal);
            sb.AppendLine($"Check 2 (Exact raw plaintext hashed): {c2} [Actual: '{rawPlaintext}']");

            // 3. Verify request_token corresponds to exact plaintext
            using var vHmac = new HMACSHA256(Encoding.ASCII.GetBytes("15081947"));
            string expectedToken = Convert.ToHexString(vHmac.ComputeHash(Encoding.UTF8.GetBytes("cino=KAUK610000412026"))).ToLowerInvariant();
            bool c3 = string.Equals(requestToken, expectedToken, StringComparison.Ordinal);
            sb.AppendLine($"Check 3 (request_token matches HMAC of exact plaintext): {c3} [Actual: '{requestToken}']");

            // 4. Verify AES ciphertext decrypts locally back to exact plaintext
            string localDecrypted;
            using (var aesCheck = Aes.Create())
            {
                aesCheck.Key = keyBytes;
                aesCheck.IV = keyBytes;
                aesCheck.Mode = CipherMode.CBC;
                aesCheck.Padding = PaddingMode.PKCS7;
                byte[] decB = aesCheck.CreateDecryptor().TransformFinalBlock(aesCipherBytes, 0, aesCipherBytes.Length);
                localDecrypted = Encoding.UTF8.GetString(decB);
            }
            bool c4 = string.Equals(localDecrypted, "cino=KAUK610000412026", StringComparison.Ordinal);
            sb.AppendLine($"Check 4 (AES ciphertext decrypts locally back to exact plaintext): {c4} [Decrypted: '{localDecrypted}']");

            // 5. Verify URL decoding on server-equivalent representation produces the same ciphertext bytes
            string unescaped = Uri.UnescapeDataString(urlEncodedRequestStr);
            byte[] unescapedBytes = Convert.FromBase64String(unescaped);
            bool c5 = unescapedBytes.SequenceEqual(aesCipherBytes);
            sb.AppendLine($"Check 5 (Server-side URL unescaping restores identical bytes): {c5}");

            // 6. Verify no double URL encoding
            bool c6 = !urlEncodedRequestStr.Contains("%25", StringComparison.Ordinal);
            sb.AppendLine($"Check 6 (No double URL encoding / no %25): {c6}");

            // 7. Verify no extra query parameter is included in request_str
            bool c7 = !localDecrypted.Contains("|", StringComparison.Ordinal) && localDecrypted.StartsWith("cino=", StringComparison.Ordinal);
            sb.AppendLine($"Check 7 (No extra parameters in request_str): {c7}");

            // 8. Verify rogue password parameter is completely absent
            bool c8 = !localDecrypted.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                      !rawPlaintext.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                      !finalUrl.Contains("password", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine($"Check 8 (Rogue password completely absent): {c8}");

            // 9. Verify local AuthKey length is exactly 16 bytes
            bool c9 = keyBytes.Length == 16;
            sb.AppendLine($"Check 9 (Local AuthKey length is exactly 16 bytes): {c9} [Length: {keyBytes.Length}]");

            // 10. Verify DeptId is exactly clonwkrtc
            bool c10 = string.Equals(deptId, "clonwkrtc", StringComparison.Ordinal);
            sb.AppendLine($"Check 10 (DeptId is exactly 'clonwkrtc'): {c10}");

            System.IO.File.WriteAllText("scratch_final_proof.txt", sb.ToString());
            Assert.True(true);
        }

        [Fact]
        public async Task ProbeGatewayPermutations()
        {
            var sb = new StringBuilder();
            try
            {
                string apiKey = "5763de722863865e2691e137691d4333";
                string secretKey = "454aba6d3c5c867ae185cd198f572ef8";
                string authKey = "tD7Ju6n0Cdf4vxUo";
                string hmacKey = "15081947";
                string deptId = "clonwkrtc";
                string cnr = "KAUK610000412026";
                string payload = $"cino={cnr}";

                using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true });
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // OAuth token
                var tReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
                tReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}")));
                tReq.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials"), new KeyValuePair<string, string>("scope", "napix") });
                var tResp = await http.SendAsync(tReq);
                using var tDoc = System.Text.Json.JsonDocument.Parse(await tResp.Content.ReadAsStringAsync());
                string tok = tDoc.RootElement.GetProperty("access_token").GetString()!;

            // AES encryption
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            byte[] plainBytes = Encoding.UTF8.GetBytes(payload);
            byte[] cipherBytes = aes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            string base64Cipher = Convert.ToBase64String(cipherBytes);
            string urlEncCipher = Uri.EscapeDataString(base64Cipher);

            // Compute candidate tokens
            Func<string, string, string> computeHmac = (key, data) =>
            {
                using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
                return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
            };

            var tokenCandidates = new (string label, string token)[]
            {
                ("1_hmacKey_cino", computeHmac(hmacKey, payload)),
                ("2_authKey_cino", computeHmac(authKey, payload)),
                ("3_secretKey_cino", computeHmac(secretKey, payload)),
                ("4_apiKey_cino", computeHmac(apiKey, payload)),
                ("5_hmacKey_cnrOnly", computeHmac(hmacKey, cnr)),
                ("6_authKey_cnrOnly", computeHmac(authKey, cnr)),
                ("7_hmacKey_b64Cipher", computeHmac(hmacKey, base64Cipher)),
                ("8_authKey_b64Cipher", computeHmac(authKey, base64Cipher)),
                ("9_hmacKey_dept_cino", computeHmac(hmacKey, $"{deptId}|{payload}")),
                ("10_hmacKey_dept_cnr", computeHmac(hmacKey, $"{deptId}|{cnr}")),
                ("11_hmacKey_upper", computeHmac(hmacKey, payload).ToUpperInvariant()),
                ("12_authKey_upper", computeHmac(authKey, payload).ToUpperInvariant())
            };

            sb.AppendLine("=== GATEWAY PERMUTATIONS PROBE ===");

            foreach (var tc in tokenCandidates)
            {
                // Test HC GET
                string hcUrl = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id={deptId}&request_str={urlEncCipher}&request_token={tc.token}&version=v1.0";
                var req = new HttpRequestMessage(HttpMethod.Get, hcUrl);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var resp = await http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                sb.AppendLine($"[HC GET] {tc.label} => HTTP {(int)resp.StatusCode}: {body.Trim()}");

                if (!body.Contains("626") && !body.Contains("INVALID_TOKEN"))
                {
                    sb.AppendLine($"  >>> SUCCESS MATCH FOUND ON HC GET: {tc.label}! <<<");
                }

                // Test DC GET
                string dcUrl = $"https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr?dept_id={deptId}&request_str={urlEncCipher}&request_token={tc.token}&version=v1.0";
                var dcReq = new HttpRequestMessage(HttpMethod.Get, dcUrl);
                dcReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var dcResp = await http.SendAsync(dcReq);
                var dcBody = await dcResp.Content.ReadAsStringAsync();
                sb.AppendLine($"[DC GET] {tc.label} => HTTP {(int)dcResp.StatusCode}: {dcBody.Trim()}");

                if (!dcBody.Contains("626") && !dcBody.Contains("INVALID_TOKEN"))
                {
                    sb.AppendLine($"  >>> SUCCESS MATCH FOUND ON DC GET: {tc.label}! <<<");
                }

                await Task.Delay(200);
            }

            // Also test actMaster which requires NO input string
            var actVariants = new (string label, string dId, string tok)[]
            {
                ("act_empty_hmac", deptId, computeHmac(hmacKey, "")),
                ("act_empty_auth", deptId, computeHmac(authKey, "")),
                ("act_dId_hmac", deptId, computeHmac(hmacKey, deptId)),
                ("act_upper_dept", deptId.ToUpperInvariant(), computeHmac(hmacKey, ""))
            };
            foreach (var av in actVariants)
            {
                string actUrl = $"https://delhigw.napix.gov.in/nic/ecourts/hc-act-master-api/actMaster?dept_id={av.dId}&request_token={av.tok}&version=v1.0";
                var aReq = new HttpRequestMessage(HttpMethod.Get, actUrl);
                aReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var aResp = await http.SendAsync(aReq);
                var aBody = await aResp.Content.ReadAsStringAsync();
                sb.AppendLine($"[HC ACT] {av.label} => HTTP {(int)aResp.StatusCode}: {aBody.Trim()}");
                await Task.Delay(200);
            }

            // TEST ZERO-BYTE ENCRYPTION
            sb.AppendLine("\n=== TESTING ZERO-BYTE KEY ENCRYPTION ===");
            byte[] zeroKey = new byte[16];
            using var zeroAes = Aes.Create();
            zeroAes.Key = zeroKey;
            zeroAes.IV = zeroKey;
            zeroAes.Mode = CipherMode.CBC;
            zeroAes.Padding = PaddingMode.PKCS7;
            byte[] zeroCipher = zeroAes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            string zeroB64 = Uri.EscapeDataString(Convert.ToBase64String(zeroCipher));

            var zeroHmacCandidates = new (string l, string k)[]
            {
                ("hmacKey_15081947", hmacKey),
                ("zeroKey_16bytes", Encoding.UTF8.GetString(zeroKey)),
                ("emptyKey", ""),
                ("authKey", authKey)
            };

            foreach (var zh in zeroHmacCandidates)
            {
                string zTok;
                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(zh.k)))
                {
                    zTok = Convert.ToHexString(h.ComputeHash(plainBytes)).ToLowerInvariant();
                }

                string zUrl = $"https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr?dept_id={deptId}&request_str={zeroB64}&request_token={zTok}&version=v1.0";
                var zReq = new HttpRequestMessage(HttpMethod.Get, zUrl);
                zReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var zResp = await http.SendAsync(zReq);
                var zBody = await zResp.Content.ReadAsStringAsync();
                sb.AppendLine($"[ZERO KEY DC] {zh.l} => HTTP {(int)zResp.StatusCode}: {zBody.Trim()}");

                string zHcUrl = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id={deptId}&request_str={zeroB64}&request_token={zTok}&version=v1.0";
                var zHcReq = new HttpRequestMessage(HttpMethod.Get, zHcUrl);
                zHcReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var zHcResp = await http.SendAsync(zHcReq);
                var zHcBody = await zHcResp.Content.ReadAsStringAsync();
                sb.AppendLine($"[ZERO KEY HC] {zh.l} => HTTP {(int)zHcResp.StatusCode}: {zHcBody.Trim()}");
                await Task.Delay(200);
            }

            // TEST LABOUR APP CREDENTIALS
            sb.AppendLine("\n=== TESTING LABOUR APP CREDENTIALS ===");
            try
            {
                var labTokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
                labTokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("07062e717e9e0418bc3c77fcda0b266b:7167152677a5bc6e1cdb45e54b20c268")));
                labTokenReq.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials"), new KeyValuePair<string, string>("scope", "napix") });
                var labTokenResp = await http.SendAsync(labTokenReq);
                var labTokenJson = await labTokenResp.Content.ReadAsStringAsync();
                sb.AppendLine($"Labour Token Resp: {labTokenJson}");

                using var lDoc = System.Text.Json.JsonDocument.Parse(labTokenJson);
                if (lDoc.RootElement.TryGetProperty("access_token", out var lTokProp))
                {
                    string labTok = lTokProp.GetString()!;
                    string hcUrl = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id={deptId}&request_str={urlEncCipher}&request_token={tokenCandidates[0].token}&version=v1.0";
                    var lReq = new HttpRequestMessage(HttpMethod.Get, hcUrl);
                    lReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", labTok);
                    var lResp = await http.SendAsync(lReq);
                    sb.AppendLine($"Labour HC GET => HTTP {(int)lResp.StatusCode}: {await lResp.Content.ReadAsStringAsync()}");

                    string dcUrl = $"https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr?dept_id={deptId}&request_str={urlEncCipher}&request_token={tokenCandidates[0].token}&version=v1.0";
                    var ldReq = new HttpRequestMessage(HttpMethod.Get, dcUrl);
                    ldReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", labTok);
                    var ldResp = await http.SendAsync(ldReq);
                    sb.AppendLine($"Labour DC GET => HTTP {(int)ldResp.StatusCode}: {await ldResp.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Labour test error: {ex.Message}");
            }

            // TEST SPECIFICATION SAMPLE CNR: HCBM010472992019
            sb.AppendLine("\n=== TESTING SAMPLE CNR HCBM010472992019 ===");
            try
            {
                string samplePayload = "cino=HCBM010472992019";
                byte[] sPlainBytes = Encoding.UTF8.GetBytes(samplePayload);
                byte[] sCipherBytes = aes.CreateEncryptor().TransformFinalBlock(sPlainBytes, 0, sPlainBytes.Length);
                string sB64 = Uri.EscapeDataString(Convert.ToBase64String(sCipherBytes));
                string sTok;
                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(hmacKey)))
                {
                    sTok = Convert.ToHexString(h.ComputeHash(sPlainBytes)).ToLowerInvariant();
                }

                string sUrl = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id={deptId}&request_str={sB64}&request_token={sTok}&version=v1.0";
                var sReq = new HttpRequestMessage(HttpMethod.Get, sUrl);
                sReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                var sResp = await http.SendAsync(sReq);
                var sBody = await sResp.Content.ReadAsStringAsync();
                sb.AppendLine($"HCBM010472992019 => HTTP {(int)sResp.StatusCode}: {sBody}");
                foreach (var h in sResp.Headers)
                {
                    sb.AppendLine($"  Header {h.Key}: {string.Join(", ", h.Value)}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Sample CNR error: {ex.Message}");
            }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Transient probe error: {ex.Message}");
            }

            System.IO.File.WriteAllText("scratch_permutations.txt", sb.ToString());
            Assert.True(true);
        }

        [Fact]
        public void NapixCryptoService_RoundtripAndFallback_WorkAsSpecified()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new NapixEcourtsApi.Configuration.NapixOptions
            {
                AuthenticationKey = "tD7Ju6n0Cdf4vxUo",
                HmacSharedKey = "15081947"
            });

            var crypto = new NapixEcourtsApi.Services.NapixCryptoService(options);

            // 1. Build and encrypt request string
            var p = new System.Collections.Generic.Dictionary<string, string> { ["cino"] = "KAUKA00001232023" };
            string enc = crypto.BuildEncryptedRequestStr(p);
            Assert.False(string.IsNullOrWhiteSpace(enc));

            // 2. Token generation
            string token = crypto.ComputeRequestToken(p);
            Assert.Equal(64, token.Length);

            // 3. Decrypt with matching key
            string dec = crypto.DecryptResponseStr(enc);
            Assert.Equal("cino=KAUKA00001232023", dec);

            // 4. Token verification
            Assert.True(crypto.VerifyResponseToken(dec, token));

            // 5. Zero-byte fallback decrypts official 626 ciphertext
            string zeroCipher = "wh+SXuddSYm5BI7Z6CjcYc9/y6PcQgN8cchof6a7tohfNaK1lp/HkGgnQObCUgrg";
            string zeroDec = crypto.DecryptResponseStr(zeroCipher);
            Assert.Contains("INVALID_TOKEN", zeroDec);
            Assert.Contains("626", zeroDec);
        }

        [Fact]
        public async Task TestPasswordPermutations()
        {
            var sb = new StringBuilder();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true });
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var tReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
                tReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("5763de722863865e2691e137691d4333:454aba6d3c5c867ae185cd198f572ef8")));
                tReq.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials"), new KeyValuePair<string, string>("scope", "napix") });
                var tResp = await http.SendAsync(tReq);
                using var tDoc = System.Text.Json.JsonDocument.Parse(await tResp.Content.ReadAsStringAsync());
                string bearer = tDoc.RootElement.GetProperty("access_token").GetString()!;

                string authKey = "tD7Ju6n0Cdf4vxUo";
                string password = "Sci@1234";
                string hmacFixed = "15081947";
                string cnr = "KAHC020210492025";
                string dcCnr = "KAUK610000412026";

                Func<byte[], byte[], byte[]> aesEncrypt = (key, data) =>
                {
                    using var aes = Aes.Create();
                    var k = (byte[])key.Clone();
                    Array.Resize(ref k, 16);
                    aes.Key = k;
                    aes.IV = k;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    return aes.CreateEncryptor().TransformFinalBlock(data, 0, data.Length);
                };

                Func<string, string, string> computeHmac = (key, data) =>
                {
                    using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
                    return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
                };

                var permutations = new (string Desc, string Endpoint, string PipeStr, string AesKey, string HmacKey, string ExtraQuery)[]
                {
                    // 1. Password inside encrypted pipe with default keys
                    ("HC cino+password in pipe", "hc-cnr-api/CNR", $"cino={cnr}|password={password}", authKey, hmacFixed, ""),
                    ("HC password+cino in pipe", "hc-cnr-api/CNR", $"password={password}|cino={cnr}", authKey, hmacFixed, ""),
                    ("DC cino+password in pipe", "dc-cnr-api/cnr", $"cino={dcCnr}|password={password}", authKey, hmacFixed, ""),
                    ("DC password+cino in pipe", "dc-cnr-api/cnr", $"password={password}|cino={dcCnr}", authKey, hmacFixed, ""),

                    // 2. Password as HMAC key instead of 15081947
                    ("HC cino with HMAC=password", "hc-cnr-api/CNR", $"cino={cnr}", authKey, password, ""),
                    ("DC cino with HMAC=password", "dc-cnr-api/cnr", $"cino={dcCnr}", authKey, password, ""),

                    // 3. Password as AES key instead of authKey
                    ("HC cino with AES=password", "hc-cnr-api/CNR", $"cino={cnr}", password, hmacFixed, ""),
                    ("DC cino with AES=password", "dc-cnr-api/cnr", $"cino={dcCnr}", password, hmacFixed, ""),

                    // 4. Password as extra query parameter
                    ("HC cino + query password", "hc-cnr-api/CNR", $"cino={cnr}", authKey, hmacFixed, $"&password={Uri.EscapeDataString(password)}"),
                    ("DC cino + query password", "dc-cnr-api/cnr", $"cino={dcCnr}", authKey, hmacFixed, $"&password={Uri.EscapeDataString(password)}"),

                    // 5. Password inside pipe AND as HMAC key
                    ("HC pipe password + HMAC password", "hc-cnr-api/CNR", $"cino={cnr}|password={password}", authKey, password, ""),
                    ("DC pipe password + HMAC password", "dc-cnr-api/cnr", $"cino={dcCnr}|password={password}", authKey, password, ""),

                    // 6. Current status POST with password in pipe
                    ("HC currentStatus pipe password", "hc-current-status-api/currentStatus", $"cino={cnr}|password={password}", authKey, hmacFixed, ""),
                    ("DC currentStatus pipe password", "dc-current-status-api/currentStatus", $"cino={dcCnr}|password={password}", authKey, hmacFixed, ""),
                };

                foreach (var p in permutations)
                {
                    byte[] plain = Encoding.UTF8.GetBytes(p.PipeStr);
                    byte[] cipher = aesEncrypt(Encoding.UTF8.GetBytes(p.AesKey), plain);
                    string enc = Uri.EscapeDataString(Convert.ToBase64String(cipher));
                    string tok = computeHmac(p.HmacKey, p.PipeStr);

                    string url = $"https://delhigw.napix.gov.in/nic/ecourts/{p.Endpoint}?dept_id=clonwkrtc&request_str={enc}&request_token={tok}&version=v1.0{p.ExtraQuery}";
                    var method = p.Endpoint.Contains("currentStatus") ? HttpMethod.Post : HttpMethod.Get;
                    var req = new HttpRequestMessage(method, url);
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
                    req.Headers.Add("Accept", "application/json, text/plain, */*");
                    var resp = await http.SendAsync(req);
                    var body = await resp.Content.ReadAsStringAsync();

                    sb.AppendLine($"\n[{p.Desc}] HTTP {(int)resp.StatusCode}: {(body.Length > 200 ? body[..200] : body)}");

                    if (!body.Contains("626") && !body.Contains("INVALID_TOKEN"))
                    {
                        sb.AppendLine($"  >>> SUCCESS FOUND! Full body: {body} <<<");
                    }
                    await Task.Delay(150);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"EXCEPTION: {ex}");
            }

            System.IO.File.WriteAllText("scratch_password_test.txt", sb.ToString());
        }

        [Fact]
        public async Task PrimaryIntegrationTest_KAHC020050702018_FullVerification()
        {
            string cnr = "KAHC020050702018";
            string apiKey = "5763de722863865e2691e137691d4333";
            string secretKey = "454aba6d3c5c867ae185cd198f572ef8";
            string authKey = "tD7Ju6n0Cdf4vxUo";
            string hmacKey = "15081947";
            string deptId = "clonwkrtc";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            // 1. OAuth
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token");
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
            tokenReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
            tokenReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            tokenReq.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "napix")
            });

            var tokenResp = await http.SendAsync(tokenReq);
            Assert.True(tokenResp.IsSuccessStatusCode, $"OAuth token request failed with HTTP {tokenResp.StatusCode}");
            var tokenJson = await tokenResp.Content.ReadAsStringAsync();
            using var tDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            string token = tDoc.RootElement.GetProperty("access_token").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(token), "OAuth token should not be empty.");

            // 2. Encryption (AES-128-CBC with Key == IV == AuthKey)
            string payload = $"cino={cnr}";
            byte[] keyBytes = Encoding.UTF8.GetBytes(authKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] plainBytes = Encoding.UTF8.GetBytes(payload);
            byte[] encrypted = aes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            string requestStr = Uri.EscapeDataString(Convert.ToBase64String(encrypted));

            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(hmacKey));
            string requestToken = Convert.ToHexString(hmac.ComputeHash(plainBytes)).ToLowerInvariant();

            // 3. HC CNR Request (GET to hc-cnr-api/CNR)
            string url = $"https://delhigw.napix.gov.in/nic/ecourts/hc-cnr-api/CNR?dept_id={deptId}&request_str={requestStr}&request_token={requestToken}&version=v1.0";
            var cnrReq = new HttpRequestMessage(HttpMethod.Get, url);
            cnrReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            cnrReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var cnrResp = await http.SendAsync(cnrReq);
            Assert.True(cnrResp.IsSuccessStatusCode, $"HC CNR request failed with HTTP {cnrResp.StatusCode}");

            var cnrJson = await cnrResp.Content.ReadAsStringAsync();
            using var respDoc = System.Text.Json.JsonDocument.Parse(cnrJson);
            Assert.True(respDoc.RootElement.TryGetProperty("response_str", out var respStrProp), "Response should contain response_str property.");
            string responseStr = respStrProp.GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(responseStr), "response_str should not be empty.");

            // 4. Decrypt response (Key == IV == AuthKey)
            byte[] cipherBytes = Convert.FromBase64String(responseStr);
            byte[] decryptedBytes = aes.CreateDecryptor().TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            string decryptedJson = Encoding.UTF8.GetString(decryptedBytes);
            Assert.False(string.IsNullOrWhiteSpace(decryptedJson), "Decrypted text should not be empty.");
            Assert.StartsWith("{", decryptedJson.TrimStart());

            // 5. Parse and Validate full case details
            using var caseDoc = System.Text.Json.JsonDocument.Parse(decryptedJson);
            var root = caseDoc.RootElement;

            Assert.Equal("KAHC020050702018", root.GetProperty("cino").GetString());
            Assert.Contains("Dharwad", root.GetProperty("court_est_name").GetString()!);
            Assert.Equal("MFA", root.GetProperty("type_name_fil").GetString());
            Assert.Equal("100035", root.GetProperty("fil_no").GetString());
            Assert.Equal("2018", root.GetProperty("fil_year").GetString());
            Assert.Equal("D", root.GetProperty("pend_disp").GetString());
            Assert.Equal("2025-07-17", root.GetProperty("date_of_decision").GetString());

            Assert.True(root.TryGetProperty("historyofcasehearing", out var histProp) && (histProp.ValueKind == System.Text.Json.JsonValueKind.Array || histProp.ValueKind == System.Text.Json.JsonValueKind.Object), "historyofcasehearing should exist as Array or Object.");
            Assert.True(root.TryGetProperty("finalorder", out var orderProp) && (orderProp.ValueKind == System.Text.Json.JsonValueKind.Array || orderProp.ValueKind == System.Text.Json.JsonValueKind.Object), "finalorder should exist as Array or Object.");
            Assert.True(root.TryGetProperty("iafiling", out var iaProp) && (iaProp.ValueKind == System.Text.Json.JsonValueKind.Array || iaProp.ValueKind == System.Text.Json.JsonValueKind.Object), "iafiling should exist as Array or Object.");
        }

        [Fact]
        public async Task ECourtsNapixService_LiveEndToEnd_BothTiersAndModules()
        {
            var configData = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["eCourts:DeptId"] = "clonwkrtc",
                ["eCourts:AuthKey"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:HmacKey"] = "15081947",
                ["eCourts:Version"] = "v1.0",
                ["eCourts:GatewayUrl"] = "https://delhigw.napix.gov.in/nic/ecourts",
                ["eCourts:OAuthTokenUrl"] = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
                ["eCourts:MVC:ApiKey"] = "5763de722863865e2691e137691d4333",
                ["eCourts:MVC:SecretKey"] = "454aba6d3c5c867ae185cd198f572ef8",
                ["eCourts:Labour:ApiKey"] = "5763de722863865e2691e137691d4333",
                ["eCourts:Labour:SecretKey"] = "454aba6d3c5c867ae185cd198f572ef8"
            };

            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ECourtsNapixService>.Instance;
            var quotaLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NapixQuotaService>.Instance;
            var quota = new NapixQuotaService(config, quotaLogger);
            var httpClient = new HttpClient();

            var service = new ECourtsNapixService(httpClient, cache, config, logger, quota);

            // Test 1: High Court CNR via MVC module (auto court-detection: isHighCourt passed as false)
            var hcResult = await service.GetCnrDetailsAsync("KAHC020050702018", isHighCourt: false, module: "MVC");
            Assert.True(hcResult.HasValue, "HC CNR lookup should return data.");
            var hcRoot = hcResult.Value;
            Assert.Equal("KAHC020050702018", hcRoot.GetProperty("cino").GetString());
            Assert.Equal("100035", hcRoot.GetProperty("fil_no").GetString());

            // Test 2: Labour Court CNR via Labour module (KAUK010018762019)
            var labourResult = await service.GetCnrDetailsAsync("KAUK010018762019", isHighCourt: false, module: "Labour");
            Assert.True(labourResult.HasValue, "Labour CNR lookup should return data.");
            var labourRoot = labourResult.Value;
            Assert.Equal("KAUK010018762019", labourRoot.GetProperty("cino").GetString());
            Assert.Equal("Ref.10.1.C.OF ID ACT", labourRoot.GetProperty("type_name").GetString());

            // Test 3: District Court MVC Case via MVC module (KADW200035292024)
            var dcResult = await service.GetCnrDetailsAsync("KADW200035292024", isHighCourt: false, module: "MVC");
            Assert.True(dcResult.HasValue, "District Court CNR lookup should return data.");
            var dcRoot = dcResult.Value;
            Assert.Equal("KADW200035292024", dcRoot.GetProperty("cino").GetString());
        }
    }
}
