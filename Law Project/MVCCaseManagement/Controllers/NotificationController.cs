using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using System.Linq;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationRepository _notificationRepo;

        public NotificationController(INotificationRepository notificationRepo)
        {
            _notificationRepo = notificationRepo;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserID")?.Value;
            return int.TryParse(userIdClaim, out int id) ? id : 0;
        }

        private int GetCurrentDivisionId()
        {
            var divClaim = User.Claims.FirstOrDefault(c => c.Type == "DivisionID")?.Value;
            return int.TryParse(divClaim, out int id) ? id : 0;
        }

        [HttpGet]
        public IActionResult GetUnread()
        {
            var unread = _notificationRepo.GetUnreadNotifications(GetCurrentUserId(), GetCurrentDivisionId());
            return Json(new { count = unread.Count(), notifications = unread.Take(5) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsRead(int id)
        {
            _notificationRepo.MarkAsRead(id);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllAsRead()
        {
            _notificationRepo.MarkAllAsRead(GetCurrentUserId(), GetCurrentDivisionId());
            return RedirectToAction("Index");
        }

        public IActionResult Index()
        {
            var notifications = _notificationRepo.GetAllNotifications(GetCurrentUserId(), GetCurrentDivisionId());
            return View(notifications);
        }
    }
}
