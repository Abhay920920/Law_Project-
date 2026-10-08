using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;

namespace NapixEcourtsApi.Services;

public interface INapixCryptoService
{
    /// <summary>Builds pipe-separated request string, encrypts with AES-128-CBC and base64-encodes.</summary>
    string BuildEncryptedRequestStr(IDictionary<string, string> parameters, string? authKey = null);

    /// <summary>Computes HMAC-SHA256 hex digest of unencrypted pipe-separated string.</summary>
    string ComputeRequestToken(IDictionary<string, string> parameters);

    /// <summary>Decrypts base64-encoded response_str back into plaintext JSON with 16-zero-bytes fallback.</summary>
    string DecryptResponseStr(string responseStrBase64, string? authKey = null);

    /// <summary>Recomputes HMAC-SHA256 and constant-time compares against response_token.</summary>
    bool VerifyResponseToken(string decryptedJson, string responseToken);

    /// <summary>Computes HMAC-SHA256 hex digest using fixed shared key.</summary>
    string ComputeHmacHex(string input);
}

public class NapixCryptoService : INapixCryptoService
{
    private static readonly byte[] ZeroKey = new byte[16];
    private readonly NapixOptions _options;

    public NapixCryptoService(IOptions<NapixOptions> options)
    {
        _options = options.Value;
    }

    private static string BuildPipeString(IDictionary<string, string> parameters) =>
        string.Join("|", parameters.Select(kv => $"{kv.Key}={kv.Value}"));

    public string BuildEncryptedRequestStr(IDictionary<string, string> parameters, string? authKey = null)
    {
        var keyBytes = GetKeyBytes(authKey ?? _options.AuthenticationKey);
        using var aes = CreateAes(keyBytes);
        byte[] plainBytes = Encoding.UTF8.GetBytes(BuildPipeString(parameters));
        return Convert.ToBase64String(aes.CreateEncryptor().TransformFinalBlock(plainBytes, 0, plainBytes.Length));
    }

    public string ComputeRequestToken(IDictionary<string, string> parameters) =>
        ComputeHmacHex(BuildPipeString(parameters));

    public string DecryptResponseStr(string responseStrBase64, string? authKey = null)
    {
        if (string.IsNullOrWhiteSpace(responseStrBase64)) return string.Empty;
        var clean = Uri.UnescapeDataString(responseStrBase64).Trim().Replace("\\/", "/").Replace("-", "+").Replace("_", "/");
        int mod4 = clean.Length % 4;
        if (mod4 > 0) clean += new string('=', 4 - mod4);

        byte[] cipherBytes;
        try { cipherBytes = Convert.FromBase64String(clean); } catch { return string.Empty; }

        var keyBytes = GetKeyBytes(authKey ?? _options.AuthenticationKey);
        try
        {
            using var aes = CreateAes(keyBytes);
            return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(cipherBytes, 0, cipherBytes.Length));
        }
        catch
        {
            // eCourts 16 zero-bytes error payload fallback (Section 13.2.2 / status 626)
            try
            {
                using var zeroAes = CreateAes(ZeroKey);
                return Encoding.UTF8.GetString(zeroAes.CreateDecryptor().TransformFinalBlock(cipherBytes, 0, cipherBytes.Length));
            }
            catch { return string.Empty; }
        }
    }

    public bool VerifyResponseToken(string decryptedJson, string responseToken)
    {
        if (string.IsNullOrWhiteSpace(responseToken)) return true;
        var expected = ComputeHmacHex(decryptedJson);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(responseToken.ToLowerInvariant()));
    }

    public string ComputeHmacHex(string input)
    {
        var keyBytes = Encoding.ASCII.GetBytes(_options.HmacSharedKey);
        var dataBytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, dataBytes)).ToLowerInvariant();
    }

    private static Aes CreateAes(byte[] key)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = key; // NAPIX spec: Key and IV share identical value
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    private static byte[] GetKeyBytes(string key)
    {
        var bytes = Encoding.ASCII.GetBytes(key ?? string.Empty);
        Array.Resize(ref bytes, 16);
        return bytes;
    }
}
