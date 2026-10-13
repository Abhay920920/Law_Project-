using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Utils
{
    public class SMSService : ISMSService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SMSService> _logger;
        
        public string LastResponse { get; private set; } = string.Empty;

        private readonly string _username;
        private readonly string _password;
        private readonly string _senderId;
        private readonly string _secureKey;
        private readonly string _apiUrl;
        private readonly string _templateId;

        public SMSService(HttpClient httpClient, ILogger<SMSService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _username = configuration["SMS:Username"] ?? Environment.GetEnvironmentVariable("SMS__USERNAME") ?? "";
            _password = configuration["SMS:Password"] ?? Environment.GetEnvironmentVariable("SMS__PASSWORD") ?? "";
            _senderId = configuration["SMS:SenderId"] ?? Environment.GetEnvironmentVariable("SMS__SENDERID") ?? "NWKRTC";
            _secureKey = configuration["SMS:SecureKey"] ?? Environment.GetEnvironmentVariable("SMS__SECUREKEY") ?? "";
            _apiUrl = configuration["SMS:ApiUrl"] ?? Environment.GetEnvironmentVariable("SMS__APIURL") ?? "https://smsmobile1.karnataka.gov.in/index.php/sendmsg";
            _templateId = configuration["SMS:TemplateId"] ?? Environment.GetEnvironmentVariable("SMS__TEMPLATEID") ?? "1107174825473036218";
        }

        public async Task<bool> SendOTPAsync(string mobileNumber, string otp)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
                {
                    _logger.LogWarning("SMS Service credentials are not configured. Unable to dispatch OTP.");
                    LastResponse = "SMS credentials not configured.";
                    return false;
                }

                // Exact Template from PHP: Your one time password for Statistical Data Analysis System is {#var#} -NWKRTC
                string appName = "Statistical Data Analysis System";
                string message = $"Your one time password for {appName} is {otp} -{_senderId.Trim()}";
                
                // Security Key Calculation (SHA512)
                string hashInput = _username.Trim() + _senderId.Trim() + message.Trim() + _secureKey.Trim();
                string hashKey = GenerateSha512(hashInput);

                // Password Hashing (SHA1)
                string encryptedPassword = GenerateSha1(_password);

                // Ensure mobile is in 91xxxxxxxxxx format (12 digits)
                string cleanMobile = string.Concat(mobileNumber.Where(char.IsDigit));
                if (cleanMobile.Length == 10) cleanMobile = "91" + cleanMobile;

                // Mask mobile number for privacy (e.g. 91******1234)
                string maskedMobile = cleanMobile.Length > 4 
                    ? string.Concat(cleanMobile.AsSpan(0, 2), "******", cleanMobile.AsSpan(cleanMobile.Length - 4))
                    : "****";

                // Build Query Parameters
                var queryParams = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "username", _username.Trim() },
                    { "password", encryptedPassword },
                    { "senderid", _senderId.Trim() },
                    { "content", message.Trim() },
                    { "smsservicetype", "otpmsg" },
                    { "mobileno", cleanMobile },
                    { "key", hashKey },
                    { "templateid", _templateId.Trim() },
                    { "param1", appName },
                    { "param2", otp }
                };

                _logger.LogInformation("Sending SMS POST Request to {Url} for {Mobile}", _apiUrl, maskedMobile);

                var contentData = new FormUrlEncodedContent(queryParams);
                var response = await _httpClient.PostAsync(_apiUrl, contentData);
                
                var content = await response.Content.ReadAsStringAsync();
                LastResponse = content;
                
                _logger.LogInformation("SMS Gateway Status: {Status}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    // PHP: stripos((string)$response, '402') !== false || stripos((string)$response, 'success') !== false
                    return content.Contains("402") || content.ToLower().Contains("success");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending SMS to {Mobile}", mobileNumber);
                LastResponse = ex.Message;
                return false;
            }
        }

        private string GenerateSha512(string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha512.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string GenerateSha1(string input)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha1.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}
