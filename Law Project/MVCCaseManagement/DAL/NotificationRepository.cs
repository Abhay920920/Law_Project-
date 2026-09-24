using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace MVCCaseManagement.DAL
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly string _connectionString;

        public NotificationRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MVCCaseDB") ?? "";
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public void AddNotification(Notification notification)
        {
            using (var db = CreateConnection())
            {
                var query = @"
                    INSERT INTO SYSTEM_NOTIFICATIONS 
                    (UserID, DivisionID, Title, Message, RelatedCaseType, RelatedCaseID, LinkUrl, IsRead, CreatedDate) 
                    VALUES 
                    (@UserID, @DivisionID, @Title, @Message, @RelatedCaseType, @RelatedCaseID, @LinkUrl, @IsRead, @CreatedDate)";
                db.Execute(query, notification);
            }
        }

        public void GenerateDailyAlerts()
        {
            using (var db = CreateConnection())
            {
                // MVC Upcoming Hearings
                var queryMvc = @"
                    INSERT INTO SYSTEM_NOTIFICATIONS (DivisionID, Title, Message, RelatedCaseType, RelatedCaseID, LinkUrl, IsRead, CreatedDate)
                    SELECT DivisionID, 'Upcoming Hearing', 'Hearing for MVC Case ' + MVCNo + ' is on ' + CONVERT(VARCHAR, NextHearingDate, 106), 
                           'MVC', CaseID, '/Case/Details/' + CAST(CaseID AS VARCHAR), 0, GETDATE()
                    FROM MVC_CASES 
                    WHERE NextHearingDate = CAST(DATEADD(day, 3, GETDATE()) AS DATE)
                      AND NOT EXISTS (
                          SELECT 1 FROM SYSTEM_NOTIFICATIONS 
                          WHERE RelatedCaseID = MVC_CASES.CaseID 
                            AND RelatedCaseType = 'MVC' 
                            AND Title = 'Upcoming Hearing' 
                            AND CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
                      )";
                db.Execute(queryMvc);

                // Gratuity Compliance Due
                var queryGra = @"
                    INSERT INTO SYSTEM_NOTIFICATIONS (DivisionID, Title, Message, RelatedCaseType, RelatedCaseID, LinkUrl, IsRead, CreatedDate)
                    SELECT DivisionCode, 'Compliance Due', 'Compliance for Gratuity Case ' + PGANumber + ' is due soon.', 
                           'Gratuity', CaseID, '/Gratuity/Details/' + CAST(CaseID AS VARCHAR), 0, GETDATE()
                    FROM GRA_CASES 
                    WHERE StayComplianceDate = CAST(DATEADD(day, 3, GETDATE()) AS DATE)
                      AND NOT EXISTS (
                          SELECT 1 FROM SYSTEM_NOTIFICATIONS 
                          WHERE RelatedCaseID = GRA_CASES.CaseID 
                            AND RelatedCaseType = 'Gratuity' 
                            AND Title = 'Compliance Due' 
                            AND CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
                      )";
                db.Execute(queryGra);
            }
        }

        public IEnumerable<Notification> GetAllNotifications(int? userId, int? divisionId)
        {
            using (var db = CreateConnection())
            {
                var query = "SELECT * FROM SYSTEM_NOTIFICATIONS WHERE 1=1";
                if (userId.HasValue) query += " AND (UserID = @UserId OR UserID IS NULL)";
                if (divisionId.HasValue && divisionId > 0 && divisionId != 5) query += " AND (DivisionID = @DivisionId OR DivisionID IS NULL)";
                
                query += " ORDER BY CreatedDate DESC";
                return db.Query<Notification>(query, new { UserId = userId, DivisionId = divisionId }).ToList();
            }
        }

        public IEnumerable<Notification> GetUnreadNotifications(int? userId, int? divisionId)
        {
            using (var db = CreateConnection())
            {
                var query = "SELECT * FROM SYSTEM_NOTIFICATIONS WHERE IsRead = 0";
                if (userId.HasValue) query += " AND (UserID = @UserId OR UserID IS NULL)";
                if (divisionId.HasValue && divisionId > 0 && divisionId != 5) query += " AND (DivisionID = @DivisionId OR DivisionID IS NULL)";
                
                query += " ORDER BY CreatedDate DESC";
                return db.Query<Notification>(query, new { UserId = userId, DivisionId = divisionId }).ToList();
            }
        }

        public void MarkAllAsRead(int? userId, int? divisionId)
        {
            using (var db = CreateConnection())
            {
                var query = "UPDATE SYSTEM_NOTIFICATIONS SET IsRead = 1 WHERE IsRead = 0";
                if (userId.HasValue) query += " AND (UserID = @UserId OR UserID IS NULL)";
                if (divisionId.HasValue && divisionId > 0 && divisionId != 5) query += " AND (DivisionID = @DivisionId OR DivisionID IS NULL)";
                
                db.Execute(query, new { UserId = userId, DivisionId = divisionId });
            }
        }

        public void MarkAsRead(int notificationId)
        {
            using (var db = CreateConnection())
            {
                var query = "UPDATE SYSTEM_NOTIFICATIONS SET IsRead = 1 WHERE NotificationID = @Id";
                db.Execute(query, new { Id = notificationId });
            }
        }
    }
}
