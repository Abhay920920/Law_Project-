using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.Linq;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationRepository _notificationRepo;

        private readonly ILogger<NotificationController> _logger;

        public NotificationController(INotificationRepository notificationRepo, ILogger<NotificationController> logger)
        {
            _notificationRepo = notificationRepo;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst("UserID") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return (claim != null && int.TryParse(claim.Value, out int id)) ? id : 0;
        }

        private int GetCurrentDivisionId()
        {
            var divClaim = User.FindFirst("DivisionID")?.Value;
            return int.TryParse(divClaim, out int id) ? id : 0;
        }

        [HttpGet]
        public IActionResult GetUnread()
        {
            try
            {
                int userId = GetCurrentUserId();
                int divisionId = GetCurrentDivisionId();
                var unread = _notificationRepo.GetUnreadNotifications(userId > 0 ? userId : null, divisionId > 0 ? divisionId : null) ?? Enumerable.Empty<Notification>();
                return Json(new { count = unread.Count(), notifications = unread.Take(5) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread notifications");
                return Json(new { count = 0, notifications = Array.Empty<object>() });
            }
        }

        [HttpPost, HttpGet]
        [IgnoreAntiforgeryToken]
        [Route("Notification/MarkAsRead/{id:int?}")]
        public IActionResult MarkAsRead(int id)
        {
            try
            {
                if (id > 0)
                {
                    _notificationRepo.MarkAsRead(id);
                }
                return Json(new { success = true, id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {Id} as read", id);
                return Json(new { success = false, id = id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllAsRead()
        {
            try
            {
                int userId = GetCurrentUserId();
                int divisionId = GetCurrentDivisionId();
                _notificationRepo.MarkAllAsRead(userId > 0 ? userId : null, divisionId > 0 ? divisionId : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
            }
            return RedirectToAction("Index");
        }

        public IActionResult Index()
        {
            try
            {
                int userId = GetCurrentUserId();
                int divisionId = GetCurrentDivisionId();
                var notifications = _notificationRepo.GetAllNotifications(userId > 0 ? userId : null, divisionId > 0 ? divisionId : null) ?? Enumerable.Empty<Notification>();
                return View(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification list");
                return View(Enumerable.Empty<Notification>());
            }
        }
    }
}
