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

        // Other Karnataka RTCs and non-NWKRTC division headquarters (Koppal, Ballari, Raichur, Kalaburagi, Bidar, Yadgir belong to NEKRTC/KKRTC)
        private static readonly Regex OtherCoOrganizationsRegex = new(
            @"(?i)(K\s*\.?\s*S\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C|K\s*\.?\s*K\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C|N\s*\.?\s*E\s*\.?\s*K\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C|N\s*\.?\s*E\s*\.?\s*K\s*\.?\s*S\s*\.?\s*R\s*\.?\s*T\s*\.?\s*C|B\s*\.?\s*M\s*\.?\s*T\s*\.?\s*C|KARNATAKA\s*(STATE\s*)?ROAD\s*TRANSPORT|KALYANA\s*KARNATAKA|NORTH\s*[- ]?EAST(ERN)?\s*KARNATAKA|BANGALORE\s*METROPOLITAN|BENGALURU\s*METROPOLITAN|NEKRTC|KKRTC|BMTC|\b(KOPPAL|BALLARI|BELLARY|RAICHUR|KALABURAGI|GULBARGA|BIDAR|YADGIR)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 4. Non-transport corporate/commercial/government entities commonly appearing with generic designations (e.g. Managing Director)
        private static readonly Regex NonTransportCorporateRegex = new(
            @"\b(SUGAR|SUGARS|NIRANI|BANK|PVT|PRIVATE\s+LTD|LTD|LIMITED|HOSPITAL|RAILWAY|RAILWAYS|METRO|HESCOM|BESCOM|GESCOM|MESCOM|CHESCOM|KPTCL|HOTEL|STEEL|CEMENT|CEMENTS|SLAO|SPECIAL\s+LAND|LAND\s+ACQUISITION|UKP|UPPER\s+KRISHNA|INSURANCE|ASSURANCE|UNITED\s+INDIA|NATIONAL\s+INSURANCE|NEW\s+INDIA|ORIENTAL\s+INSURANCE|ICICI\s+LOMBARD|BAJAJ\s+ALLIANZ|RELIANCE\s+GENERAL|SHRIRAM\s+GENERAL|CHOLAMANDALAM|TATA\s*AIG|IFFCO|HDFC\s*ERGO|SBI\s+GENERAL|ROYAL\s+SUNDARAM|FUTURE\s+GENERALI|STAR\s+HEALTH|MAGMA|UNIVERSAL\s+SOMPO|GO\s*DIGIT|LIBERTY|KGID|REGIONAL\s+OFFICER|PWD|PUBLIC\s+WORKS|POLICE|REVENUE\s+DEPT|TAHSILDAR|ZILLA\s+PANCHAYAT|TALUK\s+PANCHAYAT|GRAM\s+PANCHAYAT|MUNICIPAL|HDMC|CITY\s+CORP|EDUCATION|HEALTH\s+DEPT|FOREST\s+DEPT)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 5. Designation-specific patterns (strictly for NWKRTC, no KSRTC)
        private static readonly Regex ManagingDirectorRegex = new(
            @"\b(MANAGING\s*DIRECTOR|MANGING\s*DIRECTOR|MANAGINGDIRECTOR|THE\s+MANAGEMENT\s+OF\s+(NWKRTC|NKWRTC|NORTH\s*WEST|N\.W\.K\.R\.T\.C)|MANAGEMENTOF\s*NWKRTC|M\.?D\.?\b\s*(NWKRTC|NKWRTC|NORTH\s*WEST)?|CHIEF\s+EXECUTIVE|M\.?\s*PRIYANGA|PRIYANGA)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CpmRegex = new(
            @"\b(CHIEF\s+PERSONNEL\s+MANAGER|CHIEF\s+PERSONAL\s+MANAGER|C\.?P\.?M\.?\b|PERSONNEL\s+MANAGER)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DivisionalControllerRegex = new(
            @"\b(DIVISIONAL\s*CONTROLLER|DIVISIONALCONTROLLER|THE\s+DIVISIONAL\s*CONTROLLER|DIVISION\s+NWKRTC)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DepotManagerRegex = new(
            @"\b(DEPOT\s*MANAGER|DEPOTMANAGER|D\.?M\.?\b\s*(NWKRTC|NKWRTC|DEPOT)?|DEPOT\s+NWKRTC)",
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
            @"\b(HUBBALLI|HUBLI|BELAGAVI|BELGAUM|BAGALKOT|BAGALKOTE|GADAG|HAVERI|UTTARA\s+KANNADA|KARWAR|CHIKKODI|CHIKODI|SIRSI|GOKUL\s*ROAD|DHARWAD)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Checks if a party refers to another transport co-organization (KSRTC, KKRTC, BMTC, NEKRTC).
        /// Returns false if the party specifically specifies NWKRTC / North Western.
        /// </summary>
        public static bool IsOtherCoOrganization(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();

            // Strip High Court roster subject codes like (S, KSRTC ), (L, KSRTC ), (S, RES)
            s = Regex.Replace(s, @"\([A-Za-z0-9\s,]+\s*,\s*KSRTC\s*\)", " ", RegexOptions.IgnoreCase).Trim();

            // If it explicitly mentions NWKRTC / North West, it is NWKRTC, not another RTC
            if (AcronymRegex.IsMatch(s) || FullNameRegex.IsMatch(s) ||
                s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NWKSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WEST", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH-WEST", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NORTH WESTERN", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("KSRTC(NW)", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("KSRTC (NW)", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Explicit checks for other sibling RTCs even if concatenated without spaces by OCR
            if (s.Contains("NEKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("NEKSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("KKRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("BMTC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (s.Contains("KSRTC", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("K.S.R.T.C", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(s, @"\bKARNATAKA\s*(STATE)?\s*ROAD\s*TRANSPORT", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return OtherCoOrganizationsRegex.IsMatch(s);
        }

        /// <summary>
        /// Tests if a string refers to NWKRTC or any of its officer designations.
        /// Extracts the matched designation category.
        /// Strictly excludes non-NWKRTC entities (e.g. KSRTC, BMTC, KKRTC, Sugar Mills, Banks).
        /// </summary>
        public static bool IsNwkrtcEntity(string? text, out string designation, string? estCode = null, string? advocate = null)
        {
            designation = "General / Corporate";
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim();

            // Strip High Court subject classifications e.g. (S, KSRTC ), (L, KSRTC )
            s = Regex.Replace(s, @"\([A-Za-z0-9\s,]+\s*,\s*KSRTC\s*\)", " ", RegexOptions.IgnoreCase).Trim();

            // 1. Strictly exclude other sibling RTCs (NEKRTC, KKRTC, BMTC)
            if (IsOtherCoOrganization(s))
            {
                return false;
            }

            // 2. Exclude State Government / Police / Revenue / Municipal entities without NWKRTC reference
            if (Regex.IsMatch(s, @"\b(STATE\s+OF\s+KARNATAKA|GOVERNMENT\s+OF|DEPUTY\s+COMMISSIONER|ASSISTANT\s+COMMISSIONER|TAHSILDAR|POLICE|GRAM\s+PANCHAYAT|MUNICIPAL|CITY\s+CORPORATION|HDMC|UNIVERSITY|COLLEGE|SCHOOL)\b", RegexOptions.IgnoreCase) &&
                !s.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) &&
                !s.Contains("NKWRTC", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3. Exclude non-transport commercial / private / banking / insurance entities without NWKRTC reference
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

            // Officer designations tied to NWKRTC division hubs (Hubballi, Belagavi, Bagalkot, Gadag, Haveri, Karwar, Sirsi, Chikkodi, Dharwad)
            bool hasNwkrtcHub = DivisionHubsRegex.IsMatch(s);
            if (hasNwkrtcHub)
            {
                if (DivisionalControllerRegex.IsMatch(s)) { designation = "Divisional Controller"; return true; }
                if (DepotManagerRegex.IsMatch(s)) { designation = "Depot Manager"; return true; }
                if (ManagingDirectorRegex.IsMatch(s)) { designation = "Managing Director"; return true; }
                if (CpmRegex.IsMatch(s)) { designation = "CPM (Personnel)"; return true; }
                if (InsuranceFundRegex.IsMatch(s)) { designation = "Insurance Fund"; return true; }
                if (AccountsOfficerRegex.IsMatch(s)) { designation = "Chief Accounts Officer"; return true; }
                if (LawOfficerRegex.IsMatch(s)) { designation = "Law Officer"; return true; }
            }

            // In Dharwad Bench (KAHC02), NWKRTC is the sole regional road transport corporation.
            // Explicit designations like "THE MANAGING DIRECTOR", "THE DIVISIONAL CONTROLLER", or "THE DEPOT MANAGER" 
            // without another corporation specified refer to NWKRTC.
            bool isDharwad = string.IsNullOrEmpty(estCode) || 
                             estCode.Equals("KAHC02", StringComparison.OrdinalIgnoreCase) ||
                             estCode.IndexOf("Dharwad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             estCode.StartsWith("KAHC02", StringComparison.OrdinalIgnoreCase);
            if (isDharwad)
            {
                if (Regex.IsMatch(s, @"\b(DIVISIONAL\s*CONTROLLER|DIVISIONALCONTROLLER|THE\s+DIVISIONAL\s*CONTROLLER)\b", RegexOptions.IgnoreCase) &&
                    !Regex.IsMatch(s, @"\b(RAILWAY|BESCOM|HESCOM|NEKRTC|KKRTC|BMTC)\b", RegexOptions.IgnoreCase))
                {
                    designation = "Divisional Controller";
                    return true;
                }
                if (Regex.IsMatch(s, @"\b(DEPOT\s*MANAGER|DEPOTMANAGER|THE\s+DEPOT\s*MANAGER)\b", RegexOptions.IgnoreCase) &&
                    !Regex.IsMatch(s, @"\b(RAILWAY|BESCOM|HESCOM|NEKRTC|KKRTC|BMTC)\b", RegexOptions.IgnoreCase))
                {
                    designation = "Depot Manager";
                    return true;
                }
                if (Regex.IsMatch(s, @"\b(THE\s+MANAGEMENT\s+OF\s+NWKRTC|THE\s+MANAGING\s+OF\s+NWKRTC|MANAGEMENTOF\s*NWKRTC)\b", RegexOptions.IgnoreCase))
                {
                    designation = "Managing Director";
                    return true;
                }
                if (Regex.IsMatch(s, @"\b(MANAGING\s*DIRECTOR|MANAGINGDIRECTOR|THE\s+MANAGING\s*DIRECTOR)\b", RegexOptions.IgnoreCase) &&
                    !NonTransportCorporateRegex.IsMatch(s) &&
                    !Regex.IsMatch(s, @"\b(RAILWAY|BESCOM|HESCOM|GESCOM|CHESCOM|MESCOM|KPTCL|NEKRTC|KKRTC|BMTC|POLICE|PWD|INSURANCE)\b", RegexOptions.IgnoreCase))
                {
                    bool hasNwContext = hasNwkrtcKeyword || hasNwkrtcHub ||
                        Regex.IsMatch(s, @"\b(TRANSPORT|ROAD|NWKRTC|KSRTC|INTERNAL\s+INSURANCE)\b", RegexOptions.IgnoreCase);
                    if (hasNwContext)
                    {
                        designation = "Managing Director";
                        return true;
                    }
                }
            }

            // Empanelled NWKRTC Standing Counsel representation check
            if (!string.IsNullOrWhiteSpace(advocate))
            {
                bool isNwkrtcStandingCounsel = Regex.IsMatch(advocate, @"\b(MADANMOHAN|KHANNUR|HOSMANI|HOSAMANE|KINI|KALASOOR|HEGDE|BUDIHAL|BADAWADAGI|BHUTI|MATTI|KALADAGI|KARIGANNAVAR|DABALI|GUNDAPPA|SANJEEV|SAHUKAR)\b", RegexOptions.IgnoreCase);
                bool hasTransportAuthority = Regex.IsMatch(s, @"\b(DIRECTOR|CONTROLLER|MANAGEMENT|CORPORATION|DEPOT)\b", RegexOptions.IgnoreCase);
                if (isNwkrtcStandingCounsel && hasTransportAuthority && !NonTransportCorporateRegex.IsMatch(s))
                {
                    bool hasNwContext = hasNwkrtcKeyword || hasNwkrtcHub ||
                        Regex.IsMatch(s, @"\b(TRANSPORT|ROAD|NWKRTC|KSRTC|INTERNAL\s+INSURANCE)\b", RegexOptions.IgnoreCase);
                    if (hasNwContext)
                    {
                        if (ManagingDirectorRegex.IsMatch(s)) designation = "Managing Director";
                        else if (DivisionalControllerRegex.IsMatch(s)) designation = "Divisional Controller";
                        else designation = "NWKRTC Central";
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsNwkrtcEntity(string? text, out string designation, string? estCode)
        {
            return IsNwkrtcEntity(text, out designation, estCode, null);
        }

        /// <summary>
        /// Analyzes Petitioner, Respondent, and Appeal Type to determine whether the case is
        /// "With NWKRTC" (Petitioner/Appellant) or "Against NWKRTC" (Respondent),
        /// or an "Other RTC" / "Non-Corporation" case.
        /// </summary>
        public static NwkrtcPartyRoleResult DetectRole(string? petitioner, string? respondent, string? appealType = null, string? estCode = null, string? advocate = null)
        {
            var result = new NwkrtcPartyRoleResult();

            bool isOtherPet = IsOtherCoOrganization(petitioner);
            bool isOtherRes = IsOtherCoOrganization(respondent);

            bool isPet = IsNwkrtcEntity(petitioner, out string petDesignation, estCode, advocate);
            bool isRes = IsNwkrtcEntity(respondent, out string resDesignation, estCode, advocate);

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
