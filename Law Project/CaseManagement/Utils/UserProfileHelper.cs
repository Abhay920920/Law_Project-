using System;
using System.Security.Claims;

namespace MVCCaseManagement.Utils
{
    public record UserProfileBadgeInfo(
        string Label,
        string Initial,
        string Icon,
        string Title,
        string SubTitle
    );

    public static class UserProfileHelper
    {
        public static UserProfileBadgeInfo GetProfileBadge(ClaimsPrincipal? user)
        {
            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                return new UserProfileBadgeInfo("Guest", "?", "bi-person", "Guest", "Guest");
            }

            var username = user.Identity?.Name ?? "";
            var role = user.FindFirst(ClaimTypes.Role)?.Value 
                       ?? user.FindFirst("Role")?.Value ?? "";
            var fullName = user.FindFirst("FullName")?.Value ?? "";
            var divName = user.FindFirst("DivisionName")?.Value ?? "";
            var divId = user.FindFirst("DivisionID")?.Value ?? "0";

            // 1. Admin
            if (user.IsInRole("Admin") || 
                string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("Admin", "A", "bi-shield-check", "System Administrator", "Admin");
            }

            // 2. Managing Director (MD)
            if (string.Equals(role, "MD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "MD", StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains("Managing Director", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("Managing Director", "MD", "bi-person-badge-fill", "Managing Director (Central Office)", !string.IsNullOrEmpty(fullName) ? fullName : "Managing Director");
            }

            // 3. Deputy Chief Law Officer (Dy CLO / Dy_CLO)
            if (string.Equals(role, "Dy CLO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "DyCLO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Dy_CLO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "Dy_CLO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "DyCLO", StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains("Deputy Chief Law Officer", StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains("Dy CLO", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("Deputy Chief Law Officer", "Dy", "bi-briefcase", "Deputy Chief Law Officer (Central Office)", !string.IsNullOrEmpty(fullName) ? fullName : "Deputy Chief Law Officer");
            }

            // 4. Chief Law Officer (CLO)
            if (string.Equals(role, "CLO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "CLO", StringComparison.OrdinalIgnoreCase) ||
                (fullName.Contains("Chief Law Officer", StringComparison.OrdinalIgnoreCase) && !fullName.Contains("Deputy", StringComparison.OrdinalIgnoreCase)))
            {
                return new UserProfileBadgeInfo("Chief Law Officer", "CLO", "bi-briefcase-fill", "Chief Law Officer (Central Office)", !string.IsNullOrEmpty(fullName) ? fullName : "Chief Law Officer");
            }

            // 5. Law Officer (LO)
            if (string.Equals(role, "LO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Law Officer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "LAW_OFFICER_CO", StringComparison.OrdinalIgnoreCase) ||
                fullName.Equals("Law Officer", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("Law Officer", "LO", "bi-person-check", "Law Officer (Central Office)", !string.IsNullOrEmpty(fullName) ? fullName : "Law Officer");
            }

            // 6. Division Controller (DC)
            if (string.Equals(role, "Division Controller", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "DC", StringComparison.OrdinalIgnoreCase) ||
                username.StartsWith("DC-", StringComparison.OrdinalIgnoreCase) ||
                username.StartsWith("DC_", StringComparison.OrdinalIgnoreCase))
            {
                var cleanDiv = !string.IsNullOrEmpty(divName) ? divName : "Division";
                return new UserProfileBadgeInfo($"DC ({cleanDiv})", "DC", "bi-building-gear", $"Division Controller - {cleanDiv}", !string.IsNullOrEmpty(fullName) ? fullName : $"DC ({cleanDiv})");
            }

            // 7. General Central Office User
            if (divId == "5" || divId == "0" || 
                string.Equals(divName, "Central Office", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(username, "LAW_COH", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("Central Office", "CO", "bi-building", "Central Office", !string.IsNullOrEmpty(fullName) ? fullName : "Central Office User");
            }

            // 8. Central Office Lower Courts
            if (divId == "13" || string.Equals(divName, "Central Office(Lower Courts)", StringComparison.OrdinalIgnoreCase))
            {
                return new UserProfileBadgeInfo("CO Lower Courts", "CO", "bi-building", "Central Office (Lower Courts)", !string.IsNullOrEmpty(fullName) ? fullName : "Central Office (Lower Courts)");
            }

            // 9. Standard Division User
            var dLabel = !string.IsNullOrEmpty(divName) ? divName : "Division";
            var dInitial = !string.IsNullOrEmpty(dLabel) ? dLabel.Substring(0, 1).ToUpper() : "D";
            return new UserProfileBadgeInfo(dLabel, dInitial, "bi-building", $"{dLabel} Division", !string.IsNullOrEmpty(fullName) ? fullName : dLabel);
        }
    }
}
