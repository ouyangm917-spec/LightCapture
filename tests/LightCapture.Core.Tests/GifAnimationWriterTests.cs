using System.Text;
using LightCapture.Core.Imaging;

namespace LightCapture.Core.Tests;

public class GifAnimationWriterTests
{
    public static byte[] SinglePixelGif(byte r = 255, byte g = 0, byte b = 0) =>
    [71,73,70,56,57,97, 1,0,1,0,128,0,0, r,g,b,0,0,0, 44,0,0,0,0,1,0,1,0,0, 2,2,68,1,0,59];

    [Fact]
    public void LoopAndPerFrameControlExtensionsAreWritten()
    {
        var frame = GifFrame.Read(SinglePixelGif());
        using var stream = new MemoryStream();
        using (var writer = new GifAnimationWriter(stream, 1, 1, true))
        {
            writer.WriteFrame(frame, 7);
            writer.WriteFrame(frame, 12);
            Assert.Equal(2, writer.FrameCount);
        }
        var data = stream.ToArray();
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(data, 0, 6));
        var app = data.AsSpan().IndexOf(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        Assert.Equal(new byte[] {3,1,0,0,0}, data[(app + 11)..(app + 16)]);
        Assert.True(data.AsSpan().IndexOf(new byte[] {0x21,0xF9,4,4,7,0,0,0}) >= 0);
        Assert.True(data.AsSpan().IndexOf(new byte[] {0x21,0xF9,4,4,12,0,0,0}) >= 0);
        Assert.Equal(0x3B, data[^1]);
    }

    [Fact]
    public void MismatchedFrameSizeIsRejected()
    {
        using var stream = new MemoryStream();
        using var writer = new GifAnimationWriter(stream, 2, 1, true);
        Assert.Throws<ArgumentException>(() => writer.WriteFrame(GifFrame.Read(SinglePixelGif()), 10));
    }

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(12)] [InlineData(25)] [InlineData(31)]
    public void TruncatedInputIsRejected(int size) => Assert.Throws<InvalidDataException>(() => GifFrame.Read(SinglePixelGif()[..size]));
}
