using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;

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
        internal int LastTickMs;
        internal HudReading Reading = new HudReading();
        internal bool XpActivity;
        internal bool PackActivity;
        internal Rectangle PanelBounds;

        private readonly object gate = new object();
        private IntPtr requestHwnd;
        private WardenSettings requestSettings = new WardenSettings();
        private bool requestCapture = true;
        private Rectangle requestPanel;
        private Thread worker;
        private volatile bool alive;
        private HudReading published = new HudReading();
        private CaptureRoute publishedRoute = CaptureRoute.Missing;
        private bool publishedFocused;
        private bool publishedInput;
        private string publishedNote = "Waiting for the Kavros window.";
        private bool publishedXp;
        private bool publishedPack;
        private int publishedMs;
        private readonly bool[] keyDown = new bool[256];
        private IntRect lifeBox;
        private IntRect prayerBox;
        private IntRect buffStrip;
        private IntRect xpBox;
        private IntRect packBox;
        private bool haveBoxes;
        private int missedLocks;
        private int lastXpHash;
        private int lastPackHash;
        private bool haveXpHash;
        private bool havePackHash;
        private DateTime nextFullSearch = DateTime.MinValue;
        private DateTime nextDebugImage = DateTime.MinValue;
        private readonly Bitmap[] pads = new Bitmap[5];
        private readonly byte[][] raws = new byte[5][];

        internal void Start()
        {
            if (worker != null) return;
            alive = true;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "AfkCapture";
            worker.Start();
        }

        internal void Stop()
        {
            alive = false;
            if (worker != null && worker.IsAlive)
            {
                try { worker.Join(400); } catch (Exception) { }
            }
            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i] != null) pads[i].Dispose();
                pads[i] = null;
            }
        }

        internal void Tick(IntPtr hwnd, WardenSettings settings, bool capture)
        {
            lock (gate)
            {
                requestHwnd = hwnd;
                requestSettings = settings ?? new WardenSettings();
                requestCapture = capture;
                requestPanel = PanelBounds;
                Reading = published;
                Route = publishedRoute;
                Focused = publishedFocused;
                Input = publishedInput;
                publishedInput = false;
                Note = publishedNote;
                XpActivity = publishedXp;
                PackActivity = publishedPack;
                LastTickMs = publishedMs;
                Image = null;
            }
        }

        private void Loop()
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; } catch (Exception) { }
            DateTime nextSample = DateTime.MinValue;
            while (alive)
            {
                PollKeys();
                if (DateTime.UtcNow >= nextSample)
                {
                    var clock = Stopwatch.StartNew();
                    try { Sample(); }
                    catch (Exception) { }
                    lock (gate) { publishedMs = (int)clock.ElapsedMilliseconds; }
                    nextSample = DateTime.UtcNow.AddMilliseconds(CapturePlan.SampleMilliseconds);
                }
                Thread.Sleep(30);
            }
        }

        private void PollKeys()
        {
            IntPtr hwnd;
            lock (gate) { hwnd = requestHwnd; }
            bool foreground = hwnd != IntPtr.Zero && WindowLocator.IsForeground(hwnd);
            bool edge = false;
            for (int vk = 1; vk < 256; vk++)
            {
                if (!WatchKey(vk)) continue;
                bool pressed = (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
                if (pressed && !keyDown[vk] && foreground) edge = true;
                keyDown[vk] = pressed;
            }
            if (edge) lock (gate) { publishedInput = true; }
        }

        private static bool WatchKey(int vk)
        {
            if (vk == 1 || vk == 2 || vk == 4 || vk == 5 || vk == 6) return true;
            if (vk >= 8 && vk <= 254 && vk != 16 && vk != 17 && vk != 18) return true;
            return false;
        }

        private void Sample()
        {
            IntPtr hwnd;
            WardenSettings settings;
            bool capture;
            Rectangle panel;
            lock (gate)
            {
                hwnd = requestHwnd;
                settings = requestSettings;
                capture = requestCapture;
                panel = requestPanel;
                publishedXp = false;
                publishedPack = false;
            }
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                haveBoxes = false;
                Publish(new HudReading(), CaptureRoute.Missing, false, "Waiting for the Kavros window.");
                return;
            }
            bool iconic = NativeMethods.IsIconic(hwnd);
            bool focused = WindowLocator.IsForeground(hwnd);
            if (!capture || iconic)
            {
                Publish(new HudReading(), iconic ? CaptureRoute.Minimized : CaptureRoute.Covered, focused, iconic ? "Kavros is minimized. Hitpoints, prayer, potions and idle need the window restored. The AFK timer is still running." : "Paused while the panel is dragged.");
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(hwnd);
            if (client.Width < 80 || client.Height < 80)
            {
                Publish(new HudReading(), CaptureRoute.Covered, focused, "Waiting for the Kavros window.");
                return;
            }
            bool full = CapturePlan.WantFull(haveBoxes, DateTime.UtcNow, nextFullSearch);
            HudReading reading;
            if (full) reading = FullSearch(client, settings, panel);
            else reading = LockedSearch(client);
            string note = focused ? "Watching the visible Kavros window." : "Watching Kavros. The window is visible but not focused.";
            Publish(reading, CaptureRoute.Screen, focused, note);
        }

        private HudReading FullSearch(Rectangle client, WardenSettings settings, Rectangle panel)
        {
            nextFullSearch = DateTime.UtcNow.AddSeconds(CapturePlan.FullIntervalSeconds);
            ImageBuffer image = CopyScreen(client, 0);
            if (image == null || ImageBuffer.MostlyBlack(image))
            {
                haveBoxes = false;
                return new HudReading();
            }
            BlankPanel(image, client, panel);
            HudReading reading = HudReader.Read(image, settings);
            if (reading.Hp != null && reading.HpMax != null && reading.LifeText.Width > 8) lifeBox = Pad(reading.LifeText, 6);
            if (reading.Prayer != null && reading.PrayerMax != null && reading.PrayerText.Width > 8) prayerBox = Pad(reading.PrayerText, 6);
            buffStrip = Union(reading.BuffRects);
            IntRect xp = SceneActivity.XpWatch.Find(image);
            if (xp.Width > 8) xpBox = xp;
            IntRect pack = SceneActivity.PackWatch.Find(image);
            if (pack.Width > 8) packBox = pack;
            haveBoxes = lifeBox.Width > 8 || prayerBox.Width > 8;
            NoteActivity(image);
            MaybeDebug(image, settings, reading);
            return reading;
        }

        private HudReading LockedSearch(Rectangle client)
        {
            HudReading reading = new HudReading();
            reading.StripFound = true;
            ImageBuffer life = CopyScreen(ScreenOf(client, lifeBox), 1);
            if (life != null) HudReader.ReadBox(life, true, reading);
            ImageBuffer prayer = CopyScreen(ScreenOf(client, prayerBox), 2);
            if (prayer != null) HudReader.ReadBox(prayer, false, reading);
            if (buffStrip.Width > 8)
            {
                ImageBuffer strip = CopyScreen(ScreenOf(client, buffStrip), 3);
                if (strip != null)
                {
                    WardenSettings pin = new WardenSettings();
                    pin.BuffPinX = 0;
                    pin.BuffPinY = 0;
                    pin.BuffPinW = 1;
                    pin.BuffPinH = 1;
                    HudReading buffs = HudReader.Read(strip, pin);
                    reading.Buffs = buffs.Buffs;
                    reading.BuffRects = buffs.BuffRects;
                    reading.BuffBarFound = buffs.BuffBarFound;
                    reading.BuffTrace = buffs.BuffTrace;
                    reading.PotionCount = buffs.PotionCount;
                }
            }
            if (reading.Hp == null && reading.Prayer == null)
            {
                missedLocks++;
                if (missedLocks >= 4) haveBoxes = false;
            }
            else missedLocks = 0;
            ImageBuffer xp = xpBox.Width > 8 ? CopyScreen(ScreenOf(client, xpBox), 4) : null;
            if (xp != null)
            {
                int hash = SceneActivity.XpWatch.Hash(xp, new IntRect(0, 0, xp.Width, xp.Height));
                if (haveXpHash && hash != lastXpHash) lock (gate) { publishedXp = true; }
                lastXpHash = hash;
                haveXpHash = true;
            }
            return reading;
        }

        private void NoteActivity(ImageBuffer image)
        {
            if (xpBox.Width > 8)
            {
                int hash = SceneActivity.XpWatch.Hash(image, xpBox);
                if (haveXpHash && hash != lastXpHash) lock (gate) { publishedXp = true; }
                lastXpHash = hash;
                haveXpHash = true;
            }
            if (packBox.Width > 8)
            {
                int hash = SceneActivity.PackWatch.Hash(image, packBox);
                if (havePackHash && hash != lastPackHash) lock (gate) { publishedPack = true; }
                lastPackHash = hash;
                havePackHash = true;
            }
        }

        private void MaybeDebug(ImageBuffer image, WardenSettings settings, HudReading reading)
        {
            if (settings == null || !settings.SaveDebug || DateTime.UtcNow < nextDebugImage) return;
            nextDebugImage = DateTime.UtcNow.AddSeconds(30);
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                System.IO.Directory.CreateDirectory(dir);
                int w = Math.Max(1, image.Width / 2);
                int h = Math.Max(1, image.Height / 2);
                using (Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] raw = new byte[stride * h];
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                int color = image.Argb(Math.Min(image.Width - 1, x * 2), Math.Min(image.Height - 1, y * 2));
                                int i = y * stride + x * 3;
                                raw[i] = (byte)(color & 255);
                                raw[i + 1] = (byte)((color >> 8) & 255);
                                raw[i + 2] = (byte)((color >> 16) & 255);
                            }
                        System.Runtime.InteropServices.Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                    }
                    finally { bitmap.UnlockBits(data); }
                    bitmap.Save(System.IO.Path.Combine(dir, "debug-capture.jpg"), ImageFormat.Jpeg);
                }
            }
            catch (Exception) { }
        }

        private void Publish(HudReading reading, CaptureRoute route, bool focused, string note)
        {
            lock (gate)
            {
                published = reading ?? new HudReading();
                publishedRoute = route;
                publishedFocused = focused;
                publishedNote = note;
            }
        }

        private static void BlankPanel(ImageBuffer image, Rectangle client, Rectangle panel)
        {
            if (image == null || panel.Width < 8 || panel.Height < 8) return;
            int x = panel.X - client.X;
            int y = panel.Y - client.Y;
            image.Fill(x, y, panel.Width, panel.Height, ImageBuffer.Pack(0, 0, 0));
        }

        private static IntRect Pad(IntRect box, int pad)
        {
            return new IntRect(box.X - pad, box.Y - pad, box.Width + pad * 2, box.Height + pad * 2);
        }

        private static IntRect Union(List<IntRect> boxes)
        {
            if (boxes == null || boxes.Count == 0) return new IntRect();
            int x = boxes[0].X, y = boxes[0].Y, right = boxes[0].Right, bottom = boxes[0].Bottom;
            for (int i = 1; i < boxes.Count; i++)
            {
                if (boxes[i].X < x) x = boxes[i].X;
                if (boxes[i].Y < y) y = boxes[i].Y;
                if (boxes[i].Right > right) right = boxes[i].Right;
                if (boxes[i].Bottom > bottom) bottom = boxes[i].Bottom;
            }
            return new IntRect(x - 2, y - 2, right - x + 4, bottom - y + 4);
        }

        private static Rectangle ScreenOf(Rectangle client, IntRect box)
        {
            if (box.Width < 4 || box.Height < 4) return Rectangle.Empty;
            return new Rectangle(client.X + box.X, client.Y + box.Y, box.Width, box.Height);
        }

        private ImageBuffer CopyScreen(Rectangle bounds, int slot)
        {
            if (bounds.Width < 4 || bounds.Height < 4 || bounds.Width > 4096 || bounds.Height > 4096) return null;
            if (slot < 0 || slot >= pads.Length) slot = 0;
            if (pads[slot] == null || pads[slot].Width != bounds.Width || pads[slot].Height != bounds.Height)
            {
                if (pads[slot] != null) pads[slot].Dispose();
                pads[slot] = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                raws[slot] = null;
            }
            using (Graphics graphics = Graphics.FromImage(pads[slot]))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            BitmapData data = pads[slot].LockBits(new Rectangle(0, 0, pads[slot].Width, pads[slot].Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * pads[slot].Height;
                if (raws[slot] == null || raws[slot].Length != bytes) raws[slot] = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raws[slot], 0, bytes);
                int[] pixels = new int[pads[slot].Width * pads[slot].Height];
                byte[] raw = raws[slot];
                for (int y = 0; y < pads[slot].Height; y++)
                    for (int x = 0; x < pads[slot].Width; x++)
                    {
                        int i = y * data.Stride + x * 4;
                        pixels[y * pads[slot].Width + x] = ImageBuffer.Pack(raw[i + 2], raw[i + 1], raw[i]);
                    }
                return new ImageBuffer(pads[slot].Width, pads[slot].Height, pixels);
            }
            finally { pads[slot].UnlockBits(data); }
        }
    }
}
