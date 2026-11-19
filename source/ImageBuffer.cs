using System;

namespace KavrosAfkWarden
{
    public sealed class ImageBuffer
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int[] Pixels;

        public ImageBuffer(int width, int height, int[] pixels)
        {
            if (width < 1 || height < 1) throw new ArgumentException("Image is empty.");
            if (pixels == null || pixels.Length != width * height) throw new ArgumentException("Pixel buffer does not match the image size.");
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public static ImageBuffer Blank(int width, int height, int argb)
        {
            int[] pixels = new int[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = argb;
            return new ImageBuffer(width, height, pixels);
        }

        public bool Contains(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public int Argb(int x, int y)
        {
            if (!Contains(x, y)) return 0;
            return Pixels[y * Width + x];
        }

        public void Set(int x, int y, int argb)
        {
            if (Contains(x, y)) Pixels[y * Width + x] = argb;
        }

        public void Fill(int x, int y, int w, int h, int argb)
        {
            int x1 = x + w;
            int y1 = y + h;
            for (int yy = y; yy < y1; yy++)
                for (int xx = x; xx < x1; xx++)
                    Set(xx, yy, argb);
        }

        public static int Pack(int r, int g, int b)
        {
            return (255 << 24) | ((r & 255) << 16) | ((g & 255) << 8) | (b & 255);
        }

        public static int R(int c) { return (c >> 16) & 255; }
        public static int G(int c) { return (c >> 8) & 255; }
        public static int B(int c) { return c & 255; }

        public ImageBuffer Crop(int x, int y, int w, int h)
        {
            if (w < 1 || h < 1) return null;
            ImageBuffer image = Blank(w, h, 0);
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    image.Set(xx, yy, Argb(x + xx, y + yy));
            return image;
        }
    }

    public struct IntRect
    {
        public int X, Y, Width, Height;
        public IntRect(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; }
        public int Right { get { return X + Width; } }
        public int Bottom { get { return Y + Height; } }
        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }
        public bool Contains(int x, int y) { return x >= X && y >= Y && x < Right && y < Bottom; }
    }
}
