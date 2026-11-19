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
        private int[] cells;
        private int cellW;
        private int cellH;
        private bool haveCells;
        private int lastHp = -1;
        private int lastPrayer = -1;
        private int lastAdrenaline = -1;
        private bool haveHud;

        private int lastSplat = -1;
        private int lastXp = -1;
        private int lastTarget = -1;
        internal string Reason = "none";

        public bool Tick(ImageBuffer image, HudReading reading)
        {
            return Tick(image, reading, false);
        }

        // Player signals only. A steady overhead bar is not movement. A change
        // in that bar, a hitsplat, an XP counter, or the HUD numbers is.
        public bool Tick(ImageBuffer image, HudReading reading, bool playerInput)
        {
            return Tick(image, reading, playerInput, false, false);
        }

        public bool Tick(ImageBuffer image, HudReading reading, bool playerInput, bool xpChanged, bool packChanged)
        {
            string reason = "";
            if (playerInput) reason = Join(reason, "key");
            string hud = HudChange(reading);
            if (hud.Length > 0) reason = Join(reason, hud);
            if (xpChanged) reason = Join(reason, "xp");
            if (packChanged) reason = Join(reason, "pack");
            if (image != null)
            {
                if (TargetChanged(image, reading)) reason = Join(reason, "target-bar");
                if (HitsplatChanged(image)) reason = Join(reason, "hitsplat");
            }
            Reason = reason.Length == 0 ? "none" : reason;
            return reason.Length > 0;
        }

        private static string Join(string left, string right)
        {
            if (left.Length == 0) return right;
            return left + "+" + right;
        }

        private string HudChange(HudReading reading)
        {
            if (reading == null) return "";
            int hp = reading.Hp == null ? -1 : reading.Hp.Value;
            int prayer = reading.Prayer == null ? -1 : reading.Prayer.Value;
            int adrenaline = reading.Adrenaline == null ? -1 : reading.Adrenaline.Value;
            string changed = "";
            if (haveHud)
            {
                // Prayer drain and adrenaline decay happen while standing still.
                if (hp >= 0 && lastHp >= 0 && hp != lastHp) changed = Join(changed, "hp");
                if (adrenaline >= 0 && lastAdrenaline >= 0 && adrenaline >= lastAdrenaline + 8) changed = Join(changed, "adrenaline");
            }
            if (hp >= 0) lastHp = hp;
            if (prayer >= 0) lastPrayer = prayer;
            if (adrenaline >= 0) lastAdrenaline = adrenaline;
            haveHud = true;
            return changed;
        }

        // A short bright green bar that is not the summoning bar in the HUD row.
        // That is the overhead target (or player) health bar.
        private bool TargetChanged(ImageBuffer image, HudReading reading)
        {
            if (image == null) return false;
            int longest = 0;
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
                    if (height >= 2 && height <= 10 && run > longest) longest = run;
                }
            }
            bool changed = lastTarget >= 0 && longest >= 16 && Math.Abs(longest - lastTarget) >= 8;
            if (longest >= 16) lastTarget = longest;
            return changed;
        }

        private static bool Lime(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 160 && b <= 80 && g > r + 30 && g > b + 40;
        }

        // A burst of small saturated red pixels in the middle of the view.
        // The life bar is a long run and is excluded by the size cap.
        private bool HitsplatChanged(ImageBuffer image)
        {
            int count = 0;
            int left = image.Width / 4;
            int right = image.Width - left;
            int top = image.Height / 5;
            int bottom = image.Height - image.Height / 5;
            for (int y = top; y < bottom; y += 3)
                for (int x = left; x < right; x += 3)
                {
                    int c = image.Argb(x, y);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    if (r > 220 && g < 70 && b < 70) count++;
                }
            bool changed = lastSplat >= 0 && count >= 8 && count <= 180 && Math.Abs(count - lastSplat) >= 8;
            lastSplat = count;
            return changed;
        }

        // A compact bright counter, not scattered scenery. Foliage is not near-white.
        private bool XpCounterChanged(ImageBuffer image)
        {
            int minX = image.Width, minY = image.Height, maxX = 0, maxY = 0, count = 0;
            for (int y = 0; y < image.Height; y += 3)
                for (int x = 0; x < image.Width; x += 3)
                {
                    int c = image.Argb(x, y);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    if (r < 190 || g < 170 || b > 140 || g + 30 < r) continue;
                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            bool compact = count >= 8 && count <= 400 && maxX - minX < 200 && maxY - minY < 48;
            bool changed = lastXp >= 0 && compact && Math.Abs(count - lastXp) >= 8;
            lastXp = compact ? count : lastXp;
            if (!compact && lastXp < 0) lastXp = 0;
            return changed;
        }

    public static class XpWatch
    {
        public static IntRect Find(ImageBuffer image)
        {
            if (image == null) return new IntRect();
            int left = image.Width * 55 / 100;
            int bottom = Math.Max(1, image.Height * 40 / 100);
            int minX = image.Width, minY = bottom, maxX = 0, maxY = 0, count = 0;
            int step = image.Width > 900 ? 2 : 1;
            for (int y = 0; y < bottom; y += step)
                for (int x = left; x < image.Width; x += step)
                {
                    int color = image.Argb(x, y);
                    int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
                    if (r < 200 || g < 180 || b > 170) continue;
                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            if (count < 12 || maxX <= minX || maxY < minY) return new IntRect();
            if (maxX - minX > 280 || maxY - minY > 110) return new IntRect();
            return new IntRect(Math.Max(0, minX - 4), Math.Max(0, minY - 4), Math.Min(image.Width, maxX + 5) - Math.Max(0, minX - 4), Math.Min(image.Height, maxY + 5) - Math.Max(0, minY - 4));
        }

        public static int Hash(ImageBuffer image, IntRect box)
        {
            if (image == null || box.Width < 4 || box.Height < 4) return 0;
            int hash = 17;
            int step = Math.Max(1, Math.Min(box.Width, box.Height) / 12);
            for (int y = box.Y; y < box.Bottom && y < image.Height; y += step)
                for (int x = box.X; x < box.Right && x < image.Width; x += step)
                    hash = unchecked(hash * 31 + image.Argb(x, y));
            return hash;
        }
    }

    public static class PackWatch
    {
        public static IntRect Find(ImageBuffer image)
        {
            if (image == null) return new IntRect();
            int best = 0;
            IntRect found = new IntRect();
            int step = image.Width > 900 ? 20 : 4;
            for (int cell = 24; cell <= 42; cell += 6)
            {
                int gridW = cell * 4;
                int gridH = cell * 7;
                if (gridW >= image.Width || gridH >= image.Height) continue;
                for (int y = 0; y + gridH < image.Height; y += step)
                    for (int x = 0; x + gridW < image.Width; x += step)
                    {
                        int score = Score(image, x, y, cell);
                        if (score > best) { best = score; found = new IntRect(x, y, gridW, gridH); }
                    }
            }
            if (best < 20) return new IntRect();
            return found;
        }

        public static int Hash(ImageBuffer image, IntRect grid)
        {
            if (image == null || grid.Width < 16 || grid.Height < 16) return 0;
            int cellW = grid.Width / 4;
            int cellH = grid.Height / 7;
            if (cellW < 4 || cellH < 4) return 0;
            int hash = 17;
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                {
                    int x = grid.X + col * cellW + cellW / 2;
                    int y = grid.Y + row * cellH + cellH / 2;
                    hash = unchecked(hash * 31 + image.Argb(x, y));
                }
            return hash;
        }

        private static int Score(ImageBuffer image, int x, int y, int cell)
        {
            int hits = 0;
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                {
                    int cx = x + col * cell + cell / 2;
                    int cy = y + row * cell + cell / 2;
                    int center = image.Argb(cx, cy);
                    int edge = image.Argb(x + col * cell + 1, y + row * cell + 1);
                    int cr = ImageBuffer.R(center) + ImageBuffer.G(center) + ImageBuffer.B(center);
                    int er = ImageBuffer.R(edge) + ImageBuffer.G(edge) + ImageBuffer.B(edge);
                    if (er > cr + 40) hits++;
                }
            return hits;
        }
    }

        // Kept so a future inventory pin can hash one region. Not used for idle:
        // a whole-screen grid treats other players and trees as the user.
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
