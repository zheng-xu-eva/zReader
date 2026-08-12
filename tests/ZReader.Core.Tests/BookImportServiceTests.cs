using System.Text;
using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class BookImportServiceTests
{
    [Fact]
    public async Task Import_with_detected_utf8_creates_encrypted_book_and_shelf_entry()
    {
        var repository = new InMemoryBookRepository();
        var store = new RecordingBookStore();
        var importer = new BookImportService(new TextEncodingDetector(), store, repository);

        var book = await importer.ImportAsync("我的书.txt", Encoding.UTF8.GetBytes("正文内容"), null, CancellationToken.None);

        Assert.Equal("我的书", book.Title);
        Assert.Single(store.WrittenContents);
        Assert.Equal("正文内容", store.WrittenContents.Single().Content);
        Assert.StartsWith("books/", store.WrittenContents.Single().Path);
        Assert.Equal(store.WrittenContents.Single().Path, book.EncryptedRelativePath);
    }

    [Fact]
    public async Task Import_rejects_inconclusive_bytes_until_encoding_is_selected()
    {
        var importer = new BookImportService(new TextEncodingDetector(), new RecordingBookStore(), new InMemoryBookRepository());
        var unknownEncodingBytes = new byte[] { 0xFF, 0x81, 0x40 };

        await Assert.ThrowsAsync<EncodingSelectionRequiredException>(() =>
            importer.ImportAsync("编码未知.txt", unknownEncodingBytes, null, CancellationToken.None));
    }

    private sealed class RecordingBookStore : IEncryptedBookStore
    {
        public List<(string Path, string Content)> WrittenContents { get; } = [];

        public Task WriteAsync(string relativePath, string content, CancellationToken cancellationToken)
        {
            WrittenContents.Add((relativePath, content));
            return Task.CompletedTask;
        }

        public Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
        {
            WrittenContents.RemoveAll(item => item.Path == relativePath);
            return Task.CompletedTask;
        }
    }
}
