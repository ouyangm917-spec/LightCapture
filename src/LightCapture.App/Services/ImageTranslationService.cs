using System.Windows.Media.Imaging;
using LightCapture.App.UI;

namespace LightCapture.App.Services;

internal static class ImageTranslationService
{
    internal static async Task<BitmapSource> TranslateAsync(BitmapSource original, CancellationToken cancellation, IProgress<string>? progress = null)
    {
        progress?.Report("正在识别截图中的英文…");
        var regions = await OcrService.RecognizeRegionsAsync(BitmapIO.Png(original), cancellation);
        progress?.Report("正在离线翻译…");
        var translations = await OfflineTranslator.TranslateBatchAsync(regions.Select(region => region.Text).ToArray(), cancellation);
        cancellation.ThrowIfCancellationRequested();
        // 所有译文就绪后一次替换底图，失败或取消不会留下半张译图。
        return ImageTranslationRenderer.Render(original, regions, translations);
    }
}
