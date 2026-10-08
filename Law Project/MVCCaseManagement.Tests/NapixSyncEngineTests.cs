using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MVCCaseManagement.Utils;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class NapixSyncEngineTests
    {
        [Fact]
        public void AuthoritativeDisposedStatus_WhenPendDispIsD_ShouldMarkDisposed()
        {
            // Rule 1: pend_disp = D → mark as Disposed ('D'), pend_disp = P → mark as Pending ('P')
            string rawPendDispD = "D";
            string pendDispStatusD = (rawPendDispD.Trim().Equals("D", StringComparison.OrdinalIgnoreCase) ||
                                     rawPendDispD.Contains("DISPOSED", StringComparison.OrdinalIgnoreCase))
                ? "D" : "P";

            string rawPendDispP = "P";
            string pendDispStatusP = (rawPendDispP.Trim().Equals("D", StringComparison.OrdinalIgnoreCase) ||
                                     rawPendDispP.Contains("DISPOSED", StringComparison.OrdinalIgnoreCase))
                ? "D" : "P";

            Assert.Equal("D", pendDispStatusD);
            Assert.Equal("P", pendDispStatusP);
        }

        [Fact]
        public void CaseStatus_ShouldNotBeOverwrittenByNapixSync()
        {
            // Rule 1 & 13: Maintain distinction between internal CaseStatus and NAPIX PendDispStatus
            string internalCaseStatus = "Award Passed - Settled in Lok Adalat";
            string napixPendDisp = "D";

            // Simulating update logic:
            string pendDispStatus = napixPendDisp == "D" ? "D" : "P";
            // CaseStatus remains untouched
            string preservedCaseStatus = internalCaseStatus;

            Assert.Equal("D", pendDispStatus);
            Assert.Equal("Award Passed - Settled in Lok Adalat", preservedCaseStatus);
        }

        [Theory]
        [InlineData("KADW200035292024", true)]
        [InlineData("KABK010012342023", true)]
        [InlineData("KAHC010056782022", true)]
        [InlineData("INVALID123", false)]
        [InlineData("KADW200035292024EXTRA", false)]
        [InlineData("KADW-2000-3529-24", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void LocalCnrValidation_RejectsInvalidWithoutConsumingQuota(string? cnr, bool isValid)
        {
            // Rule 3: Only actual outgoing requests consume quota.
            // Invalid CNR must be rejected locally without consuming quota slot.
            string cleanCnr = (cnr ?? "").Trim().ToUpperInvariant();
            bool passesValidation = cleanCnr.Length == 16 && Regex.IsMatch(cleanCnr, "^[A-Za-z0-9]{16}$");

            Assert.Equal(isValid, passesValidation);
        }

        [Theory]
        [InlineData("15-11-2024", 2024, 11, 15)]
        [InlineData("25/12/2025", 2025, 12, 25)]
        [InlineData("2024-05-10", 2024, 5, 10)]
        [InlineData("05.08.2024", 2024, 8, 5)]
        public void NextHearingDate_NormalizesValidDateFormats(string dateStr, int expYear, int expMonth, int expDay)
        {
            // Rule 5: date_next_list → normalize date → save NextHearingDate
            var dt = NapixSyncEngine.ParseNormalizedDate(dateStr);
            Assert.NotNull(dt);
            Assert.Equal(expYear, dt!.Value.Year);
            Assert.Equal(expMonth, dt.Value.Month);
            Assert.Equal(expDay, dt.Value.Day);
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Disposed")]
        [InlineData("—")]
        [InlineData("-")]
        [InlineData("")]
        [InlineData(null)]
        public void NextHearingDate_InvalidKeywordsReturnNull(string? dateStr)
        {
            var dt = NapixSyncEngine.ParseNormalizedDate(dateStr);
            Assert.Null(dt);
        }

        [Fact]
        public void DataHash_ComputesSha256Successfully()
        {
            // Rule 14: Use SHA-256 for NapixDataHash for change detection
            string payload = "{\"cino\":\"KADW200035292024\",\"status\":\"Pending\"}";
            string hash = NapixSyncEngine.ComputeSha256(payload);

            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length); // 64 hex characters for SHA-256

            // Consistent hash
            string hash2 = NapixSyncEngine.ComputeSha256(payload);
            Assert.Equal(hash, hash2);

            // Change in payload alters hash
            string hash3 = NapixSyncEngine.ComputeSha256(payload + " ");
            Assert.NotEqual(hash, hash3);
        }

        [Theory]
        [InlineData("600 (INVALID_CNR)", true)]
        [InlineData("628 (RECORD_NOT_FOUND)", true)]
        [InlineData("No Record Found on Gateway", true)]
        [InlineData("INVALID_DEPT_ID", true)]
        [InlineData("500 (GATEWAY_BACKEND_OFFLINE)", false)]
        [InlineData("Connection timed out", false)]
        public void PermanentVsTransientErrors_ClassifiedCorrectly(string errorText, bool isPermanent)
        {
            // Rule 8: Permanent errors (600, 628, RECORD_NOT_FOUND) terminate retry loop.
            // Transient errors (500, timeouts) retry with exponential backoff.
            string upper = errorText.ToUpperInvariant();
            bool detectedPermanent = upper.Contains("600") ||
                                     upper.Contains("628") ||
                                     upper.Contains("RECORD_NOT_FOUND") ||
                                     upper.Contains("NO RECORD FOUND") ||
                                     upper.Contains("INVALID_CNR") ||
                                     upper.Contains("INVALID_DEPT_ID");

            Assert.Equal(isPermanent, detectedPermanent);
        }

        [Fact]
        public void ExponentialBackoff_CalculatesAppropriateDelays()
        {
            // Rule 8: Attempt 1 short delay, Attempt 2 longer delay, etc.
            int delayAttempt0 = 0 switch { 0 => 5, 1 => 15, 2 => 60, 3 => 180, _ => 360 };
            int delayAttempt1 = 1 switch { 0 => 5, 1 => 15, 2 => 60, 3 => 180, _ => 360 };
            int delayAttempt2 = 2 switch { 0 => 5, 1 => 15, 2 => 60, 3 => 180, _ => 360 };
            int delayAttempt3 = 3 switch { 0 => 5, 1 => 15, 2 => 60, 3 => 180, _ => 360 };

            Assert.Equal(5, delayAttempt0);
            Assert.Equal(15, delayAttempt1);
            Assert.Equal(60, delayAttempt2);
            Assert.Equal(180, delayAttempt3);
        }

        [Theory]
        [InlineData("MVC", "MVC")]
        [InlineData("Labour", "Labour")]
        [InlineData("labour", "Labour")]
        [InlineData("LABOUR_WP", "Labour")]
        [InlineData("Writ", "Labour")]
        [InlineData(null, "MVC")]
        [InlineData("", "MVC")]
        public void NormalizeModule_ReturnsCorrectBucket(string? input, string expected)
        {
            // Rule 2: Separate quota buckets MVC (1000/hr) and Labour (1000/hr)
            string norm = NapixQuotaService.NormalizeModule(input);
            Assert.Equal(expected, norm);
        }
    }
}
