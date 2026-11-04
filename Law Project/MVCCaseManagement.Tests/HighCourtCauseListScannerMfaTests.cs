using System;
using System.Collections.Generic;
using System.Linq;
using MVCCaseManagement.Services.ECourts;
using MVCCaseManagement.Utils;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class HighCourtCauseListScannerMfaTests
    {
        [Fact]
        public void ParseHighCourtCauseListText_Extracts_NWKRTC_MFA_Cases_Accurately()
        {
            // Sample roster snippet representing real Dharwad Bench (KAHC02) Court Hall 9 causes
            string causeListSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 9
Cause List No. 1
Sl.No. Case No. Parties / Advocates

17 MFA 100245/2023 (TCR MVC NO.332/2018 TO 334/2018) (MV, I )
PET: SRI. MUKTIYARAHMAD S/O GUDUSAB INAMDAR
RES: THE MANAGING DIRECTOR, NWKRTC
S C BHUTI FOR RESPONDENT

3 MFA 100069/2023 (TCR MVC 87/2020) (MV, I )
PET: THE DIVISIONAL CONTROLLER
RES: SRI. RAMAPPA AND OTHERS
MADANMOHAN M KHANNUR FOR PETITIONER

47 MFA 100385/2021 (MV, D )
PET: SHRI. HUSENSAB S/O IMAMSAB
RES: THE DIVISIONAL CONTROLLER AND ANOTHER
VAISHALI K KALADAGI FOR R1 & R2

34 MFA 100523/2024 (MV, I )
PET: ABUDUL MUNAF S/O RAJESAB
RES: THE MANAGING DIRECTOR AND OTHERS
HANAMANTREDDY SAHUKAR FOR R1
";

            var targetDate = new DateTime(2026, 11, 3);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                causeListSnippet,
                estCode: "KAHC02",
                benchId: "3629",
                judgeName: "JUSTICE S.V.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.NotNull(results);
            Assert.Equal(4, results.Count);

            // Case 1: Sl. 17 MFA 100245/2023
            var c1 = results.FirstOrDefault(x => x.RegNumber == "100245");
            Assert.NotNull(c1);
            Assert.Equal("MFA", c1.CaseType);
            Assert.Equal("2023", c1.RegYear);
            Assert.Contains("MUKTIYARAHMAD", c1.PetitionerName);
            Assert.Contains("MANAGING DIRECTOR", c1.RespondentName);
            Assert.Equal("Against NWKRTC", c1.NwkrtcRole);
            Assert.Equal("(MV, I )", c1.SubjectCategory);
            Assert.Equal("S C BHUTI", c1.Advocate);

            // Case 2: Sl. 3 MFA 100069/2023
            var c2 = results.FirstOrDefault(x => x.RegNumber == "100069");
            Assert.NotNull(c2);
            Assert.Equal("MFA", c2.CaseType);
            Assert.Equal("2023", c2.RegYear);
            Assert.Contains("DIVISIONAL CONTROLLER", c1.RespondentName == null ? "" : c2.PetitionerName);
            Assert.Equal("By NWKRTC", c2.NwkrtcRole);
            Assert.Equal("(MV, I )", c2.SubjectCategory);
            Assert.Equal("MADANMOHAN M KHANNUR", c2.Advocate);

            // Case 3: Sl. 47 MFA 100385/2021
            var c3 = results.FirstOrDefault(x => x.RegNumber == "100385");
            Assert.NotNull(c3);
            Assert.Equal("MFA", c3.CaseType);
            Assert.Equal("2021", c3.RegYear);
            Assert.Equal("Against NWKRTC", c3.NwkrtcRole);
            Assert.Equal("(MV, D )", c3.SubjectCategory);
            Assert.Equal("VAISHALI K KALADAGI", c3.Advocate);

            // Case 4: Sl. 34 MFA 100523/2024
            var c4 = results.FirstOrDefault(x => x.RegNumber == "100523");
            Assert.NotNull(c4);
            Assert.Equal("MFA", c4.CaseType);
            Assert.Equal("2024", c4.RegYear);
            Assert.Equal("Against NWKRTC", c4.NwkrtcRole);
            Assert.Equal("(MV, I )", c4.SubjectCategory);
            Assert.Equal("HANAMANTREDDY SAHUKAR", c4.Advocate);
        }

        [Fact]
        public void ParseHighCourtCauseListText_Strictly_Eliminates_Revenue_And_Land_Disputes()
        {
            string landRevenueSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 4
12 WP 105000/2024 (KLR, RES )
PET: SHRI. BASAVARAJ
RES: THE DEPUTY COMMISSIONER AND MANAGEMENT OF NWKRTC
GOVT PLEADER

14 WP 102000/2024 (LA, RES )
PET: SHRI. RAMANNA
RES: THE SPECIAL LAND ACQUISITION OFFICER, SLAO
GOVT ADVOCATE
";

            var targetDate = new DateTime(2026, 11, 3);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                landRevenueSnippet,
                estCode: "KAHC02",
                benchId: "3624",
                judgeName: "JUSTICE K.N.",
                causelistType: "Daily List",
                targetDate: targetDate);

            // Both Land Revenue (KLR, RES) and Land Acquisition (LA, RES) must be eliminated
            Assert.Empty(results);
        }

        [Fact]
        public void ParseHighCourtCauseListText_Strictly_Eliminates_Sibling_RTC_MFA_Claims()
        {
            string siblingRtcSnippet = @"
HIGH COURT OF KARNATAKA PRINCIPAL BENCH BENGALURU
COURT HALL NO : 15
5 MFA 7853/2026 (MV, I )
PET: SRI. CHETAN KUMAR
RES: THE MANAGING DIRECTOR, BMTC
K S VENKATARAMANA

6 MFA 8912/2026 (MV, D )
PET: SMT. LAKSHMI
RES: THE MANAGING DIRECTOR, KSRTC (CENTRAL OFFICES)
H B GOVINDAPPA

HIGH COURT OF KARNATAKA KALABURAGI BENCH
COURT HALL NO : 3
10 MFA 201034/2026 (MV, I )
PET: SRI. SHARANAPPA
RES: THE MANAGING DIRECTOR, KKRTC, KALABURAGI
SANTOSH KUMAR
";

            var targetDate = new DateTime(2026, 11, 3);

            // Bengaluru Bench (KAHC01)
            var blrResults = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                siblingRtcSnippet,
                estCode: "KAHC01",
                benchId: "1001",
                judgeName: "JUSTICE B.B.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.Empty(blrResults);

            // Kalaburagi Bench (KAHC03)
            var klbResults = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                siblingRtcSnippet,
                estCode: "KAHC03",
                benchId: "2001",
                judgeName: "JUSTICE C.C.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.Empty(klbResults);
        }

        [Fact]
        public void ParseHighCourtCauseListText_Allows_Labour_And_Service_Writs_Alongside_MFA()
        {
            string mixedSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 10
32 WA 101074/2026 (L, KSRTC )
PET: SRI MANJUNATH
RES: THE MANAGEMENT OF NWKRTC
SANGEETHA F KALLIMANI

13 WP 147951/2020 (S, KSRTC )
PET: THE MANAGEMENT OF NWKRTC
RES: ABDULSATTAR S/O GOUSEMOIDDIN PAKALI
PRASHANT S HOSMANI

17 MFA 100245/2023 (MV, I )
PET: SRI. MUKTIYARAHMAD
RES: THE MANAGING DIRECTOR, NWKRTC
S C BHUTI FOR RESPONDENT
";

            var targetDate = new DateTime(2026, 11, 3);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                mixedSnippet,
                estCode: "KAHC02",
                benchId: "3630",
                judgeName: "JUSTICE D.D.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.Equal(3, results.Count);

            var waCase = results.FirstOrDefault(x => x.CaseType == "WA" && x.RegNumber == "101074");
            Assert.NotNull(waCase);
            Assert.Equal("(L, KSRTC )", waCase.SubjectCategory);

            var wpCase = results.FirstOrDefault(x => x.CaseType == "WP" && x.RegNumber == "147951");
            Assert.NotNull(wpCase);
            Assert.Equal("(S, KSRTC )", wpCase.SubjectCategory);

            var mfaCase = results.FirstOrDefault(x => x.CaseType == "MFA" && x.RegNumber == "100245");
            Assert.NotNull(mfaCase);
            Assert.Equal("(MV, I )", mfaCase.SubjectCategory);
        }
    }
}
