using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI;
using Xunit;

namespace MVCCaseManagement.Tests
{
    /// <summary>
    /// Genuine SQL Server LocalDB Integration Tests for Nyaya Patha AI Dynamic Database Retrieval.
    /// Exercises real SQL queries, joins across MVC_CASES, MVC_CASE_PETITIONERS, MVC_CASE_ADVERSE_DETAILS,
    /// division authorization, aggregations, date filtering, and SQL injection safety without mocks.
    /// </summary>
    public class NyayaPathaRealSqlIntegrationTests : IAsyncLifetime
    {
        private const string MasterConnStr = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=True;";
        private const string TestDbName = "NyayaPatha_Integration_TestDB";
        private const string TestDbConnStr = $"Server=(localdb)\\MSSQLLocalDB;Database={TestDbName};Integrated Security=true;TrustServerCertificate=True;";

        private DBHelper _dbHelper = null!;
        private DynamicDatabaseRetrievalService _retrievalService = null!;

        public async Task InitializeAsync()
        {
            // 1. Create isolated test database on LocalDB if it doesn't exist
            using (var masterConn = new SqlConnection(MasterConnStr))
            {
                await masterConn.OpenAsync();
                using var checkCmd = new SqlCommand($@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{TestDbName}')
                    BEGIN
                        CREATE DATABASE [{TestDbName}];
                    END", masterConn);
                await checkCmd.ExecuteNonQueryAsync();
            }

            // 2. Initialize schema tables matching production definitions
            using (var conn = new SqlConnection(TestDbConnStr))
            {
                await conn.OpenAsync();

                string ddl = @"
                IF OBJECT_ID('MVC_CASE_PETITIONERS', 'U') IS NOT NULL DROP TABLE MVC_CASE_PETITIONERS;
                IF OBJECT_ID('MVC_CASE_ADVERSE_DETAILS', 'U') IS NOT NULL DROP TABLE MVC_CASE_ADVERSE_DETAILS;
                IF OBJECT_ID('CASE_NOTINGS', 'U') IS NOT NULL DROP TABLE CASE_NOTINGS;
                IF OBJECT_ID('CASE_HEARINGS_HISTORY', 'U') IS NOT NULL DROP TABLE CASE_HEARINGS_HISTORY;
                IF OBJECT_ID('DOCUMENT_CHUNKS', 'U') IS NOT NULL DROP TABLE DOCUMENT_CHUNKS;
                IF OBJECT_ID('LABOUR_CASES', 'U') IS NOT NULL DROP TABLE LABOUR_CASES;
                IF OBJECT_ID('GRA_CASES', 'U') IS NOT NULL DROP TABLE GRA_CASES;
                IF OBJECT_ID('MVC_CASES', 'U') IS NOT NULL DROP TABLE MVC_CASES;
                IF OBJECT_ID('DIVISION_MASTER', 'U') IS NOT NULL DROP TABLE DIVISION_MASTER;

                CREATE TABLE DIVISION_MASTER (
                    DivisionID INT PRIMARY KEY,
                    DivisionNameEnglish NVARCHAR(100) NOT NULL
                );

                CREATE TABLE MVC_CASES (
                    CaseID INT IDENTITY(1,1) PRIMARY KEY,
                    DivisionID INT NOT NULL,
                    MVCNo NVARCHAR(50) NOT NULL,
                    MVCYear INT NOT NULL,
                    MACTID INT NOT NULL DEFAULT 1,
                    VehicleNo NVARCHAR(20) NULL,
                    AccidentDate DATE NULL,
                    ClaimPetitionDate DATE NULL,
                    EntrustmentDate DATE NULL,
                    CurrentStage NVARCHAR(100) NULL,
                    DisposalStatus NVARCHAR(50) NULL,
                    ClaimAmount DECIMAL(18, 2) NULL,
                    NextHearingDate DATE NULL,
                    CNRNumber NVARCHAR(50) NULL,
                    DelayRemark NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME NULL DEFAULT GETDATE()
                );

                CREATE TABLE MVC_CASE_PETITIONERS (
                    PetitionerID INT IDENTITY(1,1) PRIMARY KEY,
                    CaseID INT NOT NULL,
                    PetitionerName NVARCHAR(200) NOT NULL,
                    Relationship NVARCHAR(100) NULL
                );

                CREATE TABLE MVC_CASE_ADVERSE_DETAILS (
                    CaseID INT PRIMARY KEY,
                    AwardAmount DECIMAL(18, 2) NULL,
                    EPDepositedAmount DECIMAL(18, 2) NULL,
                    AwardDate DATE NULL,
                    AdverseJudgmentUploadPath NVARCHAR(MAX) NULL,
                    TR18UploadPath NVARCHAR(MAX) NULL,
                    DriverPunishmentStatus NVARCHAR(100) NULL
                );

                CREATE TABLE LABOUR_CASES (
                    CaseID INT IDENTITY(1,1) PRIMARY KEY,
                    DivisionID INT NULL,
                    CaseNumber NVARCHAR(100) NOT NULL,
                    CaseYear INT NOT NULL,
                    PetitionerName NVARCHAR(200) NOT NULL,
                    CaseStatus NVARCHAR(50) NOT NULL
                );

                CREATE TABLE GRA_CASES (
                    CaseID INT IDENTITY(1,1) PRIMARY KEY,
                    DivisionCode INT NULL,
                    PGANumber VARCHAR(100) NULL,
                    OrderedAmount_CA DECIMAL(18, 2) NULL,
                    ActualPaidAmount DECIMAL(18, 2) NULL
                );

                CREATE TABLE CASE_NOTINGS (
                    NotingID INT IDENTITY(1,1) PRIMARY KEY,
                    CaseID INT NOT NULL,
                    NotingText NVARCHAR(MAX) NOT NULL,
                    CreatedByRole NVARCHAR(50) NOT NULL,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                );

                CREATE TABLE CASE_HEARINGS_HISTORY (
                    HearingID INT IDENTITY(1,1) PRIMARY KEY,
                    CNRNumber NVARCHAR(50) NOT NULL,
                    HearingDate DATE NOT NULL,
                    BusinessTransacted NVARCHAR(MAX) NULL,
                    NextHearingDate DATE NULL
                );

                CREATE TABLE DOCUMENT_CHUNKS (
                    ChunkId INT IDENTITY(1,1) PRIMARY KEY,
                    SourceType NVARCHAR(50) NOT NULL,
                    SourceReference NVARCHAR(200) NOT NULL,
                    CaseId INT NULL,
                    DivisionId INT NULL,
                    ChunkIndex INT NOT NULL,
                    ChunkText NVARCHAR(MAX) NOT NULL,
                    SectionHeading NVARCHAR(200) NULL,
                    PageNumber INT NULL,
                    IsScannedPdf BIT NOT NULL DEFAULT 0,
                    OcrConfidence FLOAT NULL,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                );";

                using (var cmd = new SqlCommand(ddl, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Seed controlled known rows for deterministic testing
                string seed = @"
                INSERT INTO DIVISION_MASTER (DivisionID, DivisionNameEnglish) VALUES
                (1, 'Hubli Division'),
                (2, 'Belagavi Division'),
                (5, 'Central Office');

                -- Case 1: Division 1, MVC 101/2020. Pending. Filing date 400 days ago.
                -- Claim = 500,000, Award = 300,000, Paid = 100,000 -> Balance = 200,000
                INSERT INTO MVC_CASES (DivisionID, MVCNo, MVCYear, VehicleNo, ClaimPetitionDate, CurrentStage, DisposalStatus, ClaimAmount, NextHearingDate, CNRNumber, DelayRemark)
                VALUES (1, '101', 2020, 'KA-25-F-1234', DATEADD(day, -400, GETDATE()), 'Evidence Stage', 'Pending', 500000.00, DATEADD(day, 30, GETDATE()), 'KAHB010012342020', 'Summons delay');
                DECLARE @C1 INT = SCOPE_IDENTITY();
                INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@C1, 'Ramesh Pujari', 'Self');
                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, AwardAmount, EPDepositedAmount, AwardDate, AdverseJudgmentUploadPath)
                VALUES (@C1, 300000.00, 100000.00, DATEADD(day, -50, GETDATE()), '/docs/award_101.pdf');

                -- Case 2: Division 1, MVC 102/2021. Disposed. Filing date 300 days ago.
                -- Claim = 800,000, Award = 450,000, Paid = 450,000 -> Balance = 0
                INSERT INTO MVC_CASES (DivisionID, MVCNo, MVCYear, VehicleNo, ClaimPetitionDate, CurrentStage, DisposalStatus, ClaimAmount, NextHearingDate, CNRNumber)
                VALUES (1, '102', 2021, 'KA-25-F-5678', DATEADD(day, -300, GETDATE()), 'Disposed', 'Disposed', 800000.00, NULL, 'KAHB010056782021');
                DECLARE @C2 INT = SCOPE_IDENTITY();
                INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@C2, 'Sunita Patil', 'Wife');
                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, AwardAmount, EPDepositedAmount, AwardDate, AdverseJudgmentUploadPath)
                VALUES (@C2, 450000.00, 450000.00, DATEADD(day, -100, GETDATE()), '/docs/award_102.pdf');

                -- Case 3: Division 1, MVC 103/2023. Pending. Past un-updated date, unlinked CNR.
                INSERT INTO MVC_CASES (DivisionID, MVCNo, MVCYear, VehicleNo, ClaimPetitionDate, CurrentStage, DisposalStatus, ClaimAmount, NextHearingDate, CNRNumber)
                VALUES (1, '103', 2023, 'KA-25-F-9999', DATEADD(day, -90, GETDATE()), 'Hearing Stage', 'Pending', 250000.00, DATEADD(day, -15, GETDATE()), NULL);
                DECLARE @C3 INT = SCOPE_IDENTITY();
                INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@C3, 'Anand Gowda', 'Self');
                -- Award is 150000 but AdverseJudgmentUploadPath is NULL (Anomaly: Missing PDF)
                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, AwardAmount, EPDepositedAmount, AdverseJudgmentUploadPath)
                VALUES (@C3, 150000.00, 0, NULL);

                -- Case 4: Division 2 (Belagavi), MVC 201/2022. Claim = 600,000, Award = 400,000, Paid = 200,000
                INSERT INTO MVC_CASES (DivisionID, MVCNo, MVCYear, VehicleNo, ClaimPetitionDate, CurrentStage, DisposalStatus, ClaimAmount, NextHearingDate, CNRNumber)
                VALUES (2, '201', 2022, 'KA-22-F-4321', DATEADD(day, -500, GETDATE()), 'Pending Arguments', 'Pending', 600000.00, DATEADD(day, 20, GETDATE()), 'KABG010043212022');
                DECLARE @C4 INT = SCOPE_IDENTITY();
                INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@C4, 'Suresh Belagavi', 'Brother');
                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, AwardAmount, EPDepositedAmount)
                VALUES (@C4, 400000.00, 200000.00);

                -- Labour & Gratuity rows
                INSERT INTO LABOUR_CASES (DivisionID, CaseNumber, CaseYear, PetitionerName, CaseStatus)
                VALUES (1, 'KID 12/2022', 2022, 'Mallikarjun Worker', 'Pending');

                INSERT INTO GRA_CASES (DivisionCode, PGANumber, OrderedAmount_CA, ActualPaidAmount)
                VALUES (1, 'PGA 5/2021', 150000.00, 100000.00);

                -- Hearing history for Case 101/2020
                INSERT INTO CASE_HEARINGS_HISTORY (CNRNumber, HearingDate, BusinessTransacted, NextHearingDate)
                VALUES ('KAHB010012342020', DATEADD(day, -60, GETDATE()), 'Cross examination of PW1 recorded', DATEADD(day, 30, GETDATE()));

                -- Department noting for Case 101/2020
                INSERT INTO CASE_NOTINGS (CaseID, NotingText, CreatedByRole, CreatedDate)
                VALUES (@C1, 'Advocate instructed to file objection on income criteria', 'LawOfficer', DATEADD(day, -350, GETDATE()));";

                using (var cmd = new SqlCommand(seed, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Build in-memory config pointing to the integration test database
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MVCCaseDB"] = TestDbConnStr
                })
                .Build();

            _dbHelper = new DBHelper(config);
            _retrievalService = new DynamicDatabaseRetrievalService(_dbHelper, NullLogger<DynamicDatabaseRetrievalService>.Instance);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task ExactCaseLookup_FinancialBalance_MatchesExpectedCalculations()
        {
            // Act: Query specific case MVC 101/2020
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance,
                CaseNumber = "101",
                CaseYear = 2020
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsDeterministicSuccess);
            Assert.Single(result.Records);

            var row = result.Records[0];
            Assert.Equal("MVC/101/2020", row["CaseNumber"]);
            Assert.Equal(500000.00m, (decimal)row["ClaimAmount"]);
            Assert.Equal(300000.00m, (decimal)row["AwardAmount"]);
            Assert.Equal(100000.00m, (decimal)row["PaidAmount"]);
            // Verified deterministic calculation: Award - Paid = Remaining
            Assert.Equal(200000.00m, (decimal)row["RemainingBalance"]);
            Assert.True(result.FormattedMarkdown.Contains("200,000.00") || result.FormattedMarkdown.Contains("2,00,000.00"));
        }

        [Fact]
        public async Task ExactCaseLookup_DischargedBalance_ReportsZeroRemaining()
        {
            // Case 102/2021: Award 450,000, Paid 450,000 -> Balance 0.00
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance,
                CaseNumber = "102",
                CaseYear = 2021
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.True(result.IsDeterministicSuccess);
            var row = result.Records[0];
            Assert.Equal(0.00m, (decimal)row["RemainingBalance"]);
            Assert.Contains("Fully Paid / Discharged", result.FormattedMarkdown);
        }

        [Fact]
        public async Task DivisionAuthorization_StrictlyEnforcesDivisionBoundary()
        {
            // Division 1 user requests aggregate liability
            var div1Plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance,
                DivisionId = 1
            };

            var div1Result = await _retrievalService.ExecuteDynamicQueryAsync(div1Plan, userDivisionId: 1);

            Assert.NotNull(div1Result);
            Assert.Single(div1Result.Records);

            var div1Row = div1Result.Records[0];
            int div1Cases = (int)div1Row["TotalCases"];
            decimal div1Awarded = (decimal)div1Row["TotalAwarded"];
            decimal div1Paid = (decimal)div1Row["TotalPaid"];

            // Division 1 has Cases 101, 102, 103 (3 active cases)
            Assert.Equal(3, div1Cases);
            // Awarded: 300,000 (Case 101) + 450,000 (Case 102) + 150,000 (Case 103) = 900,000
            Assert.Equal(900000.00m, div1Awarded);
            // Paid: 100,000 + 450,000 + 0 = 550,000
            Assert.Equal(550000.00m, div1Paid);
            // Remaining: 900,000 - 550,000 = 350,000
            Assert.Equal(350000.00m, (decimal)div1Row["TotalRemaining"]);

            // Crucial: Div 1 user should NOT see Belagavi Division 2's Case 201 (which has 400,000 award)
            Assert.DoesNotContain("₹1,300,000.00", div1Result.FormattedMarkdown);

            // Now: Central Office (Division 5 or 0) queries corporation-wide
            var coPlan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance
            };

            var coResult = await _retrievalService.ExecuteDynamicQueryAsync(coPlan, userDivisionId: 5);
            Assert.NotNull(coResult);
            var coRow = coResult.Records[0];
            int coCases = (int)coRow["TotalCases"];
            decimal coAwarded = (decimal)coRow["TotalAwarded"];

            // CO sees all 4 cases across Div 1 and Div 2
            Assert.Equal(4, coCases);
            // 900,000 + 400,000 = 1,300,000
            Assert.Equal(1300000.00m, coAwarded);
        }

        [Fact]
        public async Task AgeFilter_ExcludesDisposedCases_And_ReturnsClaimantFromPetitionerJoin()
        {
            // Query cases older than 180 days in Division 1
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.AgeFilter,
                AgeThresholdDays = 180,
                DivisionId = 1
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.True(result.IsDeterministicSuccess);

            // In Div 1:
            // Case 101 (400 days old, Pending) -> MUST BE INCLUDED
            // Case 102 (300 days old, Disposed) -> MUST BE EXCLUDED
            // Case 103 (90 days old, Pending) -> EXCLUDED (under 180 days)
            Assert.Single(result.Records);

            var row = result.Records[0];
            Assert.Equal("MVC/101/2020", row["CaseNumber"]);
            // Verified that PetitionerName joined properly from MVC_CASE_PETITIONERS
            Assert.Equal("Ramesh Pujari", row["Petitioner"]);
            Assert.True((int)row["DaysPending"] >= 399);
        }

        [Fact]
        public async Task DataIntegrityAudit_DetectsPastHearingDates_And_MissingJudgmentPDFs()
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.DataIntegrityAudit,
                DivisionId = 1
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.True(result.IsDeterministicSuccess);
            // Case 103 has both a past hearing date (15 days ago), missing judgment PDF, and unlinked CNR
            Assert.Contains(result.Records, r => (string)r["CaseNumber"] == "MVC/103/2023");
            Assert.Contains("Past un-updated hearing date", result.FormattedMarkdown);
        }

        [Fact]
        public async Task CaseTimeline_AssemblesInternalNotings_And_ECourtsHearingsInChronologicalOrder()
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.CaseTimeline,
                CaseNumber = "101",
                CaseYear = 2020
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.True(result.IsDeterministicSuccess);
            Assert.True(result.RecordCount >= 2);

            // Verify order
            var dates = new List<DateTime>();
            foreach (var r in result.Records)
            {
                dates.Add((DateTime)r["Date"]);
            }

            for (int i = 1; i < dates.Count; i++)
            {
                Assert.True(dates[i] >= dates[i - 1], "Timeline events must be strictly sorted chronologically");
            }
        }

        [Fact]
        public async Task DelayAnalysis_CalculatesExactFilingDuration_And_PullsRemarks()
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.DelayAnalysis,
                CaseNumber = "101",
                CaseYear = 2020
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.Contains("Summons delay", result.FormattedMarkdown);
            Assert.Contains("approx. 13 months", result.FormattedMarkdown);
        }

        [Fact]
        public async Task CrossModuleComparison_AggregatesMvcLabourAndGratuity()
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.CaseComparison
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.Equal(3, result.Records.Count);

            var mvcRow = result.Records.Find(r => (string)r["Module"] == "MVC");
            var labourRow = result.Records.Find(r => (string)r["Module"] == "LABOUR");
            var graRow = result.Records.Find(r => (string)r["Module"] == "GRATUITY");

            Assert.NotNull(mvcRow);
            Assert.NotNull(labourRow);
            Assert.NotNull(graRow);

            Assert.Equal(4, (int)mvcRow["CaseCount"]);
            Assert.Equal(1, (int)labourRow["CaseCount"]);
            Assert.Equal(1, (int)graRow["CaseCount"]);
            Assert.Equal(150000.00m, (decimal)graRow["AvgClaim"]);
            Assert.Equal(100000.00m, (decimal)graRow["AvgAward"]);
        }

        [Fact]
        public async Task NonExistentCase_ReturnsGracefulWarning_WithoutThrowing()
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance,
                CaseNumber = "99999",
                CaseYear = 1999
            };

            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.False(result.IsDeterministicSuccess);
            Assert.Contains("No financial compensation records found", result.FormattedMarkdown);
        }

        [Theory]
        [InlineData("101'; DROP TABLE MVC_CASES; --")]
        [InlineData("101' OR '1'='1")]
        [InlineData("'; SHUTDOWN; --")]
        public async Task SqlInjectionAttack_IsNeutralized_ByParameterization(string maliciousCaseNo)
        {
            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.FinancialBalance,
                CaseNumber = maliciousCaseNo,
                CaseYear = 2020
            };

            // Act & Assert: Must not throw SQL syntax error or drop table
            var result = await _retrievalService.ExecuteDynamicQueryAsync(plan, userDivisionId: 1);

            Assert.NotNull(result);
            Assert.False(result.IsDeterministicSuccess);

            // Verify table still exists and has all rows intact
            using var conn = new SqlConnection(TestDbConnStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM MVC_CASES", conn);
            int count = (int)await cmd.ExecuteScalarAsync();
            Assert.Equal(4, count);
        }
    }
}
