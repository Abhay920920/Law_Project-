using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;
using MVCCaseManagement.Services.AI;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class NyayaPathaPettyBillRAGTests
    {
        private readonly QueryRouterService _router;

        public NyayaPathaPettyBillRAGTests()
        {
            _router = new QueryRouterService(NullLogger<QueryRouterService>.Instance);
        }

        [Theory]
        [InlineData("Has the petty bill been generated for MVC 465/2017?", "465", 2017)]
        [InlineData("Show the bill details and approval reference.", null, null)]
        [InlineData("What is the gross and net payable after TDS?", null, null)]
        [InlineData("Which payments were cancelled?", null, null)]
        [InlineData("How much remains outstanding?", null, null)]
        [InlineData("List all bills and payments linked to a case", null, null)]
        [InlineData("Show details for PB-15", null, null)]
        public void QueryRouter_RoutesPettyBillQueries_ToPettyBillSettlementIntent(string query, string? expectedCaseNo, int? expectedYear)
        {
            var route = _router.RouteQuery(query);

            Assert.Equal(LegalQueryIntent.PettyBillSettlement, route.Plan.Intent);
            Assert.True(route.Plan.IsDeterministicDatabaseQuery);
            Assert.Equal("PettyBill", route.Plan.SpecificField);
            Assert.Equal(QuerySourceCategory.InternalDatabase, route.PrimaryCategory);

            if (expectedCaseNo != null)
            {
                Assert.Equal(expectedCaseNo, route.ExtractedCaseNumber);
                Assert.Equal(expectedYear, route.ExtractedYear);
            }
        }

        [Fact]
        public void QueryRouter_ExtractsPettyBillId_Accurately()
        {
            var route = _router.RouteQuery("Show details for PB-42 and approval letter");
            Assert.Equal(42, route.Plan.PettyBillId);
            Assert.Equal(LegalQueryIntent.PettyBillSettlement, route.Plan.Intent);
        }

        [Theory]
        [InlineData("MVC 465/2017 ಗೆ ಪೆಟ್ಟಿ ಬಿಲ್ ರಚಿಸಲಾಗಿದೆಯೇ?")]
        [InlineData("ಪೆಟ್ಟಿ ಬಿಲ್ ವಿವರಗಳು ಮತ್ತು ಅನುಮೋದನೆ ಪತ್ರ ತೋರಿಸಿ")]
        [InlineData("ಟಿಡಿಎಸ್ ನಂತರ ನಿವ್ವಳ ಪಾವತಿ ಎಷ್ಟು?")]
        [InlineData("ರದ್ದಾದ ಚೆಕ್ ಅಥವಾ ಪಾವತಿಗಳು ಯಾವುವು?")]
        public void QueryRouter_RoutesKannadaPettyBillQueries(string kannadaQuery)
        {
            var route = _router.RouteQuery(kannadaQuery);

            Assert.Equal(LegalQueryIntent.PettyBillSettlement, route.Plan.Intent);
            Assert.True(route.Plan.IsDeterministicDatabaseQuery);
        }

        [Fact]
        public void PettyBillAnswer_GeneratesDeterministicMarkdown_WithExactCalculationFormulas()
        {
            var bill = new PettyBillViewModel
            {
                BillID = 15,
                MVCNo = "465",
                MVCYear = 2017,
                MACTName = "MACT Dharwad",
                DivisionName = "Dharwad",
                VehicleNo = "KA-25-F-1234",
                PetitionerName = "Ramesh Patil",
                ApprovalLetterNo = "NWKRTC/LAW/2024/789",
                ApprovalDate = new DateTime(2024, 5, 20),
                BillDate = new DateTime(2024, 5, 25),
                FileRefNo = "FILE-DWR-2024-465",
                EnhancedAmount = 150000m,
                CourtCost = 1500m,
                TDSPercentage = 10m,
                InterestEntries = new List<InterestEntry>
                {
                    new() { FromDate = new DateTime(2017, 1, 1), ToDate = new DateTime(2019, 1, 1), Amount = 18000m }
                },
                PreviousPayments = new List<PreviousPayment>
                {
                    new() { PaymentID = 1, ChequeNumber = "CHQ-1001", ChequeDate = new DateTime(2018, 6, 1), Amount = 25000m, IsSelected = true, IsCancelled = false, Remarks = "Interim deposit" },
                    new() { PaymentID = 2, ChequeNumber = "CHQ-1002", ChequeDate = new DateTime(2019, 2, 1), Amount = 10000m, IsSelected = true, IsCancelled = true, Remarks = "Bounced / Cancelled" }
                }
            };

            bill.TotalInterest = 18000m;
            bill.GrossPayable = bill.EnhancedAmount + bill.TotalInterest + bill.CourtCost; // 169500
            bill.TDSAmount = bill.TotalInterest * (bill.TDSPercentage / 100m); // 1800
            bill.TotalPreviousPayments = 25000m; // only selected & NOT cancelled
            bill.TotalDeductions = bill.TotalPreviousPayments + bill.TDSAmount; // 26800
            bill.NetPayable = bill.GrossPayable - bill.TotalDeductions; // 142700

            var dossier = new CaseDossier
            {
                CaseType = "MVC",
                CaseNumber = "MVC/465/2017",
                CourtName = "MACT Dharwad",
                Petitioner = "Ramesh Patil",
                Respondent = "NWKRTC",
                VehicleNo = "KA-25-F-1234",
                PettyBills = new List<PettyBillViewModel> { bill }
            };

            var plan = new LegalQueryPlan
            {
                Intent = LegalQueryIntent.PettyBillSettlement,
                CaseNumber = "465",
                CaseYear = 2017
            };

            string answer = UnifiedLegalResearchService.GeneratePettyBillAnswer(dossier, plan, "what is the gross and net payable after tds?");

            Assert.Contains("#PB-15", answer);
            Assert.Contains("NWKRTC/LAW/2024/789", answer);
            Assert.Contains("FILE-DWR-2024-465", answer);
            Assert.True(answer.Contains("169,500.00") || answer.Contains("1,69,500.00")); // Gross Payable
            Assert.True(answer.Contains("1,800.00"));  // TDS
            Assert.True(answer.Contains("25,000.00")); // Previous Payments
            Assert.True(answer.Contains("26,800.00")); // Total Deductions
            Assert.True(answer.Contains("142,700.00") || answer.Contains("1,42,700.00")); // Net Payable
            Assert.Contains("Cancelled (Excluded from deductions)", answer); // Cancelled payment status
            Assert.Contains("CHQ-1001", answer);
            Assert.Contains("CHQ-1002", answer);
        }

        [Fact]
        public void PettyBillAnswer_HandlesMultipleBills_WithoutDuplicatingTotals()
        {
            var bill1 = new PettyBillViewModel
            {
                BillID = 1,
                MVCNo = "465",
                MVCYear = 2017,
                ApprovalLetterNo = "APP-1",
                EnhancedAmount = 100000m,
                TotalInterest = 10000m,
                CourtCost = 500m,
                GrossPayable = 110500m,
                TDSPercentage = 10m,
                TDSAmount = 1000m,
                TotalPreviousPayments = 0m,
                TotalDeductions = 1000m,
                NetPayable = 109500m
            };

            var bill2 = new PettyBillViewModel
            {
                BillID = 2,
                MVCNo = "465",
                MVCYear = 2017,
                ApprovalLetterNo = "APP-2",
                EnhancedAmount = 50000m,
                TotalInterest = 5000m,
                CourtCost = 200m,
                GrossPayable = 55200m,
                TDSPercentage = 10m,
                TDSAmount = 500m,
                TotalPreviousPayments = 0m,
                TotalDeductions = 500m,
                NetPayable = 54700m
            };

            var dossier = new CaseDossier
            {
                CaseType = "MVC",
                CaseNumber = "MVC/465/2017",
                PettyBills = new List<PettyBillViewModel> { bill1, bill2 }
            };

            string answer = UnifiedLegalResearchService.GeneratePettyBillAnswer(dossier, null, "list all bills");

            Assert.Contains("Multiple Petty Bills Linked", answer);
            Assert.Contains("#PB-1", answer);
            Assert.Contains("#PB-2", answer);
            Assert.True(answer.Contains("109,500.00") || answer.Contains("1,09,500.00"));
            Assert.True(answer.Contains("54,700.00"));
        }

        [Fact]
        public void PettyBillAnswer_HandlesNonexistentBill_ProducesClearWarningWithoutFabrication()
        {
            var dossier = new CaseDossier
            {
                CaseType = "MVC",
                CaseNumber = "MVC/999/2020",
                CourtName = "MACT Belagavi",
                CurrentStage = "Arguments",
                PettyBills = new List<PettyBillViewModel>()
            };

            string answer = UnifiedLegalResearchService.GeneratePettyBillAnswer(dossier, null, "has petty bill been generated for MVC 999/2020?");

            Assert.Contains("No Petty Bill Generated", answer);
            Assert.Contains("MVC/999/2020", answer);
            Assert.Contains("Zero Fabrication", answer);
        }

        [Fact]
        public void Security_DivisionIsolation_EnforcesDivisionalAccessControl()
        {
            // Division 6 user querying Division 2 -> DENIED
            var planOtherDiv = new LegalQueryPlan { DivisionId = 2 };
            bool authorizedOther = UnifiedLegalResearchService.IsAuthorized(6, "Officer", planOtherDiv);
            Assert.False(authorizedOther);

            // Division 6 user querying Division 6 -> ALLOWED
            var planSameDiv = new LegalQueryPlan { DivisionId = 6 };
            bool authorizedSame = UnifiedLegalResearchService.IsAuthorized(6, "Officer", planSameDiv);
            Assert.True(authorizedSame);

            // Central Office user (Division 5) querying Division 2 -> ALLOWED
            bool authorizedCO = UnifiedLegalResearchService.IsAuthorized(5, "Officer", planOtherDiv);
            Assert.True(authorizedCO);

            // Admin / CLO role querying any division -> ALLOWED
            bool authorizedAdmin = UnifiedLegalResearchService.IsAuthorized(6, "Admin", planOtherDiv);
            Assert.True(authorizedAdmin);
            bool authorizedCLO = UnifiedLegalResearchService.IsAuthorized(6, "CLO", planOtherDiv);
            Assert.True(authorizedCLO);
        }

        [Fact]
        public void SQLInjection_SafeHandling_InQueryRouter()
        {
            string maliciousQuery = "Has petty bill been generated for MVC 465/2017'; DROP TABLE PETTY_BILLS;--";
            var route = _router.RouteQuery(maliciousQuery);

            Assert.Equal(LegalQueryIntent.PettyBillSettlement, route.Plan.Intent);
            Assert.Equal("465", route.ExtractedCaseNumber);
            Assert.Equal(2017, route.ExtractedYear);
        }
    }
}
