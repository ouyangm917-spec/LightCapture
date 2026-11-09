using LightCapture.Core.Translation;

namespace LightCapture.Core.Tests;

public class OcrTextNormalizerTests
{
    [Fact]
    public void CleaningPreservesParagraphsHyphensAndIdentifiers() =>
        Assert.Equal("state-\nof-the-art\n\nfoo_bar()", OcrTextNormalizer.Normalize("  state-\r\nof-the-art \r\n\r\nfoo_bar()\f"));
}
