using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace KavrosAfkWarden
{
    internal sealed class GameWindow
    {
        internal IntPtr Handle;
        internal string Title = "";
        internal string ProcessName = "";
        internal string ClassName = "";
        internal Rectangle Client;
        public override string ToString() { return Title; }
    }

    internal static class WindowLocator
    {
        internal static List<GameWindow> Enumerate(int excludedProcessId)
        {
            List<GameWindow> list = new List<GameWindow>();
            Dictionary<uint, string> names = new Dictionary<uint, string>();
            NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (!NativeMethods.IsWindowVisible(window)) return true;
                uint pid;
                NativeMethods.GetWindowThreadProcessId(window, out pid);
                if (pid == excludedProcessId) return true;
                StringBuilder title = new StringBuilder(512);
                NativeMethods.GetWindowText(window, title, title.Capacity);
                if (title.Length == 0) return true;
                StringBuilder className = new StringBuilder(256);
                NativeMethods.GetClassName(window, className, className.Capacity);
                string process;
                if (!names.TryGetValue(pid, out process))
                {
                    try { using (Process found = Process.GetProcessById((int)pid)) process = found.ProcessName; }
                    catch (Exception) { process = ""; }
                    names[pid] = process;
                }
                GameWindow game = new GameWindow();
                game.Handle = window;
                game.Title = title.ToString();
                game.ProcessName = process;
                game.ClassName = className.ToString();
                game.Client = NativeMethods.ClientBounds(window);
                list.Add(game);
                return true;
            }, IntPtr.Zero);
            list.Sort(delegate(GameWindow a, GameWindow b) { return Score(b).CompareTo(Score(a)); });
            return list;
        }

        internal static int Score(GameWindow window)
        {
            if (window == null) return -1000;
            return WindowScore.Score(window.Title, window.ProcessName, window.ClassName, window.Client.Width, window.Client.Height);
        }

        internal static GameWindow Choose(List<GameWindow> windows, IntPtr current, string rememberedTitle)
        {
            GameWindow currentWindow = null;
            GameWindow titled = null;
            GameWindow only = null;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (Score(windows[i]) < 100) continue;
                count++;
                if (only == null) only = windows[i];
                if (windows[i].Handle == current) currentWindow = windows[i];
                if (rememberedTitle != null && rememberedTitle.Length > 0 && string.Equals(windows[i].Title, rememberedTitle, StringComparison.Ordinal)) titled = windows[i];
            }
            if (currentWindow != null) return currentWindow;
            if (titled != null) return titled;
            if (count == 1) return only;
            return null;
        }

        internal static string ClientLabel(GameWindow window, int index)
        {
            string title = window == null ? "" : window.Title ?? "";
            if (title.Length > 36) title = title.Substring(0, 36);
            return "Client " + (index + 1).ToString() + ": " + title;
        }

        internal static bool IsForeground(IntPtr game)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || game == IntPtr.Zero) return false;
            return foreground == game || NativeMethods.GetAncestor(foreground, 2) == game;
        }

        internal static bool OverGame(IntPtr game, int x, int y)
        {
            IntPtr hit = NativeMethods.WindowFromPoint(new NativeMethods.PointStruct { X = x, Y = y });
            if (hit == IntPtr.Zero) return false;
            return hit == game || NativeMethods.GetAncestor(hit, 2) == game;
        }
    }

    internal sealed class FrameGrab
    {
        internal CaptureRoute Route = CaptureRoute.Missing;
        internal bool Focused;
        internal bool Input;
        internal ImageBuffer Image;
        internal string Note = "Waiting for the Kavros window.";
        private readonly WindowSurfaceCapture surface = new WindowSurfaceCapture();
        private Point lastCursor;
        private bool haveCursor;
        private Bitmap scratch;
        private int[] scratchPixels;
        private byte[] scratchRaw;
        private IntRect lifeBox;
        private IntRect prayerBox;
        private readonly List<IntRect> buffBoxes = new List<IntRect>();
        private bool haveBoxes;
        private DateTime nextFullSearch = DateTime.MinValue;
        internal int LastTickMs;
        internal HudReading Reading = new HudReading();

        internal void Tick(IntPtr hwnd, WardenSettings settings, bool capture)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Image = null;
            Input = false;
            Focused = false;
            Reading = new HudReading();
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                Route = CaptureRoute.Missing;
                Note = "Waiting for the Kavros window.";
                haveCursor = false;
                haveBoxes = false;
                LastTickMs = (int)clock.ElapsedMilliseconds;
                return;
            }
            bool iconic = NativeMethods.IsIconic(hwnd);
            Focused = WindowLocator.IsForeground(hwnd);
            Rectangle client = iconic ? Rectangle.Empty : NativeMethods.ClientBounds(hwnd);
            if (!capture || iconic)
            {
                Route = iconic ? CaptureRoute.Minimized : Route;
                Input = PollInput(hwnd, client, iconic);
                Note = iconic ? Describe() : "Paused while the panel is dragged.";
                LastTickMs = (int)clock.ElapsedMilliseconds;
                return;
            }
            // Windows Graphics Capture draws a yellow border unless IsBorderRequired
            // can be cleared. That property is not on the Windows 10 2004 contract
            // this build uses, so capture stays on the screen path.
            surface.Ensure(hwnd);
            bool surfaceReady = surface.Available;
            Rectangle desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
            bool onScreen = !client.IsEmpty && Rectangle.Intersect(client, desktop).Width >= 80 && Rectangle.Intersect(client, desktop).Height >= 80;
            bool occluded = false;
            Route = CapturePolicy.Choose(true, iconic, onScreen, occluded, surfaceReady);
            try
            {
                if (Route == CaptureRoute.WindowSurface)
                {
                    Rectangle window = NativeMethods.WindowBounds(hwnd);
                    ImageBuffer frame = surface.Copy(client.X - window.X, client.Y - window.Y, client.Width, client.Height);
                    if (frame == null || ImageBuffer.MostlyBlack(frame)) Route = onScreen ? CaptureRoute.Screen : CaptureRoute.Covered;
                    else Image = frame;
                }
                if (Route == CaptureRoute.Screen || Image == null)
                {
                    bool full = !haveBoxes || DateTime.UtcNow >= nextFullSearch;
                    if (full)
                    {
                        Image = client.Width >= 80 && client.Height >= 80 ? CopyScreen(client) : null;
                        if (Image != null && !ImageBuffer.MostlyBlack(Image))
                        {
                            Reading = HudReader.Read(Image, settings);
                            RememberBoxes(Reading);
                            nextFullSearch = DateTime.UtcNow.AddSeconds(haveBoxes ? 4 : 1);
                        }
                        else
                        {
                            nextFullSearch = DateTime.UtcNow.AddSeconds(1);
                            Note = "The captured frame is black. Pin the bars, or turn on Save debug capture.";
                        }
                    }
                    else Reading = ReadLocked(client);
                }
                else if (Image != null)
                {
                    Reading = HudReader.Read(Image, settings);
                    RememberBoxes(Reading);
                }
            }
            catch (Exception) { Image = null; Route = CaptureRoute.Covered; }
            IntRect world = MotionDetector.WorldRect(Math.Max(client.Width, 1), Math.Max(client.Height, 1));
            ImageBuffer crop = CopyScreen(new Rectangle(client.X + world.X, client.Y + world.Y, Math.Min(world.Width, 220), Math.Min(world.Height, 140)));
            motionMoved = crop != null && motion.MovedCrop(crop);
            Moving = motionMoved;
            Input = PollInput(hwnd, client, iconic);
            Note = Describe();
            LastTickMs = (int)clock.ElapsedMilliseconds;
        }

        private readonly MotionDetector motion = new MotionDetector();
        internal bool Moving;
        private bool motionMoved;

        private void RememberBoxes(HudReading reading)
        {
            if (reading != null && reading.Hp != null && reading.Prayer != null && reading.LifeText.Width > 8 && reading.PrayerText.Width > 8)
            {
                lifeBox = Pad(reading.LifeText, 6);
                prayerBox = Pad(reading.PrayerText, 6);
                buffBoxes.Clear();
                if (reading.BuffRects != null)
                    for (int i = 0; i < reading.BuffRects.Count; i++) buffBoxes.Add(Pad(reading.BuffRects[i], 2));
                haveBoxes = true;
            }
        }

        private HudReading ReadLocked(Rectangle client)
        {
            HudReading reading = new HudReading();
            reading.StripFound = true;
            reading.Buffs = new List<BuffIcon>();
            reading.BuffRects = buffBoxes;
            ImageBuffer life = CopyScreen(ScreenOf(client, lifeBox));
            if (life != null) HudReader.ReadBox(life, true, reading);
            ImageBuffer prayer = CopyScreen(ScreenOf(client, prayerBox));
            if (prayer != null) HudReader.ReadBox(prayer, false, reading);
            reading.BuffBarFound = buffBoxes.Count > 0;
            for (int i = 0; i < buffBoxes.Count; i++)
            {
                ImageBuffer icon = CopyScreen(ScreenOf(client, buffBoxes[i]));
                if (icon == null) continue;
                reading.Buffs.Add(HudReader.ReadBuffCrop(icon, buffBoxes[i].X));
            }
            if (reading.Hp == null && reading.Prayer == null) haveBoxes = false;
            return reading;
        }

        private static IntRect Pad(IntRect box, int pad)
        {
            return new IntRect(box.X - pad, box.Y - pad, box.Width + pad * 2, box.Height + pad * 2);
        }

        private static Rectangle ScreenOf(Rectangle client, IntRect box)
        {
            return new Rectangle(client.X + box.X, client.Y + box.Y, box.Width, box.Height);
        }

        internal void Dispose() { surface.Dispose(); if (scratch != null) scratch.Dispose(); }

        private static bool AnySample(IntPtr game, Rectangle client)
        {
            int[] xs = new int[9];
            int[] ys = new int[9];
            VisibilitySamples.Fill(client.X, client.Y, client.Width, client.Height, xs, ys);
            bool[] hit = new bool[9];
            for (int i = 0; i < 9; i++) hit[i] = WindowLocator.OverGame(game, xs[i], ys[i]);
            return VisibilitySamples.Any(hit);
        }

        private bool PollInput(IntPtr game, Rectangle client, bool iconic)
        {
            NativeMethods.PointStruct cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return false;
            bool moved = haveCursor && (cursor.X - lastCursor.X) * (cursor.X - lastCursor.X) + (cursor.Y - lastCursor.Y) * (cursor.Y - lastCursor.Y) >= 9;
            lastCursor = new Point(cursor.X, cursor.Y);
            haveCursor = true;
            bool over = !iconic && client.Contains(cursor.X, cursor.Y) && WindowLocator.OverGame(game, cursor.X, cursor.Y);
            bool button = over && ((NativeMethods.GetAsyncKeyState(1) & 0x8000) != 0 || (NativeMethods.GetAsyncKeyState(2) & 0x8000) != 0);
            bool key = false;
            if (WindowLocator.IsForeground(game))
            {
                for (int vk = 8; vk <= 254; vk++)
                {
                    if (vk == 16 || vk == 17 || vk == 18) continue;
                    if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0) { key = true; break; }
                }
            }
            return InputActivity.IsActivity(iconic, WindowLocator.IsForeground(game), over, moved, button, key);
        }

        private string Describe()
        {
            if (Route == CaptureRoute.Minimized) return "Kavros is minimized. Hitpoints, prayer, potions and idle need the window restored. The AFK timer is still running.";
            if (Route == CaptureRoute.Covered) return "Kavros is covered and window capture is unavailable. Uncover the client to read hitpoints. The AFK timer is still running.";
            if (Route == CaptureRoute.WindowSurface) return Focused ? "Watching Kavros, including while other windows cover it." : "Watching Kavros in the background.";
            if (Route == CaptureRoute.Screen) return Focused ? "Watching the visible Kavros window." : "Watching Kavros. The window is visible but not focused.";
            return "Waiting for the Kavros window.";
        }

        private ImageBuffer CopyScreen(Rectangle bounds)
        {
            if (bounds.Width < 4 || bounds.Height < 4 || bounds.Width > 4096 || bounds.Height > 4096) return null;
            if (scratch == null || scratch.Width != bounds.Width || scratch.Height != bounds.Height)
            {
                if (scratch != null) scratch.Dispose();
                scratch = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                scratchPixels = new int[bounds.Width * bounds.Height];
                scratchRaw = null;
            }
            using (Graphics graphics = Graphics.FromImage(scratch))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            BitmapData data = scratch.LockBits(new Rectangle(0, 0, scratch.Width, scratch.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * scratch.Height;
                if (scratchRaw == null || scratchRaw.Length != bytes) scratchRaw = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, scratchRaw, 0, bytes);
                int[] pixels = new int[scratch.Width * scratch.Height];
                for (int y = 0; y < scratch.Height; y++)
                    for (int x = 0; x < scratch.Width; x++)
                    {
                        int i = y * data.Stride + x * 4;
                        pixels[y * scratch.Width + x] = ImageBuffer.Pack(scratchRaw[i + 2], scratchRaw[i + 1], scratchRaw[i]);
                    }
                return new ImageBuffer(scratch.Width, scratch.Height, pixels);
            }
            finally { scratch.UnlockBits(data); }
        }
    }
}
