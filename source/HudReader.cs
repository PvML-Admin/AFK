using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class HudReading
    {
        public int? Hp;
        public int? HpMax;
        public bool HpFromOrb;
        public string HpHow = "";
        public int? Prayer;
        public int? PrayerMax;
        public bool PrayerFromOrb;
        public string PrayerHow = "";
        public int? PotionSeconds;
        public int PotionCount;
        public string PotionHow = "";
        public bool BuffBarFound;
        public List<BuffIcon> Buffs = new List<BuffIcon>();
    }

    internal struct InkBlob
    {
        public int Left, Top, Width, Height;
        public int Digit;
        public bool Slash;
    }

    public static class DigitFont
    {
        public static readonly string[] Glyphs = new string[] {
            "01110100011000110001100011000101110",
            "00100011000010000100001000010001110",
            "01110100010000100010001000100011111",
            "11110100010000101110000011000101110",
            "00010001100101010010111110001000010",
            "11111100001111000001000011000101110",
            "01110010001000011110100011000101110",
            "11111000010001000100010000100001000",
            "01110100011000101110100011000101110",
            "01110100011000101111000010001001100"
        };

        // Compare the ink after it has been stretched to the full grid. A drawn
        // digit's bounding box does not include the empty margin in the prototype.
        public static int Match(int[,] grid)
        {
            int best = 99;
            int second = 99;
            int value = -1;
            for (int digit = 0; digit < Glyphs.Length; digit++)
            {
                int[,] canonical = Canonical(Glyphs[digit]);
                int distance = 0;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                        if (grid[y, x] != canonical[y, x]) distance++;
                if (distance < best)
                {
                    second = best;
                    best = distance;
                    value = digit;
                }
                else if (distance < second) second = distance;
            }
            if (best > 8) return -1;
            if (second - best < 1 && best > 2) return -1;
            return value;
        }

        private static int[,] Canonical(string glyph)
        {
            int minX = 5, minY = 7, maxX = -1, maxY = -1;
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 5; x++)
                    if (glyph[y * 5 + x] == '1')
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
            int[,] grid = new int[7, 5];
            if (maxX < minX) return grid;
            int bw = maxX - minX + 1;
            int bh = maxY - minY + 1;
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 5; x++)
                {
                    int sx = minX + Math.Min(bw - 1, (x * bw + bw / 2) / 5);
                    int sy = minY + Math.Min(bh - 1, (y * bh + bh / 2) / 7);
                    grid[y, x] = glyph[sy * 5 + sx] == '1' ? 1 : 0;
                }
            return grid;
        }
    }

    public static class HudReader
    {
        public static HudReading Read(ImageBuffer image, WardenSettings settings)
        {
            HudReading reading = new HudReading();
            if (settings.HasHpPin)
            {
                NumberRead number = ReadNumber(image, Pin(image, settings.HpPinX, settings.HpPinY, settings.HpPinW, settings.HpPinH), AnyInk);
                Apply(reading, true, number, "pin");
            }
            if (settings.HasPrayerPin)
            {
                NumberRead number = ReadNumber(image, Pin(image, settings.PrayerPinX, settings.PrayerPinY, settings.PrayerPinW, settings.PrayerPinH), AnyInk);
                Apply(reading, false, number, "pin");
            }
            if (!settings.HasHpPin || !settings.HasPrayerPin)
                ReadStrip(image, reading, !settings.HasHpPin, !settings.HasPrayerPin);
            ReadBuffBar(image, settings, reading);
            return reading;
        }

        // The real Kavros strip is movable. Life is the red bar, prayer is the
        // purple bar. Adrenaline (yellow) and summoning (green) are ignored.
        private static void ReadStrip(ImageBuffer image, HudReading reading, bool wantLife, bool wantPrayer)
        {
            List<IntRect> reds = FindBars(image, new IntRect(0, 0, image.Width, image.Height), RedBar);
            List<IntRect> purples = FindBars(image, new IntRect(0, 0, image.Width, image.Height), PurpleBar);
            IntRect red = new IntRect();
            IntRect purple = new IntRect();
            bool pair = false;
            int best = 0;
            for (int i = 0; i < reds.Count; i++)
                for (int j = 0; j < purples.Count; j++)
                {
                    if (Math.Abs(reds[i].Y - purples[j].Y) > 12) continue;
                    if (purples[j].X <= reds[i].X) continue;
                    IntRect redSpan = SpanRow(reds, reds[i]);
                    IntRect purpleSpan = SpanRow(purples, purples[j]);
                    int score = redSpan.Width + purpleSpan.Width;
                    if (!pair || score > best) { pair = true; best = score; red = redSpan; purple = purpleSpan; }
                }
            if (wantLife && pair)
            {
                NumberRead number = ReadNumber(image, Above(image, red), WhiteInk);
                Apply(reading, true, number, "bar");
            }
            if (wantPrayer && pair)
            {
                NumberRead number = ReadNumber(image, Above(image, purple), WhiteInk);
                Apply(reading, false, number, "bar");
            }
        }

        private static IntRect SpanRow(List<IntRect> bars, IntRect seed)
        {
            int left = seed.X;
            int right = seed.Right;
            for (int i = 0; i < bars.Count; i++)
            {
                if (Math.Abs(bars[i].Y - seed.Y) > 6) continue;
                if (bars[i].X < left) left = bars[i].X;
                if (bars[i].Right > right) right = bars[i].Right;
            }
            return new IntRect(left, seed.Y, right - left, seed.Height);
        }

        private static IntRect Above(ImageBuffer image, IntRect bar)
        {
            int height = Math.Max(22, bar.Height * 4);
            return Clamp(image, bar.X - 8, bar.Y - height, Math.Max(bar.Width + 16, 80), height);
        }

        private static void ReadBuffBar(ImageBuffer image, WardenSettings settings, HudReading reading)
        {
            IntRect region = settings.HasBuffPin
                ? Pin(image, settings.BuffPinX, settings.BuffPinY, settings.BuffPinW, settings.BuffPinH)
                : new IntRect(0, 0, image.Width, image.Height);
            List<IntRect> icons = FindBuffIcons(image, region);
            reading.Buffs = new List<BuffIcon>();
            if (icons.Count == 0)
            {
                reading.PotionHow = "none";
                return;
            }
            reading.BuffBarFound = true;
            reading.PotionHow = "bar";
            for (int i = 0; i < icons.Count; i++)
            {
                BuffIcon icon = ReadIconTime(image, icons[i]);
                reading.Buffs.Add(icon);
                if (icon.HasTime) reading.PotionCount++;
            }
        }

        private static bool BorderGreen(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 150 && g > r + 40 && g > b + 30;
        }

        private static List<IntRect> FindBuffIcons(ImageBuffer image, IntRect region)
        {
            List<IntRect> found = new List<IntRect>();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            for (int size = 40; size >= 28; size -= 4)
            {
                for (int y = region.Y; y + size <= region.Bottom; y += 2)
                    for (int x = region.X; x + size <= region.Right; x += 2)
                    {
                        if (!IconAt(image, x, y, size)) continue;
                        bool overlap = false;
                        for (int i = 0; i < found.Count; i++)
                        {
                            IntRect old = found[i];
                            if (x < old.Right && x + size > old.X && y < old.Bottom && y + size > old.Y) overlap = true;
                        }
                        if (overlap) continue;
                        found.Add(new IntRect(x, y, size, size));
                    }
            }
            if (found.Count == 0) return found;
            found.Sort(delegate(IntRect a, IntRect b) { return a.X.CompareTo(b.X); });
            int bestStart = 0, bestCount = 1, runStart = 0, run = 1;
            for (int i = 1; i < found.Count; i++)
            {
                if (Math.Abs(found[i].Y - found[runStart].Y) <= 8 && found[i].X - found[i - 1].Right < 24)
                    run++;
                else { runStart = i; run = 1; }
                if (run > bestCount) { bestCount = run; bestStart = runStart; }
            }
            List<IntRect> row = new List<IntRect>();
            for (int i = bestStart; i < bestStart + bestCount; i++) row.Add(found[i]);
            return row;
        }

        private static bool IconAt(ImageBuffer image, int x, int y, int size)
        {
            int edge = 0, edgeTotal = 0, inside = 0, insideTotal = 0;
            for (int i = 0; i < size; i++)
            {
                edgeTotal += 4;
                if (BorderGreen(image.Argb(x + i, y))) edge++;
                if (BorderGreen(image.Argb(x + i, y + size - 1))) edge++;
                if (BorderGreen(image.Argb(x, y + i))) edge++;
                if (BorderGreen(image.Argb(x + size - 1, y + i))) edge++;
            }
            int inset = Math.Max(4, size / 6);
            for (int yy = y + inset; yy < y + size - inset; yy += 2)
                for (int xx = x + inset; xx < x + size - inset; xx += 2)
                {
                    insideTotal++;
                    if (BorderGreen(image.Argb(xx, yy))) inside++;
                }
            return edgeTotal > 0 && edge * 100 / edgeTotal >= 55 && (insideTotal == 0 || inside * 100 / insideTotal < 30);
        }

        private static BuffIcon ReadIconTime(ImageBuffer image, IntRect icon)
        {
            BuffIcon result = new BuffIcon();
            result.X = icon.X;
            int textH = Math.Max(16, icon.Height / 2);
            IntRect text = Clamp(image, icon.X + 1, icon.Bottom - textH, Math.Max(8, icon.Width - 2), textH - 1);
            NumberRead number = ReadNumber(image, text, WhiteInk);
            if (!number.Ok) return result;
            bool minutes = IconHasMinuteMark(image, text);
            result.HasTime = true;
            result.Minutes = minutes;
            result.Value = number.Current;
            return result;
        }

        // The letter m sits to the right of the digits and is wider than a digit.
        private static bool IconHasMinuteMark(ImageBuffer image, IntRect text)
        {
            bool[] mask = Mask(image, text, WhiteInk);
            List<Blob> blobs = Components(mask, text.Width, text.Height);
            int digits = 0;
            bool letter = false;
            int digitRight = -1;
            for (int i = 0; i < blobs.Count; i++)
            {
                Blob blob = blobs[i];
                if (blob.Height < 6 || blob.Height > 24 || blob.Area < 8) continue;
                int digit = DigitFont.Match(Sample(mask, text.Width, blob));
                if (digit >= 0)
                {
                    digits++;
                    if (blob.Right > digitRight) digitRight = blob.Right;
                }
                else if (blob.Width >= blob.Height / 2 && blob.Left >= digitRight) letter = true;
            }
            return digits > 0 && letter;
        }

        private static string ReadAuto(ImageBuffer image, bool life, HudReading reading)
        {
            IntRect band = new IntRect(0, (int)(image.Height * 0.76), image.Width, image.Height - (int)(image.Height * 0.76));
            List<IntRect> bars = FindBars(image, band, life ? (InkTest)GreenInk : BlueInk);
            bars.Sort(delegate(IntRect a, IntRect b) { return b.Width.CompareTo(a.Width); });
            for (int i = 0; i < bars.Count; i++)
            {
                IntRect bar = bars[i];
                int pad = Math.Max(28, bar.Height * 4);
                IntRect search = Clamp(image, bar.X - 8, bar.Y - pad, bar.Width + 120, bar.Height + pad + 6);
                NumberRead number = ReadNumber(image, search, life ? (InkTest)LifeDigit : PrayerDigit);
                if (number.Ok)
                {
                    Apply(reading, life, number, "bar");
                    return "bar";
                }
            }
            IntRect orbs = new IntRect((int)(image.Width * 0.68), 0, image.Width - (int)(image.Width * 0.68), (int)(image.Height * 0.46));
            int percent;
            if (TryOrb(image, orbs, life ? (InkTest)RedOrb : BlueOrb, out percent))
            {
                NumberRead number = new NumberRead();
                number.Ok = true;
                number.Current = percent;
                number.Max = 100;
                number.HasMax = true;
                number.Orb = true;
                Apply(reading, life, number, "orb");
                return "orb";
            }
            if (life) reading.HpHow = "";
            else reading.PrayerHow = "";
            return "";
        }

        private static void Apply(HudReading reading, bool life, NumberRead number, string how)
        {
            if (!number.Ok || number.Current <= 0 || number.Current > 9999999) return;
            if (number.HasMax && (number.Max <= 0 || number.Max > 9999999)) number.HasMax = false;
            if (life)
            {
                reading.Hp = number.Current;
                reading.HpMax = number.HasMax ? (int?)number.Max : null;
                reading.HpFromOrb = number.Orb;
                reading.HpHow = how;
            }
            else
            {
                reading.Prayer = number.Current;
                reading.PrayerMax = number.HasMax ? (int?)number.Max : null;
                reading.PrayerFromOrb = number.Orb;
                reading.PrayerHow = how;
            }
        }

        private delegate bool InkTest(int argb);

        private static bool WhiteInk(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            int low = Math.Min(r, Math.Min(g, b));
            int high = Math.Max(r, Math.Max(g, b));
            return low >= 165 && high - low <= 50;
        }

        private static bool GreenInk(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 110 && g > r + 25 && g > b + 15;
        }

        private static bool BlueInk(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return b >= 110 && b > r + 25 && b > g + 10;
        }

        private static bool RedBar(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return r >= 150 && r > g + 50 && r > b + 50 && b < 110;
        }

        private static bool PurpleBar(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return b >= 120 && r >= 80 && b > g + 30 && r > g + 15 && b + 40 > r && g < 140;
        }

        private static bool RedOrb(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return r >= 140 && r > g + 40 && r > b + 40 && g < 130;
        }

        private static bool BlueOrb(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return b >= 130 && b > r + 30 && b > g + 10 && r < 140;
        }

        private static bool LifeDigit(int c) { return WhiteInk(c) || GreenInk(c); }
        private static bool PrayerDigit(int c) { return WhiteInk(c) || BlueInk(c); }
        private static bool AnyInk(int c) { return WhiteInk(c) || GreenInk(c) || BlueInk(c); }

        private struct NumberRead
        {
            public bool Ok;
            public int Current;
            public int Max;
            public bool HasMax;
            public bool Orb;
        }

        private static bool TryOrb(ImageBuffer image, IntRect region, InkTest ink, out int percent)
        {
            percent = 0;
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            if (region.IsEmpty) return false;
            bool[] mask = Mask(image, region, ink);
            List<Blob> blobs = Components(mask, region.Width, region.Height);
            Blob best = null;
            for (int i = 0; i < blobs.Count; i++)
            {
                Blob blob = blobs[i];
                if (blob.Width < 16 || blob.Height < 16 || blob.Width > 96 || blob.Height > 96) continue;
                double aspect = blob.Width / (double)blob.Height;
                if (aspect < 0.72 || aspect > 1.38) continue;
                if (best == null || blob.Area > best.Area) best = blob;
            }
            if (best == null) return false;
            double radius = Math.Min(best.Width, best.Height) / 2.0;
            double disk = Math.PI * radius * radius;
            if (disk < 1) return false;
            percent = (int)Math.Round(100.0 * best.Area / disk);
            if (percent < 1) return false;
            if (percent > 100) percent = 100;
            return true;
        }

        public static string Explain(ImageBuffer image, IntRect region)
        {
            NumberRead number = ReadNumber(image, region, AnyInk);
            return LastTrace + " => " + (number.Ok ? number.Current.ToString() + "/" + (number.HasMax ? number.Max.ToString() : "-") : "none");
        }

        private static string LastTrace = "";

        private static NumberRead ReadNumber(ImageBuffer image, IntRect region, InkTest ink)
        {
            NumberRead result = new NumberRead();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            if (region.Width < 8 || region.Height < 8) return result;
            bool[] mask = Mask(image, region, ink);
            List<Blob> blobs = Components(mask, region.Width, region.Height);
            List<InkBlob> glyphs = new List<InkBlob>();
            System.Text.StringBuilder rejected = new System.Text.StringBuilder();
            for (int i = 0; i < blobs.Count; i++)
            {
                Blob blob = blobs[i];
                if (blob.Height >= 3 && blob.Height < 8 && blob.Width <= 12 && blob.Area >= 3 && blob.Area <= 48)
                {
                    InkBlob comma = new InkBlob();
                    comma.Left = blob.Left;
                    comma.Top = blob.Top;
                    comma.Width = blob.Width;
                    comma.Height = blob.Height;
                    comma.Digit = -2;
                    glyphs.Add(comma);
                    continue;
                }
                if (blob.Height < 8 || blob.Height > 42 || blob.Width > 36 || blob.Area < 12)
                {
                    rejected.Append(" skip ").Append(blob.Width).Append("x").Append(blob.Height).Append(" a").Append(blob.Area);
                    continue;
                }
                int[,] grid = Sample(mask, region.Width, blob);
                int digit = DigitFont.Match(grid);
                InkBlob inkBlob = new InkBlob();
                inkBlob.Left = blob.Left;
                inkBlob.Top = blob.Top;
                inkBlob.Width = blob.Width;
                inkBlob.Height = blob.Height;
                inkBlob.Digit = digit;
                bool diagonal = IsDiagonal(mask, region.Width, blob);
                bool thin = blob.Width < blob.Height * 0.75;
                inkBlob.Slash = digit < 0 && (diagonal || thin);
                if (digit >= 0 || inkBlob.Slash) glyphs.Add(inkBlob);
            }
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            trace.Append("blobs ").Append(blobs.Count).Append(" glyphs ").Append(glyphs.Count).Append(" [");
            for (int t = 0; t < glyphs.Count; t++) trace.Append(glyphs[t].Slash ? "/" : glyphs[t].Digit.ToString());
            trace.Append("]").Append(rejected);
            LastTrace = trace.ToString();
            if (glyphs.Count == 0) return result;
            glyphs.Sort(delegate(InkBlob a, InkBlob b) { return a.Left.CompareTo(b.Left); });
            int lineTop = glyphs[0].Top;
            int lineHeight = glyphs[0].Height;
            for (int i = 1; i < glyphs.Count; i++)
            {
                if (glyphs[i].Height > lineHeight) lineHeight = glyphs[i].Height;
            }
            string current = "";
            string max = "";
            bool inMax = false;
            int previousRight = -1000;
            for (int i = 0; i < glyphs.Count; i++)
            {
                InkBlob glyph = glyphs[i];
                int overlap = Math.Min(glyph.Top + glyph.Height, lineTop + lineHeight) - Math.Max(glyph.Top, lineTop);
                if (overlap < Math.Min(glyph.Height, lineHeight) / 3) continue;
                int gap = glyph.Left - previousRight;
                // A comma is a short mark between digits. It stays inside the
                // same number, so 1,633 stays 1633 and can be larger than the max.
                if (glyph.Digit == -2)
                {
                    previousRight = glyph.Left + glyph.Width;
                    continue;
                }
                if (glyph.Slash || (current.Length > 0 && !inMax && gap > lineHeight / 2))
                {
                    inMax = true;
                    if (glyph.Slash)
                    {
                        previousRight = glyph.Left + glyph.Width;
                        continue;
                    }
                }
                else if (inMax && max.Length > 0 && gap > lineHeight) break;
                previousRight = glyph.Left + glyph.Width;
                if (glyph.Slash) continue;
                if (inMax) max += glyph.Digit.ToString();
                else current += glyph.Digit.ToString();
            }
            int value;
            if (!TryParse(current, out value)) return result;
            result.Ok = true;
            result.Current = value;
            int maxValue;
            if (TryParse(max, out maxValue))
            {
                result.Max = maxValue;
                result.HasMax = true;
            }
            return result;
        }

        private static bool TryParse(string text, out int value)
        {
            value = 0;
            if (text == null || text.Length == 0 || text.Length > 5) return false;
            return int.TryParse(text, out value);
        }

        private static bool IsDiagonal(bool[] mask, int stride, Blob blob)
        {
            int diag = 0;
            for (int y = blob.Top; y < blob.Top + blob.Height; y++)
                for (int x = blob.Left; x < blob.Left + blob.Width; x++)
                {
                    if (!mask[y * stride + x]) continue;
                    double nx = blob.Width <= 1 ? 0 : (x - blob.Left) / (double)(blob.Width - 1);
                    double ny = blob.Height <= 1 ? 0 : (y - blob.Top) / (double)(blob.Height - 1);
                    if (Math.Abs(nx - ny) < 0.34) diag++;
                }
            return blob.Area > 8 && diag > blob.Area * 0.5;
        }

        private static int[,] Sample(bool[] mask, int stride, Blob blob)
        {
            int[,] grid = new int[7, 5];
            for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    int x0 = blob.Left + gx * blob.Width / 5;
                    int x1 = blob.Left + (gx + 1) * blob.Width / 5;
                    int y0 = blob.Top + gy * blob.Height / 7;
                    int y1 = blob.Top + (gy + 1) * blob.Height / 7;
                    if (x1 <= x0) x1 = x0 + 1;
                    if (y1 <= y0) y1 = y0 + 1;
                    int on = 0, total = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            total++;
                            if (y >= 0 && x >= 0 && y * stride + x < mask.Length && mask[y * stride + x]) on++;
                        }
                    grid[gy, gx] = total > 0 && on * 2 >= total ? 1 : 0;
                }
            return grid;
        }

        private sealed class Blob
        {
            public int Left, Top, Right, Bottom, Area;
            public int Width { get { return Right - Left + 1; } }
            public int Height { get { return Bottom - Top + 1; } }
        }

        private static bool[] Mask(ImageBuffer image, IntRect region, InkTest ink)
        {
            bool[] mask = new bool[region.Width * region.Height];
            for (int y = 0; y < region.Height; y++)
                for (int x = 0; x < region.Width; x++)
                    mask[y * region.Width + x] = ink(image.Argb(region.X + x, region.Y + y));
            return mask;
        }

        private static List<Blob> Components(bool[] mask, int width, int height)
        {
            bool[] seen = new bool[mask.Length];
            List<Blob> blobs = new List<Blob>();
            int[] stack = new int[mask.Length];
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                Blob blob = new Blob();
                blob.Left = width;
                blob.Top = height;
                blob.Right = 0;
                blob.Bottom = 0;
                int count = 0;
                stack[count++] = start;
                seen[start] = true;
                int area = 0;
                while (count > 0)
                {
                    int index = stack[--count];
                    int x = index % width;
                    int y = index / width;
                    area++;
                    if (x < blob.Left) blob.Left = x;
                    if (y < blob.Top) blob.Top = y;
                    if (x > blob.Right) blob.Right = x;
                    if (y > blob.Bottom) blob.Bottom = y;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            int next = ny * width + nx;
                            if (!mask[next] || seen[next]) continue;
                            seen[next] = true;
                            stack[count++] = next;
                        }
                }
                blob.Area = area;
                blobs.Add(blob);
            }
            return blobs;
        }

        private static List<IntRect> FindBars(ImageBuffer image, IntRect region, InkTest ink)
        {
            List<IntRect> bars = new List<IntRect>();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            bool[,] on = new bool[region.Height, region.Width];
            for (int y = 0; y < region.Height; y++)
                for (int x = 0; x < region.Width; x++)
                    on[y, x] = ink(image.Argb(region.X + x, region.Y + y));
            bool[] used = new bool[region.Height * region.Width];
            for (int y = 0; y < region.Height; y++)
            {
                int x = 0;
                while (x < region.Width)
                {
                    if (!on[y, x]) { x++; continue; }
                    int start = x;
                    while (x < region.Width && on[y, x]) x++;
                    int run = x - start;
                    if (run < 36) continue;
                    int top = y;
                    int bottom = y;
                    while (bottom + 1 < region.Height)
                    {
                        int overlap = 0;
                        for (int xx = start; xx < x; xx++) if (on[bottom + 1, xx]) overlap++;
                        if (overlap < run / 2) break;
                        bottom++;
                    }
                    int height = bottom - top + 1;
                    if (height < 3 || height > 22) continue;
                    bool fresh = false;
                    for (int yy = top; yy <= bottom && !fresh; yy++)
                        for (int xx = start; xx < x; xx++)
                            if (!used[yy * region.Width + xx]) fresh = true;
                    if (!fresh) continue;
                    for (int yy = top; yy <= bottom; yy++)
                        for (int xx = start; xx < x; xx++)
                            used[yy * region.Width + xx] = true;
                    bars.Add(new IntRect(region.X + start, region.Y + top, run, height));
                }
            }
            return bars;
        }

        private static IntRect Pin(ImageBuffer image, double x, double y, double w, double h)
        {
            return Clamp(image, (int)Math.Round(x * image.Width), (int)Math.Round(y * image.Height), (int)Math.Round(w * image.Width), (int)Math.Round(h * image.Height));
        }

        private static IntRect Clamp(ImageBuffer image, int x, int y, int w, int h)
        {
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
            return new IntRect(x, y, w, h);
        }
    }
}
