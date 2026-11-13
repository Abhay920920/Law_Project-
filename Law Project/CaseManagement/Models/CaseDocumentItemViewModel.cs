using System;

namespace MVCCaseManagement.Models
{
    /// <summary>
    /// Unified representation of a case document (tribunal award, appeal order, e-Courts order, user upload)
    /// for robust retrieval, storage vault verification, modal preview, and downloading.
    /// </summary>
    public class CaseDocumentItemViewModel
    {
        public int? DocumentId { get; set; }
        public string DocKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "Case Document";
        public string RawPath { get; set; } = string.Empty;
        public string NormalizedUrl { get; set; } = string.Empty;
        public string Path => NormalizedUrl;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = ".pdf";
        public DateTime? Date { get; set; }
        public string? Remarks { get; set; }
        public string? UploadedBy { get; set; }
        public string Icon { get; set; } = "bi-file-earmark-pdf-fill";
        public string Color { get; set; } = "danger";
        public bool IsAvailableOnDisk { get; set; } = false;
        public bool IsFromECourts { get; set; } = false;
        public string? CnrNumber { get; set; }
        public string? OrderNo { get; set; }
        public DateTime? OrderDate { get; set; }
        public bool IsEditable { get; set; } = true;
    }

    /// <summary>
    /// Database entity model for user-uploaded supplementary documents in MVC cases.
    /// </summary>
    public class MvcCaseDocument
    {
        public int DocumentID { get; set; }
        public int CaseID { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; } = DateTime.Now;
        public int? UploadedBy { get; set; }
        public string? Remarks { get; set; }
    }
}
