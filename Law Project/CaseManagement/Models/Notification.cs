using System;

namespace MVCCaseManagement.Models
{
    public class Notification
    {
        public int NotificationID { get; set; }
        public int? UserID { get; set; } // Null implies global/role based, but let's stick to specific or 0 for admin
        public int? DivisionID { get; set; } // If we want to target a division
        public string Title { get; set; }
        public string Message { get; set; }
        public string RelatedCaseType { get; set; } // MVC, Gratuity, Labour
        public int? RelatedCaseID { get; set; }
        public string LinkUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
