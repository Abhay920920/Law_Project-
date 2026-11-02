using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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
    }
}

