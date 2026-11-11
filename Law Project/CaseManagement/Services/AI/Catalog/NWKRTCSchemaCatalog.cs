using System;
using System.Collections.Generic;
using System.Linq;

namespace MVCCaseManagement.Services.AI.Catalog
{
    public class ColumnDescriptor
    {
        public required string ColumnName { get; init; }
        public string DataType { get; init; } = "string"; // int, string, decimal, datetime, bool
        public bool IsPrimaryKey { get; init; }
        public bool IsForeignKey { get; init; }
        public string? ForeignTable { get; init; }
        public string? ForeignColumn { get; init; }
        public bool IsDivisionTenant { get; init; }
        public bool IsMonetary { get; init; }
        public bool IsDate { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string[] Synonyms { get; init; } = Array.Empty<string>();
    }

    public class TableDescriptor
    {
        public required string TableName { get; init; }
        public string PrimaryKey { get; init; } = "ID";
        public string? DivisionColumn { get; init; }
        public string? ParentTable { get; init; }
        public string? ForeignKeyToParent { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Module { get; init; } = "General"; // MVC, Labour, Gratuity, Appeal, PettyBill, Master
        public string[] Synonyms { get; init; } = Array.Empty<string>();
        public Dictionary<string, ColumnDescriptor> Columns { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public ColumnDescriptor? FindColumn(string nameOrSynonym)
        {
            if (string.IsNullOrWhiteSpace(nameOrSynonym)) return null;
            if (Columns.TryGetValue(nameOrSynonym, out var col)) return col;
            return Columns.Values.FirstOrDefault(c =>
                string.Equals(c.DisplayName, nameOrSynonym, StringComparison.OrdinalIgnoreCase) ||
                c.Synonyms.Any(s => string.Equals(s, nameOrSynonym, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public static class NWKRTCSchemaCatalog
    {
        private static readonly Dictionary<string, TableDescriptor> Tables = new(StringComparer.OrdinalIgnoreCase);

        static NWKRTCSchemaCatalog()
        {
            RegisterMvcCases();
            RegisterMvcAdverseDetails();
            RegisterMvcPetitioners();
            RegisterLabourCases();
            RegisterLabourServiceMatters();
            RegisterGratuityCases();
            RegisterAppealDetails();
            RegisterPettyBills();
            RegisterPettyBillPayments();
            RegisterDivisionMaster();
            RegisterMactMaster();
            RegisterAdvocateMasters();
            RegisterJudgementRepo();
        }

        public static TableDescriptor? GetTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return null;
            return Tables.TryGetValue(tableName, out var table) ? table : null;
        }

        public static IEnumerable<TableDescriptor> GetAllTables() => Tables.Values;

        public static TableDescriptor? FindTableBySynonym(string synonymOrKeyword)
        {
            if (string.IsNullOrWhiteSpace(synonymOrKeyword)) return null;
            string key = synonymOrKeyword.Trim();

            // Direct table name match
            if (Tables.TryGetValue(key, out var directMatch)) return directMatch;

            // Synonym search
            return Tables.Values.FirstOrDefault(t =>
                string.Equals(t.DisplayName, key, StringComparison.OrdinalIgnoreCase) ||
                t.Synonyms.Any(s => string.Equals(s, key, StringComparison.OrdinalIgnoreCase) ||
                                    key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public static TableDescriptor? FindTableForModule(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName)) return null;
            return Tables.Values.FirstOrDefault(t =>
                string.Equals(t.Module, moduleName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.TableName, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        public static string? GetDivisionColumn(string tableName)
        {
            var table = GetTable(tableName);
            if (table == null) return null;
            if (!string.IsNullOrEmpty(table.DivisionColumn)) return table.DivisionColumn;

            if (!string.IsNullOrEmpty(table.ParentTable))
            {
                var parent = GetTable(table.ParentTable);
                return parent?.DivisionColumn;
            }
            return null;
        }

        private static void RegisterMvcCases()
        {
            var t = new TableDescriptor
            {
                TableName = "MVC_CASES",
                PrimaryKey = "CaseID",
                DivisionColumn = "DivisionID",
                DisplayName = "Motor Accident Claims (MVC)",
                Module = "MVC",
                Synonyms = new[] { "mvc", "mact", "accident", "accident claim", "motor vehicle accident", "mvc case", "mact cases", "fatality", "injury" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsPrimaryKey = true, DisplayName = "Case ID" },
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsDivisionTenant = true, DisplayName = "Division ID", Synonyms = new[] { "division", "div" } },
                    ["MVCNo"] = new() { ColumnName = "MVCNo", DataType = "string", DisplayName = "MVC Number", Synonyms = new[] { "case number", "case no", "mvc no" } },
                    ["MVCYear"] = new() { ColumnName = "MVCYear", DataType = "int", DisplayName = "MVC Year", Synonyms = new[] { "year", "filing year" } },
                    ["MACTID"] = new() { ColumnName = "MACTID", DataType = "int", IsForeignKey = true, ForeignTable = "MACT_MASTER", ForeignColumn = "MACTID", DisplayName = "Court ID" },
                    ["VehicleNo"] = new() { ColumnName = "VehicleNo", DataType = "string", DisplayName = "Vehicle Number", Synonyms = new[] { "bus number", "bus no", "registration no", "vehicle" } },
                    ["AccidentDate"] = new() { ColumnName = "AccidentDate", DataType = "datetime", IsDate = true, DisplayName = "Accident Date", Synonyms = new[] { "date of accident", "incident date" } },
                    ["CaseType"] = new() { ColumnName = "CaseType", DataType = "string", DisplayName = "Case Type", Synonyms = new[] { "type", "nature" } },
                    ["StatusID"] = new() { ColumnName = "StatusID", DataType = "int", DisplayName = "Status ID" },
                    ["ClaimType"] = new() { ColumnName = "ClaimType", DataType = "string", DisplayName = "Claim Type", Synonyms = new[] { "injury type", "fatal", "injury", "death" } },
                    ["ClaimAmount"] = new() { ColumnName = "ClaimAmount", DataType = "decimal", IsMonetary = true, DisplayName = "Claim Amount", Synonyms = new[] { "amount claimed", "claimed compensation" } },
                    ["AdvocateID"] = new() { ColumnName = "AdvocateID", DataType = "int", IsForeignKey = true, DisplayName = "Advocate ID" },
                    ["AdvocateName"] = new() { ColumnName = "AdvocateName", DataType = "string", DisplayName = "Advocate Name", Synonyms = new[] { "advocate", "counsel", "lawyer" } },
                    ["NextHearingDate"] = new() { ColumnName = "NextHearingDate", DataType = "datetime", IsDate = true, DisplayName = "Next Hearing Date", Synonyms = new[] { "hearing date", "next date", "court date" } },
                    ["CurrentStage"] = new() { ColumnName = "CurrentStage", DataType = "string", DisplayName = "Current Stage", Synonyms = new[] { "stage", "court stage", "status" } },
                    ["DisposalStatus"] = new() { ColumnName = "DisposalStatus", DataType = "string", DisplayName = "Disposal Status", Synonyms = new[] { "disposal", "case status", "pending or disposed" } },
                    ["DisposalResult"] = new() { ColumnName = "DisposalResult", DataType = "string", DisplayName = "Disposal Result", Synonyms = new[] { "outcome", "verdict", "dismissed", "allowed" } },
                    ["CNRNumber"] = new() { ColumnName = "CNRNumber", DataType = "string", DisplayName = "CNR Number", Synonyms = new[] { "cnr", "ecourt cnr" } },
                    ["IsObjectionFiled"] = new() { ColumnName = "IsObjectionFiled", DataType = "bool", DisplayName = "Is Objection Filed" },
                    ["IsEvidenceFiled"] = new() { ColumnName = "IsEvidenceFiled", DataType = "bool", DisplayName = "Is Evidence Filed" },
                    ["ClosureDate"] = new() { ColumnName = "ClosureDate", DataType = "datetime", IsDate = true, DisplayName = "Closure Date" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterMvcAdverseDetails()
        {
            var t = new TableDescriptor
            {
                TableName = "MVC_CASE_ADVERSE_DETAILS",
                PrimaryKey = "AdverseID",
                ParentTable = "MVC_CASES",
                ForeignKeyToParent = "CaseID",
                DisplayName = "MVC Adverse Award & Compensation Details",
                Module = "MVC",
                Synonyms = new[] { "award", "compensation", "adverse judgment", "ep", "execution petition", "award amount" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsPrimaryKey = true, IsForeignKey = true, ForeignTable = "MVC_CASES", ForeignColumn = "CaseID", DisplayName = "Case ID" },
                    ["AwardDate"] = new() { ColumnName = "AwardDate", DataType = "datetime", IsDate = true, DisplayName = "Award Date", Synonyms = new[] { "judgment date", "date of award" } },
                    ["AwardAmount"] = new() { ColumnName = "AwardAmount", DataType = "decimal", IsMonetary = true, DisplayName = "Award Amount", Synonyms = new[] { "awarded amount", "compensation awarded", "principal award" } },
                    ["InterestRate"] = new() { ColumnName = "InterestRate", DataType = "decimal", DisplayName = "Interest Rate", Synonyms = new[] { "interest percentage", "interest %" } },
                    ["DisposedOnDate"] = new() { ColumnName = "DisposedOnDate", DataType = "datetime", IsDate = true, DisplayName = "Disposed Date" },
                    ["IsEPFiled"] = new() { ColumnName = "IsEPFiled", DataType = "bool", DisplayName = "Is EP Filed", Synonyms = new[] { "ep filed", "execution filed" } },
                    ["EPNumber"] = new() { ColumnName = "EPNumber", DataType = "string", DisplayName = "EP Number", Synonyms = new[] { "execution petition no" } },
                    ["EPDepositedAmount"] = new() { ColumnName = "EPDepositedAmount", DataType = "decimal", IsMonetary = true, DisplayName = "EP Deposited Amount" },
                    ["EPCourt"] = new() { ColumnName = "EPCourt", DataType = "string", DisplayName = "EP Court" },
                    ["EPStage"] = new() { ColumnName = "EPStage", DataType = "string", DisplayName = "EP Stage" },
                    ["EPNextHearingDate"] = new() { ColumnName = "EPNextHearingDate", DataType = "datetime", IsDate = true, DisplayName = "EP Next Hearing Date" },
                    ["IsFIRFiled"] = new() { ColumnName = "IsFIRFiled", DataType = "bool", DisplayName = "Is FIR Filed" },
                    ["IsChargeSheetFiled"] = new() { ColumnName = "IsChargeSheetFiled", DataType = "bool", DisplayName = "Is ChargeSheet Filed" },
                    ["IsBusCameraInstalled"] = new() { ColumnName = "IsBusCameraInstalled", DataType = "bool", DisplayName = "Is Camera Installed" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterMvcPetitioners()
        {
            var t = new TableDescriptor
            {
                TableName = "MVC_CASE_PETITIONERS",
                PrimaryKey = "PetitionerID",
                ParentTable = "MVC_CASES",
                ForeignKeyToParent = "CaseID",
                DisplayName = "MVC Petitioners and Claimants",
                Module = "MVC",
                Synonyms = new[] { "petitioners", "claimants", "victim", "petitioner" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PetitionerID"] = new() { ColumnName = "PetitionerID", DataType = "int", IsPrimaryKey = true, DisplayName = "Petitioner ID" },
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsForeignKey = true, ForeignTable = "MVC_CASES", ForeignColumn = "CaseID", DisplayName = "Case ID" },
                    ["PetitionerName"] = new() { ColumnName = "PetitionerName", DataType = "string", DisplayName = "Petitioner Name", Synonyms = new[] { "claimant", "claimant name", "victim name" } },
                    ["PetitionerAge"] = new() { ColumnName = "PetitionerAge", DataType = "int", DisplayName = "Age" },
                    ["PetitionerOccupation"] = new() { ColumnName = "PetitionerOccupation", DataType = "string", DisplayName = "Occupation" },
                    ["Relationship"] = new() { ColumnName = "Relationship", DataType = "string", DisplayName = "Relationship" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterLabourCases()
        {
            var t = new TableDescriptor
            {
                TableName = "LABOUR_CASES",
                PrimaryKey = "CaseID",
                DivisionColumn = "DivisionID",
                DisplayName = "Labour & Industrial Disputes (ID / Ref)",
                Module = "Labour",
                Synonyms = new[] { "labour", "industrial dispute", "reinstatement", "misconduct", "dismissal", "workman", "labour court", "id act", "section 10", "section 33c" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsPrimaryKey = true, DisplayName = "Case ID" },
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsDivisionTenant = true, DisplayName = "Division ID", Synonyms = new[] { "division" } },
                    ["CaseNumber"] = new() { ColumnName = "CaseNumber", DataType = "string", DisplayName = "Case Number", Synonyms = new[] { "id number", "ref number", "labour case no" } },
                    ["CaseYear"] = new() { ColumnName = "CaseYear", DataType = "int", DisplayName = "Case Year", Synonyms = new[] { "year" } },
                    ["CourtID"] = new() { ColumnName = "CourtID", DataType = "int", DisplayName = "Court ID" },
                    ["PetitionerName"] = new() { ColumnName = "PetitionerName", DataType = "string", DisplayName = "Petitioner / Workman Name", Synonyms = new[] { "employee name", "workman name", "driver name", "conductor name" } },
                    ["EmployeeNo"] = new() { ColumnName = "EmployeeNo", DataType = "string", DisplayName = "Employee Number", Synonyms = new[] { "staff no", "token no", "badge no" } },
                    ["PFNumber"] = new() { ColumnName = "PFNumber", DataType = "string", DisplayName = "PF Number" },
                    ["Designation"] = new() { ColumnName = "Designation", DataType = "string", DisplayName = "Designation", Synonyms = new[] { "role", "cadre", "driver", "conductor" } },
                    ["WorkingStatus"] = new() { ColumnName = "WorkingStatus", DataType = "string", DisplayName = "Working Status", Synonyms = new[] { "active", "retired", "dismissed" } },
                    ["NatureOfCase"] = new() { ColumnName = "NatureOfCase", DataType = "string", DisplayName = "Nature of Case", Synonyms = new[] { "case nature", "dismissal", "removal" } },
                    ["NatureOfMisconduct"] = new() { ColumnName = "NatureOfMisconduct", DataType = "string", DisplayName = "Nature of Misconduct", Synonyms = new[] { "misconduct", "charge", "dereliction" } },
                    ["CurrentStage"] = new() { ColumnName = "CurrentStage", DataType = "string", DisplayName = "Current Stage", Synonyms = new[] { "stage", "court stage" } },
                    ["NextHearingDate"] = new() { ColumnName = "NextHearingDate", DataType = "datetime", IsDate = true, DisplayName = "Next Hearing Date", Synonyms = new[] { "hearing date" } },
                    ["CaseStatus"] = new() { ColumnName = "CaseStatus", DataType = "string", DisplayName = "Case Status", Synonyms = new[] { "status", "pending or disposed" } },
                    ["DisposalMode"] = new() { ColumnName = "DisposalMode", DataType = "string", DisplayName = "Disposal Mode" },
                    ["DisposalDate"] = new() { ColumnName = "DisposalDate", DataType = "datetime", IsDate = true, DisplayName = "Disposal Date" },
                    ["DisposalResult"] = new() { ColumnName = "DisposalResult", DataType = "string", DisplayName = "Disposal Result", Synonyms = new[] { "award result", "in favour", "against" } },
                    ["AwardDetails"] = new() { ColumnName = "AwardDetails", DataType = "string", DisplayName = "Award Details" },
                    ["CO_IsWorkmanReinstated"] = new() { ColumnName = "CO_IsWorkmanReinstated", DataType = "bool", DisplayName = "Reinstated", Synonyms = new[] { "reinstatement", "reinstated" } },
                    ["CO_WP_StayGranted"] = new() { ColumnName = "CO_WP_StayGranted", DataType = "string", DisplayName = "High Court Stay Granted", Synonyms = new[] { "stay granted", "stay order" } },
                    ["CO_WP_CaseNumber"] = new() { ColumnName = "CO_WP_CaseNumber", DataType = "string", DisplayName = "Writ Petition Number", Synonyms = new[] { "wp number", "writ petition" } },
                    ["CO_WP_Year"] = new() { ColumnName = "CO_WP_Year", DataType = "int", DisplayName = "WP Year" },
                    ["AdvocateName"] = new() { ColumnName = "AdvocateName", DataType = "string", DisplayName = "Advocate Name", Synonyms = new[] { "advocate", "counsel" } },
                    ["CNRNumber"] = new() { ColumnName = "CNRNumber", DataType = "string", DisplayName = "CNR Number", Synonyms = new[] { "cnr", "ecourt cnr" } }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterLabourServiceMatters()
        {
            var t = new TableDescriptor
            {
                TableName = "LABOUR_SERVICE_MATTERS",
                PrimaryKey = "ServiceID",
                DivisionColumn = "DivisionID",
                DisplayName = "Labour Service Matters (High Court Writs & Appeals)",
                Module = "Labour",
                Synonyms = new[] { "service matter", "writ petition service", "writ appeal", "promotion dispute", "disciplinary action" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceID"] = new() { ColumnName = "ServiceID", DataType = "int", IsPrimaryKey = true, DisplayName = "Service ID" },
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsDivisionTenant = true, DisplayName = "Division ID" },
                    ["WPNumber"] = new() { ColumnName = "WPNumber", DataType = "string", DisplayName = "WP Number" },
                    ["WPYear"] = new() { ColumnName = "WPYear", DataType = "int", DisplayName = "WP Year" },
                    ["PetitionerName"] = new() { ColumnName = "PetitionerName", DataType = "string", DisplayName = "Petitioner Name" },
                    ["CaseNature"] = new() { ColumnName = "CaseNature", DataType = "string", DisplayName = "Case Nature" },
                    ["Status"] = new() { ColumnName = "Status", DataType = "string", DisplayName = "Status" },
                    ["StayGranted"] = new() { ColumnName = "StayGranted", DataType = "string", DisplayName = "Stay Granted" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterGratuityCases()
        {
            var t = new TableDescriptor
            {
                TableName = "GRA_CASES",
                PrimaryKey = "CaseID",
                DivisionColumn = "DivisionCode",
                DisplayName = "Gratuity Claims & CA Determinations (PGA)",
                Module = "Gratuity",
                Synonyms = new[] { "gratuity", "pga", "controlling authority", "gratuity act", "pga case", "retirement benefit", "gratuity claim" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsPrimaryKey = true, DisplayName = "Case ID" },
                    ["DivisionCode"] = new() { ColumnName = "DivisionCode", DataType = "string", IsDivisionTenant = true, DisplayName = "Division Code / ID", Synonyms = new[] { "division", "div" } },
                    ["PGANumber"] = new() { ColumnName = "PGANumber", DataType = "string", DisplayName = "PGA Number", Synonyms = new[] { "case number", "case no", "pga no" } },
                    ["ClaimantName"] = new() { ColumnName = "ClaimantName", DataType = "string", DisplayName = "Claimant Name", Synonyms = new[] { "employee", "claimant", "retiree" } },
                    ["ClaimantDesignation"] = new() { ColumnName = "ClaimantDesignation", DataType = "string", DisplayName = "Designation" },
                    ["AmountClaimed"] = new() { ColumnName = "AmountClaimed", DataType = "decimal", IsMonetary = true, DisplayName = "Amount Claimed", Synonyms = new[] { "claimed gratuity", "claim amount" } },
                    ["OrderedAmount_CA"] = new() { ColumnName = "OrderedAmount_CA", DataType = "decimal", IsMonetary = true, DisplayName = "CA Ordered Amount", Synonyms = new[] { "ordered amount", "ca award", "awarded gratuity" } },
                    ["ActualPaidAmount"] = new() { ColumnName = "ActualPaidAmount", DataType = "decimal", IsMonetary = true, DisplayName = "Corporation Paid Amount", Synonyms = new[] { "paid amount", "disbursed" } },
                    ["ActualPaidAmount_CA"] = new() { ColumnName = "ActualPaidAmount_CA", DataType = "decimal", IsMonetary = true, DisplayName = "CA Paid Amount" },
                    ["AdvocateName"] = new() { ColumnName = "AdvocateName", DataType = "string", DisplayName = "Advocate Name", Synonyms = new[] { "advocate", "counsel" } },
                    ["CurrentStage"] = new() { ColumnName = "CurrentStage", DataType = "string", DisplayName = "Current Stage", Synonyms = new[] { "stage" } },
                    ["NextHearingDate"] = new() { ColumnName = "NextHearingDate", DataType = "datetime", IsDate = true, DisplayName = "Next Hearing Date", Synonyms = new[] { "hearing date" } },
                    ["CaseStatus"] = new() { ColumnName = "CaseStatus", DataType = "string", DisplayName = "Case Status", Synonyms = new[] { "status", "pending", "disposed" } },
                    ["DisposalDate"] = new() { ColumnName = "DisposalDate", DataType = "datetime", IsDate = true, DisplayName = "Disposal Date" },
                    ["DisposalResult"] = new() { ColumnName = "DisposalResult", DataType = "string", DisplayName = "Disposal Result" },
                    ["IsInterestPayable"] = new() { ColumnName = "IsInterestPayable", DataType = "bool", DisplayName = "Is Interest Payable" },
                    ["InterestRate"] = new() { ColumnName = "InterestRate", DataType = "decimal", DisplayName = "Interest Rate" },
                    ["RetirementDate"] = new() { ColumnName = "RetirementDate", DataType = "datetime", IsDate = true, DisplayName = "Retirement Date" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterAppealDetails()
        {
            var t = new TableDescriptor
            {
                TableName = "APPEAL_DETAILS",
                PrimaryKey = "AppealID",
                ParentTable = "MVC_CASES",
                ForeignKeyToParent = "CaseID",
                DisplayName = "High Court MFA & Supreme Court Appeals",
                Module = "Appeal",
                Synonyms = new[] { "appeal", "mfa", "high court appeal", "slp", "supreme court appeal", "writ appeal", "stay order" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AppealID"] = new() { ColumnName = "AppealID", DataType = "int", IsPrimaryKey = true, DisplayName = "Appeal ID" },
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsForeignKey = true, ForeignTable = "MVC_CASES", ForeignColumn = "CaseID", DisplayName = "Case ID" },
                    ["CorpMFANumber"] = new() { ColumnName = "CorpMFANumber", DataType = "string", DisplayName = "Corp MFA Number", Synonyms = new[] { "mfa number", "mfa no", "appeal number" } },
                    ["CorpMFAYear"] = new() { ColumnName = "CorpMFAYear", DataType = "int", DisplayName = "Corp MFA Year", Synonyms = new[] { "mfa year", "appeal year" } },
                    ["HighCourtBench"] = new() { ColumnName = "HighCourtBench", DataType = "string", DisplayName = "High Court Bench", Synonyms = new[] { "bench", "dharwad bench", "kalaburagi bench", "principal bench" } },
                    ["StayGranted"] = new() { ColumnName = "StayGranted", DataType = "string", DisplayName = "Stay Granted", Synonyms = new[] { "stay status", "is stay granted" } },
                    ["StayDate"] = new() { ColumnName = "StayDate", DataType = "datetime", IsDate = true, DisplayName = "Stay Date" },
                    ["StayNature"] = new() { ColumnName = "StayNature", DataType = "string", DisplayName = "Stay Nature", Synonyms = new[] { "nature of stay", "interim stay" } },
                    ["CorpMFAStatus"] = new() { ColumnName = "CorpMFAStatus", DataType = "string", DisplayName = "Corp MFA Status" },
                    ["CorpMFAOutcome"] = new() { ColumnName = "CorpMFAOutcome", DataType = "string", DisplayName = "Corp MFA Outcome" },
                    ["ClaimantMFANumber"] = new() { ColumnName = "ClaimantMFANumber", DataType = "string", DisplayName = "Claimant MFA Number" },
                    ["ClaimantMFAYear"] = new() { ColumnName = "ClaimantMFAYear", DataType = "int", DisplayName = "Claimant MFA Year" },
                    ["ClaimantMFAStatus"] = new() { ColumnName = "ClaimantMFAStatus", DataType = "string", DisplayName = "Claimant MFA Status" },
                    ["FeasibilityReceived"] = new() { ColumnName = "FeasibilityReceived", DataType = "bool", DisplayName = "Feasibility Received" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterPettyBills()
        {
            var t = new TableDescriptor
            {
                TableName = "PETTY_BILLS",
                PrimaryKey = "PettyBillID",
                DisplayName = "Petty Bills & Settlement Vouchers",
                Module = "PettyBill",
                Synonyms = new[] { "petty bill", "petty bills", "settlement bill", "bill voucher", "tds payment", "approval voucher", "cheque settlement" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PettyBillID"] = new() { ColumnName = "PettyBillID", DataType = "int", IsPrimaryKey = true, DisplayName = "Petty Bill ID" },
                    ["CaseID"] = new() { ColumnName = "CaseID", DataType = "int", IsForeignKey = true, ForeignTable = "MVC_CASES", ForeignColumn = "CaseID", DisplayName = "Case ID" },
                    ["MVCNo"] = new() { ColumnName = "MVCNo", DataType = "string", DisplayName = "MVC Number", Synonyms = new[] { "case number", "mvc no" } },
                    ["MVCYear"] = new() { ColumnName = "MVCYear", DataType = "int", DisplayName = "MVC Year" },
                    ["DivisionName"] = new() { ColumnName = "DivisionName", DataType = "string", DisplayName = "Division Name", Synonyms = new[] { "division" } },
                    ["PetitionerName"] = new() { ColumnName = "PetitionerName", DataType = "string", DisplayName = "Petitioner Name", Synonyms = new[] { "claimant" } },
                    ["VehicleNo"] = new() { ColumnName = "VehicleNo", DataType = "string", DisplayName = "Vehicle Number", Synonyms = new[] { "bus no" } },
                    ["EnhancedAmount"] = new() { ColumnName = "EnhancedAmount", DataType = "decimal", IsMonetary = true, DisplayName = "Enhanced Amount" },
                    ["CourtCost"] = new() { ColumnName = "CourtCost", DataType = "decimal", IsMonetary = true, DisplayName = "Court Cost" },
                    ["TDSPercentage"] = new() { ColumnName = "TDSPercentage", DataType = "decimal", DisplayName = "TDS %" },
                    ["TDSAmount"] = new() { ColumnName = "TDSAmount", DataType = "decimal", IsMonetary = true, DisplayName = "TDS Amount", Synonyms = new[] { "tds deduction", "tax deducted" } },
                    ["NetPayable"] = new() { ColumnName = "NetPayable", DataType = "decimal", IsMonetary = true, DisplayName = "Net Payable Amount", Synonyms = new[] { "net payable", "balance payable", "net amount" } },
                    ["GrossPayable"] = new() { ColumnName = "GrossPayable", DataType = "decimal", IsMonetary = true, DisplayName = "Gross Payable Amount", Synonyms = new[] { "gross amount", "total liability" } },
                    ["TotalDeductions"] = new() { ColumnName = "TotalDeductions", DataType = "decimal", IsMonetary = true, DisplayName = "Total Deductions" },
                    ["BaseAwardAmount"] = new() { ColumnName = "BaseAwardAmount", DataType = "decimal", IsMonetary = true, DisplayName = "Base Award Amount" },
                    ["ApprovalLetterNo"] = new() { ColumnName = "ApprovalLetterNo", DataType = "string", DisplayName = "Approval Letter Number", Synonyms = new[] { "approval reference", "approval letter" } },
                    ["ApprovalDate"] = new() { ColumnName = "ApprovalDate", DataType = "datetime", IsDate = true, DisplayName = "Approval Date" },
                    ["BillDate"] = new() { ColumnName = "BillDate", DataType = "datetime", IsDate = true, DisplayName = "Bill Date" },
                    ["AwardType"] = new() { ColumnName = "AwardType", DataType = "string", DisplayName = "Award Type" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterPettyBillPayments()
        {
            var t = new TableDescriptor
            {
                TableName = "PETTY_BILL_PAYMENTS",
                PrimaryKey = "PaymentID",
                ParentTable = "PETTY_BILLS",
                ForeignKeyToParent = "PettyBillID",
                DisplayName = "Petty Bill Payment & Cheque Details",
                Module = "PettyBill",
                Synonyms = new[] { "cheque", "cancelled cheque", "cancelled payment", "bill payment", "disbursement" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PaymentID"] = new() { ColumnName = "PaymentID", DataType = "int", IsPrimaryKey = true, DisplayName = "Payment ID" },
                    ["PettyBillID"] = new() { ColumnName = "PettyBillID", DataType = "int", IsForeignKey = true, ForeignTable = "PETTY_BILLS", ForeignColumn = "PettyBillID", DisplayName = "Bill ID" },
                    ["ChequeNumber"] = new() { ColumnName = "ChequeNumber", DataType = "string", DisplayName = "Cheque Number", Synonyms = new[] { "cheque no" } },
                    ["ChequeDate"] = new() { ColumnName = "ChequeDate", DataType = "datetime", IsDate = true, DisplayName = "Cheque Date" },
                    ["Amount"] = new() { ColumnName = "Amount", DataType = "decimal", IsMonetary = true, DisplayName = "Payment Amount" },
                    ["BankName"] = new() { ColumnName = "BankName", DataType = "string", DisplayName = "Bank Name" },
                    ["Status"] = new() { ColumnName = "Status", DataType = "string", DisplayName = "Status", Synonyms = new[] { "payment status" } },
                    ["IsCancelled"] = new() { ColumnName = "IsCancelled", DataType = "bool", DisplayName = "Is Cancelled", Synonyms = new[] { "cancelled", "dishonoured" } },
                    ["CancellationReason"] = new() { ColumnName = "CancellationReason", DataType = "string", DisplayName = "Cancellation Reason" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterDivisionMaster()
        {
            var t = new TableDescriptor
            {
                TableName = "DIVISION_MASTER",
                PrimaryKey = "DivisionID",
                DisplayName = "NWKRTC Operating Divisions",
                Module = "Master",
                Synonyms = new[] { "division", "divisions", "hubballi", "belagavi", "dharwad", "haveri", "gadag", "uttara kannada", "bagalkot", "chikkodi" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsPrimaryKey = true, DisplayName = "Division ID" },
                    ["DivisionCode"] = new() { ColumnName = "DivisionCode", DataType = "string", DisplayName = "Division Code" },
                    ["DivisionNameEnglish"] = new() { ColumnName = "DivisionNameEnglish", DataType = "string", DisplayName = "Division Name (English)", Synonyms = new[] { "division name" } },
                    ["DivisionNameKannada"] = new() { ColumnName = "DivisionNameKannada", DataType = "string", DisplayName = "Division Name (Kannada)" },
                    ["IsActive"] = new() { ColumnName = "IsActive", DataType = "bool", DisplayName = "Is Active" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterMactMaster()
        {
            var t = new TableDescriptor
            {
                TableName = "MACT_MASTER",
                PrimaryKey = "MACTID",
                DisplayName = "Motor Accident Claims Tribunals",
                Module = "Master",
                Synonyms = new[] { "mact court", "tribunal", "mact" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MACTID"] = new() { ColumnName = "MACTID", DataType = "int", IsPrimaryKey = true, DisplayName = "MACT ID" },
                    ["MACTName"] = new() { ColumnName = "MACTName", DataType = "string", DisplayName = "Tribunal Name", Synonyms = new[] { "court name", "mact name" } },
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsDivisionTenant = true, DisplayName = "Division ID" },
                    ["Place"] = new() { ColumnName = "Place", DataType = "string", DisplayName = "Location" },
                    ["IsActive"] = new() { ColumnName = "IsActive", DataType = "bool", DisplayName = "Is Active" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterAdvocateMasters()
        {
            var t = new TableDescriptor
            {
                TableName = "ADVOCATE_MASTER",
                PrimaryKey = "AdvocateID",
                DisplayName = "Corporation Empanelled Advocates (MVC)",
                Module = "Master",
                Synonyms = new[] { "advocates", "panel advocate", "lawyer", "legal counsel", "empanelled advocate" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AdvocateID"] = new() { ColumnName = "AdvocateID", DataType = "int", IsPrimaryKey = true, DisplayName = "Advocate ID" },
                    ["AdvocateName"] = new() { ColumnName = "AdvocateName", DataType = "string", DisplayName = "Advocate Name" },
                    ["DivisionID"] = new() { ColumnName = "DivisionID", DataType = "int", IsDivisionTenant = true, DisplayName = "Division ID" },
                    ["ContactNo"] = new() { ColumnName = "ContactNo", DataType = "string", DisplayName = "Contact Number" },
                    ["IsActive"] = new() { ColumnName = "IsActive", DataType = "bool", DisplayName = "Is Active" }
                }
            };
            Tables[t.TableName] = t;
        }

        private static void RegisterJudgementRepo()
        {
            var t = new TableDescriptor
            {
                TableName = "JUDGEMENT_REPO",
                PrimaryKey = "JudgementID",
                DisplayName = "Precedent Judgments Repository",
                Module = "Precedents",
                Synonyms = new[] { "judgments", "rulings", "ratio decidendi", "precedents" },
                Columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase)
                {
                    ["JudgementID"] = new() { ColumnName = "JudgementID", DataType = "int", IsPrimaryKey = true, DisplayName = "Judgement ID" },
                    ["CaseType"] = new() { ColumnName = "CaseType", DataType = "string", DisplayName = "Case Type" },
                    ["Title"] = new() { ColumnName = "Title", DataType = "string", DisplayName = "Title / Citation" },
                    ["Court"] = new() { ColumnName = "Court", DataType = "string", DisplayName = "Court" },
                    ["JudgementDate"] = new() { ColumnName = "JudgementDate", DataType = "datetime", IsDate = true, DisplayName = "Judgment Date" },
                    ["KeyPrinciple"] = new() { ColumnName = "KeyPrinciple", DataType = "string", DisplayName = "Legal Principle" },
                    ["Keywords"] = new() { ColumnName = "Keywords", DataType = "string", DisplayName = "Keywords" }
                }
            };
            Tables[t.TableName] = t;
        }
    }
}

