using Tesseract;
using LightCapture.Core.Translation;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Services;

internal static class OcrService
{
    private static readonly SemaphoreSlim Gate = new(1);
    internal static async Task<string> RecognizeAsync(byte[] png, CancellationToken cancellation)
        => string.Join("\n", (await RecognizeRegionsAsync(png, cancellation)).Select(region => region.Text));

    internal static async Task<IReadOnlyList<OcrRegion>> RecognizeRegionsAsync(byte[] png, CancellationToken cancellation)
    {
        await Gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                var path = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (!File.Exists(Path.Combine(path, "eng.traineddata"))) throw new FileNotFoundException("缺少英文识别模型，请重新解压完整的软件包。");
                using var engine = new TesseractEngine(path, "eng", EngineMode.Default);
                using var image = Pix.LoadFromMemory(png);
                // 截图通常包含分散的按钮和标签；保留每行坐标才能在原图位置绘制译文。
                using var page = engine.Process(image, PageSegMode.SparseText);
                using var iterator = page.GetIterator();
                var regions = new List<OcrRegion>();
                iterator.Begin();
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    var text = OcrTextNormalizer.Normalize(iterator.GetText(PageIteratorLevel.TextLine) ?? "").Replace('\n', ' ');
                    if (!text.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z')) continue;
                    if (!iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0) continue;
                    regions.Add(new(new(bounds.X1, bounds.Y1, bounds.Width, bounds.Height), text));
                } while (iterator.Next(PageIteratorLevel.TextLine));
                cancellation.ThrowIfCancellationRequested();
                if (regions.Count == 0) throw new InvalidOperationException("没有识别到英文，请选择更清晰的文字区域。");
                return (IReadOnlyList<OcrRegion>)regions;
            }, cancellation).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }
}

internal sealed record OcrRegion(PixelRect Bounds, string Text);
