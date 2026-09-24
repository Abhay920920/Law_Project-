using System.Text.Json;
using System.Text.Json.Serialization;

namespace NapixEcourtsApi.Models;

/// <summary>The outer envelope every NAPIX data API returns before decryption.</summary>
public class NapixEnvelope
{
    [JsonPropertyName("response_str")]
    public string ResponseStr { get; set; } = string.Empty;

    [JsonPropertyName("response_token")]
    public string ResponseToken { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// NAPIX also returns this same-shaped error body (no response_str) on failures like
/// INVALID_CNR, INVALID_TOKEN, RECORD_NOT_FOUND, etc. — check for "status" before assuming success.
/// </summary>
public class NapixErrorResponse
{
    [JsonPropertyName("httpCode")]
    public string? HttpCode { get; set; }

    [JsonPropertyName("httpMessage")]
    public string? HttpMessage { get; set; }

    [JsonPropertyName("moreInformation")]
    public string? MoreInformation { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusDescription")]
    public string? StatusDescription { get; set; }
}

/// <summary>Decrypted payload of dc-cnr-api/cnr — the full case history.</summary>
public class CnrCaseDetails
{
    [JsonPropertyName("date_of_filing"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateOfFiling { get; set; }
    [JsonPropertyName("cino"), JsonConverter(typeof(StringOrNumberConverter))] public string? Cino { get; set; }
    [JsonPropertyName("dt_regis"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateOfRegistration { get; set; }
    [JsonPropertyName("type_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? CaseTypeName { get; set; }
    [JsonPropertyName("fil_no"), JsonConverter(typeof(StringOrNumberConverter))] public string? FilingNumber { get; set; }
    [JsonPropertyName("fil_year"), JsonConverter(typeof(StringOrNumberConverter))] public string? FilingYear { get; set; }
    [JsonPropertyName("reg_no"), JsonConverter(typeof(StringOrNumberConverter))] public string? RegistrationNumber { get; set; }
    [JsonPropertyName("reg_year"), JsonConverter(typeof(StringOrNumberConverter))] public string? RegistrationYear { get; set; }
    [JsonPropertyName("date_first_list"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateFirstListed { get; set; }
    [JsonPropertyName("date_next_list"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateNextListed { get; set; }
    [JsonPropertyName("pend_disp"), JsonConverter(typeof(StringOrNumberConverter))] public string? PendingOrDisposed { get; set; } // "P" or "D"
    [JsonPropertyName("date_of_decision"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateOfDecision { get; set; }
    [JsonPropertyName("disp_nature"), JsonConverter(typeof(StringOrNumberConverter))] public string? DispositionNature { get; set; }
    [JsonPropertyName("desgname"), JsonConverter(typeof(StringOrNumberConverter))] public string? JudgeDesignation { get; set; }
    [JsonPropertyName("court_no"), JsonConverter(typeof(StringOrNumberConverter))] public string? CourtNumber { get; set; }
    [JsonPropertyName("court_est_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? CourtEstablishmentName { get; set; }
    [JsonPropertyName("est_code"), JsonConverter(typeof(StringOrNumberConverter))] public string? EstablishmentCode { get; set; }
    [JsonPropertyName("state_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? StateName { get; set; }
    [JsonPropertyName("dist_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? DistrictName { get; set; }
    [JsonPropertyName("purpose_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? PurposeName { get; set; }
    [JsonPropertyName("pet_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? PetitionerName { get; set; }
    [JsonPropertyName("pet_adv"), JsonConverter(typeof(StringOrNumberConverter))] public string? PetitionerAdvocate { get; set; }
    [JsonPropertyName("res_name"), JsonConverter(typeof(StringOrNumberConverter))] public string? RespondentName { get; set; }
    [JsonPropertyName("res_adv"), JsonConverter(typeof(StringOrNumberConverter))] public string? RespondentAdvocate { get; set; }
    [JsonPropertyName("date_last_list"), JsonConverter(typeof(StringOrNumberConverter))] public string? DateLastListed { get; set; }

    // The full response also nests historyofcasehearing / interimorder / finalorder / transfer /
    // writinfo / processes / iafiling / link_cases / objections / acts as sub-objects keyed
    // "sr_no1", "sr_no2", ... (dynamic, not a fixed array). Deserialize those on demand with
    // JsonDocument/JsonElement rather than fixed classes, since key names are dynamic sr_noN.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraSections { get; set; }
}

/// <summary>
/// Converts numbers, booleans, and strings gracefully into a string.
/// Many government APIs return numbers (e.g. court_no: 1, fil_year: 2026) where strings are modeled.
/// </summary>
public class StringOrNumberConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString()
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

/// <summary>Request body for GET /api/cases/{cnr} equivalents — kept simple, CNR comes from the route.</summary>
public record CurrentStatusRequest(IReadOnlyList<string> Cnrs);

/// <summary>Comprehensive case information including structured hearings, orders, and the raw payload.</summary>
public class CaseFullDetails
{
    public string? Cnr { get; set; }
    public string CourtType { get; set; } = "District Court"; // "High Court" or "District Court"
    public CnrCaseDetails? BasicInfo { get; set; }
    public List<CaseHearing> Hearings { get; set; } = new();
    public List<CaseOrder> InterimOrders { get; set; } = new();
    public List<CaseOrder> FinalOrdersAndJudgments { get; set; } = new();
    public List<JsonElement> IaFilings { get; set; } = new();
    public List<JsonElement> Acts { get; set; } = new();
    public List<JsonElement> Transfers { get; set; } = new();
    public List<JsonElement> Objections { get; set; } = new();
    public List<JsonElement> Processes { get; set; } = new();
    public List<JsonElement> ExtraParties { get; set; } = new();
    public List<JsonElement> LinkedCases { get; set; } = new();
    public JsonElement RawPayload { get; set; }
}

public class CaseOrder
{
    public string? OrderNumber { get; set; }
    public string? OrderDate { get; set; }
    public string OrderType { get; set; } = string.Empty; // "Interim" or "Final/Judgment"
    public string? ViewUrl { get; set; }
    public string? PdfDownloadUrl { get; set; }
    public JsonElement RawDetails { get; set; }
}

public class CaseHearing
{
    public string? HearingDate { get; set; }
    public string? PurposeOfHearing { get; set; }
    public string? BusinessTransacted { get; set; }
    public string? JudgeName { get; set; }
    public JsonElement RawDetails { get; set; }
}
