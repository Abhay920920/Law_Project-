using System;
using System.Collections.Generic;
using System.Security;
using System.Text.RegularExpressions;
using Dapper;
using MVCCaseManagement.Services.AI.Catalog;

namespace MVCCaseManagement.Services.AI.Compiler
{
    public class StrixSecurityValidationResult
    {
        public bool IsValid { get; set; }
        public string CleanedSql { get; set; } = string.Empty;
        public DynamicParameters Parameters { get; set; } = new();
        public string? FailureReason { get; set; }
    }

    public static class StrixSqlSafetyGuard
    {
        private static readonly HashSet<string> DisallowedTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "EXEC", "EXECUTE",
            "MERGE", "GRANT", "REVOKE", "DENY", "SHUTDOWN", "XP_CMDSHELL", "SP_OACREATE", "SP_EXECUTESQL",
            "--", "/*", "*/", ";", "OPENROWSET", "OPENDATASOURCE", "BULK"
        };

        private static readonly HashSet<string> ReservedSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "WHERE", "ON", "INNER", "LEFT", "RIGHT", "CROSS", "FULL", "JOIN", "ORDER", "GROUP", "HAVING", "LIMIT", "OFFSET", "UNION", "AS", "WITH"
        };

        private static readonly Regex TokenRegex = new(@"\b([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
        private static readonly Regex FromJoinRegex = new(@"(?:FROM|JOIN)\s+(?:\[dbo\]\.)?\[?([A-Za-z0-9_]+)\]?(?:\s+(?:AS\s+)?([A-Za-z0-9_]+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Validates query safety, rejects DDL/DML/exec/semicolons, checks allowlisted tables,
        /// and enforces mandatory multi-tenant boundaries for non-Central Office users.
        /// </summary>
        public static StrixSecurityValidationResult ValidateAndEnforceTenancy(
            string sql,
            DynamicParameters? parameters,
            int userDivisionId,
            bool isCentralOffice)
        {
            var result = new StrixSecurityValidationResult
            {
                Parameters = parameters ?? new DynamicParameters()
            };

            if (string.IsNullOrWhiteSpace(sql))
            {
                result.IsValid = false;
                result.FailureReason = "SQL query cannot be empty.";
                return result;
            }

            string trimmedSql = sql.Trim();

            // 1. AST Token & Disallowed Keywords Gate
            if (trimmedSql.Contains(';'))
            {
                result.IsValid = false;
                result.FailureReason = "Semicolons are prohibited to prevent stacked query injection.";
                return result;
            }

            if (trimmedSql.Contains("--") || trimmedSql.Contains("/*") || trimmedSql.Contains("*/"))
            {
                result.IsValid = false;
                result.FailureReason = "SQL comment tokens are prohibited to prevent comment evasion attacks.";
                return result;
            }

            // Must begin with SELECT or WITH
            if (!trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.FailureReason = "Only read-only SELECT or CTE queries are authorized.";
                return result;
            }

            // Check against disallowed tokens
            var matches = TokenRegex.Matches(trimmedSql);
            foreach (Match match in matches)
            {
                string token = match.Value;
                if (DisallowedTokens.Contains(token))
                {
                    result.IsValid = false;
                    result.FailureReason = $"Forbidden SQL token detected: '{token}'. Dynamic queries must be strictly read-only.";
                    return result;
                }
            }

            // 2. Allowlisted Tables Check
            var joinMatches = FromJoinRegex.Matches(trimmedSql);
            string? primaryTable = null;
            string? primaryAlias = null;

            foreach (Match m in joinMatches)
            {
                string table = m.Groups[1].Value;
                string rawAlias = m.Groups[2].Success ? m.Groups[2].Value : table;
                string alias = ReservedSqlKeywords.Contains(rawAlias) ? table : rawAlias;

                // Ignore CTE or subquery names if known
                var descriptor = NWKRTCSchemaCatalog.GetTable(table);
                if (descriptor == null)
                {
                    // Allow special standard tables/functions
                    if (!string.Equals(table, "sys", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(table, "master", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's an allowlisted table
                        result.IsValid = false;
                        result.FailureReason = $"Access to table '{table}' is prohibited by schema allowlist.";
                        return result;
                    }
                }

                if (primaryTable == null && descriptor != null)
                {
                    primaryTable = table;
                    primaryAlias = alias;
                }
            }

            // 3. SQL Server 2012 Grammar & Windowing Safety
            string processedSql = trimmedSql;
            if (!Regex.IsMatch(processedSql, @"\bSELECT\s+(?:DISTINCT\s+)?TOP\s+\d+", RegexOptions.IgnoreCase))
            {
                // Inject TOP 100 into the outermost SELECT
                processedSql = Regex.Replace(processedSql, @"^SELECT\s+", "SELECT TOP 100 ", RegexOptions.IgnoreCase);
            }

            // 4. Multi-Tenant Boundary Enforcement
            // Central Office is DivisionID == 0 or 5.
            bool userIsCentral = isCentralOffice || userDivisionId == 0 || userDivisionId == 5;

            if (!userIsCentral)
            {
                // Divisional user: MUST filter by userDivisionId
                if (string.IsNullOrEmpty(primaryTable))
                {
                    result.IsValid = false;
                    result.FailureReason = "Cannot determine primary table for divisional tenant isolation.";
                    return result;
                }

                var tableDesc = NWKRTCSchemaCatalog.GetTable(primaryTable);
                string? divCol = tableDesc?.DivisionColumn;
                string aliasPrefix = !string.IsNullOrEmpty(primaryAlias) ? $"{primaryAlias}." : "";

                result.Parameters.Add("@TenantDivisionId", userDivisionId);

                if (!string.IsNullOrEmpty(divCol))
                {
                    // Direct division column on table (e.g. MVC_CASES.DivisionID, LABOUR_CASES.DivisionID, GRA_CASES.DivisionCode)
                    string tenantCondition;
                    if (string.Equals(divCol, "DivisionCode", StringComparison.OrdinalIgnoreCase))
                    {
                        // GRA_CASES DivisionCode can be int or string
                        tenantCondition = $"({aliasPrefix}{divCol} = CAST(@TenantDivisionId AS VARCHAR(10)) OR {aliasPrefix}{divCol} = @TenantDivisionId)";
                    }
                    else
                    {
                        tenantCondition = $"{aliasPrefix}{divCol} = @TenantDivisionId";
                    }

                    processedSql = InjectTenantCondition(processedSql, tenantCondition);
                }
                else if (!string.IsNullOrEmpty(tableDesc?.ParentTable) && !string.IsNullOrEmpty(tableDesc?.ForeignKeyToParent))
                {
                    // Child table (e.g. APPEAL_DETAILS, MVC_CASE_ADVERSE_DETAILS) linked to parent
                    var parentDesc = NWKRTCSchemaCatalog.GetTable(tableDesc.ParentTable);
                    if (parentDesc?.DivisionColumn != null)
                    {
                        string tenantCondition = $"{aliasPrefix}{tableDesc.ForeignKeyToParent} IN (SELECT p.{parentDesc.PrimaryKey} FROM {parentDesc.TableName} p WHERE p.{parentDesc.DivisionColumn} = @TenantDivisionId)";
                        processedSql = InjectTenantCondition(processedSql, tenantCondition);
                    }
                }
                else if (string.Equals(primaryTable, "PETTY_BILLS", StringComparison.OrdinalIgnoreCase))
                {
                    // PETTY_BILLS has DivisionName or CaseID
                    string tenantCondition = $"{aliasPrefix}CaseID IN (SELECT c.CaseID FROM MVC_CASES c WHERE c.DivisionID = @TenantDivisionId)";
                    processedSql = InjectTenantCondition(processedSql, tenantCondition);
                }
            }

            result.IsValid = true;
            result.CleanedSql = processedSql;
            return result;
        }

        private static string InjectTenantCondition(string sql, string tenantCondition)
        {
            // If query contains WHERE clause, append AND (tenantCondition)
            // If query contains GROUP BY, ORDER BY, or neither, inject WHERE or AND appropriately
            var whereMatch = Regex.Match(sql, @"\bWHERE\b", RegexOptions.IgnoreCase);
            if (whereMatch.Success)
            {
                int insertPos = whereMatch.Index + whereMatch.Length;
                return sql.Substring(0, insertPos) + $" ({tenantCondition}) AND" + sql.Substring(insertPos);
            }

            // No WHERE clause: find position before GROUP BY, ORDER BY, or end of query
            var tailMatch = Regex.Match(sql, @"\b(GROUP\s+BY|ORDER\s+BY|HAVING)\b", RegexOptions.IgnoreCase);
            if (tailMatch.Success)
            {
                int insertPos = tailMatch.Index;
                return sql.Substring(0, insertPos) + $" WHERE {tenantCondition} " + sql.Substring(insertPos);
            }

            return sql + $" WHERE {tenantCondition}";
        }
    }
}
