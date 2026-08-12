using System.IO;

namespace ZReader.Core.Services;

/// <summary>
/// Imports decoded TXT bytes into the encrypted private shelf without creating a plaintext copy.
/// </summary>
public sealed class BookImportService
{
    private readonly ITextEncodingDetector _encodingDetector;
    private readonly IEncryptedBookStore _bookStore;
    private readonly IBookRepository _repository;

    public BookImportService(ITextEncodingDetector encodingDetector, IEncryptedBookStore bookStore, IBookRepository repository)
    {
        _encodingDetector = encodingDetector;
        _bookStore = bookStore;
        _repository = repository;
    }

    public async Task<ZReader.Core.Domain.Book> ImportAsync(
        string sourceFileName,
        ReadOnlyMemory<byte> bytes,
        TextEncodingChoice? selectedEncoding,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        var detection = _encodingDetector.Detect(bytes.Span);
        var choice = detection.IsConfident ? detection.SuggestedChoice : selectedEncoding;
        if (choice is null)
        {
            throw new EncodingSelectionRequiredException(detection.AvailableChoices);
        }

        var content = _encodingDetector.Decode(bytes.Span, choice.Value);
        var title = Path.GetFileNameWithoutExtension(sourceFileName).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "未命名书籍";
        }

        var relativePath = $"books/{Guid.NewGuid():N}.zrbk";
        try
        {
            await _bookStore.WriteAsync(relativePath, content, cancellationToken);
            return await _repository.AddBookAsync(
                new BookDraft(title, sourceFileName, relativePath, content.Length, EncryptedBookFormat.CurrentVersion),
                cancellationToken);
        }
        catch
        {
            await _bookStore.DeleteAsync(relativePath, cancellationToken);
            throw;
        }
    }
}
