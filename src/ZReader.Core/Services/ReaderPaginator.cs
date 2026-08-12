namespace ZReader.Core.Services;

/// <summary>
/// Implements deterministic text pagination without depending on a platform-specific renderer.
/// </summary>
public sealed class ReaderPaginator : IReaderPaginator
{
    public ReaderPageSlice GetPage(string content, int targetCharacterCount, long offset)
    {
        ArgumentNullException.ThrowIfNull(content);
        var start = ClampOffset(offset, content.Length);
        var capacity = Math.Max(1, targetCharacterCount);
        var hardEnd = Math.Min(content.Length, start + capacity);
        var end = FindPreferredEnd(content, start, hardEnd);
        return new ReaderPageSlice(start, end, content[start..end]);
    }

    public long GetNextOffset(string content, int targetCharacterCount, long offset)
    {
        var page = GetPage(content, targetCharacterCount, offset);
        return SkipWhitespace(content, page.EndOffset);
    }

    public long GetPreviousOffset(string content, int targetCharacterCount, long offset)
    {
        ArgumentNullException.ThrowIfNull(content);
        var target = ClampOffset(offset, content.Length);
        if (target == 0)
        {
            return 0;
        }

        long pageStart = 0;
        long previousStart = 0;
        while (pageStart < target)
        {
            previousStart = pageStart;
            var next = GetNextOffset(content, targetCharacterCount, pageStart);
            if (next >= target || next <= pageStart)
            {
                return previousStart;
            }

            pageStart = next;
        }

        return previousStart;
    }

    public double GetProgress(long offset, int contentLength)
    {
        if (contentLength <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)offset / contentLength, 0, 1);
    }

    private static int FindPreferredEnd(string content, int start, int hardEnd)
    {
        if (hardEnd == content.Length)
        {
            return hardEnd;
        }

        for (var index = hardEnd - 1; index > start; index--)
        {
            if (content[index] == '\n' && content[index - 1] == '\n')
            {
                return index - 1;
            }
        }

        return hardEnd;
    }

    private static int SkipWhitespace(string content, long offset)
    {
        var index = ClampOffset(offset, content.Length);
        while (index < content.Length && char.IsWhiteSpace(content[index]))
        {
            index++;
        }

        return index;
    }

    private static int ClampOffset(long offset, int contentLength)
    {
        return (int)Math.Clamp(offset, 0, contentLength);
    }
}
