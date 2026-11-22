using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class SacrificeHit
    {
        public string Item = "";
        public long Gain;
        public long Total;
        public string Text = "";
        public string Fold = "";
    }

    // Sacrifice points come from one chat sentence. A message counts once,
    // when it is new since the previous read and its running total has not
    // been counted. There is no cooldown. The session sum starts at zero.
    public sealed class SacrificeBook
    {
        private sealed class Tok
        {
            public string Fold = "";
            public int Start;
            public int End;
            public bool Number;
        }

        private readonly List<SacrificeHit> previous = new List<SacrificeHit>();
        private readonly List<long> seenTotals = new List<long>();
        private bool primed = true;
        private bool haveTotal;
        public bool Seen;
        public long SessionGained;
        public int SessionCount;
        public long Total;
        public long LastGain;
        public string LastItem = "";
        public string Log = "";

        public void Attach()
        {
            primed = false;
            previous.Clear();
        }

        public void ResetSession()
        {
            SessionGained = 0;
            SessionCount = 0;
        }

        public string Value()
        {
            if (!Seen) return "\u2014";
            string line = Commas(SessionGained) + " this session \u00b7 " + SessionCount.ToString();
            if (!haveTotal) return line;
            string second = Commas(Total);
            if (LastItem != null && LastItem.Length > 0)
                second = second + " \u00b7 " + LastItem + " +" + Commas(LastGain);
            return line + "\n" + second;
        }

        public void Observe(string text)
        {
            List<string> lines = Join(text);
            List<SacrificeHit> current = new List<SacrificeHit>();
            for (int i = 0; i < lines.Count; i++)
            {
                SacrificeHit hit;
                if (TryParse(lines[i], out hit)) current.Add(hit);
            }
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            if (!primed)
            {
                if (text == null || text.Trim().Length == 0)
                {
                    Log = "";
                    return;
                }
                for (int i = 0; i < current.Count; i++)
                {
                    Remember(current[i].Total);
                    Append(log, current[i], "baseline", false, 0);
                }
                if (current.Count > 0) Show(current[current.Count - 1]);
                primed = true;
                Replace(current);
                Log = log.ToString();
                return;
            }
            bool[] used = new bool[previous.Count];
            for (int i = 0; i < current.Count; i++)
            {
                SacrificeHit hit = current[i];
                if (Matched(hit, used))
                {
                    Append(log, hit, "seen", false, 0);
                    continue;
                }
                if (Known(hit.Total))
                {
                    Append(log, hit, "seen", false, 0);
                    Show(hit);
                    continue;
                }
                long prev = Total;
                bool gap = !haveTotal || prev + hit.Gain != hit.Total;
                Remember(hit.Total);
                SessionGained += hit.Gain;
                SessionCount++;
                Show(hit);
                Append(log, hit, "new", gap, prev);
            }
            Replace(current);
            Log = log.ToString();
        }

        private void Replace(List<SacrificeHit> current)
        {
            previous.Clear();
            for (int i = 0; i < current.Count; i++) previous.Add(current[i]);
        }

        public static List<string> Join(string text)
        {
            List<string> done = new List<string>();
            if (text == null || text.Length == 0) return done;
            string[] raw = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = (raw[i] ?? "").Trim();
                if (line.Length == 0) continue;
                if (done.Count > 0 && Open(done[done.Count - 1]) && !StartsNew(line))
                    done[done.Count - 1] = done[done.Count - 1] + " " + line;
                else
                    done.Add(line);
            }
            return done;
        }

        public static bool TryParse(string line, out SacrificeHit hit)
        {
            hit = null;
            if (line == null) return false;
            List<Tok> tokens = Tokens(line);
            int verb = Find(tokens, FoldWord("sacrifice"), 0);
            if (verb < 0) return false;
            int gainAt = Find(tokens, FoldWord("gain"), verb + 1);
            if (gainAt < 0 || gainAt + 1 >= tokens.Count || !tokens[gainAt + 1].Number) return false;
            int have = Find(tokens, FoldWord("have"), gainAt + 2);
            if (have < 0) return false;
            int totalAt = Find(tokens, FoldWord("total"), have + 1);
            if (totalAt < 1 || !tokens[totalAt - 1].Number) return false;
            int again = Find(tokens, FoldWord("sacrifice"), totalAt + 1);
            if (again < 0) return false;
            if (Find(tokens, FoldWord("points"), again) < 0 && Find(tokens, FoldWord("points"), gainAt + 1) < 0) return false;
            long gain;
            long total;
            if (!ParseNum(tokens[gainAt + 1].Fold, out gain)) return false;
            if (!ParseNum(tokens[totalAt - 1].Fold, out total)) return false;
            int itemStart = verb + 1;
            int itemEnd = gainAt;
            if (itemEnd - 1 > itemStart && tokens[itemEnd - 1].Fold == FoldWord("and")) itemEnd--;
            if (itemEnd <= itemStart) return false;
            string item = line.Substring(tokens[itemStart].Start, tokens[itemEnd - 1].End - tokens[itemStart].Start).Trim();
            if (item.Length == 0) return false;
            hit = new SacrificeHit();
            hit.Item = item;
            hit.Gain = gain;
            hit.Total = total;
            hit.Text = line;
            hit.Fold = FoldWord(line);
            return true;
        }

        public static string Commas(long value)
        {
            if (value < 0) value = -value;
            string digits = value.ToString();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        private void Show(SacrificeHit hit)
        {
            Seen = true;
            haveTotal = true;
            Total = hit.Total;
            LastGain = hit.Gain;
            LastItem = hit.Item ?? "";
        }

        private void Remember(long total)
        {
            if (!Known(total)) seenTotals.Add(total);
        }

        private bool Known(long total)
        {
            for (int i = 0; i < seenTotals.Count; i++)
                if (seenTotals[i] == total) return true;
            return false;
        }

        private bool Matched(SacrificeHit hit, bool[] used)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                if (used[i]) continue;
                if (previous[i].Total != hit.Total && !Similar(previous[i].Fold, hit.Fold)) continue;
                used[i] = true;
                return true;
            }
            return false;
        }

        private static void Append(System.Text.StringBuilder log, SacrificeHit hit, string decision, bool gap, long prev)
        {
            if (log.Length > 0) log.Append(" | ");
            log.Append(decision).Append(" item=\"").Append(hit.Item).Append("\" gain=").Append(hit.Gain.ToString()).Append(" total=").Append(hit.Total.ToString());
            if (decision == "new")
            {
                log.Append(gap ? " sac gap" : " sac ok");
                log.Append(" prev=").Append(prev.ToString());
            }
        }

        private static bool Open(string line)
        {
            SacrificeHit hit;
            if (TryParse(line, out hit)) return false;
            List<Tok> tokens = Tokens(line);
            return Find(tokens, FoldWord("sacrifice"), 0) >= 0;
        }

        private static bool StartsNew(string line)
        {
            if (line == null) return false;
            string t = line.Trim();
            if (t.Length == 0) return false;
            if (t[0] == '[') return true;
            if (t.Length >= 5 && (t[0] == 'N' || t[0] == 'n') && t.IndexOf("ews") >= 0 && t.IndexOf(':') >= 0) return true;
            return false;
        }

        private static bool Similar(string a, string b)
        {
            if (a == null) a = "";
            if (b == null) b = "";
            if (a == b) return true;
            if (a.Length == 0 || b.Length == 0) return false;
            if (a.IndexOf(b) >= 0 || b.IndexOf(a) >= 0) return true;
            int limit = Math.Max(a.Length, b.Length) / 3;
            if (limit < 4) limit = 4;
            return Edit(a, b) <= limit;
        }

        private static int Edit(string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            if (n > 80) n = 80;
            if (m > 80) m = 80;
            int[] row = new int[m + 1];
            for (int j = 0; j <= m; j++) row[j] = j;
            for (int i = 1; i <= n; i++)
            {
                int prev = row[0];
                row[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int next = prev + cost;
                    int del = row[j] + 1;
                    int ins = row[j - 1] + 1;
                    if (del < next) next = del;
                    if (ins < next) next = ins;
                    prev = row[j];
                    row[j] = next;
                }
            }
            return row[m];
        }

        private static List<Tok> Tokens(string text)
        {
            List<Tok> tokens = new List<Tok>();
            if (text == null) return tokens;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') { i++; continue; }
                if (IsNumChar(text, i))
                {
                    int start = i;
                    while (i < text.Length && IsNumChar(text, i)) i++;
                    Tok tok = new Tok();
                    tok.Start = start;
                    tok.End = i;
                    tok.Number = true;
                    tok.Fold = text.Substring(start, i - start);
                    tokens.Add(tok);
                    continue;
                }
                if (IsWord(c))
                {
                    int start = i;
                    System.Text.StringBuilder fold = new System.Text.StringBuilder();
                    while (i < text.Length && IsWord(text[i]))
                    {
                        fold.Append(FoldLetter(text, i));
                        i++;
                    }
                    Tok tok = new Tok();
                    tok.Start = start;
                    tok.End = i;
                    tok.Fold = fold.ToString();
                    tokens.Add(tok);
                    continue;
                }
                i++;
            }
            return tokens;
        }

        private static bool IsWord(char c)
        {
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c == '-' || c == '\'' || c == '0' || c == '1') return true;
            return false;
        }

        private static bool IsNumChar(string text, int i)
        {
            char c = text[i];
            if (c >= '0' && c <= '9')
            {
                bool prev = i > 0 && (Digit(text[i - 1]) || text[i - 1] == ',');
                bool next = i + 1 < text.Length && (Digit(text[i + 1]) || text[i + 1] == ',');
                if (prev || next) return true;
                if (c != '0' && c != '1') return true;
                return false;
            }
            if (c == ',')
            {
                bool prev = i > 0 && Digit(text[i - 1]);
                bool next = i + 1 < text.Length && Digit(text[i + 1]);
                return prev && next;
            }
            return false;
        }

        private static bool Digit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static char FoldLetter(string text, int i)
        {
            char c = text[i];
            if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
            if (c == '0') return 'o';
            if (c == '1') return 'l';
            if (c == 'a') return 'e';
            if (c == 'v') return 'u';
            if (c == 'i') return 'l';
            return c;
        }

        private static string FoldWord(string word)
        {
            if (word == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<Tok> tokens = Tokens(word);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(tokens[i].Fold);
            }
            return sb.ToString();
        }

        private static int Find(List<Tok> tokens, string folded, int start)
        {
            if (start < 0) start = 0;
            for (int i = start; i < tokens.Count; i++)
            {
                if (tokens[i].Number) continue;
                if (Near(tokens[i].Fold, folded)) return i;
            }
            return -1;
        }

        private static bool Near(string got, string want)
        {
            if (got == null || want == null) return false;
            if (got == want) return true;
            int limit = want.Length >= 8 ? 2 : 1;
            if (Math.Abs(got.Length - want.Length) > limit) return false;
            return Edit(got, want) <= limit;
        }

        private static bool ParseNum(string text, out long value)
        {
            value = 0;
            if (text == null || text.Length == 0) return false;
            string digits = text.Replace(",", "");
            return long.TryParse(digits, out value) && value >= 0;
        }
    }
}
