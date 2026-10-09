using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class AIAuditService : IAIAuditService
    {
        private readonly DBHelper _db;
        private readonly ILogger<AIAuditService> _logger;

        public AIAuditService(DBHelper db, ILogger<AIAuditService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LogAuditAsync(AIAuditLog auditLog, CancellationToken cancellationToken = default)
        {
            if (auditLog == null) return;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    INSERT INTO AI_AUDIT_LOGS (
                        UserID, Role, DivisionID, ConversationID, CaseType, CaseID,
                        Question, RetrievedSources, Model, ExecutionTimeMs, Status, ErrorMessage, CreatedAt
                    ) VALUES (
                        @UserID, @Role, @DivisionID, @ConversationID, @CaseType, @CaseID,
                        @Question, @RetrievedSources, @Model, @ExecutionTimeMs, @Status, @ErrorMessage, GETDATE()
                    )";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserID", auditLog.UserID);
                cmd.Parameters.AddWithValue("@Role", auditLog.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DivisionID", auditLog.DivisionID);
                cmd.Parameters.AddWithValue("@ConversationID", auditLog.ConversationID.HasValue ? auditLog.ConversationID.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CaseType", auditLog.CaseType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CaseID", auditLog.CaseID.HasValue ? auditLog.CaseID.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Question", auditLog.Question ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RetrievedSources", auditLog.RetrievedSources ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Model", auditLog.Model ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ExecutionTimeMs", auditLog.ExecutionTimeMs);
                cmd.Parameters.AddWithValue("@Status", auditLog.Status ?? "SUCCESS");
                cmd.Parameters.AddWithValue("@ErrorMessage", auditLog.ErrorMessage ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Never crash the primary AI call due to audit logging failure
                _logger.LogError(ex, "Failed to write AI audit log for user {UserID}", auditLog.UserID);
            }
        }

        public async Task<int> CreateConversationAsync(int userId, string? caseType, int? caseId, string title, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    INSERT INTO AI_CONVERSATIONS (UserID, CaseType, CaseID, Title, CreatedAt, UpdatedAt, IsActive)
                    OUTPUT INSERTED.ConversationID
                    VALUES (@UserID, @CaseType, @CaseID, @Title, GETDATE(), GETDATE(), 1)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@CaseType", caseType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CaseID", caseId.HasValue ? caseId.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(title) ? "New Legal Research Session" : title.Trim());

                var id = await cmd.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI conversation for user {UserID}", userId);
                return 0;
            }
        }

        public async Task<List<AIConversation>> GetConversationsByUserAsync(int userId, int top = 20, CancellationToken cancellationToken = default)
        {
            var list = new List<AIConversation>();
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT TOP (@Top) ConversationID, UserID, CaseType, CaseID, Title, CreatedAt, UpdatedAt, IsActive
                    FROM AI_CONVERSATIONS
                    WHERE UserID = @UserID AND IsActive = 1
                    ORDER BY UpdatedAt DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Top", Math.Min(50, Math.Max(1, top)));

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(new AIConversation
                    {
                        ConversationID = reader.GetInt32(reader.GetOrdinal("ConversationID")),
                        UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                        CaseType = reader.IsDBNull(reader.GetOrdinal("CaseType")) ? null : reader.GetString(reader.GetOrdinal("CaseType")),
                        CaseID = reader.IsDBNull(reader.GetOrdinal("CaseID")) ? null : reader.GetInt32(reader.GetOrdinal("CaseID")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading AI conversations for user {UserID}", userId);
            }

            return list;
        }

        public async Task<AIConversation?> GetConversationByIdAsync(int conversationId, int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT TOP 1 ConversationID, UserID, CaseType, CaseID, Title, CreatedAt, UpdatedAt, IsActive
                    FROM AI_CONVERSATIONS
                    WHERE ConversationID = @ConversationID AND UserID = @UserID AND IsActive = 1";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ConversationID", conversationId);
                cmd.Parameters.AddWithValue("@UserID", userId);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return new AIConversation
                    {
                        ConversationID = reader.GetInt32(reader.GetOrdinal("ConversationID")),
                        UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                        CaseType = reader.IsDBNull(reader.GetOrdinal("CaseType")) ? null : reader.GetString(reader.GetOrdinal("CaseType")),
                        CaseID = reader.IsDBNull(reader.GetOrdinal("CaseID")) ? null : reader.GetInt32(reader.GetOrdinal("CaseID")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI conversation {ConversationID} for user {UserID}", conversationId, userId);
            }

            return null;
        }

        public async Task<int> SaveMessageAsync(int conversationId, string role, string messageText, string? model = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    INSERT INTO AI_MESSAGES (ConversationID, Role, MessageText, Model, CreatedAt)
                    OUTPUT INSERTED.MessageID
                    VALUES (@ConversationID, @Role, @MessageText, @Model, GETDATE());

                    UPDATE AI_CONVERSATIONS SET UpdatedAt = GETDATE() WHERE ConversationID = @ConversationID;";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ConversationID", conversationId);
                cmd.Parameters.AddWithValue("@Role", role);
                cmd.Parameters.AddWithValue("@MessageText", messageText);
                cmd.Parameters.AddWithValue("@Model", model ?? (object)DBNull.Value);

                var id = await cmd.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving AI message for conversation {ConversationID}", conversationId);
                return 0;
            }
        }

        public async Task<List<AIMessage>> GetMessagesByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default)
        {
            var list = new List<AIMessage>();
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT MessageID, ConversationID, Role, MessageText, Model, CreatedAt
                    FROM AI_MESSAGES
                    WHERE ConversationID = @ConversationID
                    ORDER BY MessageID ASC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ConversationID", conversationId);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(new AIMessage
                    {
                        MessageID = reader.GetInt32(reader.GetOrdinal("MessageID")),
                        ConversationID = reader.GetInt32(reader.GetOrdinal("ConversationID")),
                        Role = reader.GetString(reader.GetOrdinal("Role")),
                        MessageText = reader.GetString(reader.GetOrdinal("MessageText")),
                        Model = reader.IsDBNull(reader.GetOrdinal("Model")) ? null : reader.GetString(reader.GetOrdinal("Model")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI messages for conversation {ConversationID}", conversationId);
            }

            return list;
        }

        public async Task<bool> DeleteConversationAsync(int conversationId, int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    UPDATE AI_CONVERSATIONS 
                    SET IsActive = 0, UpdatedAt = GETDATE()
                    WHERE ConversationID = @ConversationID AND UserID = @UserID";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ConversationID", conversationId);
                cmd.Parameters.AddWithValue("@UserID", userId);

                int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AI conversation {ConversationID} for user {UserID}", conversationId, userId);
                return false;
            }
        }

        public async Task<bool> UpdateConversationTitleAsync(int conversationId, int userId, string newTitle, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(newTitle)) return false;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    UPDATE AI_CONVERSATIONS 
                    SET Title = @Title, UpdatedAt = GETDATE()
                    WHERE ConversationID = @ConversationID AND UserID = @UserID AND IsActive = 1";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ConversationID", conversationId);
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Title", newTitle.Trim().Length > 100 ? newTitle.Trim().Substring(0, 100) : newTitle.Trim());

                int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AI conversation title for {ConversationID}", conversationId);
                return false;
            }
        }

        public async Task<bool> ClearAllConversationsAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    UPDATE AI_CONVERSATIONS 
                    SET IsActive = 0, UpdatedAt = GETDATE()
                    WHERE UserID = @UserID AND IsActive = 1";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserID", userId);

                int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows >= 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing AI conversations for user {UserID}", userId);
                return false;
            }
        }
    }
}
