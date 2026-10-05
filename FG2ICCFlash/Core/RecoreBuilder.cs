using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Builds a bootable ICC recore USB by extracting a recore image zip to the FAT32 USB root.
    /// The zip already carries the correct layout (image-usb-recore/recore.sh, z.sh, factory/…);
    /// this class extracts it, normalises the shell scripts to LF (QNX requirement), and can
    /// override the z.sh worker with a script edited in the in-app engine.
    /// </summary>
    public static class RecoreBuilder
    {
        public static bool BuildFromZip(string zipPath, string usbRoot, string customWorkerScript,
            Action<string> log, Action<int> progress, Func<bool> abort, out string err)
        {
            err = null;
            try
            {
                if (!File.Exists(zipPath)) { err = "Recore zip not found: " + zipPath; return false; }
                if (!Directory.Exists(usbRoot)) { err = "USB root not found: " + usbRoot; return false; }

                using (var za = ZipFile.OpenRead(zipPath))
                {
                    var entries = za.Entries.Where(e => e.Length > 0 || !e.FullName.EndsWith("/")).ToList();
                    int total = entries.Count, done = 0;
                    log?.Invoke($"Extracting {total} file(s) to {usbRoot} …");
                    foreach (var e in za.Entries)
                    {
                        if (abort != null && abort()) { err = "Aborted."; return false; }
                        string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string dest = Path.Combine(usbRoot, rel);
                        if (e.FullName.EndsWith("/") || e.Length == 0 && e.Name.Length == 0)
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        bool isScript = e.FullName.EndsWith(".sh", StringComparison.OrdinalIgnoreCase);
                        if (isScript)
                        {
                            // Normalise scripts to LF regardless of how they were stored.
                            string text;
                            using (var sr = new StreamReader(e.Open())) text = sr.ReadToEnd();
                            File.WriteAllText(dest, RecoreScripts.ToUnix(text));
                        }
                        else
                        {
                            e.ExtractToFile(dest, true);
                        }
                        done++;
                        if (total > 0) progress?.Invoke((int)Math.Min(100, (long)done * 100 / total));
                    }
                }

                // Optional: replace the worker with a script edited in the engine (LF enforced).
                if (!string.IsNullOrEmpty(customWorkerScript))
                {
                    string z = Path.Combine(usbRoot, "z.sh");
                    File.WriteAllText(z, RecoreScripts.ToUnix(customWorkerScript));
                    log?.Invoke("z.sh overridden with the edited recore script.");
                }

                // Sanity check the expected launcher is present.
                string launcher = Path.Combine(usbRoot, "image-usb-recore", "recore.sh");
                if (!File.Exists(launcher))
                    log?.Invoke("WARNING: image-usb-recore/recore.sh is not present — the ICC will not auto-run this USB.");

                log?.Invoke("Recore USB built. Eject safely, then follow the on-vehicle recore steps.");
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }

        /// <summary>Read the z.sh worker currently inside a recore zip (for pre-loading the editor).</summary>
        public static string ReadWorker(string zipPath)
        {
            try
            {
                using (var za = ZipFile.OpenRead(zipPath))
                {
                    var z = za.GetEntry("z.sh");
                    if (z == null) return null;
                    using (var sr = new StreamReader(z.Open())) return RecoreScripts.ToUnix(sr.ReadToEnd());
                }
            }
            catch { return null; }
        }
    }
}
