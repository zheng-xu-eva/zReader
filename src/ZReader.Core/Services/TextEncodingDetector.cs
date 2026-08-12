using System.Text;

namespace ZReader.Core.Services;

/// <summary>
/// Provides conservative TXT encoding detection. Non-UTF-8 files require an explicit user choice.
/// </summary>
public sealed class TextEncodingDetector : ITextEncodingDetector
{
    private static readonly TextEncodingChoice[] ManualChoices =
    [
        TextEncodingChoice.Gbk,
        TextEncodingChoice.Gb18030
    ];

    public TextEncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TextEncodingDetection Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            return new TextEncodingDetection(TextEncodingChoice.Utf8Bom, true, [TextEncodingChoice.Utf8Bom]);
        }

        try
        {
            CreateStrictEncoding(TextEncodingChoice.Utf8).GetString(bytes);
            return new TextEncodingDetection(TextEncodingChoice.Utf8, true, [TextEncodingChoice.Utf8]);
        }
        catch (DecoderFallbackException)
        {
            return new TextEncodingDetection(TextEncodingChoice.Gb18030, false, ManualChoices);
        }
    }

    public string Decode(ReadOnlySpan<byte> bytes, TextEncodingChoice choice)
    {
        return CreateStrictEncoding(choice).GetString(bytes);
    }

    private static Encoding CreateStrictEncoding(TextEncodingChoice choice)
    {
        return choice switch
        {
            TextEncodingChoice.Utf8 or TextEncodingChoice.Utf8Bom =>
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            TextEncodingChoice.Gbk => Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            TextEncodingChoice.Gb18030 => Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unsupported text encoding.")
        };
    }
}
