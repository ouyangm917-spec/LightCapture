using Tesseract;
using LightCapture.Core.Translation;

namespace LightCapture.App.Services;

internal static class OcrService
{
    private static readonly SemaphoreSlim Gate = new(1);
    internal static async Task<string> RecognizeAsync(byte[] png, CancellationToken cancellation)
    {
        await Gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                var path = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (!File.Exists(Path.Combine(path, "eng.traineddata"))) throw new FileNotFoundException("缺少英文识别模型，请重新解压完整的软件包。");
                using var engine = new TesseractEngine(path, "eng", EngineMode.Default);
                using var image = Pix.LoadFromMemory(png);
                using var page = engine.Process(image, PageSegMode.Auto);
                var text = OcrTextNormalizer.Normalize(page.GetText());
                cancellation.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("没有识别到英文，请选择更清晰的文字区域。");
                return text;
            }, cancellation);
        }
        finally { Gate.Release(); }
    }
}
