using System.Text;

namespace LightCapture.Core.Imaging;

/// <summary>按帧写出 GIF89a 容器、时长和循环扩展；每帧使用完整画布与局部调色板。</summary>
public sealed class GifAnimationWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly ushort _width;
    private readonly ushort _height;
    private bool _disposed;
    public int FrameCount { get; private set; }

    public GifAnimationWriter(Stream output, int width, int height, bool leaveOpen = false)
    {
        if (width is <= 0 or > ushort.MaxValue || height is <= 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
        _width = (ushort)width;
        _height = (ushort)height;
        _writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen);
        _writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
        _writer.Write(_width);
        _writer.Write(_height);
        _writer.Write(new byte[] { 0x70, 0, 0 });
        _writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
        _writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        _writer.Write(new byte[] { 3, 1, 0, 0, 0 });
    }

    public void WriteFrame(GifFrame frame, ushort delayCentiseconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (frame.Width != _width || frame.Height != _height) throw new ArgumentException("所有帧必须使用相同尺寸。", nameof(frame));
        if (delayCentiseconds == 0) throw new ArgumentOutOfRangeException(nameof(delayCentiseconds));
        var colors = frame.Palette.Length / 3;
        if (frame.Palette.Length % 3 != 0 || colors is < 2 or > 256 || (colors & (colors - 1)) != 0)
            throw new ArgumentException("调色板大小无效。", nameof(frame));
        if (frame.ImageData.Length < 3 || frame.ImageData[^1] != 0) throw new ArgumentException("图像数据无效。", nameof(frame));
        _writer.Write(new byte[] { 0x21, 0xF9, 4, 4 });
        _writer.Write(delayCentiseconds);
        _writer.Write(new byte[] { 0, 0 });
        _writer.Write((byte)0x2C);
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write(_width);
        _writer.Write(_height);
        var tableSize = (int)Math.Log2(colors) - 1;
        _writer.Write((byte)(0x80 | (frame.Interlaced ? 0x40 : 0) | tableSize));
        _writer.Write(frame.Palette);
        _writer.Write(frame.ImageData);
        FrameCount++;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Write((byte)0x3B);
        _writer.Dispose();
    }
}
