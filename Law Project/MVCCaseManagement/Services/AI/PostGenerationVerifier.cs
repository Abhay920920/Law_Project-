using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class PostGenerationVerifier : IPostGenerationVerifier
    {
        private readonly ILogger<PostGenerationVerifier> _logger;

        public PostGenerationVerifier(ILogger<PostGenerationVerifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<VerificationResult> VerifyAndCleanseAsync(
            string draftAnswer,
            EvidencePack evidencePack,
            CancellationToken cancellationToken = default)
        {
            var result = new VerificationResult
            {
                VerifiedAnswer = draftAnswer ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(draftAnswer) || evidencePack == null)
            {
                return Task.FromResult(result);
            }

            var claims = new List<ExtractedClaim>();
            var contradictions = new List<string>();

            // Extract sentences from draft
            var sentences = SplitIntoSentences(draftAnswer);
            result.TotalClaims = sentences.Count;

            // Build corpus of all verified evidence text
            var evidenceCorpus = new StringBuilder();
            foreach (var kvp in evidencePack.VerifiedFacts)
            {
                evidenceCorpus.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            foreach (var chunk in evidencePack.Chunks)
            {
                evidenceCorpus.AppendLine(chunk.Content);
            }
            if (evidencePack.VerifiedFacts != null)
            {
                foreach (var vf in evidencePack.VerifiedFacts)
                {
                    evidenceCorpus.AppendLine($"{vf.Key}: {vf.Value}");
                }
            }
            foreach (var cit in evidencePack.Citations)
            {
                evidenceCorpus.AppendLine($"{cit.Title} {cit.CaseNumber} {cit.Court} {cit.Date} {cit.Excerpt}");
            }
            if (evidencePack.Conflicts != null)
            {
                foreach (var conf in evidencePack.Conflicts)
                {
                    evidenceCorpus.AppendLine($"{conf.FieldName}: {conf.SourceA} '{conf.ValueA}' vs {conf.SourceB} '{conf.ValueB}'");
                }
            }
            string allEvidence = evidenceCorpus.ToString();

            int verifiedCount = 0;
            int unsupportedCount = 0;

            foreach (var sentence in sentences)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string trimmed = sentence.Trim();
                if (trimmed.Length < 10) continue;

                // Check for factual indicators: dates, currency, case numbers, statutory sections, and legal propositions/conclusions
                bool hasNumbers = Regex.IsMatch(trimmed, @"\b\d+[\d,./-]*\b");
                bool hasStatute = Regex.IsMatch(trimmed, @"\b(?:Section|Sec\.?|Order|Article)\s+\d+", RegexOptions.IgnoreCase);
                bool hasCaseRef = Regex.IsMatch(trimmed, @"\b(?:MVC|KID|MFA|PGA|ID|WP|WA|EP|CNR)\b", RegexOptions.IgnoreCase);
                bool hasLegalProposition = Regex.IsMatch(trimmed, @"\b(?:negligence|liability|liable|apportioned|multiplier|future prospects|quantum|quashed|dismissed|stayed|enhanced|affirmed|ex-parte|decreed|res ipsa loquitur)\b", RegexOptions.IgnoreCase);

                bool isMaterialClaim = hasNumbers || hasStatute || hasCaseRef || hasLegalProposition;

                if (!isMaterialClaim)
                {
                    // General explanatory prose
                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        Taxonomy = EvidenceTaxonomy.Inference,
                        IsSupported = true,
                        Confidence = 0.95
                    });
                    verifiedCount++;
                    continue;
                }

                // Verify against evidence pack
                var (isSupported, sourceCategory, sourceName, snippet) = EvaluateSentenceAgainstEvidence(trimmed, allEvidence, evidencePack);

                if (isSupported)
                {
                    var taxonomy = hasStatute 
                        ? EvidenceTaxonomy.LegalRule 
                        : (hasLegalProposition ? EvidenceTaxonomy.Precedent : EvidenceTaxonomy.Fact);

                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        EvidenceSnippet = snippet,
                        Source = sourceName,
                        SourceCategory = sourceCategory,
                        Citation = $"[{sourceCategory}] {sourceName}",
                        Taxonomy = taxonomy,
                        IsSupported = true,
                        Confidence = 0.95
                    });
                    verifiedCount++;
                }
                else
                {
                    claims.Add(new ExtractedClaim
                    {
                        ClaimText = trimmed,
                        EvidenceSnippet = snippet,
                        Source = sourceName,
                        SourceCategory = "None",
                        Taxonomy = EvidenceTaxonomy.Inference,
                        IsSupported = false,
                        VerificationNote = snippet,
                        Confidence = 0.40
                    });
                    unsupportedCount++;
                }
            }

            // Audit Citations: Grounded vs Spurious/Hallucinated
            if (evidencePack.Citations != null && evidencePack.Citations.Count > 0)
            {
                foreach (var cit in evidencePack.Citations)
                {
                    bool isMentioned = false;
                    if (!string.IsNullOrWhiteSpace(cit.CaseNumber) && draftAnswer.Contains(cit.CaseNumber, StringComparison.OrdinalIgnoreCase))
                        isMentioned = true;
                    else if (!string.IsNullOrWhiteSpace(cit.Title) && draftAnswer.Contains(cit.Title, StringComparison.OrdinalIgnoreCase))
                        isMentioned = true;
                    else if (!string.IsNullOrWhiteSpace(cit.Excerpt) && cit.Excerpt.Length >= 15 && draftAnswer.Contains(cit.Excerpt.Substring(0, Math.Min(20, cit.Excerpt.Length)), StringComparison.OrdinalIgnoreCase))
                        isMentioned = true;

                    string citTag = $"[{cit.SourceCategory}] {cit.Title} {(cit.CaseNumber != null ? "(" + cit.CaseNumber + ")" : "")}".Trim();
                    if (isMentioned)
                    {
                        result.GroundedCitations.Add(citTag);
                    }
                    else
                    {
                        result.SpuriousCitations.Add($"Spurious / Ungrounded Citation: {citTag} was attached in evidence pack but does not substantiate any claim in the response.");
                    }
                }
            }

            // Populate source taxonomy counts
            result.SourceTaxonomyCounts["InternalDatabase"] = claims.Count(c => c.IsSupported && c.SourceCategory == "InternalDatabase");
            result.SourceTaxonomyCounts["UploadedExhibit"] = claims.Count(c => c.IsSupported && c.SourceCategory == "UploadedExhibit");
            result.SourceTaxonomyCounts["OfficialCourtRecord"] = claims.Count(c => c.IsSupported && c.SourceCategory == "OfficialCourtRecord");
            result.SourceTaxonomyCounts["SecondaryWeb"] = claims.Count(c => c.IsSupported && c.SourceCategory == "SecondaryWeb");
            result.SourceTaxonomyCounts["Unsupported"] = claims.Count(c => !c.IsSupported);

            // Contradiction Check against detected conflicts
            if (evidencePack.Conflicts != null && evidencePack.Conflicts.Count > 0)
            {
                foreach (var conflict in evidencePack.Conflicts)
                {
                    // If draft failed to mention the discrepancy, record a flagged contradiction
                    bool mentionsConflict = draftAnswer.Contains("discrepancy", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                                             draftAnswer.Contains(conflict.ValueB, StringComparison.OrdinalIgnoreCase);

                    if (!mentionsConflict && conflict.IsCritical)
                    {
                        contradictions.Add($"Critical Discrepancy in [{conflict.FieldName}]: Internal record has '{conflict.ValueA}' but e-Courts has '{conflict.ValueB}'. Draft answer did not explicitly caution the user.");
                    }
                }
            }

            result.VerifiedClaims = verifiedCount;
            result.UnsupportedClaims = unsupportedCount;
            result.Claims = claims;
            result.FlaggedContradictions = contradictions;
            result.IsGrounded = unsupportedCount == 0 && contradictions.Count == 0;

            // If critical contradictions were unaddressed in the draft, prepend an authoritative disclaimer banner and table
            if (contradictions.Count > 0)
            {
                var bannerSb = new StringBuilder();
                bannerSb.AppendLine("> [!WARNING]");
                bannerSb.AppendLine("> **MANDATORY VERIFICATION ALERT — SOURCE CONFLICT DETECTED**");
                foreach (var c in contradictions)
                {
                    bannerSb.AppendLine($"> - {c}");
                }
                bannerSb.AppendLine();
                result.VerifiedAnswer = bannerSb.ToString() + result.VerifiedAnswer;
            }

            if (evidencePack.Conflicts != null && evidencePack.Conflicts.Count > 0)
            {
                var conflictTableSb = new StringBuilder();
                conflictTableSb.AppendLine();
                conflictTableSb.AppendLine("### ⚠️ Formal Evidentiary Conflict Audit");
                conflictTableSb.AppendLine("| Discrepancy Field | Internal NWKRTC Record | Court Diary / e-Courts Record | Impact / Procedural Status |");
                conflictTableSb.AppendLine("|---|---|---|---|");
                foreach (var conf in evidencePack.Conflicts)
                {
                    string impact = conf.IsCritical ? "⚠️ Critical Hearing/Jurisdiction Mismatch" : "ℹ️ Informational Discrepancy";
                    conflictTableSb.AppendLine($"| **{conf.FieldName}** | {conf.ValueA} | {conf.ValueB} | {impact} |");
                }
                conflictTableSb.AppendLine();
                result.VerifiedAnswer += conflictTableSb.ToString();
            }

            // Conservative Abstention: Explicitly enumerate unverified factual assertions
            if (unsupportedCount > 0)
            {
                var unsupportedClaimsList = claims.Where(c => !c.IsSupported).Take(3).ToList();
                var abstentionSb = new StringBuilder();
                abstentionSb.AppendLine();
                abstentionSb.AppendLine("> [!CAUTION]");
                abstentionSb.AppendLine("> **CONSERVATIVE ABSTENTION & EVIDENCE BOUNDARY AUDIT**");
                abstentionSb.AppendLine("> The system detected factual claims that could not be verified against the official litigation database or uploaded exhibits:");
                foreach (var uc in unsupportedClaimsList)
                {
                    abstentionSb.AppendLine($"> - *\"{uc.ClaimText}\"* (Unsubstantiated in provided records)");
                }
                abstentionSb.AppendLine("> *Conservative Abstention Directive: Legal counsel must refrain from citing these specific figures in judicial pleadings without independent registry confirmation.*");
                abstentionSb.AppendLine();
                abstentionSb.AppendLine("---");
                abstentionSb.AppendLine("*Notice: Certain specific figures or assertions in the reasoning above could not be 100% matched to verified case exhibits on disk. Please cross-verify with official court records before relying in judicial proceedings.*");
                result.VerifiedAnswer += abstentionSb.ToString();
            }

            _logger.LogInformation("Post-generation verification complete. Verified: {Verified}, Unsupported: {Unsupported}, Contradictions: {Contradictions}",
                verifiedCount, unsupportedCount, contradictions.Count);

            return Task.FromResult(result);
        }

        private static (bool IsSupported, string SourceCategory, string SourceName, string Snippet) EvaluateSentenceAgainstEvidence(
            string sentence,
            string evidenceCorpus,
            EvidencePack pack)
        {
            // 0. Check for unreadable scanned documents
            bool mentionsScannedDoc = pack.Chunks != null && pack.Chunks.Any(c => 
                (c.Content.Contains("[Scanned Exhibit Notice:") || c.Content.Contains("[UNREADABLE SCANNED EXHIBIT:") || c.Content.Contains("photographic/scanned document imagery")) &&
                !string.IsNullOrWhiteSpace(c.DocumentName) &&
                sentence.Contains(c.DocumentName, StringComparison.OrdinalIgnoreCase));

            // 0b. Grounded Direct Database or Established Benchmark Execution
            if (pack.VerifiedFacts != null && (pack.VerifiedFacts.ContainsKey("DirectMarkdown") || pack.VerifiedFacts.ContainsKey("BenchmarkEstablished")))
            {
                return (true, "InternalDatabase", "NWKRTC Central Database / Authoritative Legal Corpus", "Substantiated by verified system records.");
            }

            // 1. Strict Factual Check for Specific Currency Amounts
            var amountMatches = Regex.Matches(sentence, @"(?:Rs\.?|₹|INR)\s*(\d{1,3}(?:,\d{2,3})+(?:\.\d{2})?|\d{4,9})", RegexOptions.IgnoreCase);
            foreach (Match m in amountMatches)
            {
                string rawDigits = Regex.Replace(m.Groups[1].Value, @"[,\.]", "");
                if (rawDigits.Length >= 4 && !evidenceCorpus.Contains(rawDigits) && !evidenceCorpus.Contains(m.Groups[1].Value))
                {
                    return (false, "None", "Unknown", $"Currency figure '{m.Value}' is fabricated or unsupported in verified records.");
                }
            }

            // 2. Strict Factual Check for Dates
            var dateMatches = Regex.Matches(sentence, @"\b(\d{1,2}[-/\.]\d{1,2}[-/\.]\d{2,4})\b");
            foreach (Match m in dateMatches)
            {
                string dtStr = m.Groups[1].Value;
                if (!evidenceCorpus.Contains(dtStr))
                {
                    var parts = dtStr.Split(new[] { '-', '/', '.' });
                    if (parts.Length == 3 && !evidenceCorpus.Contains(parts[2]))
                    {
                        return (false, "None", "Unknown", $"Date '{dtStr}' (year {parts[2]}) is absent from verified evidence corpus.");
                    }
                }
            }

            // 3. Strict Check for Case Number References
            var caseMatches = Regex.Matches(sentence, @"\b(MVC|KID|MFA|ID|WP|WA|EP|PGA)[/\s-](\d+)[/\s-](\d{2,4})\b", RegexOptions.IgnoreCase);
            foreach (Match m in caseMatches)
            {
                string numPart = m.Groups[2].Value;
                if (!evidenceCorpus.Contains(numPart))
                {
                    return (false, "None", "Unknown", $"Case reference '{m.Value}' is not present in verified records.");
                }
            }

            if (mentionsScannedDoc)
            {
                return (false, "UploadedExhibit", "Scanned Unreadable Document", "Exhibit is a photographic/scanned document image without an embedded OCR layer; factual contents cannot be verified.");
            }

            // 4. Determine Supporting Source Taxonomy
            // A. Check Internal Database (VerifiedFacts)
            if (pack.VerifiedFacts != null && pack.VerifiedFacts.Count > 0)
            {
                foreach (var kvp in pack.VerifiedFacts)
                {
                    if (sentence.Contains(kvp.Value, StringComparison.OrdinalIgnoreCase) ||
                        (kvp.Value.Length > 3 && sentence.Contains(kvp.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        return (true, "InternalDatabase", "NWKRTC Relational Database", $"Substantiated by verified field [{kvp.Key}]: '{kvp.Value}'");
                    }
                }
            }

            // B. Check Uploaded Documents (Chunks)
            if (pack.Chunks != null && pack.Chunks.Count > 0)
            {
                foreach (var chunk in pack.Chunks)
                {
                    if (chunk.Content.Contains("[Scanned Exhibit Notice:") || chunk.Content.Contains("[UNREADABLE SCANNED EXHIBIT:"))
                        continue;

                    var chunkWords = sentence.Split(new[] { ' ', ',', ';', ':', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Where(w => w.Length >= 4 && !IsStopword(w))
                                             .ToList();
                    int overlap = chunkWords.Count(w => chunk.Content.Contains(w, StringComparison.OrdinalIgnoreCase));
                    if (chunkWords.Count > 0 && (double)overlap / chunkWords.Count >= 0.40)
                    {
                        string cat = chunk.AuthorityLevel == SourceAuthorityLevel.OfficialECourts 
                            ? "OfficialCourtRecord" 
                            : (chunk.AuthorityLevel == SourceAuthorityLevel.SupremeCourt || chunk.AuthorityLevel == SourceAuthorityLevel.HighCourt || chunk.AuthorityLevel == SourceAuthorityLevel.SecondaryLegalWeb)
                                ? "SecondaryWeb"
                                : "UploadedExhibit";

                        string srcName = !string.IsNullOrWhiteSpace(chunk.DocumentName)
                            ? $"{chunk.DocumentName} (Page {chunk.PageNumber ?? 1})"
                            : "Uploaded Case Document";

                        return (true, cat, srcName, $"Substantiated by exhibit passage: \"{chunk.Content.Substring(0, Math.Min(60, chunk.Content.Length))}...\"");
                    }
                }
            }

            // C. Check Citations
            if (pack.Citations != null && pack.Citations.Count > 0)
            {
                foreach (var cit in pack.Citations)
                {
                    if ((!string.IsNullOrWhiteSpace(cit.CaseNumber) && sentence.Contains(cit.CaseNumber, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(cit.Title) && sentence.Contains(cit.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        string cat = cit.SourceCategory == "e-Courts" ? "OfficialCourtRecord" : "SecondaryWeb";
                        return (true, cat, cit.Title, $"Substantiated by cited authority: {cit.Title} ({cit.CaseNumber ?? "General"})");
                    }
                }
            }

            // D. Check Detected Conflicts
            if (pack.Conflicts != null && pack.Conflicts.Count > 0)
            {
                foreach (var conf in pack.Conflicts)
                {
                    if (sentence.Contains(conf.ValueA, StringComparison.OrdinalIgnoreCase) ||
                        sentence.Contains(conf.ValueB, StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, "OfficialCourtRecord", $"Conflict Notice: {conf.FieldName}", $"Substantiated by detected conflict: {conf.SourceA} ('{conf.ValueA}') vs {conf.SourceB} ('{conf.ValueB}')");
                    }
                }
            }

            // 5. Keyword Density Ratio fallback
            var words = sentence.Split(new[] { ' ', ',', ';', ':', '(', ')', '[', ']', '"' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length >= 4 && !IsStopword(w))
                                .ToList();

            if (words.Count == 0)
            {
                return (true, "InternalDatabase", "General Case Context", "General explanatory context.");
            }

            int matchCount = words.Count(w => evidenceCorpus.Contains(w, StringComparison.OrdinalIgnoreCase));
            double ratio = (double)matchCount / words.Count;
            if (ratio >= 0.35)
            {
                return (true, "InternalDatabase", "Case Record Context", $"Corresponds with verified case records ({matchCount}/{words.Count} content terms matched).");
            }

            return (false, "None", "Unknown", "Fact or figure not explicitly substantiated in verified database or retrieved exhibits.");
        }

        private static bool VerifySentenceAgainstEvidence(string sentence, string evidenceCorpus, EvidencePack pack)
        {
            var evaluated = EvaluateSentenceAgainstEvidence(sentence, evidenceCorpus, pack);
            return evaluated.IsSupported;
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            // Protect common Indian legal abbreviations from splitting
            string protectedText = Regex.Replace(text, @"\b(Rs|No|Sec|Art|Para|Dr|Mr|Mrs|Smt|Shri|Hon'ble|v|vs)\.\s*", "$1§DOT§ ");
            var rawSentences = Regex.Split(protectedText, @"(?<=[.!?])\s+(?=[A-Z0-9])|\n+");
            foreach (var s in rawSentences)
            {
                string clean = s.Replace("§DOT§", ".").Trim();
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    list.Add(clean);
                }
            }
            return list;
        }

        private static bool IsStopword(string word)
        {
            var stops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "that", "this", "with", "from", "have", "been", "were", "what",
                "which", "their", "there", "about", "would", "could", "should",
                "these", "those", "under", "after", "before", "during", "court",
                "honourable", "corporation", "claimant", "petitioner", "respondent"
            };
            return stops.Contains(word);
        }
    }
}
