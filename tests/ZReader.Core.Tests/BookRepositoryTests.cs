using ZReader.Core.Domain;
using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class BookRepositoryTests : IAsyncLifetime
{
    private InMemoryBookRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new InMemoryBookRepository(() => DateTimeOffset.Parse("2026-08-12T00:00:00+00:00"));
        await _repository.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveReadingState_persists_per_book_preferences_and_offset()
    {
        var book = await _repository.AddBookAsync(
            new BookDraft("示例", "示例.txt", "books/1.zrbk", 128, EncryptedBookFormat.CurrentVersion),
            CancellationToken.None);
        var state = ReadingState.CreateDefault(book.Id, DateTimeOffset.UtcNow);
        state.CharacterOffset = 321;
        state.FontSize = 22;
        state.LineSpacing = 1.8;
        state.Theme = ReaderTheme.Dark;

        await _repository.SaveReadingStateAsync(state, CancellationToken.None);
        var restored = await _repository.GetReadingStateAsync(book.Id, CancellationToken.None);

        Assert.Equal(321, restored.CharacterOffset);
        Assert.Equal(22, restored.FontSize);
        Assert.Equal(1.8, restored.LineSpacing);
        Assert.Equal(ReaderTheme.Dark, restored.Theme);
    }
}
