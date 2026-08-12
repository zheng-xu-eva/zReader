using System.Security.Cryptography;
using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class EncryptedBookFormatTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Encrypt_then_decrypt_returns_original_text()
    {
        var payload = EncryptedBookFormat.Encrypt("加密正文", Key);

        var text = EncryptedBookFormat.Decrypt(payload, Key);

        Assert.Equal("加密正文", text);
    }

    [Fact]
    public void Encrypt_creates_distinct_payloads_for_the_same_text()
    {
        var first = EncryptedBookFormat.Encrypt("加密正文", Key);
        var second = EncryptedBookFormat.Encrypt("加密正文", Key);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Decrypt_rejects_modified_ciphertext()
    {
        var payload = EncryptedBookFormat.Encrypt("加密正文", Key);
        payload[^1] ^= 1;

        Assert.Throws<CryptographicException>(() => EncryptedBookFormat.Decrypt(payload, Key));
    }
}
