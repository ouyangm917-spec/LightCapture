namespace LightCapture.Core.Translation;

public static class OcrTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // OCR 行尾连字符可能属于原词；保留原文，避免清洗阶段破坏代码和复合词。
        return string.Join('\n', text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\f", "")
            .Split('\n').Select(line => line.Trim())).Trim();
    }
}
