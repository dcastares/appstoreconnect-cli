using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppStoreConnectCli.Asc.Auth;

public class JwtTokenProvider
{
    private readonly string _keyId;
    private readonly string _issuerId;
    private readonly string _privateKeyPath;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    // Renew 60 seconds before actual expiry
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(1200);
    private static readonly TimeSpan RenewBuffer = TimeSpan.FromSeconds(60);

    public JwtTokenProvider(string keyId, string issuerId, string privateKeyPath)
    {
        _keyId = keyId;
        _issuerId = issuerId;
        _privateKeyPath = privateKeyPath;
    }

    public string GetToken()
    {
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry - RenewBuffer)
            return _cachedToken;

        _cachedToken = GenerateToken();
        _tokenExpiry = DateTimeOffset.UtcNow + TokenLifetime;
        return _cachedToken;
    }

    private string GenerateToken()
    {
        var now = DateTimeOffset.UtcNow;
        var iat = now.ToUnixTimeSeconds();
        var exp = (now + TokenLifetime).ToUnixTimeSeconds();

        var header = new { alg = "ES256", kid = _keyId, typ = "JWT" };
        var payload = new { iss = _issuerId, iat, exp, aud = "appstoreconnect-v1" };

        var headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));

        var signingInput = $"{headerB64}.{payloadB64}";

        var pem = File.ReadAllText(_privateKeyPath);
        var keyBytes = LoadPkcs8KeyBytes(pem);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

        var signingBytes = Encoding.ASCII.GetBytes(signingInput);
        var signature = ecdsa.SignData(signingBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var sigB64 = Base64UrlEncode(signature);

        return $"{signingInput}.{sigB64}";
    }

    private static byte[] LoadPkcs8KeyBytes(string pem)
    {
        // Strip PEM headers/footers and whitespace
        var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("-----"))
                continue;
            sb.Append(trimmed);
        }
        return Convert.FromBase64String(sb.ToString());
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
