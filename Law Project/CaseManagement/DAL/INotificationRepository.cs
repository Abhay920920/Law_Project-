using System.Collections.Generic;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface INotificationRepository
    {
        IEnumerable<Notification> GetUnreadNotifications(int? userId, int? divisionId);
        IEnumerable<Notification> GetAllNotifications(int? userId, int? divisionId);
        void MarkAsRead(int notificationId);
        void MarkAllAsRead(int? userId, int? divisionId);
        void AddNotification(Notification notification);
        void GenerateDailyAlerts();
    }
}
