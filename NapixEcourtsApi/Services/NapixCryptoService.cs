using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;

namespace NapixEcourtsApi.Services;

public interface INapixCryptoService
{
    /// <summary>
    /// Builds the pipe-separated key=value request string, encrypts it with AES-128-CBC
    /// (key = IV = Authentication Key), and base64-encodes it — i.e. the value
    /// to send as the "request_str" query/body parameter.
    /// </summary>
    string BuildEncryptedRequestStr(IDictionary<string, string> parameters);

    /// <summary>
    /// Computes the HMAC-SHA256 hex digest of the (unencrypted) pipe-separated request string,
    /// using the fixed shared key "15081947" — i.e. the value to send as "request_token".
    /// </summary>
    string ComputeRequestToken(IDictionary<string, string> parameters);

    /// <summary>
    /// Decrypts a base64-encoded "response_str" received from NAPIX back into plaintext JSON,
    /// using AES-128-CBC with key = IV = Authentication Key.
    /// </summary>
    string DecryptResponseStr(string responseStrBase64);

    /// <summary>
    /// Recomputes HMAC-SHA256 over the decrypted JSON and compares it (constant-time) against
    /// the "response_token" NAPIX sent, to verify the payload wasn't tampered with in transit.
    /// </summary>
    bool VerifyResponseToken(string decryptedJson, string responseToken);

    /// <summary>Computes the HMAC-SHA256 hex digest using the fixed shared key.</summary>
    string ComputeHmacHex(string input);
}

public class NapixCryptoService : INapixCryptoService
{
    private readonly NapixOptions _options;

    public NapixCryptoService(IOptions<NapixOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Joins parameters as "k1=v1|k2=v2|..." in the order they're provided.
    /// Order matters — it must match whatever order the NAPIX endpoint's documentation shows
    /// for that specific API, since the server reconstructs the same string to verify the hash.
    /// </summary>
    private static string BuildPipeString(IDictionary<string, string> parameters) =>
        string.Join("|", parameters.Select(kv => $"{kv.Key}={kv.Value}"));

    public string BuildEncryptedRequestStr(IDictionary<string, string> parameters)
    {
        var plainText = BuildPipeString(parameters);
        var keyBytes = GetKeyOrIvBytes(_options.AuthenticationKey);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = keyBytes; // NAPIX spec: Authentication Key and IV are the same value
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var base64 = Convert.ToBase64String(cipherBytes);
        return base64;
    }

    public string ComputeRequestToken(IDictionary<string, string> parameters)
    {
        var plainText = BuildPipeString(parameters);
        return ComputeHmacHex(plainText);
    }

    public string DecryptResponseStr(string responseStrBase64)
    {
        // Response may arrive URL-decoded already (depends on JSON deserializer); decode defensively.
        var normalized = Uri.UnescapeDataString(responseStrBase64);
        var cipherBytes = Convert.FromBase64String(normalized);
        var keyBytes = GetKeyOrIvBytes(_options.AuthenticationKey);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = keyBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool VerifyResponseToken(string decryptedJson, string responseToken)
    {
        var expected = ComputeHmacHex(decryptedJson);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(responseToken.ToLowerInvariant()));
    }

    public string ComputeHmacHex(string input)
    {
        var keyBytes = Encoding.ASCII.GetBytes(_options.HmacSharedKey);
        var dataBytes = Encoding.UTF8.GetBytes(input);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// AES-128 requires a 16-byte key. NAPIX's own .NET sample resizes/truncates to 16 bytes,
    /// so we mirror that rather than throwing if e-Committee's key happens to be a different length.
    /// </summary>
    private static byte[] GetKeyOrIvBytes(string authenticationKey)
    {
        var bytes = Encoding.ASCII.GetBytes(authenticationKey);
        Array.Resize(ref bytes, 16);
        return bytes;
    }
}
