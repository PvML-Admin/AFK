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

        public bool MovedCrop(ImageBuffer crop)
        {
            if (crop == null) return false;
            int columns = Math.Max(1, crop.Width / 4);
            int rows = Math.Max(1, crop.Height / 4);
            int[] next = new int[columns * rows];
            int n = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    next[n++] = crop.Argb(x * 4, y * 4);
            bool moved = false;
            if (sample != null && sample.Length == next.Length)
            {
                int changed = 0;
                for (int i = 0; i < next.Length; i++)
                {
                    int a = sample[i], b = next[i];
                    if (Math.Abs(ImageBuffer.R(a) - ImageBuffer.R(b)) > 28 || Math.Abs(ImageBuffer.G(a) - ImageBuffer.G(b)) > 28 || Math.Abs(ImageBuffer.B(a) - ImageBuffer.B(b)) > 28)
                        changed++;
                }
                moved = changed >= 4 && changed * 100 > next.Length;
            }
            sample = next;
            sampleWidth = crop.Width;
            sampleHeight = crop.Height;
            return moved;
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

    // Combat and skilling count as activity even when the player is not touching
    // the mouse. Signals are colours and pixel changes, not a fixed layout.
    public sealed class SceneActivity
    {
        private readonly MotionDetector motion = new MotionDetector();
        private int[] cells;
        private int cellW;
        private int cellH;
        private bool haveCells;
        private int lastHp = -1;
        private int lastPrayer = -1;
        private int lastAdrenaline = -1;
        private bool haveHud;

        public bool Tick(ImageBuffer image, HudReading reading)
        {
            bool busy = false;
            if (reading != null && HudChanged(reading)) busy = true;
            if (image != null)
            {
                if (motion.Moved(image)) busy = true;
                if (HasTargetBar(image, reading)) busy = true;
                if (PlayfieldChanged(image, reading)) busy = true;
            }
            return busy;
        }

        private bool HudChanged(HudReading reading)
        {
            int hp = reading.Hp == null ? -1 : reading.Hp.Value;
            int prayer = reading.Prayer == null ? -1 : reading.Prayer.Value;
            int adrenaline = reading.Adrenaline == null ? -1 : reading.Adrenaline.Value;
            bool changed = false;
            if (haveHud)
            {
                if (hp >= 0 && lastHp >= 0 && hp != lastHp) changed = true;
                if (prayer >= 0 && lastPrayer >= 0 && prayer != lastPrayer) changed = true;
                if (adrenaline >= 0 && lastAdrenaline >= 0 && adrenaline != lastAdrenaline) changed = true;
            }
            if (hp >= 0) lastHp = hp;
            if (prayer >= 0) lastPrayer = prayer;
            if (adrenaline >= 0) lastAdrenaline = adrenaline;
            haveHud = true;
            return changed;
        }

        // A short bright green bar that is not the summoning bar in the HUD row.
        // That is the overhead target (or player) health bar.
        private static bool HasTargetBar(ImageBuffer image, HudReading reading)
        {
            if (image == null) return false;
            int step = image.Width > 1400 ? 2 : 1;
            for (int y = 0; y < image.Height; y += step)
            {
                if (reading != null && reading.StripFound && Math.Abs(y - reading.StripY) < 28) continue;
                int x = 0;
                while (x < image.Width)
                {
                    if (!Lime(image.Argb(x, y))) { x += step; continue; }
                    int start = x;
                    while (x < image.Width && Lime(image.Argb(x, y))) x++;
                    int run = x - start;
                    if (run < 16 || run > 120) continue;
                    int height = 1;
                    int below = y + step;
                    while (below < image.Height && below < y + 12)
                    {
                        int overlap = 0;
                        int samples = 0;
                        for (int xx = start; xx < x; xx += step) { samples++; if (Lime(image.Argb(xx, below))) overlap++; }
                        if (samples == 0 || overlap * 2 < samples) break;
                        height += step;
                        below += step;
                    }
                    if (height >= 2 && height <= 10) return true;
                }
            }
            return false;
        }

        private static bool Lime(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 160 && b <= 80 && g > r + 30 && g > b + 40;
        }

        // Coarse samples outside the HUD strip and the buff icons. XP drops,
        // inventory changes and the player animation flip these cells.
        private bool PlayfieldChanged(ImageBuffer image, HudReading reading)
        {
            int columns = Math.Max(1, image.Width / 48);
            int rows = Math.Max(1, image.Height / 48);
            int[] next = new int[columns * rows];
            int n = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int sx = Math.Min(image.Width - 1, x * 48 + 24);
                    int sy = Math.Min(image.Height - 1, y * 48 + 24);
                    if (SkipSample(reading, sx, sy)) { next[n++] = -1; continue; }
                    next[n++] = image.Argb(sx, sy);
                }
            bool changed = false;
            if (haveCells && cells != null && cells.Length == next.Length && cellW == image.Width && cellH == image.Height)
            {
                int flips = 0;
                for (int i = 0; i < next.Length; i++)
                {
                    if (next[i] < 0 || cells[i] < 0) continue;
                    int a = cells[i], b = next[i];
                    if (Math.Abs(ImageBuffer.R(a) - ImageBuffer.R(b)) > 36 || Math.Abs(ImageBuffer.G(a) - ImageBuffer.G(b)) > 36 || Math.Abs(ImageBuffer.B(a) - ImageBuffer.B(b)) > 36)
                        flips++;
                }
                changed = flips >= 2;
            }
            cells = next;
            cellW = image.Width;
            cellH = image.Height;
            haveCells = true;
            return changed;
        }

        private static bool SkipSample(HudReading reading, int x, int y)
        {
            if (reading == null) return false;
            if (reading.StripFound && Math.Abs(y - reading.StripY) < 36) return true;
            if (reading.BuffRects == null) return false;
            for (int i = 0; i < reading.BuffRects.Count; i++)
            {
                IntRect box = reading.BuffRects[i];
                if (x >= box.X && x < box.Right && y >= box.Y && y < box.Bottom) return true;
            }
            return false;
        }
    }
}
