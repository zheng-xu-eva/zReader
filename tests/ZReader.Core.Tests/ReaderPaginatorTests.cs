using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class ReaderPaginatorTests
{
    private readonly ReaderPaginator _paginator = new();

    [Fact]
    public void GetPage_prefers_a_paragraph_boundary_before_page_limit()
    {
        var page = _paginator.GetPage("甲乙丙\n\n丁戊己", targetCharacterCount: 5, offset: 0);

        Assert.Equal("甲乙丙", page.Text);
        Assert.Equal(0, page.StartOffset);
        Assert.Equal(3, page.EndOffset);
    }

    [Fact]
    public void GetNextOffset_returns_following_content_after_boundary()
    {
        var offset = _paginator.GetNextOffset("甲乙丙\n\n丁戊己", targetCharacterCount: 5, offset: 0);

        Assert.Equal(5, offset);
    }

    [Theory]
    [InlineData(0, 100, 0d)]
    [InlineData(50, 100, .5d)]
    [InlineData(200, 100, 1d)]
    public void GetProgress_clamps_to_valid_range(long offset, int length, double expected)
    {
        Assert.Equal(expected, _paginator.GetProgress(offset, length));
    }
}
