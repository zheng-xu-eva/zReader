using ZReader.Core.Domain;

namespace ZReader.Core.Services;

/// <summary>
/// Persists the encrypted local shelf and per-book reading state.
/// </summary>
public interface IBookRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<Book> AddBookAsync(BookDraft draft, CancellationToken cancellationToken);

    Task<IReadOnlyList<Book>> GetShelfAsync(CancellationToken cancellationToken);

    Task<ReadingState> GetReadingStateAsync(long bookId, CancellationToken cancellationToken);

    Task SaveReadingStateAsync(ReadingState state, CancellationToken cancellationToken);
}
