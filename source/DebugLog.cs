using System.IO;

namespace KavrosAfkWarden
{
    // The debug log keeps a few megabytes, then drops the older half on a line boundary.
    public static class DebugLog
    {
        public const int CapBytes = 5 * 1024 * 1024;
        public const int KeepBytes = 2 * 1024 * 1024;

        public static bool OverCap(long length)
        {
            return length > CapBytes;
        }

        public static string Tail(string text, int keep)
        {
            if (text == null) return "";
            if (keep < 1 || text.Length <= keep) return text;
            int start = text.Length - keep;
            int newline = text.IndexOf('\n', start);
            if (newline >= 0 && newline + 1 < text.Length) start = newline + 1;
            return text.Substring(start);
        }

        public static void TrimFile(string path)
        {
            TrimFile(path, CapBytes, KeepBytes);
        }

        public static void TrimFile(string path, int cap, int keep)
        {
            if (path == null || path.Length == 0 || cap < 1 || keep < 1) return;
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length <= cap) return;
            string tail;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length > keep) stream.Position = stream.Length - keep;
                using (StreamReader reader = new StreamReader(stream))
                    tail = reader.ReadToEnd();
            }
            File.WriteAllText(path, Tail(tail, keep));
        }
    }
}
