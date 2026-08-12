using System.Text;
using ZReader.Core.Services;

namespace ZReader.Core.Tests;

public sealed class TextEncodingDetectorTests
{
    private readonly TextEncodingDetector _detector = new();

    [Fact]
    public void Detect_identifies_valid_utf8()
    {
        var result = _detector.Detect(Encoding.UTF8.GetBytes("轻量阅读器"));

        Assert.True(result.IsConfident);
        Assert.Equal(TextEncodingChoice.Utf8, result.SuggestedChoice);
    }

    [Fact]
    public void Detect_identifies_utf8_bom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("正文")).ToArray();

        var result = _detector.Detect(bytes);

        Assert.True(result.IsConfident);
        Assert.Equal(TextEncodingChoice.Utf8Bom, result.SuggestedChoice);
    }

    [Fact]
    public void Detect_requires_manual_selection_for_invalid_utf8()
    {
        var result = _detector.Detect(new byte[] { 0xFF, 0x81, 0x40 });

        Assert.False(result.IsConfident);
        Assert.Contains(TextEncodingChoice.Gbk, result.AvailableChoices);
        Assert.Contains(TextEncodingChoice.Gb18030, result.AvailableChoices);
    }

    [Fact]
    public void Decode_decodes_selected_gb18030_content()
    {
        var encoding = Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var bytes = encoding.GetBytes("阅读位置");

        var value = _detector.Decode(bytes, TextEncodingChoice.Gb18030);

        Assert.Equal("阅读位置", value);
    }
}
