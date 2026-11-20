using System.Security;

namespace LightCapture.App.Services;

/// <summary>验证本次录制缓存可读写；默认位置失败时使用临时目录，只清理本会话文件夹。</summary>
internal sealed class RecordingCache : IDisposable
{
    internal string Folder { get; }
    private RecordingCache(string folder) => Folder = folder;

    internal static RecordingCache Create(string primaryRoot, string? fallbackRoot = null, Action<string>? probe = null)
    {
        probe ??= VerifyAccess;
        var primary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(primaryRoot));
        var first = TryCreate(primary, probe, out var firstError);
        if (first != null) return first;

        var fallback = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fallbackRoot ?? Path.Combine(Path.GetTempPath(), "LightCapture", "recordings")));
        var details = $"默认目录：{primary}\n原因：{Describe(firstError!)}";
        if (!string.Equals(primary, fallback, StringComparison.OrdinalIgnoreCase))
        {
            var second = TryCreate(fallback, probe, out var secondError);
            if (second != null) return second;
            details += $"\n备用目录：{fallback}\n原因：{Describe(secondError!)}";
        }
        throw new IOException($"无法开始 GIF 录制：缓存目录不可用。\n{details}\n请检查目录写入权限、磁盘空间或安全软件的拦截记录后重试。", firstError);
    }

    private static RecordingCache? TryCreate(string root, Action<string> probe, out Exception? error)
    {
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        var owned = false;
        try
        {
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("会话路径已存在。");
            Directory.CreateDirectory(folder);
            owned = true;
            probe(folder);
            error = null;
            return new RecordingCache(folder);
        }
        catch (Exception e)
        {
            if (owned) new RecordingCache(folder).Dispose();
            if (!IsStorageError(e)) throw;
            error = e;
            return null;
        }
    }

    private static void VerifyAccess(string folder)
    {
        var path = Path.Combine(folder, ".write-test");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.WriteByte(0x5A);
            file.Flush(flushToDisk: true);
        }
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != 1 || bytes[0] != 0x5A) throw new IOException("缓存读写验证失败。");
        File.Delete(path);
    }

    internal static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException;
    private static string Describe(Exception error) => error is UnauthorizedAccessException or SecurityException
        ? "访问被拒绝。" : "无法创建或读写缓存，请检查磁盘空间及路径是否被占用。";

    public void Dispose()
    {
        // Folder 仅由本类生成；保留其他录制会话，不递归清理缓存根目录。
        if (!Guid.TryParseExact(Path.GetFileName(Folder), "N", out _)) return;
        try { if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true); }
        catch (Exception e) when (IsStorageError(e)) { }
    }
}
