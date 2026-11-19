using System;

namespace KavrosAfkWarden
{
    public sealed class MotionDetector
    {
        private int[] sample;
        private int sampleWidth;
        private int sampleHeight;
        private bool haveDot;
        private int dotX;
        private int dotY;

        public static IntRect WorldRect(int width, int height)
        {
            return new IntRect((int)(width * 0.18), (int)(height * 0.12), Math.Max(1, (int)(width * 0.56)), Math.Max(1, (int)(height * 0.62)));
        }

        public static IntRect MinimapRect(int width, int height)
        {
            return new IntRect((int)(width * 0.76), (int)(height * 0.02), Math.Max(1, (int)(width * 0.22)), Math.Max(1, (int)(height * 0.26)));
        }

        public bool Moved(ImageBuffer image)
        {
            bool world = WorldChanged(image);
            bool dot = DotMoved(image);
            return world || dot;
        }

        private bool WorldChanged(ImageBuffer image)
        {
            IntRect world = WorldRect(image.Width, image.Height);
            int columns = Math.Max(1, world.Width / 4);
            int rows = Math.Max(1, world.Height / 4);
            int[] next = new int[columns * rows];
            int n = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    next[n++] = image.Argb(world.X + x * 4, world.Y + y * 4);
            bool moved = false;
            if (sample != null && sample.Length == next.Length && sampleWidth == image.Width && sampleHeight == image.Height)
            {
                int changed = 0;
                for (int i = 0; i < next.Length; i++)
                {
                    int a = sample[i], b = next[i];
                    if (Math.Abs(ImageBuffer.R(a) - ImageBuffer.R(b)) > 28 || Math.Abs(ImageBuffer.G(a) - ImageBuffer.G(b)) > 28 || Math.Abs(ImageBuffer.B(a) - ImageBuffer.B(b)) > 28)
                        changed++;
                }
                moved = changed >= 12 && changed > next.Length * 0.018;
            }
            sample = next;
            sampleWidth = image.Width;
            sampleHeight = image.Height;
            return moved;
        }

        private bool DotMoved(ImageBuffer image)
        {
            IntRect map = MinimapRect(image.Width, image.Height);
            long sx = 0, sy = 0;
            int count = 0;
            for (int y = map.Y; y < map.Bottom; y += 2)
                for (int x = map.X; x < map.Right; x += 2)
                {
                    int c = image.Argb(x, y);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    if (r > 210 && g > 200 && b > 120 && r - b < 90)
                    {
                        sx += x;
                        sy += y;
                        count++;
                    }
                }
            if (count < 4)
            {
                haveDot = false;
                return false;
            }
            int cx = (int)(sx / count);
            int cy = (int)(sy / count);
            bool moved = haveDot && (cx - dotX) * (cx - dotX) + (cy - dotY) * (cy - dotY) > 9;
            haveDot = true;
            dotX = cx;
            dotY = cy;
            return moved;
        }
    }
}
