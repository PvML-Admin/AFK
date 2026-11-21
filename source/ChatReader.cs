using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class ChatHit
    {
        public string Phrase;
        public string Kind;
        public string State;
        public int Y;
    }

    // Reads potion lines from the chat box wherever it sits. The shapes are
    // cut from Danni's own chat crops. A line that is not one of those
    // phrases, including server chatter, is left alone.
    public static class ChatReader
    {
        public static List<ChatHit> Read(ImageBuffer image)
        {
            List<ChatHit> best = new List<ChatHit>();
            if (image == null) return best;
            int[] scales = new int[] { 100, 150, 200 };
            for (int s = 0; s < scales.Length; s++)
            {
                int width;
                int height;
                bool[] ink = Ink(image, scales[s], out width, out height);
                List<ChatHit> hits = Match(ink, width, height);
                if (hits.Count > best.Count) best = hits;
                if (best.Count >= 4) break;
            }
            best.Sort(delegate(ChatHit a, ChatHit b) { return a.Y.CompareTo(b.Y); });
            return best;
        }

        private static bool[] Ink(ImageBuffer image, int percent, out int width, out int height)
        {
            int srcW = image.Width;
            int srcH = image.Height;
            if (percent == 100)
            {
                width = srcW;
                height = srcH;
                return Mask(image, 0, 0, srcW, srcH, 100);
            }
            width = Math.Max(1, (int)Math.Round(srcW * 100.0 / percent));
            height = Math.Max(1, (int)Math.Round(srcH * 100.0 / percent));
            bool[] ink = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy = Sample(y, percent, srcH);
                for (int x = 0; x < width; x++)
                {
                    int sx = Sample(x, percent, srcW);
                    ink[y * width + x] = Text(image.Argb(sx, sy));
                }
            }
            return ink;
        }

        // Inverse of ImageBuffer's nearest enlargement, so a 150% or 200%
        // client lands back on the native chat glyphs.
        private static int Sample(int dest, int percent, int srcLimit)
        {
            int src = (int)Math.Round((dest + 0.5) * percent / 100.0 - 0.5);
            if (src < 0) src = 0;
            if (src >= srcLimit) src = srcLimit - 1;
            return src;
        }

        private static bool[] Mask(ImageBuffer image, int x0, int y0, int width, int height, int unused)
        {
            bool[] ink = new bool[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    ink[y * width + x] = Text(image.Argb(x0 + x, y0 + y));
            return ink;
        }

        public static bool Text(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            if (r >= 110 && r >= g + 25 && r >= b + 25) return true;
            int mx = r;
            if (g > mx) mx = g;
            if (b > mx) mx = b;
            int mn = r;
            if (g < mn) mn = g;
            if (b < mn) mn = b;
            if (mx >= 170 && mx - mn >= 40 && r + g + b >= 320) return true;
            if (r + g + b >= 580 && mx - mn < 50) return true;
            return false;
        }

        private static List<ChatHit> Match(bool[] ink, int width, int height)
        {
            List<ChatHit> hits = new List<ChatHit>();
            List<ChatStamp> stamps = ChatLineTemplates.All();
            List<int> tops = new List<int>();
            List<int> bottoms = new List<int>();
            Bands(ink, width, height, tops, bottoms);
            bool[] used = new bool[stamps.Count];
            for (int b = 0; b < tops.Count; b++)
            {
                int from = tops[b];
                int to = bottoms[b] - 4;
                if (to < from) to = from;
                for (int y = from; y <= to; y++)
                {
                    int left = 0;
                    int right = width - 1;
                    if (!Span(ink, width, height, y, out left, out right)) continue;
                    for (int s = 0; s < stamps.Count; s++)
                    {
                        if (used[s]) continue;
                        ChatStamp stamp = stamps[s];
                        if (right - left + 1 < stamp.Width) continue;
                        int x = left;
                        int limit = right - stamp.Width + 1;
                        while (x <= limit)
                        {
                            if (Score(ink, width, height, y, x, stamp) >= 0.93)
                            {
                                ChatHit hit = new ChatHit();
                                hit.Phrase = stamp.Phrase;
                                hit.Kind = stamp.Kind;
                                hit.State = stamp.State;
                                hit.Y = y;
                                hits.Add(hit);
                                used[s] = true;
                                break;
                            }
                            x++;
                        }
                    }
                }
            }
            return hits;
        }

        private static void Bands(bool[] ink, int width, int height, List<int> tops, List<int> bottoms)
        {
            int y = 0;
            while (y < height)
            {
                if (CountRow(ink, width, y) <= 10) { y++; continue; }
                int start = y;
                int prev = y;
                y++;
                while (y < height && y <= prev + 1 && CountRow(ink, width, y) > 10)
                {
                    prev = y;
                    y++;
                }
                if (prev - start + 1 >= 5)
                {
                    tops.Add(start);
                    bottoms.Add(prev);
                }
            }
        }

        private static int CountRow(bool[] ink, int width, int y)
        {
            int n = 0;
            int o = y * width;
            for (int x = 0; x < width; x++) if (ink[o + x]) n++;
            return n;
        }

        private static bool Span(bool[] ink, int width, int height, int y, out int left, out int right)
        {
            left = 0;
            right = -1;
            if (y < 0 || y + 5 >= height) return false;
            for (int x = 0; x < width; x++)
            {
                bool on = false;
                for (int dy = 0; dy < 6; dy++) if (ink[(y + dy) * width + x]) on = true;
                if (!on) continue;
                if (right < 0) left = x;
                right = x;
            }
            return right >= left;
        }

        private static double Score(bool[] ink, int width, int height, int y, int x, ChatStamp stamp)
        {
            int hit = 0;
            int miss = 0;
            for (int dy = 0; dy < 6; dy++)
            {
                string row = stamp.Rows[dy];
                int o = (y + dy) * width + x;
                for (int dx = 0; dx < stamp.Width; dx++)
                {
                    if (row[dx] != '1') continue;
                    if (ink[o + dx]) hit++;
                    else miss++;
                }
            }
            if (hit + miss < 30) return 0;
            return hit / (double)(hit + miss);
        }
    }
}
