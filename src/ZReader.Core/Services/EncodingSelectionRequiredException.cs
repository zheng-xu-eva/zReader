namespace ZReader.Core.Services;

/// <summary>
/// Signals that a TXT file cannot be imported until the user chooses an encoding.
/// </summary>
public sealed class EncodingSelectionRequiredException : Exception
{
    public EncodingSelectionRequiredException(IReadOnlyList<TextEncodingChoice> availableChoices)
        : base("The text encoding could not be identified automatically.")
    {
        AvailableChoices = availableChoices;
    }

    public IReadOnlyList<TextEncodingChoice> AvailableChoices { get; }
}
