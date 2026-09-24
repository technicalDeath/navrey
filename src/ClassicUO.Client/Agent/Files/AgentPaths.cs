// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;

namespace ClassicUO.Agent
{
    /// <summary>
    /// Default files used by the agent transport. Unix historically used /tmp, but Windows maps
    /// that spelling to a shared C:\tmp directory whose stale files can be locked by another
    /// process or protected by local policy. Keep the Unix contract and use a per-user directory
    /// on Windows so a normal client launch can always initialize its command/log files.
    /// </summary>
    internal static class AgentPaths
    {
        private static readonly string RuntimeDirectory = OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), "Navrey")
            : "/tmp";

        public static string CommandFile => Path.Combine(RuntimeDirectory, "cuocmd");
        public static string LogFile => Path.Combine(RuntimeDirectory, "cuolog");
        public static string StateFile => Path.Combine(RuntimeDirectory, "cuostate.json");
        public static string WorldFile => Path.Combine(RuntimeDirectory, "cuoworld.json");

        public static void EnsureParentDirectory(string path)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }
    }
}
