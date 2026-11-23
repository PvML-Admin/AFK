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

    // Sacrifice points come from the gain written in the chat line. The same
    // slot, after the chat scrolls, is the same message. A total is trusted
    // only when it equals the last trusted total plus that gain. There is no
    // cooldown. The session sum starts at zero.
    public sealed class SacrificeBook
    {
        private sealed class Tok
        {
            public string Fold = "";
            public int Start;
            public int End;
            public bool Number;
        }

        private sealed class Track
        {
            public SacrificeHit Hit;
            public int Reads;
            public bool Counted;
            public long HeldTotal;
            public bool HeldKnown;
        }

        private readonly List<Track> previous = new List<Track>();
        private readonly List<long> seenTotals = new List<long>();
        private readonly System.Text.StringBuilder counts = new System.Text.StringBuilder();
        private bool primed = true;
        private bool confirmed;
        public bool Seen;
        public long SessionGained;
        public int SessionCount;
        public DateTime SessionStarted;

        public SacrificeBook()
        {
            SessionStarted = DateTime.UtcNow;
        }
        public long Total;
        public long LastGain;
        public bool LastGainKnown;
        public string LastItem = "";
        public string Log = "";
        public string Trace = "";

        public void Attach()
        {
            primed = false;
            previous.Clear();
        }

        public void ResetSession()
        {
            SessionGained = 0;
            SessionCount = 0;
            counts.Length = 0;
            Log = "";
            SessionStarted = DateTime.UtcNow;
        }

        public double ElapsedSeconds(DateTime now)
        {
            double seconds = (now - SessionStarted).TotalSeconds;
            if (seconds < 0) seconds = 0;
            return seconds;
        }

        public string Value()
        {
            return Format(Seen, SessionGained, SessionCount, ElapsedSeconds(DateTime.UtcNow));
        }

        public static string Format(bool seen, long gained, int count, double elapsedSeconds)
        {
            if (!seen) return "\u2014";
            return Commas(gained) + " this session \u00b7 " + count.ToString() + " sacrifices \u00b7 " + Rate(gained, elapsedSeconds);
        }

        // The rate stays blank for the first minute so a single sacrifice does not look huge.
        public static string Rate(long points, double elapsedSeconds)
        {
            if (elapsedSeconds < 60 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds)) return "\u2014/h";
            double perHour = points * 3600.0 / elapsedSeconds;
            if (perHour < 0) perHour = 0;
            if (perHour > 999999999999d) perHour = 999999999999d;
            long rounded = (long)Math.Round(perHour);
            return Commas(rounded) + "/h";
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
            System.Text.StringBuilder notes = new System.Text.StringBuilder();
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            if (!primed)
            {
                if (text == null || text.Trim().Length == 0)
                {
                    Log = "";
                    Trace = "";
                    return;
                }
                previous.Clear();
                for (int i = 0; i < current.Count; i++)
                {
                    Track slot = TrackFrom(current[i], null);
                    slot.Counted = true;
                    slot.Reads = 2;
                    previous.Add(slot);
                    if (current[i].TotalKnown) Remember(current[i].Total);
                    Note(notes, "sac reject reason=baseline", current[i]);
                    TraceLine(trace, "rejected reason=baseline", current[i]);
                }
                if (current.Count > 0)
                {
                    Show(current[current.Count - 1]);
                    SacrificeHit last = current[current.Count - 1];
                    if (last.TotalKnown) Adopt(last);
                }
                primed = true;
                Publish(notes);
                Trace = trace.ToString();
                return;
            }
            if (current.Count == 0)
            {
                Publish(notes);
                Trace = "";
                return;
            }
            int shift = Align(current);
            List<Track> next = new List<Track>();
            for (int i = 0; i < current.Count; i++)
            {
                Track prior = null;
                int j = i + shift;
                if (j >= 0 && j < previous.Count) prior = previous[j];
                next.Add(TrackFrom(current[i], prior));
            }
            for (int i = 0; i < next.Count; i++)
            {
                Track slot = next[i];
                // A total that matches the last counted total plus the gain is a new
                // sacrifice, even when chat scrolled it into an older line's slot.
                // A one-digit or dropped-digit correction of that same slot is not.
                if (Validates(slot.Hit) || ConfirmedByLater(next, i) || FirstAnchor(slot.Hit))
                {
                    int linked = i + shift;
                    bool sameLine = slot.Counted && linked >= 0 && linked < previous.Count && previous[linked].HeldKnown
                        && MisreadOf(previous[linked].HeldTotal, slot.Hit.Total);
                    if (sameLine)
                    {
                        if (slot.Hit.TotalKnown) Adopt(slot.Hit);
                        Note(notes, "sac reject reason=same-slot", slot.Hit);
                        TraceLine(trace, "rejected reason=same-slot", slot.Hit);
                        continue;
                    }
                    Accept(slot, "total");
                    TraceLine(trace, "counted reason=total", slot.Hit);
                    continue;
                }
                if (slot.Hit.TotalKnown && Known(slot.Hit.Total))
                {
                    slot.Counted = true;
                    Note(notes, "sac reject reason=already-counted", slot.Hit);
                    TraceLine(trace, "rejected reason=already-counted", slot.Hit);
                    continue;
                }
                if (slot.Counted)
                {
                    Note(notes, "sac reject reason=same-slot", slot.Hit);
                    TraceLine(trace, "rejected reason=same-slot", slot.Hit);
                    continue;
                }
                if (slot.Reads >= 2)
                {
                    Accept(slot, "position");
                    TraceLine(trace, "counted reason=position", slot.Hit);
                    continue;
                }
                Note(notes, "sac pending reason=unvalidated", slot.Hit);
                TraceLine(trace, "pending reason=unvalidated", slot.Hit);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (!LooksLike(lines[i]) || Listed(current, lines[i])) continue;
                if (trace.Length > 0) trace.Append("\n");
                trace.Append("sac-trace rejected reason=gain-unreadable gain=0 total=0 raw=\"").Append(Clean(lines[i])).Append("\"");
            }
            previous.Clear();
            for (int i = 0; i < next.Count; i++) previous.Add(next[i]);
            Publish(notes);
            Trace = trace.ToString();
        }

        private bool FirstAnchor(SacrificeHit hit)
        {
            return !confirmed && hit.GainKnown && hit.TotalKnown;
        }

        // The same chat line read again: one digit flipped, or one digit dropped.
        private static bool MisreadOf(long prior, long now)
        {
            if (prior == now || Close(prior, now)) return true;
            if (OneDigitOff(prior, now)) return true;
            return DroppedDigit(prior, now);
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

        private static bool DroppedDigit(long a, long b)
        {
            if (a < 0) a = -a;
            if (b < 0) b = -b;
            string left = a.ToString();
            string right = b.ToString();
            if (left.Length + 1 == right.Length) return DroppedOne(right, left);
            if (right.Length + 1 == left.Length) return DroppedOne(left, right);
            return false;
        }

        private static bool DroppedOne(string longer, string shorter)
        {
            for (int skip = 0; skip < longer.Length; skip++)
            {
                int at = 0;
                bool same = true;
                for (int i = 0; i < longer.Length; i++)
                {
                    if (i == skip) continue;
                    if (at >= shorter.Length || longer[i] != shorter[at]) { same = false; break; }
                    at++;
                }
                if (same && at == shorter.Length) return true;
            }
            return false;
        }

        private static bool Listed(List<SacrificeHit> hits, string line)
        {
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Text == line) return true;
            return false;
        }

        public static bool LooksLike(string line)
        {
            if (line == null || line.Length == 0) return false;
            return Find(Tokens(line), FoldWord("sacrifice"), 0) >= 0;
        }

        private static void TraceLine(System.Text.StringBuilder trace, string decision, SacrificeHit hit)
        {
            if (trace.Length > 0) trace.Append("\n");
            trace.Append("sac-trace ").Append(decision);
            trace.Append(" gain=").Append(hit.GainKnown ? hit.Gain.ToString() : "0");
            trace.Append(" total=").Append(hit.Total.ToString());
            if (!hit.GainKnown) trace.Append(" gain-unreadable");
            trace.Append(" raw=\"").Append(Clean(hit.Text)).Append("\"");
        }

        private static string Clean(string text)
        {
            if (text == null) return "";
            return text.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
        }

        private static Track TrackFrom(SacrificeHit hit, Track prior)
        {
            Track slot = new Track();
            slot.Hit = hit;
            if (prior != null)
            {
                slot.Reads = prior.Reads + 1;
                slot.Counted = prior.Counted;
                slot.HeldTotal = prior.HeldTotal;
                slot.HeldKnown = prior.HeldKnown;
            }
            else slot.Reads = 1;
            if (hit.TotalKnown)
            {
                slot.HeldTotal = hit.Total;
                slot.HeldKnown = true;
            }
            return slot;
        }

        private void Accept(Track slot, string reason)
        {
            slot.Counted = true;
            long gain = slot.Hit.GainKnown ? slot.Hit.Gain : 0;
            SessionGained += gain;
            SessionCount++;
            Seen = true;
            Show(slot.Hit);
            if (slot.Hit.TotalKnown)
            {
                if (!confirmed || Validates(slot.Hit)) Adopt(slot.Hit);
                else Remember(slot.Hit.Total);
            }
            if (counts.Length > 0) counts.Append(" | ");
            counts.Append("sac count #").Append(SessionCount.ToString());
            counts.Append(" reason=").Append(reason);
            counts.Append(" gain=").Append(gain.ToString());
            counts.Append(" total=").Append(slot.Hit.Total.ToString());
            if (!slot.Hit.GainKnown) counts.Append(" gain unknown");
        }

        private void Adopt(SacrificeHit hit)
        {
            Total = hit.Total;
            confirmed = true;
            Remember(hit.Total);
        }

        private bool Validates(SacrificeHit hit)
        {
            if (!confirmed || !hit.TotalKnown || !hit.GainKnown) return false;
            return Close(hit.Total, Total + hit.Gain);
        }

        // The first totals of a session confirm each other when each line's
        // total is the one before it plus that line's own gain.
        private bool ConfirmedByLater(List<Track> slots, int index)
        {
            if (confirmed) return false;
            Track slot = slots[index];
            if (!slot.Hit.TotalKnown || !slot.Hit.GainKnown) return false;
            for (int k = index + 1; k < slots.Count; k++)
            {
                if (slots[k].Counted) continue;
                if (!slots[k].Hit.TotalKnown || !slots[k].Hit.GainKnown) return false;
                return Close(slots[k].Hit.Total, slot.Hit.Total + slots[k].Hit.Gain);
            }
            return false;
        }

        private int Align(List<SacrificeHit> current)
        {
            if (previous.Count == 0 || current.Count == 0) return 0;
            int bestShift = 0;
            int bestScore = int.MinValue;
            int minS = 1 - current.Count;
            int maxS = previous.Count;
            for (int s = minS; s < maxS; s++)
            {
                int overlap = 0;
                int close = 0;
                for (int i = 0; i < current.Count; i++)
                {
                    int j = i + s;
                    if (j < 0 || j >= previous.Count) continue;
                    overlap++;
                    if (current[i].TotalKnown && previous[j].HeldKnown && Close(current[i].Total, previous[j].HeldTotal))
                        close++;
                }
                if (overlap == 0) continue;
                int score = close * 100 + overlap;
                if (score > bestScore || (score == bestScore && PreferShift(s, bestShift)))
                {
                    bestScore = score;
                    bestShift = s;
                }
            }
            return bestShift;
        }

        private static bool PreferShift(int shift, int best)
        {
            int left = shift < 0 ? -shift : shift;
            int right = best < 0 ? -best : best;
            if (left != right) return left < right;
            return shift > best;
        }

        private static void Note(System.Text.StringBuilder notes, string reason, SacrificeHit hit)
        {
            if (notes.Length > 0) notes.Append(" | ");
            notes.Append(reason);
            notes.Append(" gain=").Append(hit.GainKnown ? hit.Gain.ToString() : "0");
            notes.Append(" total=").Append(hit.Total.ToString());
            if (!hit.GainKnown) notes.Append(" gain unknown");
        }

        private void Publish(System.Text.StringBuilder notes)
        {
            Log = counts.ToString();
            if (notes.Length == 0) return;
            if (Log.Length > 0) Log = Log + " | ";
            Log = Log + notes.ToString();
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

        private bool Known(long total)
        {
            for (int i = 0; i < seenTotals.Count; i++)
                if (Close(seenTotals[i], total)) return true;
            return false;
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
