namespace ZReader.Core.Services;

/// <summary>
/// Stores encrypted book content in platform-private storage.
/// </summary>
public interface IEncryptedBookStore
{
    Task WriteAsync(string relativePath, string content, CancellationToken cancellationToken);

    Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
}
