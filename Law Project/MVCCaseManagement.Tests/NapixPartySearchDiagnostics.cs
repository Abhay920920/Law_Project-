using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MVCCaseManagement.Utils;
using Xunit;
using Xunit.Abstractions;

namespace MVCCaseManagement.Tests
{
    public class NapixPartySearchDiagnostics
    {
        private readonly ITestOutputHelper _output;

        public NapixPartySearchDiagnostics(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Test_SearchPartyName_RawJson_And_ParsedItems()
        {
            var configValues = new Dictionary<string, string?>
            {
                ["eCourts:DeptId"] = "clonwkrtc",
                ["eCourts:AuthKey"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:IV"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:HmacKey"] = "15081947",
                ["eCourts:Version"] = "v1.0",
                ["eCourts:GatewayUrl"] = "https://delhigw.napix.gov.in/nic/ecourts",
                ["eCourts:OAuthTokenUrl"] = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
                ["eCourts:MVC:ApiKey"] = "5763de722863865e2691e137691d4333",
                ["eCourts:MVC:SecretKey"] = "454aba6d3c5c867ae185cd198f572ef8"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var httpClient = new HttpClient();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var quotaService = new NapixQuotaService(configuration, NullLogger<NapixQuotaService>.Instance);
            var napixService = new ECourtsNapixService(httpClient, memoryCache, configuration, NullLogger<ECourtsNapixService>.Instance, quotaService);

            // 1. Direct raw API query for Dharwad NWKRTC 2024
            string paramsStr = "est_code=KAHC02|pend_disp=P|litigant_name=NWKRTC|reg_year=2024";
            _output.WriteLine($"[1] Querying raw API: hc-party-name-api/partyname with params: {paramsStr}");
            
            var rawRes = await napixService.QueryApiAsync("hc-party-name-api", "partyname", paramsStr, module: "MVC");
            if (rawRes.HasValue)
            {
                _output.WriteLine($"[Raw Response Kind]: {rawRes.Value.ValueKind}");
                string jsonPreview = JsonSerializer.Serialize(rawRes.Value, new JsonSerializerOptions { WriteIndented = true });
                if (jsonPreview.Length > 2000)
                {
                    _output.WriteLine($"[Raw JSON First 2000 chars]:\n{jsonPreview.Substring(0, 2000)}");
                }
                else
                {
                    _output.WriteLine($"[Raw JSON Full]:\n{jsonPreview}");
                }
            }
            else
            {
                _output.WriteLine("[Raw Response]: NULL or error");
            }

            // 2. Parse via SearchCasesByPartyNameAsync
            var items = await napixService.SearchCasesByPartyNameAsync("KAHC02", "NWKRTC", "2024", "P", isHighCourt: true, module: "MVC");
            _output.WriteLine($"[Parsed Items Count]: {items.Count}");
            if (items.Count > 0)
            {
                var first = items[0];
                _output.WriteLine($"Sample Case: {first.CaseType} {first.RegNumber}/{first.RegYear} CNR:{first.CNRNumber}");
                _output.WriteLine($"Petitioner: {first.PetitionerName} vs Respondent: {first.RespondentName}");
                _output.WriteLine($"NextHearingDate: {first.NextHearingDate?.ToString("yyyy-MM-dd") ?? "NULL"} | Stage: {first.CaseStage} | Court: {first.CourtHall}");
            }
        }

        [Fact]
        public async Task Test_ScanDailyCauseList_Today()
        {
            var configValues = new Dictionary<string, string?>
            {
                ["eCourts:DeptId"] = "clonwkrtc",
                ["eCourts:AuthKey"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:IV"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:HmacKey"] = "15081947",
                ["eCourts:Version"] = "v1.0",
                ["eCourts:GatewayUrl"] = "https://delhigw.napix.gov.in/nic/ecourts",
                ["eCourts:OAuthTokenUrl"] = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
                ["eCourts:MVC:ApiKey"] = "5763de722863865e2691e137691d4333",
                ["eCourts:MVC:SecretKey"] = "454aba6d3c5c867ae185cd198f572ef8"
            };

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
            var httpClient = new HttpClient();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var quotaService = new NapixQuotaService(configuration, NullLogger<NapixQuotaService>.Instance);
            var napixService = new ECourtsNapixService(httpClient, memoryCache, configuration, NullLogger<ECourtsNapixService>.Instance, quotaService);

            // Mock env
            var envMock = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            envMock.Setup(e => e.ContentRootPath).Returns(@"C:\Users\IT\Desktop\Law Project\Law Project\CaseManagement");

            var scannerLogger = NullLogger<Services.ECourts.HighCourtDailyCauseListScannerService>.Instance;
            var scanner = new Services.ECourts.HighCourtDailyCauseListScannerService(napixService, envMock.Object, scannerLogger, null);

            DateTime targetDate = new DateTime(2026, 11, 3);
            string dateStr = targetDate.ToString("yyyy-MM-dd");
            _output.WriteLine($"Inspecting PDF text for Dharwad (KAHC02) on {dateStr} with enhanced regex...");

            var benchesResult = await napixService.GetHighCourtCauselistBenchesAsync("KAHC02", dateStr);
            if (!benchesResult.HasValue) return;

            var root = benchesResult.Value;
            var itemRegex = new Regex(@"(?:^|\s|[A-Za-z0-9\.\-]+?)(\d{1,3}(?:\.\d{1,2})?)\s*([A-Za-z]+(?:\.[A-Za-z]+|\([A-Za-z0-9\-]+\))?(?:\s*NO\.?)?)[\s/]+(\d+/\d{4})", RegexOptions.IgnoreCase);

            int totalFound = 0;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                string bId = prop.Value.TryGetProperty("bench_id", out var bp) ? bp.ToString() : "";
                string jName = prop.Value.TryGetProperty("judge_name", out var jp) ? jp.GetString() ?? "" : "";

                var dtlResult = await napixService.GetHighCourtCauselistDetailsAsync("KAHC02", bId, dateStr);
                if (!dtlResult.HasValue || dtlResult.Value.ValueKind != JsonValueKind.Object) continue;

                foreach (var clProp in dtlResult.Value.EnumerateObject())
                {
                    if (clProp.Value.ValueKind != JsonValueKind.Object) continue;
                    string clId = clProp.Value.TryGetProperty("causelist_id", out var cip) ? cip.ToString() : "";
                    string clType = clProp.Value.TryGetProperty("cause_list_type", out var ctp) ? ctp.GetString() ?? "" : "";

                    var showResult = await napixService.GetHighCourtShowCauselistAsync("KAHC02", bId, clId, dateStr);
                    if (!showResult.HasValue) continue;

                    string? b64 = null;
                    if (showResult.Value.ValueKind == JsonValueKind.String) b64 = showResult.Value.GetString();
                    else if (showResult.Value.ValueKind == JsonValueKind.Object && showResult.Value.TryGetProperty("causelist", out var cp)) b64 = cp.GetString();

                    if (string.IsNullOrWhiteSpace(b64) || b64.Length < 100) continue;

                    byte[] pdfBytes = Convert.FromBase64String(b64);
                    using var stream = new MemoryStream(pdfBytes);
                    using var doc = UglyToad.PdfPig.PdfDocument.Open(stream);

                    var sb = new System.Text.StringBuilder();
                    for (int p = 1; p <= doc.NumberOfPages; p++) sb.AppendLine(doc.GetPage(p).Text);
                    string text = sb.ToString();

                    var matches = itemRegex.Matches(text);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var m = matches[i];
                        string serialNo = m.Groups[1].Value.Trim();
                        string caseType = m.Groups[2].Value.Trim().ToUpperInvariant();
                        string caseNo = m.Groups[3].Value.Trim();

                        int startIndex = m.Index + m.Length;
                        int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
                        string block = (startIndex < text.Length && endIndex > startIndex) ? text.Substring(startIndex, endIndex - startIndex) : "";

                        bool isNwkrtc = System.Text.RegularExpressions.Regex.IsMatch(block, @"\b(NWKRTC|NKWRTC|NORTH\s*WEST|DIVISIONAL\s*CONTROLLER|MANAGING\s*DIRECTOR)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                        System.Text.RegularExpressions.Regex.IsMatch(block, @"\b(KHANNUR|HOSMANI|KINI|KALASOOR|HEGDE|BUDIHAL|BADAWADAGI|BHUTI|MATTI|KALADAGI)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (isNwkrtc)
                        {
                            totalFound++;
                            _output.WriteLine($"  -> FOUND [{jName}] Sl: {serialNo} | {caseType} {caseNo} | Block preview: {block.Substring(0, Math.Min(block.Length, 120)).Replace('\n', ' ')}");
                        }
                    }
                }
            }
            _output.WriteLine($"TOTAL NWKRTC MATTERS FOUND TODAY: {totalFound}");
        }
        [Fact]
        public async Task Test_InspectAllDharwadBenches_Detailed()
        {
            var configValues = new Dictionary<string, string?>
            {
                ["eCourts:DeptId"] = "clonwkrtc",
                ["eCourts:AuthKey"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:IV"] = "tD7Ju6n0Cdf4vxUo",
                ["eCourts:HmacKey"] = "15081947",
                ["eCourts:Version"] = "v1.0",
                ["eCourts:GatewayUrl"] = "https://delhigw.napix.gov.in/nic/ecourts",
                ["eCourts:OAuthTokenUrl"] = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
                ["eCourts:MVC:ApiKey"] = "5763de722863865e2691e137691d4333",
                ["eCourts:MVC:SecretKey"] = "454aba6d3c5c867ae185cd198f572ef8"
            };

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
            var httpClient = new HttpClient();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var quotaService = new NapixQuotaService(configuration, NullLogger<NapixQuotaService>.Instance);
            var napixService = new ECourtsNapixService(httpClient, memoryCache, configuration, NullLogger<ECourtsNapixService>.Instance, quotaService);

            var benchesResult = await napixService.GetHighCourtCauselistBenchesAsync("KAHC02", "2026-11-03");
            if (!benchesResult.HasValue) return;

            foreach (var prop in benchesResult.Value.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                string bId = prop.Value.TryGetProperty("bench_id", out var bp) ? bp.ToString() : "";
                string jName = prop.Value.TryGetProperty("judge_name", out var jp) ? jp.GetString() ?? "" : "";
                string cNo = prop.Value.TryGetProperty("court_no", out var cp) ? cp.GetString() ?? "" : "";

                var dtlResult = await napixService.GetHighCourtCauselistDetailsAsync("KAHC02", bId, "2026-11-03");
                if (!dtlResult.HasValue || dtlResult.Value.ValueKind != JsonValueKind.Object)
                {
                    _output.WriteLine($"Bench: {bId} | Court: {cNo} | Judge: {jName} | No causelists");
                    continue;
                }

                var lists = new List<string>();
                foreach (var clProp in dtlResult.Value.EnumerateObject())
                {
                    if (clProp.Value.ValueKind != JsonValueKind.Object) continue;
                    string clId = clProp.Value.TryGetProperty("causelist_id", out var cip) ? cip.ToString() : "";
                    string clType = clProp.Value.TryGetProperty("cause_list_type", out var ctp) ? ctp.GetString() ?? "" : "";
                    string clNo = clProp.Value.TryGetProperty("causelist_no", out var cnp) ? cnp.GetString() ?? "" : "";
                    lists.Add($"[CL:{clId} Type:{clType} No:{clNo}]");
                }
                _output.WriteLine($"Bench: {bId} | Court: {cNo} | Judge: {jName} | Lists: {string.Join(", ", lists)}");
            }
        }
    }
}

