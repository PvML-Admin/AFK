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

        // Topmost only while the player is looking at the game (or at the panel
        // or Settings themselves), and briefly after an explicit launch so a
        // Companion start cannot leave it behind a client that is about to be
        // found. When another application is in front the panel drops back
        // under it, so it never floats over the browser or Discord.
        public static bool WantTopMost(bool gameExists, bool minimized, bool gameForeground, bool ownForeground, bool launchGrace)
        {
            if (minimized) return false;
            if (launchGrace) return true;
            if (ownForeground) return true;
            return gameExists && gameForeground;
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

    // PrintWindow paints the whole window in physical pixels. The client area
    // sits inside that bitmap at the difference of the two screen rects.
    // The scale factor is not applied: 150% and 200% stay in physical pixels.
    public static class WindowCrop
    {
        public static void Client(int windowX, int windowY, int windowW, int windowH, int clientX, int clientY, int clientW, int clientH, out int ox, out int oy, out int w, out int h)
        {
            ox = clientX - windowX;
            oy = clientY - windowY;
            if (ox < 0) ox = 0;
            if (oy < 0) oy = 0;
            w = clientW;
            h = clientH;
            if (ox + w > windowW) w = windowW - ox;
            if (oy + h > windowH) h = windowH - oy;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
        }
    }

    // Steady state copies the locked slots twice a second. A full client
    // search is only for startup, a lost lock, and a slow refresh.
    public static class CapturePlan
    {
        public const int SampleMilliseconds = 500;
        public const int FullIntervalSeconds = 30;
        // Buff timers, the XP counter and the backpack change slowly. They are
        // copied on every other sample, about once a second.
        public const int SlowSlotEvery = 2;

        public static bool WantFull(bool locked, DateTime now, DateTime nextFull)
        {
            return now >= nextFull;
        }

        // Seconds until the next whole-client copy. A complete read waits the
        // slow refresh (shorter while no buff bar is locked). A failed read
        // backs off 3, 6, 12, 24 then 30 seconds, so a hidden HUD, an empty
        // prayer bar or a loading screen does not copy the whole client every
        // three seconds.
        // A minimized client has no picture. Any other client is read, focused or not.
        // A region that is actually on screen stays a tiny CopyFromScreen. A region
        // hidden behind another window is taken from the game window itself.
        public static bool ShouldRead(bool minimized, bool paused)
        {
            return !minimized && !paused;
        }

        // Focus and cover do not stop a read. Only a minimized client has no picture.
        public static bool ContinueReading(bool minimized, bool focused, bool covered)
        {
            return !minimized;
        }

        public static bool PreferWindowPixels(bool regionOnScreen)
        {
            return !regionOnScreen;
        }

        public static int NextFullSeconds(bool lifeAndPrayerRead, bool buffLocked, int failedInARow)
        {
            if (lifeAndPrayerRead) return buffLocked ? FullIntervalSeconds : 15;
            int wait = 3;
            for (int i = 1; i < failedInARow && wait < FullIntervalSeconds; i++) wait *= 2;
            return Math.Min(wait, FullIntervalSeconds);
        }

        // Copy two HUD slots in one screen grab when they sit on the same row
        // and the band between them is small. One grab instead of two halves
        // the screen reads.
        public static bool ShareGrab(IntRect a, IntRect b)
        {
            if (a.Width < 4 || b.Width < 4) return false;
            int left = Math.Min(a.X, b.X), top = Math.Min(a.Y, b.Y);
            int right = Math.Max(a.Right, b.Right), bottom = Math.Max(a.Bottom, b.Bottom);
            long union = (long)(right - left) * (bottom - top);
            long parts = (long)a.Width * a.Height + (long)b.Width * b.Height;
            return union <= parts * 3 && bottom - top <= Math.Max(a.Height, b.Height) * 2;
        }
    }
}
