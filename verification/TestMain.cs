using System;
using System.IO;

namespace KavrosAfkWarden
{
    internal static class TestMain
    {
        private static int Main(string[] args)
        {
            string report = args.Length > 0 ? args[0] : "test-results.txt";
            string root = args.Length > 1 ? args[1] : FindRoot();
            return WardenTests.Run(report, root);
        }

        private static string FindRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "companion.module.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            return "";
        }
    }
}
