using System.Security.Cryptography;
using System.Text;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Shared.Crypto;

public static class CatalogSigner
{
    public static string ComputeHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyEnvelope(CatalogSnapshotEnvelopeDto envelope, string secret)
    {
        var expected = ComputeHmac(envelope.Payload, secret);
        return string.Equals(expected, envelope.Signature, StringComparison.Ordinal);
    }

    public static string ComputeSha256(Stream stream)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string HashSecret(string secret)
    {
        return ComputeSha256(secret);
    }
}
