using System;
using System.Globalization;
using System.IO;

namespace KavrosAfkWarden
{
    internal static class CompanionStatus
    {
        internal static void Write(string state, bool paused)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kavros", "Companion", "modules");
                Directory.CreateDirectory(dir);
                string json = "{"
                    + "\"id\":\"" + AppInfo.ModuleId + "\","
                    + "\"name\":\"" + AppInfo.Name + "\","
                    + "\"author\":\"" + AppInfo.Author + "\","
                    + "\"version\":\"" + AppInfo.Version + "\","
                    + "\"state\":\"" + state + "\","
                    + "\"paused\":" + (paused ? "true" : "false") + ","
                    + "\"label\":\"" + (state == "running" ? "Running · " + AppInfo.DisplayVersion : "Stopped") + "\","
                    + "\"updatedUtc\":\"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\""
                    + "}";
                File.WriteAllText(Path.Combine(dir, AppInfo.ModuleId + ".json"), json);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
