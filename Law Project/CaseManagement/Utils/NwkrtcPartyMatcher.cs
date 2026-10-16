using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MVCCaseManagement.Utils
{
    public class NwkrtcPartyRoleResult
    {
        public string Role { get; set; } = "With NWKRTC"; // "With NWKRTC" or "Against NWKRTC" or "Non-Corporation"
        public string MatchedDesignation { get; set; } = "General / Corporate"; // e.g. "Managing Director", "CPM", "Divisional Controller", "Insurance Fund"
        public string MatchedIn { get; set; } = "None"; // "Petitioner", "Respondent", "AppealType"
        public bool IsCorporationParty => Role == "With NWKRTC" || Role == "Against NWKRTC";
    }

    /// <summary>
    /// Comprehensive regex, tokenization, alias, and typo matching engine for NWKRTC
    /// (North Western Karnataka Road Transport Corporation) in court records.
    /// Handles designations: Managing Director (MD), Chief Personnel Manager (CPM),
    /// Divisional Controller (DC), Depot Manager (DM), Chief Accounts Officer (CAO),
    /// Insurance Fund / Self-Insurance, full statutory names, and clerk data-entry typos (e.g. NKWRTC).
    /// </summary>
    public static class NwkrtcPartyMatcher
    {
        // 1. Primary entity acronyms and known data-entry typos in court systems
        private static readonly Regex AcronymRegex = new(
            @"\b(NWKRTC|NKWRTC|NWKTC|NWKRTS|NWSRTC|NWKSRTC|N\s*W\s*K\s*R\s*T\s*C|N\.W\.K\.R\.T\.C|N\.W\.K\.S\.R\.T\.C|KSRTC\s*\(NW\))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 2. Full corporate names
        private static readonly Regex FullNameRegex = new(
            @"NORTH\s*[- ]?WEST(ERN)?\s+KARNATAKA(\s+STATE)?\s+ROAD\s+TRANSPORT\s+CORP",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 3. Designation-specific patterns
        private static readonly Regex ManagingDirectorRegex = new(
            @"\b(MANAGING\s+DIRECTOR|THE\s+MANAGEMENT\s+OF|MANAGEMENT\s+OF|M\.?D\.?\b(\s*NWKRTC|\s*NKWRTC|\s*KSRTC)?|CHIEF\s+EXECUTIVE)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CpmRegex = new(
            @"\b(CHIEF\s+PERSONNEL\s+MANAGER|CHIEF\s+PERSONAL\s+MANAGER|C\.?P\.?M\.?\b|PERSONNEL\s+MANAGER)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DivisionalControllerRegex = new(
            @"\b(DIVISIONAL\s+CONTROLLER|D\.?C\.?\b\s*(NWKRTC|NKWRTC|DIVISION)?|DIVISION\s+NWKRTC)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DepotManagerRegex = new(
            @"\b(DEPOT\s+MANAGER|D\.?M\.?\b\s*(NWKRTC|NKWRTC|DEPOT)?|DEPOT\s+NWKRTC)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex InsuranceFundRegex = new(
            @"\b(SELF\s*[- ]?INSURANCE|INSURANCE\s+FUND|INTERNAL\s+INSURANCE)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AccountsOfficerRegex = new(
            @"\b(CHIEF\s+ACCOUNTS\s+OFFICER|C\.?A\.?O\.?\b|ACCOUNTS\s+OFFICER|FINANCE\s+OFFICER)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LawOfficerRegex = new(
            @"\b(CHIEF\s+LAW\s+OFFICER|LAW\s+OFFICER|LEGAL\s+ADVISOR)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Standard division hubs in NWKRTC jurisdiction (helpful for contextual matching)
        private static readonly Regex DivisionHubsRegex = new(
            @"\b(HUBBALLI|HUBLI|BELAGAVI|BELGAUM|BAGALKOT|BAGALKOTE|GADAG|HAVERI|UTTARA\s+KANNADA|KARWAR|CHIKKODI|CHIKODI|SIRSI)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Tests if a string refers to NWKRTC or any of its officer designations.
        /// Extracts the matched designation category.
        /// </summary>
        public static bool IsNwkrtcEntity(string? text, out string designation)
        {
            designation = "General / Corporate";
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim();

            bool hasAcronym = AcronymRegex.IsMatch(s);
            bool hasFullName = FullNameRegex.IsMatch(s);
            bool hasHub = DivisionHubsRegex.IsMatch(s);

            // Check specific designations (court clerks frequently omit the word 'NWKRTC' after designations)
            if (ManagingDirectorRegex.IsMatch(s))
            {
                designation = "Managing Director";
                return true;
            }

            if (CpmRegex.IsMatch(s))
            {
                designation = "CPM (Personnel)";
                return true;
            }

            if (DivisionalControllerRegex.IsMatch(s))
            {
                designation = "Divisional Controller";
                return true;
            }

            if (DepotManagerRegex.IsMatch(s))
            {
                designation = "Depot Manager";
                return true;
            }

            if (InsuranceFundRegex.IsMatch(s))
            {
                designation = "Insurance Fund";
                return true;
            }

            if (AccountsOfficerRegex.IsMatch(s))
            {
                designation = "Chief Accounts Officer";
                return true;
            }

            if (LawOfficerRegex.IsMatch(s))
            {
                designation = "Law Officer";
                return true;
            }

            // Direct corporate acronym or full legal name
            if (hasAcronym || hasFullName)
            {
                designation = "NWKRTC Central";
                return true;
            }

            // Fallback for general corporation keywords
            if (s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NWKSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WESTERN KARNATAKA", StringComparison.OrdinalIgnoreCase))
            {
                designation = "NWKRTC";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Analyzes Petitioner, Respondent, and Appeal Type to determine whether the case is
        /// "With NWKRTC" (Petitioner/Appellant) or "Against NWKRTC" (Respondent).
        /// </summary>
        public static NwkrtcPartyRoleResult DetectRole(string? petitioner, string? respondent, string? appealType = null)
        {
            var result = new NwkrtcPartyRoleResult();

            bool isPet = IsNwkrtcEntity(petitioner, out string petDesignation);
            bool isRes = IsNwkrtcEntity(respondent, out string resDesignation);

            if (isPet && !isRes)
            {
                result.Role = "With NWKRTC";
                result.MatchedDesignation = petDesignation;
                result.MatchedIn = "Petitioner";
                return result;
            }

            if (!isPet && isRes)
            {
                result.Role = "Against NWKRTC";
                result.MatchedDesignation = resDesignation;
                result.MatchedIn = "Respondent";
                return result;
            }

            if (isPet && isRes)
            {
                // In intra-corporation or ambiguous dual listings, appealType breaks ties
                if (!string.IsNullOrEmpty(appealType) && appealType.Contains("Corporation", StringComparison.OrdinalIgnoreCase))
                {
                    result.Role = "With NWKRTC";
                    result.MatchedDesignation = petDesignation;
                    result.MatchedIn = "Petitioner";
                }
                else
                {
                    result.Role = "Against NWKRTC";
                    result.MatchedDesignation = resDesignation;
                    result.MatchedIn = "Respondent";
                }
                return result;
            }

            // If text matching did not catch it, infer from AppealType
            if (!string.IsNullOrEmpty(appealType))
            {
                if (appealType.Contains("Corporation", StringComparison.OrdinalIgnoreCase))
                {
                    result.Role = "With NWKRTC";
                    result.MatchedDesignation = "Corporation Appeal";
                    result.MatchedIn = "AppealType";
                    return result;
                }
                else if (appealType.Contains("Claimant", StringComparison.OrdinalIgnoreCase))
                {
                    result.Role = "Against NWKRTC";
                    result.MatchedDesignation = "Claimant Appeal";
                    result.MatchedIn = "AppealType";
                    return result;
                }
            }

            result.Role = "Non-Corporation";
            result.MatchedDesignation = "None";
            result.MatchedIn = "None";
            return result;
        }

        /// <summary>
        /// Returns the standard list of search aliases used for querying High Court / District Court
        /// NAPIX gateways to guarantee zero missed cases.
        /// </summary>
        public static List<string> GetDefaultSearchAliases()
        {
            return new List<string>
            {
                "NWKRTC",
                "MANAGING DIRECTOR",
                "DIVISIONAL CONTROLLER",
                "CPM",
                "CHIEF PERSONNEL MANAGER",
                "INSURANCE FUND",
                "DEPOT MANAGER",
                "NKWRTC",               // Crucial: Common court clerk typographical error
                "NORTH WESTERN"
            };
        }
    }
}
