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
            else reading.HpHow = ReadAuto(image, true, reading);

            if (settings.HasPrayerPin)
            {
                NumberRead number = ReadNumber(image, Pin(image, settings.PrayerPinX, settings.PrayerPinY, settings.PrayerPinW, settings.PrayerPinH), AnyInk);
                Apply(reading, false, number, "pin");
            }
            else ReadAuto(image, false, reading);
            return reading;
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
            if (!number.Ok || number.Current <= 0 || number.Current > 99999) return;
            if (number.HasMax && (number.Max <= 0 || number.Max > 99999 || number.Current > number.Max)) number.HasMax = false;
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
