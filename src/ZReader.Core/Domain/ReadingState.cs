namespace ZReader.Core.Domain;

/// <summary>
/// Stores the recoverable reading position and display preferences for one book.
/// </summary>
public sealed class ReadingState
{
    public long BookId { get; set; }

    public long CharacterOffset { get; set; }

    public double FontSize { get; set; }

    public double LineSpacing { get; set; }

    public ReaderTheme Theme { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static ReadingState CreateDefault(long bookId, DateTimeOffset now)
    {
        return new ReadingState
        {
            BookId = bookId,
            CharacterOffset = 0,
            FontSize = 18,
            LineSpacing = 1.6,
            Theme = ReaderTheme.Light,
            UpdatedAt = now
        };
    }
}
