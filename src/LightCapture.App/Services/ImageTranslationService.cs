using System.Windows.Media.Imaging;
using LightCapture.App.UI;

namespace LightCapture.App.Services;

internal static class ImageTranslationService
{
    internal static async Task<BitmapSource> TranslateAsync(BitmapSource original, CancellationToken cancellation, IProgress<string>? progress = null)
    {
        progress?.Report("正在识别截图中的英文…");
        // PNG 编码和最终绘制会随选区面积增长；冻结底图后交给后台，UI 才能随时响应取消。
        if (!original.IsFrozen) throw new ArgumentException("截图底图必须冻结后再翻译。", nameof(original));
        var png = await Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            var bytes = BitmapIO.Png(original);
            cancellation.ThrowIfCancellationRequested();
            return bytes;
        }, cancellation);
        var regions = await OcrService.RecognizeRegionsAsync(png, cancellation);
        progress?.Report("正在离线翻译…");
        var translations = await OfflineTranslator.TranslateBatchAsync(regions.Select(region => region.Text).ToArray(), cancellation);
        cancellation.ThrowIfCancellationRequested();
        // 所有译文就绪后一次替换底图，失败或取消不会留下半张译图。
        return await Task.Run(() => ImageTranslationRenderer.Render(original, regions, translations, cancellation), cancellation);
    }
}
