using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class ConflictDetectorService : IConflictDetectorService
    {
        private readonly ILogger<ConflictDetectorService> _logger;

        public ConflictDetectorService(ILogger<ConflictDetectorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<DetectedConflictDto> DetectConflicts(CaseDossier dossier)
        {
            var conflicts = new List<DetectedConflictDto>();
            if (dossier == null) return conflicts;

            var ecourts = dossier.ECourtsSummary;
            if (ecourts == null || !ecourts.IsVerified) return conflicts;

            // 1. Conflict: Case Status (Pending vs Disposed)
            string internalStage = (dossier.CurrentStage ?? "").Trim();
            string internalDisposal = dossier.StructuredFacts.TryGetValue("Disposal Status", out var disp) ? disp.Trim() : "";
            string ecourtsStage = (ecourts.CurrentStage ?? "").Trim();

            bool internalIsPending = internalStage.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
                                     internalDisposal.Contains("Pending", StringComparison.OrdinalIgnoreCase);

            bool internalIsDisposed = internalStage.Contains("Disposed", StringComparison.OrdinalIgnoreCase) ||
                                      internalStage.Contains("Decided", StringComparison.OrdinalIgnoreCase) ||
                                      internalDisposal.Contains("Disposed", StringComparison.OrdinalIgnoreCase) ||
                                      internalDisposal.Contains("Decided", StringComparison.OrdinalIgnoreCase);

            bool ecourtsIsDisposed = ecourtsStage.Contains("Disposed", StringComparison.OrdinalIgnoreCase) ||
                                     ecourtsStage.Contains("Decided", StringComparison.OrdinalIgnoreCase) ||
                                     ecourtsStage.Contains("Dismissed", StringComparison.OrdinalIgnoreCase) ||
                                     ecourtsStage.Contains("Decreed", StringComparison.OrdinalIgnoreCase) ||
                                     ecourtsStage.Contains("Allowed", StringComparison.OrdinalIgnoreCase) ||
                                     ecourts.Orders.Any(o => o.IsJudgment);

            bool ecourtsIsPending = ecourtsStage.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
                                    ecourtsStage.Contains("Arguments", StringComparison.OrdinalIgnoreCase) ||
                                    ecourtsStage.Contains("Evidence", StringComparison.OrdinalIgnoreCase) ||
                                    ecourtsStage.Contains("Hearing", StringComparison.OrdinalIgnoreCase) ||
                                    ecourtsStage.Contains("Appearance", StringComparison.OrdinalIgnoreCase) ||
                                    ecourtsStage.Contains("Notice", StringComparison.OrdinalIgnoreCase);

            if (internalIsPending && ecourtsIsDisposed)
            {
                conflicts.Add(new DetectedConflictDto
                {
                    FieldName = "Case Status",
                    SourceA = "Internal Database Record",
                    ValueA = internalStage.Length > 0 ? internalStage : "Pending",
                    SourceB = "National e-Courts Record",
                    ValueB = ecourtsStage.Length > 0 ? ecourtsStage : "Disposed",
                    Severity = "High",
                    Description = "There is a discrepancy between the internal record and the e-Courts record. The internal system records the case as pending, while the latest e-Courts result indicates disposal. This should be verified before relying on the status.",
                    RecommendedAction = "Verify whether a certified copy of the final judgment/award has been received from panel counsel and update internal case register."
                });
            }
            else if (internalIsDisposed && ecourtsIsPending && !ecourtsIsDisposed)
            {
                conflicts.Add(new DetectedConflictDto
                {
                    FieldName = "Case Status",
                    SourceA = "Internal Database Record",
                    ValueA = internalStage.Length > 0 ? internalStage : "Disposed",
                    SourceB = "National e-Courts Record",
                    ValueB = ecourtsStage.Length > 0 ? ecourtsStage : "Pending / Active",
                    Severity = "High",
                    Description = "There is a discrepancy between the internal record and the e-Courts record. The internal system marks the case as disposed, but the e-Courts record indicates ongoing proceedings.",
                    RecommendedAction = "Verify if proceedings are execution petitions (EP) or an appeal/restoration filed by the claimant."
                });
            }

            // 2. Conflict: Next Hearing Date
            if (dossier.NextHearingDate.HasValue && ecourts.NextHearingDate.HasValue)
            {
                var intDate = dossier.NextHearingDate.Value.Date;
                var ecDate = ecourts.NextHearingDate.Value.Date;

                if (intDate != ecDate)
                {
                    conflicts.Add(new DetectedConflictDto
                    {
                        FieldName = "Next Hearing Date",
                        SourceA = "Internal Diary",
                        ValueA = intDate.ToString("dd-MM-yyyy"),
                        SourceB = "National e-Courts Cause List",
                        ValueB = ecDate.ToString("dd-MM-yyyy"),
                        Severity = "Medium",
                        SourceAAuthority = "NWKRTC Internal Case Register",
                        SourceBAuthority = "National e-Courts Services (NAPIX)",
                        TimestampA = intDate,
                        TimestampB = ecDate,
                        Description = $"Hearing date mismatch: Internal diary has {intDate:dd-MM-yyyy}, while e-Courts lists the next date as {ecDate:dd-MM-yyyy}.",
                        RecommendedAction = "Advise Law Officer to cross-check with daily cause list and panel counsel."
                    });
                }
            }

            // 3. Conflict: Court Forum / Hall
            if (!string.IsNullOrWhiteSpace(dossier.CourtName) && !string.IsNullOrWhiteSpace(ecourts.CourtName))
            {
                string c1 = dossier.CourtName.Trim().ToLowerInvariant();
                string c2 = ecourts.CourtName.Trim().ToLowerInvariant();
                if (!c1.Contains(c2) && !c2.Contains(c1) && c1.Length > 5 && c2.Length > 5)
                {
                    conflicts.Add(new DetectedConflictDto
                    {
                        FieldName = "Court Establishment",
                        SourceA = "Internal Case Record",
                        ValueA = dossier.CourtName,
                        SourceB = "e-Courts Registry",
                        ValueB = ecourts.CourtName,
                        Severity = "Low",
                        SourceAAuthority = "NWKRTC Internal Case Record",
                        SourceBAuthority = "National e-Courts Registry",
                        Description = $"Court forum name differs between internal register ('{dossier.CourtName}') and e-Courts ('{ecourts.CourtName}').",
                        RecommendedAction = "Confirm correct court hall/bench location for advocate appearance."
                    });
                }
            }

            // 4. Conflict: Presiding Judge / Coram
            if (!string.IsNullOrWhiteSpace(ecourts.JudgeName) && dossier.StructuredFacts.TryGetValue("Judge Name", out var internalJudge) && !string.IsNullOrWhiteSpace(internalJudge))
            {
                string j1 = internalJudge.Trim().ToLowerInvariant();
                string j2 = ecourts.JudgeName.Trim().ToLowerInvariant();
                if (!j1.Contains(j2) && !j2.Contains(j1) && j1.Length > 4 && j2.Length > 4)
                {
                    conflicts.Add(new DetectedConflictDto
                    {
                        FieldName = "Presiding Judge",
                        SourceA = "Internal Register",
                        ValueA = internalJudge,
                        SourceB = "e-Courts Live Order",
                        ValueB = ecourts.JudgeName,
                        Severity = "Medium",
                        SourceAAuthority = "Internal Case File",
                        SourceBAuthority = "e-Courts Roster",
                        Description = $"Presiding Judge differs between internal file ('{internalJudge}') and e-Courts ('{ecourts.JudgeName}').",
                        RecommendedAction = "Note roster transfer or bench reassignment in case diary."
                    });
                }
            }

            // 5. Conflict: Party / Claimant Identity
            if (!string.IsNullOrWhiteSpace(dossier.Petitioner) && !string.IsNullOrWhiteSpace(ecourts.Petitioner))
            {
                string p1 = dossier.Petitioner.Trim().ToLowerInvariant();
                string p2 = ecourts.Petitioner.Trim().ToLowerInvariant();
                if (!p1.Contains(p2) && !p2.Contains(p1) && p1.Length > 5 && p2.Length > 5 && !p1.Contains("claimant"))
                {
                    conflicts.Add(new DetectedConflictDto
                    {
                        FieldName = "Petitioner / Claimant",
                        SourceA = "Internal Petition Copy",
                        ValueA = dossier.Petitioner,
                        SourceB = "e-Courts Registry Listing",
                        ValueB = ecourts.Petitioner,
                        Severity = "High",
                        SourceAAuthority = "Internal Case Dossier",
                        SourceBAuthority = "e-Courts Cause Title",
                        Description = $"Claimant name mismatch: Internal record has '{dossier.Petitioner}', but e-Courts title lists '{ecourts.Petitioner}'.",
                        RecommendedAction = "Check whether impleadment of legal heirs or transposition occurred."
                    });
                }
            }

            // 6. Conflict: Case Registration Identifier
            if (!string.IsNullOrWhiteSpace(dossier.CaseNumber) && !string.IsNullOrWhiteSpace(ecourts.CaseNumber))
            {
                string num1 = Regex.Replace(dossier.CaseNumber, @"\D", "");
                string num2 = Regex.Replace(ecourts.CaseNumber, @"\D", "");
                if (num1.Length > 0 && num2.Length > 0 && num1 != num2)
                {
                    conflicts.Add(new DetectedConflictDto
                    {
                        FieldName = "Case Number",
                        SourceA = "Internal Database",
                        ValueA = dossier.CaseNumber,
                        SourceB = "e-Courts Registration",
                        ValueB = ecourts.CaseNumber,
                        Severity = "Critical",
                        SourceAAuthority = "NWKRTC Central File",
                        SourceBAuthority = "e-Courts Database",
                        Description = $"Case number registration conflict: Internal register records '{dossier.CaseNumber}' but e-Courts lists '{ecourts.CaseNumber}'.",
                        RecommendedAction = "Immediate verification required to ensure counsel is appearing in the correct court proceedings."
                    });
                }
            }

            if (conflicts.Count > 0)
            {
                _logger.LogWarning("Detected {Count} conflicts for case {CaseNumber}", conflicts.Count, dossier.CaseNumber);
            }

            return conflicts;
        }
    }
}
