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
        internal int Pid;
        internal bool Foreground;
        internal string Label = "";
        public override string ToString()
        {
            if (Label != null && Label.Length > 0) return Label;
            return Title;
        }
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
                game.Pid = (int)pid;
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

        internal static long ChooseHandle(List<GameWindow> windows, IntPtr current, long passedHwnd, int passedPid)
        {
            if (windows == null) windows = new List<GameWindow>();
            MarkForeground(windows);
            List<ClientCandidate> candidates = new List<ClientCandidate>();
            for (int i = 0; i < windows.Count; i++)
            {
                ClientCandidate candidate = new ClientCandidate();
                candidate.Handle = windows[i].Handle.ToInt64();
                candidate.Pid = windows[i].Pid;
                candidate.Title = windows[i].Title;
                candidate.Process = windows[i].ProcessName;
                candidate.ClassName = windows[i].ClassName;
                candidate.Width = windows[i].Client.Width;
                candidate.Height = windows[i].Client.Height;
                candidate.Foreground = windows[i].Foreground;
                candidates.Add(candidate);
            }
            long currentHandle = current == IntPtr.Zero ? 0 : current.ToInt64();
            return ClientBind.Choose(candidates, currentHandle, passedHwnd, passedPid);
        }

        private static void MarkForeground(List<GameWindow> windows)
        {
            for (int i = 0; i < windows.Count; i++) windows[i].Foreground = false;
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return;
            IntPtr root = NativeMethods.GetAncestor(foreground, 2);
            if (root == IntPtr.Zero) root = foreground;
            if (Mark(windows, foreground) || Mark(windows, root)) return;
            IntPtr next = foreground;
            for (int n = 0; n < 16; n++)
            {
                next = NativeMethods.GetWindow(next, 2);
                if (next == IntPtr.Zero) return;
                if (Mark(windows, next)) return;
            }
        }

        private static bool Mark(List<GameWindow> windows, IntPtr handle)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].Handle != handle) continue;
                if (Score(windows[i]) < 100) return false;
                windows[i].Foreground = true;
                return true;
            }
            return false;
        }

        internal static string ClientLabel(GameWindow window, int index)
        {
            int pid = window == null ? 0 : window.Pid;
            return ClientNames.Label(pid, index);
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
        internal bool SkillView;
        internal string SkillTrace = "xp=none pack=none";
        internal string ChatNote = "";
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
        private bool publishedSkillView;
        private string publishedSkillTrace = "xp=none pack=none";
        private string publishedChatNote = "";
        private readonly SkillSense skills = new SkillSense();
        private int publishedMs;
        private readonly bool[] keyDown = new bool[256];
        private IntRect lifeBox;
        private IntRect prayerBox;
        private IntRect buffStrip;
        private IntRect xpBox;
        private IntRect packBox;
        private IntRect packHold;
        private IntRect chatBox;
        private IntRect metricsBox;
        private readonly WindowSurfaceCapture windowSurface = new WindowSurfaceCapture();
        private bool useSurface;
        private readonly MetricsLock xpLock = new MetricsLock();
        private List<ChatHit> ocrHits = new List<ChatHit>();
        private string ocrTrace = "";
        private string ocrNote = "";
        private int chatBusy;
        private int ocrFollow;
        private readonly ChatOcrGate chatGate = new ChatOcrGate();
        private XpFloat.Sight lastPopup;
        private DateTime popupHold = DateTime.MinValue;
        private HudReading lastGood;
        private IntRect heldLife;
        private IntRect heldPrayer;
        private bool haveBoxes;
        private int missedLocks;
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
                SkillView = publishedSkillView;
                SkillTrace = publishedSkillTrace;
                ChatNote = publishedChatNote;
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
            DateTime nextPopup = DateTime.MinValue;
            DateTime nextChat = DateTime.MinValue;
            try { while (alive)
            {
                PollKeys();
                if (DateTime.UtcNow >= nextPopup)
                {
                    try { ScanPopup(); }
                    catch (Exception) { }
                    nextPopup = DateTime.UtcNow.AddMilliseconds(200);
                }
                if (DateTime.UtcNow >= nextChat)
                {
                    try { ScheduleChat(); }
                    catch (Exception) { }
                    nextChat = DateTime.UtcNow.AddMilliseconds(CapturePlan.ChatMilliseconds);
                }
                if (DateTime.UtcNow >= nextSample)
                {
                    var clock = Stopwatch.StartNew();
                    try { Sample(); }
                    catch (Exception) { }
                    lock (gate) { publishedMs = (int)clock.ElapsedMilliseconds; }
                    nextSample = DateTime.UtcNow.AddMilliseconds(CapturePlan.SampleMilliseconds);
                }
                Thread.Sleep(20);
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
                lastGood = null;
                Publish(new HudReading(), CaptureRoute.Missing, false, "Waiting for the Kavros window.");
                return;
            }
            bool iconic = NativeMethods.IsIconic(hwnd);
            bool focused = WindowLocator.IsForeground(hwnd);
            if (!capture || iconic)
            {
                Publish(HeldReading(), iconic ? CaptureRoute.Minimized : CaptureRoute.Covered, focused, iconic ? "Minimized" : "Paused while the panel is dragged.");
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(hwnd);
            if (client.Width < 80 || client.Height < 80)
            {
                Publish(HeldReading(), CaptureRoute.Covered, focused, "Waiting for the Kavros window.");
                return;
            }
            // A visible client, focused or not, is a screen copy. A covered client
            // is left unread so the desktop does not flash. PrintWindow is not used.
            if (lastClient != hwnd || lastSize != client.Size || !settings.SamePins(lastSettings))
            {
                lastClient=hwnd; lastSize=client.Size; lastSettings=settings;
                lifeBox=prayerBox=buffStrip=xpBox=packBox=packHold=chatBox=metricsBox=new IntRect();
                heldLife=heldPrayer=new IntRect();
                haveBoxes=false; missedLocks=0; nextFullSearch=DateTime.MinValue;
            }
            bool visible = RectOnGame(hwnd, client, new IntRect(0, 0, client.Width, client.Height));
            if (CapturePlan.OfferWindowSurface) windowSurface.Ensure(hwnd);
            useSurface = windowSurface.Available;
            CaptureRoute route = CapturePolicy.Choose(true, false, visible, !visible, useSurface);
            HudReading reading = new HudReading();
            xpLock.Fresh = false;
            if (CapturePolicy.PixelsAvailable(route))
            {
                bool full = CapturePlan.WantFull(haveBoxes, DateTime.UtcNow, nextFullSearch);
                if (full) reading = FullSearch(hwnd, client, settings, panel);
                else if (haveBoxes) reading = LockedSearch(hwnd, client, settings);
            }
            bool empty = reading.Hp == null && reading.Prayer == null && !reading.StripFound && !reading.BuffBarFound
                && (reading.ChatHits == null || reading.ChatHits.Count == 0) && !xpLock.Fresh;
            string note = route == CaptureRoute.WindowSurface
                ? "Watching the Kavros window."
                : (focused ? "Watching the visible Kavros window." : "Watching the visible Kavros window in the background.");
            if (empty)
            {
                if (lastGood != null)
                {
                    Publish(HeldReading(), route == CaptureRoute.WindowSurface ? route : (visible ? CaptureRoute.Screen : CaptureRoute.Covered), focused, note);
                    return;
                }
                string missed = route == CaptureRoute.WindowSurface || visible
                    ? "Waiting for the hitpoint and prayer numbers."
                    : "Kavros is covered. Hitpoints, prayer and potions stay on the last good reading.";
                Publish(reading, route == CaptureRoute.WindowSurface ? route : (visible ? CaptureRoute.Screen : CaptureRoute.Covered), focused, missed);
                return;
            }
            FillBars(reading);
            if (reading.Hp != null || reading.Prayer != null) lastGood = CopyReading(reading);
            Publish(reading, route, focused, note);
        }

        private HudReading HeldReading()
        {
            HudReading kept = CopyReading(lastGood);
            kept.MetricsGain = false;
            return kept;
        }

        private static HudReading CopyReading(HudReading source)
        {
            HudReading copy = new HudReading();
            if (source == null) return copy;
            copy.Hp = source.Hp;
            copy.HpMax = source.HpMax;
            copy.HpFromOrb = source.HpFromOrb;
            copy.HpHow = source.HpHow;
            copy.Prayer = source.Prayer;
            copy.PrayerMax = source.PrayerMax;
            copy.PrayerFromOrb = source.PrayerFromOrb;
            copy.PrayerHow = source.PrayerHow;
            copy.Adrenaline = source.Adrenaline;
            copy.StripY = source.StripY;
            copy.PotionSeconds = source.PotionSeconds;
            copy.PotionCount = source.PotionCount;
            copy.PotionHow = source.PotionHow;
            copy.BuffBarFound = source.BuffBarFound;
            copy.BuffScanned = source.BuffScanned;
            copy.StripFound = source.StripFound;
            copy.LifeText = source.LifeText;
            copy.PrayerText = source.PrayerText;
            copy.LifeBar = source.LifeBar;
            copy.PrayerBar = source.PrayerBar;
            copy.LifeTrace = source.LifeTrace;
            copy.PrayerTrace = source.PrayerTrace;
            copy.BuffTrace = source.BuffTrace;
            copy.Buffs = source.Buffs;
            copy.BuffRects = source.BuffRects;
            copy.ChatHits = source.ChatHits;
            copy.MetricsFound = source.MetricsFound;
            copy.MetricsXp = source.MetricsXp;
            copy.MetricsNote = source.MetricsNote;
            return copy;
        }

        private void FillBars(HudReading reading)
        {
            if (lastGood == null || reading == null) return;
            if (reading.Hp == null && lastGood.Hp != null)
            {
                reading.Hp = lastGood.Hp;
                reading.HpMax = lastGood.HpMax;
                reading.HpFromOrb = lastGood.HpFromOrb;
                reading.HpHow = lastGood.HpHow;
                reading.LifeText = lastGood.LifeText;
                reading.LifeTrace = lastGood.LifeTrace;
                if (reading.LifeBar.Width < 8) reading.LifeBar = lastGood.LifeBar;
            }
            if (reading.Prayer == null && lastGood.Prayer != null)
            {
                reading.Prayer = lastGood.Prayer;
                reading.PrayerMax = lastGood.PrayerMax;
                reading.PrayerFromOrb = lastGood.PrayerFromOrb;
                reading.PrayerHow = lastGood.PrayerHow;
                reading.PrayerText = lastGood.PrayerText;
                reading.PrayerTrace = lastGood.PrayerTrace;
                if (reading.PrayerBar.Width < 8) reading.PrayerBar = lastGood.PrayerBar;
            }
            if (!reading.MetricsFound && lastGood.MetricsFound && (reading.MetricsNote == null || reading.MetricsNote.Length == 0))
            {
                reading.MetricsFound = true;
                reading.MetricsXp = lastGood.MetricsXp;
                reading.MetricsNote = lastGood.MetricsNote;
            }
        }

        private ImageBuffer Grab(IntPtr hwnd, Rectangle client, IntRect local, int slot)
        {
            if (local.Width < 4 || local.Height < 4) return null;
            if (useSurface)
            {
                ImageBuffer fromWindow = windowSurface.Copy(local.X, local.Y, local.Width, local.Height);
                if (CapturePlan.AcceptFrame(fromWindow)) return fromWindow;
            }
            if (!RectOnGame(hwnd, client, local)) return null;
            ImageBuffer shot = CopyScreen(ScreenOf(client, local), slot);
            if (!CapturePlan.AcceptFrame(shot)) return null;
            return shot;
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

        private HudReading FullSearch(IntPtr hwnd, Rectangle client, WardenSettings settings, Rectangle panel)
        {
            nextFullSearch = DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(false, false, failedFull + 1));
            ImageBuffer image = Grab(hwnd, client, new IntRect(0, 0, client.Width, client.Height), 0);
            if (!CapturePlan.AcceptFrame(image))
            {
                failedFull++;
                return new HudReading();
            }
            BlankPanel(image, client, panel);
            // Chat and the XP tracker first. The buff bar is not part of this pass.
            HudReading reading = HudReader.Read(image, settings, false);
            ReadSide(image, reading, 0);
            if (reading.LifeText.Width > 8) lifeBox = reading.LifeText;
            if (reading.PrayerText.Width > 8) prayerBox = reading.PrayerText;
            IntRect foundBuff = Union(reading.BuffRects);
            if(settings.HasBuffPin) buffStrip=new IntRect((int)(settings.BuffPinX*image.Width),(int)(settings.BuffPinY*image.Height),(int)Math.Ceiling(settings.BuffPinW*image.Width),(int)Math.Ceiling(settings.BuffPinH*image.Height));
            else if(foundBuff.Width>0 && RowHasTimer(reading))
            {
                // Keep a few neighbouring slots in view when buffs reorder after a dose.
                int margin=Math.Max(16,foundBuff.Height*2);
                buffStrip=new IntRect(Math.Max(0,foundBuff.X-margin),Math.Max(0,foundBuff.Y-3),
                    Math.Min(image.Width,foundBuff.Right+margin)-Math.Max(0,foundBuff.X-margin),
                    Math.Min(image.Height,foundBuff.Bottom+4)-Math.Max(0,foundBuff.Y-3));
            }
            if (xpBox.Width < 8)
                xpBox = new IntRect(0, 0, image.Width, image.Height);
            NoteActivity(image);
            if (skills.XpRect.Width > 8)
            {
                // Follow the drop wherever it was, with room for the float, and
                // keep scanning the rest of the client on the next full pass.
                IntRect xp = skills.XpRect;
                int left = Math.Max(0, xp.X - Math.Max(80, image.Width / 5));
                int top = Math.Max(0, xp.Y - Math.Max(40, image.Height / 5));
                int right = Math.Min(image.Width, xp.Right + Math.Max(160, image.Width / 4));
                int bottom = Math.Min(image.Height, xp.Bottom + Math.Max(80, image.Height / 5));
                xpBox = new IntRect(left, top, Math.Max(32, right - left), Math.Max(32, bottom - top));
            }
            IntRect pack = SceneActivity.PackWatch.Find(image);
            if (pack.Width > 8)
            {
                if (packBox.Width < 8 || SameGrid(pack, packBox) || (packHold.Width > 8 && SameGrid(pack, packHold)))
                    packBox = pack;
                packHold = pack;
            }
            RememberBars(reading);
            haveBoxes = lifeBox.Width > 8 || prayerBox.Width > 8 || buffStrip.Width>8;
            bool complete = reading.HpMax.HasValue && reading.PrayerMax.HasValue;
            failedFull = complete ? 0 : failedFull + 1;
            nextFullSearch=DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(complete, buffStrip.Width>8, failedFull));
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
                // A single miss does not walk the lock onto scenery. The strip
                // moves only when a later search finds a buff row that has timers.
            }
            if (reading.HpMax == null || reading.PrayerMax == null || (buffStrip.Width>8 && slow && !reading.BuffBarFound))
            {
                missedLocks++;
                if (missedLocks == 3 && nextFullSearch>DateTime.UtcNow.AddSeconds(3))
                    nextFullSearch=DateTime.UtcNow.AddSeconds(3);
            }
            else missedLocks = 0;
            RememberBars(reading);
            ApplyStoredChat(reading);
            if (!slow) return reading;
            if (metricsBox.Width > 8)
            {
                ImageBuffer metrics = Grab(hwnd, client, metricsBox, 5);
                if (metrics != null)
                {
                    MetricsReader.Result read = MetricsReader.Read(metrics);
                    ApplyMetrics(reading, read, null);
                }
            }
            ImageBuffer pack = packBox.Width > 8 ? Grab(hwnd, client, packBox, 3) : null;
            skills.Observe(null, pack);
            PublishSkills();
            return reading;
        }

        private static bool SameGrid(IntRect a, IntRect b)
        {
            int ax = a.X + a.Width / 2;
            int ay = a.Y + a.Height / 2;
            int bx = b.X + b.Width / 2;
            int by = b.Y + b.Height / 2;
            int dx = ax - bx;
            int dy = ay - by;
            return dx * dx + dy * dy < 80 * 80;
        }

        private void RememberBars(HudReading reading)
        {
            if (reading == null) return;
            if (reading.LifeBar.Width >= 8 && reading.LifeBar.Y > 0) heldLife = reading.LifeBar;
            else if (heldLife.Width >= 8) reading.LifeBar = heldLife;
            if (reading.PrayerBar.Width >= 8 && reading.PrayerBar.Y > 0) heldPrayer = reading.PrayerBar;
            else if (heldPrayer.Width >= 8) reading.PrayerBar = heldPrayer;
        }

        private static bool RowHasTimer(HudReading reading)
        {
            if (reading == null || reading.Buffs == null) return false;
            for (int i = 0; i < reading.Buffs.Count; i++)
                if (reading.Buffs[i].HasTime) return true;
            return false;
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
            // An on-screen XP popup is the activity signal. A full RuneMetrics
            // total is only the backup, and an abbreviated total is ignored.
            skills.Observe(image, image);
            PublishSkills();
        }

        private void ReadSide(ImageBuffer image, HudReading reading, int yOffset)
        {
            if (image == null || reading == null) return;
            ApplyStoredChat(reading);
            IntRect foundChat = ChatPrep.FindBox(image);
            if (foundChat.Width >= 40 && foundChat.Height >= 20) chatBox = foundChat;
            MetricsReader.Result metrics = MetricsReader.Read(image);
            ApplyMetrics(reading, metrics, image);
        }

        private void ScanPopup()
        {
            IntPtr hwnd;
            bool capture;
            lock (gate)
            {
                hwnd = requestHwnd;
                capture = requestCapture;
            }
            if (!capture || hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || NativeMethods.IsIconic(hwnd)) return;
            Rectangle client = NativeMethods.ClientBounds(hwnd);
            if (client.Width < 200 || client.Height < 200) return;
            if (CapturePlan.OfferWindowSurface) windowSurface.Ensure(hwnd);
            useSurface = windowSurface.Available;
            ImageBuffer image = null;
            if (useSurface) image = windowSurface.Copy(0, 0, client.Width, client.Height);
            if (image == null) return;
            XpFloat.Sight sight = XpFloat.Look(image);
            bool moved = sight.Found && XpFloat.Changed(lastPopup, sight);
            bool live = sight.Found && (moved || DateTime.UtcNow < popupHold);
            if (moved) popupHold = DateTime.UtcNow.AddSeconds(2);
            lastPopup = sight;
            if (!live) return;
            lock (gate)
            {
                publishedXp = true;
                publishedSkillTrace = "xp=popup";
            }
        }

        private void ScheduleChat()
        {
            if (System.Threading.Interlocked.CompareExchange(ref chatBusy, 1, 0) != 0) return;
            IntPtr hwnd;
            bool capture;
            lock (gate)
            {
                hwnd = requestHwnd;
                capture = requestCapture;
            }
            if (!capture || hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || NativeMethods.IsIconic(hwnd))
            {
                System.Threading.Interlocked.Exchange(ref chatBusy, 0);
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(hwnd);
            if (client.Width < 200 || client.Height < 200)
            {
                System.Threading.Interlocked.Exchange(ref chatBusy, 0);
                return;
            }
            if (CapturePlan.OfferWindowSurface) windowSurface.Ensure(hwnd);
            ImageBuffer frame = windowSurface.Available ? windowSurface.Copy(0, 0, client.Width, client.Height) : null;
            if (frame == null)
            {
                System.Threading.Interlocked.Exchange(ref chatBusy, 0);
                return;
            }
            IntRect box = ChatPrep.FindBox(frame);
            if (box.Width < 40 || box.Height < 20)
            {
                System.Threading.Interlocked.Exchange(ref chatBusy, 0);
                return;
            }
            chatBox = box;
            ImageBuffer crop = frame.Crop(box.X, box.Y, box.Width, box.Height);
            int stamp = ChatPrep.Stamp(crop);
            bool follow = System.Threading.Interlocked.Exchange(ref ocrFollow, 0) == 1;
            if (!chatGate.Due(stamp, follow))
            {
                System.Threading.Interlocked.Exchange(ref chatBusy, 0);
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { ReadChat(crop); }
                finally { System.Threading.Interlocked.Exchange(ref chatBusy, 0); }
            });
        }

        private void ReadChat(ImageBuffer crop)
        {
            if (!WindowsChatOcr.Available)
            {
                string why = WindowsChatOcr.Reason;
                if (why == null || why.Length == 0) why = "no English OCR language pack";
                lock (gate)
                {
                    ocrHits = new List<ChatHit>();
                    ocrNote = "Chat reader unavailable";
                    ocrTrace = "unavailable: " + why;
                    publishedChatNote = ocrNote;
                }
                return;
            }
            string text = WindowsChatOcr.Read(crop);
            List<ChatHit> hits = ChatPhrase.FromOcr(text);
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (line.Length == 0) continue;
                if (trace.Length > 0) trace.Append(" | ");
                string shown = line;
                if (shown.Length > 80) shown = shown.Substring(0, 80);
                trace.Append("raw=\"").Append(shown).Append("\"");
            }
            if (trace.Length == 0 && (WindowsChatOcr.Reason ?? "").Length > 0)
                trace.Append("unavailable: ").Append(WindowsChatOcr.Reason);
            if (ChatPrep.TornPotion(text))
                System.Threading.Interlocked.Exchange(ref ocrFollow, 1);
            lock (gate)
            {
                ocrHits = hits;
                ocrTrace = trace.ToString();
                ocrNote = "";
                publishedChatNote = "";
                ChatReader.Trace = ocrTrace;
            }
        }

        private void ApplyStoredChat(HudReading reading)
        {
            if (reading == null) return;
            lock (gate)
            {
                reading.ChatHits = ocrHits ?? new List<ChatHit>();
                ChatReader.Trace = ocrTrace ?? "";
            }
        }

        private void ApplyMetrics(HudReading reading, MetricsReader.Result metrics, ImageBuffer image)
        {
            if (reading == null) return;
            xpLock.Observe(metrics.Found, metrics.Xp, metrics.Note, DateTime.UtcNow);
            if (xpLock.Relocate)
            {
                metricsBox = new IntRect();
                if (nextFullSearch > DateTime.UtcNow.AddSeconds(2))
                    nextFullSearch = DateTime.UtcNow.AddSeconds(1);
            }
            reading.MetricsFound = xpLock.Found;
            reading.MetricsXp = xpLock.Xp;
            reading.MetricsNote = xpLock.Note;
            reading.MetricsGain = xpLock.Gain;
            if (reading.MetricsGain) publishedXp = true;
            if (!metrics.Found || !xpLock.Fresh || image == null) return;
            int mx = Math.Max(0, metrics.X);
            int my = Math.Max(0, metrics.Y);
            int width = Math.Min(image.Width - mx, Math.Max(40, metrics.W));
            int height = Math.Min(image.Height - my, Math.Max(24, metrics.H));
            if (width < 4 || height < 4) return;
            metricsBox = new IntRect(mx, my, width, height);
        }

        private void PublishSkills()
        {
            lock (gate)
            {
                if (skills.XpEvent) publishedXp = true;
                if (skills.PackEvent) publishedPack = true;
                if (DateTime.UtcNow < popupHold)
                {
                    publishedXp = true;
                    string rest = skills.Trace ?? "";
                    if (rest.StartsWith("xp=none")) rest = "xp=popup" + rest.Substring(7);
                    else if (rest.IndexOf("xp=popup") < 0) rest = "xp=popup " + rest;
                    publishedSkillTrace = rest;
                }
                else publishedSkillTrace = skills.Trace;
                publishedSkillView = skills.View;
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
