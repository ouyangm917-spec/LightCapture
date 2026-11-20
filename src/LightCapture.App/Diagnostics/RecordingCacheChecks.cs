using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Diagnostics;

internal static class RecordingCacheChecks
{
    internal static async Task RunAsync(string output, Func<string, Func<Task<string>>, Task> check)
    {
        await check("录制缓存可写验证、拒绝访问回退与独立清理", () =>
        {
            var root = Path.Combine(output, "cache-access-" + Guid.NewGuid().ToString("N"));
            var primary = Path.Combine(root, "primary");
            var fallback = Path.Combine(root, "fallback");
            using (var cache = RecordingCache.Create(primary, fallback))
            {
                Require(Path.GetDirectoryName(cache.Folder) == primary && !Directory.Exists(fallback), "可写主目录仍使用备用路径");
                Require(Directory.GetFiles(cache.Folder).Length == 0, "可写验证遗留测试文件");
            }
            var probeCount = 0;
            using (var cache = RecordingCache.Create(primary, fallback, folder =>
            {
                probeCount++;
                File.WriteAllText(Path.Combine(folder, "probe"), "synthetic");
                if (Path.GetDirectoryName(folder) == primary) throw new UnauthorizedAccessException("injected denial");
            }))
            {
                Require(probeCount == 2 && Path.GetDirectoryName(cache.Folder) == fallback, "拒绝访问没有切换备用目录");
                Require(Directory.GetDirectories(primary).Length == 0, "失败探测目录未清理");
            }
            var sibling = Path.Combine(primary, "existing-session");
            Directory.CreateDirectory(sibling);
            var keep = Path.Combine(sibling, "keep.txt");
            File.WriteAllText(keep, "keep");
            using (var cache = RecordingCache.Create(primary, fallback)) { }
            Require(File.ReadAllText(keep) == "keep", "误清理其他会话");
            try
            {
                using var cache = RecordingCache.Create(primary, fallback, _ => throw new UnauthorizedAccessException("injected denial"));
                throw new InvalidOperationException("两处拒绝访问仍成功");
            }
            catch (IOException e)
            {
                Require(e.Message.Contains("无法开始 GIF 录制") && e.Message.Contains(primary) && e.Message.Contains(fallback) && e.Message.Contains("访问被拒绝"), "失败提示缺少中文原因或路径");
            }
            Require(Directory.GetDirectories(fallback).Length == 0, "备用目录遗留会话");
            File.Delete(keep); Directory.Delete(sibling); Directory.Delete(primary); Directory.Delete(fallback); Directory.Delete(root);
            return Task.FromResult("真实文件读写验证，注入拒绝访问后回退，两处失败显示中文，关闭仅删除本会话；未修改系统权限。");
        });
        await check("备用录制缓存完整导出、写入失败与取消清理", async () =>
        {
            var root = Path.Combine(output, "cache-pipeline-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var blocked = Path.Combine(root, "blocked");
            File.WriteAllText(blocked, "占位文件阻止创建子目录");
            var fallback = Path.Combine(root, "fallback");
            var count = 0;
            using var stop = new CancellationTokenSource();
            using (var recording = await RecordingSession.RecordAsync(new PixelRect(0, 0, 64, 40), blocked, new Progress<TimeSpan>(), stop.Token, _ =>
            {
                var frame = new System.Drawing.Bitmap(64, 40);
                using var graphics = System.Drawing.Graphics.FromImage(frame);
                graphics.Clear(++count % 2 == 0 ? System.Drawing.Color.Blue : System.Drawing.Color.Red);
                if (count == 3) stop.Cancel();
                return frame;
            }, fallback))
            {
                Require(recording.Frames.Count == 3 && recording.Frames.All(f => f.Path.StartsWith(fallback + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "录制未使用备用目录");
                var path = Path.Combine(output, "fallback-recording.gif");
                await GifExportService.ExportAsync(recording, path, 0, recording.DurationMilliseconds, 64, 1, new Progress<int>(), CancellationToken.None);
                using var file = File.OpenRead(path);
                var decoder = new GifBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Require(decoder.Frames.Count == 3, "备用缓存 GIF 导出错误");
            }
            Require(Directory.GetDirectories(fallback).Length == 0 && File.Exists(blocked), "录制结束清理失败或误删阻塞文件");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var untouched = Path.Combine(root, "cancelled");
            try
            {
                using var recording = await RecordingSession.RecordAsync(new PixelRect(0, 0, 64, 40), untouched, new Progress<TimeSpan>(), cancelled.Token, _ => throw new InvalidOperationException("不应捕获"), fallback);
                throw new InvalidOperationException("预先取消仍开始录制");
            }
            catch (OperationCanceledException) { }
            Require(!Directory.Exists(untouched), "取消仍创建缓存");
            try
            {
                using var recording = await RecordingSession.RecordAsync(new PixelRect(0, 0, 64, 40), blocked, new Progress<TimeSpan>(), CancellationToken.None, _ =>
                {
                    var sessionFolder = Directory.GetDirectories(fallback).Single();
                    Directory.CreateDirectory(Path.Combine(sessionFolder, "00000.png"));
                    return new System.Drawing.Bitmap(64, 40);
                }, fallback);
                throw new InvalidOperationException("帧路径不可写仍录制成功");
            }
            catch (IOException e) { Require(e.Message.Contains("录制中断：无法写入缓存图片"), "录制中写入失败未中文化"); }
            Require(Directory.GetDirectories(fallback).Length == 0, "录制失败未回收缓存");
            File.Delete(blocked); Directory.Delete(fallback); Directory.Delete(root);
            return "主路径由真实文件阻塞时，备用缓存录制 3 张合成帧并导出 GIF；取消不创建缓存，中途写入失败中文提示并清理；未截取桌面。";
        });
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
