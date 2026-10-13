using System.Collections.Generic;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IConflictDetectorService
    {
        /// <summary>
        /// Detects conflicts and discrepancies across internal records, e-Courts / NAPIX data, and documents.
        /// </summary>
        List<DetectedConflictDto> DetectConflicts(CaseDossier dossier);
    }
}
