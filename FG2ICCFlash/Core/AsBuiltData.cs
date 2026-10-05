using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FG2ICCFlasher.Core
{
    /// <summary>One row of a ForScan As-Built (.abt) file: "7A6G&lt;block&gt;G&lt;line&gt;&lt;data…&gt;&lt;checksum&gt;".</summary>
    public sealed class AbtLine
    {
        public int Block;
        public int Line;
        public byte[] Data;       // data bytes (checksum excluded)
        public byte Checksum;     // last byte of the row
    }

    /// <summary>
    /// FDIM As-Built configuration: a map of location (0x00..) to raw data bytes, read with
    /// service $21 and written with $3B (in the $10 87 adjustment session; writes need security).
    /// ABT block N corresponds to module location (N-1): block 1 = $21 00, block 2 = VIN, etc.
    ///
    /// Block map (per the As Built Data notes + the real XR6T ABT):
    ///   0x00 (block 1)  ForScan option bits (FPV splash, reverse cam, park assist, touch-screen)
    ///   0x01 (block 2)  VIN                 (module usually rejects a VIN rewrite)
    ///   0x02 (block 3)  unknown
    ///   0x03 (block 4)  zone                (byte0 0x0B = DUAL, 0x00 = SINGLE; tail 0A 03 0A 05 14)
    ///   0x04 (block 5)  Bluetooth name text ("Ford Falcon Bluetooth")
    ///   0x05/0x06 (6/7) additional config
    /// </summary>
    public sealed class AsBuiltConfig
    {
        public const byte LocOptions = 0x00;
        public const byte LocVin = 0x01;
        public const byte LocBlock3 = 0x02;
        public const byte LocZone = 0x03;
        public const byte LocBtText = 0x04;

        public static readonly byte[] DefaultLocations = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        public static readonly byte[] ZoneTail = { 0x0A, 0x03, 0x0A, 0x05, 0x14 };

        public readonly Dictionary<byte, byte[]> Blocks = new Dictionary<byte, byte[]>();
        /// <summary>Original ABT row layout (from a loaded file), used to round-trip Save with correct checksums.</summary>
        public readonly List<AbtLine> RawLines = new List<AbtLine>();
        /// <summary>Module address prefix for ABT rows (e.g. "7A6").</summary>
        public string Module = "7A6";

        public byte[] Get(byte loc) { byte[] v; return Blocks.TryGetValue(loc, out v) ? v : null; }
        public void Set(byte loc, byte[] data) { Blocks[loc] = data ?? new byte[0]; }
        public bool Has(byte loc) => Blocks.ContainsKey(loc);

        public string Vin
        {
            get { var b = Get(LocVin); return b == null ? null : Ascii(b).Trim(); }
        }

        /// <summary>true = dual zone, false = single zone, null = unknown/not read.</summary>
        public bool? ZoneDual
        {
            get { var b = Get(LocZone); if (b == null || b.Length == 0) return null; return b[0] == 0x0B; }
        }

        /// <summary>Build a zone block (byte0 selector + preserved tail from the current block if present).</summary>
        public byte[] BuildZoneBlock(bool dual)
        {
            var cur = Get(LocZone);
            var tail = (cur != null && cur.Length >= 1) ? cur.Skip(1).ToArray() : ZoneTail;
            return HexUtil.Concat(new byte[] { dual ? (byte)0x0B : (byte)0x00 }, tail);
        }

        public string DescribeAll()
        {
            var sb = new StringBuilder();
            foreach (var loc in Blocks.Keys.OrderBy(k => k))
            {
                var d = Blocks[loc];
                sb.AppendLine($"  block {loc + 1} (loc 0x{loc:X2}): {HexUtil.ToHex(d)}   \"{Ascii(d)}\"{Decode(loc, d)}");
            }
            return sb.ToString();
        }

        /// <summary>Human-readable meaning of an As-Built location, for the raw-block grid.</summary>
        public static string MeaningOf(byte loc)
        {
            switch (loc)
            {
                case LocOptions: return "ForScan option bits (splash / cam / park assist)";
                case LocVin: return "VIN (module usually rejects a rewrite)";
                case LocBlock3: return "block 3 — reserved / model config";
                case LocZone: return "Climate zone (0x0B dual / 0x00 single)";
                case LocBtText: return "Bluetooth device name";
                case 0x05: return "block 6 — config";
                case 0x06: return "block 7 — config";
                default: return "block " + (loc + 1);
            }
        }

        private static string Decode(byte loc, byte[] d)
        {
            if (loc == LocZone && d.Length >= 1) return d[0] == 0x0B ? "   [DUAL zone]" : (d[0] == 0x00 ? "   [SINGLE zone]" : "");
            if (loc == LocVin) return "   [VIN]";
            if (loc == LocOptions) return "   [ForScan option bits]";
            if (loc == LocBtText) return "   [Bluetooth name]";
            return "";
        }

        internal static string Ascii(byte[] b)
            => b == null ? "" : new string(b.Select(x => (x >= 0x20 && x < 0x7F) ? (char)x : '.').ToArray());
    }

    /// <summary>
    /// ForScan As-Built (.abt) reader/writer for the FDIM "7A6G&lt;b&gt;G&lt;l&gt;&lt;data&gt;&lt;ck&gt;" format.
    /// Save round-trips the original row layout and keeps checksums correct by adjusting each row's
    /// stored checksum by the negative of its data-sum delta (the Ford as-built checksum is a
    /// per-row seed minus the data sum, so the seed cancels out for an in-place edit).
    /// </summary>
    public static class AbtFile
    {
        private static readonly Regex Row = new Regex(@"^([0-9A-Fa-f]{3})G(\d)G(\d)([0-9A-Fa-f]+)$");

        public static AsBuiltConfig Parse(string path, Action<string> log)
        {
            var cfg = new AsBuiltConfig();
            var byBlock = new SortedDictionary<int, List<byte>>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                var m = Row.Match(line);
                if (!m.Success) continue;
                string payload = m.Groups[4].Value;
                if (payload.Length < 2 || (payload.Length % 2) != 0) continue;

                cfg.Module = m.Groups[1].Value.ToUpperInvariant();
                int blk = int.Parse(m.Groups[2].Value), ln = int.Parse(m.Groups[3].Value);
                var bytes = new byte[payload.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(payload.Substring(i * 2, 2), 16);
                var data = bytes.Take(bytes.Length - 1).ToArray();
                byte ck = bytes[bytes.Length - 1];

                cfg.RawLines.Add(new AbtLine { Block = blk, Line = ln, Data = data, Checksum = ck });
                if (!byBlock.ContainsKey(blk)) byBlock[blk] = new List<byte>();
                byBlock[blk].AddRange(data);
            }
            foreach (var kv in byBlock) cfg.Set((byte)(kv.Key - 1), kv.Value.ToArray());  // location = block - 1
            log?.Invoke($"ABT parsed: {cfg.Blocks.Count} block(s), {cfg.RawLines.Count} row(s).  VIN=\"{cfg.Vin}\"  Zone={(cfg.ZoneDual == true ? "DUAL" : cfg.ZoneDual == false ? "SINGLE" : "?")}");
            return cfg;
        }

        /// <summary>
        /// Write the config back to an ABT file, preserving the original row layout and recomputing
        /// each edited row's checksum by delta. A block whose edited data length differs from the
        /// original total is left at its original bytes (checksum cannot be derived without the seed),
        /// and that is reported.
        /// </summary>
        public static void Save(AsBuiltConfig cfg, string path, Action<string> log)
        {
            if (cfg.RawLines.Count == 0) throw new InvalidOperationException("No ABT row layout loaded — load an ABT file before saving.");
            var sb = new StringBuilder();
            foreach (var g in cfg.RawLines.GroupBy(l => l.Block).OrderBy(x => x.Key))
            {
                int blk = g.Key;
                sb.Append(";Block " + blk + "\r\n");
                var orig = g.OrderBy(l => l.Line).ToList();
                int origTotal = orig.Sum(l => l.Data.Length);
                var edited = cfg.Get((byte)(blk - 1));
                bool resize = edited != null && edited.Length != origTotal;
                if (resize) log?.Invoke($"  block {blk}: edited length {edited.Length} != original {origTotal}; keeping original bytes/checksums.");

                int pos = 0;
                foreach (var row in orig)
                {
                    byte[] nd; byte nck;
                    if (edited != null && !resize)
                    {
                        nd = new byte[row.Data.Length];
                        Array.Copy(edited, pos, nd, 0, nd.Length);
                        int delta = nd.Sum(x => x) - row.Data.Sum(x => x);
                        nck = (byte)((row.Checksum - delta) & 0xFF);
                    }
                    else { nd = row.Data; nck = row.Checksum; }
                    pos += row.Data.Length;

                    var hex = string.Concat(nd.Select(x => x.ToString("X2"))) + nck.ToString("X2");
                    sb.Append($"{cfg.Module}G{blk}G{row.Line}{hex}\r\n");
                }
            }
            File.WriteAllText(path, sb.ToString());
            log?.Invoke("ABT saved: " + path);
        }
    }

    /// <summary>Orchestration for reading/writing As-Built via a GdsClient (caller supplies the session).</summary>
    public static class ConfigOps
    {
        public static AsBuiltConfig ReadConfig(GdsClient gds, FlashOptions opt, Action<string> log, IEnumerable<byte> locations)
        {
            var cfg = new AsBuiltConfig();
            foreach (var loc in locations)
            {
                var r = gds.ReadAsBuilt(loc, opt.P2TimeoutMs);
                if (r.Positive && r.Data.Length >= 2)
                {
                    var data = r.Data.Skip(2).ToArray();   // strip 61 <loc>
                    cfg.Set(loc, data);
                    log($"  [21 {loc:X2}] = {HexUtil.ToHex(data)}   \"{AsBuiltConfig.Ascii(data)}\"");
                }
                else log($"  [21 {loc:X2}] {r.Describe()}");
            }
            return cfg;
        }

        public static bool WriteBlock(GdsClient gds, FlashOptions opt, Action<string> log, byte loc, byte[] data)
        {
            log($"  [3B {loc:X2}] write {HexUtil.ToHex(data)}");
            var r = gds.WriteAsBuilt(loc, data, opt.RoutineTimeoutMs);
            log("    " + r.Describe());
            return r.Positive;
        }
    }
}
