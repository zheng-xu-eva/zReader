using ZReader.Core.Domain;

namespace ZReader.Core.Services;

/// <summary>
/// Provides deterministic repository behavior for core tests without a platform database dependency.
/// </summary>
public sealed class InMemoryBookRepository : IBookRepository
{
    private readonly List<Book> _books = [];
    private readonly Dictionary<long, ReadingState> _states = [];
    private readonly Func<DateTimeOffset> _clock;
    private long _nextBookId = 1;

    public InMemoryBookRepository(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<Book> AddBookAsync(BookDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var now = _clock();
        var book = new Book
        {
            Id = _nextBookId++,
            Title = draft.Title,
            SourceFileName = draft.SourceFileName,
            EncryptedRelativePath = draft.EncryptedRelativePath,
            ImportedAt = now,
            LastReadAt = now,
            ContentLength = draft.ContentLength,
            EncryptionVersion = draft.EncryptionVersion
        };
        _books.Add(book);
        _states[book.Id] = ReadingState.CreateDefault(book.Id, now);
        return Task.FromResult(book);
    }

    public Task<IReadOnlyList<Book>> GetShelfAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Book>>(_books
            .OrderByDescending(book => book.LastReadAt)
            .ThenByDescending(book => book.ImportedAt)
            .ToArray());
    }

    public Task<ReadingState> GetReadingStateAsync(long bookId, CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(bookId, out var state))
        {
            throw new KeyNotFoundException($"Reading state for book {bookId} was not found.");
        }

        return Task.FromResult(Clone(state));
    }

    public Task SaveReadingStateAsync(ReadingState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_states.ContainsKey(state.BookId))
        {
            throw new KeyNotFoundException($"Reading state for book {state.BookId} was not found.");
        }

        state.UpdatedAt = _clock();
        _states[state.BookId] = Clone(state);
        var book = _books.Single(book => book.Id == state.BookId);
        book.LastReadAt = state.UpdatedAt;
        return Task.CompletedTask;
    }

    private static ReadingState Clone(ReadingState state)
    {
        return new ReadingState
        {
            BookId = state.BookId,
            CharacterOffset = state.CharacterOffset,
            FontSize = state.FontSize,
            LineSpacing = state.LineSpacing,
            Theme = state.Theme,
            UpdatedAt = state.UpdatedAt
        };
    }
}
