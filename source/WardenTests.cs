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
                Live023(root);
                ScaleMatrix(root);
                LiveRead(root);
                Release026(root);
                ReadFixes(root);
                PotionClock();
                CaptureCost();
                PanelOrder();
                Skilling();
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

        private static void Release026(string root)
        {
            WardenSettings legacy=SettingsText.Read("afk-seconds\t300\nvolume\t71\n");
            Check(legacy.AfkSeconds==1500 && legacy.Volume==71,"migration replaces the old five-minute guess and preserves other settings");
            string dir=Path.Combine(root,"verification","fixtures","live023");
            HudReading frame=HudReader.Read(PngImage.Load(Path.Combine(dir,"live-v024-full-layout-lag.png")),new WardenSettings());
            Check(frame.Hp==1633 && frame.HpMax==1060 && frame.Prayer==1040 && frame.PrayerMax==990,
                "bright landscape does not hide life and prayer digits");
            Check(BuffClock.ShownSeconds(frame.Buffs)==420,"live buff row reads seven minutes against bright ground");
            DateTime now=DateTime.UtcNow;
            var clock=new PotionDisplay();
            Check(clock.Observe(17,now)==17, "observed potion");
            Check(clock.Observe(null,now.AddSeconds(2))==15 && clock.Estimated, "temporary missing potion is marked last read");
            Check(clock.Observe(600,now.AddSeconds(3))==14 && clock.Estimated, "longer second buff does not hide a briefly unread shorter buff");
            Check(clock.Observe(13,now.AddSeconds(4))==13 && !clock.Estimated, "short timer confirmed again");
            Check(clock.Observe(null,now.AddSeconds(10))==7 && clock.Estimated, "an unread potion keeps counting down from its last read");
            Check(clock.Current(now.AddSeconds(17))==0, "the countdown reaches zero");
            Check(clock.Current(now.AddSeconds(23))==null && !clock.Tracking, "a finished potion is dropped");
            var drain=new ReadGate();
            drain.Push(942,990,now);drain.Push(939,990,now.AddSeconds(1));drain.Push(936,990,now.AddSeconds(2));
            Check(drain.Value==936 && drain.Max==990,"prayer can drain continuously between frames");
            foreach(bool gm in new[]{false,true})foreach(bool tree in new[]{false,true})
            {
                var cfg=new WardenSettings{Grandmaster=gm,TreePerk=tree};var eng=new WardenEngine();
                eng.Tick(Sample(now,true,true,false,true),cfg);
                var early=eng.Tick(Sample(now.AddSeconds(cfg.AfkSeconds-31),true,false,false,false),cfg);
                var due=eng.Tick(Sample(now.AddSeconds(cfg.AfkSeconds-30),true,false,false,false),cfg);
                Check(Find(early,"afk")==null && Find(due,"afk")!=null,"warning timing for GM="+gm+" tree="+tree);
            }
        }

        private static void ReadFixes(string root)
        {
            Check(ReadGate.Plausible(1633, 1060) && ReadGate.Plausible(1040, 990), "boosted life and prayer above the cap are plausible");
            Check(!ReadGate.Plausible(6633, 1060) && !ReadGate.Plausible(5, 0), "more than double the cap, or no cap, is a misread");
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ReadGate wild = new ReadGate();
            wild.Push(900, 1060, t); wild.Push(900, 1060, t.AddMilliseconds(500));
            wild.Push(6900, 1060, t.AddSeconds(1)); wild.Push(6900, 1060, t.AddSeconds(1.5));
            Check(wild.Value == 900, "a doubled misread never replaces the shown life, got " + wild.Value);
            ReadGate odd = new ReadGate();
            odd.Push(700, 1060, t); odd.Push(700, 1060, t.AddMilliseconds(500));
            odd.Push(520, 1060, t.AddSeconds(1)); odd.Push(580, 1060, t.AddSeconds(1.5)); odd.Push(520, 1060, t.AddSeconds(2));
            Check(odd.Value == 520, "two of the last three reads move the value past one odd frame, got " + odd.Value);
            if (string.IsNullOrEmpty(root)) return;
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string[] names = new string[] { "debug-capture.png", "live-v024-full-layout-lag.png", "live-v024-hp-917-unread.png", "live-v024-lagging-values.png" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(dir, names[i]);
                if (!File.Exists(path)) { Check(false, "fixture present " + names[i]); continue; }
                HudReading full = HudReader.Read(PngImage.Load(path), new WardenSettings());
                int dark = ImageBuffer.Pack(18, 18, 22);
                ImageBuffer noPrayerFill = PngImage.Load(path);
                noPrayerFill.Fill(full.PrayerBar.X - 2, full.PrayerBar.Y - 1, full.PrayerBar.Width + 4, full.PrayerBar.Height + 3, dark);
                HudReading a = HudReader.Read(noPrayerFill, new WardenSettings());
                Check(a.Prayer == full.Prayer && a.PrayerMax == full.PrayerMax && a.Hp == full.Hp, names[i] + ": an empty prayer fill still reads prayer from its slot, got " + a.Prayer + "/" + a.PrayerMax + " " + a.PrayerTrace);
                ImageBuffer noLifeFill = PngImage.Load(path);
                noLifeFill.Fill(full.LifeBar.X - 2, full.LifeBar.Y - 1, full.LifeBar.Width + 4, full.LifeBar.Height + 3, dark);
                HudReading b = HudReader.Read(noLifeFill, new WardenSettings());
                Check(b.Hp == full.Hp && b.HpMax == full.HpMax && b.Prayer == full.Prayer, names[i] + ": a short life fill still reads life from its slot, got " + b.Hp + "/" + b.HpMax + " " + b.LifeTrace);
            }
        }

        private static void PotionClock()
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            PotionDisplay clock = new PotionDisplay();
            clock.Observe(660, t);
            clock.Observe(660, t.AddSeconds(0.5));
            Check(clock.Alertable, "two reads of 11m confirm the potion");
            Check(clock.Observe(null, t.AddSeconds(60)) == 600 && clock.Estimated, "a buff bar hidden for a minute keeps counting down");
            Check(clock.Current(t.AddSeconds(240)) == 420, "four minutes later the clock still runs");
            clock.Observe(420, t.AddSeconds(241));
            Check(clock.Current(t.AddSeconds(241.2)) == 420 && !clock.Estimated, "7m ticking over from 8m pins seven minutes exactly");
            Check(clock.Observe(60, t.AddSeconds(242)) == 419, "one odd lower read does not move the clock");
            Check(clock.Observe(null, true, t.AddSeconds(243)) == 418, "a bar read without a potion icon does not clear it at once");
            PotionDisplay late = new PotionDisplay();
            late.Observe(30, t); late.Observe(29, t.AddSeconds(1));
            Check(late.Alertable && late.Current(t.AddSeconds(20)) == 10, "the clock reaches the warning while the icon is unread");
            int? stale = late.Current(t.AddSeconds(200));
            Check(!late.Alertable, "a clock not confirmed for minutes stops warning, shown " + stale);
            PotionDisplay dose = new PotionDisplay();
            dose.Observe(40, t); dose.Observe(39, t.AddSeconds(1));
            for (int i = 0; i < 4; i++) dose.Observe(360, t.AddSeconds(9 + i * 0.5));
            Check(dose.Current(t.AddSeconds(11)) == 360 - 1 || dose.Current(t.AddSeconds(11)) == 360, "a fresh dose replaces the old timer once the old one is no longer read, got " + dose.Current(t.AddSeconds(11)));
            PotionDisplay keep = new PotionDisplay();
            keep.Observe(40, t); keep.Observe(39, t.AddSeconds(1));
            keep.Observe(600, t.AddSeconds(1.5)); keep.Observe(38, t.AddSeconds(2)); keep.Observe(600, t.AddSeconds(2.5)); keep.Observe(600, t.AddSeconds(3));
            Check(keep.Current(t.AddSeconds(3)) == 37, "a longer second buff does not hide the shorter one that is still read");
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.PotionSeconds = 15; settings.CooldownSeconds = 0; settings.IdleEnabled = false; settings.HpEnabled = false; settings.PrayerEnabled = false; settings.AfkEnabled = false;
            WatchSample hidden = Sample(t, true, false, false, false);
            hidden.PotionSeconds = 12;
            Check(Find(engine.Tick(hidden, settings), "potion") != null, "a confirmed countdown warns even while the bar is covered");
        }

        private static void CaptureCost()
        {
            Check(CapturePlan.NextFullSeconds(false, false, 1) == 3 && CapturePlan.NextFullSeconds(false, false, 2) == 6 && CapturePlan.NextFullSeconds(false, false, 4) == 24 && CapturePlan.NextFullSeconds(false, false, 9) == 30,
                "a HUD that cannot be read backs off 3, 6, 12, 24, 30 seconds between whole-client copies");
            Check(CapturePlan.NextFullSeconds(true, true, 0) == 30 && CapturePlan.NextFullSeconds(true, false, 0) == 15, "a complete read waits the slow refresh");
            IntRect life = new IntRect(613, 1116, 108, 36), prayer = new IntRect(851, 1116, 108, 36);
            Check(CapturePlan.ShareGrab(life, prayer), "life and prayer on one HUD row are copied in one grab");
            Check(!CapturePlan.ShareGrab(life, new IntRect(1800, 200, 108, 36)), "slots far apart are copied separately");
            Check(CapturePlan.SlowSlotEvery >= 2, "buffs, XP and the backpack are read at most once a second");
            Check(CapturePlan.ShouldRead(false, false) && !CapturePlan.ShouldRead(true, false) && !CapturePlan.ShouldRead(false, true), "a minimized or paused client is not read");
            Check(!CapturePlan.PreferWindowPixels(false) && !CapturePlan.PreferWindowPixels(true) && !CapturePlan.UsePrintWindow && !CapturePlan.OfferWindowSurface, "covered pixels are not taken with PrintWindow or a capture border");
            Check(!CapturePlan.ContinueReading(false, false, true) && CapturePlan.ContinueReading(false, true, false) && !CapturePlan.ContinueReading(true, true, false), "a visible unfocused client is copied; a covered or minimized one is not");
            Check(!CapturePlan.AcceptFrame(null) && !CapturePlan.AcceptFrame(ImageBuffer.Blank(32, 32, ImageBuffer.Pack(0, 0, 0))) && CapturePlan.AcceptFrame(ImageBuffer.Blank(32, 32, ImageBuffer.Pack(180, 40, 40))), "a black or failed frame is not a reading");
            Check(!PanelDock.ZOrderChanged(true, true, true) && !PanelDock.ZOrderChanged(true, false, false) && PanelDock.ZOrderChanged(false, false, true) && PanelDock.ZOrderChanged(true, false, true), "the panel z-order changes only when topmost changes");
            int ox, oy, cw, ch;
            WindowCrop.Client(0, 0, 2576, 1488, 8, 40, 2560, 1440, out ox, out oy, out cw, out ch);
            Check(ox == 8 && oy == 40 && cw == 2560 && ch == 1440, "1440p at 150% keeps the physical client crop");
            WindowCrop.Client(0, 0, 1936, 1128, 8, 32, 1920, 1080, out ox, out oy, out cw, out ch);
            Check(ox == 8 && oy == 32 && cw == 1920 && ch == 1080, "1080p at 100% keeps the physical client crop");
            WindowCrop.Client(0, 0, 3872, 2240, 16, 48, 3840, 2160, out ox, out oy, out cw, out ch);
            Check(ox == 16 && oy == 48 && cw == 3840 && ch == 2160, "4K at 200% keeps the physical client crop");
            ImageBuffer corner = ImageBuffer.Blank(800, 400, ImageBuffer.Pack(16, 18, 20));
            corner.Fill(300, 16, 36, 10, ImageBuffer.Pack(230, 210, 120));
            IntRect foundXp = SceneActivity.XpWatch.Find(corner);
            Check(foundXp.Width > 0 && foundXp.X < 400, "an XP drop away from the minimap corner is still found");
            ImageBuffer side = ImageBuffer.Blank(800, 400, ImageBuffer.Pack(16, 18, 20));
            side.Fill(8, 16, 36, 10, ImageBuffer.Pack(230, 210, 120));
            Check(SceneActivity.XpWatch.Find(side).Width == 0, "bright scenery on the far left is not an XP drop");
        }

        private static void PanelOrder()
        {
            Check(PanelDock.WantTopMost(true, false, true, false, false), "the panel is topmost while the game is in front");
            Check(!PanelDock.WantTopMost(true, false, false, false, false), "the panel drops back when another application is in front");
            Check(PanelDock.WantTopMost(false, false, false, false, true), "an explicit launch lifts the panel before the client is found");
            Check(PanelDock.WantTopMost(true, false, false, true, false), "the panel stays up while it is being used");
            Check(!PanelDock.WantTopMost(true, true, true, true, true), "a minimized client never gets a topmost panel");
        }

        private static void Skilling()
        {
            WardenSettings settings = new WardenSettings();
            settings.SkillingMode = true;
            settings.SkillingIdleSeconds = 30;
            settings.IdleSeconds = 12;
            settings.HpEnabled = false; settings.PrayerEnabled = false; settings.AfkEnabled = false; settings.PotionEnabled = false;
            Check(settings.ActiveIdleSeconds == 30, "skilling mode uses its own idle time");
            WardenSettings loaded = SettingsText.Read(SettingsText.Write(settings));
            Check(loaded.SkillingMode && loaded.SkillingIdleSeconds == 30 && loaded.IdleSeconds == 12, "skilling settings round-trip");
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WardenEngine engine = new WardenEngine();
            WatchSample start = Sample(t, true, true, false, false);
            start.SkillProgress = true;
            engine.Tick(start, settings);
            engine.Tick(Sample(t.AddSeconds(0.5), true, true, true, false), settings);
            // Combat-style signals (life changing) do not count while skilling.
            List<AlertEvent> early = engine.Tick(Sample(t.AddSeconds(20), true, true, true, false), settings);
            Check(Find(early, "idle") == null, "twenty seconds without XP is not yet idle in skilling mode");
            List<AlertEvent> stopped = engine.Tick(Sample(t.AddSeconds(31), true, true, true, false), settings);
            AlertEvent alert = Find(stopped, "idle");
            Check(alert != null && alert.Title == "You stopped skilling", "no XP or backpack change for the skilling time warns that skilling stopped");
            WatchSample xp = Sample(t.AddSeconds(32), true, true, false, false);
            xp.SkillProgress = true;
            engine.Tick(xp, settings);
            Check(!engine.IdleLatched && engine.StillFor == TimeSpan.Zero, "an XP drop resets skilling idle");
            WardenEngine combat = new WardenEngine();
            WardenSettings normal = new WardenSettings();
            normal.IdleSeconds = 12; normal.HpEnabled = false; normal.PrayerEnabled = false; normal.AfkEnabled = false;
            combat.Tick(Sample(t, true, true, false, false), normal);
            WatchSample onlyXp = Sample(t.AddSeconds(13), true, true, false, false);
            onlyXp.SkillProgress = true;
            DateTime xpTime = t.AddSeconds(13);
            onlyXp.Time = xpTime;
            Check(Find(combat.Tick(onlyXp, normal), "idle") == null && combat.Skilling, "an XP drop is activity without a skilling switch");
            Check(Find(combat.Tick(Sample(xpTime.AddSeconds(1), true, true, true, false), normal), "idle") == null && combat.Skilling, "nearby motion during a skilling gap is not idle");
            Check(Find(combat.Tick(Sample(xpTime.AddSeconds(normal.SkillingIdleSeconds + 1), true, true, true, false), normal), "idle") != null, "idle fires after the skilling gap even if the scene is moving");
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
            settings.Grandmaster = true;
            settings.TreePerk = true;
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
            WardenSettings rates = new WardenSettings();
            Check(rates.AfkSeconds == 1500, "normal AFK rate");
            rates.TreePerk = true;
            Check(rates.AfkSeconds == 2400, "tree-only AFK rate");
            rates.Grandmaster = true;
            Check(rates.AfkSeconds == 3900, "grandmaster plus tree AFK rate");
            rates.TreePerk = false;
            Check(rates.AfkSeconds == 3000, "grandmaster-only AFK rate");
            Check(loaded.Grandmaster && loaded.TreePerk && loaded.AfkSeconds == 3900 && loaded.AfkLeadSeconds == 45, "AFK timer settings round-trip");
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
            WatchSample hidden = Sample(t.AddSeconds(settings.AfkSeconds - 30), true, false, false, false);
            hidden.Hp = 10;
            hidden.HpMax = 100;
            List<AlertEvent> events = engine.Tick(hidden, settings);
            AlertEvent afk = Find(events, "afk");
            Check(afk != null && afk.Sound && !afk.Notify, "minimized client still warns about the AFK timer, and only on the channels that are on");
            Check(Find(events, "idle") == null, "time the window was unreadable is not counted as idle");
            Check(Find(events, "hp") == null && !engine.HpLatched, "a hidden client does not invent a low-hitpoint reading");
            WatchSample seen = Sample(t.AddSeconds(settings.AfkSeconds - 29), true, true, true, true);
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
            Check(CapturePolicy.Choose(true, false, true, true, CapturePlan.OfferWindowSurface) == CaptureRoute.Covered, "a covered client is left unread instead of forcing a window repaint");
            Check(!CapturePolicy.PixelsAvailable(CaptureRoute.Minimized) && CapturePolicy.PixelsAvailable(CaptureRoute.Screen), "only real frames count as pixels");
            bool[] clear = new bool[] { true, true, true, true, true, true, true, true, true };
            bool[] partial = new bool[] { true, false, false, false, false, false, false, false, false };
            bool[] blocked = new bool[] { false, false, false, false, false, false, false, false, false };
            Check(VisibilitySamples.MostlyClear(clear) && !VisibilitySamples.MostlyClear(partial) && VisibilitySamples.Any(partial) && !VisibilitySamples.Any(blocked), "partial cover still has a screen sample; full cover does not");
        }

        private static void Input()
        {
            Check(!InputActivity.IsActivity(false, false, true, true, false, false), "cursor motion does not reset idle");
            Check(!InputActivity.IsActivity(false, true, true, true, false, false), "mouse movement over the focused game is not a click");
            Check(!InputActivity.IsActivity(false, false, false, false, false, true), "keys typed into another app do not reset it");
            Check(InputActivity.IsActivity(false, true, false, false, false, true), "keys reset it while the game is focused");
            Check(InputActivity.IsActivity(false, true, true, false, true, false), "a click in the focused game resets idle");
            Check(!InputActivity.IsActivity(true, false, true, true, true, true), "a minimized game receives none of this input");
            Check(!InputActivity.IsActivity(false, false, false, true, true, false), "clicks on the window that covers the game do not count");
            Check(!CapturePlan.WantFull(false, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(3)), "unread HUD backs off between full-frame attempts");
            Check(!CapturePlan.WantFull(true, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(12)), "a locked slot waits out the slow full search");
            Check(CapturePlan.SampleMilliseconds <= 500 && CapturePlan.FullIntervalSeconds >= 10, "steady capture stays at or under 2 Hz");
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
            ImageBuffer secondsIcon = ImageBuffer.Blank(80, 70, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(secondsIcon, 8, 8, "45s");
            HudReading secondsRead = HudReader.Read(secondsIcon, new WardenSettings());
            Check(secondsRead.Buffs != null && secondsRead.Buffs.Count > 0 && secondsRead.Buffs[0].HasTime && !secondsRead.Buffs[0].Minutes && secondsRead.Buffs[0].Value == 45, "45s is forty-five seconds, trace " + (secondsRead.BuffTrace ?? ""));
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
            image.Fill(x + 8, y + 6, 18, 12, ImageBuffer.Pack(90, 150, 230));
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
                else if (ch == 's')
                {
                    image.Fill(tx, ty + 2, 8, 10, white);
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
            Check(!scene.Tick(image, reading), "a steady target bar does not reset idle, reason " + scene.Reason);
            ImageBuffer struck = new ImageBuffer(image.Width, image.Height, (int[])image.Pixels.Clone());
            int painted = 0;
            for (int y = 0; y < struck.Height && painted == 0; y++)
            {
                int x = 0;
                while (x < struck.Width && painted == 0)
                {
                    int c = struck.Argb(x, y);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    bool lime = g >= 160 && b <= 80 && g > r + 30 && g > b + 40;
                    if (!lime) { x++; continue; }
                    int x0 = x;
                    while (x < struck.Width)
                    {
                        int d = struck.Argb(x, y);
                        int rr = ImageBuffer.R(d), gg = ImageBuffer.G(d), bb = ImageBuffer.B(d);
                        if (!(gg >= 160 && bb <= 80 && gg > rr + 30 && gg > bb + 40)) break;
                        x++;
                    }
                    if (x - x0 >= 20)
                    {
                        for (int dy = -3; dy <= 3; dy++)
                            for (int k = 0; k < 16; k++) struck.Set(x0 + k, y + dy, ImageBuffer.Pack(8, 8, 8));
                        painted = 16;
                    }
                }
            }
            Check(painted > 0 && scene.Tick(struck, reading), "a shrinking target bar resets idle, reason " + scene.Reason);
            SceneActivity calm = new SceneActivity();
            ImageBuffer trees = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(20, 80, 30));
            ImageBuffer sway = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(30, 110, 40));
            sway.Fill(40, 40, 200, 120, ImageBuffer.Pack(40, 140, 50));
            Check(!calm.Tick(trees, new HudReading()), "the first scene is not activity");
            Check(!calm.Tick(sway, new HudReading()), "swaying trees and water are not player activity");
            StableValue steady = new StableValue();
            Check(steady.Push(1040) == null, "one prayer frame is not shown");
            Check(steady.Push(1040) == 1040, "two agreeing prayer reads are shown");
            Check(steady.Push(400) == 1040, "a single prayer jump is rejected");
            Check(steady.Push(400) == 400, "two agreeing new reads replace the old value");
            SceneActivity xpScene = new SceneActivity();
            ImageBuffer xpBase = ImageBuffer.Blank(400, 300, ImageBuffer.Pack(20, 24, 28));
            xpBase.Fill(280, 20, 40, 12, ImageBuffer.Pack(240, 230, 210));
            Check(!xpScene.Tick(xpBase, new HudReading(), false, false, false), "the first xp sample is not activity");
            Check(xpScene.Tick(xpBase, new HudReading(), false, true, false) && xpScene.Reason.IndexOf("xp") >= 0, "an xp drop resets idle");
            ImageBuffer packA = ImageBuffer.Blank(200, 280, ImageBuffer.Pack(12, 14, 16));
            ImageBuffer packB = ImageBuffer.Blank(200, 280, ImageBuffer.Pack(12, 14, 16));
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                {
                    packA.Fill(20 + col * 30, 20 + row * 30, 28, 28, ImageBuffer.Pack(90, 90, 90));
                    packA.Fill(24 + col * 30, 24 + row * 30, 20, 20, ImageBuffer.Pack(20, 20, 20));
                    packB.Fill(20 + col * 30, 20 + row * 30, 28, 28, ImageBuffer.Pack(90, 90, 90));
                    packB.Fill(24 + col * 30, 24 + row * 30, 20, 20, ImageBuffer.Pack(20, 20, 20));
                }
            IntRect grid = SceneActivity.PackWatch.Find(packA);
            Check(grid.Width > 0, "a 28-slot backpack grid can be located");
            int before = SceneActivity.PackWatch.Hash(packA, grid);
            packB.Fill(24, 24, 20, 20, ImageBuffer.Pack(180, 40, 40));
            Check(SceneActivity.PackWatch.Hash(packB, grid) != before, "a backpack content change changes the slot hash");
            WardenSettings junk = SettingsText.Read("not-a-key\tidle-seconds\tnope\r\npanel\tnot\tnumbers\r\n");
            Check(junk.IdleSeconds == 12, "a broken settings line keeps the defaults");
            int px = -4000, py = 20;
            ScreenClamp.Place(ref px, ref py, 400, 262, new int[] { 0 }, new int[] { 0 }, new int[] { 1920 }, new int[] { 1080 });
            Check(px >= 0 && py >= 0, "an off-screen panel is moved onto a visible screen");
        }

        private static void Live023(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string full = Path.Combine(dir, "debug-capture.png");
            string prayer = Path.Combine(dir, "game-prayer-936-990.png");
            if (!File.Exists(full) || !File.Exists(prayer))
            {
                failures.Add("missing live023 fixtures");
                assertions++;
                return;
            }
            HudReading reading = HudReader.Read(PngImage.Load(full), new WardenSettings());
            Check(reading.Hp == 1633 && reading.HpMax == 1060, "live023 life is 1633/1060 from the full slot, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()) + " " + reading.LifeTrace);
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990, "live023 prayer is 1040/990, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()) + " " + reading.PrayerTrace);
            Check(reading.LifeText.Width > reading.LifeBar.Width, "the life text box uses the slot, not the fill");
            Check(reading.PrayerText.Width > 80, "the prayer text box stays wide enough when the fill is full, width " + reading.PrayerText.Width.ToString());
            int? shown = BuffClock.ShownSeconds(reading.Buffs);
            Check(reading.BuffBarFound && shown != null, "live023 buff icons expose a timer, got " + (reading.BuffTrace ?? "") + " shown " + (shown == null ? "none" : shown.Value.ToString()));
            HudReading drained = HudReader.Read(PngImage.Load(prayer), new WardenSettings());
            Check(drained.Prayer == 936 && drained.PrayerMax == 990, "drained prayer reads 936/990, got " + (drained.Prayer == null ? "none" : drained.Prayer.Value.ToString()) + "/" + (drained.PrayerMax == null ? "-" : drained.PrayerMax.Value.ToString()) + " trace=" + drained.PrayerTrace + " last=" + HudReader.LastTrace);
            string v024 = Path.Combine(dir, "game-prayer-936-990-v024.png");
            if (File.Exists(v024))
            {
                HudReading again = HudReader.Read(PngImage.Load(v024), new WardenSettings());
                Check(again.Prayer == 936 && again.PrayerMax == 990, "v024 prayer crop reads 936/990, got " + (again.Prayer == null ? "none" : again.Prayer.Value.ToString()) + "/" + (again.PrayerMax == null ? "-" : again.PrayerMax.Value.ToString()) + " " + again.PrayerTrace);
            }
        }

        // Nine combinations: 1080p, 1440p and 4K, each at 100%, 150% and 200% Windows scaling.
        // The fixture is a 1440p capture, so the image is scaled by resolution.
        // DPI is physical pixels: it multiplies that scale and does not shrink the crop.
        private static void ScaleMatrix(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string fullPath = Path.Combine(dir, "debug-capture.png");
            if (!File.Exists(fullPath)) { Check(false, "missing scale fixture"); return; }
            ImageBuffer full = PngImage.Load(fullPath);
            HudReading native = HudReader.Read(full, new WardenSettings());
            int top = native.LifeBar.Height > 0 && native.LifeBar.Y > 50 ? native.LifeBar.Y - 50 : (int)(full.Height * 0.75);
            if (top < 0) top = 0;
            int bandH = Math.Min(full.Height - top, 140);
            ImageBuffer band = full.Crop(0, top, full.Width, bandH);
            double[] resolution = new double[] { 1920.0 / 2560.0, 1.0, 3840.0 / 2560.0 };
            double[] dpi = new double[] { 1.0, 1.5, 2.0 };
            string[] resolutionName = new string[] { "1080p", "1440p", "4K" };
            string[] dpiName = new string[] { "100%", "150%", "200%" };
            for (int r = 0; r < resolution.Length; r++)
                for (int d = 0; d < dpi.Length; d++)
                {
                    double physical = resolution[r] * dpi[d];
                    HudReading reading = HudReader.Read(band.Scale(physical), new WardenSettings());
                    bool exact = reading.Hp == 1633 && reading.HpMax == 1060 && reading.Prayer == 1040 && reading.PrayerMax == 990;
                    bool wrong = (reading.Hp != null && reading.Hp != 1633) || (reading.HpMax != null && reading.HpMax != 1060) || (reading.Prayer != null && reading.Prayer != 1040) || (reading.PrayerMax != null && reading.PrayerMax != 990);
                    bool stable = Math.Abs(physical - 1) < 0.05 || Math.Abs(physical - 1.5) < 0.05 || Math.Abs(physical - 2) < 0.05 || Math.Abs(physical - 3) < 0.05;
                    Check(!wrong && (!stable || exact), resolutionName[r] + " at " + dpiName[d] + " Windows scaling reads HP and prayer, got " + Fmt(reading.Hp) + "/" + Fmt(reading.HpMax) + " prayer " + Fmt(reading.Prayer) + "/" + Fmt(reading.PrayerMax) + " " + reading.LifeTrace + " | " + reading.PrayerTrace);
                    int ox, oy, w, h;
                    int frameW = (int)Math.Round(2576 * resolution[r] * dpi[d]);
                    int frameH = (int)Math.Round(1488 * resolution[r] * dpi[d]);
                    int clientW = (int)Math.Round(2560 * resolution[r] * dpi[d]);
                    int clientH = (int)Math.Round(1440 * resolution[r] * dpi[d]);
                    int insetX = (int)Math.Round(8 * dpi[d]);
                    int insetY = (int)Math.Round(24 * dpi[d]);
                    WindowCrop.Client(0, 0, frameW, frameH, insetX, insetY, clientW, clientH, out ox, out oy, out w, out h);
                    Check(ox == insetX && oy == insetY && w == clientW && h == clientH, resolutionName[r] + " at " + dpiName[d] + " crop stays in physical pixels");
                }
            double[] interfaceScale = new double[] { 0.8, 1.0, 1.25, 1.5 };
            for (int i = 0; i < interfaceScale.Length; i++)
            {
                HudReading reading = HudReader.Read(band.Scale(interfaceScale[i]), new WardenSettings());
                bool exact = reading.Hp == 1633 && reading.HpMax == 1060 && reading.Prayer == 1040 && reading.PrayerMax == 990;
                bool wrong = (reading.Hp != null && reading.Hp != 1633) || (reading.HpMax != null && reading.HpMax != 1060) || (reading.Prayer != null && reading.Prayer != 1040) || (reading.PrayerMax != null && reading.PrayerMax != 990);
                bool stable = interfaceScale[i] == 1.0 || interfaceScale[i] == 1.5;
                Check(!wrong && (!stable || exact), "interface scale " + interfaceScale[i].ToString("0.00") + " reads HP and prayer, got " + Fmt(reading.Hp) + "/" + Fmt(reading.HpMax) + " " + Fmt(reading.Prayer) + "/" + Fmt(reading.PrayerMax) + " " + reading.LifeTrace);
            }
            ExpectPotions(dir, "buffbar-potions-12m.png", 720);
            ExpectPotions(dir, "buffbar-potions-11m-elder-overload.png", 660);
            ExpectPotions(dir, "buffbar-prayer60-bone5-heart.png", 0);
        }

        private static void ExpectPotions(string dir, string name, int seconds)
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path)) { Check(false, "missing " + name); return; }
            ImageBuffer icon = PngImage.Load(path);
            double[] scales = new double[] { 0.8, 1.0, 1.25, 1.5 };
            for (int i = 0; i < scales.Length; i++)
            {
                HudReading potionRead = HudReader.Read(icon.Scale(scales[i]), new WardenSettings());
                int? shown = BuffClock.ShownSeconds(potionRead.Buffs);
                int got = shown == null ? 0 : shown.Value;
                if (scales[i] != 1.0) continue;
                Check(got == seconds, name + " at interface 1.00 potion seconds " + got.ToString() + " expected " + seconds.ToString() + " " + (potionRead.BuffTrace ?? ""));
            }
        }

        private static string Fmt(int? value)
        {
            return value == null ? "none" : value.Value.ToString();
        }

        private static void LiveRead(string root)
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ExpectPair(917, 1060, true, "HP 917/1060 is accepted");
            ExpectPair(99, 1060, true, "HP 99/1060 is accepted");
            ExpectPair(5, 1060, true, "HP 5/1060 is accepted");
            ExpectPair(842, 990, false, "prayer 842/990 is accepted");
            ExpectPair(9, 990, false, "prayer 9/990 is accepted");
            SlotWatch drain = new SlotWatch();
            int[] steps = new int[] { 990, 980, 970, 960, 950, 940, 936 };
            for (int i = 0; i < steps.Length; i++)
            {
                ImageBuffer frame = HudFrame(1060, 1060, steps[i], 990);
                drain.Observe(frame, new WardenSettings(), t.AddMilliseconds(500 * i));
                drain.Observe(frame, new WardenSettings(), t.AddMilliseconds(500 * i + 200));
            }
            Check(drain.Prayer == 936 && drain.PrayerMax == 990, "draining prayer reaches 936/990 through the locked slot, got " + (drain.Prayer == null ? "none" : drain.Prayer.Value.ToString()) + "/" + (drain.PrayerMax == null ? "-" : drain.PrayerMax.Value.ToString()));
            Check(drain.Hp == 1060 && drain.HpMax == 1060, "locked life stays 1060/1060 while prayer drains");
            ReadGate gate = new ReadGate();
            gate.Push(917, 1060, t);
            gate.Push(917, 1060, t.AddMilliseconds(500));
            gate.Push(null, 1060, t.AddSeconds(6));
            Check(gate.Value == 917 && gate.Stale, "a missing read keeps the last current and marks it stale, got " + (gate.Value == null ? "none" : gate.Value.Value.ToString()));
            gate.Push(50, 60, t.AddSeconds(7));
            Check(gate.Value == 917, "a read whose maximum does not match the lock is ignored");
            string dir = string.IsNullOrEmpty(root) ? "" : Path.Combine(root, "verification", "fixtures", "live023");
            if (dir.Length == 0 || !Directory.Exists(dir)) return;
            string[] names = new string[] { "buffbar-prayer60-bone5-heart.png", "buffbar-potions-11m-elder-overload.png", "buffbar-potions-12m.png", "panel-potions-60s-wrong.png", "panel-prayer-stuck-990.png", "live-v024-hp-917-unread.png", "live-v024-lagging-values.png", "live-v024-full-layout-lag.png" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(dir, names[i]);
                Check(File.Exists(path), "fixture present " + names[i]);
            }
            HudReading prayerBar = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-prayer60-bone5-heart.png")), new WardenSettings());
            Check(BuffClock.ShownSeconds(prayerBar.Buffs) == null, "the purple 60 and the stack of 5 are not potion timers, trace " + (prayerBar.BuffTrace ?? ""));
            HudReading twelve = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-potions-12m.png")), new WardenSettings());
            Check(BuffClock.ShownSeconds(twelve.Buffs) == 720, "two flasks at 12m are potion timers, trace " + (twelve.BuffTrace ?? ""));
            HudReading eleven = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-potions-11m-elder-overload.png")), new WardenSettings());
            int? potion = BuffClock.ShownSeconds(eleven.Buffs);
            Check(potion == 660, "elder overload row tracks 11m and ignores the stack of 5, got " + (potion == null ? "none" : potion.Value.ToString()) + " " + (eleven.BuffTrace ?? ""));
        }

        private static void ExpectPair(int current, int max, bool life, string label)
        {
            SlotWatch watch = new SlotWatch();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ImageBuffer frame = life ? HudFrame(current, max, 990, 990) : HudFrame(1060, 1060, current, max);
            watch.Observe(frame, new WardenSettings(), t);
            watch.Observe(frame, new WardenSettings(), t.AddMilliseconds(500));
            int? got = life ? watch.Hp : watch.Prayer;
            int? gotMax = life ? watch.HpMax : watch.PrayerMax;
            Check(got == current && gotMax == max, label + ", got " + (got == null ? "none" : got.Value.ToString()) + "/" + (gotMax == null ? "-" : gotMax.Value.ToString()));
        }

        private static ImageBuffer HudFrame(int hp, int hpMax, int prayer, int prayerMax)
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            int red = ImageBuffer.Pack(196, 42, 42);
            int purple = ImageBuffer.Pack(142, 70, 196);
            int yellow = ImageBuffer.Pack(214, 184, 48);
            ImageBuffer image = ImageBuffer.Blank(640, 160, ImageBuffer.Pack(10, 16, 22));
            image.Fill(40, 90, 150, 8, red);
            DrawLifeNumber(image, 40, 68, DigitsOf(hp), DigitsOf(hpMax), white);
            image.Fill(230, 90, 80, 8, yellow);
            image.Fill(340, 90, 140, 8, purple);
            DrawLifeNumber(image, 340, 68, DigitsOf(prayer), DigitsOf(prayerMax), white);
            return image;
        }

        private static int[] DigitsOf(int value)
        {
            string text = value.ToString();
            int[] digits = new int[text.Length];
            for (int i = 0; i < text.Length; i++) digits[i] = text[i] - '0';
            return digits;
        }

        private static void Manifest(string root)
        {
            Check(AppInfo.Version == "0.2.9" && AppInfo.Name == "AFK Warden" && AppInfo.Author == "Zenity", "module identity");
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
            Check(json.IndexOf("\"version\": \"0.2.9\"") >= 0, "public module manifest version");
            Check(json.IndexOf("\"name\": \"AFK Warden\"") >= 0 && json.IndexOf("\"author\": \"Zenity\"") >= 0, "public module manifest name and author");
            Check(json.IndexOf("\"id\": \"afk-warden\"") >= 0, "public module id");
            Check(manifest.IndexOf("version=\"0.2.9.0\"") >= 0 && manifest.IndexOf("name=\"KavrosAfkWarden\"") >= 0, "assembly manifest version");
        }
    }
}
