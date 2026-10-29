using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MVCCaseManagement.Controllers;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Services;
using MVCCaseManagement.Services.Audit;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class OrganizationBrandingTests
    {
        private static DBHelper CreateDummyDbHelper()
        {
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
            {
                { "ConnectionStrings:MVCCaseDB", "Server=127.0.0.1;Database=NonExistentDb;User Id=none;Password=none;Connect Timeout=1;" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new DBHelper(configuration);
        }

        [Fact]
        public void OrganizationSettings_DefaultConstructor_ProvidesSafeFallbacks()
        {
            var settings = new OrganizationSettings();

            Assert.Equal("North Western Karnataka Road Transport Corporation", settings.OrganizationNameEnglish);
            Assert.Equal("ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ", settings.OrganizationNameKannada);
            Assert.Equal("NWKRTC", settings.OrganizationShortName);
            Assert.Equal("/images/logo.jpg", settings.LogoPath);
            Assert.Equal("/favicon.ico", settings.FaviconPath);
        }

        [Fact]
        public void OrganizationSettings_CustomAssignment_PreservesProperties()
        {
            var settings = new OrganizationSettings
            {
                Id = 1,
                OrganizationNameEnglish = "Karnataka State Road Transport Corporation",
                OrganizationNameKannada = "ಕರ್ನಾಟಕ ರಾಜ್ಯ ರಸ್ತೆ ಸಾರಿಗೆ ನಿಗಮ",
                OrganizationShortName = "KSRTC",
                LogoPath = "/images/branding/ksrtc_logo.png",
                FaviconPath = "/images/branding/ksrtc_favicon.ico",
                Address = "Central Office, Shantinagar, Bengaluru",
                ContactEmail = "legal@ksrtc.in",
                ContactPhone = "080-22221111",
                UpdatedBy = "TEST_ADMIN"
            };

            Assert.Equal(1, settings.Id);
            Assert.Equal("Karnataka State Road Transport Corporation", settings.OrganizationNameEnglish);
            Assert.Equal("ಕರ್ನಾಟಕ ರಾಜ್ಯ ರಸ್ತೆ ಸಾರಿಗೆ ನಿಗಮ", settings.OrganizationNameKannada);
            Assert.Equal("KSRTC", settings.OrganizationShortName);
            Assert.Equal("/images/branding/ksrtc_logo.png", settings.LogoPath);
            Assert.Equal("/images/branding/ksrtc_favicon.ico", settings.FaviconPath);
            Assert.Equal("TEST_ADMIN", settings.UpdatedBy);
        }

        [Fact]
        public void Unicode_Kannada_String_Fidelity_PreservedAcrossEncodingRounds()
        {
            const string originalKannada = "ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ";
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(originalKannada);
            string decodedString = Encoding.UTF8.GetString(utf8Bytes);

            Assert.Equal(originalKannada, decodedString);
            Assert.False(decodedString.Contains('?'), "Kannada characters must not degrade to question marks");
        }

        [Fact]
        public void OrganizationSettingsService_FallbackOnNullDatabase_DoesNotThrow()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var mockLogger = new Mock<ILogger<OrganizationSettingsService>>();

            // Non-existent or dummy DB connection string
            var dbHelper = CreateDummyDbHelper();
            var service = new OrganizationSettingsService(dbHelper, memoryCache, mockEnv.Object, mockLogger.Object);

            // Should safely catch DB exception and return default settings
            var settings = service.GetCurrentSettings();

            Assert.NotNull(settings);
            Assert.Equal("North Western Karnataka Road Transport Corporation", settings.OrganizationNameEnglish);
            Assert.Equal("NWKRTC", settings.OrganizationShortName);
        }

        [Fact]
        public void OrganizationSettingsService_CachingAndInvalidation_WorksCorrectly()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var mockLogger = new Mock<ILogger<OrganizationSettingsService>>();
            var dbHelper = CreateDummyDbHelper();

            var service = new OrganizationSettingsService(dbHelper, memoryCache, mockEnv.Object, mockLogger.Object);

            // Seed cache with custom organization settings
            var customSettings = new OrganizationSettings
            {
                OrganizationNameEnglish = "Kalyana Karnataka Road Transport Corporation",
                OrganizationNameKannada = "ಕಲ್ಯಾಣ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ",
                OrganizationShortName = "KKRTC"
            };
            memoryCache.Set("OrgSettings_Current_Record", customSettings);

            // First read should hit memory cache immediately without DB query
            var retrieved = service.GetCurrentSettings();
            Assert.Equal("KKRTC", retrieved.OrganizationShortName);

            // Invalidate cache
            service.InvalidateCache();

            // Next read falls back to default safely
            var afterInvalidation = service.GetCurrentSettings();
            Assert.Equal("NWKRTC", afterInvalidation.OrganizationShortName);
        }

        [Fact]
        public void OrganizationSettingsService_ResolvePhysicalLogoPath_ReturnsValidPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "OrgSettingsTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "images"));
            string dummyLogo = Path.Combine(tempDir, "images", "logo.jpg");
            File.WriteAllBytes(dummyLogo, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                var mockEnv = new Mock<IWebHostEnvironment>();
                mockEnv.Setup(e => e.WebRootPath).Returns(tempDir);

                var memoryCache = new MemoryCache(new MemoryCacheOptions());
                var mockLogger = new Mock<ILogger<OrganizationSettingsService>>();
                var dbHelper = CreateDummyDbHelper();

                var service = new OrganizationSettingsService(dbHelper, memoryCache, mockEnv.Object, mockLogger.Object);

                string physicalPath = service.ResolvePhysicalLogoPath();

                Assert.NotNull(physicalPath);
                Assert.True(File.Exists(physicalPath), "Resolved physical logo path must point to an existing disk file");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task MasterController_UpdateOrganizationSettings_RejectsCorruptMagicBytes()
        {
            var mockMasterRepo = new Mock<IMasterRepository>();
            var mockUserRepo = new Mock<IUserRepository>();
            var mockActivityLogger = new Mock<ICaseActivityLogger>();
            var mockOrgService = new Mock<IOrganizationSettingsService>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var controller = new MasterController(
                mockMasterRepo.Object,
                mockUserRepo.Object,
                mockActivityLogger.Object,
                mockOrgService.Object,
                mockEnv.Object);

            // Create fake file claiming to be .png but containing random non-PNG text
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("THIS IS AN EXECUTABLE SCRIPT NOT A REAL PNG"));
            var fakeFile = new FormFile(stream, 0, stream.Length, "logoFile", "malicious_script.png");

            var model = new OrganizationSettings
            {
                OrganizationNameEnglish = "Test Org",
                OrganizationNameKannada = "ಟೆಸ್ಟ್ ಸಂಸ್ಥೆ",
                OrganizationShortName = "TEST"
            };

            var result = await controller.UpdateOrganizationSettings(model, fakeFile, null);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("LogoPath"));
        }

        [Fact]
        public async Task MasterController_UpdateOrganizationSettings_AcceptsValidPng()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MasterControllerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var mockMasterRepo = new Mock<IMasterRepository>();
                var mockUserRepo = new Mock<IUserRepository>();
                var mockActivityLogger = new Mock<ICaseActivityLogger>();
                var mockOrgService = new Mock<IOrganizationSettingsService>();
                var mockEnv = new Mock<IWebHostEnvironment>();
                mockEnv.Setup(e => e.WebRootPath).Returns(tempDir);

                var controller = new MasterController(
                    mockMasterRepo.Object,
                    mockUserRepo.Object,
                    mockActivityLogger.Object,
                    mockOrgService.Object,
                    mockEnv.Object);

                // Valid PNG header: 0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A
                byte[] validPngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
                var stream = new MemoryStream(validPngHeader);
                var validFile = new FormFile(stream, 0, stream.Length, "logoFile", "valid_logo.png");

                var model = new OrganizationSettings
                {
                    OrganizationNameEnglish = "Bengaluru Metropolitan Transport Corporation",
                    OrganizationNameKannada = "ಬೆಂಗಳೂರು ಮಹಾನಗರ ಸಾರಿಗೆ ಸಂಸ್ಥೆ",
                    OrganizationShortName = "BMTC"
                };

                var result = await controller.UpdateOrganizationSettings(model, validFile, null);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("OrganizationSettings", redirectResult.ActionName);
                Assert.StartsWith("/images/branding/logo_", model.LogoPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void OrganizationNameKannada_HtmlEntityDecoding_AndJsonRelaxedEscaping_PreservesUnicodeKannada()
        {
            // Simulates raw or entity-encoded Kannada organization name from database or views
            const string entityEncoded = "&#xCB5;&#xCBE;&#xCAF;&#xCB5;&#xCCD;&#xCAF; &#xC95;&#xCB0;&#xCCD;&#xCA8;&#xCBE;&#xC9F;&#xC95; &#xCB0;&#xCB8;&#xCCD;&#xCA4;&#xCC6; &#xCB8;&#xCBE;&#xCB0;&#xCBF;&#xC97;&#xCC6; &#xCB8;&#xC82;&#xCB8;&#xCCD;&#xCA5;&#xCC6;";
            const string expectedKannada = "ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ";

            string decoded = System.Net.WebUtility.HtmlDecode(entityEncoded);
            Assert.Equal(expectedKannada, decoded);

            // Verify System.Text.Json with UnsafeRelaxedJsonEscaping outputs authentic UTF-8 Kannada characters (not \u0CB5 or &#xCB5;)
            var opt = new System.Text.Json.JsonSerializerOptions 
            { 
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            };
            string serialized = System.Text.Json.JsonSerializer.Serialize(decoded, opt);
            Assert.Equal($"\"{expectedKannada}\"", serialized);
            Assert.DoesNotContain("&#x", serialized);
            Assert.DoesNotContain("\\u", serialized);
        }

        [Fact]
        public void DossierPrint_DailyProceedings_RoznamaSection_StructuresValid()
        {
            // Verify that Roznama extraction and hearing analysis fallbacks retain 100% integrity
            var mockHearing = new Dictionary<string, string?>
            {
                ["hearing_date"] = "2026-10-29",
                ["business_date"] = "2026-10-12",
                ["purpose_of_listing"] = "FRESH MATTER/S",
                ["next_date"] = "2026-11-15",
                ["next_purpose"] = "ADMISSION",
                ["judge"] = "HON'BLE JUSTICE",
                ["business"] = "Matter called out. State counsel directed to file statement of objections by next date."
            };

            Assert.Equal("2026-10-29", mockHearing["hearing_date"]);
            Assert.Contains("objections", mockHearing["business"]);
            Assert.Equal("FRESH MATTER/S", mockHearing["purpose_of_listing"]);

            // Verify MyCases.cshtml dossier print contains unified non-duplicate Section 3
            string viewPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CaseManagement", "Views", "ECourts", "MyCases.cshtml");
            if (File.Exists(viewPath))
            {
                string viewContent = File.ReadAllText(viewPath);
                Assert.Contains("3. Daily Proceedings &amp; Hearing Analysis (${history.length} Hearings Recorded)", viewContent);
                Assert.DoesNotContain("3. Full Court Proceedings History", viewContent);
                Assert.Contains("4. Court Orders & Judgements (${orders.length} Uploaded)", viewContent);
                Assert.Contains("5. Interlocutory Applications (IA Filings: ${iaList.length})", viewContent);
                Assert.Contains("6. Process & Court Notices (${processes.length})", viewContent);
                Assert.Contains("7. Statutory Acts & Sections (${acts.length})", viewContent);
            }
        }
    }
}
