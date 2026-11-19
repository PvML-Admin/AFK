using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace KavrosAfkWarden
{
    // Enough of PNG for the self-test fixtures: 8-bit RGB or RGBA, no interlacing.
    internal static class PngImage
    {
        public static ImageBuffer Load(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < 8 || file[0] != 137 || file[1] != 80) throw new InvalidDataException("Not a PNG.");
            int width = 0, height = 0, color = 0, depth = 0;
            List<byte> idat = new List<byte>();
            int pos = 8;
            while (pos + 12 <= file.Length)
            {
                int length = Int(file, pos);
                string type = "" + (char)file[pos + 4] + (char)file[pos + 5] + (char)file[pos + 6] + (char)file[pos + 7];
                int data = pos + 8;
                if (type == "IHDR")
                {
                    width = Int(file, data);
                    height = Int(file, data + 4);
                    depth = file[data + 8];
                    color = file[data + 9];
                    if (file[data + 12] != 0) throw new InvalidDataException("Interlaced PNG is not supported.");
                }
                else if (type == "IDAT")
                {
                    for (int i = 0; i < length; i++) idat.Add(file[data + i]);
                }
                else if (type == "IEND") break;
                pos = data + length + 4;
            }
            if (depth != 8 || (color != 2 && color != 6) || width < 1 || height < 1) throw new InvalidDataException("Unsupported PNG.");
            int channels = color == 6 ? 4 : 3;
            byte[] zlib = idat.ToArray();
            if (zlib.Length < 8) throw new InvalidDataException("Empty PNG.");
            byte[] raw;
            using (MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6))
            using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                deflate.CopyTo(output);
                raw = output.ToArray();
            }
            int stride = width * channels;
            int[] pixels = new int[width * height];
            byte[] prior = new byte[stride];
            int offset = 0;
            for (int y = 0; y < height; y++)
            {
                if (offset >= raw.Length) break;
                int filter = raw[offset++];
                byte[] row = new byte[stride];
                for (int i = 0; i < stride; i++)
                {
                    if (offset >= raw.Length) break;
                    int value = raw[offset++] & 255;
                    int left = i >= channels ? row[i - channels] & 255 : 0;
                    int up = prior[i] & 255;
                    int ul = i >= channels ? prior[i - channels] & 255 : 0;
                    if (filter == 1) value += left;
                    else if (filter == 2) value += up;
                    else if (filter == 3) value += (left + up) / 2;
                    else if (filter == 4)
                    {
                        int p = left + up - ul;
                        int pa = Math.Abs(p - left), pb = Math.Abs(p - up), pc = Math.Abs(p - ul);
                        value += pa <= pb && pa <= pc ? left : pb <= pc ? up : ul;
                    }
                    row[i] = (byte)value;
                }
                for (int x = 0; x < width; x++)
                {
                    int r = row[x * channels] & 255;
                    int g = row[x * channels + 1] & 255;
                    int b = row[x * channels + 2] & 255;
                    pixels[y * width + x] = ImageBuffer.Pack(r, g, b);
                }
                prior = row;
            }
            return new ImageBuffer(width, height, pixels);
        }

        private static int Int(byte[] file, int index)
        {
            return (file[index] << 24) | (file[index + 1] << 16) | (file[index + 2] << 8) | file[index + 3];
        }
    }
}
