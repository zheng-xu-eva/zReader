namespace ZReader.Core.Services;

/// <summary>
/// Contains the encoding suggestion and whether importing can proceed without user input.
/// </summary>
public sealed record TextEncodingDetection(
    TextEncodingChoice SuggestedChoice,
    bool IsConfident,
    IReadOnlyList<TextEncodingChoice> AvailableChoices);
