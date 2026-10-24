# 🧭 NWKRTC Law Management System — Complete Application Navigation Tree

**Document Reference**: Visual Menu Hierarchy, Conditional Rendering & Role Scoping  
**Source Baseline**: [`Views/Shared/_Layout.cshtml`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/Views/Shared/_Layout.cshtml) & [`Views/Shared/_OtherCourtsSidebar.cshtml`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/Views/Shared/_OtherCourtsSidebar.cshtml)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Global Navigation Architecture

The application layout renders a persistent **Official Branding Header** across the top and a dynamic, context-aware **Sidebar** on the left.

The Sidebar dynamically selects which navigation block to display based on the active controller, user division, and authenticated role claims:
1. **`isReport`**: Active when `controller == "Report"` $\to$ Renders **REPORTS & MIS** menu.
2. **`isLabour`**: Active when `controller == "Labour"` or `"LabourEP"` $\to$ Renders **LABOUR DISPUTES** menu.
3. **`isGratuity`**: Active when `controller == "Gratuity"` $\to$ Renders **GRATUITY CLAIMS** menu.
4. **`isOtherCourts`**: Active when `controller == "Home"` and action in Other Courts set $\to$ Renders **OTHER COURTS** menu.
5. **`isMVC`** (Default): Active for all other case controllers $\to$ Renders **MVC / MACT CLAIMS** menu.

---

## 2. Full Navigable Menu Hierarchy

```text
NWKRTC LAW MANAGEMENT SYSTEM
│
├── 🔑 AUTHENTICATION & HEADER CONTROLS
│   ├── [Logo / Home] NWKRTC Central Branding -> /Home/Index
│   ├── [Notification Bell] Unread Alert Dropdown -> /Notification/Index
│   ├── [User Profile Widget] Shows Officer FullName & DivisionName
│   └── [Sign Out Button] -> /Account/Logout
│
├── 🏠 GLOBAL COMMON NAVIGATION (Always Visible)
│   ├── [Central Portal Home] -> /Home/Index (Summary cards across all legal divisions)
│   └── [Nyaya Patha AI] -> /NyayaPatha (Air-gapped local AI legal research assistant)
│
├── 🚗 MODULE: MVC / MACT CLAIMS (Default Sidebar Block)
│   ├── [Dashboard] -> /Dashboard (MACT operational dashboard & daily hearing schedule)
│   ├── [New Registration] -> /Case/Create (Division users only; DivID != 0, 5)
│   ├── [Third Party Cases] -> /Case/ThirdPartyList (Accident claims involving third parties)
│   ├── [MVC Case List] -> /Case/Index (Master searchable case register, division-scoped)
│   │   ├── Action: Details -> /Case/Details/{id} (Full case dossier, hearings, awards, docs)
│   │   ├── Action: Edit -> /Case/Edit/{id} (Update case particulars; owner division only)
│   │   ├── Action: Add Hearing -> Modal /Case/SaveHearing
│   │   ├── Action: Record Award -> Modal /Case/SaveAdverseDetails
│   │   └── Action: Sync eCourts -> Trigger AJAX /ECourts/QueueCaseSync
│   ├── [HC Cause List] -> /ECourts/HighCourtCauseList (Central Office only; DivID == 0, 5)
│   ├── [My Cases] -> /ECourts/MyCases?module=MVC (Central Office tracked cases)
│   ├── [Claimant Appeals] -> /Appeal/Register (Central Office; file/track claimant MFA)
│   ├── [SLP Registration] -> /Appeal/SLPRegistration (Central Office; Supreme Court matters)
│   ├── [Audit Registry] -> /Audit (Financial compensation & disbursement ledger)
│   ├── [EP (Execution Petitions)] -> /EP/Index (MVC Execution Petitions list)
│   │   ├── [Register EP] -> /EP/Create (Create execution petition)
│   │   ├── [Edit EP] -> /EP/Edit/{id} (Owner division only)
│   │   └── [EP Details] -> /EP/Details/{id} (Liability calculation & deposits)
│   ├── [Petty Bill] -> /pettybill (Division users only; advocate fee & expense portal)
│   │   ├── [New Bill Form] -> /Report/SavePettyBill
│   │   └── [Print Bill Voucher] -> /Report/PrintBill/{id}
│   ├── [MVC Remand Back Case] -> /Case/RemindBack (Division users only; remanded retrials)
│   └── [Judgements & Circulars] -> /Judgement/Index (Legal repository of precedents)
│
├── 💼 MODULE: LABOUR DISPUTES & INDUSTRIAL RELATIONS
│   ├── [Labour Dashboard] -> /Labour/Index (Dispute statistics & compliance widgets)
│   ├── [Live HC Cause List] -> /Labour/HighCourtCauseList (High Court daily board)
│   ├── [My Cases] -> /ECourts/MyCases?module=Labour (Labour tracked matters)
│   ├── [New Registration] -> /Labour/Create (Division users only; register ID/KID dispute)
│   ├── [Labour Case List] -> /Labour/CaseList (Searchable Industrial Disputes)
│   │   ├── Action: Details -> /Labour/Details/{id} (Labour dossier, hearings, notings)
│   │   ├── Action: Edit -> /Labour/Edit/{id} (Owner division only)
│   │   └── Multi-Tier Legal Review:
│   │       ├── [LO Action] -> /Labour/LOAction (Law Officer Review)
│   │       ├── [Dy CLO Action] -> /Labour/DyCLOAction (Deputy Chief Law Officer Review)
│   │       ├── [CLO Action] -> /Labour/CLOAction (Chief Law Officer Review)
│   │       └── [MD Action] -> /Labour/MDAction (Managing Director Final Approval)
│   ├── [Arising Applications] -> /Labour/ArisingApplication (Section 33-C(2) recovery claims)
│   │   ├── Action: Details -> /Labour/DetailsArisingApplication/{id}
│   │   ├── Action: Edit -> /Labour/EditArisingApplication/{id}
│   │   └── Action: Delete -> /Labour/DeleteArisingApplication/{id} (Owner division only)
│   ├── [Case Transfer] -> /Labour/Transfer (Inter-division transfer wizard)
│   ├── [EP (Execution Petitions)] -> /LabourEP/Index (Labour execution petitions list)
│   │   ├── [Register Labour EP] -> /LabourEP/Create
│   │   └── [Edit Labour EP] -> /LabourEP/Edit/{id}
│   ├── [Service/Commercial Matters] -> /Labour/ServiceMatters (Central Office only)
│   ├── [Claimant Writ Petition] -> /Labour/ClaimantAppeals (Central Office only)
│   ├── [CCC Cases] -> /Labour/CCCCases (Central Office only; Civil Contempt of Court)
│   ├── [Review Petition Filing] -> /Labour/ReviewPetitions (Central Office only)
│   ├── [Claimant Writ Appeal] -> /Labour/ClaimantWritAppeals (Central Office only)
│   └── [Claimant SLP] -> /Labour/ClaimantSLPs (Central Office only)
│
├── 💰 MODULE: GRATUITY CLAIMS (Payment of Gratuity Act 1972)
│   ├── [Gratuity Dashboard] -> /Gratuity/Index (Controlling Authority statistics)
│   ├── [New Registration] -> /Gratuity/Create (Division users only; register Form T/N)
│   ├── [Gratuity Case List] -> /Gratuity/CaseList (Searchable gratuity disputes)
│   │   └── Action: Details -> /Gratuity/Details/{id}
│   ├── [Audit Calculation] -> /Gratuity/AuditCalculation (10% statutory simple interest)
│   └── [Claimant PGA/APPL/CR] -> /Gratuity/ClaimantAppeals (Appellate Authority proceedings)
│
├── ⚖️ MODULE: OTHER COURTS GATEWAY
│   ├── [Other Courts Gateway] -> /Home/OtherCourts (Jurisdiction selection portal)
│   ├── [O.S (Corporation)] -> /Home/OSCorporation (Original Suits where NWKRTC is Plaintiff)
│   │   ├── Registration Form -> /Home/OSCorporationForm
│   │   └── Dashboard -> /Home/OSCorporation
│   ├── [O.S (Claimant)] -> /Home/OSClaimant (Original Suits where NWKRTC is Defendant)
│   │   ├── Registration Form -> /Home/OSClaimantForm
│   │   └── Dashboard -> /Home/OSClaimant
│   ├── [ECA Cases] -> /Home/ECA (Employees' Compensation Act tribunal claims)
│   └── [Case List] -> /Home/OtherCourtsCaseList (Consolidated non-MVC civil matters)
│
├── 📊 MODULE: REPORTS & CONSOLIDATED MIS
│   ├── [MVC Monthly Statement] -> /Report/CentralOffice?module=MVC
│   ├── [Labour Monthly Statement] -> /Report/CentralOffice?module=Labour
│   ├── [Gratuity Monthly Statement] -> /Report/CentralOffice?module=Gratuity
│   ├── [Other Courts Statement] -> /Report/CentralOffice?module=Others
│   └── [MMR-ST2 Particulars] -> /Report/CentralOffice?module=MMR-ST2
│
├── ⚙️ MODULE: SYSTEM SETUP & ADMINISTRATION
│   ├── [Systems Setup] -> /Master/Index (Admin only; Courts, Advocates, Divisions)
│   └── [API Dashboard] -> /ApiDashboard (Admin, CO, CLO, MD, LO; NAPIX health & quota)
│
└── 🔒 HIDDEN / DIRECT-ROUTE / BACKGROUND-ONLY FEATURES
    ├── [Physical File Streamer] -> /Uploads/{fileName} (Zero-trust cross-domain check)
    ├── [NAPIX Queue Background Worker] -> NapixBackgroundSyncService (Continuous)
    ├── [Daily SMS Notification Worker] -> NotificationBackgroundService (Daily 07:00 IST)
    └── [Audit Activity Logger] -> /ActivityLog/CaseLogs/{id} (Embedded audit modal)
```

---

## 3. Role-Based Visibility Rules in Layout

The Razor navigation engine applies the following role rules defined in lines 90-122 of `_Layout.cshtml`:
- **Read-Only Roles (`isReadOnlyRole = true`)**:
  - Roles: `LO`, `Dy CLO`, `CLO`, `MD`, `Division Controller` (`DC`).
  - Restrictions: Create buttons, Edit links, Arising Applications, Petty Bills, and Systems Setup are hidden from the sidebar. These executive roles have read-only visibility into Dashboards, Case Lists, Dossiers, and Legal Action Review forms.
- **Central Office Roles (`isCO_Layout = true`)**:
  - Trigger: `DivisionID == "0"`, `DivisionID == "5"`, or empty.
  - Options: Unlocks High Court Cause Lists, Central Office MIS Statements, Service Matters, CCC Cases, Review Petitions, Writ Appeals, and Supreme Court SLP registrations.
- **Division Office Users (`!isCO_Layout`)**:
  - Trigger: Specific operating division (e.g., Belagavi=1, Hubballi=2, etc.).
  - Options: Unlocks New Registration, Third Party Registration, Execution Petitions, Petty Bill Vouchers, and Remand Back entries.
