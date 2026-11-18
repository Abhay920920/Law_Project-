using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MVCCaseManagement.Services.Storage;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class PhysicalFileStorageServiceTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly PhysicalFileStorageService _service;

        public PhysicalFileStorageServiceTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "StorageServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);

            string webRoot = Path.Combine(_testRoot, "wwwroot");
            Directory.CreateDirectory(webRoot);

            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.ContentRootPath).Returns(_testRoot);
            _mockEnv.Setup(e => e.WebRootPath).Returns(webRoot);

            string vaultRoot = Path.Combine(_testRoot, "custom_vault");
            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["Storage:UploadsDirectory"]).Returns(vaultRoot);

            _service = new PhysicalFileStorageService(
                _mockEnv.Object,
                _mockConfig.Object,
                NullLogger<PhysicalFileStorageService>.Instance
            );
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }
            }
            catch { /* non-critical */ }
        }

        [Fact]
        public void GetVaultRootPath_ReturnsConfiguredDirectory()
        {
            string vault = _service.GetVaultRootPath();
            Assert.False(string.IsNullOrEmpty(vault));
            Assert.True(Directory.Exists(vault));
        }

        [Fact]
        public async Task SaveFileAsync_NullOrEmptyFile_ReturnsExistingPath()
        {
            string result = await _service.SaveFileAsync(null, "punishments", "/existing/path.pdf");
            Assert.Equal("/existing/path.pdf", result);
        }

        [Fact]
        public async Task SaveFileAsync_DisallowedExtension_ThrowsException()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("malicious.exe");
            fileMock.Setup(f => f.Length).Returns(1024);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SaveFileAsync(fileMock.Object, "punishments"));
        }

        [Fact]
        public async Task SaveFileAsync_ExceedsSizeLimit_ThrowsException()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("large.pdf");
            fileMock.Setup(f => f.Length).Returns(60 * 1024 * 1024); // 60 MB

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SaveFileAsync(fileMock.Object, "punishments"));
        }

        [Fact]
        public async Task SaveFileAsync_ValidPdfFile_SavesToVaultAndReturnsRelativeUrl()
        {
            byte[] pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Legal Order Content");

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("Order_Copy.pdf");
            fileMock.Setup(f => f.Length).Returns(pdfBytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken ct) => new MemoryStream(pdfBytes).CopyToAsync(target, ct));

            string relativeUrl = await _service.SaveFileAsync(fileMock.Object, "punishments");

            Assert.StartsWith("/uploads/punishments/", relativeUrl);
            Assert.EndsWith(".pdf", relativeUrl);

            // Locate file physical path
            string? physicalPath = _service.LocatePhysicalFile(relativeUrl);
            Assert.NotNull(physicalPath);
            Assert.True(File.Exists(physicalPath));
            Assert.True(_service.FileExists(relativeUrl));
        }

        [Fact]
        public void LocatePhysicalFile_PathTraversalAttempt_ReturnsNull()
        {
            string? result = _service.LocatePhysicalFile("../../windows/system32/cmd.exe");
            Assert.Null(result);
        }

        [Fact]
        public void LocatePhysicalFile_NonExistentFile_ReturnsNull()
        {
            string? result = _service.LocatePhysicalFile("punishments/does_not_exist_9999.pdf");
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteFileIfExists_RemovesFileFromVaultAndLegacyFolder()
        {
            byte[] pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Legal Order Content");
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("To_Delete.pdf");
            fileMock.Setup(f => f.Length).Returns(pdfBytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken ct) => new MemoryStream(pdfBytes).CopyToAsync(target, ct));

            string relativeUrl = await _service.SaveFileAsync(fileMock.Object, "claims");
            Assert.True(_service.FileExists(relativeUrl));

            bool deleted = _service.DeleteFileIfExists(relativeUrl);
            Assert.True(deleted);
            Assert.False(_service.FileExists(relativeUrl));
        }

        [Fact]
        public async Task SaveFileAsync_WithExistingFile_DeletesOldFileAndSavesNew()
        {
            byte[] pdfBytes1 = Encoding.UTF8.GetBytes("%PDF-1.4 First Version");
            var fileMock1 = new Mock<IFormFile>();
            fileMock1.Setup(f => f.FileName).Returns("Version1.pdf");
            fileMock1.Setup(f => f.Length).Returns(pdfBytes1.Length);
            fileMock1.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes1));
            fileMock1.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken ct) => new MemoryStream(pdfBytes1).CopyToAsync(target, ct));

            string url1 = await _service.SaveFileAsync(fileMock1.Object, "orders");
            Assert.True(_service.FileExists(url1));

            byte[] pdfBytes2 = Encoding.UTF8.GetBytes("%PDF-1.4 Second Version");
            var fileMock2 = new Mock<IFormFile>();
            fileMock2.Setup(f => f.FileName).Returns("Version2.pdf");
            fileMock2.Setup(f => f.Length).Returns(pdfBytes2.Length);
            fileMock2.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes2));
            fileMock2.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken ct) => new MemoryStream(pdfBytes2).CopyToAsync(target, ct));

            string url2 = await _service.SaveFileAsync(fileMock2.Object, "orders", existingFilePath: url1);

            Assert.True(_service.FileExists(url2));
            Assert.False(_service.FileExists(url1));
        }

        [Fact]
        public async Task SaveFileAsync_ValidJpegImage_SavesSuccessfully()
        {
            // JPEG magic bytes: FF D8 FF
            byte[] jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("Photo_Evidence.jpg");
            fileMock.Setup(f => f.Length).Returns(jpegBytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(jpegBytes));
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken ct) => new MemoryStream(jpegBytes).CopyToAsync(target, ct));

            string url = await _service.SaveFileAsync(fileMock.Object, "evidence");

            Assert.StartsWith("/uploads/evidence/", url);
            Assert.EndsWith(".jpg", url);
            Assert.True(_service.FileExists(url));
        }

        [Fact]
        public void LocatePhysicalFile_UrlWithPercent20Spaces_ResolvesCorrectly()
        {
            string vaultRoot = _service.GetVaultRootPath();
            string dir = Path.Combine(vaultRoot, "punishments");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, "Order with spaces.pdf");
            File.WriteAllText(filePath, "%PDF-1.4 test");

            string? resolved = _service.LocatePhysicalFile("/uploads/punishments/Order%20with%20spaces.pdf");
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
        }

        [Fact]
        public void LocatePhysicalFile_WithQueryString_ResolvesCorrectly()
        {
            string vaultRoot = _service.GetVaultRootPath();
            string dir = Path.Combine(vaultRoot, "judgments");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, "Award_Copy.pdf");
            File.WriteAllText(filePath, "%PDF-1.4 test");

            string? resolved = _service.LocatePhysicalFile("/uploads/judgments/Award_Copy.pdf?v=12345&t=67890");
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
        }

        [Fact]
        public void LocatePhysicalFile_WithUploadsVaultPrefix_ResolvesCorrectly()
        {
            string vaultRoot = _service.GetVaultRootPath();
            string dir = Path.Combine(vaultRoot, "orders");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, "Interim_Order.pdf");
            File.WriteAllText(filePath, "%PDF-1.4 test");

            string? resolved = _service.LocatePhysicalFile("uploads_vault/orders/Interim_Order.pdf");
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
        }

        [Fact]
        public void LocatePhysicalFile_InsideContentRootUploadsVault_ResolvesCorrectly()
        {
            // Simulate Plesk httpdocs\uploads_vault structure
            string pleskVault = Path.Combine(_testRoot, "uploads_vault", "judgments");
            Directory.CreateDirectory(pleskVault);
            string filePath = Path.Combine(pleskVault, "Plesk_Adverse_Award.pdf");
            File.WriteAllText(filePath, "%PDF-1.4 test adverse award");

            string? resolved = _service.LocatePhysicalFile("/uploads/judgments/Plesk_Adverse_Award.pdf");
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
        }

        [Fact]
        public void LocatePhysicalFile_DeepSubdirectorySearch_ResolvesCorrectly()
        {
            // Simulate deeply nested file
            string nestedDir = Path.Combine(_testRoot, "uploads_vault", "Labour", "reinstated", "nested_folder");
            Directory.CreateDirectory(nestedDir);
            string filePath = Path.Combine(nestedDir, "DeeplyNestedDoc.pdf");
            File.WriteAllText(filePath, "%PDF-1.4 nested");

            // Look up by filename only or partial path
            string? resolved = _service.LocatePhysicalFile("DeeplyNestedDoc.pdf");
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
        }

        [Fact]
        public async Task SaveFileAsync_DualStorage_WritesToBothPrimaryAndSecondaryVaultsOutsidePublish()
        {
            // Setup dual vault test environment
            string tempBase = Path.Combine(Path.GetTempPath(), "DualStorageTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBase);
            try
            {
                string publishDir = Path.Combine(tempBase, "law.itnwkrtc.in", "publish");
                Directory.CreateDirectory(publishDir);

                string primaryVault = Path.Combine(tempBase, "law.itnwkrtc.in", "uploads_vault");
                string secondaryVault = Path.Combine(tempBase, "httpdocs", "uploads_vault");

                var mockEnv = new Mock<IWebHostEnvironment>();
                mockEnv.Setup(e => e.ContentRootPath).Returns(publishDir);
                mockEnv.Setup(e => e.WebRootPath).Returns(Path.Combine(publishDir, "wwwroot"));

                var mockConfig = new Mock<IConfiguration>();
                mockConfig.Setup(c => c["Storage:UploadsDirectory"]).Returns("..\\uploads_vault");
                mockConfig.Setup(c => c["Storage:SecondaryUploadsDirectory"]).Returns("..\\..\\httpdocs\\uploads_vault");

                var dualService = new PhysicalFileStorageService(
                    mockEnv.Object,
                    mockConfig.Object,
                    NullLogger<PhysicalFileStorageService>.Instance
                );

                // Verify both vaults are outside publish
                Assert.Equal(Path.GetFullPath(primaryVault), Path.GetFullPath(dualService.GetVaultRootPath()));
                Assert.Equal(Path.GetFullPath(secondaryVault), Path.GetFullPath(dualService.GetSecondaryVaultRootPath()!));
                Assert.DoesNotContain("publish", dualService.GetVaultRootPath());
                Assert.DoesNotContain("publish", dualService.GetSecondaryVaultRootPath());

                // Save a test file
                byte[] pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 NWKRTC Dual Vault Test Document");
                var fileMock = new Mock<IFormFile>();
                fileMock.Setup(f => f.FileName).Returns("Dual_Award.pdf");
                fileMock.Setup(f => f.Length).Returns(pdfBytes.Length);
                fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(pdfBytes));
                fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns((Stream stream, CancellationToken token) => stream.WriteAsync(pdfBytes, 0, pdfBytes.Length, token));

                string savedUrl = await dualService.SaveFileAsync(fileMock.Object, "judgments");

                Assert.StartsWith("/uploads/judgments/", savedUrl);
                string fileName = Path.GetFileName(savedUrl);

                // Assert file exists in PRIMARY vault (law.itnwkrtc.in/uploads_vault)
                string primaryExpectedFile = Path.Combine(primaryVault, "judgments", fileName);
                Assert.True(File.Exists(primaryExpectedFile), $"File missing in primary vault: {primaryExpectedFile}");

                // Assert file ALSO exists in SECONDARY vault (itnwkrtc.in/httpdocs/uploads_vault)
                string secondaryExpectedFile = Path.Combine(secondaryVault, "judgments", fileName);
                Assert.True(File.Exists(secondaryExpectedFile), $"File missing in secondary vault: {secondaryExpectedFile}");

                // Assert LocatePhysicalFile resolves it
                string? located = dualService.LocatePhysicalFile(savedUrl);
                Assert.NotNull(located);
                Assert.True(File.Exists(located));

                // Assert DeleteFileIfExists removes from BOTH
                bool deleted = dualService.DeleteFileIfExists(savedUrl);
                Assert.True(deleted);
                Assert.False(File.Exists(primaryExpectedFile));
                Assert.False(File.Exists(secondaryExpectedFile));
            }
            finally
            {
                try { Directory.Delete(tempBase, recursive: true); } catch { }
            }
        }

        [Fact]
        public void MigrateLegacyPublishFiles_SafelyCopiesFilesFromLegacyPublishToPersistentVaults()
        {
            string tempBase = Path.Combine(Path.GetTempPath(), "MigrationTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBase);
            try
            {
                string publishDir = Path.Combine(tempBase, "law.itnwkrtc.in", "publish");
                string legacyVaultDir = Path.Combine(publishDir, "uploads_vault", "claims");
                Directory.CreateDirectory(legacyVaultDir);

                // Put legacy file inside publish\uploads_vault
                string legacyFile = Path.Combine(legacyVaultDir, "LegacyClaim.pdf");
                File.WriteAllText(legacyFile, "%PDF-1.4 Legacy Claim Content from Nov 18");

                // Put legacy file inside publish\wwwroot\uploads
                string legacyWebRootDir = Path.Combine(publishDir, "wwwroot", "uploads", "judgments");
                Directory.CreateDirectory(legacyWebRootDir);
                string legacyWebFile = Path.Combine(legacyWebRootDir, "LegacyWebRoot.pdf");
                File.WriteAllText(legacyWebFile, "%PDF-1.4 Legacy WebRoot Content from Nov 17");

                string primaryVault = Path.Combine(tempBase, "law.itnwkrtc.in", "uploads_vault");
                string secondaryVault = Path.Combine(tempBase, "httpdocs", "uploads_vault");

                var mockEnv = new Mock<IWebHostEnvironment>();
                mockEnv.Setup(e => e.ContentRootPath).Returns(publishDir);
                mockEnv.Setup(e => e.WebRootPath).Returns(Path.Combine(publishDir, "wwwroot"));

                var mockConfig = new Mock<IConfiguration>();
                mockConfig.Setup(c => c["Storage:UploadsDirectory"]).Returns("..\\uploads_vault");
                mockConfig.Setup(c => c["Storage:SecondaryUploadsDirectory"]).Returns("..\\..\\httpdocs\\uploads_vault");

                // Initialize service — should auto-migrate the legacy file
                var service = new PhysicalFileStorageService(
                    mockEnv.Object,
                    mockConfig.Object,
                    NullLogger<PhysicalFileStorageService>.Instance
                );

                string migratedPrimary = Path.Combine(primaryVault, "claims", "LegacyClaim.pdf");
                string migratedSecondary = Path.Combine(secondaryVault, "claims", "LegacyClaim.pdf");

                string migratedWebPrimary = Path.Combine(primaryVault, "judgments", "LegacyWebRoot.pdf");
                string migratedWebSecondary = Path.Combine(secondaryVault, "judgments", "LegacyWebRoot.pdf");

                Assert.True(File.Exists(migratedPrimary), $"Legacy file was not migrated to primary vault: {migratedPrimary}");
                Assert.True(File.Exists(migratedSecondary), $"Legacy file was not migrated to secondary vault: {migratedSecondary}");
                Assert.True(File.Exists(migratedWebPrimary), $"Legacy webroot file was not migrated to primary vault: {migratedWebPrimary}");
                Assert.True(File.Exists(migratedWebSecondary), $"Legacy webroot file was not migrated to secondary vault: {migratedWebSecondary}");
            }
            finally
            {
                try { Directory.Delete(tempBase, recursive: true); } catch { }
            }
        }
    }
}
