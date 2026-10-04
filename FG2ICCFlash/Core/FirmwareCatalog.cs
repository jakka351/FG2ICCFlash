using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FG2ICCFlasher.Core
{
    /// <summary>A single firmware image available to the tool (embedded resource or a browsed file).</summary>
    public sealed class FirmwareEntry
    {
        public string FileName { get; set; }
        public string ResourceName { get; set; }   // null for a browsed file
        public string FilePath { get; set; }        // null for an embedded resource
        public PhfFile Phf { get; set; }
        public bool IsFlashDriver => Phf != null && Phf.IsFlashDriver;

        public string Display
        {
            get
            {
                if (Phf == null) return FileName;
                string kind = Phf.IsFlashDriver ? "Flash Driver" : "Application";
                return $"{Path.GetFileNameWithoutExtension(FileName)}  —  {Phf.Application} v{Phf.MaskNumber} (cksum 0x{Phf.FileChecksum:X4})";
            }
        }

        public override string ToString() => Display;
    }

    /// <summary>
    /// Enumerates the PHF firmware images embedded in the assembly (the six FDM application
    /// variants and the AR79-14D019-AA secondary bootloader), and loads PHFs from embedded
    /// resources or the filesystem.
    /// </summary>
    public static class FirmwareCatalog
    {
        private const string Marker = ".Firmware.";

        public static List<FirmwareEntry> LoadEmbedded()
        {
            var list = new List<FirmwareEntry>();
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames()
                         .Where(n => n.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) >= 0
                                     && n.EndsWith(".PHF", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(n => n))
            {
                try
                {
                    byte[] bytes;
                    using (var s = asm.GetManifestResourceStream(res))
                    using (var ms = new MemoryStream())
                    {
                        if (s == null) continue;
                        s.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    string fileName = res.Substring(res.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) + Marker.Length);
                    list.Add(new FirmwareEntry { FileName = fileName, ResourceName = res, Phf = PhfFile.Parse(bytes) });
                }
                catch { /* skip an unreadable embedded resource */ }
            }
            return list;
        }

        public static FirmwareEntry LoadFile(string path)
        {
            return new FirmwareEntry { FileName = Path.GetFileName(path), FilePath = path, Phf = PhfFile.Load(path) };
        }

        public static List<FirmwareEntry> Applications(this List<FirmwareEntry> all) => all.Where(e => !e.IsFlashDriver).ToList();
        public static List<FirmwareEntry> FlashDrivers(this List<FirmwareEntry> all) => all.Where(e => e.IsFlashDriver).ToList();
    }
}
