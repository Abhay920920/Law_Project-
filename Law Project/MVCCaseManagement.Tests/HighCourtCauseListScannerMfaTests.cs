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

        [Fact]
        public void ParseHighCourtCauseListText_Strictly_Eliminates_Insurance_Company_MFA_Claims()
        {
            // Even when empanelled advocates (Budihal, Hegde, Kuppelur, Matti) appear, pure insurance claims must be rejected
            string insuranceSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 9
Cause List No. 1
Sl.No. Case No. Parties / Advocates

1 MFA 101111/2023 (MV, I )
PET: SMT. RENUKA W/O BASAVARAJ
RES: 1. SRI. SHIVANAND (OWNER)
     2. THE DIVISIONAL MANAGER, UNITED INDIA INSURANCE CO. LTD.
VEERESH R BUDIHAL FOR R2

2 MFA 102222/2023 (MV, D )
PET: SRI. MALLIKARJUN
RES: 1. THE MANAGING DIRECTOR, RELIANCE GENERAL INSURANCE CO LTD
     2. SRI. RAMESH (DRIVER)
S L MATTI FOR R1

3 MFA 103333/2024 (MV, I )
PET: THE REGIONAL OFFICER, NATIONAL INSURANCE COMPANY
RES: SRI. PARASAPPA AND OTHERS
KUPPELUR FOR PETITIONER

4 MFA 104444/2024 (MV, I )
PET: SMT. SUMA
RES: THE LEGAL MANAGER, ICICI LOMBARD GENERAL INSURANCE
VEENA HEGDE FOR RESPONDENT
";

            var targetDate = new DateTime(2026, 11, 4);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                insuranceSnippet,
                estCode: "KAHC02",
                benchId: "3629",
                judgeName: "JUSTICE TEST",
                causelistType: "Daily List",
                targetDate: targetDate);

            // All insurance company claims must be strictly eliminated!
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public void ParseHighCourtCauseListText_Strictly_Eliminates_Non_Transport_Dept_MFA_Claims()
        {
            // Land Acquisition, Upper Krishna Project (UKP), PWD, Police, and Electricity boards must be rejected
            string otherDeptSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 98
Cause List No. 1
Sl.No. Case No. Parties / Advocates

34 MFA 101942/2023
PET: VEERAPPA S/O MALLAPPA CHEBBI
RES: THE SPECIAL LAND ACQUISTITION OFFICER UKP, BAGALKOT
GOVT ADVOCATE

53 MFA 104672/2025
PET: MAHAGUNDAPPA S/O HOLIYAPPA MACHAKUNUR
RES: THE SPECIAL LAND ACQUISITION OFFICER, SLAO
GOVT PLEADER

12 MFA 105555/2023 (MV, I )
PET: THE MANAGING DIRECTOR, HESCOM, HUBLI
RES: SRI. BASAPPA
ADVOCATE FOR PETITIONER

15 MFA 106666/2022 (MV, D )
PET: SMT. KAVITA
RES: THE EXECUTIVE ENGINEER, PWD, DHARWAD
GOVT ADVOCATE

20 MFA 107777/2023 (MV, I )
PET: SRI. CHANDRAKANT
RES: THE SUPERINTENDENT OF POLICE, BELAGAVI DISTRICT
GOVT ADVOCATE
";

            var targetDate = new DateTime(2026, 11, 4);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                otherDeptSnippet,
                estCode: "KAHC02",
                benchId: "3698",
                judgeName: "REGISTRAR",
                causelistType: "Daily List",
                targetDate: targetDate);

            // All non-transport government departments must be strictly eliminated!
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public void ParseHighCourtCauseListText_Accepts_All_Valid_NWKRTC_MFA_Matters()
        {
            string validNwkrtcSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 9
Cause List No. 1
Sl.No. Case No. Parties / Advocates

1 MFA 100250/2020 (MV, I)
PET: SRI. DURGAPPA S/O HANAMANTAPPA KARI
RES: THE DIVISIONAL CONTROLLER, NWKRTC
MADANMOHAN M KHANNUR FOR RESPONDENT

2 MFA 101510/2022 (MV, I)
PET: SMT. SHILPA H K
RES: THE DIVISIONAL CONTROLLER, NWKRTC BELAGAVI DIVISION
CHANDRASHEKHAR M HOSAMANI FOR RES

3 MFA 100999/2024 (MV, D )
PET: NWKRTC INTERNAL INSURANCE FUND
RES: SMT. SHANTAWWA AND OTHERS
S C BHUTI FOR PETITIONER

4 MFA 100888/2023 (MV, I )
PET: SRI. BASAVARAJU
RES: NORTH WEST KARNATAKA ROAD TRANSPORT CORPORATION
VAISHALI K KALADAGI FOR R1

5 MFA 100777/2024 (MV, I )
PET: SRI. IRAPPA
RES: THE MANAGING DIRECTOR, NWKRTC, GOKUL ROAD, HUBLI
HANAMANTREDDY SAHUKAR FOR RESPONDENT
";

            var targetDate = new DateTime(2026, 11, 4);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                validNwkrtcSnippet,
                estCode: "KAHC02",
                benchId: "3629",
                judgeName: "JUSTICE S.V.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.NotNull(results);
            Assert.Equal(5, results.Count);

            Assert.Contains(results, x => x.RegNumber == "100250" && x.NwkrtcRole == "Against NWKRTC");
            Assert.Contains(results, x => x.RegNumber == "101510" && x.NwkrtcRole == "Against NWKRTC");
            Assert.Contains(results, x => x.RegNumber == "100999" && x.NwkrtcRole == "By NWKRTC");
            Assert.Contains(results, x => x.RegNumber == "100888" && x.NwkrtcRole == "Against NWKRTC");
            Assert.Contains(results, x => x.RegNumber == "100777" && x.NwkrtcRole == "Against NWKRTC");
        }
    }
}
