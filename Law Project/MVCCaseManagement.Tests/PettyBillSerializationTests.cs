using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Tests
{
    public class PettyBillSerializationTests
    {
        [Fact]
        public void PettyBill_Payload_Deserialization_HandlesCleanJsonSuccessfully()
        {
            string json = """
            {
                "BillID": 0,
                "CaseID": 101,
                "MVCNo": "465",
                "MVCYear": 2017,
                "MACTName": "MACT Bangalore",
                "DivisionName": "Bangalore Division",
                "VehicleNo": "KA-01-AB-1234",
                "AccidentDate": "2016-05-10",
                "PetitionerName": "Ramesh Kumar",
                "AppealNumber": null,
                "ApprovalLetterNo": "APP-2024-001",
                "ApprovalDate": null,
                "FileRefNo": "NWKRT/2024/LAW/465",
                "BillDate": "2026-10-13",
                "EnhancedAmount": 150000.00,
                "BaseAwardAmount": 0,
                "AwardType": "Lower Court",
                "CourtCost": 500.00,
                "TDSPercentage": 10,
                "IsRelaxationApplied": false,
                "RelaxationFrom": null,
                "RelaxationTo": null,
                "InterestEntries": [
                    {
                        "FromDate": "2017-01-01",
                        "ToDate": "2018-01-01",
                        "Amount": 9000.00
                    }
                ],
                "PreviousPayments": [
                    {
                        "PaymentID": 0,
                        "IsSelected": true,
                        "IsCancelled": false,
                        "ChequeNumber": "CHQ12345",
                        "ChequeDate": null,
                        "Amount": 25000.00,
                        "Remarks": "Interim Deposit"
                    }
                ],
                "TotalInterest": 9000.00,
                "GrossPayable": 159500.00,
                "TDSAmount": 900.00,
                "TotalPreviousPayments": 25000.00,
                "TotalDeductions": 25900.00,
                "NetPayable": 133600.00
            }
            """;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var model = JsonSerializer.Deserialize<PettyBillViewModel>(json, options);

            Assert.NotNull(model);
            Assert.Equal("465", model!.MVCNo);
            Assert.Equal(2017, model.MVCYear);
            Assert.Equal(150000.00m, model.EnhancedAmount);
            Assert.Single(model.InterestEntries);
            Assert.Equal(9000.00m, model.InterestEntries[0].Amount);
            Assert.Single(model.PreviousPayments);
            Assert.Null(model.PreviousPayments[0].ChequeDate);
            Assert.Equal(25000.00m, model.PreviousPayments[0].Amount);
            Assert.Equal(133600.00m, model.NetPayable);
        }

        [Fact]
        public void PettyBill_NetPayableFormula_MatchesStatutoryLogic()
        {
            decimal enhancedAmount = 200000m;
            decimal totalInterest = 24000m;
            decimal courtCost = 1500m;
            decimal tdsPercent = 10m;
            decimal previousPayments = 50000m;

            decimal grossPayable = enhancedAmount + totalInterest + courtCost;
            decimal tdsAmount = totalInterest * (tdsPercent / 100m);
            decimal totalDeductions = previousPayments + tdsAmount;
            decimal netPayable = grossPayable - totalDeductions;

            Assert.Equal(225500m, grossPayable);
            Assert.Equal(2400m, tdsAmount);
            Assert.Equal(52400m, totalDeductions);
            Assert.Equal(173100m, netPayable);
        }
    }
}
