using System;

namespace MVCCaseManagement.Models
{
    /// <summary>
    /// Represents the centralized, database-backed organization branding configuration.
    /// Eliminates hardcoded names and static logo paths across the application.
    /// </summary>
    public class OrganizationSettings
    {
        public int Id { get; set; }

        /// <summary>
        /// Official English name of the organization (e.g., North Western Karnataka Road Transport Corporation).
        /// </summary>
        public string OrganizationNameEnglish { get; set; } = "North Western Karnataka Road Transport Corporation";

        /// <summary>
        /// Official Kannada name in Unicode (e.g., ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ).
        /// </summary>
        public string OrganizationNameKannada { get; set; } = "ವಾಯವ್ಯ ಕರ್ನಾಟಕ ರಸ್ತೆ ಸಾರಿಗೆ ಸಂಸ್ಥೆ";

        /// <summary>
        /// Short corporate abbreviation or acronym (e.g., NWKRTC).
        /// </summary>
        public string OrganizationShortName { get; set; } = "NWKRTC";

        /// <summary>
        /// Web-accessible relative URL path to the primary corporate logo (e.g., /images/logo.jpg or /uploads/branding/...).
        /// </summary>
        public string LogoPath { get; set; } = "/images/logo.jpg";

        /// <summary>
        /// Web-accessible relative URL path to the browser favicon.
        /// </summary>
        public string? FaviconPath { get; set; } = "/favicon.ico";

        /// <summary>
        /// Corporate Headquarters / Legal Directorate physical address.
        /// </summary>
        public string? Address { get; set; } = "Central Office, Gokul Road, Hubballi - 580030";

        /// <summary>
        /// Official contact email address for legal / administrative correspondence.
        /// </summary>
        public string? ContactEmail { get; set; } = "clo@nwkrtc.in";

        /// <summary>
        /// Official corporate contact phone / PBX number.
        /// </summary>
        public string? ContactPhone { get; set; } = "0836-2237200";

        /// <summary>
        /// Timestamp of last configuration update in UTC.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Username of the administrator who performed the last update.
        /// </summary>
        public string? UpdatedBy { get; set; } = "SYSTEM";

        /// <summary>
        /// SQL Server concurrency timestamp for optimistic concurrency checks.
        /// </summary>
        public byte[]? RowVersion { get; set; }
    }
}
