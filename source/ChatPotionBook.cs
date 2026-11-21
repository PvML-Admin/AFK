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
            public string Phrase;
            public int Y;
        }

        private sealed class Pot
        {
            public string Kind;
            public string State;
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
                for (int i = 0; i < hits.Count; i++)
                {
                    Remember(hits[i]);
                    Append(log, hits[i], "skip-preexisting");
                }
                if (hits.Count > 0) primed = true;
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
                    Remember(hit);
                    Append(log, hit, "rearm");
                    continue;
                }
                if (!Fresh(hit))
                {
                    Append(log, hit, "skip-seen");
                    continue;
                }
                // An expired pot stays quiet until a drink or a warning re-arms it.
                if (hit.State == "expired" && StateOf(hit.Kind) == "expired")
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

        private static void Append(System.Text.StringBuilder log, ChatHit hit, string decision)
        {
            if (hit == null) return;
            if (log.Length > 0) log.Append(" | ");
            hit.Decision = decision;
            log.Append(decision).Append(" ").Append(hit.Kind).Append(" ").Append(hit.State)
                .Append(" score=").Append(hit.Score.ToString("0.00")).Append(" \"").Append(hit.Phrase).Append("\"");
        }

        private void Remember(ChatHit hit)
        {
            if (hit == null || hit.Phrase == null) return;
            for (int i = 0; i < seen.Count; i++)
            {
                if (seen[i].Phrase == hit.Phrase) { seen[i].Y = hit.Y; return; }
            }
            Seen add = new Seen();
            add.Phrase = hit.Phrase;
            add.Y = hit.Y;
            seen.Add(add);
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

        private bool Fresh(ChatHit hit)
        {
            for (int i = 0; i < seen.Count; i++)
            {
                Seen old = seen[i];
                if (old.Phrase != hit.Phrase) continue;
                // Same line, or the chat scrolled it upward. A new copy sits lower.
                int dy = hit.Y - old.Y;
                if (dy <= 8 && dy > -80)
                {
                    old.Y = hit.Y;
                    return false;
                }
            }
            Seen add = new Seen();
            add.Phrase = hit.Phrase;
            add.Y = hit.Y;
            seen.Add(add);
            return true;
        }

        private void Set(string kind, string state)
        {
            for (int i = 0; i < pots.Count; i++)
            {
                if (pots[i].Kind == kind)
                {
                    pots[i].State = state;
                    return;
                }
            }
            Pot pot = new Pot();
            pot.Kind = kind;
            pot.State = state;
            pots.Add(pot);
        }

        private string Roster()
        {
            string[] order = new string[] { "elder", "overload", "aggression", "prayer", "antifire", "poison" };
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            for (int i = 0; i < order.Length; i++)
            {
                string state = StateOf(order[i]);
                if (state.Length == 0) continue;
                if (line.Length > 0) line.Append(" · ");
                line.Append(Short(order[i])).Append(" ").Append(ShortState(state));
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
            if (kind == "elder") return "Elder";
            if (kind == "overload") return "Overload";
            if (kind == "aggression") return "Aggression";
            if (kind == "prayer") return "Prayer renewal";
            if (kind == "antifire") return "Antifire";
            if (kind == "poison") return "Antipoison";
            return "Potion";
        }

        public static string Short(string kind)
        {
            if (kind == "elder") return "Elder";
            if (kind == "overload") return "Ovl";
            if (kind == "aggression") return "Aggro";
            if (kind == "prayer") return "Pray";
            if (kind == "antifire") return "Anti";
            if (kind == "poison") return "Poison";
            return "Pot";
        }

        public static string ShortState(string state)
        {
            if (state == "low") return "low";
            if (state == "expired") return "expired";
            if (state == "active") return "ok";
            return "";
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
