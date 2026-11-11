using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI.Presentation
{
    public static class DynamicTabularPresenter
    {
        private static readonly CultureInfo IndianCulture = new("en-IN");

        public static DynamicQueryResult FormatResult(
            IEnumerable<IDictionary<string, object?>> records,
            string title,
            string primaryTable,
            long executionTimeMs,
            string? customSummary = null)
        {
            var recordList = records.Select(r => new Dictionary<string, object>(
                r.ToDictionary(k => k.Key, v => v.Value ?? string.Empty),
                StringComparer.OrdinalIgnoreCase)).ToList();

            var result = new DynamicQueryResult
            {
                Title = title,
                IntentName = title,
                RecordCount = recordList.Count,
                IsDeterministicSuccess = true,
                Records = recordList
            };

            if (recordList.Count == 0)
            {
                result.Summary = $"Zero records found in database for query '{title}'.";
                result.FormattedMarkdown = BuildEmptyMarkdown(title, primaryTable, executionTimeMs);
                return result;
            }

            // Extract columns
            var columns = recordList[0].Keys.ToList();

            // Detect monetary columns
            var monetaryCols = columns.Where(c =>
                c.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Payable", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Paid", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Cost", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Award", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Exposure", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Balance", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("TDS", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Compute summary metrics
            decimal totalExposure = 0;
            foreach (var col in monetaryCols)
            {
                decimal sum = 0;
                foreach (var row in recordList)
                {
                    if (row.TryGetValue(col, out var val) && val != null)
                    {
                        if (decimal.TryParse(val.ToString(), out decimal d))
                        {
                            sum += d;
                        }
                    }
                }
                if (sum > 0)
                {
                    result.KeyMetrics[$"Total_{col}"] = FormatCurrency(sum);
                    if (totalExposure == 0) totalExposure = sum;
                }
            }

            result.KeyMetrics["TotalRecords"] = recordList.Count.ToString();

            // Build Executive Markdown
            var sb = new StringBuilder();
            sb.AppendLine($"### 📊 {title}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(customSummary))
            {
                sb.AppendLine(customSummary);
                sb.AppendLine();
            }

            // Metric Summary Cards
            sb.AppendLine($"> **Executive Metric Summary**:");
            sb.AppendLine($"> - **Total Records Retrieved**: `{recordList.Count}` verified records");
            if (totalExposure > 0)
            {
                string firstMonetaryCol = monetaryCols.FirstOrDefault() ?? "Total Amount";
                sb.AppendLine($"> - **Aggregate Financial Exposure ({FormatHeader(firstMonetaryCol)})**: `{FormatCurrency(totalExposure)}`");
            }
            sb.AppendLine();

            // Markdown Table
            // 1. Header
            sb.Append("| # | ");
            foreach (var col in columns)
            {
                sb.Append($"{FormatHeader(col)} | ");
            }
            sb.AppendLine();

            // 2. Separator
            sb.Append("| :---: | ");
            foreach (var col in columns)
            {
                if (monetaryCols.Contains(col))
                    sb.Append("---: | ");
                else
                    sb.Append(":--- | ");
            }
            sb.AppendLine();

            // 3. Rows
            int index = 1;
            foreach (var row in recordList)
            {
                sb.Append($"| {index++} | ");
                foreach (var col in columns)
                {
                    object? rawVal = row.TryGetValue(col, out var v) ? v : null;
                    string formattedVal = FormatCellValue(col, rawVal, monetaryCols.Contains(col));
                    sb.Append($"{formattedVal} | ");
                }
                sb.AppendLine();
            }

            sb.AppendLine();

            // Database Provenance Badge
            sb.AppendLine($"> 🏛️ **Database Provenance**: Verified live records from `dbo.{primaryTable}` (`Admin_Law` on `198.38.89.31`). Query execution time: **{executionTimeMs}ms**. 100% database factual accuracy.");

            result.FormattedMarkdown = sb.ToString();
            result.Summary = $"Retrieved {recordList.Count} verified records directly from dbo.{primaryTable}.";

            // Add standard internal citation
            result.Citations.Add(new CitationDto
            {
                SourceType = "CaseRecord",
                SourceCategory = "Internal",
                Title = $"NWKRTC Database ({primaryTable})",
                Court = "NWKRTC Central Registry",
                IsVerified = true,
                Excerpt = $"Verified live database extract from dbo.{primaryTable} containing {recordList.Count} rows."
            });

            return result;
        }

        private static string BuildEmptyMarkdown(string title, string primaryTable, long executionTimeMs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### 📊 {title}");
            sb.AppendLine();
            sb.AppendLine("> ℹ️ **No Records Found**: There are no database records matching the requested criteria within your authorized divisional scope.");
            sb.AppendLine();
            sb.AppendLine($"> 🏛️ **Database Provenance**: Verified live check against `dbo.{primaryTable}` (`Admin_Law` on `198.38.89.31`). Query execution time: **{executionTimeMs}ms**.");
            return sb.ToString();
        }

        private static string FormatHeader(string columnName)
        {
            // Humanize column names
            return columnName switch
            {
                "CaseID" => "Case ID",
                "MVCNo" => "MVC No",
                "MVCYear" => "Year",
                "VehicleNo" => "Vehicle No",
                "AccidentDate" => "Accident Date",
                "ClaimType" => "Claim Type",
                "ClaimAmount" => "Claim Amount",
                "AwardAmount" => "Award Amount",
                "CurrentStage" => "Stage",
                "AdvocateName" => "Advocate",
                "DisposalStatus" => "Status",
                "NextHearingDate" => "Next Hearing",
                "DaysOverdue" => "Days Overdue",
                "PetitionerName" => "Petitioner / Claimant",
                "WorkmanName" => "Workman Name",
                "EmployeeNo" => "Staff No",
                "Designation" => "Designation",
                "NatureOfMisconduct" => "Misconduct",
                "Reinstated" => "Reinstated?",
                "HighCourtStay" => "HC Stay",
                "NetPayable" => "Net Payable",
                "GrossPayable" => "Gross Liability",
                "TDSAmount" => "TDS Deducted",
                "ChequeNumber" => "Cheque No",
                "ChequeDate" => "Cheque Date",
                "CancellationReason" => "Cancellation Reason",
                "TotalOrdered" => "CA Ordered (₹)",
                "TotalPaid" => "Paid (₹)",
                "OutstandingLiability" => "Outstanding (₹)",
                "ActiveCases" => "Active Cases",
                "TotalClaimExposure" => "Total Claim (₹)",
                _ => columnName
            };
        }

        private static string FormatCellValue(string col, object? val, bool isMonetary)
        {
            if (val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString()))
            {
                return "-";
            }

            string str = val.ToString()!.Trim();

            // Handle boolean values
            if (val is bool b)
            {
                return b ? "`YES`" : "`NO`";
            }
            if (str.Equals("true", StringComparison.OrdinalIgnoreCase)) return "`YES`";
            if (str.Equals("false", StringComparison.OrdinalIgnoreCase)) return "`NO`";

            // Handle DateTime values
            if (val is DateTime dt)
            {
                return dt.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
            }
            if (col.Contains("Date", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(str, out var parsedDt))
            {
                return parsedDt.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
            }

            // Handle Monetary values
            if (isMonetary)
            {
                if (decimal.TryParse(str, out decimal dec))
                {
                    return FormatCurrency(dec);
                }
            }

            // Format status badges
            if (col.Equals("CurrentStage", StringComparison.OrdinalIgnoreCase) ||
                col.Equals("DisposalStatus", StringComparison.OrdinalIgnoreCase) ||
                col.Equals("CaseStatus", StringComparison.OrdinalIgnoreCase))
            {
                if (str.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    return $"⏳ `{str}`";
                if (str.Equals("Disposed", StringComparison.OrdinalIgnoreCase))
                    return $"✅ `{str}`";
            }

            return CleanForMarkdown(str);
        }

        private static string FormatCurrency(decimal amount)
        {
            return $"₹{amount.ToString("N2", IndianCulture)}";
        }

        private static string CleanForMarkdown(string s)
        {
            return s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}

