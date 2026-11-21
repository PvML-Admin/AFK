using System;

namespace KavrosAfkWarden
{
    // Turns a full client frame into the chat crop Windows OCR should read:
    // the stacked coloured lines, tripled, as dark ink on a light page.
    public static class ChatPrep
    {
        public static bool Coloured(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            // Dark red game text is about (135, 2, 3). A luminance cut drops it.
            if (r > 70 && r > g + 30 && r > b + 30) return true;
            if (r > 170 && g > 80 && g < r - 10 && b < 110) return true;
            if (g > 135 && g > r + 18 && g > b + 12) return true;
            if (r > 200 && g > 200 && b > 180) return true;
            return false;
        }

        public static IntRect FindBox(ImageBuffer image)
        {
            if (image == null || image.Width < 80 || image.Height < 80) return new IntRect();
            int yStart = image.Height * 68 / 100;
            int xLimit = image.Width * 45 / 100;
            int top = image.Height;
            int bottom = 0;
            int left = image.Width;
            int right = 0;
            int lines = 0;
            for (int y = yStart; y < image.Height - 1; y++)
            {
                int runLeft = -1;
                int runRight = -1;
                int count = 0;
                for (int x = 2; x < xLimit; x++)
                {
                    if (!Coloured(image.Argb(x, y))) continue;
                    count++;
                    if (runLeft < 0) runLeft = x;
                    runRight = x;
                }
                if (count < 14 || runLeft < 0 || runLeft > 160) continue;
                int width = runRight - runLeft + 1;
                if (width < 50 || width > 480) continue;
                if (count > width * 9 / 10) continue;
                lines++;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
                if (runLeft < left) left = runLeft;
                if (runRight > right) right = runRight;
            }
            if (lines < 3 || right <= left || bottom <= top) return new IntRect();
            int x0 = Math.Max(0, left - 8);
            int y0 = Math.Max(0, top - 6);
            int x1 = Math.Min(image.Width, right + 12);
            int y1 = Math.Min(image.Height, bottom + 8);
            return new IntRect(x0, y0, x1 - x0, y1 - y0);
        }

        public static ImageBuffer ForOcr(ImageBuffer image)
        {
            if (image == null) return null;
            int w = image.Width * 3;
            int h = image.Height * 3;
            if (w < 2 || h < 2) return null;
            int[] pixels = new int[w * h];
            int white = ImageBuffer.Pack(255, 255, 255);
            int black = ImageBuffer.Pack(0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                int sy = y / 3;
                if (sy >= image.Height) sy = image.Height - 1;
                for (int x = 0; x < w; x++)
                {
                    int sx = x / 3;
                    if (sx >= image.Width) sx = image.Width - 1;
                    pixels[y * w + x] = Coloured(image.Argb(sx, sy)) ? black : white;
                }
            }
            return new ImageBuffer(w, h, pixels);
        }
    }
}
