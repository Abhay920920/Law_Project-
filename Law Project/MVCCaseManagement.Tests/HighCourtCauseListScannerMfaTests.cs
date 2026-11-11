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

        [Fact]
        public void ParseHighCourtCauseListText_Accepts_Dotted_N_W_K_R_T_C_Variations()
        {
            string dottedSnippet = @"
HIGH COURT OF KARNATAKA DHARWAD BENCH
COURT HALL NO : 23
Cause List No. 1
Sl.No. Case No. Parties / Advocates

23 WA 100627/2026 (L, KSRTC )
PET: SRI IRAYYA S/O
RES: N.W.K.R.T.C (DATE) PANCHAXARAYYA HIREMATH VENKATESH M KHARVI DHANANJAY H M, PRAVEEN
ADVOCATE FOR PETITIONER

24 WP 104500/2026 (S, KSRTC )
PET: SRI BASAVARAJ
RES: THE MANAGING DIRECTOR, N.W.K.R.T.C.
MADANMOHAN M KHANNUR FOR R1

25 WA 100800/2026 (L, KSRTC )
PET: N W K R T C HUBLI DIVISION
RES: SRI SHIVAPPA
S C BHUTI FOR PETITIONER
";

            var targetDate = new DateTime(2026, 11, 11);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                dottedSnippet,
                estCode: "KAHC02",
                benchId: "3670",
                judgeName: "JUSTICE M.N.",
                causelistType: "Daily List",
                targetDate: targetDate);

            Assert.NotNull(results);
            Assert.Equal(3, results.Count);

            // WA 100627/2026
            var wa1 = results.FirstOrDefault(x => x.RegNumber == "100627");
            Assert.NotNull(wa1);
            Assert.Equal("WA", wa1.CaseType);
            Assert.Equal("Against NWKRTC", wa1.NwkrtcRole);

            // WP 104500/2026
            var wp1 = results.FirstOrDefault(x => x.RegNumber == "104500");
            Assert.NotNull(wp1);
            Assert.Equal("WP", wp1.CaseType);
            Assert.Equal("Against NWKRTC", wp1.NwkrtcRole);

            // WA 100800/2026
            var wa2 = results.FirstOrDefault(x => x.RegNumber == "100800");
            Assert.NotNull(wa2);
            Assert.Equal("WA", wa2.CaseType);
            Assert.Equal("By NWKRTC", wa2.NwkrtcRole);
        }

        [Theory]
        [InlineData("NWKRTC", true, "NWKRTC Central")]
        [InlineData("N.W.K.R.T.C", true, "NWKRTC Central")]
        [InlineData("N.W.K.R.T.C.", true, "NWKRTC Central")]
        [InlineData("N W K R T C", true, "NWKRTC Central")]
        [InlineData("N-W-K-R-T-C", true, "NWKRTC Central")]
        [InlineData("N.W.K.S.R.T.C", true, "NWKRTC Central")]
        [InlineData("THE MANAGING DIRECTOR, NWKRTC", true, "Managing Director")]
        [InlineData("THE DIVISIONAL CONTROLLER, HUBLI", true, "Divisional Controller")]
        [InlineData("THE DEPOT MANAGER, BELAGAVI", true, "Depot Manager")]
        [InlineData("CHIEF PERSONNEL MANAGER", true, "CPM (Personnel)")]
        [InlineData("INTERNAL INSURANCE FUND", true, "Insurance Fund")]
        [InlineData("THE MANAGING DIRECTOR, BMTC", false, "General / Corporate")]
        [InlineData("THE MANAGING DIRECTOR, KKRTC", false, "General / Corporate")]
        [InlineData("HESCOM, HUBLI", false, "General / Corporate")]
        public void NwkrtcPartyMatcher_Identifies_All_Valid_Entity_Variants(string partyName, bool expectedMatch, string expectedDesignation)
        {
            bool isMatch = NwkrtcPartyMatcher.IsNwkrtcEntity(partyName, out string designation, "KAHC02");
            Assert.Equal(expectedMatch, isMatch);
            if (expectedMatch)
            {
                Assert.Equal(expectedDesignation, designation);
            }
        }

        [Fact]
        public void ParseHighCourtCauseListText_Extracts_UserProvided_11_11_2026_Cases()
        {
            string userSnippet = @"
11-11-2026
COURT HALL NO : 2 Cause List No. 1 
54 WA 100767/2025 A/W IA 1/2025 FOR DISPENSATION OF PRODUCTION OF ENGLISH TRANSLATION COPIES OF ANNEXURE- A AND B (TCR K/B) (DATE)     (L, ID ) 
PET: SRI. SHEKARGOUDA S/O VIRAPPANAGOUDA PIDDANAGOUDAR RAVI HEGDE VINAYKUMAR BHAT 
RES: THE DIVISIONAL CONTROLLER  PRASHANT S HOSMANI 

COURT HALL NO : 3 Cause List No. 1 ORDERS 
1 CCC 100207/2025 REG : 1) NON-FILING OF COMPLIANCE REPORT BY THE ACCUSED, AS PER ORDER DTD: 18.08.2026 2) AFFIDAVIT DTD; 08.09.2025 FILED BY ACCUSED NO.1 3) REJOINDER 07.01.2026 FILED BY THE COMPLAINANT 4) AFFIDAVIT DATED 28.07.2026 FILED BY ACCUSED NO. 1 (DATE) (, ) 
PET: SHRI SOMALINGAPPA S/O. NINGAPPA SYRYAVANSHI RAVI HEGDE VINAYKUMAR BHAT 
RES: MR. V.G.GULAPPANAVAR AND ANOTHER  PRASHANT S HOSMANI FOR A1 & A2 

COURT HALL NO : 4 Cause List No. 1 
63 WP 103018/2026 (TCR K/B) (CH MOVED) (L, ID ) 
PET: THE DIVISIONAL CONTROLLER KHANNUR MADAN. M 
RES: SHRI IRAPPA S/O DUNDAPPA NAIK  S K HEGDE 

66 WP 104534/2026 (DATE) (L, KSRTC ) 
PET: THE MANAGEMENT OF NWKRTC KHANNUR MADAN. M 
RES: SHRI. SHANKARGOUDA RAMAGOUDRA PATIL  SHANKARGOUDA-SD 
Connected With   
66.1 WP 104642/2026 (L, ID ) 
PET: THE MANAGEMENT OF NWKRTC KHANNUR MADAN. M 
RES: SHRI. SHANKARGOUDA RAMAGOUDRA PATIL  RAVI HEGDE & VINAYKUMAR BHAT 

COURT HALL NO : 6 Cause List No. 1 
17 WP 105227/2018 REG : IA 1/2026 FOR RECALLING ARJ ORDER DTD. 10/12/2025 BY CD OF 208 DAYS (THE ABOVE WP IS DNP BEFORE ARJ ON 10/12/2025) (DATE) (S, KSRTC ) 
PET: INAYAT BAGBAN S/O. IMAMSAHEB BAGBAN K L PATIL 
RES: KARNATAKA STATE ROAD TRANSPORT CORPORATION AND OTHERS  SHIVAKUMAR S BADAWADAGI FOR R1 
R2 : THE MANAGING DIRECTOR & DISCIPLINARY AUTHORITY KSRTC CENTRAL OFFICE SHANTINAGAR BENGALURU -SD 
R3 : THE BOARD DIRECTOR & APPELLATE AUTHORITY KSRTC CENTRAL OFFICE SHANTINAGAR BENGALURU -SD 

40 WP 112282/2019 (CH MOVED) (S, KSRTC ) 
PET: SMT.MAHADEVI W/O SHARANAPPA BIRADAR AND ANOTHER RAVI HEGDE VINAYKUMAR BHAT 
RES: THE DIVISIONAL CONTROLER  PRASHANT S HOSMANI (NOC) 

COURT HALL NO : 10 Cause List No. 1 
23 WP 85123/2013 (DATE) (SCANNED) (L, KSRTC ) 
PET: THE DIVISIONAL CONTROLLER NWKRTC  PRAKASH HOSAMANI 
RES: MANJUNATH S/O. CHUDAMANI AMMINABHAVI  RESPONDENT : MANJUNATH - SERVED 

INAL HEARING 
43 WP 67437/2010 REG : NON FURNISHING OF PF COVER ACK & CORRECT ADDRESS TO ISSUE NOTICE TO R1 (L, KSRTC ) 
PET: THE DIVISIONAL CONTROLLER,  PRASHANT S 
RES: BASAPPA, AND OTHERS GA FOR R2 and R3 R1 : NO SUCH PERSON IN THIS (FOR 3RD TIME) (DATE) (SCANNED) HOSMANI ADDRESSEE 
Connected With   
43.1 WP 66631/2010 REG : FURNISHING OF PF COVER ACK & CORRECT ADDRESS TO ISSUE NOTICE TO R1 (SCANNED) (L, KSRTC ) 
PET: THE DIVISIONAL CONTROLLER,  PRASHANT S HOSMANI 
RES: B. SHIVASHANKARAPPA, AND OTHERS  GA FOR R2 & R3 R1 : NO SUCH PERSON IN THIS ADDRESS   
43.2 WP 67216/2010 REG : FURNISHING OF PF COVER ACK & CORRECT ADDRESS TO ISSUE NOTICE TO R1 (SCANNED) (L, KSRTC ) 
PET: THE DIVISIONAL CONTROLLER, NEKRTC, BELLARY  PRASHANT HOSMANI 
RES: B PADDANNA, AND OTHERS GA FOR R2 and R3 R1 : NO SUCH PERSON IN THIS ADDRESS   
43.3 WP 67428/2010 REG: STEPS TO BE TAKEN IN R/O R1 (ALL READY 3RD TIME NOTICE ISSUED) (DATE) (SCANNED) (L, KSRTC ) 
PET: THE DIVISONAL CONTROLLER, NEKRTC, KOPPAL  PRASHANT HOSMANI 
RES: K BASAVARAJ S/O.CHANDRAYYA, AND OTHERS GA FOR R2 and R3 R1 - NOTICE DISPENSED WITH 

COURT HALL (ADDL. REGISTRAR JUDICIAL)
Cause List No. 1
23 WA 100627/2026 (L, KSRTC )
PET: SRI IRAYYA S/O
RES: N.W.K.R.T.C (DATE) PANCHAXARAYYA HIREMATH VENKATESH M KHARVI DHANANJAY H M, PRAVEEN
";

            var targetDate = new DateTime(2026, 11, 11);
            var results = HighCourtDailyCauseListScannerService.ParseHighCourtCauseListText(
                userSnippet,
                estCode: "KAHC02",
                benchId: "3670",
                judgeName: "JUSTICE M.N.",
                causelistType: "Daily List",
                targetDate: targetDate);

            // Let's inspect what cases were captured
            var capturedCaseNumbers = results.Select(r => $"{r.CaseType} {r.RegNumber}/{r.RegYear}").ToList();
            Console.WriteLine($"Captured {results.Count} cases: {string.Join(", ", capturedCaseNumbers)}");
            
            // Check essential NWKRTC cases
            Assert.Contains(results, r => r.RegNumber == "104534"); // WP 104534/2026 (Management of NWKRTC)
            Assert.Contains(results, r => r.RegNumber == "100767"); // WA 100767/2025 (Divisional Controller)
            Assert.Contains(results, r => r.RegNumber == "103018"); // WP 103018/2026 (Divisional Controller)
            Assert.Contains(results, r => r.RegNumber == "104642"); // WP 104642/2026 (Connected)
            Assert.Contains(results, r => r.RegNumber == "85123");  // WP 85123/2013 (Divisional Controller NWKRTC)
            Assert.Contains(results, r => r.RegNumber == "112282"); // WP 112282/2019 (Divisional Controler)
            Assert.Contains(results, r => r.RegNumber == "67437");  // WP 67437/2010 (Divisional Controller)
            Assert.Contains(results, r => r.RegNumber == "100207"); // CCC 100207/2025 (Contempt Petition)
            Assert.Contains(results, r => r.RegNumber == "105227"); // WP 105227/2018 (Service Writ Petition)
            Assert.Contains(results, r => r.RegNumber == "100627"); // WA 100627/2026 (Addl Registrar Judicial)

            // Cache for application serving
            string json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            string appDataCache = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\CaseManagement\App_Data\causelist_cache\highcourt_KAHC02_2026-11-11.json"));
            string appDataDir = Path.GetDirectoryName(appDataCache)!;
            if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
            File.WriteAllText(appDataCache, json);

            // Also sync to master store
            string masterPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\CaseManagement\App_Data\party_search_cache\highcourt_nwkrtc_master.json"));
            if (File.Exists(masterPath))
            {
                var mockEnv = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                string contentRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\CaseManagement"));
                mockEnv.Setup(e => e.ContentRootPath).Returns(contentRoot);
                var store = new MVCCaseManagement.Services.ECourts.HighCourtPartySearchStore(
                    mockEnv.Object,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<MVCCaseManagement.Services.ECourts.HighCourtPartySearchStore>.Instance);
                store.MergeFreshCasesAsync(results).GetAwaiter().GetResult();
            }
        }
    }
}
