using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.DAL;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class CaseFilterIntegrationTests
    {
        [Fact]
        public void Verify_Case19_2026_In_MFAPending_Not_SentToCO()
        {
            var myConfig = new Dictionary<string, string?>
            {
                {"ConnectionStrings:MVCCaseDB", "Data Source=198.38.89.31;Initial Catalog=Admin_Law;User ID=admin_Law;Password=4c4H_0l8q;Connection Timeout=60;Encrypt=True;TrustServerCertificate=True;Pooling=true;Max Pool Size=200;MultiSubnetFailover=True;ConnectRetryCount=3;ConnectRetryInterval=10;"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(myConfig).Build();
            var db = new DBHelper(config);
            var repo = new CaseRepository(db);

            // Hubballi Rural division is 8
            var sentToCoCases = repo.GetAllCases(8, 1, 50, null, "SentToCO").ToList();
            var caseInSent = sentToCoCases.FirstOrDefault(c => c.MVCNo == "19" && c.MVCYear == 2026);
            Assert.Null(caseInSent);

            var mfaPendingCases = repo.GetAllCases(8, 1, 50, null, "MFAPending").ToList();
            var caseInMfa = mfaPendingCases.FirstOrDefault(c => c.MVCNo == "19" && c.MVCYear == 2026);
            Assert.NotNull(caseInMfa);
            Assert.Equal("19", caseInMfa.MVCNo);
            Assert.Equal(2026, caseInMfa.MVCYear);
        }
    }
}
