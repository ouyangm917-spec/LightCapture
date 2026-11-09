using System.Text;

namespace LightCapture.Core.Imaging;

/// <summary>读取系统 GIF 编码器生成的单帧，复用其调色板与 LZW 数据，不重复实现压缩算法。</summary>
public sealed record GifFrame(ushort Width, ushort Height, byte[] Palette, bool Interlaced, byte[] ImageData)
{
    public static GifFrame Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        byte[] ReadExact(int size)
        {
            var result = reader.ReadBytes(size);
            if (result.Length != size) throw new InvalidDataException("GIF 数据被截断。");
            return result;
        }
        try
        {
            var signature = Encoding.ASCII.GetString(ReadExact(6));
            if (signature is not ("GIF87a" or "GIF89a")) throw new InvalidDataException("不是 GIF 数据。");
            var canvasWidth = reader.ReadUInt16();
            var canvasHeight = reader.ReadUInt16();
            var flags = reader.ReadByte();
            ReadExact(2);
            var globalPalette = (flags & 0x80) != 0 ? ReadExact(3 * (1 << ((flags & 7) + 1))) : [];
            while (stream.Position < stream.Length)
            {
                switch (reader.ReadByte())
                {
                    case 0x21:
                        reader.ReadByte();
                        for (var size = reader.ReadByte(); size != 0; size = reader.ReadByte()) ReadExact(size);
                        break;
                    case 0x2C:
                        var left = reader.ReadUInt16();
                        var top = reader.ReadUInt16();
                        var width = reader.ReadUInt16();
                        var height = reader.ReadUInt16();
                        var packed = reader.ReadByte();
                        if (left != 0 || top != 0 || width != canvasWidth || height != canvasHeight || width == 0 || height == 0)
                            throw new InvalidDataException("仅支持完整画布的单帧 GIF。");
                        var palette = (packed & 0x80) != 0 ? ReadExact(3 * (1 << ((packed & 7) + 1))) : globalPalette;
                        if (palette.Length == 0) throw new InvalidDataException("GIF 缺少调色板。");
                        var imageStart = checked((int)stream.Position);
                        var minCodeSize = reader.ReadByte();
                        if (minCodeSize is < 2 or > 8) throw new InvalidDataException("GIF LZW 码宽无效。");
                        for (var size = reader.ReadByte(); size != 0; size = reader.ReadByte()) ReadExact(size);
                        return new(width, height, palette, (packed & 0x40) != 0, data[imageStart..(int)stream.Position]);
                    case 0x3B:
                        throw new InvalidDataException("GIF 没有图像帧。");
                    default:
                        throw new InvalidDataException("GIF 块类型无效。");
                }
            }
            throw new InvalidDataException("GIF 没有图像帧。");
        }
        catch (EndOfStreamException exception) { throw new InvalidDataException("GIF 数据被截断。", exception); }
    }
}
