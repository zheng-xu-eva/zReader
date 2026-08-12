using ZReader.Core.Domain;

namespace ZReader.Core.Services;

/// <summary>
/// Coordinates book content, stable paging offsets, and persisted reading preferences.
/// </summary>
public sealed class ReaderSession
{
    private readonly IBookRepository _repository;
    private readonly IEncryptedBookStore _bookStore;
    private readonly IReaderPaginator _paginator;
    private string _content = string.Empty;
    private ReadingState? _state;
    private int _targetCharacterCount;

    public ReaderSession(IBookRepository repository, IEncryptedBookStore bookStore, IReaderPaginator paginator)
    {
        _repository = repository;
        _bookStore = bookStore;
        _paginator = paginator;
    }

    public long CurrentOffset => _state?.CharacterOffset ?? 0;

    public ReaderTheme Theme => _state?.Theme ?? ReaderTheme.Light;

    public double FontSize => _state?.FontSize ?? 18;

    public double LineSpacing => _state?.LineSpacing ?? 1.6;

    public double Progress => _paginator.GetProgress(CurrentOffset, _content.Length);

    public string PageText => _paginator.GetPage(_content, _targetCharacterCount, CurrentOffset).Text;

    public async Task LoadAsync(long bookId, int targetCharacterCount, CancellationToken cancellationToken)
    {
        var shelf = await _repository.GetShelfAsync(cancellationToken);
        var book = shelf.SingleOrDefault(item => item.Id == bookId)
            ?? throw new KeyNotFoundException($"Book {bookId} was not found.");
        _content = await _bookStore.ReadAsync(book.EncryptedRelativePath, cancellationToken);
        _state = await _repository.GetReadingStateAsync(bookId, cancellationToken);
        _state.CharacterOffset = Math.Clamp(_state.CharacterOffset, 0, _content.Length);
        _targetCharacterCount = Math.Max(1, targetCharacterCount);
    }

    public async Task NextPageAsync(CancellationToken cancellationToken)
    {
        EnsureLoaded();
        _state!.CharacterOffset = _paginator.GetNextOffset(_content, _targetCharacterCount, _state.CharacterOffset);
        await PersistAsync(cancellationToken);
    }

    public async Task PreviousPageAsync(CancellationToken cancellationToken)
    {
        EnsureLoaded();
        _state!.CharacterOffset = _paginator.GetPreviousOffset(_content, _targetCharacterCount, _state.CharacterOffset);
        await PersistAsync(cancellationToken);
    }

    public async Task SeekAsync(double progress, CancellationToken cancellationToken)
    {
        EnsureLoaded();
        _state!.CharacterOffset = (long)Math.Round(Math.Clamp(progress, 0, 1) * _content.Length, MidpointRounding.AwayFromZero);
        await PersistAsync(cancellationToken);
    }

    public async Task SaveSettingsAsync(double fontSize, double lineSpacing, ReaderTheme theme, CancellationToken cancellationToken)
    {
        EnsureLoaded();
        _state!.FontSize = Math.Clamp(fontSize, 14, 28);
        _state.LineSpacing = Math.Clamp(lineSpacing, 1.2, 2.2);
        _state.Theme = theme;
        await PersistAsync(cancellationToken);
    }

    public void UpdatePageCapacity(int targetCharacterCount)
    {
        _targetCharacterCount = Math.Max(1, targetCharacterCount);
    }

    private Task PersistAsync(CancellationToken cancellationToken) => _repository.SaveReadingStateAsync(_state!, cancellationToken);

    private void EnsureLoaded()
    {
        if (_state is null)
        {
            throw new InvalidOperationException("The reader session must be loaded before it can be used.");
        }
    }
}
