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

        internal static GameWindow Choose(List<GameWindow> windows, IntPtr current)
        {
            GameWindow best = null;
            for (int i = 0; i < windows.Count; i++)
            {
                if (Score(windows[i]) < 100) continue;
                if (windows[i].Handle == current) return windows[i];
                if (best == null) best = windows[i];
            }
            return best;
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

        internal void Tick(IntPtr hwnd)
        {
            Image = null;
            Input = false;
            Focused = false;
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                Route = CaptureRoute.Missing;
                Note = "Waiting for the Kavros window.";
                haveCursor = false;
                return;
            }
            bool iconic = NativeMethods.IsIconic(hwnd);
            Focused = WindowLocator.IsForeground(hwnd);
            Rectangle client = iconic ? Rectangle.Empty : NativeMethods.ClientBounds(hwnd);
            Rectangle desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
            bool onScreen = !client.IsEmpty && Rectangle.Intersect(client, desktop).Width >= 80 && Rectangle.Intersect(client, desktop).Height >= 80;
            bool occluded = onScreen && !AnySample(hwnd, client);
            bool surfaceReady = false;
            if (!iconic)
            {
                surface.Ensure(hwnd);
                surfaceReady = surface.Available;
            }
            Route = CapturePolicy.Choose(true, iconic, onScreen, occluded, surfaceReady);
            if (Route == CaptureRoute.WindowSurface)
            {
                Rectangle window = NativeMethods.WindowBounds(hwnd);
                int cropX = client.X - window.X;
                int cropY = client.Y - window.Y;
                ImageBuffer frame = surface.Copy(cropX, cropY, client.Width, client.Height);
                if (frame == null && onScreen && !occluded) Route = CaptureRoute.Screen;
                else Image = frame;
                if (Image == null && Route == CaptureRoute.WindowSurface) Route = CaptureRoute.Covered;
            }
            if (Route == CaptureRoute.Screen)
            {
                try
                {
                    try { NativeMethods.DwmFlush(); } catch (DllNotFoundException) { }
                    Image = CopyScreen(client);
                }
                catch (Exception) { Image = null; Route = CaptureRoute.Covered; }
            }
            Input = PollInput(hwnd, client, iconic);
            Note = Describe();
        }

        internal void Dispose() { surface.Dispose(); }

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
            if (Route == CaptureRoute.Minimized) return "Kavros is minimized. Hitpoints, prayer and idle need the window restored. The AFK timer is still running.";
            if (Route == CaptureRoute.Covered) return "Kavros is covered and window capture is unavailable. Uncover the client to read hitpoints. The AFK timer is still running.";
            if (Route == CaptureRoute.WindowSurface) return Focused ? "Watching Kavros, including while other windows cover it." : "Watching Kavros in the background.";
            if (Route == CaptureRoute.Screen) return Focused ? "Watching the visible Kavros window." : "Watching Kavros. The window is visible but not focused.";
            return "Waiting for the Kavros window.";
        }

        private static ImageBuffer CopyScreen(Rectangle bounds)
        {
            if (bounds.Width < 80 || bounds.Height < 80) return null;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] pixels = new int[bitmap.Width * bitmap.Height];
                    byte[] raw = new byte[Math.Abs(data.Stride) * bitmap.Height];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                    for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int i = y * data.Stride + x * 4;
                            pixels[y * bitmap.Width + x] = ImageBuffer.Pack(raw[i + 2], raw[i + 1], raw[i]);
                        }
                    return new ImageBuffer(bitmap.Width, bitmap.Height, pixels);
                }
                finally { bitmap.UnlockBits(data); }
            }
        }
    }
}
