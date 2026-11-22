using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class ChatNotice
    {
        public string Kind;
        public string Title;
        public string Detail;
        // A 30 second warning is panel-only. Sound and the toast wait for expiry.
        public bool Audible;
    }

    // Potion alerts come from chat lines. A line fires only when it is new
    // since the previous read and that pot and warning are outside a
    // five-minute cooldown. Scrolling the same line up does not fire again.
    // The drink line is not required.
    public sealed class ChatPotionBook
    {
        public const int CooldownSeconds = 300;

        private sealed class Line
        {
            public string Kind;
            public string State;
            public string Text;
            public int Y;
            public int Order;
        }

        private sealed class Pot
        {
            public string Kind;
            public string State;
            public DateTime Since;
        }

        private sealed class Cool
        {
            public string Kind;
            public string State;
            public DateTime Fired;
        }

        private readonly List<Line> previous = new List<Line>();
        private readonly List<Pot> pots = new List<Pot>();
        private readonly List<Cool> cool = new List<Cool>();
        private bool primed = true;
        public string Detail = "";
        public string Log = "";

        // Lines already on screen when the client is attached are the baseline.
        public void Attach()
        {
            primed = false;
            previous.Clear();
            cool.Clear();
        }

        public List<ChatNotice> Observe(List<ChatHit> hits)
        {
            return Observe(hits, DateTime.UtcNow);
        }

        public List<ChatNotice> Observe(List<ChatHit> hits, DateTime now)
        {
            List<ChatNotice> notices = new List<ChatNotice>();
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            if (hits == null) hits = new List<ChatHit>();
            List<Line> current = Lines(hits);
            if (!primed)
            {
                // Only this first read is old. An empty read still counts, so a
                // pot line that shows up afterwards is not treated as preexisting.
                for (int i = 0; i < hits.Count; i++)
                    Append(log, hits[i], "skip-preexisting");
                previous.Clear();
                for (int i = 0; i < current.Count; i++) previous.Add(current[i]);
                primed = true;
                Log = log.ToString();
                Detail = Roster();
                return notices;
            }
            bool[] used = new bool[previous.Count];
            for (int i = 0; i < current.Count; i++)
            {
                ChatHit hit = HitAt(hits, current[i]);
                if (hit == null) continue;
                if (Drink(hit.Phrase))
                {
                    Set(hit.Kind, "active", now);
                    ForgetKind(hit.Kind);
                    Append(log, hit, "rearm");
                    continue;
                }
                if (SameAsPrevious(current[i], used))
                {
                    Append(log, hit, "skip-seen");
                    continue;
                }
                if (Cooling(hit.Kind, hit.State, now))
                {
                    Append(log, hit, "skip-cooldown");
                    continue;
                }
                Set(hit.Kind, hit.State, now);
                Stamp(hit.Kind, hit.State, now);
                ChatNotice notice = new ChatNotice();
                notice.Kind = "potion";
                notice.Title = Title(hit.Kind, hit.State);
                notice.Detail = notice.Title;
                notice.Audible = hit.State == "expired";
                notices.Add(notice);
                Append(log, hit, "alert");
            }
            previous.Clear();
            for (int i = 0; i < current.Count; i++) previous.Add(current[i]);
            Log = log.ToString();
            Detail = Roster();
            return notices;
        }

        public void Refresh()
        {
            Refresh(DateTime.UtcNow);
        }

        // An expired row is only a moment. After about 45 seconds it leaves,
        // and the panel goes back to Watching chat unless another pot is still up.
        public void Refresh(DateTime now)
        {
            for (int i = pots.Count - 1; i >= 0; i--)
            {
                if (pots[i].Since == DateTime.MinValue) continue;
                double age = (now - pots[i].Since).TotalSeconds;
                if (pots[i].State == "low" && age >= 30) pots.RemoveAt(i);
                else if (pots[i].State == "expired" && age >= 45) pots.RemoveAt(i);
            }
            Detail = Roster();
        }

        private static void Append(System.Text.StringBuilder log, ChatHit hit, string decision)
        {
            if (hit == null) return;
            if (log.Length > 0) log.Append(" | ");
            hit.Decision = decision;
            log.Append(decision).Append(" ").Append(hit.Kind).Append(" ").Append(hit.State)
                .Append(" score=").Append(hit.Score.ToString("0.00")).Append(" \"").Append(hit.Phrase).Append("\"");
        }

        private static List<Line> Lines(List<ChatHit> hits)
        {
            List<Line> lines = new List<Line>();
            for (int i = 0; i < hits.Count; i++)
            {
                ChatHit hit = hits[i];
                if (hit == null || hit.Kind == null || hit.Kind.Length == 0) continue;
                if (hit.Phrase == null) continue;
                Line line = new Line();
                line.Kind = hit.Kind;
                line.State = hit.State ?? "";
                line.Text = hit.Phrase;
                line.Y = hit.Y;
                line.Order = i;
                lines.Add(line);
            }
            lines.Sort(delegate(Line a, Line b)
            {
                int byY = a.Y.CompareTo(b.Y);
                if (byY != 0) return byY;
                return a.Order.CompareTo(b.Order);
            });
            return lines;
        }

        private static ChatHit HitAt(List<ChatHit> hits, Line line)
        {
            if (line == null || line.Order < 0 || line.Order >= hits.Count) return null;
            return hits[line.Order];
        }

        // The same warning, after it scrolls up or an OCR pass rewrites a few
        // letters, still matches the line from the previous read. A new copy
        // below the one already on screen does not.
        private bool SameAsPrevious(Line line, bool[] used)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                if (used[i]) continue;
                if (previous[i].Kind != line.Kind || previous[i].State != line.State) continue;
                if (!SameText(previous[i].Text, line.Text)) continue;
                used[i] = true;
                return true;
            }
            return false;
        }

        private static bool SameText(string a, string b)
        {
            string fa = ChatPhrase.Fold(a ?? "");
            string fb = ChatPhrase.Fold(b ?? "");
            if (fa == fb) return true;
            if (fa.Length == 0 || fb.Length == 0) return false;
            if (fa.IndexOf(fb) >= 0 || fb.IndexOf(fa) >= 0) return true;
            int limit = Math.Max(fa.Length, fb.Length) / 3;
            if (limit < 4) limit = 4;
            return Edit(fa, fb) <= limit;
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
                int previous = row[0];
                row[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int next = previous + cost;
                    int del = row[j] + 1;
                    int ins = row[j - 1] + 1;
                    if (del < next) next = del;
                    if (ins < next) next = ins;
                    previous = row[j];
                    row[j] = next;
                }
            }
            return row[m];
        }

        private bool Cooling(string kind, string state, DateTime now)
        {
            for (int i = 0; i < cool.Count; i++)
            {
                if (cool[i].Kind != kind || cool[i].State != state) continue;
                return (now - cool[i].Fired).TotalSeconds < CooldownSeconds;
            }
            return false;
        }

        private void Stamp(string kind, string state, DateTime now)
        {
            for (int i = 0; i < cool.Count; i++)
            {
                if (cool[i].Kind == kind && cool[i].State == state)
                {
                    cool[i].Fired = now;
                    return;
                }
            }
            Cool add = new Cool();
            add.Kind = kind;
            add.State = state;
            add.Fired = now;
            cool.Add(add);
        }

        private void ForgetKind(string kind)
        {
            for (int i = cool.Count - 1; i >= 0; i--)
                if (cool[i].Kind == kind) cool.RemoveAt(i);
        }

        // A drink line re-arms that potion so the next warning can fire.
        public static bool Drink(string text)
        {
            if (text == null) return false;
            string lower = text.ToLowerInvariant();
            if (lower.IndexOf("elder overload salve") >= 0) return true;
            if (lower.IndexOf("aggroverload") >= 0) return true;
            if (lower.IndexOf("supreme overload") >= 0) return true;
            if (lower.IndexOf("overload salve") >= 0) return true;
            if (lower.IndexOf("searing overload") >= 0) return true;
            if (lower.IndexOf("holy overload") >= 0) return true;
            if (lower.IndexOf("perfect plus") >= 0) return true;
            if (lower.IndexOf("antifire potion") >= 0 && lower.IndexOf("run out") < 0 && lower.IndexOf("worn") < 0) return true;
            if (lower.IndexOf("wyrmfire") >= 0) return true;
            if (lower.IndexOf("antipoison") >= 0 && lower.IndexOf("run out") < 0 && lower.IndexOf("expired") < 0) return true;
            return false;
        }

        private void Set(string kind, string state, DateTime now)
        {
            for (int i = 0; i < pots.Count; i++)
            {
                if (pots[i].Kind == kind)
                {
                    bool changed = state != pots[i].State;
                    pots[i].State = state;
                    if (state == "active" || changed) pots[i].Since = now;
                    return;
                }
            }
            Pot pot = new Pot();
            pot.Kind = kind;
            pot.State = state;
            if (state == "active" || state == "expired" || state == "low") pot.Since = now;
            pots.Add(pot);
        }

        private string Roster()
        {
            string[] order = new string[] { "elder", "aggression" };
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            for (int i = 0; i < order.Length; i++)
            {
                string state = StateOf(order[i]);
                if (state.Length == 0) continue;
                if (line.Length > 0) line.Append(" · ");
                line.Append(Short(order[i])).Append(" ").Append(LabelFor(order[i], state));
            }
            return line.ToString();
        }

        private string StateOf(string kind)
        {
            for (int i = 0; i < pots.Count; i++)
                if (pots[i].Kind == kind) return pots[i].State ?? "";
            return "";
        }

        public static string Label(string kind)
        {
            if (kind == "elder") return "Elder overload";
            if (kind == "aggression") return "Aggression";
            return "Potion";
        }

        public static string Short(string kind)
        {
            if (kind == "elder") return "Elder overload";
            if (kind == "aggression") return "Aggression";
            return "";
        }

        public static string ShortState(string state)
        {
            if (state == "low") return "low";
            if (state == "expired") return "expired";
            if (state == "active") return "Active";
            return "";
        }

        // Overload potions last six minutes. Other chat pots stay "Active" with no guessed clock.
        public static int ActiveSeconds(string kind)
        {
            if (kind == "elder" || kind == "overload") return 360;
            return 0;
        }

        // Low is orange and expired is red. Active keeps the panel text colour.
        public static int ValueArgb(string part)
        {
            if (part != null && part.IndexOf("expired") >= 0) return unchecked((int)0xFFE64640);
            if (part != null && part.IndexOf("low") >= 0) return unchecked((int)0xFFFF9628);
            return 0;
        }

        private string LabelFor(string kind, string state)
        {
            if (state != "active") return ShortState(state);
            int seconds = ActiveSeconds(kind);
            if (seconds <= 0) return "Active";
            DateTime since = DateTime.MinValue;
            for (int i = 0; i < pots.Count; i++)
                if (pots[i].Kind == kind) since = pots[i].Since;
            if (since == DateTime.MinValue) return "Active";
            int left = seconds - (int)(DateTime.UtcNow - since).TotalSeconds;
            if (left < 0) left = 0;
            return (left / 60).ToString() + ":" + (left % 60).ToString("00");
        }

        public static string Words(string state)
        {
            if (state == "low") return "running low";
            if (state == "expired") return "expired";
            if (state == "active") return "active";
            return "";
        }

        public static string Title(string kind, string state)
        {
            string name = Label(kind);
            if (state == "expired") return name + " has expired";
            if (state == "active") return name + " is active";
            return name + " is running low";
        }
    }
}
