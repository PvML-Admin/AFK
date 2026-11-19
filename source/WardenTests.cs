using System;
using System.Collections.Generic;
using System.IO;

namespace KavrosAfkWarden
{
    public static class WardenTests
    {
        private static int assertions;
        private static readonly List<string> failures = new List<string>();

        public static int Run(string report, string root)
        {
            assertions = 0;
            failures.Clear();
            try
            {
                Channels();
                Persistence();
                Gates();
                Levels();
                IdleAndAfk();
                Background();
                Capture();
                Input();
                Placement();
                Windows();
                Motion();
                Digits();
                Hud();
                Potions();
                LiveCapture(root);
                Manifest(root);
            }
            catch (Exception ex)
            {
                failures.Add(ex.ToString());
            }
            string text = "AFK Warden " + AppInfo.DisplayVersion + " self-test\r\nAssertions: " + assertions.ToString() + "\r\nFailures: " + failures.Count.ToString() + "\r\n";
            for (int i = 0; i < failures.Count; i++) text += failures[i] + "\r\n";
            if (failures.Count == 0) text += "PASS\r\n";
            File.WriteAllText(report, text);
            return failures.Count == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) failures.Add(message);
        }

        private static AlertEvent Find(List<AlertEvent> events, string kind)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Kind == kind) return events[i];
            return null;
        }

        private static void Channels()
        {
            WardenSettings settings = new WardenSettings();
            Check(settings.AllowsSound("idle") && settings.AllowsNotify("hp"), "defaults allow sound and notification");
            settings.SoundMaster = false;
            Check(!settings.AllowsSound("afk") && settings.AllowsNotify("afk"), "global sound off leaves notifications on");
            settings.SoundMaster = true;
            settings.NotifyMaster = false;
            Check(settings.AllowsSound("prayer") && !settings.AllowsNotify("prayer"), "global notification off leaves sound on");
            settings.NotifyMaster = true;
            settings.IdleSound = false;
            settings.HpNotify = false;
            Check(!settings.AllowsSound("idle") && settings.AllowsSound("hp"), "idle sound can be off while hitpoints sound stays on");
            Check(settings.AllowsNotify("idle") && !settings.AllowsNotify("hp"), "hitpoints notification can be off while idle notification stays on");
            settings.AfkSound = false;
            settings.AfkNotify = false;
            Check(!settings.AllowsSound("afk") && !settings.AllowsNotify("afk"), "an alert can silence both of its channels");
            Check(settings.AllowsSound("prayer") && settings.AllowsNotify("prayer"), "other alerts keep their channels");
            Check(settings.AllowsSound("potion") && settings.AllowsNotify("potion") && settings.PotionSeconds == 15, "potion alert defaults to 15 seconds with both channels on");
            settings.PotionSound = false;
            settings.PotionNotify = false;
            Check(!settings.AllowsSound("potion") && !settings.AllowsNotify("potion"), "potion sound and notification can both be off");
        }

        private static void Persistence()
        {
            WardenSettings settings = new WardenSettings();
            settings.IdleSeconds = 40;
            settings.HpThreshold = 15;
            settings.HpPercent = false;
            settings.HpThreshold = 1500;
            settings.PrayerPercent = true;
            settings.PrayerThreshold = 12;
            settings.AfkSeconds = 600;
            settings.AfkLeadSeconds = 45;
            settings.SoundMaster = false;
            settings.NotifyMaster = true;
            settings.IdleSound = false;
            settings.IdleNotify = true;
            settings.HpSound = true;
            settings.HpNotify = false;
            settings.PrayerSound = false;
            settings.PrayerNotify = false;
            settings.AfkSound = true;
            settings.AfkNotify = true;
            settings.CooldownSeconds = 7;
            settings.BannerSeconds = 11;
            settings.Paused = true;
            settings.PanelTopMost = false;
            settings.PanelX = 40;
            settings.PanelY = 80;
            settings.HpPinX = 0.1;
            settings.HpPinY = 0.2;
            settings.HpPinW = 0.3;
            settings.HpPinH = 0.05;
            settings.PotionEnabled = false;
            settings.PotionSeconds = 22;
            settings.PotionSound = false;
            settings.PotionNotify = true;
            settings.BuffPinX = 0.05;
            settings.BuffPinY = 0.02;
            settings.BuffPinW = 0.4;
            settings.BuffPinH = 0.08;
            string text = SettingsText.Write(settings);
            WardenSettings loaded = SettingsText.Read(text);
            Check(loaded.IdleSeconds == 40 && loaded.HpThreshold == 1500 && !loaded.HpPercent, "numeric settings round-trip");
            Check(!loaded.SoundMaster && loaded.NotifyMaster, "global channel switches round-trip");
            Check(!loaded.IdleSound && loaded.IdleNotify && loaded.HpSound && !loaded.HpNotify, "per-alert sound and notification round-trip");
            Check(!loaded.PrayerSound && !loaded.PrayerNotify && loaded.AfkSound && loaded.AfkNotify, "prayer and AFK channels round-trip");
            Check(loaded.CooldownSeconds == 7 && loaded.BannerSeconds == 11 && loaded.Paused && !loaded.PanelTopMost, "cooldown, banner and panel flags round-trip");
            Check(loaded.PanelX == 40 && loaded.PanelY == 80 && loaded.HasHpPin, "panel position and hitpoint pin round-trip");
            Check(loaded.AfkSeconds == 600 && loaded.AfkLeadSeconds == 45, "AFK timer settings round-trip");
            Check(!loaded.PotionEnabled && loaded.PotionSeconds == 22 && !loaded.PotionSound && loaded.PotionNotify && loaded.HasBuffPin, "potion alert and buff pin round-trip");
            string path = Path.Combine(Path.GetTempPath(), "afk-warden-settings-" + Guid.NewGuid().ToString("N") + ".txt");
            SettingsText.Save(path, settings);
            WardenSettings fromDisk = SettingsText.Load(path);
            File.Delete(path);
            Check(!fromDisk.SoundMaster && fromDisk.HpNotify == false && fromDisk.IdleSeconds == 40, "settings file survives a reload");
            WardenSettings defaults = SettingsText.Read("");
            Check(defaults.SoundMaster && defaults.NotifyMaster && defaults.IdleSound && defaults.AfkNotify, "missing file keeps both channels on");
        }

        private static void Gates()
        {
            AlertGate gate = new AlertGate();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Check(gate.ShouldFire(true, t, TimeSpan.FromSeconds(20)), "first breach fires");
            Check(!gate.ShouldFire(true, t.AddSeconds(5), TimeSpan.FromSeconds(20)), "holding the breach does not fire again");
            Check(!gate.ShouldFire(false, t.AddSeconds(6), TimeSpan.FromSeconds(20)) && gate.Armed, "recovery rearms");
            Check(!gate.ShouldFire(true, t.AddSeconds(10), TimeSpan.FromSeconds(20)), "cooldown blocks the next fire");
            Check(gate.ShouldFire(true, t.AddSeconds(21), TimeSpan.FromSeconds(20)), "fire after recovery and cooldown");
        }

        private static void Levels()
        {
            LevelWatch watch = new LevelWatch();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Check(watch.Tick(20, 100, true, 30, true, t, TimeSpan.FromSeconds(20)), "20 percent fires a 30 percent hitpoint alert");
            int boosted;
            Check(Thresholds.TryMeasure(1633, 1060, true, out boosted) && boosted > 100 && boosted == 154, "boosted life 1,633/1,060 is over 100 percent and is not clamped, got " + boosted.ToString());
            Check(!Thresholds.IsLow(1633, 1060, true, 30), "life above the cap is not a low-hitpoint alert");
            int prayerPct;
            Check(Thresholds.TryMeasure(1040, 990, true, out prayerPct) && prayerPct > 100, "boosted prayer 1,040/990 stays above 100 percent, got " + prayerPct.ToString());
            Check(!Thresholds.IsLow(1040, 990, true, 20), "prayer above the cap is not a low-prayer alert");
            Check(watch.Low && !watch.Tick(22, 100, true, 30, true, t.AddSeconds(1), TimeSpan.FromSeconds(20)), "still low, inside the margin, does not refire or recover");
            Check(watch.Low && !watch.Tick(null, null, true, 30, true, t.AddSeconds(2), TimeSpan.FromSeconds(20)), "a missing reading is not a recovery");
            Check(!watch.Tick(21, 100, true, 30, true, t.AddSeconds(3), TimeSpan.FromSeconds(20)), "the same low reading still does not refire");
            Check(!watch.Tick(80, 100, true, 30, true, t.AddSeconds(4), TimeSpan.FromSeconds(20)) && !watch.Low, "crossing the margin recovers");
            Check(!watch.Tick(20, 100, true, 30, true, t.AddSeconds(10), TimeSpan.FromSeconds(20)), "cooldown still applies after recovery");
            Check(watch.Tick(20, 100, true, 30, true, t.AddSeconds(25), TimeSpan.FromSeconds(20)), "the alert fires again after cooldown");
            Check(!WardenSettings.Comparable(true, false, false), "percent mode waits until the maximum is known");
            Check(WardenSettings.Comparable(true, true, true), "an orb fill is a percent");
            Check(!WardenSettings.Comparable(false, true, true), "absolute mode does not treat an orb fill as hitpoints");
        }

        private static WatchSample Sample(DateTime time, bool attached, bool pixels, bool moving, bool input)
        {
            WatchSample sample = new WatchSample();
            sample.Time = time;
            sample.GameAttached = attached;
            sample.PixelsFresh = pixels;
            sample.CharacterMoving = moving;
            sample.InputActivity = input;
            return sample;
        }

        private static void IdleAndAfk()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.HpEnabled = false;
            settings.PrayerEnabled = false;
            settings.IdleSeconds = 12;
            settings.AfkSeconds = 300;
            settings.AfkLeadSeconds = 30;
            settings.CooldownSeconds = 20;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            engine.Tick(Sample(t, true, true, false, false), settings);
            List<AlertEvent> early = engine.Tick(Sample(t.AddSeconds(5), true, true, false, false), settings);
            Check(Find(early, "idle") == null && Find(early, "afk") == null, "five quiet seconds is not idle and not an AFK warning");
            List<AlertEvent> idle = engine.Tick(Sample(t.AddSeconds(12), true, true, false, true), settings);
            AlertEvent idleEvent = Find(idle, "idle");
            Check(idleEvent != null && idleEvent.Sound && idleEvent.Notify, "stillness fires idle while input keeps the AFK timer reset");
            Check(Find(engine.Tick(Sample(t.AddSeconds(20), true, true, false, true), settings), "idle") == null, "idle does not repeat while you stay still");
            engine.Tick(Sample(t.AddSeconds(13), true, true, true, true), settings);
            Check(!engine.IdleLatched, "animation clears idle");
            engine.Tick(Sample(t.AddSeconds(14), true, true, false, true), settings);
            List<AlertEvent> cooled = engine.Tick(Sample(t.AddSeconds(26), true, true, false, true), settings);
            Check(Find(cooled, "idle") == null, "idle cooldown blocks the return of stillness");
            Check(Find(engine.Tick(Sample(t.AddSeconds(34), true, true, false, true), settings), "idle") != null, "idle fires again after cooldown");
        }

        private static void Background()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.IdleSeconds = 12;
            settings.AfkSeconds = 300;
            settings.AfkLeadSeconds = 30;
            settings.CooldownSeconds = 0;
            settings.HpEnabled = true;
            settings.HpPercent = true;
            settings.HpThreshold = 30;
            settings.PrayerEnabled = false;
            settings.SoundMaster = true;
            settings.NotifyMaster = false;
            settings.AfkNotify = true;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WatchSample start = Sample(t, true, true, true, false);
            start.Hp = 90;
            start.HpMax = 100;
            engine.Tick(start, settings);
            WatchSample hidden = Sample(t.AddSeconds(270), true, false, false, false);
            hidden.Hp = 10;
            hidden.HpMax = 100;
            List<AlertEvent> events = engine.Tick(hidden, settings);
            AlertEvent afk = Find(events, "afk");
            Check(afk != null && afk.Sound && !afk.Notify, "minimized client still warns about the AFK timer, and only on the channels that are on");
            Check(Find(events, "idle") == null, "time the window was unreadable is not counted as idle");
            Check(Find(events, "hp") == null && !engine.HpLatched, "a hidden client does not invent a low-hitpoint reading");
            WatchSample seen = Sample(t.AddSeconds(271), true, true, true, true);
            seen.Hp = 10;
            seen.HpMax = 100;
            List<AlertEvent> hp = engine.Tick(seen, settings);
            Check(Find(hp, "hp") != null, "hitpoints alert as soon as the client can be read again");
            WardenEngine covered = new WardenEngine();
            covered.Tick(Sample(t, true, true, false, false), settings);
            List<AlertEvent> stillHidden = covered.Tick(Sample(t.AddMinutes(30), true, false, false, false), settings);
            Check(Find(stillHidden, "idle") == null, "a long minimize does not mature the idle alert");
            Check(Find(stillHidden, "afk") != null, "the inactivity timer keeps running with no pixels");
        }

        private static void Capture()
        {
            Check(CapturePolicy.Choose(false, false, true, false, true) == CaptureRoute.Missing, "no game window");
            Check(CapturePolicy.Choose(true, true, true, false, true) == CaptureRoute.Minimized, "minimized wins over every capture path");
            Check(CapturePolicy.Choose(true, false, true, false, true) == CaptureRoute.WindowSurface, "a visible unfocused client uses the window surface when it is offered");
            Check(CapturePolicy.Choose(true, false, true, true, true) == CaptureRoute.WindowSurface, "a covered client is still read from the window surface");
            Check(CapturePolicy.Choose(true, false, true, false, false) == CaptureRoute.Screen, "without window capture, a visible client is read from the screen");
            Check(CapturePolicy.Choose(true, false, true, true, false) == CaptureRoute.Covered, "a covered client cannot be read from the screen alone");
            Check(!CapturePolicy.PixelsAvailable(CaptureRoute.Minimized) && CapturePolicy.PixelsAvailable(CaptureRoute.Screen), "only real frames count as pixels");
            bool[] clear = new bool[] { true, true, true, true, true, true, true, true, true };
            bool[] partial = new bool[] { true, false, false, false, false, false, false, false, false };
            bool[] blocked = new bool[] { false, false, false, false, false, false, false, false, false };
            Check(VisibilitySamples.MostlyClear(clear) && !VisibilitySamples.MostlyClear(partial) && VisibilitySamples.Any(partial) && !VisibilitySamples.Any(blocked), "partial cover still has a screen sample; full cover does not");
        }

        private static void Input()
        {
            Check(InputActivity.IsActivity(false, false, true, true, false, false), "cursor motion over an unfocused game resets the timer");
            Check(!InputActivity.IsActivity(false, false, false, false, false, true), "keys typed into another app do not reset it");
            Check(InputActivity.IsActivity(false, true, false, false, false, true), "keys reset it while the game is focused");
            Check(!InputActivity.IsActivity(true, false, true, true, true, true), "a minimized game receives none of this input");
            Check(!InputActivity.IsActivity(false, false, false, true, true, false), "clicks on the window that covers the game do not count");
        }

        private static void Placement()
        {
            IntRect alert = AlertPlacement.Place(0, 0, 1600, 900, 380, 92, 0);
            IntRect game = new IntRect(40, 40, 400, 300);
            Check(alert.X > game.Right && alert.Y + alert.Height <= 900, "the alert sits on the work area, not inside the game client");
            Check(alert.Right <= 1600 && alert.Bottom <= 900, "the alert stays inside the work area");
        }

        private static void Windows()
        {
            Check(WindowScore.Score("Kavros", "Kavros", "JagWindow", 1024, 768) >= 100, "a Kavros client scores as the game");
            Check(WindowScore.Score("Kavros Launcher", "launcher", "Launcher", 800, 600) < 0, "the launcher is not the client");
            Check(WindowScore.Score("AFK Warden", "KavrosAfkWarden", "WindowsForms", 300, 200) < 0, "the module does not select itself");
            Check(WindowScore.Score("RuneScape", "rs2client", "SunAwtFrame", 1024, 768) < 0, "another game with no Kavros title is ignored");
            int px, py;
            PanelDock.Place(100, 80, 900, 600, 400, 262, 12, 72, 0, 0, 1920, 1080, out px, out py);
            Check(px == 112 && py == 152, "the panel keeps its offset inside the chosen client");
            Check(!PanelDock.Visible(true, true, true, true, false, false), "a minimized client hides the panel");
            Check(PanelDock.Visible(true, true, false, false, false, false), "tabbing out keeps the panel owned by the game instead of hiding it");
            Check(PanelDock.Visible(true, true, false, true, false, false), "the panel shows while the chosen client is in front");
            Check(PanelDock.Visible(true, false, false, false, false, true), "settings keeps the panel available while it is focused");
        }

        private static void Motion()
        {
            ImageBuffer still = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(20, 30, 40));
            MotionDetector detector = new MotionDetector();
            Check(!detector.Moved(still), "the first frame is a baseline, not movement");
            Check(!detector.Moved(still), "an unchanged client is not movement");
            ImageBuffer moved = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(20, 30, 40));
            IntRect world = MotionDetector.WorldRect(200, 160);
            moved.Fill(world.X, world.Y, world.Width, world.Height / 2, ImageBuffer.Pack(180, 40, 40));
            Check(detector.Moved(moved), "a large change in the character view is movement");
            MotionDetector dot = new MotionDetector();
            ImageBuffer map = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(12, 16, 20));
            IntRect mini = MotionDetector.MinimapRect(200, 160);
            map.Fill(mini.X + 4, mini.Y + 4, 6, 6, ImageBuffer.Pack(240, 230, 160));
            Check(!dot.Moved(map), "the first minimap dot is not movement");
            ImageBuffer shifted = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(12, 16, 20));
            shifted.Fill(mini.X + 20, mini.Y + 20, 6, 6, ImageBuffer.Pack(240, 230, 160));
            Check(dot.Moved(shifted), "a moved minimap dot is movement");
        }

        private static void DrawGlyph(ImageBuffer image, int digit, int x, int y, int scale, int color)
        {
            string glyph = DigitFont.Glyphs[digit];
            for (int py = 0; py < 7; py++)
                for (int px = 0; px < 5; px++)
                    if (glyph[py * 5 + px] == '1')
                        image.Fill(x + px * scale, y + py * scale, scale, scale, color);
        }

        private static void DrawLifeNumber(ImageBuffer image, int x, int y, int[] current, int[] max, int color)
        {
            int dx = x;
            for (int i = 0; i < current.Length; i++)
            {
                if (i > 0 && i % 3 == current.Length % 3) { image.Fill(dx, y + 12, 3, 3, color); dx += 6; }
                DrawGlyph(image, current[i], dx, y, 2, color);
                dx += 12;
            }
            DrawSlash(image, dx, y, color);
            dx += 10;
            for (int i = 0; i < max.Length; i++)
            {
                if (i > 0 && i % 3 == max.Length % 3) { image.Fill(dx, y + 12, 3, 3, color); dx += 6; }
                DrawGlyph(image, max[i], dx, y, 2, color);
                dx += 12;
            }
        }

        private static void DrawSlash(ImageBuffer image, int x, int y, int color)
        {
            for (int i = 0; i < 12; i++)
            {
                image.Set(x + i / 2, y + i, color);
                image.Set(x + i / 2 + 1, y + i, color);
            }
        }

        private static void Digits()
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            for (int digit = 0; digit < 10; digit++)
            {
                ImageBuffer image = ImageBuffer.Blank(80, 50, ImageBuffer.Pack(8, 12, 16));
                DrawGlyph(image, 1, 8, 8, 3, white);
                DrawGlyph(image, digit, 26, 8, 3, white);
                WardenSettings settings = new WardenSettings();
                settings.HpPinX = 0;
                settings.HpPinY = 0;
                settings.HpPinW = 1;
                settings.HpPinH = 1;
                settings.PrayerPinW = 0;
                HudReading reading = HudReader.Read(image, settings);
                int expected = 10 + digit;
                if (reading.Hp != expected) failures.Add("digit " + digit.ToString() + " read as " + (reading.Hp == null ? "nothing" : reading.Hp.Value.ToString()) + " " + HudReader.Explain(image, new IntRect(0, 0, image.Width, image.Height)));
                assertions++;
            }
        }

        private static void Hud()
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            int green = ImageBuffer.Pack(40, 186, 70);
            int orb = ImageBuffer.Pack(210, 36, 36);
            ImageBuffer image = ImageBuffer.Blank(640, 220, ImageBuffer.Pack(10, 16, 22));
            int y = 120;
            int red = ImageBuffer.Pack(196, 42, 42);
            int purple = ImageBuffer.Pack(142, 70, 196);
            int yellow = ImageBuffer.Pack(214, 184, 48);
            image.Fill(40, y, 150, 8, red);
            DrawLifeNumber(image, 40, y - 22, new int[] { 1, 6, 3, 3 }, new int[] { 1, 0, 6, 0 }, white);
            image.Fill(210, y, 90, 8, yellow);
            DrawGlyph(image, 1, 210, y - 22, 2, white);
            DrawGlyph(image, 3, 222, y - 22, 2, white);
            DrawGlyph(image, 5, 234, y - 22, 2, white);
            image.Fill(320, y, 140, 8, purple);
            DrawLifeNumber(image, 320, y - 22, new int[] { 1, 0, 4, 0 }, new int[] { 9, 9, 0 }, white);
            image.Fill(480, y, 80, 8, green);
            DrawGlyph(image, 6, 480, y - 22, 2, white);
            DrawGlyph(image, 0, 492, y - 22, 2, white);
            DrawSlash(image, 506, y - 22, white);
            DrawGlyph(image, 6, 516, y - 22, 2, white);
            DrawGlyph(image, 0, 528, y - 22, 2, white);
            int cx = 320, cy = 40, radius = 24;
            for (int py = -radius; py <= radius; py++)
                for (int px = -radius; px <= radius; px++)
                {
                    if (px * px + py * py > radius * radius) continue;
                    bool edge = px * px + py * py > (radius - 2) * (radius - 2);
                    if (py >= 0 || edge) image.Set(cx + px, cy + py, orb);
                }
            WardenSettings settings = new WardenSettings();
            HudReading reading = HudReader.Read(image, settings);
            Check(reading.Hp == 1633 && reading.HpMax == 1060 && reading.HpHow == "bar", "red life bar reads 1,633/1,060 even when current exceeds max, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()) + " " + reading.HpHow + " " + HudReader.Explain(image, new IntRect(32, 88, 160, 40)));
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990 && reading.PrayerHow == "bar", "purple prayer bar reads 1,040/990, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()));
            ImageBuffer pinned = ImageBuffer.Blank(200, 80, ImageBuffer.Pack(10, 16, 22));
            DrawGlyph(pinned, 4, 10, 20, 3, white);
            DrawGlyph(pinned, 2, 27, 20, 3, white);
            WardenSettings pin = new WardenSettings();
            pin.HpPinX = 0;
            pin.HpPinY = 0;
            pin.HpPinW = 1;
            pin.HpPinH = 1;
            HudReading pinnedReading = HudReader.Read(pinned, pin);
            Check(pinnedReading.Hp == 42 && pinnedReading.HpHow == "pin", "a pinned rectangle overrides the automatic search");
            ImageBuffer wideNumber = ImageBuffer.Blank(520, 80, ImageBuffer.Pack(10, 16, 22));
            int redWide = ImageBuffer.Pack(196, 42, 42);
            wideNumber.Fill(20, 48, 220, 8, redWide);
            DrawLifeNumber(wideNumber, 20, 26, new int[] { 1, 2, 3, 4, 5, 6, 7 }, new int[] { 1, 0, 0, 0, 0, 0, 0 }, white);
            int purpleWide = ImageBuffer.Pack(142, 70, 196);
            wideNumber.Fill(270, 48, 200, 8, purpleWide);
            DrawLifeNumber(wideNumber, 270, 26, new int[] { 1, 0, 4, 0 }, new int[] { 9, 9, 0 }, white);
            HudReading wideReading = HudReader.Read(wideNumber, new WardenSettings());
            Check(wideReading.Hp == 1234567 && wideReading.HpMax == 1000000, "a seven-digit boosted life value is kept, got " + (wideReading.Hp == null ? "none" : wideReading.Hp.Value.ToString()) + "/" + (wideReading.HpMax == null ? "-" : wideReading.HpMax.Value.ToString()) + " " + HudReader.Explain(wideNumber, new IntRect(10, 16, 240, 40)));
            Check(wideReading.Prayer == 1040 && wideReading.PrayerMax == 990, "prayer above its cap is kept");
            int sevenPct;
            Check(Thresholds.TryMeasure(1234567, 1000000, true, out sevenPct) && sevenPct == 123, "seven-digit life uses the cap as the percent base, got " + sevenPct.ToString());
        }

        private static void Potions()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.IdleEnabled = false;
            settings.HpEnabled = false;
            settings.PrayerEnabled = false;
            settings.AfkEnabled = false;
            settings.PotionSeconds = 15;
            settings.CooldownSeconds = 20;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WatchSample high = Sample(t, true, true, true, false);
            high.PotionSeconds = 40;
            Check(Find(engine.Tick(high, settings), "potion") == null && !engine.PotionLatched, "40 seconds left does not fire a 15 second potion alert");
            WatchSample low = Sample(t.AddSeconds(1), true, true, true, false);
            low.PotionSeconds = 10;
            List<AlertEvent> fired = engine.Tick(low, settings);
            AlertEvent potion = Find(fired, "potion");
            Check(potion != null && potion.Sound && potion.Notify && engine.PotionLatched, "10 seconds left fires the potion alert on both channels");
            low.Time = t.AddSeconds(2);
            Check(Find(engine.Tick(low, settings), "potion") == null && engine.PotionLatched, "the potion alert does not refire while the timer stays low");
            WatchSample missed = Sample(t.AddSeconds(3), true, true, true, false);
            Check(Find(engine.Tick(missed, settings), "potion") == null && engine.PotionLatched, "a missed frame is not a potion recovery");
            WatchSample margin = Sample(t.AddSeconds(4), true, true, true, false);
            margin.PotionSeconds = 16;
            Check(Find(engine.Tick(margin, settings), "potion") == null && engine.PotionLatched, "16 seconds is still inside the recovery margin");
            WatchSample recovered = Sample(t.AddSeconds(5), true, true, true, false);
            recovered.PotionSeconds = 30;
            Check(Find(engine.Tick(recovered, settings), "potion") == null && !engine.PotionLatched, "30 seconds recovers the potion alert");
            low.Time = t.AddSeconds(10);
            Check(Find(engine.Tick(low, settings), "potion") == null, "potion cooldown blocks the return");
            low.Time = t.AddSeconds(26);
            Check(Find(engine.Tick(low, settings), "potion") != null, "the potion alert fires again after cooldown");
            WatchSample gone = Sample(t.AddSeconds(27), true, true, true, false);
            gone.PotionClear = true;
            Check(Find(engine.Tick(gone, settings), "potion") == null && !engine.PotionLatched, "no buff timers means the potion is no longer running out");
            settings.PotionSound = false;
            low.Time = t.AddSeconds(50);
            AlertEvent quiet = Find(engine.Tick(low, settings), "potion");
            Check(quiet != null && !quiet.Sound && quiet.Notify, "potion sound can be off while its notification stays on");

            int white = ImageBuffer.Pack(236, 236, 236);
            ImageBuffer bar = ImageBuffer.Blank(420, 90, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(bar, 16, 24, "60");
            DrawBuffIcon(bar, 64, 24, "11m");
            DrawBuffIcon(bar, 112, 24, "4");
            DrawBuffIcon(bar, 160, 24, "");
            var readClock = System.Diagnostics.Stopwatch.StartNew();
            ImageBuffer wide = ImageBuffer.Blank(1920, 1080, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(wide, 40, 40, "11m");
            HudReading wideRead = HudReader.Read(wide, new WardenSettings());
            readClock.Stop();
            Check(wideRead.BuffBarFound && wideRead.Buffs.Count >= 1 && readClock.ElapsedMilliseconds < 250, "a 1080p buff search stays under 250 ms, took " + readClock.ElapsedMilliseconds.ToString() + " ms");
            Console.WriteLine("hud-read-1080p-ms=" + readClock.ElapsedMilliseconds.ToString());
            HudReading icons = HudReader.Read(bar, new WardenSettings());
            Check(icons.BuffBarFound && icons.Buffs.Count == 4, "green-bordered buff icons are found in a row, got " + icons.Buffs.Count.ToString());
            Check(icons.Buffs.Count > 0 && icons.Buffs[0].HasTime && !icons.Buffs[0].Minutes && icons.Buffs[0].Value == 60, "a plain buff number is seconds");
            Check(icons.Buffs.Count > 1 && icons.Buffs[1].Minutes && icons.Buffs[1].Value == 11, "11m is eleven minutes, not eleven seconds, got has=" + (icons.Buffs.Count > 1 && icons.Buffs[1].HasTime) + " min=" + (icons.Buffs.Count > 1 && icons.Buffs[1].Minutes) + " value=" + (icons.Buffs.Count > 1 ? icons.Buffs[1].Value.ToString() : "-"));
            Check(icons.Buffs.Count > 2 && icons.Buffs[2].HasTime && !icons.Buffs[2].Minutes && icons.Buffs[2].Value == 4, "a stack count is still read as a number");
            Check(icons.Buffs.Count > 3 && !icons.Buffs[3].HasTime, "an icon with no text has no timer");
            BuffClock clock = new BuffClock();
            int? first = clock.Tick(icons.Buffs);
            Check(first != null && first.Value == 660, "only the minute timer counts until a countdown is confirmed, got " + (first == null ? "none" : first.Value.ToString()));
            List<BuffIcon> dropped = new List<BuffIcon>();
            BuffIcon seconds = new BuffIcon();
            seconds.X = icons.Buffs[1].X;
            seconds.HasTime = true;
            seconds.Value = 10;
            dropped.Add(icons.Buffs[0]);
            dropped.Add(seconds);
            dropped.Add(icons.Buffs[2]);
            int? lowest = clock.Tick(dropped);
            Check(lowest != null && lowest.Value == 10, "after 11m becomes 10 seconds that icon can alert, got " + (lowest == null ? "none" : lowest.Value.ToString()));
        }

        private static void DrawBuffIcon(ImageBuffer image, int x, int y, string text)
        {
            int green = ImageBuffer.Pack(80, 210, 90);
            int dark = ImageBuffer.Pack(14, 20, 32);
            int white = ImageBuffer.Pack(236, 236, 236);
            int size = 40;
            image.Fill(x, y, size, 3, green);
            image.Fill(x, y + size - 3, size, 3, green);
            image.Fill(x, y, 3, size, green);
            image.Fill(x + size - 3, y, 3, size, green);
            image.Fill(x + 3, y + 3, size - 6, size - 6, dark);
            int tx = x + 4;
            int ty = y + size - 18;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= '0' && ch <= '9')
                {
                    DrawGlyph(image, ch - '0', tx, ty, 2, white);
                    tx += 12;
                }
                else if (ch == 'm')
                {
                    image.Fill(tx, ty + 2, 2, 10, white);
                    image.Fill(tx + 4, ty + 2, 2, 10, white);
                    image.Fill(tx + 8, ty + 2, 2, 10, white);
                    image.Fill(tx, ty + 2, 10, 2, white);
                    tx += 12;
                }
            }
        }

        private static void LiveCapture(string root)
        {
            string path = Path.Combine(root, "verification", "fixtures", "debug-capture.png");
            if (!File.Exists(path))
            {
                failures.Add("missing live fixture " + path);
                assertions++;
                return;
            }
            ImageBuffer image = PngImage.Load(path);
            HudReading reading = HudReader.Read(image, new WardenSettings());
            Check(reading.Hp == 1293 && reading.HpMax == 1060 && reading.Hp > reading.HpMax, "live capture life is the red bar 1293/1060, above the cap, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()));
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990 && reading.Prayer > reading.PrayerMax, "live capture prayer is the purple bar 1040/990, above the cap, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()));
            Check(reading.Hp != 60 && reading.Hp != 6060 && reading.Hp != 66, "summoning 60/60 is not reported as hitpoints");
            int seconds = 0, minutes = 0;
            if (reading.Buffs != null)
            {
                for (int i = 0; i < reading.Buffs.Count; i++)
                {
                    if (!reading.Buffs[i].HasTime) continue;
                    if (reading.Buffs[i].Minutes && reading.Buffs[i].Value == 10) minutes++;
                    if (!reading.Buffs[i].Minutes && reading.Buffs[i].Value == 60) seconds++;
                }
            }
            string seen = "";
            if (reading.Buffs != null)
                for (int i = 0; i < reading.Buffs.Count; i++)
                    seen += reading.BuffRects[i].X.ToString() + ":" + (reading.Buffs[i].HasTime ? (reading.Buffs[i].Minutes ? reading.Buffs[i].Value.ToString() + "m" : reading.Buffs[i].Value.ToString()) : "-") + " ";
            Check(reading.BuffBarFound && seconds >= 1 && minutes >= 4, "live buff row reads 60 seconds and four 10 minute icons, got " + seen);
            SceneActivity scene = new SceneActivity();
            Check(scene.Tick(image, reading), "the combat frame is activity, so idle does not start");
            SceneActivity calm = new SceneActivity();
            ImageBuffer trees = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(20, 80, 30));
            ImageBuffer sway = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(30, 110, 40));
            sway.Fill(40, 40, 200, 120, ImageBuffer.Pack(40, 140, 50));
            Check(!calm.Tick(trees, new HudReading()), "the first scene is not activity");
            Check(!calm.Tick(sway, new HudReading()), "swaying trees and water are not player activity");
            StableValue steady = new StableValue();
            steady.Need = 3;
            Check(steady.Push(1040) == null, "one prayer frame is not shown");
            Check(steady.Push(1040) == null, "two prayer frames are not enough");
            Check(steady.Push(1040) == 1040, "three consistent prayer reads are shown");
            Check(steady.Push(400) == 1040, "a single prayer jump is rejected");
        }

        private static void Manifest(string root)
        {
            Check(AppInfo.Version == "0.2.3" && AppInfo.Name == "AFK Warden" && AppInfo.Author == "Zenity", "module identity");
            if (string.IsNullOrEmpty(root)) return;
            string jsonPath = Path.Combine(root, "companion.module.json");
            string manifestPath = Path.Combine(root, "source", "app.manifest");
            if (!File.Exists(jsonPath) || !File.Exists(manifestPath))
            {
                failures.Add("missing companion.module.json or app.manifest under " + root);
                return;
            }
            string json = File.ReadAllText(jsonPath);
            string manifest = File.ReadAllText(manifestPath);
            Check(json.IndexOf("\"version\": \"0.2.3\"") >= 0, "public module manifest version");
            Check(json.IndexOf("\"name\": \"AFK Warden\"") >= 0 && json.IndexOf("\"author\": \"Zenity\"") >= 0, "public module manifest name and author");
            Check(json.IndexOf("\"id\": \"afk-warden\"") >= 0, "public module id");
            Check(manifest.IndexOf("version=\"0.2.3.0\"") >= 0 && manifest.IndexOf("name=\"KavrosAfkWarden\"") >= 0, "assembly manifest version");
        }
    }
}
