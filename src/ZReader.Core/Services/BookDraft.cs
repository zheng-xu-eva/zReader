namespace ZReader.Core.Services;

/// <summary>
/// Contains validated metadata needed to create a local shelf entry.
/// </summary>
public sealed record BookDraft(
    string Title,
    string SourceFileName,
    string EncryptedRelativePath,
    long ContentLength,
    int EncryptionVersion);
