using System.Security.Cryptography;
using System.Text;

namespace ZReader.Core.Services;

/// <summary>
/// Serializes UTF-8 book text to a versioned AES-GCM binary payload.
/// </summary>
public static class EncryptedBookFormat
{
    public const byte CurrentVersion = 1;

    private static readonly byte[] Magic = "ZRBK"u8.ToArray();
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int HeaderLength = 7;

    public static byte[] Encrypt(string content, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(content);

        var plaintext = Encoding.UTF8.GetBytes(content);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Magic);

        var payload = new byte[HeaderLength + nonce.Length + tag.Length + ciphertext.Length];
        Magic.CopyTo(payload, 0);
        payload[4] = CurrentVersion;
        payload[5] = NonceLength;
        payload[6] = TagLength;
        nonce.CopyTo(payload, HeaderLength);
        tag.CopyTo(payload, HeaderLength + NonceLength);
        ciphertext.CopyTo(payload, HeaderLength + NonceLength + TagLength);
        return payload;
    }

    public static string Decrypt(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> key)
    {
        if (payload.Length < HeaderLength + NonceLength + TagLength || !payload[..4].SequenceEqual(Magic))
        {
            throw new CryptographicException("The encrypted book format is invalid.");
        }

        if (payload[4] != CurrentVersion || payload[5] != NonceLength || payload[6] != TagLength)
        {
            throw new CryptographicException("The encrypted book format version is unsupported.");
        }

        var nonce = payload.Slice(HeaderLength, NonceLength);
        var tag = payload.Slice(HeaderLength + NonceLength, TagLength);
        var ciphertext = payload[(HeaderLength + NonceLength + TagLength)..];
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, Magic);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new CryptographicException("The encrypted book authentication tag is invalid.", exception);
        }

        return new UTF8Encoding(false, true).GetString(plaintext);
    }
}
