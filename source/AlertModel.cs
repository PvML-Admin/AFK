using System;
using System.Collections.Generic;

namespace KavrosAfkWarden
{
    public sealed class WardenSettings
    {
        public bool IdleEnabled = true;
        public int IdleSeconds = 12;
        public bool HpEnabled = true;
        public int HpThreshold = 30;
        public bool HpPercent = true;
        public bool PrayerEnabled = true;
        public int PrayerThreshold = 20;
        public bool PrayerPercent = true;
        public bool AfkEnabled = true;
        public int AfkSeconds = 300;
        public int AfkLeadSeconds = 30;
        public bool SoundMaster = true;
        public bool NotifyMaster = true;
        public bool IdleSound = true;
        public bool IdleNotify = true;
        public bool HpSound = true;
        public bool HpNotify = true;
        public bool PrayerSound = true;
        public bool PrayerNotify = true;
        public bool AfkSound = true;
        public bool AfkNotify = true;
        public bool PotionEnabled = true;
        public int PotionSeconds = 15;
        public bool PotionSound = true;
        public bool PotionNotify = true;
        public int Volume = 100;
        public int CooldownSeconds = 20;
        public int PanelOffsetX = 12;
        public int PanelOffsetY = 72;
        public string ClientTitle = "";
        public int BannerSeconds = 8;
        public bool Paused;
        public bool CheckForUpdates = true;
        public bool SaveDebug;
        public bool PanelTopMost = true;
        public int PanelX = int.MinValue;
        public int PanelY = int.MinValue;
        public double HpPinX = -1, HpPinY, HpPinW, HpPinH;
        public double PrayerPinX = -1, PrayerPinY, PrayerPinW, PrayerPinH;
        public double BuffPinX = -1, BuffPinY, BuffPinW, BuffPinH;

        public bool HasHpPin { get { return HpPinW > 0.005 && HpPinH > 0.005; } }
        public bool HasPrayerPin { get { return PrayerPinW > 0.005 && PrayerPinH > 0.005; } }
        public bool HasBuffPin { get { return BuffPinW > 0.005 && BuffPinH > 0.005; } }

        public void Clamp()
        {
            IdleSeconds = ClampInt(IdleSeconds, 3, 600);
            HpThreshold = ClampInt(HpThreshold, 1, HpPercent ? 999 : 9999999);
            PrayerThreshold = ClampInt(PrayerThreshold, 1, PrayerPercent ? 999 : 9999999);
            AfkSeconds = ClampInt(AfkSeconds, 60, 1800);
            AfkLeadSeconds = ClampInt(AfkLeadSeconds, 5, AfkSeconds - 5);
            PotionSeconds = ClampInt(PotionSeconds, 1, 600);
            Volume = ClampInt(Volume, 0, 100);
            CooldownSeconds = ClampInt(CooldownSeconds, 0, 600);
            BannerSeconds = ClampInt(BannerSeconds, 2, 120);
        }

        public bool AllowsSound(string kind)
        {
            if (!SoundMaster) return false;
            if (kind == "idle") return IdleSound;
            if (kind == "hp") return HpSound;
            if (kind == "prayer") return PrayerSound;
            if (kind == "afk") return AfkSound;
            if (kind == "potion") return PotionSound;
            return false;
        }

        public bool AllowsNotify(string kind)
        {
            if (!NotifyMaster) return false;
            if (kind == "idle") return IdleNotify;
            if (kind == "hp") return HpNotify;
            if (kind == "prayer") return PrayerNotify;
            if (kind == "afk") return AfkNotify;
            if (kind == "potion") return PotionNotify;
            return false;
        }

        // A percent threshold needs a maximum, unless the orb already is a percent.
        // An absolute threshold cannot be applied to an orb fill.
        public static bool Comparable(bool percentMode, bool fromOrb, bool hasMax)
        {
            if (fromOrb) return percentMode;
            if (percentMode) return hasMax;
            return true;
        }

        private static int ClampInt(int value, int lo, int hi)
        {
            if (value < lo) return lo;
            if (value > hi) return hi;
            return value;
        }
    }

    public sealed class AlertGate
    {
        private bool armed = true;
        private DateTime lastFired = DateTime.MinValue;

        public bool Armed { get { return armed; } }

        // Fires once while the condition holds. After it clears, it can fire
        // again only once the cooldown since the last fire has also elapsed.
        public bool ShouldFire(bool condition, DateTime now, TimeSpan cooldown)
        {
            if (!condition)
            {
                armed = true;
                return false;
            }
            if (!armed) return false;
            if (lastFired != DateTime.MinValue && now - lastFired < cooldown) return false;
            armed = false;
            lastFired = now;
            return true;
        }
    }

    public static class Thresholds
    {
        public static bool TryMeasure(int current, int? max, bool percentMode, out int value)
        {
            if (!percentMode)
            {
                value = current;
                return current > 0;
            }
            if (max == null || max.Value <= 0)
            {
                value = 0;
                return false;
            }
            value = (int)Math.Round(100.0 * current / max.Value);
            return true;
        }

        public static bool IsLow(int current, int? max, bool percentMode, int threshold)
        {
            int value;
            if (!TryMeasure(current, max, percentMode, out value)) return false;
            return value < threshold;
        }

        public static bool IsRecovered(int current, int? max, bool percentMode, int threshold)
        {
            int value;
            if (!TryMeasure(current, max, percentMode, out value)) return false;
            int margin = percentMode ? 3 : Math.Max(10, threshold / 20);
            return value >= threshold + margin;
        }
    }

    public sealed class LevelWatch
    {
        private readonly AlertGate gate = new AlertGate();
        private bool have;
        public bool Low;

        public bool Tick(int? current, int? max, bool percentMode, int threshold, bool enabled, DateTime now, TimeSpan cooldown)
        {
            if (!enabled)
            {
                have = false;
                Low = false;
                gate.ShouldFire(false, now, cooldown);
                return false;
            }
            // A missing frame holds the last condition. It is not a recovery.
            if (current == null) return false;
            if (!have)
            {
                Low = Thresholds.IsLow(current.Value, max, percentMode, threshold);
                have = true;
            }
            else if (Low)
            {
                if (Thresholds.IsRecovered(current.Value, max, percentMode, threshold)) Low = false;
            }
            else if (Thresholds.IsLow(current.Value, max, percentMode, threshold)) Low = true;
            return gate.ShouldFire(Low, now, cooldown);
        }
    }

    // Same gate as hitpoints: fire once while any buff timer stays under the
    // threshold, and do not treat a missed frame as the buff having recovered.
    public sealed class PotionWatch
    {
        private readonly AlertGate gate = new AlertGate();
        private bool have;
        public bool Low;

        public bool Tick(int? seconds, bool clear, bool enabled, int threshold, DateTime now, TimeSpan cooldown)
        {
            if (!enabled)
            {
                have = false;
                Low = false;
                gate.ShouldFire(false, now, cooldown);
                return false;
            }
            if (clear)
            {
                Low = false;
                have = true;
                return gate.ShouldFire(false, now, cooldown);
            }
            if (seconds == null) return false;
            if (!have)
            {
                Low = seconds.Value < threshold;
                have = true;
            }
            else if (Low)
            {
                if (seconds.Value >= threshold + 3) Low = false;
            }
            else if (seconds.Value < threshold) Low = true;
            return gate.ShouldFire(Low, now, cooldown);
        }
    }

    public sealed class StillnessClock
    {
        private DateTime? since;
        private DateTime hold;
        private bool started;
        private TimeSpan frozen;

        public TimeSpan StillFor(bool moving, DateTime now, bool running)
        {
            if (!started)
            {
                started = true;
                hold = now;
            }
            if (!running)
            {
                // Drop the gap so a minimized or covered client is not treated as idle time.
                if (since != null) since = since.Value + (now - hold);
                hold = now;
                return frozen;
            }
            hold = now;
            if (moving)
            {
                since = null;
                frozen = TimeSpan.Zero;
                return TimeSpan.Zero;
            }
            if (since == null) since = now;
            TimeSpan span = now - since.Value;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            frozen = span;
            return span;
        }
    }

    public sealed class InactivityClock
    {
        private DateTime mark;
        private DateTime frozenAt;
        private bool started;

        public void Observe(bool activity, DateTime now, bool running)
        {
            if (!started)
            {
                started = true;
                mark = now;
                frozenAt = now;
                return;
            }
            if (!running)
            {
                mark = mark + (now - frozenAt);
                frozenAt = now;
                return;
            }
            frozenAt = now;
            if (activity) mark = now;
        }

        public TimeSpan Remaining(DateTime now, TimeSpan duration)
        {
            if (!started) return duration;
            TimeSpan elapsed = now - mark;
            if (elapsed < TimeSpan.Zero || elapsed >= duration) return elapsed >= duration ? TimeSpan.Zero : duration;
            return duration - elapsed;
        }
    }

    public sealed class StableValue
    {
        private int value;
        private bool have;
        private int hits;

        public int? Push(int? next)
        {
            if (next == null)
            {
                hits = 0;
                have = false;
                return null;
            }
            if (have && Math.Abs(next.Value - value) <= Math.Max(8, value / 5)) hits++;
            else hits = 1;
            have = true;
            value = next.Value;
            return hits >= 2 ? next : (int?)null;
        }
    }

    public struct BuffIcon
    {
        public int X;
        public bool HasTime;
        public bool Minutes;
        public int Value;
    }

    // A plain number on a buff icon can be a stack count. It becomes a potion
    // timer only after that icon has shown minutes, or after the number counts down.
    public sealed class BuffClock
    {
        private struct Slot
        {
            public int X;
            public bool SawMinutes;
            public bool Fell;
            public bool Have;
            public int Last;
            public bool Seen;
        }

        private readonly List<Slot> slots = new List<Slot>();

        public int? Tick(List<BuffIcon> icons)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                slot.Seen = false;
                slots[i] = slot;
            }
            int best = -1;
            if (icons != null)
            {
                for (int i = 0; i < icons.Count; i++)
                {
                    BuffIcon icon = icons[i];
                    if (!icon.HasTime) continue;
                    int index = -1;
                    for (int s = 0; s < slots.Count; s++)
                        if (Math.Abs(slots[s].X - icon.X) <= 18) { index = s; break; }
                    Slot slot = index < 0 ? new Slot() : slots[index];
                    slot.X = icon.X;
                    slot.Seen = true;
                    int seconds;
                    if (icon.Minutes)
                    {
                        slot.SawMinutes = true;
                        seconds = icon.Value * 60;
                    }
                    else
                    {
                        if (slot.Have && icon.Value < slot.Last) slot.Fell = true;
                        slot.Last = icon.Value;
                        slot.Have = true;
                        seconds = icon.Value;
                        if (!slot.SawMinutes && !slot.Fell) seconds = -1;
                    }
                    if (index < 0) slots.Add(slot);
                    else slots[index] = slot;
                    if (seconds >= 0 && (best < 0 || seconds < best)) best = seconds;
                }
            }
            for (int i = slots.Count - 1; i >= 0; i--) if (!slots[i].Seen) slots.RemoveAt(i);
            return best < 0 ? (int?)null : best;
        }
    }

    public sealed class WatchSample
    {
        public DateTime Time;
        public bool GameAttached;
        public bool PixelsFresh;
        public bool CharacterMoving;
        public bool InputActivity;
        public int? Hp;
        public int? HpMax;
        public int? Prayer;
        public int? PrayerMax;
        public int? PotionSeconds;
        // True when the buff bar was scanned and no timer was there. A null
        // PotionSeconds without this flag is a missed frame, not a recovery.
        public bool PotionClear;
    }

    public sealed class AlertEvent
    {
        public string Kind;
        public string Title;
        public string Detail;
        public bool Sound;
        public bool Notify;
    }

    public sealed class WardenEngine
    {
        private readonly StillnessClock stillness = new StillnessClock();
        private readonly InactivityClock inactivity = new InactivityClock();
        private readonly AlertGate idleGate = new AlertGate();
        private readonly AlertGate afkGate = new AlertGate();
        private readonly LevelWatch hp = new LevelWatch();
        private readonly LevelWatch prayer = new LevelWatch();
        private readonly PotionWatch potion = new PotionWatch();

        public TimeSpan StillFor;
        public TimeSpan AfkRemaining = TimeSpan.FromMinutes(5);
        public bool IdleLatched;
        public bool HpLatched;
        public bool PrayerLatched;
        public bool AfkLatched;
        public bool PotionLatched;

        public List<AlertEvent> Tick(WatchSample sample, WardenSettings settings)
        {
            settings.Clamp();
            List<AlertEvent> events = new List<AlertEvent>();
            bool attached = sample.GameAttached && !settings.Paused;
            // Idle, hitpoints and prayer need a picture of the client.
            // The inactivity timer does not: it keeps running while you are in another app,
            // including while the client is minimized or covered and cannot be read.
            bool see = attached && sample.PixelsFresh;
            StillFor = stillness.StillFor(see && sample.CharacterMoving, sample.Time, see);
            inactivity.Observe(sample.InputActivity, sample.Time, attached);
            AfkRemaining = inactivity.Remaining(sample.Time, TimeSpan.FromSeconds(settings.AfkSeconds));
            if (!attached)
            {
                IdleLatched = false;
                HpLatched = false;
                PrayerLatched = false;
                AfkLatched = false;
                idleGate.ShouldFire(false, sample.Time, TimeSpan.Zero);
                afkGate.ShouldFire(false, sample.Time, TimeSpan.Zero);
                hp.Tick(null, null, settings.HpPercent, settings.HpThreshold, false, sample.Time, TimeSpan.Zero);
                prayer.Tick(null, null, settings.PrayerPercent, settings.PrayerThreshold, false, sample.Time, TimeSpan.Zero);
                potion.Tick(null, true, false, settings.PotionSeconds, sample.Time, TimeSpan.Zero);
                return events;
            }

            TimeSpan cooldown = TimeSpan.FromSeconds(settings.CooldownSeconds);
            bool idle = see && settings.IdleEnabled && StillFor >= TimeSpan.FromSeconds(settings.IdleSeconds);
            IdleLatched = idle;
            if (idleGate.ShouldFire(idle, sample.Time, cooldown))
                events.Add(Make(settings, "idle", "You have gone idle", "No animation or movement for " + (int)StillFor.TotalSeconds + "s."));

            int? hpNow = see ? sample.Hp : (int?)null;
            if (hp.Tick(hpNow, sample.HpMax, settings.HpPercent, settings.HpThreshold, settings.HpEnabled, sample.Time, cooldown))
                events.Add(Make(settings, "hp", "Hitpoints are low", FormatLevel("Hitpoints", sample.Hp, sample.HpMax, settings.HpPercent)));
            HpLatched = hp.Low;

            int? prayerNow = see ? sample.Prayer : (int?)null;
            if (prayer.Tick(prayerNow, sample.PrayerMax, settings.PrayerPercent, settings.PrayerThreshold, settings.PrayerEnabled, sample.Time, cooldown))
                events.Add(Make(settings, "prayer", "Prayer is low", FormatLevel("Prayer", sample.Prayer, sample.PrayerMax, settings.PrayerPercent)));
            PrayerLatched = prayer.Low;

            int? potionNow = see ? sample.PotionSeconds : (int?)null;
            bool potionClear = see && sample.PotionClear;
            if (potion.Tick(potionNow, potionClear, settings.PotionEnabled, settings.PotionSeconds, sample.Time, cooldown))
                events.Add(Make(settings, "potion", "A potion is running out", "About " + (potionNow == null ? 0 : potionNow.Value).ToString() + "s left on a buff timer."));
            PotionLatched = potion.Low;

            bool afkDue = settings.AfkEnabled && AfkRemaining <= TimeSpan.FromSeconds(settings.AfkLeadSeconds);
            AfkLatched = afkDue;
            if (afkGate.ShouldFire(afkDue, sample.Time, cooldown))
                events.Add(Make(settings, "afk", "AFK teleport soon", "About " + Math.Max(0, (int)AfkRemaining.TotalSeconds) + "s left on the inactivity timer."));
            return events;
        }

        private static AlertEvent Make(WardenSettings settings, string kind, string title, string detail)
        {
            AlertEvent ev = new AlertEvent();
            ev.Kind = kind;
            ev.Title = title;
            ev.Detail = detail;
            ev.Sound = settings.AllowsSound(kind);
            ev.Notify = settings.AllowsNotify(kind);
            return ev;
        }

        public static string FormatLevel(string name, int? current, int? max, bool percent)
        {
            if (current == null) return name + " unread";
            if (max != null && max.Value > 0)
            {
                int pct = (int)Math.Round(100.0 * current.Value / max.Value);
                return current.Value.ToString() + " / " + max.Value.ToString() + " (" + pct.ToString() + "%)";
            }
            return percent ? current.Value.ToString() + " (maximum unread)" : current.Value.ToString();
        }
    }

    public static class InputActivity
    {
        public static bool IsActivity(bool iconic, bool foreground, bool cursorOverGame, bool cursorMoved, bool buttonDown, bool keyDown)
        {
            if (iconic) return false;
            if (cursorOverGame && (cursorMoved || buttonDown)) return true;
            if (foreground && keyDown) return true;
            return false;
        }
    }

    public static class AlertPlacement
    {
        public static IntRect Place(int workLeft, int workTop, int workWidth, int workHeight, int width, int height, int index)
        {
            int x = workLeft + workWidth - width - 16;
            int y = workTop + workHeight - height - 16 - index * (height + 8);
            if (x < workLeft) x = workLeft;
            if (y < workTop) y = workTop;
            return new IntRect(x, y, width, height);
        }
    }
}
