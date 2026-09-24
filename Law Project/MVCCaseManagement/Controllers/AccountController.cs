using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Common;
using MVCCaseManagement.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly ISMSService _smsService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IUserRepository userRepository, ISMSService smsService, IMemoryCache cache, ILogger<AccountController> logger)
        {
            _userRepository = userRepository;
            _smsService = smsService;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _userRepository.GetUserByUsername(model.Username);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found or is inactive.");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Mobile))
            {
                ModelState.AddModelError(string.Empty, "Mobile number not registered. Please contact administrator.");
                return View(model);
            }

            // Generate 6-digit OTP (cryptographically secure)
            string otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            
            // Save OTP to DB
            _userRepository.SaveOtp(user.UserID, otp, DateTime.Now.AddMinutes(10));

            // Send via SMS
            bool sent = await _smsService.SendOTPAsync(user.Mobile, otp);

            if (sent)
            {
                TempData["Username"] = user.Username;
                TempData["SuccessMessage"] = "OTP has been sent to your registered mobile number.";
                return RedirectToAction("VerifyOtp", new { username = user.Username });
            }
            else
            {
                // In a real app we wouldn't show the raw response, but for debugging this integration it's helpful
                ModelState.AddModelError(string.Empty, $"Failed to send OTP. Gateway response: {_smsService.LastResponse}. Please contact technical support.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult VerifyOtp(string username)
        {
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");
            var model = new VerifyOtpViewModel { Username = username };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _userRepository.GetUserByUsername(model.Username);
            if (user == null) return RedirectToAction("Login");

            // OTP attempt limiting: max 5 attempts per user
            string otpAttemptKey = $"otp_attempts_{user.UserID}";
            int attempts = _cache.GetOrCreate(otpAttemptKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return 0;
            });

            if (attempts >= 5)
            {
                ModelState.AddModelError(string.Empty, "Too many incorrect OTP attempts. Please request a new OTP.");
                return View(model);
            }

            bool isValid = _userRepository.VerifyOtp(user.UserID, model.OTP);
            if (isValid)
            {
                // Success — clear attempt counter and update password
                _cache.Remove(otpAttemptKey);

                var newHash = PasswordHelper.HashPassword(model.NewPassword);
                bool updated = _userRepository.UpdatePassword(user.Username, newHash);

                if (updated)
                {
                    _userRepository.MarkOtpAsVerified(user.UserID, model.OTP);
                    TempData["SuccessMessage"] = "Password reset successfully. Please login with your new password.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the password.");
                    return View(model);
                }
            }
            else
            {
                // Increment attempt counter
                _cache.Set(otpAttemptKey, attempts + 1, TimeSpan.FromMinutes(10));
                ModelState.AddModelError(string.Empty, "Invalid or expired OTP.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, redirect to Home dashboard
            if (HttpContext.Session.GetInt32(SessionKeys.UserID).HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Brute-force protection: lock after 5 failed attempts for 15 minutes
            string lockKey = $"login_fail_{model.Username?.ToLower()}";
            string lockoutKey = $"login_lockout_{model.Username?.ToLower()}";

            if (_cache.TryGetValue(lockoutKey, out _))
            {
                ModelState.AddModelError(string.Empty, "Account temporarily locked due to multiple failed login attempts. Please try again in 15 minutes.");
                return View(model);
            }

            var user = _userRepository.GetUserByUsername(model.Username);

            if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
            {
                // Increment fail counter
                int failCount = _cache.GetOrCreate(lockKey, e =>
                {
                    e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                    return 0;
                });
                failCount++;
                _cache.Set(lockKey, failCount, TimeSpan.FromMinutes(15));

                if (failCount >= 5)
                {
                    _cache.Set(lockoutKey, true, TimeSpan.FromMinutes(15));
                    _logger.LogWarning("Login lockout triggered for username '{Username}' from IP {IP}",
                        model.Username, HttpContext.Connection.RemoteIpAddress);
                    ModelState.AddModelError(string.Empty, "Account locked after 5 failed attempts. Please try again in 15 minutes.");
                    return View(model);
                }

                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administrator.");
                return View(model);
            }

            // Success — clear fail counter
            _cache.Remove(lockKey);
            _cache.Remove(lockoutKey);

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUser(User user)
        {
            // Create Claims for Identity
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.RoleName),
                new Claim("DivisionID", user.DivisionID?.ToString() ?? "0"),
                new Claim("DivisionName", user.DivisionName ?? "Central Office")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            // Sign in using Cookie Authentication
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Security: Clear session to prevent session fixation attacks
            HttpContext.Session.Clear();

            // Maintain Session for legacy compatibility and ease of use in Views
            HttpContext.Session.SetInt32(SessionKeys.UserID, user.UserID);
            HttpContext.Session.SetString(SessionKeys.Username, user.Username);
            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            HttpContext.Session.SetInt32(SessionKeys.RoleID, user.RoleID);
            HttpContext.Session.SetString(SessionKeys.RoleName, user.RoleName);
            
            if (user.DivisionID.HasValue)
            {
                HttpContext.Session.SetInt32(SessionKeys.DivisionID, user.DivisionID.Value);
                HttpContext.Session.SetString(SessionKeys.DivisionName, user.DivisionName ?? string.Empty);
            }

            // Update last login
            _userRepository.UpdateLastLogin(user.UserID);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [Authorize]
        public IActionResult Heartbeat()
        {
            return Json(new { success = true, time = DateTime.Now.ToString("HH:mm:ss") });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
