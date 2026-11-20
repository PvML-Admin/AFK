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
        private readonly Bitmap[] pads = new Bitmap[6];
        private readonly int[][] pixelsBySlot = new int[6][];
        private int failedFull;
        private int lockedSamples;
        private IntPtr lastClient;
        private Size lastSize;
        private WardenSettings lastSettings;
        private uint lastInputStamp;
        private bool pollForeground;
        private DateTime publishedAt;
        private IntPtr publishedWindow;
        internal DateTime FrameTime;

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
        }

        private void DisposeBuffers()
        {
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
                requestSettings = settings == null ? new WardenSettings() : settings.Snapshot();
                requestCapture = capture;
                requestPanel = PanelBounds;
                Reading = publishedWindow==hwnd ? published : new HudReading();
                FrameTime = publishedAt;
                Route = publishedRoute;
                Focused = publishedFocused;
                Input = publishedInput;
                publishedInput = false;
                Note = publishedNote;
                XpActivity = publishedXp;
                PackActivity = publishedPack;
                LastTickMs = publishedMs;
                publishedXp = false;
                publishedPack = false;
                Image = null;
            }
        }

        private void Loop()
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; } catch (Exception) { }
            DateTime nextSample = DateTime.MinValue;
            try { while (alive)
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
                Thread.Sleep(35);
            } }
            finally { DisposeBuffers(); }
        }

        private void PollKeys()
        {
            IntPtr hwnd;
            lock (gate) { hwnd = requestSettings.Paused ? IntPtr.Zero : requestHwnd; }
            bool foreground = hwnd != IntPtr.Zero && WindowLocator.IsForeground(hwnd);
            if (!foreground) { pollForeground = false; return; }
            NativeMethods.LastInput last = new NativeMethods.LastInput();
            last.Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.LastInput));
            if (!NativeMethods.GetLastInputInfo(ref last)) return;
            if (pollForeground && last.Time == lastInputStamp) return;
            bool wasForeground = pollForeground;
            pollForeground = true;
            lastInputStamp = last.Time;
            bool edge = false;
            bool click = false;
            for (int vk = 1; vk < 256; vk++)
            {
                if (!WatchKey(vk)) continue;
                short state = NativeMethods.GetAsyncKeyState(vk);
                bool pressed = (state & 0x8000) != 0;
                // Bit 0 catches a press that went down and up between two polls.
                bool tapped = (state & 1) != 0;
                if (((pressed && !keyDown[vk]) || tapped) && foreground)
                {
                    edge = true;
                    if (vk <= 6) click = true;
                }
                keyDown[vk] = pressed;
            }
            // Keys only count once the game was already in front, so typing in
            // another app right before switching does not. A mouse click that
            // brings the game to the front is a click on the game and counts.
            if (edge && (wasForeground || click)) lock (gate) { publishedInput = true; }
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
            }
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                lastClient = IntPtr.Zero;
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
            // Unfocused or covered clients are still read. PrintWindow supplies
            // the game's own pixels when the screen shows something else.
            printed = null;
            if (lastClient != hwnd || lastSize != client.Size || !settings.SamePins(lastSettings))
            {
                lastClient=hwnd; lastSize=client.Size; lastSettings=settings;
                lifeBox=prayerBox=buffStrip=xpBox=packBox=new IntRect();
                haveBoxes=haveXpHash=havePackHash=false; missedLocks=0; nextFullSearch=DateTime.MinValue;
            }
            bool full = CapturePlan.WantFull(haveBoxes, DateTime.UtcNow, nextFullSearch);
            HudReading reading;
            if (full) reading = FullSearch(hwnd, client, settings, panel);
            else reading = haveBoxes ? LockedSearch(hwnd, client, settings) : new HudReading();
            string note = focused ? "Watching the visible Kavros window." : "Watching Kavros behind other windows.";
            Publish(reading, CaptureRoute.Screen, focused, note);
        }

        private ImageBuffer printed;

        private ImageBuffer Grab(IntPtr hwnd, Rectangle client, IntRect local, int slot)
        {
            if (local.Width < 4 || local.Height < 4) return null;
            if (!CapturePlan.PreferWindowPixels(!RectOnGame(hwnd, client, local)))
                return CopyScreen(ScreenOf(client, local), slot);
            ImageBuffer frame = EnsurePrinted(hwnd, client);
            ImageBuffer crop = CropOf(frame, local);
            if (crop == null || ImageBuffer.MostlyBlack(crop))
                return CopyScreen(ScreenOf(client, local), slot);
            return crop;
        }

        private bool RectOnGame(IntPtr hwnd, Rectangle client, IntRect local)
        {
            Rectangle screen = ScreenOf(client, local);
            if (screen.Width < 4 || screen.Height < 4) return false;
            int hits = 0;
            int[] xs = new int[] { screen.Left + 1, Math.Max(screen.Left + 1, screen.Right - 2) };
            int[] ys = new int[] { screen.Top + 1, Math.Max(screen.Top + 1, screen.Bottom - 2) };
            for (int y = 0; y < ys.Length; y++)
                for (int x = 0; x < xs.Length; x++)
                    if (WindowLocator.OverGame(hwnd, xs[x], ys[y])) hits++;
            return hits >= 3;
        }

        private ImageBuffer EnsurePrinted(IntPtr hwnd, Rectangle client)
        {
            if (printed != null) return printed;
            Rectangle window = NativeMethods.WindowBounds(hwnd);
            if (window.Width < 80 || window.Height < 80 || window.Width > 4096 || window.Height > 4096) return null;
            const int slot = 0;
            if (pads[slot] == null || pads[slot].Width != window.Width || pads[slot].Height != window.Height)
            {
                if (pads[slot] != null) pads[slot].Dispose();
                pads[slot] = new Bitmap(window.Width, window.Height, PixelFormat.Format32bppArgb);
                pixelsBySlot[slot] = new int[window.Width * window.Height];
            }
            using (Graphics graphics = Graphics.FromImage(pads[slot]))
            {
                graphics.Clear(Color.Black);
                IntPtr hdc = graphics.GetHdc();
                try { NativeMethods.PrintWindow(hwnd, hdc, 2); }
                finally { graphics.ReleaseHdc(hdc); }
            }
            BitmapData data = pads[slot].LockBits(new Rectangle(0, 0, window.Width, window.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = pixelsBySlot[slot];
                if (data.Stride == window.Width * 4)
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                else for (int y = 0; y < window.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * window.Width, window.Width);
            }
            finally { pads[slot].UnlockBits(data); }
            int ox = client.X - window.X;
            int oy = client.Y - window.Y;
            if (ox < 0) ox = 0;
            if (oy < 0) oy = 0;
            int cw = Math.Min(client.Width, window.Width - ox);
            int ch = Math.Min(client.Height, window.Height - oy);
            if (cw < 80 || ch < 80) return null;
            int[] source = pixelsBySlot[slot];
            int[] clientPixels = new int[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    clientPixels[y * cw + x] = source[(oy + y) * window.Width + (ox + x)];
            printed = new ImageBuffer(cw, ch, clientPixels);
            if (ImageBuffer.MostlyBlack(printed)) printed = null;
            return printed;
        }

        private static ImageBuffer CropOf(ImageBuffer image, IntRect local)
        {
            if (image == null) return null;
            int x = local.X, y = local.Y, w = local.Width, h = local.Height;
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 4 || h < 4) return null;
            return image.Crop(x, y, w, h);
        }

        private HudReading FullSearch(IntPtr hwnd, Rectangle client, WardenSettings settings, Rectangle panel)
        {
            nextFullSearch = DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(false, false, failedFull + 1));
            ImageBuffer image = Grab(hwnd, client, new IntRect(0, 0, client.Width, client.Height), 0);
            if (image == null || ImageBuffer.MostlyBlack(image))
            {
                haveBoxes = false;
                failedFull++;
                return new HudReading();
            }
            BlankPanel(image, client, panel);
            HudReading reading = HudReader.Read(image, settings);
            if (reading.LifeText.Width > 8) lifeBox = reading.LifeText;
            if (reading.PrayerText.Width > 8) prayerBox = reading.PrayerText;
            IntRect foundBuff = Union(reading.BuffRects);
            if(settings.HasBuffPin) buffStrip=new IntRect((int)(settings.BuffPinX*image.Width),(int)(settings.BuffPinY*image.Height),(int)Math.Ceiling(settings.BuffPinW*image.Width),(int)Math.Ceiling(settings.BuffPinH*image.Height));
            else if(foundBuff.Width>0)
            {
                // Keep a few neighbouring slots in view when buffs reorder after a dose.
                int margin=Math.Max(16,foundBuff.Height*2);
                buffStrip=new IntRect(Math.Max(0,foundBuff.X-margin),Math.Max(0,foundBuff.Y-3),
                    Math.Min(image.Width,foundBuff.Right+margin)-Math.Max(0,foundBuff.X-margin),
                    Math.Min(image.Height,foundBuff.Bottom+4)-Math.Max(0,foundBuff.Y-3));
            }
            IntRect xp = SceneActivity.XpWatch.Find(image);
            if (xp.Width > 8) xpBox = xp;
            IntRect pack = SceneActivity.PackWatch.Find(image);
            if (pack.Width > 8) packBox = pack;
            haveBoxes = lifeBox.Width > 8 || prayerBox.Width > 8 || buffStrip.Width>8;
            bool complete = reading.HpMax.HasValue && reading.PrayerMax.HasValue;
            failedFull = complete ? 0 : failedFull + 1;
            nextFullSearch=DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(complete, buffStrip.Width>8, failedFull));
            NoteActivity(image);
            MaybeDebug(image, settings, reading);
            return reading;
        }

        private HudReading LockedSearch(IntPtr hwnd, Rectangle client, WardenSettings settings)
        {
            HudReading reading = new HudReading();
            reading.StripFound = lifeBox.Width>8 || prayerBox.Width>8;
            lockedSamples++;
            bool slow = lockedSamples % CapturePlan.SlowSlotEvery == 0;
            ImageBuffer life = null, prayer = null;
            if (CapturePlan.ShareGrab(lifeBox, prayerBox))
            {
                // Life and prayer share the HUD row: one screen copy for both.
                IntRect both = Union(lifeBox, prayerBox);
                ImageBuffer row = Grab(hwnd, client, both, 1);
                if (row != null)
                {
                    life = Slice(row, lifeBox.X - both.X, lifeBox.Y - both.Y, lifeBox.Width, lifeBox.Height);
                    prayer = Slice(row, prayerBox.X - both.X, prayerBox.Y - both.Y, prayerBox.Width, prayerBox.Height);
                }
            }
            else
            {
                life = Grab(hwnd, client, lifeBox, 1);
                prayer = Grab(hwnd, client, prayerBox, 2);
            }
            if (life != null) HudReader.ReadBox(life, true, reading);
            if (prayer != null) HudReader.ReadBox(prayer, false, reading);
            if (buffStrip.Width > 8 && slow)
            {
                ImageBuffer strip = Grab(hwnd, client, buffStrip, 3);
                if (strip != null)
                {
                    WardenSettings pin = new WardenSettings();
                    pin.BuffPinX = 0;
                    pin.BuffPinY = 0;
                    pin.BuffPinW = 1;
                    pin.BuffPinH = 1;
                    HudReading buffs = new HudReading();
                    HudReader.ReadBuffBar(strip, pin, buffs);
                    reading.Buffs = buffs.Buffs;
                    reading.BuffRects = buffs.BuffRects;
                    reading.BuffBarFound = buffs.BuffBarFound;
                    reading.BuffTrace = buffs.BuffTrace;
                    reading.PotionCount = buffs.PotionCount;
                }
                if (!reading.BuffBarFound)
                {
                    int grown = buffStrip.Width + 64;
                    int x = Math.Max(0, buffStrip.X - 32);
                    int w = Math.Min(client.Width - x, grown);
                    buffStrip = new IntRect(x, buffStrip.Y, w, buffStrip.Height);
                }
            }
            if (reading.HpMax == null || reading.PrayerMax == null || (buffStrip.Width>8 && slow && !reading.BuffBarFound))
            {
                missedLocks++;
                if (missedLocks == 3 && nextFullSearch>DateTime.UtcNow.AddSeconds(3))
                    nextFullSearch=DateTime.UtcNow.AddSeconds(3);
            }
            else missedLocks = 0;
            if (!slow) return reading;
            ImageBuffer xp = xpBox.Width > 8 ? Grab(hwnd, client, xpBox, 4) : null;
            if (xp != null)
            {
                int hash = SceneActivity.XpWatch.Hash(xp, new IntRect(0, 0, xp.Width, xp.Height));
                if (haveXpHash && hash != lastXpHash) lock (gate) { publishedXp = true; }
                lastXpHash = hash;
                haveXpHash = true;
            }
            // A new log, ore or fish is progress whether or not skilling mode is ticked.
            ImageBuffer pack = packBox.Width > 8 ? Grab(hwnd, client, packBox, 5) : null;
            if (pack != null)
            {
                int hash = SceneActivity.PackWatch.Hash(pack, new IntRect(0, 0, pack.Width, pack.Height));
                if (havePackHash && hash != lastPackHash) lock (gate) { publishedPack = true; }
                lastPackHash = hash;
                havePackHash = true;
            }
            return reading;
        }

        private static IntRect Union(IntRect a, IntRect b)
        {
            int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            return new IntRect(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
        }

        private static ImageBuffer Slice(ImageBuffer image, int x, int y, int w, int h)
        {
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 4 || h < 4) return null;
            return image.Crop(x, y, w, h);
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
                publishedWindow=lastClient;
                publishedAt = DateTime.UtcNow;
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
                pixelsBySlot[slot] = new int[bounds.Width*bounds.Height];
            }
            using (Graphics graphics = Graphics.FromImage(pads[slot]))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            BitmapData data = pads[slot].LockBits(new Rectangle(0, 0, pads[slot].Width, pads[slot].Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int width=bounds.Width,height=bounds.Height;
                int[] pixels=pixelsBySlot[slot];
                if(data.Stride==width*4)
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                else for(int y=0;y<height;y++)
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*width,width);
                return new ImageBuffer(width,height,pixels);
            }
            finally { pads[slot].UnlockBits(data); }
        }
    }
}
