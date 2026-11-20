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
        public IntRect LifeBar;
        public IntRect PrayerBar;
        public string LifeTrace = "";
        public string PrayerTrace = "";
        public string BuffTrace = "";
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
        internal static readonly int[] ExtraDigits = { 1, 2, 3, 4, 5, 6, 9, 0, 0 };
        internal static readonly string[] ExtraPatterns = {
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
            NumberRead number = ReadResource(box, new IntRect(0, 0, box.Width, box.Height), life);
            Apply(reading, life, number, "bar");
        }

        public static BuffIcon ReadBuffCrop(ImageBuffer icon, int x)
        {
            BuffIcon read = icon == null ? new BuffIcon() : ReadIconTime(icon, new IntRect(0, 0, icon.Width, icon.Height));
            if (icon != null) read.Kind = ClassifyBuff(icon, new IntRect(0, 0, icon.Width, icon.Height));
            read.X = x;
            return read;
        }

        public static HudReading Read(ImageBuffer image, WardenSettings settings)
        {
            HudReading reading = new HudReading();
            if (settings.HasHpPin)
            {
                reading.LifeText = Pin(image, settings.HpPinX, settings.HpPinY, settings.HpPinW, settings.HpPinH);
                NumberRead number = ReadResource(image, reading.LifeText, true);
                Apply(reading, true, number, "pin");
            }
            if (settings.HasPrayerPin)
            {
                reading.PrayerText = Pin(image, settings.PrayerPinX, settings.PrayerPinY, settings.PrayerPinW, settings.PrayerPinH);
                NumberRead number = ReadResource(image, reading.PrayerText, false);
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
            if (!pair)
            {
                if (InferStrip(image, reading, reds, yellows, purples, greens, wantLife, wantPrayer)) return;
                if (wantLife) ReadLoneBar(image, reading, reds, true);
                if (wantPrayer) ReadLoneBar(image, reading, purples, false);
                return;
            }
            reading.StripFound = true;
            reading.StripY = red.Y;
            // The coloured rectangle is only the fill. It shrinks as the bar
            // empties. The number stays over the whole slot, whose pitch is the
            // distance from this fill's left edge to the next icon.
            int pitch = 0;
            int slotGap = Math.Max(24, image.Width / 80);
            if (yellow.Width > 0 && yellow.X > red.X + slotGap) pitch = yellow.X - red.X;
            else if (purple.X > red.X + slotGap) pitch = (purple.X - red.X) / 2;
            int minPitch = Math.Max(36, image.Width / 70);
            if (pitch < minPitch) pitch = Math.Max(Math.Max(red.Width, purple.Width), minPitch) + Math.Max(24, minPitch / 2);
            int inset = Math.Max(8, pitch / 10);
            int textW = Math.Max(pitch - inset, Math.Max(red.Width, purple.Width) + inset);
            int textH = Math.Max(26, Math.Max(red.Height, purple.Height) * 6);
            int textPad = Math.Max(2, textH / 12);
            IntRect lifeText = Clamp(image, red.X - textPad, red.Y - textH, textW, textH);
            IntRect prayerText = Clamp(image, purple.X - textPad, purple.Y - textH, textW, textH);
            reading.LifeBar = red;
            reading.PrayerBar = purple;
            reading.LifeText = lifeText;
            reading.PrayerText = prayerText;
            if (wantLife)
            {
                NumberRead number = ReadResource(image, lifeText, true);
                reading.LifeTrace = LastTrace;
                Apply(reading, true, number, "bar");
            }
            if (wantPrayer)
            {
                NumberRead number = ReadResource(image, prayerText, false);
                reading.PrayerTrace = LastTrace;
                Apply(reading, false, number, "bar");
            }
            if (yellow.Width > 0)
            {
                bool ignored;
                NumberRead adrenaline = ReadLine(image, Above(image, yellow), WhiteInk, out ignored);
                if (adrenaline.Ok && adrenaline.Current > 0 && adrenaline.Current <= 200) reading.Adrenaline = adrenaline.Current;
            }
        }

        // A fill shorter than a findable bar (life or prayer under about a
        // third) or an empty one breaks the red/purple pair. The four HUD
        // slots (life, adrenaline, prayer, summoning) share one pitch, so the
        // missing slot is placed from any two other fills on the same row. A
        // placed slot is kept only when a current/max number is read above it.
        private static bool InferStrip(ImageBuffer image, HudReading reading, List<IntRect> reds, List<IntRect> yellows, List<IntRect> purples, List<IntRect> greens, bool wantLife, bool wantPrayer)
        {
            List<IntRect>[] sets = new List<IntRect>[] { reds, yellows, purples, greens };
            int attempts = 0;
            for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    for (int i = 0; i < sets[a].Count; i++)
                    {
                        IntRect left = sets[a][i];
                        int maxBar = Math.Max(9, image.Height / 80);
                        if (left.Height > maxBar) continue;
                        for (int j = 0; j < sets[b].Count; j++)
                        {
                            IntRect right = sets[b][j];
                            if (right.Height > maxBar) continue;
                            if (Math.Abs(left.Y - right.Y) > Math.Max(4, maxBar / 2) || Math.Abs(left.Height - right.Height) > Math.Max(3, maxBar / 3)) continue;
                            int steps = b - a;
                            int dx = right.X - left.X;
                            int pitch = (int)Math.Round(dx / (double)steps);
                            int minPitch = Math.Max(36, image.Width / 70);
                            int maxPitch = Math.Max(320, image.Width / 5);
                            if (pitch < minPitch || pitch > maxPitch) continue;
                            if (left.Width > pitch || right.Width > pitch) continue;
                            if (++attempts > 8) return false;
                            if (ReadInferred(image, reading, reds, purples, left.X - a * pitch, left.Y, Math.Max(left.Height, right.Height), pitch, wantLife, wantPrayer)) return true;
                        }
                    }
            return false;
        }

        private static bool ReadInferred(ImageBuffer image, HudReading reading, List<IntRect> reds, List<IntRect> purples, int lifeX, int y, int barHeight, int pitch, bool wantLife, bool wantPrayer)
        {
            // The pitch between fills varies by a few pixels per slot, so the box
            // starts a little left of the predicted fill and the reader skips the
            // slot icon the same way it does for the paired bars.
            int margin = Math.Max(6, pitch / 12);
            int textW = pitch - Math.Max(8, pitch / 10) + margin;
            int textH = Math.Max(26, barHeight * 6);
            int textPad = Math.Max(2, textH / 12);
            int prayerX = lifeX + 2 * pitch;
            // A fill that is still there is the exact slot edge; use it.
            int lifeEdge = FillNear(reds, lifeX, y, pitch);
            int prayerEdge = FillNear(purples, prayerX, y, pitch);
            IntRect lifeText = lifeEdge >= 0
                ? Clamp(image, lifeEdge - textPad, y - textH, textW, textH)
                : Clamp(image, lifeX - textPad - margin, y - textH, textW, textH);
            IntRect prayerText = prayerEdge >= 0
                ? Clamp(image, prayerEdge - textPad, y - textH, textW, textH)
                : Clamp(image, prayerX - textPad - margin, y - textH, textW, textH);
            bool any = false;
            if (wantLife && lifeText.Width > 24)
            {
                NumberRead number = ReadResource(image, lifeText, true);
                if (number.Ok && number.HasMax && ReadGate.Plausible(number.Current, number.Max))
                {
                    reading.LifeText = lifeText;
                    reading.LifeBar = new IntRect(lifeEdge >= 0 ? lifeEdge : lifeX, y, Math.Max(1, pitch - 36), barHeight);
                    reading.LifeTrace = "slot " + LastTrace;
                    Apply(reading, true, number, "slot");
                    any = true;
                }
            }
            if (wantPrayer && prayerText.Width > 24)
            {
                NumberRead number = ReadResource(image, prayerText, false);
                if (number.Ok && number.HasMax && ReadGate.Plausible(number.Current, number.Max))
                {
                    reading.PrayerText = prayerText;
                    reading.PrayerBar = new IntRect(prayerEdge >= 0 ? prayerEdge : prayerX, y, Math.Max(1, pitch - 36), barHeight);
                    reading.PrayerTrace = "slot " + LastTrace;
                    Apply(reading, false, number, "slot");
                    any = true;
                }
            }
            if (!any) return false;
            reading.StripFound = true;
            reading.StripY = y;
            return true;
        }

        private static int FillNear(List<IntRect> bars, int x, int y, int pitch)
        {
            for (int i = 0; i < bars.Count; i++)
                if (bars[i].Height <= 9 && Math.Abs(bars[i].Y - y) <= 4 && Math.Abs(bars[i].X - x) <= pitch / 4) return bars[i].X;
            return -1;
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

        private static void ReadLoneBar(ImageBuffer image, HudReading reading, List<IntRect> bars, bool life)
        {
            IntRect best = new IntRect();
            for (int i = 0; i < bars.Count; i++)
                if (bars[i].Width > best.Width) best = bars[i];
            if (best.Width < 24) return;
            int textH = Math.Max(26, best.Height * 6);
            int textW = Math.Max(best.Width + 48, 110);
            IntRect text = Clamp(image, best.X - 10, best.Y - textH, textW, textH);
            NumberRead number = ReadResource(image, text, life);
            if (life) reading.LifeTrace = LastTrace;
            else reading.PrayerTrace = LastTrace;
            if (!number.Ok || !number.HasMax) return;
            if (life) { reading.LifeBar = best; reading.LifeText = text; reading.LifeTrace = LastTrace; }
            else { reading.PrayerBar = best; reading.PrayerText = text; reading.PrayerTrace = LastTrace; reading.StripFound = true; reading.StripY = best.Y; }
            Apply(reading, life, number, "bar");
        }

        private static IntRect Above(ImageBuffer image, IntRect bar)
        {
            int height = Math.Max(22, bar.Height * 5);
            return Clamp(image, bar.X - 4, bar.Y - height, Math.Max(bar.Width + 8, 72), height);
        }

        // The heart icon sits just left of the life number. If that icon breaks
        // the read, try the same line again a few pixels further right.
        private static NumberRead ReadResource(ImageBuffer image, IntRect box, bool life)
        {
            // Digits sit immediately above the fill. A bright scene above the HUD
            // otherwise wins the row-ink vote over the actual numeric line.
            // The band grows with the slot so a larger interface is not clipped at 17px.
            int height = Math.Min(box.Height, Math.Max(17, box.Height * 2 / 3));
            IntRect band = new IntRect(box.X, box.Bottom - height, box.Width, height);
            NumberRead sample = ReadNumber(image, band, WhiteInk);
            if (sample.HasMax) return sample;
            sample = life ? ReadLifeNumber(image, box) : ReadNumber(image, box, WhiteInk);
            if (sample.HasMax) return sample;
            return ReadNumber(image, box, life ? (InkTest)LifeDigit : PrayerDigit);
        }

        private static NumberRead ReadLifeNumber(ImageBuffer image, IntRect box)
        {
            NumberRead best = ReadNumber(image, box, WhiteInk);
            string bestTrace = LastTrace;
            if (best.Ok && best.HasMax) return best;
            for (int inset = 6; inset <= 28; inset += 6)
            {
                if (box.Width - inset < 24) break;
                IntRect cut = Clamp(image, box.X + inset, box.Y, box.Width - inset, box.Height);
                NumberRead alt = ReadNumber(image, cut, WhiteInk);
                string trace = LastTrace;
                if (alt.Ok && alt.HasMax && (!best.HasMax || alt.Current > 0))
                {
                    best = alt;
                    bestTrace = trace;
                    break;
                }
            }
            LastTrace = bestTrace;
            return best;
        }

        internal static void ReadBuffBar(ImageBuffer image, WardenSettings settings, HudReading reading)
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
            System.Text.StringBuilder buffTrace = new System.Text.StringBuilder();
            for (int i = 0; i < icons.Count; i++)
            {
                BuffIcon icon = ReadIconTime(image, icons[i]);
                icon.Kind = ClassifyBuff(image, icons[i]);
                reading.Buffs.Add(icon);
                reading.BuffRects.Add(icons[i]);
                if (icon.HasTime && icon.Kind == "potion") reading.PotionCount++;
                if (buffTrace.Length > 0) buffTrace.Append(" ");
                buffTrace.Append(icons[i].X).Append(":").Append(icon.Kind).Append(":").Append(LastTrace);
            }
            reading.BuffTrace = buffTrace.ToString();
        }

        private static bool BorderGreen(int c)
        {
            int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
            return g >= 80 && r < 220 && b < 190 && g > r + 12 && g > b + 12;
        }

        private static List<IntRect> FindBuffIcons(ImageBuffer image, IntRect region)
        {
            List<IntRect> found = new List<IntRect>();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            for (int y = region.Y; y < region.Bottom; y++)
            {
                int x = region.X + 11;
                while (x < region.Right)
                {
                    if (!BorderGreen(image.Argb(x, y))) { x += 12; continue; }
                    int start = x;
                    while (start > region.X && x - start < 99 && BorderGreen(image.Argb(start-1,y))) start--;
                    while (x < region.Right && x - start < 100 && BorderGreen(image.Argb(x, y))) x++;
                    int span = x - start;
                    int minSpan = Math.Max(8, image.Width / 320);
                    int maxSpan = Math.Max(120, image.Width / 12);
                    if (span < minSpan || span > maxSpan) continue;
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
            // A buff row uses square icons with the same size and pitch. Scenery
            // can produce hundreds of green edges at roughly the same height.
            found.Sort(delegate(IntRect a, IntRect b) { return a.X.CompareTo(b.X); });
            var best = new List<IntRect>();
            int bestScore = -1;
            foreach (IntRect first in found)
            {
                var row = new List<IntRect> { first };
                IntRect last = first;
                foreach (IntRect cand in found)
                {
                    int gap = cand.X - last.Right;
                    int gapLimit = Math.Max(9, first.Width / 3);
                    if (gap < -1 || gap > gapLimit || Math.Abs(cand.Y - first.Y) > Math.Max(3, first.Height / 6) || Math.Abs(cand.Width - first.Width) > Math.Max(3, first.Width / 6)) continue;
                    row.Add(cand); last=cand;
                }
                int score = row.Count * 3;
                if (row.Count >= 2 || found.Count == 1)
                {
                    // Read only the candidate icons, never the whole scenery row.
                    foreach (IntRect icon in row)
                    {
                        BuffIcon clock = ReadIconTime(image, icon);
                        if (clock.HasTime && ClassifyBuff(image, icon) == "potion") score += 12;
                    }
                }
                if (score > bestScore) { bestScore = score; best = row; }
            }
            return best;
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

        // Potion buffs are flasks, overload crystals, or a ringed antifire/poison
        // icon. A purple prayer icon and a stack picture are not potions.
        // Duration is not used: a potion can read 6m, 11m, or 5 seconds.
        internal static string ClassifyBuff(ImageBuffer image, IntRect icon)
        {
            if (image == null || icon.Width < 8 || icon.Height < 8) return "other";
            int bodyBottom = icon.Y + (icon.Height * 62) / 100;
            int inset = Math.Max(2, icon.Width / 8);
            int purple = 0, blue = 0, green = 0, samples = 0;
            int redEdge = 0, redCore = 0, edgeN = 0, coreN = 0;
            int topInk = 0, midInk = 0, topN = 0, midN = 0;
            int midY0 = icon.Y + icon.Height / 5;
            int midY1 = icon.Y + (icon.Height * 46) / 100;
            int coreR = Math.Max(3, icon.Width / 5);
            int cx = icon.X + icon.Width / 2;
            int cy = icon.Y + icon.Height / 3;
            for (int y = icon.Y + 2; y < bodyBottom; y++)
            {
                for (int x = icon.X + inset; x < icon.Right - inset; x++)
                {
                    int color = image.Argb(x, y);
                    int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
                    if (g >= 80 && g > r + 12 && g > b + 12) continue;
                    samples++;
                    bool isPurple = r > 70 && b > 80 && b + 15 >= r && g + 30 < r && g + 20 < b;
                    bool isBlue = b > 80 && b > r + 20 && b > g + 8;
                    bool isRed = r > 150 && r > g + 40 && r > b + 40;
                    bool isGreen = g > 80 && g > r + 20 && g > b + 20;
                    if (isPurple) purple++;
                    else if (isBlue) blue++;
                    if (isGreen) green++;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= coreR * coreR) { coreN++; if (isRed) redCore++; }
                    else { edgeN++; if (isRed) redEdge++; }
                    bool ink = r + g + b > 150;
                    if (y < midY0) { topN++; if (ink) topInk++; }
                    else if (y < midY1) { midN++; if (ink) midInk++; }
                }
            }
            if (samples < 8) return "other";
            if (purple * 100 / samples >= 28 && purple > blue) return "prayer";
            if (blue * 100 / samples >= 10 && blue > purple) return "potion";
            int magenta = 0, cork = 0, dark = 0;
            int topCut = icon.Y + icon.Height * 2 / 5;
            for (int y = icon.Y + 2; y < icon.Bottom - 2; y++)
            {
                for (int x = icon.X + inset; x < icon.Right - inset; x++)
                {
                    int color = image.Argb(x, y);
                    int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
                    if (g >= 80 && g > r + 12 && g > b + 12) continue;
                    if (r > 100 && b > 60 && r > g + 15 && b > g) magenta++;
                    if (y < topCut && r > 80 && g > 40 && b < 100 && r > b + 20 && g > b) cork++;
                    if (r + g + b < 90) dark++;
                }
            }
            if (magenta >= 24) return "potion";
            if (dark >= 80 && cork >= 8 && cork <= 45) return "potion";
            bool ring = edgeN > 0 && redEdge * 100 / edgeN >= 25 && (coreN == 0 || redEdge > redCore);
            if (ring) return "potion";
            return "other";
        }

        private static NumberRead ReadIconLine(ImageBuffer image, IntRect icon, int textH, out bool minutes)
        {
            IntRect text = Clamp(image, icon.X + 1, icon.Bottom - textH, Math.Max(8, icon.Width - 2), Math.Max(6, textH - 1));
            NumberRead number = ReadLine(image, text, BuffInk, out minutes, 4);
            if (!number.Ok)
            {
                NumberRead bright = ReadLine(image, text, BrightBuffInk, out minutes, 4);
                if (bright.Ok) return bright;
                bool whiteMinutes;
                NumberRead white = ReadLine(image, text, WhiteInk, out whiteMinutes, 4);
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
            if (!number.Ok || number.Current < 0 || (number.Current == 0 && !number.HasMax)) return;
            if (number.HasMax && number.Max <= 0) number.HasMax = false;
            // Above the cap is fine (boosts), at more than double it is a misread.
            if (number.HasMax && !ReadGate.Plausible(number.Current, number.Max)) return;
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

        private static bool BrightBuffInk(int c)
        {
            int r=ImageBuffer.R(c),g=ImageBuffer.G(c),b=ImageBuffer.B(c);
            int min=Math.Min(r,Math.Min(g,b));
            return min>=205 && Math.Max(r,Math.Max(g,b))-min<=20;
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

        internal static string LastTrace = "";

        private static NumberRead ReadNumber(ImageBuffer image, IntRect region, InkTest ink)
        {
            bool minutes;
            return ReadLine(image, region, ink, out minutes, 5, false);
        }

        // Column groups, not connected blobs: the HUD font's 2, 3 and 9 break
        // into pieces, and the slash must stay a separator so 60/60 is not 6060.
        private static NumberRead ReadLine(ImageBuffer image, IntRect region, InkTest ink, out bool minutes)
        {
            return ReadLine(image, region, ink, out minutes, 5, false);
        }

        private static NumberRead ReadLine(ImageBuffer image, IntRect region, InkTest ink, out bool minutes, int minDigit)
        {
            return ReadLine(image, region, ink, out minutes, minDigit, true);
        }

        private static NumberRead ReadLine(ImageBuffer image, IntRect region, InkTest ink, out bool minutes, int minDigit, bool allowLetter)
        {
            minutes = false;
            NumberRead result = new NumberRead();
            region = Clamp(image, region.X, region.Y, region.Width, region.Height);
            if (region.Width < 6 || region.Height < 6) return result;
            bool[] mask = Mask(image, region, ink);
            int width = region.Width;
            int height = region.Height;
            // Keep the digit line only. Pixels above it (icon glow) otherwise
            // bridge neighbouring digits such as the 3 and the 6 in 936.
            int[] rowInk = new int[height];
            for (int row = 0; row < height; row++)
                for (int x = 0; x < width; x++)
                    if (mask[row * width + x]) rowInk[row]++;
            int bestTop = 0, bestBottom = height - 1, bestSum = -1;
            int rowCursor = 0;
            while (rowCursor < height)
            {
                if (rowInk[rowCursor] < 2) { rowCursor++; continue; }
                int top = rowCursor;
                int sum = 0;
                while (rowCursor < height && (rowInk[rowCursor] >= 2 || (rowCursor + 1 < height && rowInk[rowCursor + 1] >= 2)))
                {
                    sum += rowInk[rowCursor];
                    rowCursor++;
                }
                if (sum > bestSum) { bestSum = sum; bestTop = top; bestBottom = rowCursor - 1; }
            }
            for (int yy = 0; yy < height; yy++)
            {
                if (yy >= bestTop && yy <= bestBottom) continue;
                for (int x = 0; x < width; x++) mask[yy * width + x] = false;
            }
            int typical = TypicalHeight(mask, width, height, minDigit);
            bool[] column = new bool[width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (mask[y * width + x]) column[x] = true;
            string current = "";
            string max = "";
            bool inMax = false;
            bool unknown = false;
            int digitRight = -1;
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            trace.Append("cols");
            int cursor = 0;
            while (cursor < width)
            {
                if (!column[cursor]) { cursor++; continue; }
                int start = cursor;
                while (cursor < width && column[cursor]) cursor++;
                int end = cursor;
                if (typical > 0 && end - start > typical)
                {
                    int edge = Math.Max(2, typical / 4);
                    int seam = -1;
                    int seamInk = int.MaxValue;
                    for (int x = start + edge; x < end - edge; x++)
                    {
                        int inkCount = 0;
                        for (int y = 0; y < height; y++) if (mask[y * width + x]) inkCount++;
                        if (inkCount < seamInk) { seamInk = inkCount; seam = x; }
                    }
                    if (seam > start && seamInk * 4 <= typical)
                    {
                        end = seam;
                        cursor = seam;
                    }
                }
                int top = height;
                int bottom = 0;
                int area = 0;
                for (int yy = 0; yy < height; yy++)
                    for (int xx = start; xx < end; xx++)
                        if (mask[yy * width + xx])
                        {
                            area++;
                            if (yy < top) top = yy;
                            if (yy > bottom) bottom = yy;
                        }
                int ww = cursor - start;
                int hh = bottom - top + 1;
                if (area < 3 || hh < 3) continue;
                int commaHeight = Math.Max(3, typical / 3);
                if (hh <= commaHeight && ww <= commaHeight + 1 && area <= commaHeight * commaHeight + 4) { trace.Append(" ,"); continue; }
                Blob blob = new Blob();
                blob.Left = start;
                blob.Top = top;
                blob.Right = cursor - 1;
                blob.Bottom = bottom;
                blob.Area = area;
                if (inMax && max.Length > 0 && digitRight > 0 && start - digitRight > Math.Max(14, hh * 2)) break;
                int digit = -1;
                int digitMin = Math.Max(4, typical * 3 / 5);
                int digitMaxH = typical + typical / 2;
                int digitMaxW = Math.Max(typical, typical + typical / 2);
                if (ww <= digitMaxW && hh >= digitMin && hh <= digitMaxH)
                {
                    digit = DigitFont.Match(Sample(mask, width, blob));
                    if (digit < 0 && !allowLetter) digit = MatchScaled(mask, width, blob);
                }
                bool slash = !allowLetter && digit < 0 && hh >= digitMin && ww <= hh && ww * 2 <= hh + 4 && IsDiagonal(mask, width, blob);
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
                    digitRight = cursor;
                }
                else if (allowLetter && digitRight >= 0 && start + 2 >= digitRight && ww >= 4 && hh >= 5)
                {
                    // "m" has a row of three separate stems. "s" is one shape, so 45s stays 45 seconds.
                    bool minuteMark = MaxHorizontalRuns(mask, width, blob) >= 3;
                    trace.Append(minuteMark ? " m" : " s");
                    if (minuteMark) minutes = true;
                }
                else if (hh >= digitMin && ww >= 2)
                {
                    trace.Append(" ?");
                    unknown = true;
                }
            }
            LastTrace = trace.ToString();
            if (unknown) return result;
            int value;
            if (!TryParse(current, out value)) return result;
            result.Ok = true;
            result.Current = value;
            int maxValue;
            if (TryParse(max, out maxValue) && maxValue > 0)
            {
                result.Max = maxValue;
                result.HasMax = true;
            }
            return result;
        }

        // Median height of digit-like columns, so a 1440p or 4K interface and a
        // smaller one share the same templates after the glyph is normalized.
        private static int TypicalHeight(bool[] mask, int width, int height, int minDigit)
        {
            List<int> heights = new List<int>();
            bool[] column = new bool[width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (mask[y * width + x]) column[x] = true;
            int cursor = 0;
            while (cursor < width)
            {
                if (!column[cursor]) { cursor++; continue; }
                int start = cursor;
                while (cursor < width && column[cursor]) cursor++;
                int top = height, bottom = 0, area = 0;
                for (int y = 0; y < height; y++)
                    for (int x = start; x < cursor; x++)
                        if (mask[y * width + x])
                        {
                            area++;
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                        }
                int ww = cursor - start;
                int hh = bottom - top + 1;
                if (area >= 4 && hh >= Math.Max(4, minDigit) && ww <= hh * 2) heights.Add(hh);
            }
            if (heights.Count == 0) return Math.Max(8, minDigit);
            heights.Sort();
            return heights[heights.Count / 2];
        }

        private static bool TryParse(string text, out int value)
        {
            value = 0;
            if (text == null || text.Length == 0 || text.Length > 9) return false;
            return int.TryParse(text, out value);
        }

        private static int MaxHorizontalRuns(bool[] mask, int stride, Blob blob)
        {
            int best = 0;
            for (int y = blob.Top; y <= blob.Bottom; y++)
            {
                int runs = 0;
                bool on = false;
                for (int x = blob.Left; x <= blob.Right; x++)
                {
                    bool ink = mask[y * stride + x];
                    if (ink && !on) runs++;
                    on = ink;
                }
                if (runs > best) best = runs;
            }
            return best;
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

        // Compare the blob with each prototype drawn at the blob's own size.
        // Squashing a 1440p or 4K glyph into 5x7 aliases the 4 and the 9.
        private static int MatchScaled(bool[] mask, int stride, Blob blob)
        {
            if (blob.Width < 2 || blob.Height < 4) return -1;
            int best = -1;
            int bestErr = int.MaxValue;
            int second = int.MaxValue;
            for (int digit = 0; digit < DigitFont.Glyphs.Length; digit++)
                ScorePattern(mask, stride, blob, DigitFont.Glyphs[digit], digit, ref best, ref bestErr, ref second);
            for (int i = 0; i < DigitFont.ExtraPatterns.Length; i++)
                ScorePattern(mask, stride, blob, DigitFont.ExtraPatterns[i], DigitFont.ExtraDigits[i], ref best, ref bestErr, ref second);
            int total = blob.Width * blob.Height;
            if (best < 0 || bestErr * 5 > total) return -1;
            if (second < int.MaxValue && second - bestErr < Math.Max(2, total / 25) && bestErr * 8 > total) return -1;
            return best;
        }

        private static void ScorePattern(bool[] mask, int stride, Blob blob, string pattern, int digit, ref int best, ref int bestErr, ref int second)
        {
            int err = 0;
            for (int y = 0; y < blob.Height; y++)
            {
                int sy = Math.Min(6, y * 7 / blob.Height);
                for (int x = 0; x < blob.Width; x++)
                {
                    int sx = Math.Min(4, x * 5 / blob.Width);
                    bool on = pattern[sy * 5 + sx] == '1';
                    bool ink = mask[(blob.Top + y) * stride + blob.Left + x];
                    if (on != ink) err++;
                }
            }
            if (err < bestErr)
            {
                second = bestErr;
                bestErr = err;
                best = digit;
            }
            else if (err < second) second = err;
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
            int minRun = Math.Max(36, region.Width / 80);
            int maxH = Math.Max(22, region.Height / 40);
            for (int y = region.Y; y < region.Bottom; y++)
            {
                int probe = region.X + minRun - 1;
                while (probe < region.Right)
                {
                    if (!ink(image.Argb(probe,y))) { probe += minRun; continue; }
                    int start=probe, end=probe+1;
                    while (start>region.X && ink(image.Argb(start-1,y))) start--;
                    while (end<region.Right && ink(image.Argb(end,y))) end++;
                    int run=end-start;
                    probe=end+minRun;
                    if (run<minRun) continue;
                    int bottom=y;
                    while (bottom+1<region.Bottom && bottom-y<maxH)
                    {
                        int overlap=0;
                        for (int xx=start; xx<end; xx++) if (ink(image.Argb(xx,bottom+1))) overlap++;
                        if (overlap<run/2) break;
                        bottom++;
                    }
                    int h=bottom-y+1;
                    if(h<3 || h>maxH)continue;
                    bool covered=false;
                    for(int i=bars.Count-1;i>=0;i--)
                    {
                        IntRect prior=bars[i];
                        if(prior.Y<y-maxH)break;
                        if(prior.Bottom>y && prior.X<=start && prior.Right>=end){covered=true;break;}
                    }
                    if(!covered)bars.Add(new IntRect(start,y,run,h));
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
