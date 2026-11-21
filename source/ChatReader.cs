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
        public double Score;
        public string Decision;
    }

    // Reads potion lines from the chat box wherever it sits. The shapes are
    // cut from Danni's own chat crops. A line that is not one of those
    // phrases, including server chatter, is left alone.
    public static class ChatReader
    {
        public static string Trace = "";

        public static List<ChatHit> Read(ImageBuffer image)
        {
            List<ChatHit> best = new List<ChatHit>();
            string bestTrace = "";
            if (image == null) return best;
            int[] scales = new int[] { 100, 150, 200 };
            bool tried = false;
            for (int s = 0; s < scales.Length; s++)
            {
                int width;
                int height;
                bool[] ink = Ink(image, scales[s], out width, out height);
                List<ChatHit> hits = Match(ink, width, height);
                if (!tried || hits.Count > best.Count)
                {
                    best = hits;
                    bestTrace = Trace;
                    tried = true;
                }
                if (best.Count >= 4) break;
            }
            Trace = bestTrace;
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
            if (r >= 160 && g >= 70 && g <= r - 20 && b <= 90) return true;
            if (g >= 140 && g >= r + 25 && g >= b + 25) return true;
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
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            for (int b = 0; b < tops.Count; b++)
            {
                int y = BestRow(ink, width, tops[b], bottoms[b]);
                int left;
                int right;
                if (!Span(ink, width, height, y, out left, out right)) continue;
                if (trace.Length > 0) trace.Append(" | ");
                // The title strip of a full client sits at the top of the window.
                // A chat crop is short, so a line near its top is still chat.
                if (height >= 600 && y < 16)
                {
                    trace.Append("y=").Append(y).Append(" skip-top");
                    continue;
                }
                ChatHit best = null;
                ChatHit near = null;
                for (int s = 0; s < stamps.Count; s++)
                {
                    ChatStamp stamp = stamps[s];
                    int line = right - left + 1;
                    if (line < stamp.Width) continue;
                    int x = left;
                    int limit = right - stamp.Width + 1;
                    double topScore = 0;
                    int topX = left;
                    while (x <= limit)
                    {
                        double score = Score(ink, width, y, x, stamp);
                        if (score > topScore) { topScore = score; topX = x; }
                        x++;
                    }
                    int extraLeft = topX - left;
                    int extraRight = right - (topX + stamp.Width - 1);
                    bool gates = topScore >= 0.92 && extraRight <= 18 && extraLeft <= 120;
                    if (near == null || topScore > near.Score)
                    {
                        near = new ChatHit();
                        near.Phrase = stamp.Phrase;
                        near.Kind = stamp.Kind;
                        near.State = stamp.State;
                        near.Y = y;
                        near.Score = topScore;
                        near.Decision = gates ? "match" : "no-match";
                    }
                    // The phrase has to fill the line. A timestamp may sit in front.
                    // A busy chat line that only overlaps part of a phrase is rejected.
                    if (!gates) continue;
                    if (best == null || topScore > best.Score)
                    {
                        best = new ChatHit();
                        best.Phrase = stamp.Phrase;
                        best.Kind = stamp.Kind;
                        best.State = stamp.State;
                        best.Y = y;
                        best.Score = topScore;
                        best.Decision = "match";
                    }
                }
                if (best == null)
                {
                    string read = near == null ? "" : near.Phrase;
                    if (read.Length > 42) read = read.Substring(0, 42);
                    trace.Append("y=").Append(y).Append(" no-match");
                    if (near != null)
                        trace.Append(" score=").Append(near.Score.ToString("0.00")).Append(" \"").Append(read).Append("\"");
                    continue;
                }
                trace.Append("y=").Append(y).Append(" ").Append(best.Kind).Append(" ").Append(best.State)
                    .Append(" score=").Append(best.Score.ToString("0.00")).Append(" \"").Append(best.Phrase).Append("\"");
                hits.Add(best);
            }
            Trace = trace.ToString();
            return hits;
        }

        private static int BestRow(bool[] ink, int width, int top, int bottom)
        {
            int best = top;
            int bestCount = -1;
            int last = bottom - 5;
            if (last < top) last = top;
            for (int y = top; y <= last; y++)
            {
                int n = 0;
                for (int k = 0; k < 6 && y + k <= bottom; k++) n += CountRow(ink, width, y + k);
                if (n > bestCount) { bestCount = n; best = y; }
            }
            return best;
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

        // Agreement across every cell of the phrase, ink and gaps alike.
        private static double Score(bool[] ink, int width, int y, int x, ChatStamp stamp)
        {
            int agree = 0;
            int total = stamp.Width * 6;
            if (total < 30) return 0;
            for (int dy = 0; dy < 6; dy++)
            {
                string row = stamp.Rows[dy];
                int o = (y + dy) * width + x;
                for (int dx = 0; dx < stamp.Width; dx++)
                {
                    bool on = ink[o + dx];
                    bool want = row[dx] == '1';
                    if (on == want) agree++;
                }
            }
            return agree / (double)total;
        }
    }
}
