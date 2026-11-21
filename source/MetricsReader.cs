using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    // The RuneMetrics XP column. The first row, beside the little bar chart,
    // is the total. Digits are cut from Danni's own client.
    public static class MetricsReader
    {
        public struct Result
        {
            public bool Found;
            public long Xp;
            public int X;
            public int Y;
            public int W;
            public int H;
            public string Note;
        }

        private sealed class Glyph
        {
            public char Name;
            public int Width;
            public string[] Rows;
        }

        private static readonly Glyph[] Digits = new Glyph[]
        {
            G('0', 5, ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            G('1', 4, ".#..", ".#..", ".#..", ".#..", ".#..", ".#..", ".#..", "####"),
            G('2', 4, ".###", "...#", "...#", "...#", "..#.", ".#..", "##..", "####"),
            G('3', 4, ".##.", "...#", "...#", ".###", "...#", "....", "...#", "####"),
            G('4', 6, "...##.", "..###.", "..#.#.", ".#..#.", "##..#.", "######", "....#.", "....#."),
            G('5', 4, "####", "#...", "#...", "###.", "...#", "...#", "...#", "###."),
            G('6', 5, "..##.", ".#...", "#....", "#.##.", "#...#", "#...#", "#...#", ".###."),
            G('7', 5, "#####", "....#", "...#.", "...#.", "..#..", "..#..", "..#..", ".#..."),
            G('8', 5, ".###.", "#...#", "#..##", ".###.", "##.#.", "#...#", "#...#", ".###."),
            G('9', 5, ".###.", "#...#", "#...#", "#...#", ".####", "....#", "...#.", "####.")
        };

        public static Result Read(ImageBuffer image)
        {
            Result result = new Result();
            result.Note = "not found";
            if (image == null) { result.Note = "no image"; return result; }
            result = ReadScale(image);
            if (result.Found) return result;
            ImageBuffer mid = Downscale(image, 150);
            if (mid != null)
            {
                Result scaled = ReadScale(mid);
                if (scaled.Found) return scaled;
            }
            ImageBuffer large = Downscale(image, 200);
            if (large != null)
            {
                Result scaled = ReadScale(large);
                if (scaled.Found) return scaled;
            }
            // A small interface draws the bars and digits under the template size.
            if (image.Width < 1700)
            {
                ImageBuffer up = image.Scale(2);
                if (up != null)
                {
                    Result grown = ReadScale(up);
                    if (grown.Found) return grown;
                }
            }
            return result;
        }

        private static Result ReadScale(ImageBuffer image)
        {
            Result result = new Result();
            result.Note = "no XP row";
            bool sawChart = false;
            int step = image.Width > 1600 ? 3 : 1;
            for (int y = 0; y < image.Height - 20; y += step)
            {
                for (int x = 0; x < image.Width - 30; x += step)
                {
                    if (!Chart(image, x, y)) continue;
                    sawChart = true;
                    long xp;
                    if (TryRow(image, x + 20, y - 4, 240, 24, out xp))
                    {
                        result.Found = true;
                        result.Xp = xp;
                        result.Note = "xp=" + xp.ToString();
                        result.X = Math.Max(0, x - 8);
                        result.Y = Math.Max(0, y - 8);
                        result.W = 260;
                        result.H = 36;
                        return result;
                    }
                }
            }
            result.Note = sawChart ? "chart, digits unread" : "no XP row";
            return result;
        }

        // Inverse of ImageBuffer's nearest enlargement.
        private static ImageBuffer Downscale(ImageBuffer image, int percent)
        {
            if (image == null || percent < 110) return null;
            int width = Math.Max(1, (int)Math.Round(image.Width * 100.0 / percent));
            int height = Math.Max(1, (int)Math.Round(image.Height * 100.0 / percent));
            if (width >= image.Width || height >= image.Height) return null;
            int[] pixels = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy = (int)Math.Round((y + 0.5) * percent / 100.0 - 0.5);
                if (sy < 0) sy = 0;
                if (sy >= image.Height) sy = image.Height - 1;
                for (int x = 0; x < width; x++)
                {
                    int sx = (int)Math.Round((x + 0.5) * percent / 100.0 - 0.5);
                    if (sx < 0) sx = 0;
                    if (sx >= image.Width) sx = image.Width - 1;
                    pixels[y * width + x] = image.Argb(sx, sy);
                }
            }
            return new ImageBuffer(width, height, pixels);
        }

        // The total row's icon is three bars: red, then green, then blue.
        // A compass has the same colours in a ring, so the order matters.
        // The probe is wider than 16px because at 1440p the icon is about 24px.
        private static bool Chart(ImageBuffer image, int x, int y)
        {
            int width = 28;
            int height = 18;
            if (x + width >= image.Width || y + height >= image.Height) return false;
            int red = 0, green = 0, blue = 0;
            int redX = 0, greenX = 0, blueX = 0;
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int c = image.Argb(x + dx, y + dy);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    if (g > 90 && g > r + 25 && g > b) { green++; greenX += x + dx; }
                    else if (r > 90 && r > g + 25 && b < 150) { red++; redX += x + dx; }
                    else if (b > 90 && b > r + 12 && b > g) { blue++; blueX += x + dx; }
                }
            }
            if (red < 4 || green < 4 || blue < 4) return false;
            int rx = redX / red;
            int gx = greenX / green;
            int bx = blueX / blue;
            return rx + 1 < gx && gx + 1 < bx && bx - rx >= 4 && bx - rx <= 24;
        }

        private static bool TryRow(ImageBuffer image, int x, int y, int w, int h, out long xp)
        {
            xp = 0;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= image.Width || y >= image.Height) return false;
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 20 || h < 8) return false;
            bool[] ink = new bool[w * h];
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    ink[yy * w + xx] = Bright(image.Argb(x + xx, y + yy));
            int top = 0;
            int best = -1;
            for (int yy = 0; yy + 7 < h; yy++)
            {
                int n = 0;
                for (int k = 0; k < 8; k++)
                    for (int xx = 0; xx < w; xx++)
                        if (ink[(yy + k) * w + xx]) n++;
                if (n > best) { best = n; top = yy; }
            }
            if (best < 12) return false;
            string text = ReadDigits(ink, w, h, top);
            if (!IsFullTotal(text)) return false;
            return Parse(text, out xp);
        }

        // A full total is digits and thousands commas. 1.6M and 250K are not.
        public static bool IsFullTotal(string text)
        {
            if (text == null || text.Length == 0) return false;
            bool digit = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') { digit = true; continue; }
                if (c == ',') continue;
                return false;
            }
            return digit;
        }

        private static string ReadDigits(bool[] ink, int width, int height, int top)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            int x = 0;
            int gap = 0;
            while (x < width)
            {
                if (!Column(ink, width, top, x))
                {
                    gap++;
                    // The XP/h column sits further along the same row. Stop at that gap.
                    if (text.Length > 0 && gap > 8) break;
                    x++;
                    continue;
                }
                gap = 0;
                int start = x;
                while (x < width && Column(ink, width, top, x)) x++;
                int gw = x - start;
                // A real digit in this face is at least four pixels wide. A narrower
                // mark is a thousands comma or the dot in 1.6M.
                if (gw <= 3)
                {
                    if (BottomDot(ink, width, top, start, gw) && !MidDot(ink, width, top, start, gw)) text.Append(',');
                    else text.Append('.');
                    continue;
                }
                char digit = '?';
                int best = 99;
                for (int i = 0; i < Digits.Length; i++)
                {
                    Glyph glyph = Digits[i];
                    if (Math.Abs(glyph.Width - gw) > 1) continue;
                    int score = Score(ink, width, top, start, gw, glyph);
                    if (score < best) { best = score; digit = glyph.Name; }
                }
                if (digit != '?' && best <= glyphLimit(gw)) text.Append(digit);
                else text.Append('?');
            }
            return text.ToString();
        }

        private static bool MidDot(bool[] ink, int width, int top, int x, int gw)
        {
            int mid = 0;
            for (int dy = 2; dy <= 4; dy++)
                for (int dx = 0; dx < gw; dx++)
                    if (x + dx < width && ink[(top + dy) * width + x + dx]) mid++;
            return mid >= 1;
        }

        private static int glyphLimit(int gw)
        {
            return Math.Max(6, gw);
        }

        private static int Score(bool[] ink, int width, int top, int x, int gw, Glyph glyph)
        {
            int wrong = 0;
            for (int dy = 0; dy < 8; dy++)
            {
                string row = glyph.Rows[dy];
                for (int dx = 0; dx < glyph.Width; dx++)
                {
                    int sx = x + dx * gw / glyph.Width;
                    if (sx >= width) { wrong++; continue; }
                    bool on = ink[(top + dy) * width + sx];
                    bool want = row[dx] == '#';
                    if (on != want) wrong++;
                }
            }
            return wrong;
        }

        private static bool Column(bool[] ink, int width, int top, int x)
        {
            for (int dy = 0; dy < 8; dy++) if (ink[(top + dy) * width + x]) return true;
            return false;
        }

        private static bool BottomDot(bool[] ink, int width, int top, int x, int gw)
        {
            int n = 0;
            for (int dy = 5; dy < 8; dy++)
                for (int dx = 0; dx < gw; dx++)
                    if (ink[(top + dy) * width + x + dx]) n++;
            return n >= 1 && n <= 4;
        }

        private static bool Bright(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            return r + g + b > 420 && Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 90;
        }

        public static bool Parse(string text, out long xp)
        {
            xp = 0;
            if (text == null || text.Length == 0) return false;
            bool neg = text[0] == '-';
            long value = 0;
            bool any = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= '0' && ch <= '9')
                {
                    any = true;
                    value = value * 10 + (ch - '0');
                }
            }
            if (!any) return false;
            char last = text[text.Length - 1];
            if (last == 'M' || last == 'm') value *= 1000000;
            else if (last == 'K' || last == 'k') value *= 1000;
            xp = neg ? -value : value;
            return true;
        }

        private static Glyph G(char name, int width, string a, string b, string c, string d, string e, string f, string g, string h)
        {
            Glyph glyph = new Glyph();
            glyph.Name = name;
            glyph.Width = width;
            glyph.Rows = new string[] { a, b, c, d, e, f, g, h };
            return glyph;
        }
    }

    // Keeps the last total that agreed with itself. A one-off jump or drop,
    // such as a fragment of the number, is ignored until the same value repeats.
    public sealed class MetricsLock
    {
        public bool Found;
        public long Xp;
        public bool Gain;
        public bool Fresh;
        public string Note = "not found";
        private bool have;
        private long locked;
        private long pending;
        private int pendingCount;
        private DateTime missedSince = DateTime.MinValue;

        public void Observe(bool found, long xp, string rawNote, DateTime now)
        {
            Gain = false;
            Fresh = false;
            if (found && Accept(xp))
            {
                if (have && xp > locked) Gain = true;
                locked = xp;
                have = true;
                pendingCount = 0;
                missedSince = DateTime.MinValue;
                Found = true;
                Fresh = true;
                Xp = locked;
                Note = "xp=" + locked.ToString();
                return;
            }
            if (!have)
            {
                Found = false;
                Xp = 0;
                Note = found ? "reject " + xp.ToString() : (rawNote ?? "not found");
                return;
            }
            if (found)
            {
                Found = true;
                Xp = locked;
                Note = "held xp=" + locked.ToString() + " reject " + xp.ToString();
                missedSince = DateTime.MinValue;
                return;
            }
            if (missedSince == DateTime.MinValue) missedSince = now;
            if ((now - missedSince).TotalSeconds >= 5)
            {
                Found = false;
                Note = "lost";
                return;
            }
            Found = true;
            Xp = locked;
            Note = "held xp=" + locked.ToString();
        }

        private bool Accept(long xp)
        {
            if (!have) return xp >= 0;
            long delta = xp - locked;
            long room = locked / 4;
            if (room < 200) room = 200;
            if (delta >= -5 && delta <= room) return true;
            if (xp == pending) pendingCount++;
            else { pending = xp; pendingCount = 1; }
            return pendingCount >= 5;
        }
    }
}
