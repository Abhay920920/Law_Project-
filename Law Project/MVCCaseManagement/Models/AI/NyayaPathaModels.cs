using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models.AI
{
    public class AnthropicOptions
    {
        public const string SectionName = "Anthropic";

        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        public int MaxTokens { get; set; } = 4096;
        public double Temperature { get; set; } = 0.2;
        public int TimeoutSeconds { get; set; } = 90;
    }

    public class NyayaPathaOptions
    {
        public const string SectionName = "NyayaPathaAI";

        public bool IsEnabled { get; set; } = true;
        public int MaxContextTokens { get; set; } = 100000;
        public bool EnableDocumentExtraction { get; set; } = true;
        public int MaxPagesPerDocument { get; set; } = 15;
        public bool EnableLegalSearch { get; set; } = true;
        public bool EnableECourtsIntegration { get; set; } = true;
        public bool EnableWebSearch { get; set; } = true;
        public string WebSearchProvider { get; set; } = "Configured";
        public int WebSearchTimeoutSeconds { get; set; } = 15;
        public int ECourtsTimeoutSeconds { get; set; } = 15;
    }

    public class AIOptions
    {
        public const string SectionName = "AI";

        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = "Ollama"; // "Ollama" local 0-cost engine
        public string Model { get; set; } = "qwen2.5:7b";
        public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
        public int TimeoutSeconds { get; set; } = 240;
        public bool AllowExternalProviders { get; set; } = false;
        public string? FallbackProvider { get; set; } = string.Empty;
        public string? OpenRouterApiKey { get; set; } = string.Empty;
        public string? OpenRouterBaseUrl { get; set; } = string.Empty;
        public string? ClaudeApiKey { get; set; } = string.Empty;
        public int MaxConcurrentRequests { get; set; } = 5;
        public int MaxInputLength { get; set; } = 4000;
    }

    public class LLMRequest
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public List<AIMessage> Messages { get; set; } = new();
        public double Temperature { get; set; } = 0.2;
        public int MaxTokens { get; set; } = 4096;
        public string? ModelOverride { get; set; }
    }

    public class LLMResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TotalTokens { get; set; }
        public string? ErrorMessage { get; set; }
        public int ExecutionTimeMs { get; set; }
        public bool IsFallback { get; set; }
    }

    public class AIHealthReportDto
    {
        public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Unhealthy, Disabled
        public bool IsEnabled { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ConfiguredModel { get; set; } = string.Empty;
        public bool IsProviderReachable { get; set; }
        public bool IsDatabaseConnected { get; set; }
        public bool IsECourtsConfigured { get; set; }
        public bool AllowExternalProviders { get; set; }
        public int ActiveConcurrentRequests { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AIConversation
    {
        public int ConversationID { get; set; }
        public int UserID { get; set; }
        public string? CaseType { get; set; } // MVC, LABOUR, APPEAL, GRATUITY, OTHERCOURTS
        public int? CaseID { get; set; }
        public string Title { get; set; } = "New Case Consultation";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }

    public class AIMessage
    {
        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public string Role { get; set; } = "user"; // user, assistant, system
        public string MessageText { get; set; } = string.Empty;
        public string? Model { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class AIAuditLog
    {
        public int AuditID { get; set; }
        public int UserID { get; set; }
        public string? Role { get; set; }
        public int DivisionID { get; set; }
        public int? ConversationID { get; set; }
        public string? CaseType { get; set; }
        public int? CaseID { get; set; }
        public string? Question { get; set; }
        public string? RetrievedSources { get; set; }
        public string? Model { get; set; }
        public int ExecutionTimeMs { get; set; }
        public string Status { get; set; } = "SUCCESS";
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CitationDto
    {
        public string SourceType { get; set; } = "CaseRecord"; // CaseRecord, CourtOrder, Document, LegalPrecedent, CaseNoting, StatutoryProvision, WebPrecedent
        public string SourceCategory { get; set; } = "Internal"; // Internal, e-Courts, Web
        public string Title { get; set; } = string.Empty;
        public string? CaseNumber { get; set; }
        public string? Court { get; set; }
        public string? Date { get; set; }
        public int? PageNumber { get; set; }
        public string? Section { get; set; }
        public string? RecordId { get; set; }
        public string? DocumentUrl { get; set; }
        public string? WebUrl { get; set; }
        public string? Domain { get; set; }
        public bool IsVerified { get; set; } = true;
        public string Excerpt { get; set; } = string.Empty;
    }

    public class CaseNotingDto
    {
        public int NotingId { get; set; }
        public string NotingText { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorRole { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class ECourtsHistoryItemDto
    {
        public DateTime EventDate { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string? CourtHall { get; set; }
        public string? Judge { get; set; }
        public string OrderDetails { get; set; } = string.Empty;
    }

    public class ECourtsOrderDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime? OrderDate { get; set; }
        public string OrderType { get; set; } = "Daily Order";
        public string? Judge { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? OrderPdfUrl { get; set; }
        public bool IsJudgment { get; set; }
    }

    public class ECourtsCaseSummaryDto
    {
        public string? CNRNumber { get; set; }
        public string? CaseNumber { get; set; }
        public string? CourtName { get; set; }
        public string? CurrentStage { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? JudgeName { get; set; }
        public string? Petitioner { get; set; }
        public string? Respondent { get; set; }
        public DateTime? FilingDate { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public bool IsVerified { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public List<ECourtsOrderDto> Orders { get; set; } = new();
        public List<ECourtsHistoryItemDto> History { get; set; } = new();
    }

    public class ExternalLegalSourceDto
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string SourceDomain { get; set; } = string.Empty;
        public string Court { get; set; } = string.Empty;
        public string? CaseNumber { get; set; }
        public string? Parties { get; set; }
        public DateTime? JudgmentDate { get; set; }
        public DateTime RetrievalDate { get; set; } = DateTime.Now;
        public string Excerpt { get; set; } = string.Empty;
        public string SourceType { get; set; } = "HighCourt"; // SupremeCourt, HighCourt, eCourtsGov, Statute, LegalPortal
        public bool IsVerifiedDomain { get; set; } = true;
    }

    public class LegalSearchFilter
    {
        public string? Court { get; set; }
        public int? MinYear { get; set; }
        public string? Bench { get; set; }
        public bool AuthoritativeOnly { get; set; } = true;
    }

    public class UnifiedSimilarCaseDto
    {
        public int? CaseId { get; set; }
        public string SourceType { get; set; } = "NWKRTC_MVC"; // NWKRTC_MVC, NWKRTC_Labour, NWKRTC_Appeal, JudgementRepo, eCourts, WebPrecedent
        public string CaseNumber { get; set; } = string.Empty;
        public string Court { get; set; } = string.Empty;
        public string FactsSummary { get; set; } = string.Empty;
        public string LegalIssues { get; set; } = string.Empty;
        public string OutcomeOrStage { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public string SimilarityBasis { get; set; } = string.Empty;
        public string? Citation { get; set; }
        public string? Url { get; set; }
    }

    public class UnifiedSearchRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public string? CaseType { get; set; }
        public int? CaseId { get; set; }
        public int MaxResults { get; set; } = 5;
        public bool IncludeWeb { get; set; } = true;
        public bool IncludeECourts { get; set; } = true;
    }

    public class UnifiedSearchResponseDto
    {
        public string Query { get; set; } = string.Empty;
        public List<UnifiedSimilarCaseDto> SimilarCases { get; set; } = new();
        public List<ExternalLegalSourceDto> WebSources { get; set; } = new();
        public List<RelevantJudgmentDto> Judgments { get; set; } = new();
        public ECourtsCaseSummaryDto? ECourtsSummary { get; set; }
    }

    public class DocumentExtractDto
    {
        public string DocumentName { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public int PageCount { get; set; }
        public string ExtractedText { get; set; } = string.Empty;
    }

    public class RelevantJudgmentDto
    {
        public int JudgementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Court { get; set; } = string.Empty;
        public DateTime? JudgementDate { get; set; }
        public string KeyPrinciple { get; set; } = string.Empty;
        public string Citation { get; set; } = string.Empty;
    }

    public class SimilarCaseMatch
    {
        public int CaseId { get; set; }
        public string CaseType { get; set; } = string.Empty;
        public string CaseNumber { get; set; } = string.Empty;
        public string CourtOrTribunal { get; set; } = string.Empty;
        public string SimilarityBasis { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public class CaseSearchItemDto
    {
        public int CaseId { get; set; }
        public string CaseType { get; set; } = string.Empty;
        public string CaseNumber { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class CaseDossier
    {
        public int CaseId { get; set; }
        public string CaseType { get; set; } = "MVC";
        public string CaseNumber { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;
        public DateTime? NextHearingDate { get; set; }
        public string Petitioner { get; set; } = string.Empty;
        public string Respondent { get; set; } = string.Empty;
        public string? VehicleNo { get; set; }
        public string? CNRNumber { get; set; }

        public Dictionary<string, string> StructuredFacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CaseNotingDto> Notings { get; set; } = new();
        public List<ECourtsHistoryItemDto> ECourtsHistory { get; set; } = new();
        public ECourtsCaseSummaryDto? ECourtsSummary { get; set; }
        public List<DocumentExtractDto> ExtractedDocuments { get; set; } = new();
        public List<DocumentSearchResultDto> SearchedDocumentPassages { get; set; } = new();
        public List<RelevantJudgmentDto> RelevantJudgments { get; set; } = new();
        public List<SimilarCaseMatch> SimilarCases { get; set; } = new();
        public List<UnifiedSimilarCaseDto> UnifiedSimilarCases { get; set; } = new();
        public List<ExternalLegalSourceDto> ExternalLegalSources { get; set; } = new();
        public List<string> ApplicableProvisions { get; set; } = new();
        public List<DetectedConflictDto> DetectedConflicts { get; set; } = new();
    }

    public static class QuerySourceCategory
    {
        public const string InternalDatabase = "INTERNAL_DATABASE";
        public const string InternalDocuments = "INTERNAL_DOCUMENTS";
        public const string ECourts = "ECOURTS";
        public const string LegalWeb = "LEGAL_WEB";
        public const string Judgments = "JUDGMENTS";
        public const string SimilarCases = "SIMILAR_CASES";
        public const string MultiSource = "MULTI_SOURCE";
    }

    public enum LegalQueryIntent
    {
        GeneralInquiry = 0,
        CaseLookup,
        CaseDeepDive,
        VehicleHistory,
        AdvocatePortfolio,
        HearingCalendar,
        FinancialRisk,
        ExecutionRisk,
        DivisionalStatistics,
        CompoundFilter,
        DocumentRAG,
        LegalPrecedent
    }

    public class LegalQueryPlan
    {
        public LegalQueryIntent Intent { get; set; } = LegalQueryIntent.GeneralInquiry;
        public string IntentName => Intent.ToString();

        // Extracted Entities
        public string? CaseNumber { get; set; }
        public string? CaseType { get; set; }
        public int? CaseYear { get; set; }
        public string? CNRNumber { get; set; }
        public string? VehicleNumber { get; set; }
        public string? AdvocateName { get; set; }
        public int? DivisionId { get; set; }
        public string? DivisionName { get; set; }
        public string? DivisionCode { get; set; }
        public DateTime? DateRangeStart { get; set; }
        public DateTime? DateRangeEnd { get; set; }
        public string? DateRangeLabel { get; set; } // "today", "tomorrow", "this_week", "next_week", "this_month", "specific_date"
        public decimal? AmountThreshold { get; set; }
        public string? AmountComparator { get; set; } = "greater_than"; // "greater_than", "less_than"
        public string? StatusFilter { get; set; } = "Pending"; // "Pending", "Disposed", "All"
        public List<string> StatutoryProvisions { get; set; } = new();

        // Source Requirements
        public bool IsDeterministicDatabaseQuery { get; set; }
        public bool RequiresDocuments { get; set; }
        public bool RequiresECourts { get; set; }
        public bool RequiresLegalPrecedents { get; set; }
        public bool RequiresWebSearch { get; set; }

        public string RawQuery { get; set; } = string.Empty;
        public string PlanSummary { get; set; } = string.Empty;
    }

    public class QueryRouteResult
    {
        public string PrimaryCategory { get; set; } = QuerySourceCategory.MultiSource;
        public List<string> RequiredSources { get; set; } = new();
        public string? ExtractedCaseType { get; set; }
        public int? ExtractedCaseId { get; set; }
        public string? ExtractedCaseNumber { get; set; }
        public int? ExtractedYear { get; set; }
        public string? ExtractedCNR { get; set; }
        public string? ExtractedVehicleNo { get; set; }
        public string? ExtractedAdvocateName { get; set; }
        public List<string> ExtractedProvisions { get; set; } = new();
        public string CleanedKeywords { get; set; } = string.Empty;
        public bool IsFollowUp { get; set; }
        public string Reasoning { get; set; } = string.Empty;

        public LegalQueryPlan Plan { get; set; } = new();
    }

    public class DocumentSearchResultDto
    {
        public string DocumentName { get; set; } = string.Empty;
        public string? CaseType { get; set; }
        public int? CaseId { get; set; }
        public string? CaseNumber { get; set; }
        public int PageNumber { get; set; } = 1;
        public string? Section { get; set; } = "Content";
        public string MatchedSnippet { get; set; } = string.Empty;
        public string? SecureViewerUrl { get; set; }
        public double RelevanceScore { get; set; } = 1.0;
        public bool IsVerified { get; set; } = true;
    }

    public class DocumentPageExtractDto
    {
        public string DocumentName { get; set; } = string.Empty;
        public string? CaseType { get; set; }
        public int? CaseId { get; set; }
        public int PageNumber { get; set; }
        public string Section { get; set; } = "General";
        public string Text { get; set; } = string.Empty;
    }

    public class DetectedConflictDto
    {
        public string FieldName { get; set; } = string.Empty;
        public string SourceA { get; set; } = string.Empty;
        public string ValueA { get; set; } = string.Empty;
        public string SourceB { get; set; } = string.Empty;
        public string ValueB { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium"; // High, Medium, Low
        public string? RecommendedAction { get; set; }
    }

    public class NyayaPathaChatRequest
    {
        [Required]
        public string Message { get; set; } = string.Empty;
        public int? ConversationId { get; set; }
        public string? CaseType { get; set; } // MVC, LABOUR, APPEAL, GRATUITY, OTHERCOURTS
        public int? CaseId { get; set; }
        public string? QuickAction { get; set; } // complete_analysis, strengths, weaknesses, missing_evidence, contradictions, opposing_arguments, hearing_preparation, similar_cases, relevant_law
        public bool IncludeWeb { get; set; } = false;
    }

    public class NyayaPathaChatResponse
    {
        public bool Success { get; set; } = true;
        public int ConversationId { get; set; }
        public string Reply { get; set; } = string.Empty;
        public List<CitationDto> Citations { get; set; } = new();
        public List<string> SourcesSearched { get; set; } = new();
        public List<DetectedConflictDto> Conflicts { get; set; } = new();
        public string? RouteCategory { get; set; }
        public string? FollowUpContext { get; set; }
        public string? DossierSummary { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Model { get; set; }
        public int ExecutionTimeMs { get; set; }
    }
}

