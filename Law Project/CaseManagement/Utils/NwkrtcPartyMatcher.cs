using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MVCCaseManagement.Utils
{
    public class NwkrtcPartyRoleResult
    {
        public string Role { get; set; } = "With NWKRTC"; // "With NWKRTC" or "Against NWKRTC" or "Non-Corporation" or "Other RTC"
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
    /// Strictly filters out other sibling RTCs (KSRTC, KKRTC, NEKRTC, BMTC) and non-transport corporate entities.
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

        // 3. Other Karnataka RTC co-organizations that must be strictly excluded from NWKRTC cause lists
        private static readonly Regex OtherCoOrganizationsRegex = new(
            @"\b(K\s*\.?\s*S\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C\.?|K\s*\.?\s*K\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C\.?|N\s*\.?\s*E\s*\.?\s*K\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C\.?|N\s*\.?\s*E\s*\.?\s*K\s*\.?\s*S\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C\.?|K\s*\.?\s*E\s*\.?\s*K\s*\.?\s*S\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C\.?|B\s*\.?\s*M\s*\.?\s*T\s*\.?\s*C\.?|KARNATAKA\s+(STATE\s+)?ROAD\s+TRANSPORT|KALYANA\s+KARNATAKA|NORTH\s*[- ]?EAST(ERN)?\s+KARNATAKA|BANGALORE\s+METROPOLITAN|BENGALURU\s+METROPOLITAN)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 4. Non-transport corporate/commercial/government entities commonly appearing with generic designations (e.g. Managing Director)
        private static readonly Regex NonTransportCorporateRegex = new(
            @"\b(SUGAR|SUGARS|NIRANI|BANK|PVT|PRIVATE\s+LTD|LTD|LIMITED|HOSPITAL|RAILWAY|RAILWAYS|METRO|HESCOM|BESCOM|GESCOM|MESCOM|KPTCL|HOTEL|STEEL|CEMENT|CEMENTS|SLAO|SPECIAL\s+LAND|INSURANCE\s+CO)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 5. Designation-specific patterns (strictly for NWKRTC, no KSRTC)
        private static readonly Regex ManagingDirectorRegex = new(
            @"\b(MANAGING\s+DIRECTOR|MANGING\s+DIRECTOR|THE\s+MANAGEMENT\s+OF|MANAGEMENT\s+OF|M\.?D\.?\b(\s*NWKRTC|\s*NKWRTC)?|CHIEF\s+EXECUTIVE|THE\s+MANAGING|M\.?\s*PRIYANGA|PRIYANGA)\b",
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
        /// Checks if a party refers to another transport co-organization (KSRTC, KKRTC, BMTC, NEKRTC).
        /// Returns false if the party specifically specifies NWKRTC / North Western.
        /// </summary>
        public static bool IsOtherCoOrganization(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();

            // If it explicitly mentions NWKRTC / North West, it is NWKRTC, not another RTC
            if (AcronymRegex.IsMatch(s) || FullNameRegex.IsMatch(s) ||
                s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NWKSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WEST", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("KSRTC(NW)", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("KSRTC (NW)", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return OtherCoOrganizationsRegex.IsMatch(s);
        }

        /// <summary>
        /// Tests if a string refers to NWKRTC or any of its officer designations.
        /// Extracts the matched designation category.
        /// Strictly excludes non-NWKRTC entities (e.g. KSRTC, BMTC, KKRTC, Sugar Mills, Banks).
        /// </summary>
        public static bool IsNwkrtcEntity(string? text, out string designation, string? estCode = null)
        {
            designation = "General / Corporate";
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim();

            // 1. Strictly exclude other sibling RTCs (KSRTC, BMTC, KKRTC, NEKRTC)
            if (IsOtherCoOrganization(s))
            {
                return false;
            }

            // 2. Exclude non-transport commercial / private / banking entities without NWKRTC reference
            if (NonTransportCorporateRegex.IsMatch(s) &&
                !s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) &&
                !s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool hasAcronym = AcronymRegex.IsMatch(s);
            bool hasFullName = FullNameRegex.IsMatch(s);
            bool hasNwkrtcKeyword = hasAcronym || hasFullName ||
                s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NWKSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WEST", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH-WEST", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WESTERN", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("GOKUL ROAD", StringComparison.OrdinalIgnoreCase);

            if (hasNwkrtcKeyword)
            {
                if (ManagingDirectorRegex.IsMatch(s)) designation = "Managing Director";
                else if (CpmRegex.IsMatch(s)) designation = "CPM (Personnel)";
                else if (DivisionalControllerRegex.IsMatch(s)) designation = "Divisional Controller";
                else if (DepotManagerRegex.IsMatch(s)) designation = "Depot Manager";
                else if (InsuranceFundRegex.IsMatch(s)) designation = "Insurance Fund";
                else if (AccountsOfficerRegex.IsMatch(s)) designation = "Chief Accounts Officer";
                else if (LawOfficerRegex.IsMatch(s)) designation = "Law Officer";
                else designation = "NWKRTC Central";

                return true;
            }

            // In Dharwad Bench (KAHC02) or when mentioning NWKRTC division hubs (Hubballi, Belagavi, etc.):
            // Generic officer designations (MD, DC, DM) are NWKRTC authorities.
            // In Bengaluru (KAHC01) or Kalaburagi (KAHC03), generic MD/DC belong to KSRTC/KKRTC unless an NWKRTC hub is named.
            bool isDharwadOrHub = string.IsNullOrEmpty(estCode) || estCode.Equals("KAHC02", StringComparison.OrdinalIgnoreCase) || DivisionHubsRegex.IsMatch(s);

            if (isDharwadOrHub)
            {
                if (ManagingDirectorRegex.IsMatch(s)) { designation = "Managing Director"; return true; }
                if (CpmRegex.IsMatch(s)) { designation = "CPM (Personnel)"; return true; }
                if (DivisionalControllerRegex.IsMatch(s)) { designation = "Divisional Controller"; return true; }
                if (DepotManagerRegex.IsMatch(s)) { designation = "Depot Manager"; return true; }
                if (InsuranceFundRegex.IsMatch(s)) { designation = "Insurance Fund"; return true; }
                if (AccountsOfficerRegex.IsMatch(s)) { designation = "Chief Accounts Officer"; return true; }
                if (LawOfficerRegex.IsMatch(s)) { designation = "Law Officer"; return true; }
            }

            return false;
        }

        /// <summary>
        /// Analyzes Petitioner, Respondent, and Appeal Type to determine whether the case is
        /// "With NWKRTC" (Petitioner/Appellant) or "Against NWKRTC" (Respondent),
        /// or an "Other RTC" / "Non-Corporation" case.
        /// </summary>
        public static NwkrtcPartyRoleResult DetectRole(string? petitioner, string? respondent, string? appealType = null, string? estCode = null)
        {
            var result = new NwkrtcPartyRoleResult();

            bool isOtherPet = IsOtherCoOrganization(petitioner);
            bool isOtherRes = IsOtherCoOrganization(respondent);

            bool isPet = IsNwkrtcEntity(petitioner, out string petDesignation, estCode);
            bool isRes = IsNwkrtcEntity(respondent, out string resDesignation, estCode);

            // If either party is explicitly an Other RTC and neither party is NWKRTC, mark clearly as Other RTC
            if ((isOtherPet || isOtherRes) && !isPet && !isRes)
            {
                result.Role = "Other RTC";
                result.MatchedDesignation = "Other Road Transport Corp";
                result.MatchedIn = isOtherPet ? "Petitioner" : "Respondent";
                return result;
            }

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
        /// NAPIX gateways strictly targeted to NWKRTC to guarantee zero missed NWKRTC cases
        /// and avoid querying thousands of non-NWKRTC matters.
        /// </summary>
        public static List<string> GetDefaultSearchAliases(string? estCode = null)
        {
            if (string.IsNullOrEmpty(estCode) || estCode.Equals("KAHC02", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "NWKRTC",
                    "DIVISIONAL CONTROLLER",
                    "MANAGING DIRECTOR",
                    "NORTH WEST",
                    "DEPOT MANAGER",
                    "NKWRTC"
                };
            }

            return new List<string>
            {
                "NWKRTC",
                "NORTH WEST",
                "NKWRTC",
                "NWKSRTC"
            };
        }
    }
}
