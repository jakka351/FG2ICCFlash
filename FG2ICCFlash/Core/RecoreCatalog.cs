using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace FG2ICCFlasher.Core
{
    /// <summary>One selectable recore image in the catalog (a factory package-tree USB re-image).</summary>
    public sealed class RecoreEntry
    {
        public string Id = "";
        public string Name = "";
        public string File = "";          // zip file name, relative to the catalog location
        public string Body = "";
        public string Zone = "";
        public bool Nav;
        public string Engine = "";
        public string Date = "";
        public long Bytes;
        public int Files;
        public string Sha256 = "";
        public string Source = "";
        public List<string> Packages = new List<string>();

        // Runtime resolution (not from JSON):
        public string LocalZipPath;       // if set, the zip is already on disk (built-in / local catalog)
        public string RemoteBaseUrl;      // if set, download File from here (base URL, no trailing slash)

        public string SizeText => Bytes > 0 ? (Bytes / 1048576.0).ToString("0.0") + " MB" : "";

        public string Details()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Body)) parts.Add(Body);
            if (!string.IsNullOrEmpty(Engine)) parts.Add(Engine);
            if (!string.IsNullOrEmpty(Zone)) parts.Add(Zone + " zone");
            parts.Add(Nav ? "with nav" : "no nav");
            if (!string.IsNullOrEmpty(Date)) parts.Add(Date);
            string meta = string.Join(" · ", parts);
            string size = Bytes > 0 ? $"  —  {SizeText}, {Files} files" : "";
            string pkgs = Packages != null && Packages.Count > 0 ? $"\r\npackages: {string.Join(" ", Packages)}" : "";
            return meta + size + pkgs;
        }

        public override string ToString()
            => Bytes > 0 ? $"{Name}   ({SizeText})" : Name;
    }

    /// <summary>
    /// A catalog of recore images, loaded from a local folder (manifest.json + the zips) or a
    /// remote base URL (…/manifest.json + …/&lt;file&gt;.zip). The schema is "fg2icc-recore-catalog/1".
    /// </summary>
    public sealed class RecoreCatalog
    {
        public readonly List<RecoreEntry> Backups = new List<RecoreEntry>();
        public string Source = "";
        public string Note = "";

        private static object Get(Dictionary<string, object> d, string k)
            => (d != null && d.ContainsKey(k)) ? d[k] : null;
        private static string Str(Dictionary<string, object> d, string k)
            => Convert.ToString(Get(d, k)) ?? "";
        private static long Lng(Dictionary<string, object> d, string k)
            { try { var v = Get(d, k); return v == null ? 0L : Convert.ToInt64(v); } catch { return 0L; } }
        private static bool Bln(Dictionary<string, object> d, string k)
            { try { var v = Get(d, k); return v != null && Convert.ToBoolean(v); } catch { return false; } }

        private static RecoreCatalog FromJson(string json)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = ser.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("manifest.json is not a JSON object.");
            var cat = new RecoreCatalog { Note = Str(root, "note") };
            var arr = Get(root, "backups") as object[];
            if (arr == null) throw new InvalidDataException("manifest.json has no 'backups' array.");
            foreach (var o in arr)
            {
                var b = o as Dictionary<string, object>;
                if (b == null) continue;
                var e = new RecoreEntry
                {
                    Id = Str(b, "id"), Name = Str(b, "name"), File = Str(b, "file"),
                    Body = Str(b, "body"), Zone = Str(b, "zone"), Engine = Str(b, "engine"),
                    Date = Str(b, "date"), Nav = Bln(b, "nav"), Bytes = Lng(b, "bytes"),
                    Files = (int)Lng(b, "files"), Sha256 = Str(b, "sha256"), Source = Str(b, "source"),
                };
                var pk = Get(b, "packages") as object[];
                if (pk != null) e.Packages = pk.Select(x => Convert.ToString(x)).ToList();
                cat.Backups.Add(e);
            }
            return cat;
        }

        /// <summary>Load from a local folder that holds manifest.json alongside the zip files.</summary>
        public static RecoreCatalog LoadLocalFolder(string folder, Action<string> log)
        {
            string mf = Path.Combine(folder, "manifest.json");
            if (!System.IO.File.Exists(mf)) throw new FileNotFoundException("No manifest.json in " + folder);
            var cat = FromJson(System.IO.File.ReadAllText(mf));
            cat.Source = folder;
            foreach (var e in cat.Backups) e.LocalZipPath = Path.Combine(folder, e.File);
            log?.Invoke($"Recore catalog (local): {cat.Backups.Count} image(s) from {folder}");
            return cat;
        }

        /// <summary>Load from a remote base URL (…/manifest.json). Zips are downloaded on demand.</summary>
        public static RecoreCatalog LoadRemote(string baseUrl, Action<string> log)
        {
            baseUrl = baseUrl.TrimEnd('/');
            string url = baseUrl + "/manifest.json";
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "FG2ICCFlash";
                string json = wc.DownloadString(url);
                var cat = FromJson(json);
                cat.Source = baseUrl;
                foreach (var e in cat.Backups) e.RemoteBaseUrl = baseUrl;
                log?.Invoke($"Recore catalog (remote): {cat.Backups.Count} image(s) from {baseUrl}");
                return cat;
            }
        }

        /// <summary>Load whichever location applies: an http(s) URL → remote, else a local folder.</summary>
        public static RecoreCatalog Load(string location, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("No catalog location given.");
            return location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   location.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? LoadRemote(location, log)
                : LoadLocalFolder(location, log);
        }

        /// <summary>Default per-user cache directory for downloaded recore zips.</summary>
        public static string CacheDir
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FG2ICCFlash", "recore-cache");

        /// <summary>
        /// Resolve an entry to a local zip path: a built-in / local-folder zip is used in place;
        /// a remote entry is downloaded to the cache (reused if a cached copy matches its sha256)
        /// and verified. Returns null on failure (err is set).
        /// </summary>
        public static string EnsureZip(RecoreEntry e, Action<string> log, Action<int> progress, Func<bool> abort, out string err)
        {
            err = null;
            try
            {
                if (!string.IsNullOrEmpty(e.LocalZipPath))
                {
                    if (!System.IO.File.Exists(e.LocalZipPath)) { err = "Zip not found: " + e.LocalZipPath; return null; }
                    if (!string.IsNullOrEmpty(e.Sha256) && !VerifySha(e.LocalZipPath, e.Sha256, log))
                    { err = "SHA-256 mismatch for " + Path.GetFileName(e.LocalZipPath); return null; }
                    return e.LocalZipPath;
                }
                if (!string.IsNullOrEmpty(e.RemoteBaseUrl))
                {
                    Directory.CreateDirectory(CacheDir);
                    string dest = Path.Combine(CacheDir, e.File);
                    if (System.IO.File.Exists(dest) && !string.IsNullOrEmpty(e.Sha256) && VerifySha(dest, e.Sha256, null))
                    { log?.Invoke("Using cached " + e.File); return dest; }
                    string url = e.RemoteBaseUrl.TrimEnd('/') + "/" + e.File;
                    log?.Invoke($"Downloading {e.File} ({e.SizeText}) …");
                    if (!Download(url, dest, e.Bytes, progress, abort, out err)) return null;
                    if (!string.IsNullOrEmpty(e.Sha256) && !VerifySha(dest, e.Sha256, log))
                    { err = "Downloaded file failed SHA-256 verification."; try { System.IO.File.Delete(dest); } catch { } return null; }
                    log?.Invoke("Download verified: " + e.File);
                    return dest;
                }
                err = "Entry has no local path or remote URL.";
                return null;
            }
            catch (Exception ex) { err = ex.Message; return null; }
        }

        private static bool Download(string url, string dest, long expected, Action<int> progress, Func<bool> abort, out string err)
        {
            err = null;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "FG2ICCFlash";
            req.Timeout = 30000; req.ReadWriteTimeout = 120000;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rs = resp.GetResponseStream())
                using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long total = resp.ContentLength > 0 ? resp.ContentLength : expected;
                    var buf = new byte[1 << 16];
                    long got = 0; int n;
                    while ((n = rs.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (abort != null && abort()) { err = "Download aborted."; return false; }
                        fs.Write(buf, 0, n); got += n;
                        if (total > 0) progress?.Invoke((int)Math.Min(100, got * 100 / total));
                    }
                }
                return true;
            }
            catch (Exception ex) { err = "Download failed: " + ex.Message; try { System.IO.File.Delete(dest); } catch { } return false; }
        }

        private static bool VerifySha(string path, string expectedHex, Action<string> log)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var fs = System.IO.File.OpenRead(path))
                {
                    var hash = sha.ComputeHash(fs);
                    string hex = string.Concat(hash.Select(b => b.ToString("x2")));
                    bool ok = string.Equals(hex, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
                    if (!ok) log?.Invoke($"  SHA-256 mismatch: got {hex}, expected {expectedHex}");
                    return ok;
                }
            }
            catch (Exception ex) { log?.Invoke("  SHA-256 error: " + ex.Message); return false; }
        }
    }
}
