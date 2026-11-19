using System;
using System.Globalization;
using System.IO;

namespace KavrosAfkWarden
{
    internal static class CompanionStatus
    {
        private static string lastState;
        private static bool lastPaused;
        private static DateTime lastWrite;
        internal static void Write(string state, bool paused)
        {
            try
            {
                if(state==lastState && paused==lastPaused && (DateTime.UtcNow-lastWrite).TotalSeconds<5)return;
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
                string path=Path.Combine(dir, AppInfo.ModuleId + ".json");
                string next=path+".tmp";
                File.WriteAllText(next,json);
                if(File.Exists(path)) File.Replace(next,path,null); else File.Move(next,path);
                lastWrite=DateTime.UtcNow;lastState=state;lastPaused=paused;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
