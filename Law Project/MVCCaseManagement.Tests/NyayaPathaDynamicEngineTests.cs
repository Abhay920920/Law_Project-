using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI.Catalog;
using MVCCaseManagement.Services.AI.Compiler;
using MVCCaseManagement.Services.AI.Presentation;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class NyayaPathaDynamicEngineTests
    {
        // =========================================================================
        // 1. STRIX SECURITY GUARD TESTS
        // =========================================================================

        [Fact]
        public void StrixGuard_RejectsSemicolonInjection()
        {
            string maliciousSql = "SELECT * FROM MVC_CASES; DROP TABLE MVC_CASES";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(maliciousSql, null, 5, true);

            Assert.False(result.IsValid);
            Assert.Contains("Semicolons are prohibited", result.FailureReason);
        }

        [Fact]
        public void StrixGuard_RejectsCommentEvasionTokens()
        {
            string sqlWithDashComment = "SELECT * FROM MVC_CASES -- comment bypass";
            var res1 = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sqlWithDashComment, null, 5, true);
            Assert.False(res1.IsValid);
            Assert.Contains("SQL comment tokens are prohibited", res1.FailureReason);

            string sqlWithBlockComment = "SELECT * FROM MVC_CASES /* comment */";
            var res2 = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sqlWithBlockComment, null, 5, true);
            Assert.False(res2.IsValid);
            Assert.Contains("SQL comment tokens are prohibited", res2.FailureReason);
        }

        [Theory]
        [InlineData("INSERT INTO MVC_CASES (MVCNo) VALUES ('123')")]
        [InlineData("UPDATE MVC_CASES SET ClaimAmount = 0")]
        [InlineData("DELETE FROM MVC_CASES")]
        [InlineData("DROP TABLE MVC_CASES")]
        [InlineData("ALTER TABLE MVC_CASES ADD ColumnX INT")]
        [InlineData("TRUNCATE TABLE MVC_CASES")]
        [InlineData("EXEC xp_cmdshell 'dir'")]
        [InlineData("EXEC sp_executesql N'SELECT 1'")]
        public void StrixGuard_RejectsDisallowedMutationAndExecTokens(string disallowedSql)
        {
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(disallowedSql, null, 5, true);

            Assert.False(result.IsValid);
            Assert.True(
                result.FailureReason!.Contains("Only read-only SELECT") ||
                result.FailureReason!.Contains("Forbidden SQL token"));
        }

        [Fact]
        public void StrixGuard_RejectsNonAllowlistedTable()
        {
            string sql = "SELECT * FROM SENSITIVE_USER_PASSWORDS";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sql, null, 5, true);

            Assert.False(result.IsValid);
            Assert.Contains("prohibited by schema allowlist", result.FailureReason);
        }

        [Fact]
        public void StrixGuard_EnforcesDivisionalTenantFilter_ForNonCentralOffice()
        {
            // Division 7 = Haveri (Not Central Office)
            string sql = "SELECT CaseID, MVCNo FROM MVC_CASES WHERE ClaimAmount > 100000";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sql, null, 7, false);

            Assert.True(result.IsValid);
            Assert.Contains("@TenantDivisionId", result.CleanedSql);
            Assert.Contains("MVC_CASES.DivisionID = @TenantDivisionId", result.CleanedSql);

            // Verify parameter injected
            var paramValue = result.Parameters.Get<int>("@TenantDivisionId");
            Assert.Equal(7, paramValue);
        }

        [Fact]
        public void StrixGuard_DoesNotEnforceTenantFilter_ForCentralOffice()
        {
            // Division 5 = Central Office
            string sql = "SELECT CaseID, MVCNo FROM MVC_CASES WHERE ClaimAmount > 100000";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sql, null, 5, true);

            Assert.True(result.IsValid);
            Assert.DoesNotContain("@TenantDivisionId", result.CleanedSql);
        }

        [Fact]
        public void StrixGuard_EnforcesTenantSubqueryOnChildTable_AppealDetails()
        {
            // Division 2 = Belagavi querying APPEAL_DETAILS (which lacks DivisionID directly)
            string sql = "SELECT a.AppealID, a.CorpMFANumber FROM APPEAL_DETAILS a WHERE a.CorpMFAYear = 2023";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sql, null, 2, false);

            Assert.True(result.IsValid);
            Assert.Contains("a.CaseID IN (SELECT p.CaseID FROM MVC_CASES p WHERE p.DivisionID = @TenantDivisionId)", result.CleanedSql);
            Assert.Equal(2, result.Parameters.Get<int>("@TenantDivisionId"));
        }

        [Fact]
        public void StrixGuard_InjectsTop100Window_WhenMissing()
        {
            string sql = "SELECT c.CaseID, c.MVCNo FROM MVC_CASES c";
            var result = StrixSqlSafetyGuard.ValidateAndEnforceTenancy(sql, null, 5, true);

            Assert.True(result.IsValid);
            Assert.StartsWith("SELECT TOP 100", result.CleanedSql, StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================================
        // 2. DYNAMIC SQL COMPILER TESTS
        // =========================================================================

        [Fact]
        public void Compiler_GeneratesFatalClaimsQuery_ForBelagavi()
        {
            string query = "Fatal MVC cases in Belagavi in 2023";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("MVC", plan.Module);
            Assert.Equal("MVC_CASES", plan.PrimaryTable);
            Assert.Contains("c.ClaimType LIKE '%Fatal%'", plan.Sql);
            Assert.Contains("c.DivisionID = @DivId", plan.Sql);
            Assert.Contains("c.MVCYear = @CaseYear", plan.Sql);
            Assert.Equal(2, plan.Parameters.Get<int>("@DivId")); // Belagavi = 2
            Assert.Equal(2023, plan.Parameters.Get<int>("@CaseYear"));
        }

        [Fact]
        public void Compiler_GeneratesLabourReinstatementQuery()
        {
            string query = "Labour cases pending reinstatement in Bagalkot";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("Labour", plan.Module);
            Assert.Equal("LABOUR_CASES", plan.PrimaryTable);
            Assert.Contains("CO_IsWorkmanReinstated = 1", plan.Sql);
            Assert.Contains("l.DivisionID = @DivId", plan.Sql);
            Assert.Equal(6, plan.Parameters.Get<int>("@DivId")); // Bagalkot = 6
        }

        [Fact]
        public void Compiler_GeneratesGratuityOutstandingBalanceQuery()
        {
            string query = "Total unpaid gratuity across divisions";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("Gratuity", plan.Module);
            Assert.Equal("GRA_CASES", plan.PrimaryTable);
            Assert.True(plan.IsAggregation);
            Assert.Contains("SUM(g.OrderedAmount_CA)", plan.Sql);
            Assert.Contains("SUM(g.ActualPaidAmount)", plan.Sql);
        }

        [Fact]
        public void Compiler_GeneratesCancelledChequeQuery_ForPettyBills()
        {
            string query = "Show petty bills with cancelled cheques";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("PettyBill", plan.Module);
            Assert.Equal("PETTY_BILLS", plan.PrimaryTable);
            Assert.Contains("p.IsCancelled = 1", plan.Sql);
            Assert.Contains("PETTY_BILL_PAYMENTS", plan.Sql);
        }

        [Fact]
        public void Compiler_GeneratesVehicleHistoryQuery()
        {
            string query = "Litigation history for bus KA-25-F-1234";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("MVC", plan.Module);
            Assert.Contains("REPLACE(REPLACE(c.VehicleNo, '-', ''), ' ', '') = @CleanVehicleNo", plan.Sql);
            Assert.Equal("KA25F1234", plan.Parameters.Get<string>("@CleanVehicleNo"));
        }

        [Fact]
        public void Compiler_GeneratesAdvocateCaseloadQuery()
        {
            string query = "Top 5 advocates by active caseload in Dharwad";
            var plan = DynamicSqlCompiler.CompileQuery(query, 5, true);

            Assert.Equal("MVC", plan.Module);
            Assert.True(plan.IsAggregation);
            Assert.Contains("SELECT TOP 5", plan.Sql);
            Assert.Contains("COUNT(c.CaseID) AS ActiveCases", plan.Sql);
            Assert.Contains("LTRIM(RTRIM(ISNULL(c.AdvocateName, 'Unassigned')))", plan.Sql);
            Assert.Equal(4, plan.Parameters.Get<int>("@DivId")); // Dharwad = 4
        }

        [Fact]
        public void Compiler_GeneratesOverdueHearingsQuery()
        {
            string query = "Cases with hearing dates overdue by 30 days in Haveri";
            var plan = DynamicSqlCompiler.CompileQuery(query, 7, false);

            Assert.Equal("MVC", plan.Module);
            Assert.Contains("NextHearingDate < DATEADD(day, -30, GETDATE())", plan.Sql);
            Assert.Equal(7, plan.Parameters.Get<int>("@DivId")); // Haveri = 7
        }

        // =========================================================================
        // 3. DYNAMIC TABULAR PRESENTER TESTS
        // =========================================================================

        [Fact]
        public void TabularPresenter_FormatsIndianCurrencyAndDates()
        {
            var rows = new List<IDictionary<string, object?>>
            {
                new Dictionary<string, object?>
                {
                    ["CaseID"] = 101,
                    ["MVCNo"] = "450",
                    ["MVCYear"] = 2023,
                    ["VehicleNo"] = "KA-25-F-1234",
                    ["ClaimAmount"] = 1250000.50m,
                    ["AccidentDate"] = new DateTime(2023, 5, 14),
                    ["CurrentStage"] = "Pending"
                }
            };

            var result = DynamicTabularPresenter.FormatResult(rows, "Test Active MVC Cases", "MVC_CASES", 18);

            Assert.True(result.IsDeterministicSuccess);
            Assert.Equal(1, result.RecordCount);
            // Verify Indian currency format: ₹12,50,000.50
            Assert.Contains("₹12,50,000.50", result.FormattedMarkdown);
            // Verify date format: 14-May-2023
            Assert.Contains("14-May-2023", result.FormattedMarkdown);
            // Verify database provenance badge
            Assert.Contains("Database Provenance", result.FormattedMarkdown);
            Assert.Contains("dbo.MVC_CASES", result.FormattedMarkdown);
            Assert.Contains("18ms", result.FormattedMarkdown);
        }

        [Fact]
        public void TabularPresenter_HandlesEmptyResultSetGracefully()
        {
            var rows = new List<IDictionary<string, object?>>();
            var result = DynamicTabularPresenter.FormatResult(rows, "Empty Query Test", "MVC_CASES", 12);

            Assert.True(result.IsDeterministicSuccess);
            Assert.Equal(0, result.RecordCount);
            Assert.Contains("No Records Found", result.FormattedMarkdown);
            Assert.Contains("dbo.MVC_CASES", result.FormattedMarkdown);
        }

        // =========================================================================
        // 4. SCHEMA CATALOG TESTS
        // =========================================================================

        [Fact]
        public void Catalog_ContainsAllCoreLitigationTables()
        {
            var expectedTables = new[]
            {
                "MVC_CASES", "MVC_CASE_ADVERSE_DETAILS", "LABOUR_CASES",
                "GRA_CASES", "APPEAL_DETAILS", "PETTY_BILLS",
                "DIVISION_MASTER", "ADVOCATE_MASTER"
            };

            foreach (var tbl in expectedTables)
            {
                var tableDesc = NWKRTCSchemaCatalog.GetTable(tbl);
                Assert.NotNull(tableDesc);
                Assert.Equal(tbl, tableDesc.TableName);
            }
        }

        [Fact]
        public void Catalog_ResolvesSynonymsAccurately()
        {
            var mvc = NWKRTCSchemaCatalog.FindTableBySynonym("mact accident");
            Assert.NotNull(mvc);
            Assert.Equal("MVC_CASES", mvc.TableName);

            var labour = NWKRTCSchemaCatalog.FindTableBySynonym("industrial dispute");
            Assert.NotNull(labour);
            Assert.Equal("LABOUR_CASES", labour.TableName);

            var gra = NWKRTCSchemaCatalog.FindTableBySynonym("pga gratuity claim");
            Assert.NotNull(gra);
            Assert.Equal("GRA_CASES", gra.TableName);

            var appeal = NWKRTCSchemaCatalog.FindTableBySynonym("high court mfa stay");
            Assert.NotNull(appeal);
            Assert.Equal("APPEAL_DETAILS", appeal.TableName);

            var petty = NWKRTCSchemaCatalog.FindTableBySynonym("settlement bill voucher");
            Assert.NotNull(petty);
            Assert.Equal("PETTY_BILLS", petty.TableName);
        }
    }
}
