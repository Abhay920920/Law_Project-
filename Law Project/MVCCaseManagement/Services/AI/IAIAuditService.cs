using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IAIAuditService
    {
        Task LogAuditAsync(AIAuditLog auditLog, CancellationToken cancellationToken = default);

        Task<int> CreateConversationAsync(int userId, string? caseType, int? caseId, string title, CancellationToken cancellationToken = default);

        Task<List<AIConversation>> GetConversationsByUserAsync(int userId, int top = 20, CancellationToken cancellationToken = default);

        Task<AIConversation?> GetConversationByIdAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

        Task<int> SaveMessageAsync(int conversationId, string role, string messageText, string? model = null, CancellationToken cancellationToken = default);

        Task<List<AIMessage>> GetMessagesByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default);

        Task<bool> DeleteConversationAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

        Task<bool> UpdateConversationTitleAsync(int conversationId, int userId, string newTitle, CancellationToken cancellationToken = default);

        Task<bool> ClearAllConversationsAsync(int userId, CancellationToken cancellationToken = default);
    }
}
