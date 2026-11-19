namespace KavrosAfkWarden
{
    // How a frame is obtained. Focus is intentionally not an input: Clue Helper's
    // own capture tests treat an unobstructed background client as readable.
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
            // Same pixels Clue Helper reads: whatever is on the monitor. This does
            // not require the game to be the foreground window.
            if (onScreen && !occluded) return CaptureRoute.Screen;
            return CaptureRoute.Covered;
        }

        public static bool PixelsAvailable(CaptureRoute route)
        {
            return route == CaptureRoute.Screen || route == CaptureRoute.WindowSurface;
        }
    }

    // Nine sample points, matching Clue Helper's GameVisibility layout, so a
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
}
