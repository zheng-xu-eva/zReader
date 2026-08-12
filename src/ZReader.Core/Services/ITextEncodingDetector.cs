namespace ZReader.Core.Services;

/// <summary>
/// Detects supported TXT encodings and decodes bytes selected by the importer.
/// </summary>
public interface ITextEncodingDetector
{
    TextEncodingDetection Detect(ReadOnlySpan<byte> bytes);

    string Decode(ReadOnlySpan<byte> bytes, TextEncodingChoice choice);
}
