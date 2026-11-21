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
        public string Detail = "";

        public List<ChatNotice> Observe(List<ChatHit> hits)
        {
            List<ChatNotice> notices = new List<ChatNotice>();
            if (hits == null) hits = new List<ChatHit>();
            for (int i = 0; i < hits.Count; i++)
            {
                ChatHit hit = hits[i];
                if (hit == null || hit.Phrase == null || hit.Kind == null) continue;
                if (Drink(hit.Phrase))
                {
                    Set(hit.Kind, "active");
                    continue;
                }
                if (!Fresh(hit)) continue;
                Set(hit.Kind, hit.State);
                ChatNotice notice = new ChatNotice();
                notice.Kind = "potion";
                notice.Title = Title(hit.Kind, hit.State);
                notice.Detail = notice.Title;
                notices.Add(notice);
            }
            Detail = Roster();
            return notices;
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
                line.Append(Label(order[i])).Append(": ").Append(Words(state));
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
