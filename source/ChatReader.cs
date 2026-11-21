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
                // A one-pixel slip makes a real line score about 0.78 and look like
                // the wrong phrase. Try the rows just above and below the darkest one.
                for (int dy = -2; dy <= 2; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy + 5 >= height) continue;
                    int rowLeft;
                    int rowRight;
                    if (!Span(ink, width, height, yy, out rowLeft, out rowRight)) continue;
                    Consider(ink, width, yy, rowLeft, rowRight, stamps, ref best, ref near);
                }
                string raw = ReadRaw(ink, width, height, best != null ? best.Y : y, left, right);
                string candidate = near == null ? "" : near.Phrase;
                if (candidate.Length > 48) candidate = candidate.Substring(0, 48);
                if (raw.Length > 48) raw = raw.Substring(0, 48);
                if (best == null)
                {
                    trace.Append("y=").Append(y).Append(" no-match");
                    trace.Append(" raw=\"").Append(raw).Append("\"");
                    if (near != null)
                        trace.Append(" candidate=\"").Append(candidate).Append("\" score=").Append(near.Score.ToString("0.00"));
                    continue;
                }
                trace.Append("y=").Append(best.Y).Append(" ").Append(best.Kind).Append(" ").Append(best.State)
                    .Append(" score=").Append(best.Score.ToString("0.00"))
                    .Append(" raw=\"").Append(raw).Append("\" candidate=\"").Append(candidate).Append("\"");
                if (best.Kind == "elder" || best.Kind == "aggression") hits.Add(best);
            }
            Trace = trace.ToString();
            return hits;
        }

        private static void Consider(bool[] ink, int width, int y, int left, int right, List<ChatStamp> stamps, ref ChatHit best, ref ChatHit near)
        {
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
        }

        // Glyph widths of the red line. This is the pixels, not the nearest phrase.
        private static string ReadRaw(bool[] ink, int width, int height, int y, int left, int right)
        {
            if (y < 0 || y + 5 >= height || right < left) return "";
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            int x = left;
            int gap = 0;
            int glyphs = 0;
            while (x <= right && glyphs < 28)
            {
                bool on = false;
                for (int dy = 0; dy < 6; dy++) if (ink[(y + dy) * width + x]) on = true;
                if (!on)
                {
                    gap++;
                    x++;
                    continue;
                }
                if (gap >= 2 && text.Length > 0) text.Append(' ');
                gap = 0;
                int start = x;
                while (x <= right)
                {
                    bool col = false;
                    for (int dy = 0; dy < 6; dy++) if (ink[(y + dy) * width + x]) col = true;
                    if (!col) break;
                    x++;
                }
                int gw = x - start;
                text.Append(gw.ToString());
                if (x <= right) text.Append('.');
                glyphs++;
            }
            return text.ToString();
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

    // Known potion sentences, compared after the timestamp, dots and case are removed.
    public static class ChatPhrase
    {
        public static string Normalize(string text)
        {
            if (text == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            bool space = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '[' && i + 8 < text.Length && text[i + 3] == ':' && text[i + 6] == ':')
                {
                    int end = text.IndexOf(']', i);
                    if (end > i && end - i < 12) { i = end; continue; }
                }
                if (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';' || c == '\'' || c == '"') continue;
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length == 0 || space) continue;
                    space = true;
                    sb.Append(' ');
                    continue;
                }
                space = false;
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static bool Same(string read, string phrase)
        {
            string a = Normalize(read);
            string b = Normalize(phrase);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;
            if (a.IndexOf(b) >= 0 || b.IndexOf(a) >= 0) return true;
            return Distance(a, b) <= 2;
        }

        public static List<ChatHit> FromOcr(string text)
        {
            List<ChatHit> hits = new List<ChatHit>();
            if (text == null || text.Length == 0) return hits;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string kind;
                string state;
                string phrase;
                if (!MatchLine(lines[i], out kind, out state, out phrase)) continue;
                ChatHit hit = new ChatHit();
                hit.Phrase = phrase;
                hit.Kind = kind;
                hit.State = state;
                hit.Y = i * 16;
                hit.Score = 1;
                hit.Decision = "match";
                hits.Add(hit);
            }
            return hits;
        }

        public static bool MatchLine(string line, out string kind, out string state, out string phrase)
        {
            kind = "";
            state = "";
            phrase = "";
            string folded = Fold(line);
            if (folded.Length < 12) return false;
            string[] got = Tokens(folded);
            if (got.Length == 0) return false;
            string[] known = Known();
            int bestHit = -1;
            double bestRatio = 0;
            for (int i = 0; i < known.Length; i++)
            {
                string[] parts = known[i].Split('|');
                if (parts.Length != 3) continue;
                int hit;
                int need;
                bool anchor;
                bool cue;
                Score(got, parts[2], out hit, out need, out anchor, out cue);
                if (!cue || need <= 0) continue;
                if (!anchor && parts[0] != "aggression") continue;
                if (parts[0] == "aggression" && !anchor && hit < 4) continue;
                double ratio = (double)hit / (double)need;
                if (ratio < 0.5) continue;
                if (hit > bestHit || (hit == bestHit && ratio > bestRatio))
                {
                    bestHit = hit;
                    bestRatio = ratio;
                    kind = parts[0];
                    state = parts[1];
                    phrase = parts[2];
                }
            }
            return phrase.Length > 0;
        }

        // Lower case, punctuation dropped, and the mixes Windows OCR makes on this
        // font folded together: u/v, a/e, rn/m, 0/o, 1/l/i.
        public static string Fold(string text)
        {
            if (text == null || text.Length == 0) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            bool space = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if (c == 'v') c = 'u';
                else if (c == 'a') c = 'e';
                else if (c == '0') c = 'o';
                else if (c == '1' || c == 'i' || c == 'l') c = 'l';
                if (c < 'a' || c > 'z')
                {
                    if (sb.Length == 0 || space) continue;
                    space = true;
                    sb.Append(' ');
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            return sb.ToString().Trim().Replace("rn", "m");
        }

        private static string[] Tokens(string folded)
        {
            if (folded == null || folded.Length == 0) return new string[0];
            return folded.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void Score(string[] got, string phrase, out int hit, out int need, out bool anchor, out bool cue)
        {
            hit = 0;
            need = 0;
            anchor = false;
            cue = false;
            string[] words = Tokens(Fold(phrase));
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length < 3 || Stop(word)) continue;
                int best = 9;
                for (int g = 0; g < got.Length; g++)
                {
                    int d = Distance(word, got[g]);
                    if (d < best) best = d;
                }
                int allow = word.Length >= 8 ? 2 : 1;
                // The word Aggression is often missing. The rest of that sentence is not shared.
                if (word == Fold("aggression"))
                {
                    if (best <= allow) { hit++; anchor = true; }
                    continue;
                }
                need++;
                if (best > allow) continue;
                hit++;
                if (Anchor(word)) anchor = true;
                if (Cue(word) && best <= 1) cue = true;
            }
        }

        private static bool Anchor(string word)
        {
            return word == Fold("elder") || word == Fold("aggression");
        }

        private static bool Cue(string word)
        {
            return word == Fold("about") || word == Fold("will") || word == Fold("worn")
                || word == Fold("expired") || word == Fold("expire") || word == Fold("wear")
                || word == Fold("has") || word == Fold("off");
        }

        private static bool Stop(string word)
        {
            return word == Fold("your") || word == Fold("the") || word == Fold("of") || word == Fold("is")
                || word == Fold("to") || word == Fold("in") || word == Fold("a") || word == Fold("an")
                || word == Fold("and") || word == Fold("some") || word == Fold("are");
        }

        private static string[] Known()
        {
            return new string[] {
                "elder|low|your elder overload is about to run out",
                "elder|expired|the effects of elder overload have worn off and you feel normal again",
                "aggression|low|the effects of your aggression potion will expire in 30 seconds",
                "aggression|expired|the effects of your aggression potion have expired"
            };
        }

        public static string KindOf(string text)
        {
            string n = Normalize(text);
            if (n.IndexOf("elder") >= 0 && n.IndexOf("overload") >= 0) return "elder";
            if (n.IndexOf("aggression") >= 0) return "aggression";
            return "";
        }

        public static string StateOf(string text)
        {
            string n = Normalize(text);
            if (n.Length == 0 || KindOf(text).Length == 0) return "";
            if (n.IndexOf("worn off") >= 0 || n.IndexOf("has run out") >= 0 || n.IndexOf("has expired") >= 0 || n.IndexOf("have worn") >= 0 || n.IndexOf("have expired") >= 0) return "expired";
            if (n.IndexOf("about to") >= 0 || n.IndexOf("will run out") >= 0 || n.IndexOf("will expire") >= 0 || n.IndexOf("about to expire") >= 0 || n.IndexOf("about to wear") >= 0) return "low";
            if (n.IndexOf("you drink") >= 0 || n.IndexOf("salve") >= 0 || n.IndexOf("potion") >= 0) return "active";
            return "";
        }

        private static int Distance(string a, string b)
        {
            if (System.Math.Abs(a.Length - b.Length) > 2) return 9;
            int n = a.Length;
            int m = b.Length;
            int[] row = new int[m + 1];
            for (int j = 0; j <= m; j++) row[j] = j;
            for (int i = 1; i <= n; i++)
            {
                int prev = row[0];
                row[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cur = row[j];
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int add = prev + cost;
                    if (row[j] + 1 < add) add = row[j] + 1;
                    if (row[j - 1] + 1 < add) add = row[j - 1] + 1;
                    prev = cur;
                    row[j] = add;
                }
            }
            return row[m];
        }
    }
}
