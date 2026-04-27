using PatchPlatform.Shared.Contracts;
using PatchPlatform.Shared.Crypto;

namespace PatchPlatform.Server.Tests;

public class CatalogSignerTests
{
    [Fact]
    public void VerifyEnvelope_ValidSignature_ReturnsTrue()
    {
        var secret = "test-secret-key";
        var payload = """{"apps":[]}""";
        var signature = CatalogSigner.ComputeHmac(payload, secret);
        var envelope = new CatalogSnapshotEnvelopeDto(1, payload, signature, DateTimeOffset.UtcNow);
        Assert.True(CatalogSigner.VerifyEnvelope(envelope, secret));
    }

    [Fact]
    public void VerifyEnvelope_InvalidSignature_ReturnsFalse()
    {
        var envelope = new CatalogSnapshotEnvelopeDto(1, """{"apps":[]}""", "invalidsignature", DateTimeOffset.UtcNow);
        Assert.False(CatalogSigner.VerifyEnvelope(envelope, "test-secret-key"));
    }

    [Fact]
    public void VerifyEnvelope_WrongSecret_ReturnsFalse()
    {
        var payload = """{"apps":[]}""";
        var signature = CatalogSigner.ComputeHmac(payload, "correct-secret");
        var envelope = new CatalogSnapshotEnvelopeDto(1, payload, signature, DateTimeOffset.UtcNow);
        Assert.False(CatalogSigner.VerifyEnvelope(envelope, "wrong-secret"));
    }

    [Fact]
    public void ComputeSha256_ConsistentHash()
    {
        var hash1 = CatalogSigner.ComputeSha256("test input");
        var hash2 = CatalogSigner.ComputeSha256("test input");
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void HashSecret_DifferentInputs_ProduceDifferentHashes()
    {
        var h1 = CatalogSigner.HashSecret("secret1");
        var h2 = CatalogSigner.HashSecret("secret2");
        Assert.NotEqual(h1, h2);
    }
}
