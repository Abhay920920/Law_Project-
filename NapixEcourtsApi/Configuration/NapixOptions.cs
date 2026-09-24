namespace NapixEcourtsApi.Configuration;

/// <summary>
/// Maps to the "Napix" section in appsettings.json / appsettings.{Environment}.json / env vars.
/// In production, prefer environment variables or a secret store (Key Vault, dotnet user-secrets)
/// for ClientSecret and AuthenticationKey rather than committing them to appsettings.json.
/// </summary>
public class NapixOptions
{
    public const string SectionName = "Napix";

    /// <summary>OAuth2 token endpoint: https://delhigw.napix.gov.in/nic/ecourts/oauth2/token</summary>
    public string TokenUrl { get; set; } = string.Empty;

    /// <summary>Base URL for all data APIs, e.g. https://delhigw.napix.gov.in/nic/ecourts</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>NAPIX Application "Key" (Client ID) — from App creation step in NAPIX portal.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>NAPIX Application "Secret" (Client Secret) — shown once at App creation.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Department ID — same as NAPIX Subscriber User Name, issued by e-Committee.</summary>
    public string DeptId { get; set; } = string.Empty;

    /// <summary>
    /// AES-128 Authentication Key issued by e-Committee. Used as both the AES key AND the IV
    /// (per NAPIX spec — this is unusual but is exactly what the documentation specifies).
    /// Must be 16 bytes for AES-128 once encoded as ASCII/UTF8 bytes.
    /// </summary>
    public string AuthenticationKey { get; set; } = string.Empty;

    /// <summary>API version query param, currently "v1.0" for all eCourts endpoints.</summary>
    public string ApiVersion { get; set; } = "v1.0";

    /// <summary>
    /// Fixed shared HMAC-SHA256 key documented by NAPIX for hashing request/response strings.
    /// Per Annexure-A of the eCourts Open API spec this is the literal string "15081947" —
    /// it is NOT subscriber-specific (unlike AuthenticationKey).
    /// </summary>
    public string HmacSharedKey { get; set; } = "15081947";
}
