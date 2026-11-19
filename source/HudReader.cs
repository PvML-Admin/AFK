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
        public int? Adrenaline;
        public int StripY;
        public string PrayerHow = "";
        public int? PotionSeconds;
        public int PotionCount;
        public string PotionHow = "";
        public bool BuffBarFound;
        public bool StripFound;
        public IntRect LifeText;
        public IntRect PrayerText;
        public List<BuffIcon> Buffs = new List<BuffIcon>();
        public List<IntRect> BuffRects = new List<IntRect>();
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
            int[] score = new int[10];
            for (int i = 0; i < 10; i++) score[i] = 99;
            for (int digit = 0; digit < Glyphs.Length; digit++)
            {
                int[,] canonical = Canonical(Glyphs[digit]);
                int distance = 0;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                        if (grid[y, x] != canonical[y, x]) distance++;
                if (distance < score[digit]) score[digit] = distance;
            }
            for (int i = 0; i < ExtraDigits.Length; i++)
            {
                int distance = 0;
                string pattern = ExtraPatterns[i];
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                        if (grid[y, x] != (pattern[y * 5 + x] == '1' ? 1 : 0)) distance++;
                int digit = ExtraDigits[i];
                if (distance < score[digit]) score[digit] = distance;
            }
            int best = 99;
            int second = 99;
            int value = -1;
            for (int digit = 0; digit < 10; digit++)
            {
                if (score[digit] < best)
                {
                    second = best;
                    best = score[digit];
                    value = digit;
                }
                else if (score[digit] < second) second = score[digit];
            }
            if (best > 8) return -1;
            if (second - best < 2 && best > 3) return -1;
            return value;
        }

        // Shapes sampled from the Kavros HUD font (outlined, about 8px). The
        // prototype glyphs above still match the self-test's drawn digits.
        private static readonly int[] ExtraDigits = { 1, 2, 3, 4, 5, 6, 9, 0, 0 };
        private static readonly string[] ExtraPatterns = {
            "00100111000010000100001000010011111",
            "01110000100001100010001000100011110",
            "01100000100001001110000100001111110",
            "00011001110010101001110011111100001",
            "11111110001100011110000010000111111",
            "00110010001000011110100011000111111",
            "01110100111000110001011110000111110",
            "01110110011000110001100011000111111",
            "00110011101110101001110011100100110"
        };

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
        public static void ReadBox(ImageBuffer box, bool life, HudReading reading)
        {
            if (box == null || reading == null) return;
            NumberRead number = ReadNumber(box, new IntRect(0, 0, box.Width, box.Height), WhiteInk);
            Apply(reading, life, number, "bar");
        }

        public static BuffIcon ReadBuffCrop(ImageBuffer icon, int x)
        {
            BuffIcon read = icon == null ? new BuffIcon() : ReadIconTime(icon, new IntRect(0, 0, icon.Width, icon.Height));
            read.X = x;
            return read;
        }

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

        // The strip can sit anywhere. Life is the red bar, prayer the purple
        // bar. Yellow adrenaline and green summoning are never life or prayer.
        private static void ReadStrip(ImageBuffer image, HudReading reading, bool wantLife, bool wantPrayer)
        {
            IntRect whole = new IntRect(0, 0, image.Width, image.Height);
            List<IntRect> reds = FindBars(image, whole, RedBar);
            List<IntRect> yellows = FindBars(image, whole, YellowBar);
            List<IntRect> purples = FindBars(image, whole, PurpleBar);
            List<IntRect> greens = FindBars(image, whole, SummonBar);
            IntRect red = new IntRect();
            IntRect yellow = new IntRect();
            IntRect purple = new IntRect();
            bool pair = false;
            int best = 0;
            for (int i = 0; i < reds.Count; i++)
                for (int j = 0; j < purples.Count; j++)
                {
                    if (purples[j].X <= reds[i].Right) continue;
                    if (Math.Abs(reds[i].Y - purples[j].Y) > 10) continue;
                    if (purples[j].X - reds[i].Right > Math.Max(reds[i].Width, 40) * 6) continue;
                    IntRect redSpan = SpanClose(reds, reds[i]);
                    IntRect purpleSpan = SpanClose(purples, purples[j]);
                    int score = 20;
                    IntRect between = BarBetween(yellows, redSpan.Right, purpleSpan.X, redSpan.Y);
                    if (between.Width > 0) score += 30;
                    IntRect after = BarBetween(greens, purpleSpan.Right - 4, purpleSpan.Right + purpleSpan.Width * 3, purpleSpan.Y);
                    if (after.Width > 0) score += 30;
                    int widthGap = Math.Abs(redSpan.Width - purpleSpan.Width);
                    if (widthGap < 24) score += 10;
                    if (!pair || score > best)
                    {
                        pair = true;
                        best = score;
                        red = redSpan;
                        purple = purpleSpan;
                        yellow = between;
                    }
                }
            if (!pair) return;
            reading.StripFound = true;
            reading.StripY = red.Y;
            IntRect lifeText = Above(image, red);
            IntRect prayerText = Above(image, purple);
            reading.LifeText = lifeText;
            reading.PrayerText = prayerText;
            if (wantLife)
            {
                NumberRead number = ReadNumber(image, lifeText, WhiteInk);
                Apply(reading, true, number, "bar");
            }
            if (wantPrayer)
            {
                NumberRead number = ReadNumber(image, prayerText, WhiteInk);
                Apply(reading, false, number, "bar");
            }
            if (yellow.Width > 0)
            {
                bool ignored;
                NumberRead adrenaline = ReadLine(image, Above(image, yellow), WhiteInk, out ignored);
                if (adrenaline.Ok && adrenaline.Current > 0 && adrenaline.Current <= 200) reading.Adrenaline = adrenaline.Current;
            }
        }

        private static IntRect BarBetween(List<IntRect> bars, int left, int right, int y)
        {
            IntRect found = new IntRect();
            for (int i = 0; i < bars.Count; i++)
            {
                if (Math.Abs(bars[i].Y - y) > 10) continue;
                if (bars[i].X < left - 8 || bars[i].Right > right + 8) continue;
                if (bars[i].Width > found.Width) found = bars[i];
            }
            return found;
        }

        private static IntRect SpanClose(List<IntRect> bars, IntRect seed)
        {
            int left = seed.X;
            int right = seed.Right;
            for (int i = 0; i < bars.Count; i++)
            {
                if (Math.Abs(bars[i].Y - seed.Y) > 4) continue;
                if (bars[i].X > right + 8 || bars[i].Right < left - 8) continue;
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
            reading.BuffRects = new List<IntRect>();
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
                reading.BuffRects.Add(icons[i]);
                if (icon.HasTime) reading.PotionCount++;
            }
        }

        private static bool BorderGreen(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 120 && r < 180 && b < 150 && g > r + 25 && g > b + 20;
        }

        private static List<IntRect> FindBuffIcons(ImageBuffer image, IntRect region)
        {
            List<IntRect> found = new List<IntRect>();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            for (int y = region.Y; y < region.Bottom; y++)
            {
                int x = region.X;
                while (x < region.Right)
                {
                    if (!BorderGreen(image.Argb(x, y))) { x += 2; continue; }
                    int start = x;
                    while (x < region.Right && x - start < 48 && BorderGreen(image.Argb(x, y))) x++;
                    int span = x - start;
                    if (span < 22 || span > 46) continue;
                    if (!IconAt(image, start, y, span)) continue;
                    bool overlap = false;
                    for (int i = 0; i < found.Count; i++)
                    {
                        IntRect old = found[i];
                        if (start < old.Right && start + span > old.X && y < old.Bottom && y + span > old.Y) overlap = true;
                    }
                    if (!overlap) found.Add(new IntRect(start, y, span, span));
                }
            }
            if (found.Count == 0) return found;
            int bestY = found[0].Y;
            int bestCount = 0;
            for (int i = 0; i < found.Count; i++)
            {
                int count = 0;
                for (int j = 0; j < found.Count; j++)
                    if (Math.Abs(found[j].Y - found[i].Y) <= 4) count++;
                if (count > bestCount) { bestCount = count; bestY = found[i].Y; }
            }
            List<IntRect> row = new List<IntRect>();
            for (int i = 0; i < found.Count; i++)
                if (Math.Abs(found[i].Y - bestY) <= 4) row.Add(found[i]);
            row.Sort(delegate(IntRect a, IntRect b) { return a.X.CompareTo(b.X); });
            return row;
        }

        private static bool IconAt(ImageBuffer image, int x, int y, int size)
        {
            int edge = 0, edgeTotal = 0;
            for (int i = 0; i < size; i++)
            {
                edgeTotal += 4;
                if (BorderGreen(image.Argb(x + i, y))) edge++;
                if (BorderGreen(image.Argb(x + i, y + size - 1))) edge++;
                if (BorderGreen(image.Argb(x, y + i))) edge++;
                if (BorderGreen(image.Argb(x + size - 1, y + i))) edge++;
            }
            int inset = Math.Max(3, size / 8);
            int inner = 0;
            int innerTotal = 0;
            for (int yy = y + inset; yy < y + size - inset; yy += 3)
                for (int xx = x + inset; xx < x + size - inset; xx += 3)
                {
                    innerTotal++;
                    if (BorderGreen(image.Argb(xx, yy))) inner++;
                }
            bool filled = innerTotal > 0 && inner * 100 / innerTotal >= 92;
            return edgeTotal > 0 && edge * 100 / edgeTotal >= 40 && !filled;
        }

        private static BuffIcon ReadIconTime(ImageBuffer image, IntRect icon)
        {
            BuffIcon result = new BuffIcon();
            result.X = icon.X;
            bool minutes;
            NumberRead number = ReadIconLine(image, icon, Math.Max(10, icon.Height / 2), out minutes);
            bool tightMinutes;
            NumberRead tight = ReadIconLine(image, icon, Math.Min(12, icon.Height), out tightMinutes);
            if (!number.Ok || (tight.Ok && (tightMinutes && !minutes || tight.Current.ToString().Length > number.Current.ToString().Length)))
            {
                number = tight;
                minutes = tightMinutes;
            }
            if (!number.Ok) return result;
            result.HasTime = true;
            result.Minutes = minutes;
            result.Value = number.Current;
            return result;
        }

        private static NumberRead ReadIconLine(ImageBuffer image, IntRect icon, int textH, out bool minutes)
        {
            IntRect text = Clamp(image, icon.X + 1, icon.Bottom - textH, Math.Max(8, icon.Width - 2), Math.Max(6, textH - 1));
            NumberRead number = ReadLine(image, text, BuffInk, out minutes);
            if (!number.Ok)
            {
                bool whiteMinutes;
                NumberRead white = ReadLine(image, text, WhiteInk, out whiteMinutes);
                if (white.Ok) { minutes = whiteMinutes; return white; }
            }
            return number;
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
            if (!number.Ok || number.Current <= 0) return;
            if (number.HasMax && number.Max <= 0) number.HasMax = false;
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
            return low >= 120 && high - low <= 80;
        }

        private static bool BuffInk(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            int low = Math.Min(r, Math.Min(g, b));
            int high = Math.Max(r, Math.Max(g, b));
            return low >= 200 && high - low <= 70;
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
            // Yellow adrenaline fails g < 150. Green summoning fails r >= 160.
            return r >= 160 && g < 150 && r > g + 50 && r > b + 40 && b < 170;
        }

        private static bool YellowBar(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return r >= 160 && g >= 90 && b < 90 && g > b + 40 && r > b + 40 && g + 80 > r;
        }

        private static bool PurpleBar(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return b >= 80 && r >= 30 && r < 200 && b > r && b > g + 15 && g < 170;
        }

        private static bool SummonBar(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 110 && r < 170 && g > r + 25 && g > b + 20 && b < 140;
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
            bool minutes;
            return ReadLine(image, region, ink, out minutes);
        }

        // Column groups, not connected blobs: the HUD font's 2, 3 and 9 break
        // into pieces, and the slash must stay a separator so 60/60 is not 6060.
        private static NumberRead ReadLine(ImageBuffer image, IntRect region, InkTest ink, out bool minutes)
        {
            minutes = false;
            NumberRead result = new NumberRead();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            if (region.Width < 6 || region.Height < 6) return result;
            bool[] mask = Mask(image, region, ink);
            int width = region.Width;
            int height = region.Height;
            bool[] column = new bool[width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (mask[y * width + x]) column[x] = true;
            string current = "";
            string max = "";
            bool inMax = false;
            int digitRight = -1;
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            trace.Append("cols");
            int cursor = 0;
            while (cursor < width)
            {
                if (!column[cursor]) { cursor++; continue; }
                int start = cursor;
                while (cursor < width && column[cursor]) cursor++;
                int top = height;
                int bottom = 0;
                int area = 0;
                for (int yy = 0; yy < height; yy++)
                    for (int xx = start; xx < cursor; xx++)
                        if (mask[yy * width + xx])
                        {
                            area++;
                            if (yy < top) top = yy;
                            if (yy > bottom) bottom = yy;
                        }
                int ww = cursor - start;
                int hh = bottom - top + 1;
                if (area < 3 || hh < 3) continue;
                if (hh <= 4 && ww <= 5 && area <= 14) { trace.Append(" ,"); continue; }
                Blob blob = new Blob();
                blob.Left = start;
                blob.Top = top;
                blob.Right = cursor - 1;
                blob.Bottom = bottom;
                blob.Area = area;
                int digit = -1;
                if (ww <= 16 && hh >= 5 && hh <= 24)
                    digit = DigitFont.Match(Sample(mask, width, blob));
                bool slash = digit < 0 && hh >= 6 && ww <= 8 && ww * 2 <= hh + 4 && IsDiagonal(mask, width, blob);
                if (digit >= 0)
                {
                    trace.Append(" ").Append(digit);
                    if (inMax) max += digit.ToString();
                    else current += digit.ToString();
                    digitRight = cursor;
                }
                else if (slash && current.Length > 0 && !inMax)
                {
                    trace.Append(" /");
                    inMax = true;
                }
                else if (digitRight >= 0 && start + 2 >= digitRight && ww >= 3 && hh >= 5)
                {
                    trace.Append(" m");
                    minutes = true;
                }
            }
            LastTrace = trace.ToString();
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
            if (text == null || text.Length == 0 || text.Length > 9) return false;
            return int.TryParse(text, out value);
        }

        private static bool IsDiagonal(bool[] mask, int stride, Blob blob)
        {
            int main = 0;
            int anti = 0;
            for (int y = blob.Top; y < blob.Top + blob.Height; y++)
                for (int x = blob.Left; x < blob.Left + blob.Width; x++)
                {
                    if (!mask[y * stride + x]) continue;
                    double nx = blob.Width <= 1 ? 0 : (x - blob.Left) / (double)(blob.Width - 1);
                    double ny = blob.Height <= 1 ? 0 : (y - blob.Top) / (double)(blob.Height - 1);
                    if (Math.Abs(nx - ny) < 0.42) main++;
                    if (Math.Abs(nx + ny - 1) < 0.42) anti++;
                }
            int diag = main > anti ? main : anti;
            return blob.Area >= 6 && diag * 2 > blob.Area;
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
