using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KavrosAfkWarden
{
    // Character names for the client picker. Companion writes them to
    // %AppData%\KavrosOverlay\client-names.json. The name is a label only.
    public sealed class ClientRoster
    {
        private readonly string path;
        private DateTime seen = DateTime.MinValue;
        private Dictionary<int, string> names = new Dictionary<int, string>();

        public ClientRoster(string path)
        {
            this.path = path ?? "";
        }

        public string Label(int pid, int index)
        {
            Refresh();
            string name;
            if (pid > 0 && names.TryGetValue(pid, out name) && name != null && name.Length > 0) return name;
            return "Client " + (index + 1).ToString();
        }

        private void Refresh()
        {
            try
            {
                if (path.Length == 0 || !File.Exists(path))
                {
                    if (names.Count > 0 || seen != DateTime.MinValue)
                    {
                        names = new Dictionary<int, string>();
                        seen = DateTime.MinValue;
                    }
                    return;
                }
                DateTime written = File.GetLastWriteTimeUtc(path);
                if (written == seen) return;
                string text;
                try { text = File.ReadAllText(path); }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { return; }
                if (!Complete(text))
                {
                    seen = written;
                    return;
                }
                names = Parse(text);
                seen = written;
            }
            catch (Exception) { }
        }

        public static Dictionary<int, string> Parse(string json)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            if (json == null || json.Length == 0) return map;
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] != '{') { i++; continue; }
                int end = EndObject(json, i);
                if (end < 0) break;
                int pid;
                string name;
                if (TryField(json, i, end, "pid", out pid) && TryName(json, i, end, out name))
                {
                    name = Sanitize(name);
                    if (pid > 0 && name != null) map[pid] = name;
                }
                i = end + 1;
            }
            return map;
        }

        public static string Sanitize(string value)
        {
            if (value == null || value.Length == 0) return null;
            StringBuilder sb = new StringBuilder(value.Length);
            bool space = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 32 && !char.IsWhiteSpace(c)) continue;
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length == 0 || space) continue;
                    space = true;
                    sb.Append(' ');
                    continue;
                }
                space = false;
                sb.Append(c);
                if (sb.Length >= 24) break;
            }
            string text = sb.ToString().Trim();
            return text.Length == 0 ? null : text;
        }

        private static bool Complete(string text)
        {
            if (text == null || text.Length == 0) return false;
            int depth = 0;
            bool inString = false;
            bool escape = false;
            bool any = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{' || c == '[') { depth++; any = true; }
                else if (c == '}' || c == ']') depth--;
            }
            return any && depth == 0 && !inString;
        }

        private static int EndObject(string json, int start)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static bool TryField(string json, int start, int end, string key, out int value)
        {
            value = 0;
            int at = Key(json, start, end, key);
            if (at < 0) return false;
            int i = at;
            while (i <= end && (json[i] == ' ' || json[i] == '\t' || json[i] == ':')) i++;
            int sign = 1;
            if (i <= end && json[i] == '-') { sign = -1; i++; }
            if (i > end || json[i] < '0' || json[i] > '9') return false;
            int number = 0;
            while (i <= end && json[i] >= '0' && json[i] <= '9')
            {
                number = number * 10 + (json[i] - '0');
                i++;
            }
            value = number * sign;
            return true;
        }

        private static bool TryName(string json, int start, int end, out string value)
        {
            value = null;
            int at = Key(json, start, end, "name");
            if (at < 0) return false;
            int i = at;
            while (i <= end && json[i] != '"') i++;
            if (i > end) return false;
            int stop;
            value = ReadString(json, i, out stop);
            return value != null && stop > i;
        }

        private static int Key(string json, int start, int end, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, start, end - start + 1, StringComparison.Ordinal);
            if (at < 0) return -1;
            return at + needle.Length;
        }

        private static string ReadString(string json, int quote, out int end)
        {
            end = quote;
            StringBuilder sb = new StringBuilder();
            int i = quote + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"') { end = i; return sb.ToString(); }
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    if (n == 'u' && i + 5 < json.Length)
                    {
                        int code = Hex(json, i + 2);
                        if (code >= 0) sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                    if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append('\t');
                    else sb.Append(n);
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            end = -1;
            return null;
        }

        private static int Hex(string text, int at)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                char c = text[at + i];
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                else return -1;
                value = value * 16 + digit;
            }
            return value;
        }
    }

    public static class ClientOrder
    {
        public static int Place(int pid, int[] pids)
        {
            if (pids == null || pid <= 0) return 0;
            int[] copy = new int[pids.Length];
            for (int i = 0; i < pids.Length; i++) copy[i] = pids[i];
            System.Array.Sort(copy);
            int place = 0;
            int last = int.MinValue;
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] <= 0 || copy[i] == last) continue;
                if (copy[i] == pid) return place;
                last = copy[i];
                place++;
            }
            return 0;
        }
    }

    public static class ClientNames
    {
        private static readonly ClientRoster Shared = new ClientRoster(DefaultPath());

        public static string Label(int pid, int index)
        {
            return Shared.Label(pid, index);
        }

        public static string DefaultPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KavrosOverlay", "client-names.json");
        }
    }
}
