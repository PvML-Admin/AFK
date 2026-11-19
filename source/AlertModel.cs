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
        // Skilling mode: idle means no XP drop, no backpack change and no
        // click or key for SkillingIdleSeconds. Combat stats do not count.
        public bool SkillingMode;
        public int SkillingIdleSeconds = 45;
        public int ActiveIdleSeconds { get { return SkillingMode ? SkillingIdleSeconds : IdleSeconds; } }
        public bool Grandmaster;
        public bool TreePerk;
        public int AfkSeconds { get { return ((Grandmaster ? 50 : 25) + (TreePerk ? 15 : 0)) * 60; } }
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

        public WardenSettings Snapshot() { return (WardenSettings)MemberwiseClone(); }
        public bool SamePins(WardenSettings other)
        {
            return other!=null && HpPinX==other.HpPinX && HpPinY==other.HpPinY && HpPinW==other.HpPinW && HpPinH==other.HpPinH &&
                PrayerPinX==other.PrayerPinX && PrayerPinY==other.PrayerPinY && PrayerPinW==other.PrayerPinW && PrayerPinH==other.PrayerPinH &&
                BuffPinX==other.BuffPinX && BuffPinY==other.BuffPinY && BuffPinW==other.BuffPinW && BuffPinH==other.BuffPinH;
        }

        public void Clamp()
        {
            IdleSeconds = ClampInt(IdleSeconds, 3, 600);
            SkillingIdleSeconds = ClampInt(SkillingIdleSeconds, 3, 600);
            HpThreshold = ClampInt(HpThreshold, 1, HpPercent ? 999 : 9999999);
            PrayerThreshold = ClampInt(PrayerThreshold, 1, PrayerPercent ? 999 : 9999999);
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
                return current >= 0;
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
        private readonly int[] recent = new int[3];
        private int count;
        private int shown;
        private bool have;

        // Two of the last three accepted reads. A single different value does not move the panel.
        public int? Push(int? next)
        {
            if (next == null) return have ? (int?)shown : null;
            recent[0] = recent[1];
            recent[1] = recent[2];
            recent[2] = next.Value;
            if (count < 3) count++;
            int agreed;
            if (Agree(out agreed))
            {
                shown = agreed;
                have = true;
            }
            return have ? (int?)shown : null;
        }

        private bool Agree(out int value)
        {
            value = 0;
            if (count < 2) return false;
            int start = 3 - count;
            for (int i = start; i < 3; i++)
            {
                int hits = 0;
                for (int j = start; j < 3; j++) if (recent[j] == recent[i]) hits++;
                if (hits >= 2) { value = recent[i]; return true; }
            }
            return false;
        }
    }

    // A HUD number is shown only when the slash was read and the maximum matches
    // the maximum already locked from earlier reads. The maximum is never copied
    // into the current value.
    public sealed class ReadGate
    {
        // Boosted life and prayer can sit above the cap (the live capture shows
        // 1,633/1,060), but never at double it. Anything beyond is a misread.
        public static bool Plausible(int current, int max)
        {
            return max >= 1 && max <= 10000000 && current >= 0 && current <= max * 2;
        }

        private StableValue current = new StableValue();
        private readonly StableValue maximum = new StableValue();
        private int? lockedMax;
        private DateTime lastGood;
        private bool saw;
        private int? candidateMax, previous;
        private int maxCount, previousDirection;
        public int? Value;
        public int? Max;
        public bool Stale;

        public void Push(int? nowCurrent, int? nowMax, DateTime now)
        {
            bool valid = nowCurrent != null && nowMax != null && Plausible(nowCurrent.Value, nowMax.Value);
            if (valid && lockedMax.HasValue && nowMax != lockedMax)
            {
                maxCount = candidateMax==nowMax ? maxCount+1 : 1;
                candidateMax=nowMax;
                if(maxCount>=3)
                {
                    lockedMax=nowMax; current=new StableValue(); previous=null; previousDirection=0;
                    maxCount=0;
                }
                else valid=false;
            }
            else { maxCount=0; candidateMax=null; }
            if (!valid)
            {
                Stale = saw && (now - lastGood).TotalSeconds > 5;
                return;
            }
            lastGood=now; saw=true; Stale=false;
            int? maxShown = maximum.Push(nowMax);
            if (!lockedMax.HasValue && maxShown.HasValue) lockedMax=maxShown;
            int value=nowCurrent.Value;
            int delta=previous.HasValue?value-previous.Value:0;
            int direction=Math.Sign(delta);
            int? agreed=current.Push(value);
            // A draining bar can change on every frame. Three plausible readings
            // travelling in one direction corroborate each other at the same cap.
            bool trend=lockedMax==nowMax && direction!=0 && direction==previousDirection && Math.Abs(delta)<=Math.Max(5,nowMax.Value/8);
            if(trend || (previous==value && lockedMax==nowMax)) Value=value;
            // Two of the last three reads agree on the newest value (A, B, A):
            // one odd frame in between does not hold the panel back.
            else if(agreed==value && lockedMax==nowMax) Value=value;
            else if(!Value.HasValue && agreed.HasValue) Value=agreed;
            previousDirection=Math.Abs(delta)<=Math.Max(5,nowMax.Value/8)?direction:0;
            previous=value;
            Max = lockedMax != null ? lockedMax : maxShown;
        }
    }

    // Full search once, then the same text rectangle. This is the live path the
    // panel uses after the bars have been found.
    public sealed class SlotWatch
    {
        private IntRect life;
        private IntRect prayer;
        private bool haveLife;
        private bool havePrayer;
        private readonly ReadGate hp = new ReadGate();
        private readonly ReadGate prayerGate = new ReadGate();
        public int? Hp { get { return hp.Value; } }
        public int? HpMax { get { return hp.Max; } }
        public int? Prayer { get { return prayerGate.Value; } }
        public int? PrayerMax { get { return prayerGate.Max; } }
        public bool HpStale { get { return hp.Stale; } }
        public bool PrayerStale { get { return prayerGate.Stale; } }

        public void Observe(ImageBuffer image, WardenSettings settings, DateTime now)
        {
            int? h = null, hm = null, p = null, pm = null;
            if (image == null)
            {
                hp.Push(null, null, now);
                prayerGate.Push(null, null, now);
                return;
            }
            if (!haveLife || !havePrayer)
            {
                HudReading reading = HudReader.Read(image, settings == null ? new WardenSettings() : settings);
                if (reading.Hp != null && reading.HpMax != null && reading.LifeText.Width > 8)
                {
                    life = Pad(reading.LifeText);
                    haveLife = true;
                }
                if (reading.Prayer != null && reading.PrayerMax != null && reading.PrayerText.Width > 8)
                {
                    prayer = Pad(reading.PrayerText);
                    havePrayer = true;
                }
                if (reading.Hp != null && reading.HpMax != null) { h = reading.Hp; hm = reading.HpMax; }
                if (reading.Prayer != null && reading.PrayerMax != null) { p = reading.Prayer; pm = reading.PrayerMax; }
            }
            else
            {
                HudReading lifeRead = new HudReading();
                ImageBuffer lifeCrop = Crop(image, life);
                if (lifeCrop != null) HudReader.ReadBox(lifeCrop, true, lifeRead);
                if (lifeRead.Hp != null && lifeRead.HpMax != null) { h = lifeRead.Hp; hm = lifeRead.HpMax; }
                HudReading prayerRead = new HudReading();
                ImageBuffer prayerCrop = Crop(image, prayer);
                if (prayerCrop != null) HudReader.ReadBox(prayerCrop, false, prayerRead);
                if (prayerRead.Prayer != null && prayerRead.PrayerMax != null) { p = prayerRead.Prayer; pm = prayerRead.PrayerMax; }
            }
            hp.Push(h, hm, now);
            prayerGate.Push(p, pm, now);
        }

        private static IntRect Pad(IntRect box)
        {
            return new IntRect(box.X - 4, box.Y - 2, box.Width + 8, box.Height + 4);
        }

        private static ImageBuffer Crop(ImageBuffer image, IntRect box)
        {
            int x = box.X;
            int y = box.Y;
            int w = box.Width;
            int h = box.Height;
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 4 || h < 4) return null;
            return image.Crop(x, y, w, h);
        }
    }

    public static class ScreenClamp
    {
        public static void Place(ref int x, ref int y, int width, int height, int[] screenX, int[] screenY, int[] screenW, int[] screenH)
        {
            if (screenX == null || screenX.Length == 0)
            {
                x = 24;
                y = 24;
                return;
            }
            for (int i = 0; i < screenX.Length; i++)
            {
                int overlapW = Math.Min(x + width, screenX[i] + screenW[i]) - Math.Max(x, screenX[i]);
                int overlapH = Math.Min(y + height, screenY[i] + screenH[i]) - Math.Max(y, screenY[i]);
                if (overlapW >= 48 && overlapH >= 48) return;
            }
            x = screenX[0] + 24;
            y = screenY[0] + 24;
        }
    }

    public struct BuffIcon
    {
        public int X;
        public bool HasTime;
        public bool Minutes;
        public int Value;
        public string Kind;
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
                    if (icon.Kind != null && icon.Kind.Length > 0 && icon.Kind != "potion") continue;
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

        // Lowest potion timer. Only icons classified as potions count, including
        // a plain seconds value once the flask has counted down past a minute.
        public static int? ShownSeconds(List<BuffIcon> icons)
        {
            int best = -1;
            if (icons == null) return null;
            for (int i = 0; i < icons.Count; i++)
            {
                BuffIcon icon = icons[i];
                if (icon.Kind != "potion" || !icon.HasTime || icon.Value <= 0) continue;
                int seconds = icon.Minutes ? icon.Value * 60 : icon.Value;
                if (best < 0 || seconds < best) best = seconds;
            }
            return best < 0 ? (int?)null : best;
        }
    }

    // Follows the shortest potion timer between reads. Once a timer has been
    // read it keeps counting down on the clock while icons blink, scenery
    // hides the digits, or another interface covers the buff bar, and each
    // new read re-anchors it. A minute value ("11m") is a one-minute band; the
    // read where it ticks down to the next minute pins the exact time. A read
    // that disagrees with the clock has to repeat before the clock moves.
    public sealed class PotionDisplay
    {
        public const double FreshSeconds = 1.0;
        public const int AlertableSeconds = 120;
        public const int ClearAfterSeconds = 60;
        public const int ShorterHoldSeconds = 8;

        private bool tracking;
        private double anchor;
        private DateTime anchorTime;
        private DateTime lastConfirmed;
        private int confirmations;
        private int lastMinuteRead = -1;
        private int? pending;
        private int pendingCount;
        private DateTime pendingTime;
        private DateTime clearSince = DateTime.MinValue;
        private DateTime zeroSince = DateTime.MinValue;

        // True when the shown value comes from the clock rather than this frame.
        public bool Estimated { get; private set; }
        public bool Tracking { get { return tracking; } }
        // Confirmed by at least two reads, the latest within two minutes.
        public bool Alertable { get; private set; }

        public int? Observe(int? fresh, DateTime now) { return Observe(fresh, false, now); }

        // fresh: lowest potion timer read this frame, or null when none was read.
        // barWithoutPotion: the buff bar itself was read and had no potion timer.
        public int? Observe(int? fresh, bool barWithoutPotion, DateTime now)
        {
            if (fresh.HasValue && fresh.Value > 0)
            {
                clearSince = DateTime.MinValue;
                Take(fresh.Value, now);
            }
            else if (barWithoutPotion && clearSince == DateTime.MinValue) clearSince = now;
            return Current(now);
        }

        public int? Current(DateTime now)
        {
            if (!tracking)
            {
                Estimated = false;
                Alertable = false;
                return null;
            }
            double left = Left(now);
            if (left <= 0)
            {
                if (zeroSince == DateTime.MinValue) zeroSince = now;
                else if ((now - zeroSince).TotalSeconds >= 5) { Reset(); return Current(now); }
            }
            else zeroSince = DateTime.MinValue;
            if (clearSince != DateTime.MinValue && (now - clearSince).TotalSeconds >= ClearAfterSeconds) { Reset(); return Current(now); }
            double age = (now - lastConfirmed).TotalSeconds;
            Estimated = age >= FreshSeconds;
            Alertable = confirmations >= 2 && age <= AlertableSeconds;
            return (int)Math.Ceiling(left);
        }

        private double Left(DateTime now)
        {
            double left = anchor - (now - anchorTime).TotalSeconds;
            return left < 0 ? 0 : left;
        }

        private void Take(int value, DateTime now)
        {
            bool minute = value >= 120 && value % 60 == 0;
            if (!tracking)
            {
                Start(value, minute, now, 1);
                return;
            }
            double left = Left(now);
            bool consistent = minute
                ? left >= value - 63 && left <= value + 63
                : Math.Abs(left - value) <= 3;
            if (consistent)
            {
                confirmations++;
                lastConfirmed = now;
                pending = null;
                pendingCount = 0;
                if (minute)
                {
                    // The minute just ticked over, or the clock runs ahead of the
                    // band: the exact time is this minute value.
                    if ((lastMinuteRead > 0 && lastMinuteRead > value) || left > value) Anchor(value, now);
                    lastMinuteRead = value;
                }
                else
                {
                    Anchor(value, now);
                    lastMinuteRead = -1;
                }
                return;
            }
            bool same = pending.HasValue && Math.Abs(pending.Value - value) <= (minute ? 60 : 3) && (now - pendingTime).TotalSeconds <= 6;
            pendingCount = same ? pendingCount + 1 : 1;
            pending = value;
            pendingTime = now;
            bool shorter = value < left;
            // A longer timer stays pending while the shorter one is still being
            // read now and then; a fresh dose shows once the old value is gone.
            bool oldStillSeen = (now - lastConfirmed).TotalSeconds < ShorterHoldSeconds;
            if (confirmations < 2 && pendingCount >= 2) Start(value, minute, now, 2);
            else if (shorter && pendingCount >= 2) Start(value, minute, now, 2);
            else if (!shorter && pendingCount >= 3 && !oldStillSeen) Start(value, minute, now, 2);
        }

        private void Start(int value, bool minute, DateTime now, int confirmed)
        {
            tracking = true;
            Anchor(value, now);
            confirmations = confirmed;
            lastConfirmed = now;
            lastMinuteRead = minute ? value : -1;
            pending = null;
            pendingCount = 0;
            zeroSince = DateTime.MinValue;
            clearSince = DateTime.MinValue;
        }

        private void Anchor(int value, DateTime now)
        {
            anchor = value;
            anchorTime = now;
        }

        private void Reset()
        {
            tracking = false;
            confirmations = 0;
            pending = null;
            pendingCount = 0;
            lastMinuteRead = -1;
            zeroSince = DateTime.MinValue;
            clearSince = DateTime.MinValue;
        }
    }

    public sealed class WatchSample
    {
        public DateTime Time;
        public bool GameAttached;
        public bool PixelsFresh;
        public bool CharacterMoving;
        // Skilling mode: an XP drop or a backpack change this frame.
        public bool SkillProgress;
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
        public TimeSpan AfkRemaining = TimeSpan.FromMinutes(25);
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
            bool active = settings.SkillingMode ? sample.SkillProgress || sample.InputActivity : sample.CharacterMoving;
            StillFor = stillness.StillFor(see && active, sample.Time, see);
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
            bool idle = see && settings.IdleEnabled && StillFor >= TimeSpan.FromSeconds(settings.ActiveIdleSeconds);
            IdleLatched = idle;
            if (idleGate.ShouldFire(idle, sample.Time, cooldown))
            {
                if (settings.SkillingMode)
                    events.Add(Make(settings, "idle", "You stopped skilling", "No XP drop, backpack change or click for " + (int)StillFor.TotalSeconds + "s."));
                else
                    events.Add(Make(settings, "idle", "You have gone idle", "No animation or movement for " + (int)StillFor.TotalSeconds + "s."));
            }

            int? hpNow = see ? sample.Hp : (int?)null;
            if (hp.Tick(hpNow, sample.HpMax, settings.HpPercent, settings.HpThreshold, settings.HpEnabled, sample.Time, cooldown))
                events.Add(Make(settings, "hp", "Hitpoints are low", FormatLevel("Hitpoints", sample.Hp, sample.HpMax, settings.HpPercent)));
            HpLatched = hp.Low;

            int? prayerNow = see ? sample.Prayer : (int?)null;
            if (prayer.Tick(prayerNow, sample.PrayerMax, settings.PrayerPercent, settings.PrayerThreshold, settings.PrayerEnabled, sample.Time, cooldown))
                events.Add(Make(settings, "prayer", "Prayer is low", FormatLevel("Prayer", sample.Prayer, sample.PrayerMax, settings.PrayerPercent)));
            PrayerLatched = prayer.Low;

            // The potion clock keeps running from its last confirmed read, so a
            // covered or unreadable buff bar can still warn.
            int? potionNow = sample.PotionSeconds;
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
            if (iconic || !foreground) return false;
            return buttonDown || keyDown;
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
