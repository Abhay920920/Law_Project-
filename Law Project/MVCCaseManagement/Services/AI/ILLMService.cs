using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface ILLMService
    {
        Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default);

        Task<AIHealthReportDto> CheckHealthAsync(CancellationToken cancellationToken = default);

        AIOptions GetCurrentOptions();
    }
}
