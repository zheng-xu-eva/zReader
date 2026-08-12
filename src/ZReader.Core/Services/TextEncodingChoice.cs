namespace ZReader.Core.Services;

/// <summary>
/// Lists text encodings that can be selected during TXT import.
/// </summary>
public enum TextEncodingChoice
{
    Utf8 = 0,
    Utf8Bom = 1,
    Gbk = 2,
    Gb18030 = 3
}
