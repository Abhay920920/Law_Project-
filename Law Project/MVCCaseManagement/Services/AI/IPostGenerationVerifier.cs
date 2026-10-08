using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IPostGenerationVerifier
    {
        Task<VerificationResult> VerifyAndCleanseAsync(
            string draftAnswer, 
            EvidencePack evidencePack, 
            CancellationToken cancellationToken = default);
    }
}
