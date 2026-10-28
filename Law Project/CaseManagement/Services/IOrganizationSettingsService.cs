using System.Threading.Tasks;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Services
{
    /// <summary>
    /// Service contract for managing and retrieving centralized organization branding settings.
    /// Supports high-performance in-memory caching with immediate invalidation upon updates.
    /// </summary>
    public interface IOrganizationSettingsService
    {
        /// <summary>
        /// Retrieves the current organization settings synchronously from cache or database.
        /// Optimized for Razor layouts and views. Returns a safe fallback if database is inaccessible.
        /// </summary>
        OrganizationSettings GetCurrentSettings();

        /// <summary>
        /// Asynchronously retrieves current organization settings.
        /// </summary>
        Task<OrganizationSettings> GetSettingsAsync();

        /// <summary>
        /// Updates the organization branding in the SQL Server database and invalidates the cache.
        /// </summary>
        Task<bool> UpdateSettingsAsync(OrganizationSettings settings, string updatedBy);

        /// <summary>
        /// Evicts cached settings forcing a fresh database read on the next request.
        /// </summary>
        void InvalidateCache();

        /// <summary>
        /// Resolves the absolute physical file path on the server disk for the configured logo.
        /// Ensures document and report generators find the physical asset without relying on HTTP URLs.
        /// </summary>
        string ResolvePhysicalLogoPath();
    }
}
