using System.Text;
using System.Security.Cryptography;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using ZReader.Core.Services;

namespace ZReader.Platforms.Android;

/// <summary>
/// Stores AES-GCM encrypted book content under the app-private files directory.
/// The AES key is non-exportable and held by Android Keystore.
/// </summary>
public sealed class AndroidEncryptedBookStore : IEncryptedBookStore
{
    private const string KeyAlias = "zreader-book-content-key-v1";
    private static readonly byte[] Magic = "ZRBK"u8.ToArray();
    private readonly string _storageRoot;

    public AndroidEncryptedBookStore()
    {
        _storageRoot = Path.Combine(FileSystem.AppDataDirectory, "books");
    }

    public async Task WriteAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var targetPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var key = GetOrCreateKey();
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        cipher.Init(CipherMode.EncryptMode, key);
        var encryptedBytes = cipher.DoFinal(Encoding.UTF8.GetBytes(content));
        var nonce = cipher.GetIV();
        var payload = new byte[6 + nonce.Length + encryptedBytes.Length];
        Magic.CopyTo(payload, 0);
        payload[4] = 1;
        payload[5] = checked((byte)nonce.Length);
        nonce.CopyTo(payload, 6);
        encryptedBytes.CopyTo(payload, 6 + nonce.Length);

        await File.WriteAllBytesAsync(targetPath, payload, cancellationToken);
    }

    public async Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        var payload = await File.ReadAllBytesAsync(ResolvePath(relativePath), cancellationToken);
        if (payload.Length < 6 || !payload.AsSpan(0, 4).SequenceEqual(Magic) || payload[4] != 1)
        {
            throw new CryptographicException("The encrypted book format is invalid or unsupported.");
        }

        var nonceLength = payload[5];
        if (nonceLength == 0 || payload.Length <= 6 + nonceLength)
        {
            throw new CryptographicException("The encrypted book nonce is invalid.");
        }

        var nonce = payload.AsSpan(6, nonceLength).ToArray();
        var ciphertext = payload.AsSpan(6 + nonceLength).ToArray();
        try
        {
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
            using var parameters = new GCMParameterSpec(128, nonce);
            cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), parameters);
            return new UTF8Encoding(false, true).GetString(cipher.DoFinal(ciphertext));
        }
        catch (Javax.Crypto.AEADBadTagException exception)
        {
            throw new CryptographicException("The encrypted book authentication tag is invalid.", exception);
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private IKey GetOrCreateKey()
    {
        var keyStore = KeyStore.GetInstance("AndroidKeyStore");
        keyStore.Load(null);
        var existingKey = keyStore.GetKey(KeyAlias, null);
        if (existingKey is not null)
        {
            return existingKey;
        }

        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
        using var specification = new KeyGenParameterSpec.Builder(
                KeyAlias,
                KeyProperties.PurposeEncrypt | KeyProperties.PurposeDecrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .Build();
        generator.Init(specification);
        return generator.GenerateKey();
    }

    private string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(_storageRoot);
        var path = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, normalized));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !string.Equals(path, root, StringComparison.Ordinal))
        {
            throw new ArgumentException("The encrypted book path must remain inside private storage.", nameof(relativePath));
        }

        return path;
    }
}
