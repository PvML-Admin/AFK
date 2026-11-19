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
            string text = SettingsText.Write(settings);
            WardenSettings loaded = SettingsText.Read(text);
            Check(loaded.IdleSeconds == 40 && loaded.HpThreshold == 1500 && !loaded.HpPercent, "numeric settings round-trip");
            Check(!loaded.SoundMaster && loaded.NotifyMaster, "global channel switches round-trip");
            Check(!loaded.IdleSound && loaded.IdleNotify && loaded.HpSound && !loaded.HpNotify, "per-alert sound and notification round-trip");
            Check(!loaded.PrayerSound && !loaded.PrayerNotify && loaded.AfkSound && loaded.AfkNotify, "prayer and AFK channels round-trip");
            Check(loaded.CooldownSeconds == 7 && loaded.BannerSeconds == 11 && loaded.Paused && !loaded.PanelTopMost, "cooldown, banner and panel flags round-trip");
            Check(loaded.PanelX == 40 && loaded.PanelY == 80 && loaded.HasHpPin, "panel position and hitpoint pin round-trip");
            Check(loaded.AfkSeconds == 600 && loaded.AfkLeadSeconds == 45, "AFK timer settings round-trip");
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

        private static void DrawSlash(ImageBuffer image, int x, int y, int color)
        {
            for (int i = 0; i < 28; i++)
            {
                int px = x + i / 2;
                int py = y + i;
                image.Fill(px, py, 2, 2, color);
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
            int blue = ImageBuffer.Pack(50, 90, 210);
            int red = ImageBuffer.Pack(210, 36, 36);
            ImageBuffer image = ImageBuffer.Blank(400, 400, ImageBuffer.Pack(10, 16, 22));
            int y = 340;
            image.Fill(40, y, 120, 8, green);
            int dx = 40;
            int[] hp = new int[] { 2, 5, 0, 0 };
            for (int i = 0; i < hp.Length; i++)
            {
                DrawGlyph(image, hp[i], dx, y - 24, 3, white);
                dx += 17;
            }
            DrawSlash(image, dx, y - 26, white);
            dx += 16;
            int[] max = new int[] { 1, 0, 0, 0, 0 };
            for (int i = 0; i < max.Length; i++)
            {
                DrawGlyph(image, max[i], dx, y - 24, 3, white);
                dx += 17;
            }
            image.Fill(40, 300, 90, 8, blue);
            dx = 40;
            DrawGlyph(image, 8, dx, 276, 3, white);
            DrawGlyph(image, 0, dx + 17, 276, 3, white);
            DrawSlash(image, dx + 34, 274, white);
            DrawGlyph(image, 4, dx + 50, 276, 3, white);
            DrawGlyph(image, 0, dx + 67, 276, 3, white);
            DrawGlyph(image, 0, dx + 84, 276, 3, white);
            int cx = 320, cy = 40, radius = 24;
            for (int py = -radius; py <= radius; py++)
                for (int px = -radius; px <= radius; px++)
                {
                    if (px * px + py * py > radius * radius) continue;
                    bool edge = px * px + py * py > (radius - 2) * (radius - 2);
                    if (py >= 0 || edge) image.Set(cx + px, cy + py, red);
                }
            WardenSettings settings = new WardenSettings();
            HudReading reading = HudReader.Read(image, settings);
            Check(reading.Hp == 2500 && reading.HpMax == 10000 && reading.HpHow == "bar", "life bar reads current and maximum, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()) + " " + reading.HpHow + " " + HudReader.Explain(image, new IntRect(0, 260, 400, 140)));
            Check(reading.Prayer == 80 && reading.PrayerMax == 400 && !reading.PrayerFromOrb, "prayer bar reads current and maximum, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()) + " " + reading.PrayerHow);
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
        }

        private static void Manifest(string root)
        {
            Check(AppInfo.Version == "0.1.0" && AppInfo.Name == "AFK Warden" && AppInfo.Author == "Kavros", "module identity");
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
            Check(json.IndexOf("\"version\": \"0.1.0\"") >= 0, "public module manifest version");
            Check(json.IndexOf("\"name\": \"AFK Warden\"") >= 0 && json.IndexOf("\"author\": \"Kavros\"") >= 0, "public module manifest name and author");
            Check(json.IndexOf("\"id\": \"afk-warden\"") >= 0, "public module id");
            Check(manifest.IndexOf("version=\"0.1.0.0\"") >= 0 && manifest.IndexOf("name=\"KavrosAfkWarden\"") >= 0, "assembly manifest version");
        }
    }
}
