using System;

namespace KavrosAfkWarden
{
    public static class WindowScore
    {
        private static bool Has(string text, string value)
        {
            return (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsLauncher(string title, string process, string className)
        {
            bool gameTitle = Has(title, "nxt") || Has(title, "944") || Has(className, "jag") || Has(className, "glfw");
            return Has(title, "launcher") || Has(className, "launcher") || (Has(process, "launcher") && !gameTitle);
        }

        public static int Score(string title, string process, string className, int width, int height)
        {
            if (Has(title, "AFK Warden") || Has(process, "KavrosAfkWarden")) return -1000;
            if (Has(title, "Companion") || Has(process, "Companion")) return -1000;
            if (Has(title, "Clue Helper") || Has(title, "ClueHelper") || Has(process, "ClueHelper") || Has(process, "Clue Helper")) return -1000;
            if (Has(className, "CabinetWClass") || Has(className, "WinRarWindow") || Has(className, "Chrome_WidgetWin") || Has(className, "ApplicationFrameWindow")) return -1000;
            if (IsLauncher(title, process, className)) return -1000;
            if (!Has(title, "kavros")) return -1000;
            int score = 400;
            if (Has(process, "kavros")) score += 80;
            if (Has(className, "jag") || Has(className, "glfw") || Has(className, "lwjgl")) score += 70;
            if (width >= 600 && height >= 350) score += 40;
            if (width < 250 || height < 150) score -= 250;
            return score;
        }
    }
}
