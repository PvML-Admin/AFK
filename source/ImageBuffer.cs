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

        public static bool MostlyBlack(ImageBuffer image)
        {
            if (image == null) return true;
            int dark = 0, n = 0;
            for (int y = 0; y < image.Height; y += 8)
                for (int x = 0; x < image.Width; x += 8)
                {
                    int c = image.Argb(x, y);
                    n++;
                    if (R(c) + G(c) + B(c) < 30) dark++;
                }
            return n > 0 && dark * 100 / n >= 92;
        }

        public static int R(int c) { return (c >> 16) & 255; }
        public static int G(int c) { return (c >> 8) & 255; }
        public static int B(int c) { return c & 255; }

        public ImageBuffer Scale(double factor)
        {
            if (factor <= 0) factor = 1;
            int w = Math.Max(1, (int)Math.Round(Width * factor));
            int h = Math.Max(1, (int)Math.Round(Height * factor));
            int[] pixels = new int[w * h];
            bool shrink = w < Width || h < Height;
            // Below 1.4x, keeping the brightest source pixel preserves a one-pixel
            // stroke. At 1.5x that same rule fattens the stroke until digits merge,
            // so those enlargements take the single nearest pixel instead.
            bool nearest = !shrink && (double)w / Width >= 1.4;
            for (int y = 0; y < h; y++)
            {
                if (nearest)
                {
                    int sy = Math.Min(Height - 1, (int)((y + 0.5) * Height / h));
                    for (int x = 0; x < w; x++)
                    {
                        int sx = Math.Min(Width - 1, (int)((x + 0.5) * Width / w));
                        pixels[y * w + x] = Pixels[sy * Width + sx];
                    }
                    continue;
                }
                int y0 = (int)Math.Floor(y * (double)Height / h);
                int y1 = (int)Math.Ceiling((y + 1) * (double)Height / h) - 1;
                if (y0 < 0) y0 = 0;
                if (y1 < y0) y1 = y0;
                if (y1 >= Height) y1 = Height - 1;
                for (int x = 0; x < w; x++)
                {
                    int x0 = (int)Math.Floor(x * (double)Width / w);
                    int x1 = (int)Math.Ceiling((x + 1) * (double)Width / w) - 1;
                    if (x0 < 0) x0 = 0;
                    if (x1 < x0) x1 = x0;
                    if (x1 >= Width) x1 = Width - 1;
                    int best = Pixels[y0 * Width + x0];
                    int lum = R(best) + G(best) + B(best);
                    for (int yy = y0; yy <= y1; yy++)
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            int color = Pixels[yy * Width + xx];
                            int sum = R(color) + G(color) + B(color);
                            if (sum > lum) { lum = sum; best = color; }
                        }
                    pixels[y * w + x] = best;
                }
            }
            return new ImageBuffer(w, h, pixels);
        }

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
