namespace ZReader.Core.Services;

/// <summary>
/// Describes one visible page by its stable offsets in the source text.
/// </summary>
public sealed record ReaderPageSlice(long StartOffset, long EndOffset, string Text);
