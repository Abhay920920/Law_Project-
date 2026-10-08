using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class LegalWebSearchService : ILegalWebSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly NyayaPathaOptions _options;
        private readonly ILogger<LegalWebSearchService> _logger;

        private static readonly HashSet<string> ApprovedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "sci.gov.in",
            "main.sci.gov.in",
            "api.sci.gov.in",
            "karnatakahi.gov.in",
            "hck.gov.in",
            "judgments.ecourts.gov.in",
            "services.ecourts.gov.in",
            "indiacode.nic.in",
            "morth.nic.in",
            "indiankanoon.org",
            "livelaw.in",
            "barandbench.com",
            "verdictum.in",
            "casemine.com",
            "lawweb.in",
            "supremetoday.ai",
            "lawtext.in"
        };

        public LegalWebSearchService(
            HttpClient httpClient,
            IConfiguration config,
            IOptions<NyayaPathaOptions> options,
            ILogger<LegalWebSearchService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _options = options?.Value ?? new NyayaPathaOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ExternalLegalSourceDto>> SearchPrecedentsAsync(
            string query, 
            LegalSearchFilter? filter = null, 
            int maxResults = 8, 
            CancellationToken cancellationToken = default)
        {
            if (!_options.EnableLegalSearch || string.IsNullOrWhiteSpace(query))
                return new List<ExternalLegalSourceDto>();

            var matchedSources = new List<ExternalLegalSourceDto>();
            string cleanQuery = SanitizeSearchQuery(query);

            try
            {
                // 1. Live Google/Web engine search and Indian Kanoon judicial search in parallel
                var webTask = ExecuteLiveWebSearchEngineAsync(cleanQuery, filter, maxResults, cancellationToken);
                var kanoonTask = ExecuteLiveIndianKanoonSearchAsync(cleanQuery, filter, maxResults, cancellationToken);

                await Task.WhenAll(webTask, kanoonTask);

                matchedSources.AddRange(await webTask);
                matchedSources.AddRange(await kanoonTask);

                // 2. If SerpApi or Google Custom Search is configured, query official court domains
                string apiKey = _config["LegalWebSearch:ApiKey"] ?? Environment.GetEnvironmentVariable("LEGAL_SEARCH_API_KEY") ?? "";
                string endpoint = _config["LegalWebSearch:Endpoint"] ?? "";

                if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint) && _options.EnableWebSearch)
                {
                    var liveResults = await ExecuteLiveWebSearchAsync(cleanQuery, endpoint, apiKey, filter, maxResults, cancellationToken);
                    matchedSources.AddRange(liveResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live legal web search error for query: {Query}", cleanQuery);
            }

            // Deduplicate by URL and Title, then prioritize relevance
            return matchedSources
                .GroupBy(s => s.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(s => ScoreSourceRelevance(s, cleanQuery))
                .Take(Math.Max(1, maxResults))
                .ToList();
        }

        public async Task<List<ExternalLegalSourceDto>> SearchRecentJudgmentsAsync(
            string legalTopic, 
            string? court = null, 
            int maxResults = 5, 
            CancellationToken cancellationToken = default)
        {
            var filter = new LegalSearchFilter
            {
                Court = court,
                AuthoritativeOnly = true
            };
            return await SearchPrecedentsAsync(legalTopic, filter, maxResults, cancellationToken);
        }

        public async Task<List<ExternalLegalSourceDto>> SearchByCaseOrCitationAsync(
            string caseOrCitation, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(caseOrCitation))
                return new List<ExternalLegalSourceDto>();

            return await SearchPrecedentsAsync(caseOrCitation.Trim(), null, 5, cancellationToken);
        }

        public async Task<List<ExternalLegalSourceDto>> SearchByPartiesAsync(
            string petitioner, 
            string respondent, 
            string? court = null, 
            int maxResults = 5, 
            CancellationToken cancellationToken = default)
        {
            string query = $"{petitioner} vs {respondent}";
            var filter = new LegalSearchFilter { Court = court };
            return await SearchPrecedentsAsync(query, filter, maxResults, cancellationToken);
        }

        private async Task<List<ExternalLegalSourceDto>> ExecuteLiveWebSearchEngineAsync(
            string query,
            LegalSearchFilter? filter,
            int maxResults,
            CancellationToken cancellationToken)
        {
            var list = new List<ExternalLegalSourceDto>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(8, _options.WebSearchTimeoutSeconds)));

            try
            {
                string encodedQuery = Uri.EscapeDataString(query);
                string searchUrl = $"https://html.duckduckgo.com/html/?q={encodedQuery}";

                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                using var response = await _httpClient.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Live web search engine returned {StatusCode} for query {Query}", response.StatusCode, query);
                    return list;
                }

                string html = await response.Content.ReadAsStringAsync(cts.Token);

                var resultMatches = Regex.Matches(html, @"<div[^>]*class=""[^""]*web-result[^""]*""[\s\S]*?</h2>[\s\S]*?<a[^>]*class=""result__snippet""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);

                foreach (Match m in resultMatches)
                {
                    string block = m.Value;
                    var urlMatch = Regex.Match(block, @"uddg=([^&""'>]+)", RegexOptions.IgnoreCase);
                    var titleMatch = Regex.Match(block, @"<a[^>]*class=""result__a""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                    var snippetMatch = Regex.Match(block, @"<a[^>]*class=""result__snippet""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);

                    if (urlMatch.Success && titleMatch.Success)
                    {
                        string rawUrl = Uri.UnescapeDataString(urlMatch.Groups[1].Value);
                        if (!rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                        string rawTitle = CleanWebText(titleMatch.Groups[1].Value);
                        string snippet = snippetMatch.Success ? CleanWebText(snippetMatch.Groups[1].Value) : "";

                        Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri);
                        string domain = uri?.Host.ToLowerInvariant() ?? "";

                        string court = "Indian Judiciary / Legal Source";
                        if (domain.Contains("sci.gov.in") || rawTitle.Contains("Supreme Court", StringComparison.OrdinalIgnoreCase) || snippet.Contains("Supreme Court", StringComparison.OrdinalIgnoreCase))
                        {
                            court = "Supreme Court of India";
                        }
                        else if (domain.Contains("karnatakahi.gov.in") || rawTitle.Contains("Karnataka High Court", StringComparison.OrdinalIgnoreCase) || snippet.Contains("Karnataka High Court", StringComparison.OrdinalIgnoreCase))
                        {
                            court = "High Court of Karnataka";
                        }

                        if (!list.Any(x => x.Url.Equals(rawUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(new ExternalLegalSourceDto
                            {
                                Title = rawTitle,
                                Url = rawUrl,
                                SourceDomain = domain,
                                Court = court,
                                JudgmentDate = ParseDateFromTitle(rawTitle) ?? ParseDateFromSnippet(snippet),
                                Excerpt = snippet,
                                SourceType = court.Contains("Supreme", StringComparison.OrdinalIgnoreCase) ? "SupremeCourt" : "HighCourt",
                                IsVerifiedDomain = true,
                                RetrievalDate = DateTime.Now
                            });
                        }

                        if (list.Count >= maxResults) break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live web search engine execution failed for query {Query}", query);
            }

            return list;
        }

        private async Task<List<ExternalLegalSourceDto>> ExecuteLiveIndianKanoonSearchAsync(
            string query,
            LegalSearchFilter? filter,
            int maxResults,
            CancellationToken cancellationToken)
        {
            var list = new List<ExternalLegalSourceDto>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(8, _options.WebSearchTimeoutSeconds)));

            try
            {
                string searchParam = query;
                if (filter != null && !string.IsNullOrWhiteSpace(filter.Court))
                {
                    if (filter.Court.Contains("Supreme", StringComparison.OrdinalIgnoreCase))
                        searchParam += " doctypes:supremecourt";
                    else if (filter.Court.Contains("Karnataka", StringComparison.OrdinalIgnoreCase))
                        searchParam += " doctypes:karnataka";
                }

                string searchUrl = $"https://indiankanoon.org/search/?formInput={Uri.EscapeDataString(searchParam)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                using var response = await _httpClient.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Indian Kanoon search returned status code {StatusCode} for query {Query}", response.StatusCode, query);
                    return list;
                }

                string html = await response.Content.ReadAsStringAsync(cts.Token);

                // 1. Try parsing JSON script data block if present
                var jsonMatch = Regex.Match(html, @"<script\s+id=""ai-overview-data""\s+type=""application/json"">([\s\S]*?)</script>");
                if (jsonMatch.Success)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonMatch.Groups[1].Value);
                        if (doc.RootElement.TryGetProperty("results", out var resArray) && resArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in resArray.EnumerateArray())
                            {
                                string title = item.TryGetProperty("title", out var tp) ? tp.GetString() ?? "" : "";
                                string fragment = item.TryGetProperty("fragment", out var fp) ? fp.GetString() ?? "" : "";
                                string court = item.TryGetProperty("court", out var cp) ? cp.GetString() ?? "" : "Indian Judiciary";

                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    string fullUrl = "https://indiankanoon.org/search/?formInput=" + Uri.EscapeDataString(title);
                                    list.Add(new ExternalLegalSourceDto
                                    {
                                        Title = CleanWebText(title),
                                        Url = fullUrl,
                                        SourceDomain = "indiankanoon.org",
                                        Court = CleanWebText(court),
                                        JudgmentDate = ParseDateFromTitle(title),
                                        Excerpt = CleanWebText(fragment),
                                        SourceType = court.Contains("Supreme", StringComparison.OrdinalIgnoreCase) ? "SupremeCourt" : "HighCourt",
                                        IsVerifiedDomain = true,
                                        RetrievalDate = DateTime.Now
                                    });
                                }

                                if (list.Count >= maxResults) break;
                            }
                        }
                    }
                    catch { /* fallback to HTML parser below */ }
                }

                // 2. Parse HTML article results to extract direct document URLs (/doc/{id}/)
                var articleMatches = Regex.Matches(html, @"<article\s+class=""result""[\s\S]*?</article>", RegexOptions.IgnoreCase);
                foreach (Match m in articleMatches)
                {
                    string articleHtml = m.Value;

                    var titleMatch = Regex.Match(articleHtml, @"<h4\s+class=""result_title"">\s*<a\s+href=""([^""]+)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                    if (!titleMatch.Success) continue;

                    string href = titleMatch.Groups[1].Value.Trim();
                    string rawTitle = CleanWebText(titleMatch.Groups[2].Value);

                    string docId = "";
                    var docIdMatch = Regex.Match(href, @"/(?:doc|docfragment)/(\d+)/");
                    if (docIdMatch.Success)
                    {
                        docId = docIdMatch.Groups[1].Value;
                    }

                    string fullUrl = !string.IsNullOrEmpty(docId)
                        ? $"https://indiankanoon.org/doc/{docId}/"
                        : (href.StartsWith("http") ? href : $"https://indiankanoon.org{href}");

                    string snippet = "";
                    var snippetMatch = Regex.Match(articleHtml, @"<div\s+class=""headline"">([\s\S]*?)</div>", RegexOptions.IgnoreCase);
                    if (snippetMatch.Success)
                    {
                        snippet = CleanWebText(snippetMatch.Groups[1].Value);
                    }

                    string court = "Indian Judiciary";
                    var courtMatch = Regex.Match(articleHtml, @"<span\s+class=""docsource"">([\s\S]*?)</span>", RegexOptions.IgnoreCase);
                    if (courtMatch.Success)
                    {
                        court = CleanWebText(courtMatch.Groups[1].Value);
                    }

                    if (!list.Any(x => x.Url.Equals(fullUrl, StringComparison.OrdinalIgnoreCase) || x.Title.Equals(rawTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new ExternalLegalSourceDto
                        {
                            Title = rawTitle,
                            Url = fullUrl,
                            SourceDomain = "indiankanoon.org",
                            Court = court,
                            JudgmentDate = ParseDateFromTitle(rawTitle),
                            Excerpt = snippet,
                            SourceType = court.Contains("Supreme", StringComparison.OrdinalIgnoreCase) ? "SupremeCourt" : "HighCourt",
                            IsVerifiedDomain = true,
                            RetrievalDate = DateTime.Now
                        });
                    }

                    if (list.Count >= maxResults) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live Indian Kanoon search request error for query {Query}", query);
            }

            return list;
        }

        private async Task<List<ExternalLegalSourceDto>> ExecuteLiveWebSearchAsync(
            string query, 
            string endpoint, 
            string apiKey, 
            LegalSearchFilter? filter, 
            int maxResults, 
            CancellationToken cancellationToken)
        {
            var list = new List<ExternalLegalSourceDto>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(8, _options.WebSearchTimeoutSeconds)));

            try
            {
                string siteFilter = string.Join(" OR site:", ApprovedDomains.Take(4).Select(d => $"site:{d}"));
                string finalQuery = $"{query} ({siteFilter})";

                var requestUri = $"{endpoint}?q={Uri.EscapeDataString(finalQuery)}&api_key={apiKey}&num={maxResults}";
                using var response = await _httpClient.GetAsync(requestUri, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("organic_results", out var organic) && organic.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in organic.EnumerateArray())
                        {
                            string title = item.TryGetProperty("title", out var tp) ? (tp.GetString() ?? "") : "";
                            string link = item.TryGetProperty("link", out var lp) ? (lp.GetString() ?? "") : "";
                            string snippet = item.TryGetProperty("snippet", out var sp) ? (sp.GetString() ?? "") : "";

                            if (!string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri))
                            {
                                string domain = uri.Host.ToLowerInvariant();
                                bool isApproved = ApprovedDomains.Any(ad => domain.EndsWith(ad, StringComparison.OrdinalIgnoreCase));
                                string courtName = domain.Contains("sci.gov.in") ? "Supreme Court of India"
                                                 : domain.Contains("karnatakahi.gov.in") ? "High Court of Karnataka"
                                                 : domain.Contains("ecourts") ? "National e-Courts Portal"
                                                 : "Indian Judiciary / Legal Portal";

                                list.Add(new ExternalLegalSourceDto
                                {
                                    Title = CleanWebText(title),
                                    Url = link,
                                    SourceDomain = domain,
                                    Court = courtName,
                                    JudgmentDate = ParseDateFromTitle(title),
                                    Excerpt = CleanWebText(snippet),
                                    SourceType = domain.Contains("sci.gov.in") ? "SupremeCourt" : "HighCourt",
                                    IsVerifiedDomain = isApproved,
                                    RetrievalDate = DateTime.Now
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live legal web search request failed for query {Query}", query);
            }

            return list;
        }

        private static int ScoreSourceRelevance(ExternalLegalSourceDto source, string query)
        {
            int score = 0;
            if (source.IsVerifiedDomain) score += 10;
            if (source.SourceDomain.Contains("sci.gov.in", StringComparison.OrdinalIgnoreCase)) score += 25;
            if (source.SourceDomain.Contains("karnatakahi.gov.in", StringComparison.OrdinalIgnoreCase)) score += 20;
            if (source.SourceType == "SupremeCourt") score += 15;
            if (source.SourceType == "HighCourt") score += 10;

            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var w in words)
            {
                if (source.Title.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 5;
                if (source.Excerpt.Contains(w, StringComparison.OrdinalIgnoreCase)) score += 2;
            }

            return score;
        }

        private static DateTime? ParseDateFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var match = Regex.Match(title, @"on\s+(\d{1,2}\s+[A-Za-z]+,?\s+\d{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string rawDate = match.Groups[1].Value.Replace(",", "");
                if (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt;
                }
            }
            return null;
        }

        private static DateTime? ParseDateFromSnippet(string snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet)) return null;

            var match = Regex.Match(snippet, @"(?:on\s+|dated\s+|-\s+)([A-Za-z]+\s+\d{1,2},?\s+\d{4}|\d{1,2}\s+[A-Za-z]+,?\s+\d{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string raw = match.Groups[1].Value.Replace(",", "");
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt;
                }
            }
            return null;
        }

        private static string SanitizeSearchQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;
            string clean = Regex.Replace(query, @"[^\w\s\-\/\.]", " ");
            return Regex.Replace(clean, @"\s+", " ").Trim();
        }

        private static string CleanWebText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string noHtml = Regex.Replace(input, "<.*?>", string.Empty);
            string sanitized = noHtml.Replace("SYSTEM:", "").Replace("INSTRUCTION:", "");
            return sanitized.Length > 400 ? sanitized.Substring(0, 400) + "..." : sanitized;
        }
    }
}
