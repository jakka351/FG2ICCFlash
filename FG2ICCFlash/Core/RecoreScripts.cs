using System;
using System.Collections.Generic;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Canonical QNX shell scripts for the FDIM USB recore, plus snippet building blocks for the
    /// script editor. Derived from the real Mark-II FDIM recore procedure:
    ///   recore.sh  (in image-usb-recore\) is run by the ICC recovery; it copies z.sh to /packages
    ///              and launches it.
    ///   z.sh       (USB root) is the worker — it remounts the USB and restores the factory package
    ///              tree to /packages/factory, per repair.log.
    /// Scripts MUST be written with LF (\n) line endings for QNX.
    /// </summary>
    public static class RecoreScripts
    {
        /// <summary>The /packages/factory package list restored during a recore, in the exact order the
        /// factory tool logs in repair.log. Nav units additionally carry navi / navi_maps / speech.</summary>
        public static readonly string[] FactoryPackages =
        {
            "diagnostics", "applications", "core-pkg", "iplspl", "qnx_binaries_and_libraries",
            "bluetooth", "io", "swsa_binaries_and_libraries", "power", "media_player",
            "fast_startup", "hmi", "graphics",
        };

        /// <summary>The launcher dropped in image-usb-recore\recore.sh.</summary>
        public const string RecoreLauncher =
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Custom script is running!\"\n" +
            "cp -f /fs/usb0/z.sh /packages/z.sh\n" +
            "cd /\n" +
            "/packages/z.sh &\n";

        /// <summary>Worker template that restores the factory package tree from the USB.</summary>
        public static string FactoryRecoreWorker()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("# FDIM Recore worker — restores the factory package tree from USB to /packages/factory\n");
            sb.Append("command -v hmiShow.sh >/dev/null && . hmiShow.sh \"FDIM Recore starting...\"\n");
            sb.Append("sleep 3\n");
            sb.Append("command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Preparing USB filesystem...\"\n");
            sb.Append("umount -f /fs/usb0\n");
            sb.Append("slay devb-umass\n");
            sb.Append("devb-umass cam pnp blk cache=2m,auto=partition,automount=hd0@dos:/fs/usb0,rw dos exe=all\n");
            sb.Append("sleep 10\n");
            sb.Append("cd /fs/usb0\n");
            foreach (var p in FactoryPackages)
            {
                sb.Append($"command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Restoring {p} ...\"\n");
                sb.Append($"cp -LR ./factory/{p} /packages/factory/{p}\n");
            }
            sb.Append("sleep 5\n");
            sb.Append("command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Recore complete — please reset the FDIM!\"\n");
            return sb.ToString();
        }

        /// <summary>Worker template for the gauges + FPV-logo mods (the usb-root z.sh).</summary>
        public const string GaugesModsWorker =
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Gauge Activation Script\"\n" +
            "sleep 5\n" +
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Preparing USB Filesystem...\"\n" +
            "sleep 3\n" +
            "umount -f /fs/usb0\n" +
            "slay devb-umass\n" +
            "devb-umass cam pnp blk cache=2m,auto=partition,automount=hd0@dos:/fs/usb0,rw dos exe=all\n" +
            "sleep 10\n" +
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Copying code to FDIM file system...\"\n" +
            "cd fs/usb0\n" +
            "cp -LR ./gauges.sh /packages/systen/trailer/gauges.sh\n" +
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Transferring FPV Files...\"\n" +
            "cp -LR ./FpvLe /packages/factory/hmi/root_dir/usr/hmi/HighSeries/FpvLe\n" +
            "sleep 10\n" +
            "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Script has finished, please reset FDIM!\"\n";

        /// <summary>Named snippets for the editor's insert palette.</summary>
        public static readonly List<KeyValuePair<string, string>> Snippets = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Show HMI message", "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"your message here\"\n"),
            new KeyValuePair<string, string>("Remount USB (devb-umass)",
                "umount -f /fs/usb0\nslay devb-umass\ndevb-umass cam pnp blk cache=2m,auto=partition,automount=hd0@dos:/fs/usb0,rw dos exe=all\nsleep 10\ncd /fs/usb0\n"),
            new KeyValuePair<string, string>("Copy one factory package", "cp -LR ./factory/<package> /packages/factory/<package>\n"),
            new KeyValuePair<string, string>("Copy ALL factory packages (loop)",
                "for pkg in " + string.Join(" ", FactoryPackages) + "; do\n  command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Copying $pkg ...\"\n  cp -LR ./factory/$pkg /packages/factory/$pkg\ndone\n"),
            new KeyValuePair<string, string>("Copy file to FDIM", "cp -LR ./<source> /packages/<destination>\n"),
            new KeyValuePair<string, string>("Sleep", "sleep 5\n"),
            new KeyValuePair<string, string>("Finish / reset prompt", "command -v hmiShow.sh >/dev/null && . hmiShow.sh \"Done — please reset the FDIM!\"\n"),
        };

        /// <summary>Normalise any line endings to LF for QNX.</summary>
        public static string ToUnix(string s) => (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
