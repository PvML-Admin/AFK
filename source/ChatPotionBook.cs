using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class ChatNotice
    {
        public string Kind;
        public string Title;
        public string Detail;
    }

    // Potion alerts come from chat lines. The same line, including after it
    // scrolls up, does not fire again. A later copy lower on the screen does.
    public sealed class ChatPotionBook
    {
        private sealed class Seen
        {
            public string Key;
        }

        private sealed class Pot
        {
            public string Kind;
            public string State;
            public DateTime Since;
        }

        private readonly List<Seen> seen = new List<Seen>();
        private readonly List<Pot> pots = new List<Pot>();
        private bool primed = true;
        public string Detail = "";
        public string Log = "";

        // Lines already on screen when the client is attached are not alerts.
        public void Attach()
        {
            primed = false;
            seen.Clear();
        }

        public List<ChatNotice> Observe(List<ChatHit> hits)
        {
            List<ChatNotice> notices = new List<ChatNotice>();
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            if (hits == null) hits = new List<ChatHit>();
            if (!primed)
            {
                // Only the lines in this first read are old. An empty read still counts,
                // so a pot line that shows up afterwards is not treated as preexisting.
                for (int i = 0; i < hits.Count; i++)
                {
                    Remember(hits[i]);
                    Append(log, hits[i], "skip-preexisting");
                }
                primed = true;
                Log = log.ToString();
                Detail = Roster();
                return notices;
            }
            for (int i = 0; i < hits.Count; i++)
            {
                ChatHit hit = hits[i];
                if (hit == null || hit.Phrase == null || hit.Kind == null) continue;
                if (Drink(hit.Phrase))
                {
                    Set(hit.Kind, "active");
                    ForgetKind(hit.Kind);
                    Remember(hit);
                    Append(log, hit, "rearm");
                    continue;
                }
                if (Already(hit))
                {
                    Append(log, hit, "skip-seen");
                    continue;
                }
                Set(hit.Kind, hit.State);
                ChatNotice notice = new ChatNotice();
                notice.Kind = "potion";
                notice.Title = Title(hit.Kind, hit.State);
                notice.Detail = notice.Title;
                notices.Add(notice);
                Append(log, hit, "alert");
            }
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
                if (pots[i].State != "expired" || pots[i].Since == DateTime.MinValue) continue;
                if ((now - pots[i].Since).TotalSeconds >= 45) pots.RemoveAt(i);
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

        private static string KeyOf(ChatHit hit)
        {
            if (hit == null) return "";
            return (hit.Kind ?? "") + "|" + (hit.State ?? "") + "|" + (hit.Phrase ?? "");
        }

        private void Remember(ChatHit hit)
        {
            string key = KeyOf(hit);
            if (key.Length == 0) return;
            for (int i = 0; i < seen.Count; i++)
                if (seen[i].Key == key) return;
            Seen add = new Seen();
            add.Key = key;
            seen.Add(add);
        }

        private bool Already(ChatHit hit)
        {
            string key = KeyOf(hit);
            for (int i = 0; i < seen.Count; i++)
                if (seen[i].Key == key) return true;
            Remember(hit);
            return false;
        }

        private void ForgetKind(string kind)
        {
            string prefix = (kind ?? "") + "|";
            for (int i = seen.Count - 1; i >= 0; i--)
                if (seen[i].Key != null && seen[i].Key.StartsWith(prefix)) seen.RemoveAt(i);
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

        private void Set(string kind, string state)
        {
            for (int i = 0; i < pots.Count; i++)
            {
                if (pots[i].Kind == kind)
                {
                    bool becameExpired = state == "expired" && pots[i].State != "expired";
                    pots[i].State = state;
                    if (state == "active" || becameExpired) pots[i].Since = DateTime.UtcNow;
                    return;
                }
            }
            Pot pot = new Pot();
            pot.Kind = kind;
            pot.State = state;
            if (state == "active" || state == "expired") pot.Since = DateTime.UtcNow;
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
