using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LightCapture.App.Services;

internal static class OfflineTranslator
{
    internal static string PackageRoot => Path.Combine(AppContext.BaseDirectory, "offline");
    internal static bool Available => File.Exists(Path.Combine(PackageRoot, "python", "python.exe")) && File.Exists(Path.Combine(PackageRoot, "models", "en-zh", "model", "model.bin"));
    internal static async Task<string> TranslateAsync(string text, CancellationToken cancellation, Action<int>? onStarted = null)
        => (await TranslateBatchAsync([text], cancellation, onStarted).ConfigureAwait(false))[0];

    internal static async Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellation, Action<int>? onStarted = null)
    {
        if (texts.Count == 0 || texts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("没有可翻译的英文。");
        if (texts.Count > 512 || texts.Sum(text => (long)text.Length) > 12000) throw new ArgumentException("这张截图的文字过多，请缩小选区后翻译。");
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
        var started = false;
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started) throw new InvalidOperationException("无法启动离线翻译引擎。");
            // 关闭窗口时立即终止子进程，不能依赖即将关闭的 UI Dispatcher 执行 finally。
            using var termination = timeout.Token.Register(() => Terminate(process));
            onStarted?.Invoke(process.Id);
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { texts }).AsMemory(), timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidOperationException("离线翻译引擎运行失败，请确认软件包完整且已安装 Microsoft Visual C++ 2015–2022 x64 运行库。");
            using var response = JsonDocument.Parse(output);
            if (!response.RootElement.TryGetProperty("translations", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != texts.Count)
                throw new InvalidOperationException("译文与截图文字数量不匹配，请重试。");
            var translated = items.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : null).ToArray();
            if (translated.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("离线模型没有返回完整译文，请缩小范围后重试。");
            return translated.Select(text => text!).ToArray();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new TimeoutException("离线翻译超过 3 分钟，请减少文字后重试。"); }
        finally { if (started) Terminate(process); }
    }
    private static void Terminate(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
