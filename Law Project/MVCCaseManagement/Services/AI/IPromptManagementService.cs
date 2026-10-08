using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IPromptManagementService
    {
        /// <summary>
        /// Gets the immutable, enterprise legal system prompt with anti-injection and hallucination boundaries.
        /// </summary>
        string GetSystemPrompt();

        /// <summary>
        /// Formats structured case dossier into hardened XML-tagged evidence context.
        /// </summary>
        string BuildDossierContextXml(CaseDossier dossier);

        /// <summary>
        /// Builds user prompt combining dossier context, user question, and optional quick-action directives.
        /// </summary>
        string BuildUserPrompt(CaseDossier? dossier, string userQuestion, string? quickAction = null);

        /// <summary>
        /// Resolves quick action title into user instruction directive.
        /// </summary>
        string ResolveQuickActionDirective(string quickAction);
    }
}
