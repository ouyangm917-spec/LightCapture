using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LightCapture.App.Services;

internal static class OfflineTranslator
{
    internal static string PackageRoot => Path.Combine(AppContext.BaseDirectory, "offline");
    internal static bool Available => File.Exists(Path.Combine(PackageRoot, "python", "python.exe")) && File.Exists(Path.Combine(PackageRoot, "models", "en-zh", "model", "model.bin"));
    internal static async Task<string> TranslateAsync(string text, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("请先输入英文。");
        if (text.Length > 12000) throw new ArgumentException("一次最多翻译 12,000 个字符，请缩小选区或分段翻译。");
        if (!Available) throw new FileNotFoundException("缺少离线翻译包，请使用带有 offline 文件夹的完整版本。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(PackageRoot, "python", "python.exe"),
            WorkingDirectory = PackageRoot,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-B"); start.ArgumentList.Add("-u"); start.ArgumentList.Add(Path.Combine(PackageRoot, "translate.py"));
        start.ArgumentList.Add(Path.Combine(PackageRoot, "models", "en-zh"));
        start.Environment["OMP_NUM_THREADS"] = Math.Clamp(Environment.ProcessorCount / 2, 1, 4).ToString();
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("无法启动离线翻译引擎。");
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { text }).AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException("离线翻译引擎运行失败，请确认软件包完整且已安装 Microsoft Visual C++ 2015–2022 x64 运行库。");
            using var response = JsonDocument.Parse(output);
            var translated = response.RootElement.GetProperty("translation").GetString();
            return string.IsNullOrWhiteSpace(translated) ? throw new InvalidOperationException("离线模型没有返回译文，请缩小范围后重试。") : translated.Trim();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new TimeoutException("离线翻译超过 3 分钟，请减少文字后重试。"); }
        finally
        {
            try { if (process.Id != 0 && !process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }
}
