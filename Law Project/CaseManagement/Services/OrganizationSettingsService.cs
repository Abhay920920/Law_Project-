using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Services
{
    /// <summary>
    /// Thread-safe, cached service implementation for managing centralized organization branding.
    /// Eliminates multiple database hits per page render by caching settings for 60 minutes,
    /// with instant cache eviction upon administrative updates.
    /// </summary>
    public class OrganizationSettingsService : IOrganizationSettingsService
    {
        private readonly DBHelper _db;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<OrganizationSettingsService> _logger;

        private const string CacheKey = "OrgSettings_Current_Record";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

        public OrganizationSettingsService(
            DBHelper db,
            IMemoryCache cache,
            IWebHostEnvironment env,
            ILogger<OrganizationSettingsService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public OrganizationSettings GetCurrentSettings()
        {
            if (_cache.TryGetValue(CacheKey, out OrganizationSettings? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var settings = LoadFromDatabase();
                _cache.Set(CacheKey, settings, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = CacheDuration,
                    Priority = CacheItemPriority.High
                });
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load OrganizationSettings from database. Returning safe fallback.");
                return CreateDefaultFallback();
            }
        }

        /// <inheritdoc />
        public async Task<OrganizationSettings> GetSettingsAsync()
        {
            if (_cache.TryGetValue(CacheKey, out OrganizationSettings? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var settings = await LoadFromDatabaseAsync();
                _cache.Set(CacheKey, settings, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = CacheDuration,
                    Priority = CacheItemPriority.High
                });
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load OrganizationSettings asynchronously. Returning safe fallback.");
                return CreateDefaultFallback();
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSettingsAsync(OrganizationSettings settings, string updatedBy)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            const string updateSql = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ORGANIZATION_SETTINGS')
                BEGIN
                    IF EXISTS (SELECT 1 FROM ORGANIZATION_SETTINGS WHERE Id = @Id)
                    BEGIN
                        UPDATE ORGANIZATION_SETTINGS
                        SET OrganizationNameEnglish = @NameEn,
                            OrganizationNameKannada = @NameKn,
                            OrganizationShortName = @ShortName,
                            LogoPath = @LogoPath,
                            FaviconPath = @FaviconPath,
                            Address = @Address,
                            ContactEmail = @ContactEmail,
                            ContactPhone = @ContactPhone,
                            UpdatedAt = SYSUTCDATETIME(),
                            UpdatedBy = @UpdatedBy
                        WHERE Id = @Id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO ORGANIZATION_SETTINGS (
                            OrganizationNameEnglish, OrganizationNameKannada, OrganizationShortName,
                            LogoPath, FaviconPath, Address, ContactEmail, ContactPhone,
                            UpdatedAt, UpdatedBy
                        )
                        VALUES (
                            @NameEn, @NameKn, @ShortName,
                            @LogoPath, @FaviconPath, @Address, @ContactEmail, @ContactPhone,
                            SYSUTCDATETIME(), @UpdatedBy
                        );
                    END
                END";

            var parameters = new[]
            {
                new SqlParameter("@Id", SqlDbType.Int) { Value = settings.Id > 0 ? settings.Id : 1 },
                new SqlParameter("@NameEn", SqlDbType.NVarChar, 250) { Value = settings.OrganizationNameEnglish ?? "" },
                new SqlParameter("@NameKn", SqlDbType.NVarChar, 250) { Value = settings.OrganizationNameKannada ?? "" },
                new SqlParameter("@ShortName", SqlDbType.NVarChar, 50) { Value = settings.OrganizationShortName ?? "" },
                new SqlParameter("@LogoPath", SqlDbType.NVarChar, 500) { Value = settings.LogoPath ?? "/images/logo.jpg" },
                new SqlParameter("@FaviconPath", SqlDbType.NVarChar, 500) { Value = (object?)settings.FaviconPath ?? DBNull.Value },
                new SqlParameter("@Address", SqlDbType.NVarChar, 500) { Value = (object?)settings.Address ?? DBNull.Value },
                new SqlParameter("@ContactEmail", SqlDbType.NVarChar, 150) { Value = (object?)settings.ContactEmail ?? DBNull.Value },
                new SqlParameter("@ContactPhone", SqlDbType.NVarChar, 50) { Value = (object?)settings.ContactPhone ?? DBNull.Value },
                new SqlParameter("@UpdatedBy", SqlDbType.NVarChar, 100) { Value = (object?)updatedBy ?? "ADMIN" }
            };

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand(updateSql, conn);
            cmd.Parameters.AddRange(parameters);
            int rows = await cmd.ExecuteNonQueryAsync();

            InvalidateCache();
            _logger.LogInformation("OrganizationSettings updated by {User}. Cache invalidated.", updatedBy);
            return rows > 0;
        }

        /// <inheritdoc />
        public void InvalidateCache()
        {
            _cache.Remove(CacheKey);
        }

        /// <inheritdoc />
        public string ResolvePhysicalLogoPath()
        {
            var settings = GetCurrentSettings();
            string relative = settings.LogoPath ?? "/images/logo.jpg";
            string trimmed = relative.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(_env.WebRootPath, trimmed);

            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // Fallback to default asset
            string defaultPath = Path.Combine(_env.WebRootPath, "images", "logo.jpg");
            return File.Exists(defaultPath) ? defaultPath : fullPath;
        }

        private OrganizationSettings LoadFromDatabase()
        {
            const string selectSql = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ORGANIZATION_SETTINGS')
                BEGIN
                    SELECT TOP 1 Id, OrganizationNameEnglish, OrganizationNameKannada, OrganizationShortName,
                                 LogoPath, FaviconPath, Address, ContactEmail, ContactPhone,
                                 UpdatedAt, UpdatedBy, RowVersion
                    FROM ORGANIZATION_SETTINGS
                    ORDER BY Id ASC;
                END";

            var dt = _db.ExecuteQuery(selectSql);
            if (dt.Rows.Count > 0)
            {
                return MapRow(dt.Rows[0]);
            }

            return CreateDefaultFallback();
        }

        private async Task<OrganizationSettings> LoadFromDatabaseAsync()
        {
            const string selectSql = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ORGANIZATION_SETTINGS')
                BEGIN
                    SELECT TOP 1 Id, OrganizationNameEnglish, OrganizationNameKannada, OrganizationShortName,
                                 LogoPath, FaviconPath, Address, ContactEmail, ContactPhone,
                                 UpdatedAt, UpdatedBy, RowVersion
                    FROM ORGANIZATION_SETTINGS
                    ORDER BY Id ASC;
                END";

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand(selectSql, conn);
            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            adapter.Fill(dt);

            if (dt.Rows.Count > 0)
            {
                return MapRow(dt.Rows[0]);
            }

            return CreateDefaultFallback();
        }

        private static OrganizationSettings MapRow(DataRow row)
        {
            return new OrganizationSettings
            {
                Id = Convert.ToInt32(row["Id"]),
                OrganizationNameEnglish = row["OrganizationNameEnglish"]?.ToString() ?? "North Western Karnataka Road Transport Corporation",
                OrganizationNameKannada = row["OrganizationNameKannada"]?.ToString() ?? "ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ",
                OrganizationShortName = row["OrganizationShortName"]?.ToString() ?? "NWKRTC",
                LogoPath = row["LogoPath"]?.ToString() ?? "/images/logo.jpg",
                FaviconPath = row["FaviconPath"] != DBNull.Value ? row["FaviconPath"]?.ToString() : "/favicon.ico",
                Address = row["Address"] != DBNull.Value ? row["Address"]?.ToString() : null,
                ContactEmail = row["ContactEmail"] != DBNull.Value ? row["ContactEmail"]?.ToString() : null,
                ContactPhone = row["ContactPhone"] != DBNull.Value ? row["ContactPhone"]?.ToString() : null,
                UpdatedAt = row["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(row["UpdatedAt"]) : DateTime.UtcNow,
                UpdatedBy = row["UpdatedBy"] != DBNull.Value ? row["UpdatedBy"]?.ToString() : null,
                RowVersion = row["RowVersion"] != DBNull.Value ? (byte[])row["RowVersion"] : null
            };
        }

        private static OrganizationSettings CreateDefaultFallback()
        {
            return new OrganizationSettings();
        }
    }
}
