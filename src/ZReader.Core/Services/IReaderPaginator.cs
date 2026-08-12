namespace ZReader.Core.Services;

/// <summary>
/// Produces display pages using offsets that remain valid when display settings change.
/// </summary>
public interface IReaderPaginator
{
    ReaderPageSlice GetPage(string content, int targetCharacterCount, long offset);

    long GetNextOffset(string content, int targetCharacterCount, long offset);

    long GetPreviousOffset(string content, int targetCharacterCount, long offset);

    double GetProgress(long offset, int contentLength);
}
