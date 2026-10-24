using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MVCCaseManagement.Controllers;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Services.Audit;
using MVCCaseManagement.Utils;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class SecurityRemediationValidationTests
    {
        [Fact]
        public async Task ECourtsRepository_UpdateCaseCnrLinkAsync_InvalidPrimaryKeyColumn_ThrowsArgumentException()
        {
            var myConfig = new Dictionary<string, string?>
            {
                {"ConnectionStrings:MVCCaseDB", "Server=(localdb)\\mssqllocaldb;Database=TestDB;Trusted_Connection=True;"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(myConfig).Build();
            var db = new DBHelper(config);
            var repo = new ECourtsRepository(db);

            // Hostile SQL injection payload in primaryKeyCol
            string maliciousCol = "CaseID]; DROP TABLE MVC_CASES; --";

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await repo.UpdateCaseCnrLinkAsync("MVC_CASES", maliciousCol, 10, "KABG010001232026");
            });
        }

        [Fact]
        public async Task ECourtsRepository_UpdateCaseCnrLinkAsync_InvalidTable_ThrowsArgumentException()
        {
            var myConfig = new Dictionary<string, string?>
            {
                {"ConnectionStrings:MVCCaseDB", "Server=(localdb)\\mssqllocaldb;Database=TestDB;Trusted_Connection=True;"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(myConfig).Build();
            var db = new DBHelper(config);
            var repo = new ECourtsRepository(db);

            // Hostile unapproved table
            string maliciousTable = "tbl_Users";

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await repo.UpdateCaseCnrLinkAsync(maliciousTable, "CaseID", 10, "KABG010001232026");
            });
        }

        [Fact]
        public void NetworkDeviceHelper_RemoteIpResolution_HandlesIPv4Directly()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.10.45");

            string mac = NetworkDeviceHelper.GetClientMacAddress(context);

            Assert.NotNull(mac);
            Assert.NotEmpty(mac);
            Assert.NotEqual("N/A", mac);
        }

        [Fact]
        public void UrlSecurityValidator_LocalMetadataAndLoopback_AreBlocked()
        {
            Assert.False(UrlSecurityValidator.IsSafeExternalUrl("http://127.0.0.1", out _));
            Assert.False(UrlSecurityValidator.IsSafeExternalUrl("https://169.254.169.254/latest/meta-data/", out _));
            Assert.False(UrlSecurityValidator.IsSafeExternalUrl("https://localhost:5000", out _));
        }

        [Fact]
        public void UrlSecurityValidator_LegitimateCourtUrls_AreAllowed()
        {
            Assert.True(UrlSecurityValidator.IsSafeExternalUrl("https://services.ecourts.gov.in/ecourtindia_v6/", out var uri1));
            Assert.NotNull(uri1);
            Assert.Equal("services.ecourts.gov.in", uri1!.Host);

            Assert.True(UrlSecurityValidator.IsSafeExternalUrl("https://delhigw.napix.gov.in/nic/ecourts", out var uri2));
            Assert.NotNull(uri2);
            Assert.Equal("delhigw.napix.gov.in", uri2!.Host);
        }
    }
}
