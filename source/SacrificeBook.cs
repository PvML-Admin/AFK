using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class SacrificeHit
    {
        public string Item = "";
        public long Gain;
        public long Total;
        public bool GainKnown;
        public bool TotalKnown;
        public string Text = "";
        public string Fold = "";
    }

    public sealed class OcrLine
    {
        public string Text = "";
        public int X;
        public int Y;
        public int Order;
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
        private bool confirmed;
        private bool havePending;
        private long pendingTotal;
        private long gainsSince;
        public bool Seen;
        public long SessionGained;
        public int SessionCount;
        public long Total;
        public long LastGain;
        public bool LastGainKnown;
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
            return Commas(SessionGained) + " this session \u00b7 " + SessionCount.ToString() + " sacrifices";
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
                    if (current[i].TotalKnown) Remember(current[i].Total);
                    Append(log, current[i], "baseline", false, 0);
                }
                if (current.Count > 0)
                {
                    Show(current[current.Count - 1]);
                    SacrificeHit last = current[current.Count - 1];
                    if (last.TotalKnown) ConsiderTotal(last.Total, last.Gain, last.GainKnown);
                }
                primed = true;
                Replace(current);
                Log = log.ToString();
                return;
            }
            bool[] used = Pair(current);
            for (int i = 0; i < current.Count; i++)
            {
                SacrificeHit hit = current[i];
                if (used[i])
                {
                    if (hit.TotalKnown) ConsiderTotal(hit.Total, hit.Gain, hit.GainKnown);
                    Append(log, hit, "seen", false, haveTotal ? Total : 0);
                    continue;
                }
                CountNew(hit, log);
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
            List<OcrLine> lines = new List<OcrLine>();
            if (text == null || text.Length == 0) return new List<string>();
            string[] raw = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = (raw[i] ?? "").Trim();
                if (line.Length == 0) continue;
                OcrLine row = new OcrLine();
                row.Text = line;
                row.Y = i;
                row.Order = i;
                lines.Add(row);
            }
            return Join(lines);
        }

        // Lines are ordered by on-screen Y, then X. A continuation with no
        // leading timestamp is joined to the sacrifice line above it. Tails
        // that OCR emitted somewhere else are attached to the open lines in order.
        public static List<string> Join(List<OcrLine> lines)
        {
            List<string> done = new List<string>();
            List<string> tails = new List<string>();
            if (lines == null || lines.Count == 0) return done;
            List<OcrLine> ordered = new List<OcrLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null || lines[i].Text == null) continue;
                string trimmed = lines[i].Text.Trim();
                if (trimmed.Length == 0) continue;
                OcrLine copy = new OcrLine();
                copy.Text = trimmed;
                copy.X = lines[i].X;
                copy.Y = lines[i].Y;
                copy.Order = lines[i].Order;
                ordered.Add(copy);
            }
            ordered.Sort(delegate(OcrLine a, OcrLine b)
            {
                int byY = a.Y.CompareTo(b.Y);
                if (byY != 0) return byY;
                int byX = a.X.CompareTo(b.X);
                if (byX != 0) return byX;
                return a.Order.CompareTo(b.Order);
            });
            for (int i = 0; i < ordered.Count; i++)
            {
                string line = ordered[i].Text;
                bool tail = !StartsNew(line) && LooksLikeTail(line);
                if (done.Count > 0 && !StartsNew(line) && (Open(done[done.Count - 1]) || (tail && SacrificeStart(done[done.Count - 1]))))
                    done[done.Count - 1] = done[done.Count - 1] + " " + line;
                else if (tail && (done.Count == 0 || !SacrificeStart(done[done.Count - 1])))
                    tails.Add(line);
                else
                    done.Add(line);
            }
            int tailAt = 0;
            for (int i = 0; i < done.Count; i++)
            {
                if (!Open(done[i])) continue;
                while (tailAt < tails.Count && !TailHasGain(tails[tailAt])) tailAt++;
                if (tailAt >= tails.Count) break;
                done[i] = done[i] + " " + tails[tailAt];
                tailAt++;
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
            // "total sacrifice" at the end of a wrapped half is not its own message.
            if (gainAt < 0) return false;
            long gain = 0;
            bool gainKnown = NumberAfter(line, tokens[gainAt].End, out gain);
            long total = 0;
            bool totalKnown = false;
            int totalAt = Find(tokens, FoldWord("total"), gainAt + 1);
            int have = Find(tokens, FoldWord("have"), gainAt);
            int now = Find(tokens, FoldWord("now"), gainAt);
            int anchor = -1;
            if (have >= 0) anchor = have;
            if (now >= 0 && (anchor < 0 || now > anchor)) anchor = now;
            if (totalAt > 0 || anchor >= 0)
            {
                int begin = anchor >= 0 ? tokens[anchor].End : (totalAt > 0 ? Math.Max(0, tokens[totalAt].Start - 24) : line.Length);
                long parsed;
                // Stop at the number. A trailing "points." is a word, not part of the total.
                if (NumberAfter(line, begin, out parsed) && parsed > TotalTolerance)
                {
                    total = parsed;
                    totalKnown = true;
                }
            }
            string item = "";
            int itemStart = verb + 1;
            int itemEnd = gainAt;
            if (itemEnd - 1 >= itemStart && tokens[itemEnd - 1].Fold == FoldWord("and")) itemEnd--;
            if (itemEnd > itemStart)
                item = line.Substring(tokens[itemStart].Start, tokens[itemEnd - 1].End - tokens[itemStart].Start).Trim();
            if (item.Length == 0 && !gainKnown) return false;
            hit = new SacrificeHit();
            hit.Item = item;
            hit.Gain = gain;
            hit.GainKnown = gainKnown;
            hit.Total = total;
            hit.TotalKnown = totalKnown;
            hit.Text = line;
            hit.Fold = FoldWord(line);
            return true;
        }

        // Letter fixes apply only inside a number slot, never to the item name.
        public static bool FixNum(string slot, out long value)
        {
            value = 0;
            if (slot == null || slot.Length == 0) return false;
            System.Text.StringBuilder digits = new System.Text.StringBuilder();
            int junk = 0;
            for (int i = 0; i < slot.Length; i++)
            {
                char c = slot[i];
                if (c >= '0' && c <= '9') digits.Append(c);
                else if (c == 'I' || c == 'l' || c == 'L' || c == '|' || c == '!') digits.Append('1');
                else if (c == 'S' || c == 's') digits.Append('5');
                else if (c == 'O' || c == 'o') digits.Append('0');
                else if (c == 'g' || c == 'G' || c == 'q' || c == 'Q') digits.Append('9');
                else if (c == 'B' || c == 'b') digits.Append('8');
                else if (c == 'Z' || c == 'z') digits.Append('2');
                else if (c == ' ' || c == ',' || c == '\'' || c == '\u2019' || c == '.') continue;
                else junk++;
            }
            if (junk > 0) return false;
            if (digits.Length == 0 || digits.Length > 12) return false;
            return long.TryParse(digits.ToString(), out value) && value >= 0;
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
            LastGain = hit.Gain;
            LastGainKnown = hit.GainKnown;
            LastItem = hit.Item ?? "";
        }

        private void Remember(long total)
        {
            if (!Known(total)) seenTotals.Add(total);
        }

        // A last-digit OCR flip is still the same sacrifice. Real gains are larger.
        private const long TotalTolerance = 9;

        private static bool Close(long a, long b)
        {
            long gap = a - b;
            if (gap < 0) gap = -gap;
            return gap <= TotalTolerance;
        }

        private static bool OneDigitOff(long a, long b)
        {
            if (a < 0) a = -a;
            if (b < 0) b = -b;
            string left = a.ToString();
            string right = b.ToString();
            if (left.Length != right.Length) return false;
            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) diff++;
            return diff == 1;
        }

        private bool Known(long total)
        {
            for (int i = 0; i < seenTotals.Count; i++)
                if (Close(seenTotals[i], total) || OneDigitOff(seenTotals[i], total)) return true;
            return false;
        }

        private bool[] Pair(List<SacrificeHit> current)
        {
            bool[] matched = new bool[current.Count];
            bool[] taken = new bool[previous.Count];
            for (int i = 0; i < current.Count; i++)
            {
                if (!current[i].TotalKnown) continue;
                for (int j = 0; j < previous.Count; j++)
                {
                    if (taken[j] || !previous[j].TotalKnown) continue;
                    if (!Close(current[i].Total, previous[j].Total) && !OneDigitOff(current[i].Total, previous[j].Total)) continue;
                    taken[j] = true;
                    matched[i] = true;
                    break;
                }
            }
            for (int i = 0; i < current.Count; i++)
            {
                if (matched[i]) continue;
                if (i >= previous.Count || taken[i]) continue;
                if (current[i].TotalKnown && previous[i].TotalKnown && !Close(current[i].Total, previous[i].Total) && !OneDigitOff(current[i].Total, previous[i].Total)) continue;
                if (!Similar(current[i].Fold, previous[i].Fold)) continue;
                taken[i] = true;
                matched[i] = true;
            }
            return matched;
        }

        private void CountNew(SacrificeHit hit, System.Text.StringBuilder log)
        {
            if (hit.TotalKnown && Known(hit.Total))
            {
                ConsiderTotal(hit.Total, hit.Gain, hit.GainKnown);
                Append(log, hit, "seen", false, haveTotal ? Total : 0);
                return;
            }
            long prev = haveTotal ? Total : 0;
            if (!hit.GainKnown)
            {
                long inferred;
                if (TryInfer(hit, out inferred))
                {
                    hit.Gain = inferred;
                    hit.GainKnown = true;
                }
                else
                {
                    SessionCount++;
                    Seen = true;
                    Append(log, hit, "new", false, prev);
                    return;
                }
            }
            bool trusted = hit.TotalKnown && ConsiderTotal(hit.Total, hit.Gain, true);
            bool gap = hit.TotalKnown && !trusted;
            if (hit.TotalKnown && !trusted) Remember(hit.Total);
            if (!trusted) gainsSince += hit.Gain;
            SessionGained += hit.Gain;
            SessionCount++;
            Show(hit);
            Append(log, hit, "new", gap, prev);
        }

        // A total is trusted when it agrees with the last good total plus the
        // gain, or plus the gains counted since that total. A lower total, a
        // different number of digits, or a wild jump is a misread.
        private bool ConsiderTotal(long total, long gain, bool gainKnown)
        {
            if (!gainKnown) gain = 0;
            if (!haveTotal)
            {
                haveTotal = true;
                confirmed = false;
                Total = total;
                gainsSince = 0;
                Remember(total);
                return true;
            }
            // Digit length and a falling total are checks against a confirmed total.
            // The first total is only tentative, so a later consistent pair may re-anchor.
            if (confirmed)
            {
                if (Digits(total) != Digits(Total)) return false;
                if (total + TotalTolerance < Total) return false;
            }
            if (Close(total, Total + gain) || (gainsSince > 0 && Close(total, Total + gainsSince + gain)))
            {
                Total = total;
                confirmed = true;
                havePending = false;
                gainsSince = 0;
                Remember(total);
                return true;
            }
            if (!confirmed)
            {
                if (havePending && gainKnown && Close(total, pendingTotal + gain))
                {
                    Total = total;
                    confirmed = true;
                    havePending = false;
                    gainsSince = 0;
                    Remember(pendingTotal);
                    Remember(total);
                    return true;
                }
                havePending = true;
                pendingTotal = total;
            }
            return false;
        }

        private bool TryInfer(SacrificeHit hit, out long gain)
        {
            gain = 0;
            if (!hit.TotalKnown || !haveTotal || !confirmed) return false;
            if (Digits(hit.Total) != Digits(Total) || hit.Total <= Total) return false;
            if (gainsSince != 0) return false;
            long diff = hit.Total - Total;
            if (diff <= 0) return false;
            gain = diff;
            return true;
        }

        private static int Digits(long value)
        {
            if (value < 0) value = -value;
            int count = 1;
            while (value >= 10) { value /= 10; count++; }
            return count;
        }

        private static bool NumberAfter(string line, int start, out long value)
        {
            value = 0;
            if (line == null || start < 0 || start >= line.Length) return false;
            int i = start;
            while (i < line.Length && line[i] == ' ') i++;
            int begin = i;
            bool any = false;
            while (i < line.Length)
            {
                char c = line[i];
                if ((c >= '0' && c <= '9') || FixLetter(c) || c == ',' || c == '.' || c == '\'' || c == '\u2019')
                {
                    any = true;
                    i++;
                    continue;
                }
                if (c == ' ')
                {
                    int k = i + 1;
                    while (k < line.Length && line[k] == ' ') k++;
                    if (k < line.Length && ((line[k] >= '0' && line[k] <= '9') || FixLetter(line[k])))
                    {
                        i = k;
                        continue;
                    }
                    break;
                }
                break;
            }
            if (!any || i <= begin) return false;
            string slice = line.Substring(begin, i - begin);
            bool digit = false;
            for (int n = 0; n < slice.Length; n++)
                if (slice[n] >= '0' && slice[n] <= '9') digit = true;
            if (!digit && i < line.Length && ((line[i] >= 'A' && line[i] <= 'Z') || (line[i] >= 'a' && line[i] <= 'z'))) return false;
            return FixNum(slice, out value) && value > 0;
        }

        private static bool FixLetter(char c)
        {
            return c == 'I' || c == 'l' || c == 'L' || c == '|' || c == '!'
                || c == 'S' || c == 's' || c == 'O' || c == 'o'
                || c == 'g' || c == 'G' || c == 'q' || c == 'Q'
                || c == 'B' || c == 'b' || c == 'Z' || c == 'z';
        }

        private static void Append(System.Text.StringBuilder log, SacrificeHit hit, string decision, bool gap, long prev)
        {
            if (log.Length > 0) log.Append(" | ");
            log.Append(decision).Append(" item=\"").Append(hit.Item).Append("\" gain=").Append(hit.Gain.ToString()).Append(" total=").Append(hit.Total.ToString());
            if (!hit.GainKnown) log.Append(" gain unknown");
            if (decision == "new")
            {
                if (!hit.TotalKnown) log.Append(" total unknown");
                else log.Append(gap ? " sac gap" : " sac ok");
                log.Append(" prev=").Append(prev.ToString());
            }
        }

        private static bool Open(string line)
        {
            return SacrificeStart(line) && !HasGain(line);
        }

        private static bool HasGain(string line)
        {
            SacrificeHit hit;
            return TryParse(line, out hit) && hit.GainKnown;
        }

        private static bool SacrificeStart(string line)
        {
            List<Tok> tokens = Tokens(line);
            int verb = Find(tokens, FoldWord("sacrifice"), 0);
            if (verb < 0) return false;
            if (verb > 0 && tokens[verb - 1].Fold == FoldWord("total")) return false;
            return true;
        }

        private static bool LooksLikeTail(string line)
        {
            if (line == null || StartsNew(line) || SacrificeStart(line)) return false;
            string lower = line.ToLowerInvariant();
            if (lower.IndexOf("points") >= 0 || lower.IndexOf("total") >= 0) return true;
            long ignored;
            return FixNum(line, out ignored);
        }

        private static bool TailHasGain(string line)
        {
            if (HasGain(line)) return true;
            List<Tok> tokens = Tokens(line);
            int stop = NextStop(tokens, 0);
            int end = stop >= 0 ? tokens[stop].Start : Math.Min(line.Length, 16);
            if (end < 0) end = 0;
            if (end > line.Length) end = line.Length;
            long ignored;
            return FixNum(line.Substring(0, end), out ignored);
        }

        private static int NextStop(List<Tok> tokens, int start)
        {
            string[] stops = new string[] { "points", "you", "now", "total", "have" };
            int best = -1;
            for (int s = 0; s < stops.Length; s++)
            {
                int at = Find(tokens, FoldWord(stops[s]), start);
                if (at >= 0 && (best < 0 || at < best)) best = at;
            }
            return best;
        }

        private static bool StartsNew(string line)
        {
            if (line == null) return false;
            string t = line.Trim();
            if (t.Length == 0) return false;
            if (t[0] == '[' || t[0] == ']') return true;
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
