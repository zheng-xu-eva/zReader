using ZReader.Core.Domain;

namespace ZReader.Core.Tests;

public sealed class ReadingStateTests
{
    [Fact]
    public void CreateDefault_uses_first_character_and_readable_defaults()
    {
        var now = DateTimeOffset.UtcNow;

        var state = ReadingState.CreateDefault(bookId: 42, now);

        Assert.Equal(42, state.BookId);
        Assert.Equal(0, state.CharacterOffset);
        Assert.InRange(state.FontSize, 14, 28);
        Assert.InRange(state.LineSpacing, 1.2, 2.2);
        Assert.Equal(ReaderTheme.Light, state.Theme);
        Assert.Equal(now, state.UpdatedAt);
    }
}
