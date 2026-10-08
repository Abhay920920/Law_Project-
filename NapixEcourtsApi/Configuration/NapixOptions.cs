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

    /// <summary>Optional High Court password if mandated by a specific High Court portal.</summary>
    public string? HcPassword { get; set; } = null;

    /// <summary>MVC Application credentials.</summary>
    public NapixModuleCredentials? MVC { get; set; }

    /// <summary>Labour Application credentials.</summary>
    public NapixModuleCredentials? Labour { get; set; }

    /// <summary>High Court Application credentials (subscribed separately from DC).</summary>
    public NapixModuleCredentials? HC { get; set; }

    /// <summary>
    /// Resolves the credentials for the specified module ("Labour" or "MVC"),
    /// falling back to root settings if module-specific credentials are not defined.
    /// </summary>
    public NapixModuleCredentials GetModuleCredentials(string? module)
    {
        if (string.Equals(module, "Labour", StringComparison.OrdinalIgnoreCase) && Labour != null && !string.IsNullOrWhiteSpace(Labour.ClientId))
        {
            return new NapixModuleCredentials
            {
                ClientId = Labour.ClientId,
                ClientSecret = Labour.ClientSecret,
                DeptId = !string.IsNullOrWhiteSpace(Labour.DeptId) ? Labour.DeptId : DeptId,
                AuthenticationKey = !string.IsNullOrWhiteSpace(Labour.AuthenticationKey) ? Labour.AuthenticationKey : AuthenticationKey,
                HcPassword = !string.IsNullOrWhiteSpace(Labour.HcPassword) ? Labour.HcPassword : (HC?.HcPassword ?? HcPassword)
            };
        }

        // HC module uses same gateway credentials but needs the HcPassword in pipe params
        if (string.Equals(module, "HC", StringComparison.OrdinalIgnoreCase) && HC != null && !string.IsNullOrWhiteSpace(HC.ClientId))
        {
            return new NapixModuleCredentials
            {
                ClientId = HC.ClientId,
                ClientSecret = HC.ClientSecret,
                DeptId = !string.IsNullOrWhiteSpace(HC.DeptId) ? HC.DeptId : DeptId,
                AuthenticationKey = !string.IsNullOrWhiteSpace(HC.AuthenticationKey) ? HC.AuthenticationKey : AuthenticationKey,
                HcPassword = !string.IsNullOrWhiteSpace(HC.HcPassword) ? HC.HcPassword : HcPassword
            };
        }

        if (MVC != null && !string.IsNullOrWhiteSpace(MVC.ClientId))
        {
            return new NapixModuleCredentials
            {
                ClientId = MVC.ClientId,
                ClientSecret = MVC.ClientSecret,
                DeptId = !string.IsNullOrWhiteSpace(MVC.DeptId) ? MVC.DeptId : DeptId,
                AuthenticationKey = !string.IsNullOrWhiteSpace(MVC.AuthenticationKey) ? MVC.AuthenticationKey : AuthenticationKey,
                HcPassword = !string.IsNullOrWhiteSpace(MVC.HcPassword) ? MVC.HcPassword : HcPassword
            };
        }

        return new NapixModuleCredentials
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            DeptId = DeptId,
            AuthenticationKey = AuthenticationKey,
        };
    }
}

public class NapixModuleCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string DeptId { get; set; } = string.Empty;
    public string AuthenticationKey { get; set; } = string.Empty;

    /// <summary>
    /// HC-specific password included in the encrypted pipe for HC API calls.
    /// Issued by NIC e-Committee alongside the Authentication Key for HC subscriptions.
    /// </summary>
    public string? HcPassword { get; set; }
}
