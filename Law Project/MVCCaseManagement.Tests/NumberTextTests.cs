using Xunit;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Tests
{
    public class NumberTextTests
    {
        [Fact]
        public void Convert_Zero_ReturnsRupeesZeroOnly()
        {
            var result = NumberText.Convert(0);
            Assert.Equal("Rupees Zero Only", result);
        }

        [Theory]
        [InlineData(100, "Rupees One Hundred Only")]
        [InlineData(1500, "Rupees One Thousand Five Hundred Only")]
        [InlineData(100000, "Rupees One Lakh Only")]
        [InlineData(10000000, "Rupees One Crore Only")]
        public void Convert_WholeNumbers_FormatsCorrectly(decimal amount, string expected)
        {
            var result = NumberText.Convert(amount);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_WithPaise_FormatsDecimalCorrectly()
        {
            var result = NumberText.Convert(500.50m);
            Assert.Contains("Paise", result);
            Assert.StartsWith("Rupees Five Hundred and", result);
        }
    }
}
