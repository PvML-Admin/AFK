using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    // Bitmap glyphs cut from Danni's own screenshots. Matched by stretching
    // the unknown blob and the template to one grid, at the template scale
    // whose height is closest to the blob. No Alt1 assets.
    internal static class GlyphBook
    {
        internal struct Stamp
        {
            public char Name;
            public int Width;
            public int Height;
            public string Bits;
            public int Scale;
        }

        // Native (100%) shapes. 150% and 200% are produced from these.
        private static readonly Stamp[] Native = new Stamp[]
        {
            S('+', 9, 9, "000010000000010000000010000000010000111111111000010000000010000000010000000010000"),
            S('x', 8, 8, "1100001101100110011001100011110000111100011001100110011011000011"),
            S('p', 8, 11, "1101110011100110110000111100001111000011110000111100011011111100110000001100000011000000"),
            S('5', 9, 11, "011111111011000000011000000011000000011111100000000110000000011000000011110000011110000110011111100"),
            S('8', 9, 11, "001111100011000110110000011110000011011000110001111100011000110110000011110000011011000110001111100"),
            S('9', 9, 11, "001111100011000110110000011110000011110000011011000111001111011000000011000000110000001100011111000"),
            S('1', 3, 8, "010110010010010010010111"),
            S('2', 5, 8, "0111010001000010001000100010001000011111"),
            S('6', 5, 8, "0011001001100101011011001100011000101110"),
            S('m', 5, 5, "0101010101101011010110101"),
            S('0', 5, 8, "0111010001100011000110001100011000101110")
        };

        // A 150% nearest-neighbour "2" cut from the 12m flask fixture. The
        // upscale thickens the stroke, so the 100% template no longer wins.
        private static readonly Stamp[] Extra = new Stamp[]
        {
            S('2', 7, 9, "011111001111101000001000000100000010000110000100000010000110000")
        };

        private static readonly Stamp[] All = Build();

        private static Stamp S(char name, int w, int h, string bits)
        {
            Stamp stamp = new Stamp();
            stamp.Name = name;
            stamp.Width = w;
            stamp.Height = h;
            stamp.Bits = bits;
            stamp.Scale = 100;
            return stamp;
        }

        private static Stamp[] Build()
        {
            List<Stamp> list = new List<Stamp>();
            for (int i = 0; i < Native.Length; i++)
            {
                list.Add(Native[i]);
                list.Add(Scale(Native[i], 150));
                list.Add(Scale(Native[i], 200));
            }
            for (int i = 0; i < Extra.Length; i++)
            {
                Extra[i].Scale = 150;
                list.Add(Extra[i]);
            }
            return list.ToArray();
        }

        private static Stamp Scale(Stamp stamp, int percent)
        {
            int w = Math.Max(1, stamp.Width * percent / 100);
            int h = Math.Max(1, stamp.Height * percent / 100);
            char[] bits = new char[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = Math.Min(stamp.Height - 1, y * stamp.Height / h);
                for (int x = 0; x < w; x++)
                {
                    int sx = Math.Min(stamp.Width - 1, x * stamp.Width / w);
                    bits[y * w + x] = stamp.Bits[sy * stamp.Width + sx];
                }
            }
            Stamp scaled = new Stamp();
            scaled.Name = stamp.Name;
            scaled.Width = w;
            scaled.Height = h;
            scaled.Bits = new string(bits);
            scaled.Scale = percent;
            return scaled;
        }

        internal static char Match(bool[] mask, int stride, int left, int top, int width, int height)
        {
            if (width < 2 || height < 3) return '?';
            char best = '?';
            int bestScore = 1000;
            int second = 1000;
            for (int i = 0; i < All.Length; i++)
            {
                Stamp stamp = All[i];
                int score = Score(mask, stride, left, top, width, height, stamp);
                if (score < bestScore)
                {
                    second = bestScore;
                    bestScore = score;
                    best = stamp.Name;
                }
                else if (score < second) second = score;
            }
            if (bestScore > 48) return '?';
            if (second - bestScore < 3 && bestScore > 18) return '?';
            return best;
        }

        // Percent of cells that disagree, after both are stretched to 10x14.
        private static int Score(bool[] mask, int stride, int left, int top, int width, int height, Stamp stamp)
        {
            int wrong = 0;
            for (int y = 0; y < 14; y++)
            {
                int by = Math.Min(height - 1, y * height / 14);
                int sy = Math.Min(stamp.Height - 1, y * stamp.Height / 14);
                for (int x = 0; x < 10; x++)
                {
                    int bx = Math.Min(width - 1, x * width / 10);
                    int sx = Math.Min(stamp.Width - 1, x * stamp.Width / 10);
                    bool ink = mask[(top + by) * stride + left + bx];
                    bool on = stamp.Bits[sy * stamp.Width + sx] == '1';
                    if (ink != on) wrong++;
                }
            }
            return wrong * 100 / 140;
        }

        internal static int FindPlus(ImageBuffer image, out int scale)
        {
            scale = 100;
            if (image == null) return -1;
            int bestX = -1, bestY = -1, bestScale = 100, bestMiss = 100;
            int limit = image.Height * 70 / 100;
            int[] scales = new int[] { 100, 150, 200 };
            for (int s = 0; s < scales.Length; s++)
            {
                Stamp plus = Scaled('+', scales[s]);
                int step = scales[s] == 100 ? 2 : 3;
                for (int y = 0; y + plus.Height < limit; y += step)
                {
                    for (int x = 0; x + plus.Width < image.Width; x += step)
                    {
                        if (!Orange(image.Argb(x + plus.Width / 2, y + plus.Height / 2))) continue;
                        int miss = PlusMiss(image, x, y, plus);
                        int limitMiss = 5;
                        if (miss < bestMiss && miss <= limitMiss && ConfirmXp(image, x, y, scales[s]))
                        {
                            bestMiss = miss;
                            bestX = x;
                            bestY = y;
                            bestScale = scales[s];
                        }
                    }
                }
            }
            if (bestX < 0) return -1;
            scale = bestScale;
            return bestY * image.Width + bestX;
        }

        private static Stamp Scaled(char name, int percent)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Name == name && All[i].Scale == percent) return All[i];
            return Native[0];
        }

        private static int PlusMiss(ImageBuffer image, int x, int y, Stamp plus)
        {
            int wrong = 0, seen = 0;
            for (int ty = 0; ty < plus.Height; ty++)
                for (int tx = 0; tx < plus.Width; tx++)
                {
                    bool on = plus.Bits[ty * plus.Width + tx] == '1';
                    bool orange = Orange(image.Argb(x + tx, y + ty));
                    seen++;
                    if (on != orange) wrong++;
                }
            if (seen == 0) return 100;
            return wrong * 100 / seen;
        }

        internal static bool ConfirmXp(ImageBuffer image, int x, int y, int scale)
        {
            Stamp ex = Scaled('x', scale);
            Stamp pee = Scaled('p', scale);
            int reach = 110 * scale / 100;
            for (int dx = plusWidth(scale); dx < reach; dx++)
            {
                if (x + dx + ex.Width >= image.Width) break;
                for (int dy = -4; dy <= 6; dy += 2)
                {
                    if (y + dy < 0) continue;
                    if (PlusMiss(image, x + dx, y + dy, ex) <= 30) return true;
                    if (PlusMiss(image, x + dx, y + dy, pee) <= 30) return true;
                }
            }
            return false;
        }

        private static int plusWidth(int scale)
        {
            return Math.Max(1, 9 * scale / 100);
        }

        internal static bool Orange(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            return r >= 165 && g >= 80 && g <= 220 && b <= 150 && r >= g + 12 && r >= b + 35;
        }
    }
}
