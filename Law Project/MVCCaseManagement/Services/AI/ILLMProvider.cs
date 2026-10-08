using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    /// <summary>
    /// Provider-agnostic abstraction for Large Language Model generation.
    /// Supports local open-weight inference (Ollama) and cloud APIs (OpenRouter, Claude).
    /// </summary>
    public interface ILLMProvider
    {
        string ProviderName { get; }

        Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default);

        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }
}
