using ZReader.Core.Domain;
using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class ReaderSessionTests
{
    [Fact]
    public async Task NextPage_persists_the_new_character_offset()
    {
        var repository = new InMemoryBookRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var book = await repository.AddBookAsync(new BookDraft("示例", "示例.txt", "book.zrbk", 20, 1), CancellationToken.None);
        var session = new ReaderSession(repository, new StubBookContentStore("第一段内容\n\n第二段内容\n\n第三段内容"), new ReaderPaginator());

        await session.LoadAsync(book.Id, targetCharacterCount: 6, CancellationToken.None);
        await session.NextPageAsync(CancellationToken.None);
        var state = await repository.GetReadingStateAsync(book.Id, CancellationToken.None);

        Assert.True(state.CharacterOffset > 0);
        Assert.Equal(state.CharacterOffset, session.CurrentOffset);
    }

    [Fact]
    public async Task SaveSettings_keeps_current_character_offset()
    {
        var repository = new InMemoryBookRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var book = await repository.AddBookAsync(new BookDraft("示例", "示例.txt", "book.zrbk", 20, 1), CancellationToken.None);
        var session = new ReaderSession(repository, new StubBookContentStore("第一段内容\n\n第二段内容\n\n第三段内容"), new ReaderPaginator());

        await session.LoadAsync(book.Id, targetCharacterCount: 6, CancellationToken.None);
        await session.NextPageAsync(CancellationToken.None);
        var offset = session.CurrentOffset;
        await session.SaveSettingsAsync(22, 1.8, ReaderTheme.Dark, CancellationToken.None);

        Assert.Equal(offset, session.CurrentOffset);
        Assert.Equal(ReaderTheme.Dark, session.Theme);
    }

    private sealed class StubBookContentStore(string content) : IEncryptedBookStore
    {
        public Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult(content);

        public Task WriteAsync(string relativePath, string content, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
