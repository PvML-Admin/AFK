using System;
using System.Collections.Generic;

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

    public static class XpPopup
    {
        public static bool Matches(string text)
        {
            string n = Fold(text);
            if (n.Length < 4 || n[0] != '+') return false;
            int i = 1;
            bool digit = false;
            while (i < n.Length && n[i] >= '0' && n[i] <= '9') { digit = true; i++; }
            if (!digit) return false;
            if (i >= n.Length || n[i] != ' ') return false;
            i++;
            if (i + 1 >= n.Length || n[i] != 'x' || n[i + 1] != 'p') return false;
            i += 2;
            if (i >= n.Length) return true;
            if (n[i] != ' ') return false;
            i++;
            if (i >= n.Length || n[i] != '(') return false;
            i++;
            bool any = false;
            while (i < n.Length && ((n[i] >= '0' && n[i] <= '9') || n[i] == '.')) { any = true; i++; }
            if (!any || i >= n.Length || n[i] != ' ') return false;
            return n.Substring(i).IndexOf("bonus xp") >= 0;
        }

        public static bool WantHint(bool popup, bool fullTotal)
        {
            return !popup && !fullTotal;
        }

        private static string Fold(string text)
        {
            if (text == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            bool space = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ',') continue;
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length == 0 || space) continue;
                    space = true;
                    sb.Append(' ');
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }

    public static class XpWatch
    {
        public static IntRect Find(ImageBuffer image)
        {
            XpSight sight = Read(image);
            return sight.Found ? sight.Rect : new IntRect();
        }

        // A drop is "+N" text, or a compact counter whose digits can be read.
        // The box is the glyphs only. Scenery behind them is not part of the match.
        public static XpSight Read(ImageBuffer image)
        {
            return Read(image, 0);
        }

        // onlyScale is 100, 150 or 200 once that scale has been confirmed, otherwise 0.
        public static XpSight Read(ImageBuffer image, int onlyScale)
        {
            XpSight best = new XpSight();
            if (image == null || image.Width < 12 || image.Height < 12) return best;
            int plusScale;
            int plusAt = GlyphBook.FindPlus(image, onlyScale, out plusScale);
            if (plusAt >= 0)
            {
                int px = plusAt % image.Width;
                int py = plusAt / image.Width;
                XpSight parsed = ReadAroundPlus(image, px, py, plusScale);
                if (parsed.Found && HasDigit(parsed.Text))
                {
                    parsed.Scale = plusScale;
                    parsed.Trace = "xp=" + parsed.Text + "@" + parsed.X.ToString() + "," + parsed.Y.ToString() + " scale=" + plusScale.ToString();
                    return parsed;
                }
                // Digits can be too faint after a rescale. The plus was already
                // confirmed by the xp letters, so it is still the drop.
                best.Found = true;
                best.Plus = true;
                best.Scale = plusScale;
                best.Text = "+";
                best.Rect = new IntRect(px, py, Math.Max(8, 9 * plusScale / 100), Math.Max(8, 9 * plusScale / 100));
                best.Trace = "xp=+@" + px.ToString() + "," + py.ToString() + " scale=" + plusScale.ToString();
                return best;
            }
            List<InkBand> bands = new List<InkBand>();
            int guard = 0;
            int limit = image.Height;
            for (int y = 0; y < limit && guard < 6000; y++)
            {
                int x = 0;
                while (x < image.Width && guard < 6000)
                {
                    if (!Ink(image.Argb(x, y))) { x++; continue; }
                    int x0 = x;
                    while (x < image.Width && Ink(image.Argb(x, y))) x++;
                    if (x - x0 < 2) continue;
                    guard++;
                    InkBand band = null;
                    for (int i = bands.Count - 1; i >= 0 && i >= bands.Count - 24; i--)
                    {
                        if (bands[i].Touches(x0, y, x)) { band = bands[i]; break; }
                    }
                    if (band == null)
                    {
                        band = new InkBand();
                        bands.Add(band);
                    }
                    band.Add(x0, y, x);
                }
            }
            for (int i = 0; i < bands.Count; i++)
            {
                InkBand band = bands[i];
                if (band.Dead) continue;
                XpSight sight = Parse(image, band);
                if (!sight.Found) continue;
                if (!best.Found || (sight.Plus && !best.Plus) || (sight.Plus == best.Plus && sight.Y < best.Y)) best = sight;
            }
            return best;
        }

        // The plus is only the anchor. A drop still needs the orange digits beside it,
        // so a plus-shaped patch of leaves is not an XP drop.
        private static XpSight ReadAroundPlus(ImageBuffer image, int px, int py, int scale)
        {
            int reach = Math.Max(48, 160 * scale / 100);
            int tall = Math.Max(18, 32 * scale / 100);
            int x0 = Math.Max(0, px - 6);
            int y0 = Math.Max(0, py - 6);
            int x1 = Math.Min(image.Width - 1, px + reach);
            int y1 = Math.Min(image.Height - 1, py + tall);
            InkBand band = new InkBand();
            for (int y = y0; y <= y1; y++)
            {
                int x = x0;
                while (x <= x1)
                {
                    if (!Ink(image.Argb(x, y))) { x++; continue; }
                    int run = x;
                    while (x <= x1 && Ink(image.Argb(x, y))) x++;
                    if (x - run >= 2) band.Add(run, y, x);
                }
            }
            return Parse(image, band);
        }

        private static bool HasDigit(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < text.Length; i++)
                if (text[i] >= '0' && text[i] <= '9') return true;
            return false;
        }

        private static XpSight Parse(ImageBuffer image, InkBand band)
        {
            XpSight sight = new XpSight();
            int w = band.X1 - band.X0;
            int h = band.Y1 - band.Y0 + 1;
            if (h < 6 || h > 48 || w < 8 || w > 420) return sight;
            bool[] mask = new bool[w * h];
            int ink = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Ink(image.Argb(band.X0 + x, band.Y0 + y))) { mask[y * w + x] = true; ink++; }
            if (ink < 8 || ink * 3 > w * h) return sight;
            string text = "";
            bool plus = false;
            bool digit = false;
            int others = 0;
            int cursor = 0;
            while (cursor < w)
            {
                if (!Column(mask, w, h, cursor)) { cursor++; continue; }
                int start = cursor;
                while (cursor < w && Column(mask, w, h, cursor)) cursor++;
                int gw = cursor - start;
                int top = h, bottom = 0;
                for (int y = 0; y < h; y++)
                    for (int x = start; x < cursor; x++)
                        if (mask[y * w + x])
                        {
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                        }
                int gh = bottom - top + 1;
                if (gw < 2 || gh < 5) continue;
                if (IsPlus(mask, w, start, top, gw, gh)) { text += "+"; plus = true; continue; }
                int value = DigitFont.Match(Sample(mask, w, start, top, gw, gh));
                if (value >= 0) { text += value.ToString(); digit = true; }
                else others++;
            }
            if (plus && !digit && others >= 2) digit = true;
            if (!digit || !plus) return sight;
            sight.Found = true;
            sight.Plus = plus;
            sight.Text = text;
            sight.Rect = new IntRect(band.X0, band.Y0, w, h);
            sight.Trace = "xp=" + text + "@" + band.X0.ToString() + "," + band.Y0.ToString() + " " + w.ToString() + "x" + h.ToString();
            return sight;
        }

        private static bool Column(bool[] mask, int stride, int height, int x)
        {
            for (int y = 0; y < height; y++) if (mask[y * stride + x]) return true;
            return false;
        }

        private static bool IsPlus(bool[] mask, int stride, int left, int top, int w, int h)
        {
            if (w + 2 < h / 2 || w > h + 3) return false;
            int row = 0;
            int midY = top + h / 2;
            for (int x = left; x < left + w; x++) if (mask[midY * stride + x]) row++;
            int col = 0;
            int midX = left + w / 2;
            for (int y = top; y < top + h; y++) if (mask[y * stride + midX]) col++;
            int area = 0;
            for (int y = top; y < top + h; y++)
                for (int x = left; x < left + w; x++)
                    if (mask[y * stride + x]) area++;
            return row * 5 >= w * 3 && col * 5 >= h * 3 && area * 3 < w * h;
        }

        private static int[,] Sample(bool[] mask, int stride, int left, int top, int w, int h)
        {
            int[,] grid = new int[7, 5];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (mask[(top + y) * stride + left + x])
                        grid[Math.Min(6, y * 7 / h), Math.Min(4, x * 5 / w)] = 1;
            return grid;
        }

        private static bool Ink(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            // Orange floating "+N xp" text, including the darker outline.
            // Near-white scenery is not included; it joins the glyphs into one blob.
            return r >= 140 && g >= 55 && g <= 220 && b <= 160 && r >= g + 15 && r >= b + 25;
        }

        private sealed class InkBand
        {
            public int X0, Y0, X1, Y1;
            public bool Dead;
            private bool any;

            public bool Touches(int x0, int y, int x1)
            {
                if (Dead || !any) return false;
                if (y > Y1 + 2 || y + 2 < Y0) return false;
                return x1 >= X0 - 6 && x0 <= X1 + 6;
            }

            public void Add(int x0, int y, int x1)
            {
                if (!any) { X0 = x0; X1 = x1; Y0 = y; Y1 = y; any = true; }
                else
                {
                    if (x0 < X0) X0 = x0;
                    if (x1 > X1) X1 = x1;
                    if (y < Y0) Y0 = y;
                    if (y > Y1) Y1 = y;
                }
                if (X1 - X0 > 420 || Y1 - Y0 > 48) Dead = true;
            }
        }
    }

    public sealed class XpSight
    {
        public bool Found;
        public bool Plus;
        public int Scale;
        public string Text = "";
        public string Trace = "xp=none";
        public IntRect Rect;
        public int X { get { return Rect.X; } }
        public int Y { get { return Rect.Y; } }
    }

    public static class PackWatch
    {
        public static IntRect Find(ImageBuffer image)
        {
            // A complete 4 by 7 lattice wins. Otherwise take a partial slot grid
            // anywhere on the client, which is what a moved backpack looks like.
            IntRect square = FindSquare(image);
            if (square.Width > 8) return square;
            return FindGrid(image);
        }

        // Four columns and three rows of slot borders, searched across the whole
        // client so a moved backpack is found in any corner.
        private static IntRect FindGrid(ImageBuffer image)
        {
            if (image == null || image.Width < 140 || image.Height < 100) return new IntRect();
            int[] widths = new int[] { 30, 36, 42, 46, 56, 68, 84 };
            int[] heights = new int[] { 24, 30, 36, 42, 48, 56, 68 };
            int step = image.Width > 1600 ? 6 : 4;
            int bestHits = 0;
            int bestAvg = 9999;
            IntRect found = new IntRect();
            for (int ih = 0; ih < heights.Length; ih++)
            {
                int cellH = heights[ih];
                for (int iw = 0; iw < widths.Length; iw++)
                {
                    int cellW = widths[iw];
                    int gridW = cellW * 4;
                    int gridH = cellH * 3;
                    if (gridW >= image.Width || gridH >= image.Height) continue;
                    for (int y = 0; y + gridH < image.Height; y += step)
                    {
                        for (int x = 0; x + gridW < image.Width; x += step)
                        {
                            int hits;
                            int avg;
                            if (!ScoreLive(image, x, y, cellW, cellH, out hits, out avg)) continue;
                            if (x < 8 || y < 8) continue;
                            if (hits < 10) continue;
                            // Dark empty slots outrank a bright panel that happens to have edges.
                            bool better = bestHits < 10 || avg + 20 < bestAvg || (avg <= bestAvg + 20 && hits > bestHits);
                            if (better)
                            {
                                bestHits = hits;
                                bestAvg = avg;
                                found = new IntRect(x, y, gridW, gridH);
                            }
                        }
                    }
                }
            }
            if (bestHits < 10) return new IntRect();
            return found;
        }

        private static IntRect FindLive(ImageBuffer image)
        {
            if (image == null || image.Width < 400 || image.Height < 300) return new IntRect();
            int cellW0 = Math.Max(16, image.Width * 16 / 1000);
            int cellW1 = Math.Max(cellW0 + 2, image.Width * 22 / 1000);
            int cellH0 = Math.Max(14, image.Height * 28 / 1000);
            int cellH1 = Math.Max(cellH0 + 2, image.Height * 36 / 1000);
            int stepW = Math.Max(2, (cellW1 - cellW0) / 3);
            int stepH = Math.Max(2, (cellH1 - cellH0) / 3);
            int xStart = image.Width * 68 / 100;
            int yStart = image.Height * 78 / 100;
            int bestHits = 0;
            int bestAvg = 9999;
            IntRect found = new IntRect();
            for (int cellH = cellH0; cellH <= cellH1; cellH += stepH)
            {
                for (int cellW = cellW0; cellW <= cellW1; cellW += stepW)
                {
                    int gridW = cellW * 4;
                    int gridH = cellH * 3;
                    if (xStart + gridW >= image.Width || yStart + gridH >= image.Height) continue;
                    int step = Math.Max(3, cellW / 10);
                    for (int y = yStart; y + gridH < image.Height; y += step)
                    {
                        for (int x = xStart; x + gridW < image.Width; x += step)
                        {
                            int hits;
                            int avg;
                            if (!ScoreLive(image, x, y, cellW, cellH, out hits, out avg)) continue;
                            if (hits > bestHits || (hits == bestHits && avg < bestAvg))
                            {
                                bestHits = hits;
                                bestAvg = avg;
                                found = new IntRect(x, y, gridW, gridH);
                            }
                        }
                    }
                }
            }
            if (bestHits < 9) return new IntRect();
            return found;
        }

        private static bool ScoreLive(ImageBuffer image, int x, int y, int cellW, int cellH, out int hits, out int avg)
        {
            hits = 0;
            int sum = 0;
            int cells = 0;
            int dark = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int left = x + col * cellW;
                    int top = y + row * cellH;
                    int cx = left + cellW / 2;
                    int cy = top + cellH / 2;
                    if (cx >= image.Width || cy >= image.Height) continue;
                    int center = Lum(image.Argb(cx, cy));
                    int edge = center;
                    for (int dx = 0; dx < 4; dx++)
                    {
                        int sample = Lum(image.Argb(Math.Min(image.Width - 1, left + dx), cy));
                        if (sample > edge) edge = sample;
                    }
                    int topEdge = center;
                    for (int dy = 0; dy < 3; dy++)
                    {
                        int sample = Lum(image.Argb(cx, Math.Min(image.Height - 1, top + dy)));
                        if (sample > topEdge) topEdge = sample;
                    }
                    int inside = Lum(image.Argb(Math.Min(image.Width - 1, left + Math.Max(6, cellW / 6)), cy));
                    if (edge > center + 22 && edge > inside + 8 && topEdge > center + 14 && center < 220) hits++;
                    if (center < 90) dark++;
                    sum += center;
                    cells++;
                }
            }
            avg = cells == 0 ? 9999 : sum / cells;
            return hits >= 9 && dark >= 8 && avg < 140;
        }

        private static int Lum(int color)
        {
            return ImageBuffer.R(color) + ImageBuffer.G(color) + ImageBuffer.B(color);
        }

        private static IntRect FindSquare(ImageBuffer image)
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
            // A real backpack is a full lattice. A score of 20 is a random UI edge.
            if (best < 26) return new IntRect();
            if (found.Y < image.Height * 40 / 100 && best < 28) return new IntRect();
            return found;
        }

        public static int[] Slots(ImageBuffer image, IntRect grid)
        {
            int[] slots = new int[28];
            if (image == null || grid.Width < 16 || grid.Height < 16) return slots;
            int cellW = grid.Width / 4;
            int cellH = grid.Height / 7;
            if (cellW < 4 || cellH < 4) return slots;
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                {
                    int x0 = grid.X + col * cellW + cellW / 4;
                    int y0 = grid.Y + row * cellH + cellH / 4;
                    int x1 = x0 + Math.Max(1, cellW / 2);
                    int y1 = y0 + Math.Max(1, cellH / 2);
                    int sum = 0, n = 0, r = 0, g = 0, b = 0;
                    for (int y = y0; y < y1 && y < image.Height; y += 2)
                        for (int x = x0; x < x1 && x < image.Width; x += 2)
                        {
                            int color = image.Argb(x, y);
                            r += ImageBuffer.R(color);
                            g += ImageBuffer.G(color);
                            b += ImageBuffer.B(color);
                            sum += ImageBuffer.R(color) + ImageBuffer.G(color) + ImageBuffer.B(color);
                            n++;
                        }
                    if (n < 1 || sum / n < 90) continue;
                    slots[row * 4 + col] = 1 + (r / n) / 48 + 8 * ((g / n) / 48) + 64 * ((b / n) / 48);
                }
            return slots;
        }

        public static bool SameSlots(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static int Hash(ImageBuffer image, IntRect grid)
        {
            int[] slots = Slots(image, grid);
            int hash = 17;
            for (int i = 0; i < slots.Length; i++) hash = unchecked(hash * 31 + slots[i]);
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

    // A confirmed XP drop resets idle immediately. The same number still
    // floating on that spot is not a second drop. A backpack grid, once
    // found, stays put when a later frame misses it.
    // The '+N xp' line floats in the centre of the sky for a second or two.
    // Any compact gold, orange or white mark there counts, including while it
    // fades or drifts upward. The digits do not have to be read.
    public static class XpFloat
    {
        public struct Sight
        {
            public bool Found;
            public int X;
            public int Y;
            public int W;
            public int H;
            public int Ink;
            public int Stamp;
        }

        public static bool Present(ImageBuffer image)
        {
            return Look(image).Found;
        }

        public static Sight Look(ImageBuffer image)
        {
            if (image == null || image.Width < 80 || image.Height < 80) return new Sight();
            int x0 = image.Width * 20 / 100;
            int x1 = image.Width * 80 / 100;
            int y0 = 0;
            int y1 = image.Height * 40 / 100;
            return Scan(image, x0, x1, y0, y1);
        }

        // A crop that is already the centre-top uses the same colour test.
        public static bool InCrop(ImageBuffer image)
        {
            if (image == null || image.Width < 40 || image.Height < 16) return false;
            if (image.Height * 2 > image.Width) return Present(image);
            return Scan(image, 0, image.Width, 0, image.Height).Found;
        }

        // True when the mark appears, moves, or its pixels change. A mark that
        // is still sitting in the same place with the same pixels is not a new drop.
        public static bool Changed(Sight before, Sight now)
        {
            if (!now.Found) return false;
            if (!before.Found) return true;
            if (now.Stamp != before.Stamp) return true;
            if (Math.Abs(now.X - before.X) > 6 || Math.Abs(now.Y - before.Y) > 3) return true;
            return false;
        }

        private struct Run
        {
            public int Left;
            public int Right;
            public int Count;
        }

        private struct Band
        {
            public int Top;
            public int Bottom;
            public int Left;
            public int Right;
            public int Ink;
        }

        private static Sight Scan(ImageBuffer image, int x0, int x1, int y0, int y1)
        {
            Sight none = new Sight();
            if (image == null || y1 <= y0 + 4 || x1 <= x0 + 24) return none;
            int w = image.Width;
            int h = image.Height;
            int gap = Math.Max(3, w / 420);
            // A compass can sit in the middle of the line. Join across that gap.
            int join = Math.Max(18, w / 50);
            int minW = Math.Max(64, w * 5 / 100);
            int maxW = Math.Max(180, w * 16 / 100);
            int maxH = Math.Max(16, h * 2 / 100);
            int minInk = Math.Max(40, w * 3 / 100);
            if (x0 < gap) x0 = gap;
            if (x1 > w - gap) x1 = w - gap;
            if (y0 < 0) y0 = 0;
            if (y1 > h) y1 = h;
            if (x1 <= x0 + 8 || y1 <= y0 + 3) return none;
            int rows = y1 - y0;
            List<Run>[] lines = new List<Run>[rows];
            for (int y = y0; y < y1; y++)
            {
                List<Run> runs = new List<Run>();
                int runLeft = 0;
                int runRight = 0;
                int runCount = 0;
                int prev = -999;
                for (int x = x0; x < x1; x++)
                {
                    if (!Warm(image, x, y, gap)) continue;
                    if (runCount > 0 && x - prev > join)
                    {
                        if (runCount >= 4)
                        {
                            Run closed = new Run();
                            closed.Left = runLeft;
                            closed.Right = runRight;
                            closed.Count = runCount;
                            runs.Add(closed);
                        }
                        runCount = 0;
                    }
                    if (runCount == 0) runLeft = x;
                    runRight = x;
                    runCount++;
                    prev = x;
                }
                if (runCount >= 4)
                {
                    Run closed = new Run();
                    closed.Left = runLeft;
                    closed.Right = runRight;
                    closed.Count = runCount;
                    runs.Add(closed);
                }
                lines[y - y0] = runs;
            }
            List<Band> bands = new List<Band>();
            for (int i = 0; i < rows; i++)
            {
                List<Run> runs = lines[i];
                if (runs == null) continue;
                for (int r = 0; r < runs.Count; r++)
                {
                    Run run = runs[r];
                    bool placed = false;
                    for (int b = 0; b < bands.Count; b++)
                    {
                        Band band = bands[b];
                        if (i != band.Bottom && i != band.Bottom + 1) continue;
                        if (run.Right < band.Left - join || run.Left > band.Right + join) continue;
                        if (run.Left < band.Left) band.Left = run.Left;
                        if (run.Right > band.Right) band.Right = run.Right;
                        band.Bottom = i;
                        band.Ink += run.Count;
                        bands[b] = band;
                        placed = true;
                        break;
                    }
                    if (!placed)
                    {
                        Band band = new Band();
                        band.Top = i;
                        band.Bottom = i;
                        band.Left = run.Left;
                        band.Right = run.Right;
                        band.Ink = run.Count;
                        bands.Add(band);
                    }
                }
            }
            Sight best = none;
            for (int b = 0; b < bands.Count; b++)
            {
                Band band = bands[b];
                int height = band.Bottom - band.Top + 1;
                int width = band.Right - band.Left + 1;
                if (height < 3 || height > maxH || width < minW || width > maxW || band.Ink < minInk) continue;
                int pale = 0;
                int green = 0;
                int n = 0;
                int stamp = 17;
                int top = y0 + band.Top;
                for (int y = top; y < top + height; y++)
                {
                    for (int x = band.Left; x <= band.Right; x++)
                    {
                        if (!Warm(image, x, y, gap)) continue;
                        int color = image.Argb(x, y);
                        int g = ImageBuffer.G(color);
                        int bl = ImageBuffer.B(color);
                        n++;
                        green += g - bl;
                        if (bl + 28 >= g) pale++;
                        stamp = unchecked(stamp * 31 + x);
                        stamp = unchecked(stamp * 31 + y);
                    }
                }
                // Real popup ink is pale gold. Leaves and the compass ring are greener.
                if (n < 10 || pale * 5 < n * 2 || green > n * 34) continue;
                if (best.Found && n <= best.Ink) continue;
                best.Found = true;
                best.X = band.Left;
                best.Y = top;
                best.W = width;
                best.H = height;
                best.Ink = n;
                best.Stamp = stamp;
            }
            return best;
        }

        private static bool Warm(ImageBuffer image, int x, int y, int gap)
        {
            int color = image.Argb(x, y);
            int left = image.Argb(x - gap, y);
            int right = image.Argb(x + gap, y);
            int r = ImageBuffer.R(color);
            int g = ImageBuffer.G(color);
            int b = ImageBuffer.B(color);
            int baseWarm = ((ImageBuffer.R(left) - ImageBuffer.B(left)) + (ImageBuffer.R(right) - ImageBuffer.B(right))) / 2;
            return r - b > baseWarm + 8 && r + 8 >= g && g + 4 >= b && r > 100 && b < 210 && r - b > 14;
        }
    }

    public sealed class SkillSense
    {
        public bool XpEvent;
        public bool PackEvent;
        public bool View;
        public int LockedScale;
        public IntRect XpRect;
        public string Trace = "xp=none pack=none";
        private int scaleVote;
        private int scaleVotes;
        private string shown = "";
        private int lastDropX = -1;
        private int lastDropY = -1;
        private DateTime lastDropAt = DateTime.MinValue;
        private IntRect heldPack;
        private IntRect pendingPack;
        private int quiet;
        private bool ever;
        private int[] pendingSlots;
        private int slotHits;
        private int[] confirmedSlots;

        public void Observe(ImageBuffer xpImage, ImageBuffer packImage)
        {
            XpEvent = false;
            PackEvent = false;
            bool saw = false;
            string xpText = "xp=none";
            if (xpImage != null)
            {
                var sight = SceneActivity.XpWatch.Read(xpImage, LockedScale);
                if (sight.Found && HasXpDigit(sight.Text))
                {
                    saw = true;
                    ever = true;
                    XpRect = sight.Rect;
                    xpText = sight.Trace;
                    if (sight.Scale == scaleVote) scaleVotes++;
                    else { scaleVote = sight.Scale; scaleVotes = 1; }
                    if (scaleVotes >= 2 && (sight.Scale == 100 || sight.Scale == 150 || sight.Scale == 200))
                        LockedScale = sight.Scale;
                    // Each confirmed drop resets idle. The same number still
                    // sitting on that spot about a second later is the drop
                    // floating, not a second drop.
                    int cx = sight.X + Math.Max(1, sight.Rect.Width) / 2;
                    int cy = sight.Y + Math.Max(1, sight.Rect.Height) / 2;
                    bool close = sight.Text == shown && lastDropX >= 0
                        && Math.Abs(cx - lastDropX) < 48 && Math.Abs(cy - lastDropY) < 48;
                    bool recent = lastDropAt != DateTime.MinValue && (DateTime.UtcNow - lastDropAt).TotalSeconds < 1.2;
                    if (!(close && recent)) XpEvent = true;
                    shown = sight.Text;
                    lastDropX = cx;
                    lastDropY = cy;
                    lastDropAt = DateTime.UtcNow;
                }
            }
            string packText = "pack=none";
            if (packImage != null)
            {
                IntRect grid = SceneActivity.PackWatch.Find(packImage);
                if (grid.Width > 8)
                {
                    if (heldPack.Width < 8 || NearGrid(grid, heldPack) || (pendingPack.Width > 8 && NearGrid(grid, pendingPack)))
                        heldPack = grid;
                    else
                        pendingPack = grid;
                    grid = heldPack;
                }
                else if (heldPack.Width > 8)
                {
                    // A locked grab is already the backpack. A full frame that misses keeps the last grid.
                    if (packImage.Width < 500 && packImage.Height < 500)
                        grid = new IntRect(0, 0, packImage.Width, packImage.Height);
                    else
                        grid = heldPack;
                }
                if (grid.Width > 8)
                {
                    saw = true;
                    ever = true;
                    int[] now = SceneActivity.PackWatch.Slots(packImage, grid);
                    packText = "pack@" + grid.X.ToString() + "," + grid.Y.ToString() + " slots";
                    if (pendingSlots == null || !SceneActivity.PackWatch.SameSlots(pendingSlots, now))
                    {
                        pendingSlots = now;
                        slotHits = 1;
                    }
                    else slotHits++;
                    if (slotHits >= 2 && confirmedSlots != null && !SceneActivity.PackWatch.SameSlots(confirmedSlots, now))
                        PackEvent = true;
                    if (slotHits >= 2) confirmedSlots = now;
                }
            }
            if (saw) quiet = 0;
            else if (quiet < 4) quiet++;
            View = saw || (ever && quiet < 3);
            Trace = xpText + " " + packText + (XpEvent ? " xp-event" : "") + (PackEvent ? " pack-event" : "");
        }

        private static bool NearGrid(IntRect a, IntRect b)
        {
            int dx = (a.X + a.Width / 2) - (b.X + b.Width / 2);
            int dy = (a.Y + a.Height / 2) - (b.Y + b.Height / 2);
            return dx * dx + dy * dy < 80 * 80;
        }

        private static bool HasXpDigit(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < text.Length; i++)
                if (text[i] >= '0' && text[i] <= '9') return true;
            return false;
        }
    }
}
