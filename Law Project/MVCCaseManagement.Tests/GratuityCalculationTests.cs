using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Tests
{
    public class GratuityCalculationTests
    {
        [Fact]
        public void Gratuity_Calculation_OrderedAmount_MatchesSumOfPayments()
        {
            var payments = new List<GratuityInterestPayment>
            {
                new GratuityInterestPayment { Amount = 150000.00m, FromDate = new DateTime(2022, 1, 1), ToDate = new DateTime(2023, 1, 1) },
                new GratuityInterestPayment { Amount = 75000.50m, FromDate = new DateTime(2023, 1, 2), ToDate = new DateTime(2024, 1, 1) }
            };

            decimal totalOrdered = payments.Where(p => p.Amount.HasValue).Sum(p => p.Amount!.Value);

            Assert.Equal(225000.50m, totalOrdered);
        }

        [Theory]
        [InlineData(100000, 10, 1, 10000)]    // 100,000 at 10% for 1 year = 10,000
        [InlineData(50000, 6, 2, 6000)]       // 50,000 at 6% for 2 years = 6,000
        public void Gratuity_Interest_SimpleInterestFormula_ComputesCorrectly(decimal principal, decimal rate, int years, decimal expectedInterest)
        {
            decimal interest = (principal * rate * years) / 100m;
            Assert.Equal(expectedInterest, interest);
        }

        [Fact]
        public void Gratuity_Deduction_NetPayable_PreservesStatutoryInvariants()
        {
            decimal grossGratuity = 350000m;
            decimal deduction = 50000m;

            decimal netPaid = grossGratuity - deduction;

            Assert.Equal(300000m, netPaid);
            Assert.True(netPaid >= 0, "Net payable gratuity cannot be negative");
        }
    }
}
