using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace MVCCaseManagement.Utils
{
    public interface ITR18Service
    {
        Task<TR18AccidentResponse> GetAccidentDetails(DateTime? accidentDate, string vehicleNo);
    }

    public class TR18Service : ITR18Service
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TR18Service> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public TR18Service(HttpClient httpClient, ILogger<TR18Service> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["AppSettings:TR18ApiKey"] 
                ?? configuration["TR18:ApiKey"] 
                ?? Environment.GetEnvironmentVariable("TR18__APIKEY") 
                ?? "";
            _baseUrl = configuration["AppSettings:TR18ApiBaseUrl"] 
                ?? configuration["TR18:BaseUrl"] 
                ?? Environment.GetEnvironmentVariable("TR18__BASEURL") 
                ?? "https://tr18.itnwkrtc.in/api/";
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<TR18AccidentResponse> GetAccidentDetails(DateTime? accidentDate, string vehicleNo)
        {
            if (!accidentDate.HasValue || string.IsNullOrEmpty(vehicleNo))
            {
                return new TR18AccidentResponse { Success = false, Message = "Missing parameters." };
            }

            try
            {
                // Format: https://tr18.itnwkrtc.in/api/v1/accidents.php?action=detail&date=2024-01-01&vehicle_no=KA01F1234
                string formattedDate = accidentDate.Value.ToString("yyyy-MM-dd");
                string url = $"{_baseUrl}?action=detail&date={formattedDate}&vehicle_no={Uri.EscapeDataString(vehicleNo)}";

                _logger.LogInformation("Fetching TR-18 data for {Vehicle} on {Date} via {Url}", vehicleNo, formattedDate, url);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-API-KEY", _apiKey);
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString 
                    };
                    
                    AccidentDetails accidentData = null;
                    try 
                    {
                        var resultWrapper = JsonSerializer.Deserialize<TR18AccidentResponse>(content, options);
                        if (resultWrapper != null && (resultWrapper.Data != null)) {
                            accidentData = resultWrapper.Data;
                        }
                    }
                    catch { }

                    if (accidentData == null)
                    {
                        accidentData = JsonSerializer.Deserialize<AccidentDetails>(content, options);
                    }

                    if (accidentData != null)
                    {
                        // Ensure URLs are absolute
                        string portalBase = _baseUrl.ToLower().Split("/api/")[0]; // Get base domain/path
                        
                        Action<MediaItem> makeAbsolute = (item) => {
                            if (item != null && !string.IsNullOrEmpty(item.url)) {
                                if (item.url.Contains("localhost")) {
                                    // The remote TR18 API is incorrectly returning localhost URLs (probably from local hardcoded DB entries or server config issues).
                                    // Replace 'http://localhost/Tr_system' and 'http://localhost:80/Tr_system' with 'https://tr18.itnwkrtc.in'
                                    item.url = item.url.Replace("http://localhost:80/Tr_system", portalBase.TrimEnd('/'));
                                    item.url = item.url.Replace("http://localhost/Tr_system", portalBase.TrimEnd('/'));
                                }
                                else if (!item.url.StartsWith("http")) {
                                    item.url = portalBase.TrimEnd('/') + "/" + item.url.TrimStart('/');
                                }
                            }
                        };

                        // Process images
                        if (accidentData.images != null) accidentData.images.ForEach(makeAbsolute);
                        // Process videos
                        if (accidentData.videos != null) accidentData.videos.ForEach(makeAbsolute);
                        // Process tr18_form media
                        if (accidentData.tr18_form?.media != null) accidentData.tr18_form.media.ForEach(makeAbsolute);

                        // Map to legacy UI format (List<string>)
                        accidentData.PhotoUrls = accidentData.images?.Select(i => i.url).ToList() ?? new List<string>();
                        accidentData.VideoUrls = accidentData.videos?.Select(i => i.url).ToList() ?? new List<string>();

                        // Include tr18_form.media in lists if not already there
                        if (accidentData.tr18_form?.media != null)
                        {
                            foreach (var m in accidentData.tr18_form.media)
                            {
                                if (m.type == "image" && !accidentData.PhotoUrls.Contains(m.url))
                                    accidentData.PhotoUrls.Add(m.url);
                                else if (m.type == "video" && !accidentData.VideoUrls.Contains(m.url))
                                    accidentData.VideoUrls.Add(m.url);
                            }
                        }

                        return new TR18AccidentResponse { Success = true, Data = accidentData };
                    }

                    return new TR18AccidentResponse { Success = false, Message = "Failed to parse response data." };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("TR-18 API Key Invalid or Domain Not Allowed (401/403).");
                    return new TR18AccidentResponse { Success = false, Message = "API Authorization Failed. Please check X-API-KEY and Allowed Origins." };
                }

                return new TR18AccidentResponse { Success = false, Message = $"API returned {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching TR-18 data");
                return new TR18AccidentResponse { Success = false, Message = ex.Message };
            }
        }
    }
}
