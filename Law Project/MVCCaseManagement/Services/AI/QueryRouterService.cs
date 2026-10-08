using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class QueryRouterService : IQueryRouterService
    {
        private readonly ILogger<QueryRouterService> _logger;

        public QueryRouterService(ILogger<QueryRouterService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public QueryRouteResult RouteQuery(string question, string? contextCaseType = null, int? contextCaseId = null)
        {
            var result = new QueryRouteResult();
            if (string.IsNullOrWhiteSpace(question))
            {
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = "Empty prompt routed to internal database by default.";
                return result;
            }

            string clean = question.Trim();
            result.CleanedKeywords = clean;

            // 1. Extract Case Numbers, Types & CNR
            ExtractCaseIdentifiers(clean, result);

            // Inherit active case context if not explicitly overridden
            if (result.ExtractedCaseId == null && contextCaseId.HasValue && contextCaseId.Value > 0)
            {
                result.ExtractedCaseId = contextCaseId.Value;
                result.ExtractedCaseType = !string.IsNullOrWhiteSpace(result.ExtractedCaseType) 
                    ? result.ExtractedCaseType 
                    : (contextCaseType ?? "MVC");
            }

            // 2. Extract Specialized Entities
            var plan = result.Plan;
            plan.RawQuery = clean;
            plan.CaseType = result.ExtractedCaseType;
            plan.CaseNumber = result.ExtractedCaseNumber;
            plan.CaseYear = result.ExtractedYear;
            plan.CNRNumber = result.ExtractedCNR;

            ExtractVehicleNumber(clean, result, plan);
            ExtractAdvocateName(clean, result, plan);
            ExtractDateRange(clean, plan);
            ExtractFinancialThreshold(clean, plan);
            ExtractDivision(clean, plan);

            // 3. Extract Statutory Provisions
            ExtractStatutoryProvisions(clean, result);
            plan.StatutoryProvisions = new List<string>(result.ExtractedProvisions);

            // 4. Detect Follow-Up Intent
            result.IsFollowUp = DetectFollowUp(clean, contextCaseId.HasValue && contextCaseId.Value > 0);

            // 5. Classify Intent & Construct Execution Plan
            string lower = clean.ToLowerInvariant();

            bool mentionsSupremeOrHighCourt = lower.Contains("supreme court") || lower.Contains("high court") ||
                                              lower.Contains("apex court") || lower.Contains("ruling") ||
                                              lower.Contains("landmark") || lower.Contains("ratio") ||
                                              lower.Contains("jurisprudence") || lower.Contains("citation") ||
                                              lower.Contains("air 20") || lower.Contains("scc");

            bool mentionsSimilarCases = lower.Contains("similar") || lower.Contains("comparable") ||
                                        lower.Contains("have we handled") || lower.Contains("precedent in nwkrtc") ||
                                        lower.Contains("similar cases") || lower.Contains("other cases like this") ||
                                        lower.Contains("other cases");

            bool mentionsHearingOrECourts = lower.Contains("hearing") || lower.Contains("ecourt") ||
                                            lower.Contains("e-court") || lower.Contains("cnr") ||
                                            lower.Contains("coram") || lower.Contains("court hall") ||
                                            lower.Contains("order date") || lower.Contains("next date") ||
                                            lower.Contains("daily order") || lower.Contains("napix") ||
                                            lower.Contains("latest order");

            bool mentionsDocuments = lower.Contains("document") || lower.Contains("pdf") ||
                                     lower.Contains("page ") || lower.Contains("page no") ||
                                     lower.Contains("chargesheet") || lower.Contains("spot sketch") ||
                                     lower.Contains("award copy") || lower.Contains("fir copy") ||
                                     lower.Contains("exhibit") || lower.Contains("uploaded");

            bool mentionsExecution = lower.Contains("execution petition") || lower.Contains("pending ep") ||
                                     lower.Contains("labour ep") || lower.Contains("mvc ep") ||
                                     lower.Contains("ep details") || lower.Contains("attachment warrant") ||
                                     lower.Contains("bus attachment") || lower.Contains("conditional order") ||
                                     Regex.IsMatch(lower, @"\b(ep|eps)\b");

            bool mentionsInternalDbOnly = lower.Contains("claimant") || lower.Contains("petitioner") ||
                                          lower.Contains("respondent") || lower.Contains("vehicle no") ||
                                          lower.Contains("bus no") || lower.Contains("driver name") ||
                                          lower.Contains("accident date") || lower.Contains("claim amount") ||
                                          lower.Contains("noting") || lower.Contains("opinion of") ||
                                          lower.Contains("mact name") || lower.Contains("mact id") ||
                                          lower.Contains("lo opinion") || lower.Contains("clo opinion") ||
                                          lower.Contains("who is the claimant");

            bool mentionsFullAnalysis = lower.Contains("complete analysis") || lower.Contains("full analysis") ||
                                        lower.Contains("complete case analysis") || lower.Contains("comprehensive briefing") ||
                                        lower.Contains("21-point") || lower.Contains("prepare me for the next hearing") ||
                                        lower.Contains("strengths and weaknesses") || lower.Contains("contradictions in our case") ||
                                        lower.Contains("complete legal analysis");

            bool mentionsDivisionalStats = (lower.Contains("how many") || lower.Contains("count") || lower.Contains("total cases") ||
                                            lower.Contains("pending cases") || lower.Contains("disposed cases") ||
                                            lower.Contains("cases in") || lower.Contains("cases of") || lower.Contains("cases under") ||
                                            lower.Contains("statistics") || lower.Contains("case register")) &&
                                           plan.DivisionId.HasValue;

            // Route Evaluation Matrix
            if (!string.IsNullOrWhiteSpace(plan.VehicleNumber) && string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                if (mentionsSupremeOrHighCourt || mentionsSimilarCases)
                {
                    plan.Intent = LegalQueryIntent.CompoundFilter;
                    plan.IsDeterministicDatabaseQuery = false;
                    plan.RequiresLegalPrecedents = true;
                    plan.RequiresWebSearch = true;
                    result.PrimaryCategory = QuerySourceCategory.MultiSource;
                    result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                    result.RequiredSources.Add(QuerySourceCategory.Judgments);
                    result.RequiredSources.Add(QuerySourceCategory.LegalWeb);
                    result.Reasoning = $"Vehicle litigation combined with judicial precedent search for {plan.VehicleNumber}.";
                }
                else
                {
                    // A. Vehicle Litigation History Intent
                    plan.Intent = LegalQueryIntent.VehicleHistory;
                    plan.IsDeterministicDatabaseQuery = true;
                    result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                    result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                    result.Reasoning = $"Vehicle litigation and accident claim history query for {plan.VehicleNumber} routed to database.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(plan.AdvocateName) && string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                // B. Advocate Portfolio & Caseload Intent
                plan.Intent = LegalQueryIntent.AdvocatePortfolio;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = $"Advocate portfolio and active caseload query for {plan.AdvocateName} routed to database.";
            }
            else if (plan.DateRangeStart.HasValue && !mentionsSupremeOrHighCourt && string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                // C. Judicial Hearing Calendar Intent
                plan.Intent = LegalQueryIntent.HearingCalendar;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.RequiredSources.Add(QuerySourceCategory.ECourts);
                result.Reasoning = $"Judicial hearing calendar query for window '{plan.DateRangeLabel}' routed to database and e-Courts.";
            }
            else if (plan.AmountThreshold.HasValue && string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                // D. Financial Exposure & High Claim Risk Intent
                plan.Intent = LegalQueryIntent.FinancialRisk;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = $"Financial exposure and high-risk claims query (Threshold: ₹{plan.AmountThreshold:N0}) routed to database.";
            }
            else if (mentionsExecution && string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                // E. Execution Petition & Attachment Risk Intent
                plan.Intent = LegalQueryIntent.ExecutionRisk;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = "Execution Petitions and attachment risk query routed to database.";
            }
            else if (plan.DivisionId.HasValue && (plan.AmountThreshold.HasValue || plan.DateRangeStart.HasValue))
            {
                // F. Multi-Filter Compound Query
                plan.Intent = LegalQueryIntent.CompoundFilter;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = $"Multi-filter compound query for division {plan.DivisionCode} routed to parameterized SQL engine.";
            }
            else if (mentionsDivisionalStats)
            {
                // G. Divisional Statistics & Case Registers
                plan.Intent = LegalQueryIntent.DivisionalStatistics;
                plan.IsDeterministicDatabaseQuery = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.ExtractedCaseType = lower.Contains("labour") ? "LABOUR" : (lower.Contains("gratuity") ? "GRATUITY" : "MVC");
                result.Reasoning = "Divisional case count and litigation statistics query routed strictly to internal database.";
            }
            else if (mentionsFullAnalysis)
            {
                // H. Comprehensive Multi-Source Case Briefing
                plan.Intent = LegalQueryIntent.CaseDeepDive;
                plan.RequiresDocuments = true;
                plan.RequiresECourts = true;
                plan.RequiresLegalPrecedents = true;
                plan.RequiresWebSearch = true;
                result.PrimaryCategory = QuerySourceCategory.MultiSource;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.RequiredSources.Add(QuerySourceCategory.InternalDocuments);
                result.RequiredSources.Add(QuerySourceCategory.ECourts);
                result.RequiredSources.Add(QuerySourceCategory.Judgments);
                result.RequiredSources.Add(QuerySourceCategory.LegalWeb);
                result.RequiredSources.Add(QuerySourceCategory.SimilarCases);
                result.Reasoning = "Multi-source comprehensive analysis requested across all internal, judicial and web repositories.";
            }
            else if (mentionsSimilarCases)
            {
                // I. Similar Precedents Comparison
                plan.Intent = LegalQueryIntent.LegalPrecedent;
                result.PrimaryCategory = QuerySourceCategory.SimilarCases;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.RequiredSources.Add(QuerySourceCategory.Judgments);
                result.RequiredSources.Add(QuerySourceCategory.ECourts);
                result.RequiredSources.Add(QuerySourceCategory.LegalWeb);
                result.Reasoning = "Similar case comparison requires internal repository records, judgment rulings, and e-Courts history.";
            }
            else if (mentionsSupremeOrHighCourt && !mentionsHearingOrECourts && !mentionsInternalDbOnly)
            {
                // J. Higher Court Legal Precedent
                plan.Intent = LegalQueryIntent.LegalPrecedent;
                plan.RequiresLegalPrecedents = true;
                plan.RequiresWebSearch = true;
                result.PrimaryCategory = QuerySourceCategory.LegalWeb;
                result.RequiredSources.Add(QuerySourceCategory.LegalWeb);
                result.RequiredSources.Add(QuerySourceCategory.Judgments);
                result.Reasoning = "Higher court jurisprudence query routed to authoritative legal web and judgment repositories.";
            }
            else if (mentionsDocuments)
            {
                // K. Uploaded Document Exhibit RAG
                plan.Intent = LegalQueryIntent.DocumentRAG;
                plan.RequiresDocuments = true;
                result.PrimaryCategory = QuerySourceCategory.InternalDocuments;
                result.RequiredSources.Add(QuerySourceCategory.InternalDocuments);
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = "Document text extraction and page search required for uploaded case exhibits/orders.";
            }
            else if (mentionsHearingOrECourts)
            {
                plan.Intent = LegalQueryIntent.CaseLookup;
                plan.RequiresECourts = true;
                result.PrimaryCategory = QuerySourceCategory.ECourts;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.RequiredSources.Add(QuerySourceCategory.ECourts);
                result.Reasoning = "Hearing schedule and judicial stage routed to internal case register and e-Courts / NAPIX gateway.";
            }
            else if (mentionsInternalDbOnly || !string.IsNullOrWhiteSpace(result.ExtractedCaseNumber))
            {
                plan.Intent = LegalQueryIntent.CaseLookup;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = "Internal case metadata, parties, vehicle, or internal opinions routed to internal database.";
            }
            else if (result.ExtractedProvisions.Count > 0)
            {
                plan.Intent = LegalQueryIntent.LegalPrecedent;
                plan.RequiresLegalPrecedents = true;
                result.PrimaryCategory = QuerySourceCategory.LegalWeb;
                result.RequiredSources.Add(QuerySourceCategory.LegalWeb);
                result.RequiredSources.Add(QuerySourceCategory.Judgments);
                result.Reasoning = "Statutory provision legal interpretation routed to authoritative legal web sources and judgment repository.";
            }
            else
            {
                plan.Intent = LegalQueryIntent.GeneralInquiry;
                result.PrimaryCategory = QuerySourceCategory.InternalDatabase;
                result.RequiredSources.Add(QuerySourceCategory.InternalDatabase);
                result.Reasoning = "General query routed to internal database without external web search.";
            }

            // Deduplicate required sources
            result.RequiredSources = result.RequiredSources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            plan.PlanSummary = result.Reasoning;
            plan.PerSourceTimeoutSeconds = 15;

            // Multi-Stage Query Decomposition & Planning
            DecomposeQuery(clean, plan, result);

            _logger.LogInformation("Query routed to {Category} (Intent: {Intent}, SubQueries: {SubCount}). Required sources: {Sources}.",
                result.PrimaryCategory, plan.Intent, plan.SubQueries.Count, string.Join(", ", result.RequiredSources));

            return result;
        }

        private static void ExtractVehicleNumber(string input, QueryRouteResult result, LegalQueryPlan plan)
        {
            // Karnataka State RTC Registration Regex: KA-XX-X-XXXX or KA XX X XXXX or KAXX XXXXX
            var match = Regex.Match(input, @"\b(KA[- ]?\d{1,2}[- ]?[A-Z]{1,3}[- ]?\d{1,4})\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string raw = match.Groups[1].Value.ToUpperInvariant();
                string cleaned = Regex.Replace(raw, @"\s+", "-");
                result.ExtractedVehicleNo = cleaned;
                plan.VehicleNumber = cleaned;
            }
        }

        private static void ExtractAdvocateName(string input, QueryRouteResult result, LegalQueryPlan plan)
        {
            var match = Regex.Match(input, @"(?:advocate|adv\.?|counsel|lawyer)\s+([A-Za-z\.\s]{3,30}?)(?:\b|in|for|cases|handling|\?|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string name = match.Groups[1].Value.Trim(' ', '.', ',');
                if (name.Length >= 3 && !IsCommonStopword(name))
                {
                    result.ExtractedAdvocateName = name;
                    plan.AdvocateName = name;
                }
            }
            else
            {
                var assignedMatch = Regex.Match(input, @"assigned\s+to\s+(?:advocate\s+|adv\.?\s+)?([A-Za-z\.\s]{3,30}?)(?:\b|in|for|\?|$)", RegexOptions.IgnoreCase);
                if (assignedMatch.Success)
                {
                    string name = assignedMatch.Groups[1].Value.Trim(' ', '.', ',');
                    if (name.Length >= 3 && !IsCommonStopword(name))
                    {
                        result.ExtractedAdvocateName = name;
                        plan.AdvocateName = name;
                    }
                }
            }
        }

        private static void ExtractDateRange(string input, LegalQueryPlan plan)
        {
            string lower = input.ToLowerInvariant();
            DateTime today = DateTime.Today;

            if (lower.Contains("today"))
            {
                plan.DateRangeStart = today;
                plan.DateRangeEnd = today;
                plan.DateRangeLabel = "today";
            }
            else if (lower.Contains("tomorrow"))
            {
                plan.DateRangeStart = today.AddDays(1);
                plan.DateRangeEnd = today.AddDays(1);
                plan.DateRangeLabel = "tomorrow";
            }
            else if (lower.Contains("this week"))
            {
                plan.DateRangeStart = today;
                plan.DateRangeEnd = today.AddDays(7);
                plan.DateRangeLabel = "this_week";
            }
            else if (lower.Contains("next week"))
            {
                plan.DateRangeStart = today.AddDays(7);
                plan.DateRangeEnd = today.AddDays(14);
                plan.DateRangeLabel = "next_week";
            }
            else if (lower.Contains("this month"))
            {
                plan.DateRangeStart = new DateTime(today.Year, today.Month, 1);
                plan.DateRangeEnd = plan.DateRangeStart.Value.AddMonths(1).AddDays(-1);
                plan.DateRangeLabel = "this_month";
            }
            else
            {
                var dateMatch = Regex.Match(input, @"\b(\d{1,2})(?:st|nd|rd|th)?\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)(?:\s+(\d{4}))?\b", RegexOptions.IgnoreCase);
                if (dateMatch.Success)
                {
                    int day = int.Parse(dateMatch.Groups[1].Value);
                    string monthStr = dateMatch.Groups[2].Value;
                    int year = dateMatch.Groups[3].Success ? int.Parse(dateMatch.Groups[3].Value) : today.Year;
                    if (DateTime.TryParse($"{day} {monthStr} {year}", out var parsedDate))
                    {
                        plan.DateRangeStart = parsedDate;
                        plan.DateRangeEnd = parsedDate;
                        plan.DateRangeLabel = parsedDate.ToString("dd-MMM-yyyy");
                    }
                }
            }
        }

        private static void ExtractFinancialThreshold(string input, LegalQueryPlan plan)
        {
            var match = Regex.Match(input, @"(?:above|greater than|exceed(?:s|ed|ing)?|more than|over|>|>=)\s*(?:₹|rs\.?|inr)?\s*(\d+(?:\.\d+)?)\s*(lakh|lakhs|lac|lacs|crore|crores|cr)?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (decimal.TryParse(match.Groups[1].Value, out decimal value))
                {
                    string unit = match.Groups[2].Value.ToLowerInvariant();
                    if (unit.StartsWith("l"))
                        value *= 100000m;
                    else if (unit.StartsWith("c"))
                        value *= 10000000m;

                    plan.AmountThreshold = value;
                    plan.AmountComparator = "greater_than";
                }
            }
        }

        private static void ExtractDivision(string input, LegalQueryPlan plan)
        {
            string lower = input.ToLowerInvariant();
            if (lower.Contains("dharwad") || Regex.IsMatch(input, @"\b(dwr|hdc)\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = lower.Contains("city") ? 9 : 6;
                plan.DivisionCode = lower.Contains("city") ? "HDC" : "DWR";
                plan.DivisionName = lower.Contains("city") ? "Hubballi-Dharwad City" : "Dharwad";
            }
            else if (lower.Contains("hubballi") || lower.Contains("hubli") || Regex.IsMatch(input, @"\b(hbl)\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = lower.Contains("city") ? 9 : 8;
                plan.DivisionCode = lower.Contains("city") ? "HDC" : "HBL";
                plan.DivisionName = lower.Contains("city") ? "Hubballi-Dharwad City" : "Hubballi Rural";
            }
            else if (lower.Contains("belagavi") || lower.Contains("belgaum") || Regex.IsMatch(input, @"\bbgm\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 2; plan.DivisionCode = "BGM"; plan.DivisionName = "Belagavi";
            }
            else if (lower.Contains("bagalkot") || Regex.IsMatch(input, @"\bbgk\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 1; plan.DivisionCode = "BGK"; plan.DivisionName = "Bagalkot";
            }
            else if (lower.Contains("chikkodi") || Regex.IsMatch(input, @"\bckd\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 4; plan.DivisionCode = "CKD"; plan.DivisionName = "Chikkodi";
            }
            else if (lower.Contains("gadag") || Regex.IsMatch(input, @"\bgdg\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 7; plan.DivisionCode = "GDG"; plan.DivisionName = "Gadag";
            }
            else if (lower.Contains("haveri") || Regex.IsMatch(input, @"\bhvr\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 10; plan.DivisionCode = "HVR"; plan.DivisionName = "Haveri";
            }
            else if (lower.Contains("uttara kannada") || lower.Contains("karwar") || Regex.IsMatch(input, @"\bnkd\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 11; plan.DivisionCode = "NKD"; plan.DivisionName = "Uttara Kannada";
            }
            else if (lower.Contains("regional workshop") || Regex.IsMatch(input, @"\brwh\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 12; plan.DivisionCode = "RWH"; plan.DivisionName = "Regional Workshop Hubballi";
            }
            else if (lower.Contains("central office") || Regex.IsMatch(input, @"\bcoh\b", RegexOptions.IgnoreCase))
            {
                plan.DivisionId = 5; plan.DivisionCode = "COH"; plan.DivisionName = "Central Office";
            }
        }

        private static bool IsCommonStopword(string word)
        {
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "with", "case", "cases", "court", "status", "show", "give", "list", "who", "what"
            };
            return stopwords.Contains(word);
        }

        private static void ExtractCaseIdentifiers(string input, QueryRouteResult result)
        {
            // CNR match (e.g. KADH010012342024)
            var cnrMatch = Regex.Match(input, @"\b([A-Z]{4}\d{12})\b", RegexOptions.IgnoreCase);
            if (cnrMatch.Success)
            {
                result.ExtractedCNR = cnrMatch.Groups[1].Value.ToUpperInvariant();
            }

            // MVC match (e.g. MVC 123/2024 or MVC/123/24)
            var mvcMatch = Regex.Match(input, @"\b(?:MVC[/\s-]*)(\d+)[/\s-]*(20\d\d|\d\d)\b", RegexOptions.IgnoreCase);
            if (mvcMatch.Success)
            {
                result.ExtractedCaseType = "MVC";
                result.ExtractedCaseNumber = mvcMatch.Groups[1].Value;
                string yr = mvcMatch.Groups[2].Value;
                result.ExtractedYear = yr.Length == 2 ? 2000 + int.Parse(yr) : int.Parse(yr);
                return;
            }

            // Labour match (e.g. KID 45/2023 or ID 12/2022)
            var labourMatch = Regex.Match(input, @"\b(KID|ID|REF|WP|WA|LCA)[/\s-]*(\d+)[/\s-]*(20\d\d|\d\d)\b", RegexOptions.IgnoreCase);
            if (labourMatch.Success)
            {
                result.ExtractedCaseType = "LABOUR";
                result.ExtractedCaseNumber = labourMatch.Groups[2].Value;
                string yr = labourMatch.Groups[3].Value;
                result.ExtractedYear = yr.Length == 2 ? 2000 + int.Parse(yr) : int.Parse(yr);
                return;
            }

            // Appeal match (e.g. MFA 1001/2021)
            var appealMatch = Regex.Match(input, @"\b(MFA)[/\s-]*(\d+)[/\s-]*(20\d\d|\d\d)\b", RegexOptions.IgnoreCase);
            if (appealMatch.Success)
            {
                result.ExtractedCaseType = "APPEAL";
                result.ExtractedCaseNumber = appealMatch.Groups[2].Value;
                string yr = appealMatch.Groups[3].Value;
                result.ExtractedYear = yr.Length == 2 ? 2000 + int.Parse(yr) : int.Parse(yr);
                return;
            }

            // Gratuity match (e.g. PGA 15/2023)
            var gratuityMatch = Regex.Match(input, @"\b(PGA)[/\s-]*(\d+)[/\s-]*(20\d\d|\d\d)\b", RegexOptions.IgnoreCase);
            if (gratuityMatch.Success)
            {
                result.ExtractedCaseType = "GRATUITY";
                result.ExtractedCaseNumber = gratuityMatch.Groups[2].Value;
                string yr = gratuityMatch.Groups[3].Value;
                result.ExtractedYear = yr.Length == 2 ? 2000 + int.Parse(yr) : int.Parse(yr);
                return;
            }
        }

        private static void ExtractStatutoryProvisions(string input, QueryRouteResult result)
        {
            var matches = Regex.Matches(input, @"(?:Section|Sec\.?|Article|Order)\s+(\d+[\-A-Za-z]*)", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                string provision = m.Value.Trim();
                if (!result.ExtractedProvisions.Contains(provision, StringComparer.OrdinalIgnoreCase))
                {
                    result.ExtractedProvisions.Add(provision);
                }
            }

            if (Regex.IsMatch(input, @"Motor\s+Vehicles\s+Act", RegexOptions.IgnoreCase) && 
                !result.ExtractedProvisions.Any(p => p.Contains("Motor Vehicles Act", StringComparison.OrdinalIgnoreCase)))
            {
                result.ExtractedProvisions.Add("Motor Vehicles Act, 1988");
            }

            if (Regex.IsMatch(input, @"Payment\s+of\s+Gratuity\s+Act", RegexOptions.IgnoreCase) &&
                !result.ExtractedProvisions.Any(p => p.Contains("Payment of Gratuity Act", StringComparison.OrdinalIgnoreCase)))
            {
                result.ExtractedProvisions.Add("Payment of Gratuity Act, 1972");
            }

            if (Regex.IsMatch(input, @"(?:Employee'?s?\s+Compensation\s+Act|Workmen'?s?\s+Compensation\s+Act)", RegexOptions.IgnoreCase) &&
                !result.ExtractedProvisions.Any(p => p.Contains("Compensation Act", StringComparison.OrdinalIgnoreCase)))
            {
                result.ExtractedProvisions.Add("Employees' Compensation Act, 1923");
            }

            if (Regex.IsMatch(input, @"(?:Civil\s+Procedure\s+Code|CPC)", RegexOptions.IgnoreCase) &&
                !result.ExtractedProvisions.Any(p => p.Contains("CPC", StringComparison.OrdinalIgnoreCase)))
            {
                result.ExtractedProvisions.Add("Code of Civil Procedure, 1908 (CPC)");
            }
        }

        private static void DecomposeQuery(string input, LegalQueryPlan plan, QueryRouteResult result)
        {
            plan.SubQueries.Clear();
            int step = 1;

            // Detect Requested Output Type
            string lower = input.ToLowerInvariant();
            if (lower.Contains("table") || lower.Contains("tabular") || lower.Contains("register"))
                plan.RequestedOutputTypes.Add("Table");
            if (lower.Contains("summary") || lower.Contains("briefing") || lower.Contains("overview"))
                plan.RequestedOutputTypes.Add("Summary");
            if (lower.Contains("risk") || lower.Contains("exposure") || lower.Contains("liability"))
                plan.RequestedOutputTypes.Add("RiskAssessment");
            if (lower.Contains("cause list") || lower.Contains("schedule") || lower.Contains("calendar"))
                plan.RequestedOutputTypes.Add("CauseList");
            if (lower.Contains("compare") || lower.Contains("similar") || lower.Contains("precedent"))
                plan.RequestedOutputTypes.Add("ComparativeAnalysis");

            if (!plan.RequestedOutputTypes.Any())
                plan.RequestedOutputTypes.Add("Summary");

            // SubQuery 1: Structured Entity / Case Records
            if (!string.IsNullOrWhiteSpace(plan.VehicleNumber) || 
                !string.IsNullOrWhiteSpace(plan.CaseNumber) || 
                !string.IsNullOrWhiteSpace(plan.CNRNumber) ||
                !string.IsNullOrWhiteSpace(plan.AdvocateName) ||
                plan.DivisionId.HasValue)
            {
                var entities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(plan.VehicleNumber)) entities["VehicleNumber"] = plan.VehicleNumber;
                if (!string.IsNullOrWhiteSpace(plan.CaseNumber)) entities["CaseNumber"] = plan.CaseNumber;
                if (!string.IsNullOrWhiteSpace(plan.CNRNumber)) entities["CNRNumber"] = plan.CNRNumber;
                if (!string.IsNullOrWhiteSpace(plan.AdvocateName)) entities["AdvocateName"] = plan.AdvocateName;
                if (plan.DivisionId.HasValue) entities["DivisionId"] = plan.DivisionId.Value.ToString();

                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Retrieve internal database litigation records for target entity",
                    TargetSource = "Database",
                    QueryText = input,
                    Entities = entities
                });
            }

            // SubQuery 2: Financial Threshold or Hearing Calendar Filters
            if (plan.AmountThreshold.HasValue || plan.DateRangeStart.HasValue)
            {
                var filterEntities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (plan.AmountThreshold.HasValue) filterEntities["Threshold"] = plan.AmountThreshold.Value.ToString();
                if (plan.DateRangeStart.HasValue) filterEntities["DateStart"] = plan.DateRangeStart.Value.ToString("yyyy-MM-dd");
                if (plan.DateRangeEnd.HasValue) filterEntities["DateEnd"] = plan.DateRangeEnd.Value.ToString("yyyy-MM-dd");

                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Filter records by financial exposure or judicial date calendar",
                    TargetSource = "Database",
                    QueryText = input,
                    Entities = filterEntities
                });
            }

            // SubQuery 3: Document Exhibits / Orders / Awards
            if (plan.RequiresDocuments || result.RequiredSources.Contains(QuerySourceCategory.InternalDocuments))
            {
                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Search uploaded case files, chargesheets, orders, and award copies",
                    TargetSource = "Document",
                    QueryText = input,
                    Entities = new Dictionary<string, string> { ["CaseNumber"] = plan.CaseNumber ?? "" }
                });
            }

            // SubQuery 4: e-Courts Live Tracking
            if (plan.RequiresECourts || result.RequiredSources.Contains(QuerySourceCategory.ECourts))
            {
                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Query National e-Courts NAPIX registry for official court stage and daily orders",
                    TargetSource = "ECourts",
                    QueryText = input,
                    Entities = new Dictionary<string, string> { ["CNR"] = plan.CNRNumber ?? "" }
                });
            }

            // SubQuery 5: Judicial Precedents & High Court / Supreme Court Case Law
            if (plan.RequiresLegalPrecedents || plan.RequiresWebSearch || result.RequiredSources.Contains(QuerySourceCategory.Judgments) || result.RequiredSources.Contains(QuerySourceCategory.LegalWeb))
            {
                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Retrieve binding Supreme Court and High Court precedents and statutory interpretations",
                    TargetSource = "JudgmentRepo",
                    QueryText = input,
                    Entities = new Dictionary<string, string> { ["Provisions"] = string.Join(", ", plan.StatutoryProvisions) }
                });
            }

            // SubQuery 6: Comparative Analysis / Similar Cases
            if (result.RequiredSources.Contains(QuerySourceCategory.SimilarCases))
            {
                plan.SubQueries.Add(new DecomposedSubQuery
                {
                    Step = step++,
                    Description = "Evaluate factual and legal similarity against past NWKRTC matters",
                    TargetSource = "SimilarCases",
                    QueryText = input,
                    Entities = new Dictionary<string, string>()
                });
            }
        }

        private static bool DetectFollowUp(string input, bool hasActiveCaseContext)
        {
            if (!hasActiveCaseContext)
                return false;

            string lower = input.ToLowerInvariant();
            var followUpIndicators = new[]
            {
                "its ", " it ", "this case", "the case", "in this matter",
                "what are its", "who is the driver", "our strongest",
                "opposing party", "our position", "support us", "supporting us",
                "what did they decide", "what about the award"
            };

            return followUpIndicators.Any(ind => lower.Contains(ind));
        }
    }
}
