using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace KavrosAfkWarden
{
    public static class SettingsText
    {
        public static string Write(WardenSettings settings)
        {
            settings.Clamp();
            StringBuilder text = new StringBuilder();
            Line(text, "idle-enabled", settings.IdleEnabled);
            Line(text, "idle-seconds", settings.IdleSeconds);
            Line(text, "hp-enabled", settings.HpEnabled);
            Line(text, "hp-threshold", settings.HpThreshold);
            Line(text, "hp-percent", settings.HpPercent);
            Line(text, "prayer-enabled", settings.PrayerEnabled);
            Line(text, "prayer-threshold", settings.PrayerThreshold);
            Line(text, "prayer-percent", settings.PrayerPercent);
            Line(text, "afk-enabled", settings.AfkEnabled);
            Line(text, "afk-grandmaster", settings.Grandmaster);
            Line(text, "afk-tree-perk", settings.TreePerk);
            Line(text, "afk-lead", settings.AfkLeadSeconds);
            Line(text, "sound", settings.SoundMaster);
            Line(text, "notify", settings.NotifyMaster);
            Line(text, "idle-sound", settings.IdleSound);
            Line(text, "idle-notify", settings.IdleNotify);
            Line(text, "hp-sound", settings.HpSound);
            Line(text, "hp-notify", settings.HpNotify);
            Line(text, "prayer-sound", settings.PrayerSound);
            Line(text, "prayer-notify", settings.PrayerNotify);
            Line(text, "afk-sound", settings.AfkSound);
            Line(text, "afk-notify", settings.AfkNotify);
            Line(text, "potion-enabled", settings.PotionEnabled);
            Line(text, "potion-seconds", settings.PotionSeconds);
            Line(text, "potion-sound", settings.PotionSound);
            Line(text, "potion-notify", settings.PotionNotify);
            Line(text, "volume", settings.Volume);
            Line(text, "cooldown", settings.CooldownSeconds);
            Line(text, "panel-offset-x", settings.PanelOffsetX);
            Line(text, "panel-offset-y", settings.PanelOffsetY);
            text.Append("client-title\t").Append(settings.ClientTitle.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")).Append("\r\n");
            Line(text, "banner-seconds", settings.BannerSeconds);
            Line(text, "paused", settings.Paused);
            Line(text, "check-updates", settings.CheckForUpdates);
            Line(text, "save-debug", settings.SaveDebug);
            Line(text, "panel-topmost", settings.PanelTopMost);
            text.Append("panel\t").Append(settings.PanelX.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(settings.PanelY.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            Pin(text, "hp-pin", settings.HpPinX, settings.HpPinY, settings.HpPinW, settings.HpPinH);
            Pin(text, "prayer-pin", settings.PrayerPinX, settings.PrayerPinY, settings.PrayerPinW, settings.PrayerPinH);
            Pin(text, "buff-pin", settings.BuffPinX, settings.BuffPinY, settings.BuffPinW, settings.BuffPinH);
            return text.ToString();
        }

        public static WardenSettings Read(string text)
        {
            WardenSettings settings = new WardenSettings();
            if (string.IsNullOrEmpty(text)) return settings;
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                try { ReadLine(lines[i], settings); }
                catch (Exception) { }
            }
            try { settings.Clamp(); }
            catch (Exception) { return new WardenSettings(); }
            return settings;
        }

        private static void ReadLine(string line, WardenSettings settings)
        {
                string[] parts = line.Split('\t');
                if (parts.Length < 2) return;
                bool flag;
                int number;
                if (parts[0] == "idle-enabled" && bool.TryParse(parts[1], out flag)) settings.IdleEnabled = flag;
                else if (parts[0] == "idle-seconds" && int.TryParse(parts[1], out number)) settings.IdleSeconds = number;
                else if (parts[0] == "hp-enabled" && bool.TryParse(parts[1], out flag)) settings.HpEnabled = flag;
                else if (parts[0] == "hp-threshold" && int.TryParse(parts[1], out number)) settings.HpThreshold = number;
                else if (parts[0] == "hp-percent" && bool.TryParse(parts[1], out flag)) settings.HpPercent = flag;
                else if (parts[0] == "prayer-enabled" && bool.TryParse(parts[1], out flag)) settings.PrayerEnabled = flag;
                else if (parts[0] == "prayer-threshold" && int.TryParse(parts[1], out number)) settings.PrayerThreshold = number;
                else if (parts[0] == "prayer-percent" && bool.TryParse(parts[1], out flag)) settings.PrayerPercent = flag;
                else if (parts[0] == "afk-enabled" && bool.TryParse(parts[1], out flag)) settings.AfkEnabled = flag;
                else if (parts[0] == "afk-grandmaster" && bool.TryParse(parts[1], out flag)) settings.Grandmaster = flag;
                else if (parts[0] == "afk-tree-perk" && bool.TryParse(parts[1], out flag)) settings.TreePerk = flag;
                else if (parts[0] == "afk-lead" && int.TryParse(parts[1], out number)) settings.AfkLeadSeconds = number;
                else if (parts[0] == "sound" && bool.TryParse(parts[1], out flag)) settings.SoundMaster = flag;
                else if (parts[0] == "notify" && bool.TryParse(parts[1], out flag)) settings.NotifyMaster = flag;
                else if (parts[0] == "idle-sound" && bool.TryParse(parts[1], out flag)) settings.IdleSound = flag;
                else if (parts[0] == "idle-notify" && bool.TryParse(parts[1], out flag)) settings.IdleNotify = flag;
                else if (parts[0] == "hp-sound" && bool.TryParse(parts[1], out flag)) settings.HpSound = flag;
                else if (parts[0] == "hp-notify" && bool.TryParse(parts[1], out flag)) settings.HpNotify = flag;
                else if (parts[0] == "prayer-sound" && bool.TryParse(parts[1], out flag)) settings.PrayerSound = flag;
                else if (parts[0] == "prayer-notify" && bool.TryParse(parts[1], out flag)) settings.PrayerNotify = flag;
                else if (parts[0] == "afk-sound" && bool.TryParse(parts[1], out flag)) settings.AfkSound = flag;
                else if (parts[0] == "afk-notify" && bool.TryParse(parts[1], out flag)) settings.AfkNotify = flag;
                else if (parts[0] == "potion-enabled" && bool.TryParse(parts[1], out flag)) settings.PotionEnabled = flag;
                else if (parts[0] == "potion-seconds" && int.TryParse(parts[1], out number)) settings.PotionSeconds = number;
                else if (parts[0] == "potion-sound" && bool.TryParse(parts[1], out flag)) settings.PotionSound = flag;
                else if (parts[0] == "potion-notify" && bool.TryParse(parts[1], out flag)) settings.PotionNotify = flag;
                else if (parts[0] == "volume" && int.TryParse(parts[1], out number)) settings.Volume = number;
                else if (parts[0] == "cooldown" && int.TryParse(parts[1], out number)) settings.CooldownSeconds = number;
                else if (parts[0] == "panel-offset-x" && int.TryParse(parts[1], out number)) settings.PanelOffsetX = number;
                else if (parts[0] == "panel-offset-y" && int.TryParse(parts[1], out number)) settings.PanelOffsetY = number;
                else if (parts[0] == "client-title") settings.ClientTitle = parts[1];
                else if (parts[0] == "banner-seconds" && int.TryParse(parts[1], out number)) settings.BannerSeconds = number;
                else if (parts[0] == "paused" && bool.TryParse(parts[1], out flag)) settings.Paused = flag;
                else if (parts[0] == "check-updates" && bool.TryParse(parts[1], out flag)) settings.CheckForUpdates = flag;
                else if (parts[0] == "save-debug" && bool.TryParse(parts[1], out flag)) settings.SaveDebug = flag;
                else if (parts[0] == "panel-topmost" && bool.TryParse(parts[1], out flag)) settings.PanelTopMost = flag;
                else if (parts[0] == "panel" && parts.Length >= 3)
                {
                    int x, y;
                    if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                    {
                        settings.PanelX = x;
                        settings.PanelY = y;
                    }
                }
                else if (parts[0] == "hp-pin") ReadPin(parts, settings, 0);
                else if (parts[0] == "prayer-pin") ReadPin(parts, settings, 1);
                else if (parts[0] == "buff-pin") ReadPin(parts, settings, 2);
        }

        public static void Save(string path, WardenSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, Write(settings));
        }

        public static WardenSettings Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new WardenSettings();
                return Read(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return new WardenSettings();
            }
        }

        private static void Line(StringBuilder text, string key, bool value)
        {
            text.Append(key).Append('\t').Append(value ? "True" : "False").Append("\r\n");
        }

        private static void Line(StringBuilder text, string key, int value)
        {
            text.Append(key).Append('\t').Append(value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }

        private static void Pin(StringBuilder text, string key, double x, double y, double w, double h)
        {
            text.Append(key).Append('\t').Append(x.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(y.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(w.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(h.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }

        private static void ReadPin(string[] parts, WardenSettings settings, int which)
        {
            if (parts.Length < 5) return;
            double x, y, w, h;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out w)) return;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out h)) return;
            if (which == 0) { settings.HpPinX = x; settings.HpPinY = y; settings.HpPinW = w; settings.HpPinH = h; }
            else if (which == 1) { settings.PrayerPinX = x; settings.PrayerPinY = y; settings.PrayerPinW = w; settings.PrayerPinH = h; }
            else { settings.BuffPinX = x; settings.BuffPinY = y; settings.BuffPinW = w; settings.BuffPinH = h; }
        }
    }
}
