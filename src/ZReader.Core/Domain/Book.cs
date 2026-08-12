namespace ZReader.Core.Domain;

/// <summary>
/// Represents a book stored in the private encrypted local shelf.
/// </summary>
public sealed class Book
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string EncryptedRelativePath { get; set; } = string.Empty;

    public DateTimeOffset ImportedAt { get; set; }

    public DateTimeOffset LastReadAt { get; set; }

    public long ContentLength { get; set; }

    public int EncryptionVersion { get; set; }
}
