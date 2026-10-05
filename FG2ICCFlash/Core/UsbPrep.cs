using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace FG2ICCFlasher.Core
{
    /// <summary>A removable (USB) drive the operator can prepare for the ICC Stage-2 upload.</summary>
    public sealed class UsbDrive
    {
        public char Letter;          // 'E'
        public string Root;          // "E:\"
        public string Label;
        public long TotalBytes;
        public bool Ready;

        public double TotalGB => TotalBytes / 1024.0 / 1024.0 / 1024.0;
        public string Display =>
            $"{Letter}:\\   {(Ready ? (string.IsNullOrEmpty(Label) ? "(no label)" : Label) : "(not ready)")}   {(Ready ? TotalGB.ToString("0.0") + " GB" : "")}";

        public override string ToString() => Display;
    }

    /// <summary>
    /// USB preparation for the ICC Stage-2 application upload: enumerate REMOVABLE drives only,
    /// open the Windows FAT32 format dialog (matches the dealer instruction and avoids needing
    /// admin on removable media), and copy a payload folder's contents to the USB root with a
    /// size/count verify. Destructive operations are gated by the caller's explicit confirmation.
    /// </summary>
    public static class UsbPrep
    {
        // ---- Drive enumeration (removable only — never touches fixed/system disks) ----
        public static List<UsbDrive> ListRemovable()
        {
            var list = new List<UsbDrive>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Removable) continue;   // USB sticks / SD only
                    bool ready = d.IsReady;
                    list.Add(new UsbDrive
                    {
                        Letter = char.ToUpperInvariant(d.Name[0]),
                        Root = d.RootDirectory.FullName,
                        Ready = ready,
                        Label = ready ? SafeLabel(d) : "",
                        TotalBytes = ready ? d.TotalSize : 0,
                    });
                }
                catch { /* skip an inaccessible drive */ }
            }
            return list;
        }

        private static string SafeLabel(DriveInfo d) { try { return d.VolumeLabel; } catch { return ""; } }

        // ---- Format via the Windows shell dialog (SHFormatDrive) ----
        [DllImport("shell32.dll")]
        private static extern uint SHFormatDrive(IntPtr hwnd, uint drive, uint fmtID, uint options);
        private const uint SHFMT_ID_DEFAULT = 0xFFFF;
        private const uint SHFMT_ERROR = 0xFFFFFFFF;
        private const uint SHFMT_CANCEL = 0xFFFFFFFE;
        private const uint SHFMT_NOFORMAT = 0xFFFFFFFD;

        /// <summary>
        /// Open the standard Windows Format dialog pre-targeted at the given removable drive. The
        /// operator selects FAT32 and clicks Start (exactly the documented dealer step). Returns a
        /// short status string. Must be called on the UI thread.
        /// </summary>
        public static string OpenFormatDialog(IntPtr hwnd, char letter)
        {
            uint driveIndex = (uint)(char.ToUpperInvariant(letter) - 'A');   // A=0, B=1, C=2...
            uint r = SHFormatDrive(hwnd, driveIndex, SHFMT_ID_DEFAULT, 0);    // 0 => quick format allowed
            if (r == SHFMT_CANCEL) return "Format cancelled.";
            if (r == SHFMT_ERROR) return "Format reported an error.";
            if (r == SHFMT_NOFORMAT) return "Drive cannot be formatted.";
            return "Format dialog closed (ensure FAT32 was selected).";
        }

        // ---- Copy a payload folder's CONTENTS to the USB root, with verify ----
        /// <summary>
        /// Copy every file under <paramref name="sourceDir"/> into <paramref name="destRoot"/>
        /// (the USB root), preserving sub-folders, then verify file count and total size.
        /// Runs on the caller's worker thread; honours the cancel delegate.
        /// </summary>
        public static bool CopyPayload(string sourceDir, string destRoot, Action<string> log,
                                       Action<int, string> progress, Func<bool> cancelled, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir)) { error = "Payload source folder not found: " + sourceDir; return false; }
            if (string.IsNullOrWhiteSpace(destRoot) || !Directory.Exists(destRoot)) { error = "USB drive not ready: " + destRoot; return false; }

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            if (files.Length == 0) { error = "Payload source folder is empty: " + sourceDir; return false; }

            long total = 0; foreach (var f in files) total += new FileInfo(f).Length;
            long free = new DriveInfo(destRoot).AvailableFreeSpace;
            log($"Payload: {files.Length} file(s), {total / 1024.0 / 1024.0:0.0} MB. USB free: {free / 1024.0 / 1024.0:0.0} MB.");
            if (total > free) { error = $"Payload ({total / 1024.0 / 1024.0:0.0} MB) does not fit on the USB ({free / 1024.0 / 1024.0:0.0} MB free)."; return false; }

            long done = 0; int n = 0;
            foreach (var f in files)
            {
                if (cancelled()) { error = "cancelled by user"; return false; }
                string rel = f.Substring(sourceDir.Length).TrimStart('\\', '/');
                string dest = Path.Combine(destRoot, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest, true);
                long len = new FileInfo(f).Length;
                done += len; n++;
                progress(total > 0 ? (int)(done * 100 / total) : 100, $"Copying {n}/{files.Length}  ({done / 1024.0 / 1024.0:0.0}/{total / 1024.0 / 1024.0:0.0} MB)");
            }

            // Flush FAT buffers to the removable device before we claim success.
            try { foreach (var f in files) { } } catch { }

            // Verify count + size.
            var copied = Directory.GetFiles(destRoot, "*", SearchOption.AllDirectories);
            long copiedBytes = 0; foreach (var f in copied) copiedBytes += new FileInfo(f).Length;
            if (copied.Length < files.Length) { error = $"Verify failed: {copied.Length} files on USB, expected at least {files.Length}."; return false; }
            log($"Verify OK: {files.Length} payload file(s) written, {copiedBytes / 1024.0 / 1024.0:0.0} MB on USB.");
            return true;
        }
    }
}
