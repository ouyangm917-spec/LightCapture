using System.Windows.Media.Imaging;
using LightCapture.Core.Imaging;
using LightCapture.Core.Recording;

namespace LightCapture.App.Services;

internal static class GifExportService
{
    internal static GifFrame EncodeFrame(BitmapSource bitmap)
    {
        var encoder = new GifBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream);
        return GifFrame.Read(stream.ToArray());
    }
    internal static Task ExportAsync(RecordingSession recording, string destination, double start, double end, int width, double speed, IProgress<int> progress, CancellationToken cancellation)
        => Task.Run(() =>
        {
            var slices = RecordingTimeline.Trim(recording.Frames, recording.DurationMilliseconds, start, end);
            var delays = RecordingTimeline.ToGifDelays(slices, speed);
            width = Math.Clamp(width, 1, recording.Width);
            var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, $".LightCapture-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    GifAnimationWriter? writer = null;
                    try
                    {
                        for (var i = 0; i < slices.Count; i++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            var bitmap = BitmapIO.Load(recording.Frames[slices[i].FrameIndex].Path, width);
                            writer ??= new GifAnimationWriter(file, bitmap.PixelWidth, bitmap.PixelHeight, leaveOpen: true);
                            writer.WriteFrame(EncodeFrame(bitmap), delays[i]);
                            progress.Report((i + 1) * 100 / slices.Count);
                        }
                    }
                    finally { writer?.Dispose(); }
                }
                cancellation.ThrowIfCancellationRequested();
                File.Move(temporary, destination, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }, cancellation);
}
