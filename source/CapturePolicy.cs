using System;

namespace KavrosAfkWarden
{
    // Sit inside the chosen client at a saved offset. Hide only while that
    // client is minimized. Other applications cover the panel by z-order.
    public static class PanelDock
    {
        public static void Place(int gameX, int gameY, int gameW, int gameH, int panelW, int panelH, int offsetX, int offsetY, int deskX, int deskY, int deskW, int deskH, out int x, out int y)
        {
            int areaX = gameX, areaY = gameY, areaW = gameW, areaH = gameH;
            int overlapW = Math.Min(gameX + gameW, deskX + deskW) - Math.Max(gameX, deskX);
            int overlapH = Math.Min(gameY + gameH, deskY + deskH) - Math.Max(gameY, deskY);
            if (overlapW >= panelW && overlapH >= panelH)
            {
                areaX = Math.Max(gameX, deskX);
                areaY = Math.Max(gameY, deskY);
                areaW = overlapW;
                areaH = overlapH;
            }
            else
            {
                areaX = deskX;
                areaY = deskY;
                areaW = deskW;
                areaH = deskH;
            }
            x = gameX + offsetX;
            y = gameY + offsetY;
            if (x < areaX) x = areaX;
            if (y < areaY) y = areaY;
            if (x > areaX + areaW - panelW) x = areaX + areaW - panelW;
            if (y > areaY + areaH - panelH) y = areaY + areaH - panelH;
        }

        public static bool Visible(bool enabled, bool gameExists, bool minimized, bool gameForeground, bool panelFocused, bool settingsFocused)
        {
            // Owned by the game window, so other applications cover it. Only a
            // minimized client should hide the panel. gameForeground is kept
            // in the signature so callers stay explicit.
            if (!enabled || minimized) return false;
            if (!gameExists && !panelFocused && !settingsFocused) return true;
            return true;
        }
    }

    // How a frame is obtained. Focus is intentionally not an input: an
    // unobstructed background client is readable.
    public enum CaptureRoute
    {
        Missing,
        Minimized,
        Covered,
        Screen,
        WindowSurface
    }

    public static class CapturePolicy
    {
        public static CaptureRoute Choose(bool attached, bool minimized, bool onScreen, bool occluded, bool surfaceAvailable)
        {
            if (!attached) return CaptureRoute.Missing;
            // A minimized game is not composed. Do not keep serving the last frame.
            if (minimized) return CaptureRoute.Minimized;
            // The window surface is valid while another app is focused and while
            // other windows cover the client. Prefer it whenever the OS offers it.
            if (surfaceAvailable) return CaptureRoute.WindowSurface;
            // Whatever is on the monitor. This does
            // not require the game to be the foreground window.
            if (onScreen && !occluded) return CaptureRoute.Screen;
            return CaptureRoute.Covered;
        }

        public static bool PixelsAvailable(CaptureRoute route)
        {
            return route == CaptureRoute.Screen || route == CaptureRoute.WindowSurface;
        }
    }

    // Nine sample points across the client, so a
    // client that is only partly covered can still be read from the screen.
    public static class VisibilitySamples
    {
        public static void Fill(int left, int top, int width, int height, int[] xs, int[] ys)
        {
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = y * 3 + x;
                    xs[i] = left + (2 * x + 1) * width / 6;
                    ys[i] = top + (2 * y + 1) * height / 6;
                }
        }

        public static bool Any(bool[] belongsToGame)
        {
            for (int i = 0; i < belongsToGame.Length; i++) if (belongsToGame[i]) return true;
            return false;
        }

        public static bool MostlyClear(bool[] belongsToGame)
        {
            if (belongsToGame.Length < 9 || !belongsToGame[4]) return false;
            int visible = 0;
            for (int i = 0; i < belongsToGame.Length; i++) if (belongsToGame[i]) visible++;
            return visible >= 7;
        }
    }

    // Steady state copies the locked slots twice a second. A full client
    // search is only for startup, a lost lock, and a slow refresh.
    public static class CapturePlan
    {
        public const int SampleMilliseconds = 500;
        public const int FullIntervalSeconds = 30;

        public static bool WantFull(bool locked, DateTime now, DateTime nextFull)
        {
            return now >= nextFull;
        }
    }
}
