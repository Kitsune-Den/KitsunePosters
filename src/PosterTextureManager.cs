using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KitsunePosters
{
    /// <summary>
    /// Downloads poster images, with a session memory cache and a disk cache.
    /// All entry points run on the main thread (callers are Harmony postfixes
    /// on main-thread game code); downloads run as coroutines on GameManager.
    /// </summary>
    public static class PosterTextureManager
    {
        public const int MaxBytes = 5 * 1024 * 1024;
        public const int MaxDimension = 4096;
        public const int TimeoutSeconds = 15;

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, List<Action<Texture2D>>> pending = new Dictionary<string, List<Action<Texture2D>>>();
        private static readonly HashSet<string> failed = new HashSet<string>();

        /// <summary>Forget this session's failed URLs so they get another try.</summary>
        public static void ClearFailures()
        {
            failed.Clear();
        }

        public static void Get(string _url, Action<Texture2D> _callback)
        {
            if (cache.TryGetValue(_url, out var tex))
            {
                _callback(tex);
                return;
            }
            if (failed.Contains(_url))
            {
                _callback(null);
                return;
            }
            if (pending.TryGetValue(_url, out var waiters))
            {
                waiters.Add(_callback);
                return;
            }
            pending[_url] = new List<Action<Texture2D>> { _callback };
            GameManager.Instance.StartCoroutine(Fetch(_url));
        }

        private class Download
        {
            public byte[] Data;
            public string Error;
        }

        private static IEnumerator DownloadBytes(string _url, Download _result)
        {
            using (var request = UnityWebRequest.Get(_url))
            {
                request.timeout = TimeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _result.Error = request.error;
                    yield break;
                }
                _result.Data = request.downloadHandler.data;
            }
            if (_result.Data == null || _result.Data.Length == 0)
            {
                _result.Error = "empty response";
            }
            else if (_result.Data.Length > MaxBytes)
            {
                _result.Error = $"too large ({_result.Data.Length / 1024} KB, cap {MaxBytes / 1024} KB)";
                _result.Data = null;
            }
        }

        private static IEnumerator Fetch(string _url)
        {
            byte[] data = ReadDiskCache(_url);
            if (data == null)
            {
                var dl = new Download();
                yield return DownloadBytes(_url, dl);
                if (dl.Data == null)
                {
                    Fail(_url, dl.Error);
                    yield break;
                }
                data = dl.Data;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(tex, data))
            {
                // A page instead of an image (an imgur gallery, say): pages name
                // their picture in an og:image meta tag. Resolve it, once.
                string direct = LooksLikeHtml(data) ? ExtractPageImage(data) : null;
                if (direct == null || direct == _url)
                {
                    UnityEngine.Object.Destroy(tex);
                    Fail(_url, "not a decodable PNG/JPG");
                    yield break;
                }
                if (!PosterSettings.IsDomainAllowed(direct))
                {
                    UnityEngine.Object.Destroy(tex);
                    Fail(_url, $"page image is on a disallowed domain ({direct})");
                    yield break;
                }
                Log.Out($"[KitsunePosters] Page link resolved to {direct}");
                var dl2 = new Download();
                yield return DownloadBytes(direct, dl2);
                if (dl2.Data == null || !ImageConversion.LoadImage(tex, dl2.Data))
                {
                    UnityEngine.Object.Destroy(tex);
                    Fail(_url, dl2.Error ?? "page image not decodable");
                    yield break;
                }
                data = dl2.Data; // cached under the ORIGINAL url, so reloads skip the page hop
            }
            if (tex.width > MaxDimension || tex.height > MaxDimension)
            {
                UnityEngine.Object.Destroy(tex);
                Fail(_url, $"dimensions over {MaxDimension}px");
                yield break;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;

            WriteDiskCache(_url, data);
            cache[_url] = tex;
            Log.Out($"[KitsunePosters] Loaded {tex.width}x{tex.height} poster from {_url}");
            Flush(_url, tex);
        }

        private static void Fail(string _url, string _reason)
        {
            Log.Warning($"[KitsunePosters] Poster rejected ({_reason}): {_url}");
            failed.Add(_url);
            Flush(_url, null);
        }

        private static void Flush(string _url, Texture2D _tex)
        {
            if (!pending.TryGetValue(_url, out var waiters))
            {
                return;
            }
            pending.Remove(_url);
            foreach (var cb in waiters)
            {
                try { cb(_tex); }
                catch (Exception e) { Log.Warning($"[KitsunePosters] Poster callback failed: {e.Message}"); }
            }
        }

        // --- page-link resolution ------------------------------------------

        private static bool LooksLikeHtml(byte[] _data)
        {
            int n = System.Math.Min(_data.Length, 1024);
            string head = Encoding.UTF8.GetString(_data, 0, n);
            int i = 0;
            while (i < head.Length && char.IsWhiteSpace(head[i]))
            {
                i++;
            }
            if (i < head.Length && head[i] == '<')
            {
                return true;
            }
            var lower = head.ToLowerInvariant();
            return lower.Contains("<!doctype") || lower.Contains("<html");
        }

        /// <summary>og:image (or twitter:image) URL from an HTML page, or null.</summary>
        private static string ExtractPageImage(byte[] _data)
        {
            string html = Encoding.UTF8.GetString(_data);
            foreach (var key in new[] { "og:image", "twitter:image" })
            {
                // content= can come before or after the property/name attribute.
                var match = System.Text.RegularExpressions.Regex.Match(html,
                    $@"<meta\b[^>]*?(?:property|name)\s*=\s*[""']{key}[""'][^>]*?content\s*=\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    match = System.Text.RegularExpressions.Regex.Match(html,
                        $@"<meta\b[^>]*?content\s*=\s*[""']([^""']+)[""'][^>]*?(?:property|name)\s*=\s*[""']{key}[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                if (!match.Success)
                {
                    continue;
                }
                string url = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    continue;
                }
                // imgur's og:image carries ?fb, a 600x315 crop: take the real file.
                try
                {
                    var uri = new System.Uri(url);
                    if (uri.Host.ToLowerInvariant().EndsWith("i.imgur.com") && !string.IsNullOrEmpty(uri.Query))
                    {
                        url = url.Substring(0, url.IndexOf('?'));
                    }
                }
                catch { }
                return url;
            }
            return null;
        }

        // --- disk cache ---------------------------------------------------

        private static string cacheDir;

        private static string CacheDir()
        {
            if (cacheDir != null)
            {
                return cacheDir;
            }
            string baseDir;
            try { baseDir = GameIO.GetUserGameDataDir(); }
            catch { baseDir = Application.persistentDataPath; }
            cacheDir = Path.Combine(baseDir, "KitsunePosters");
            Directory.CreateDirectory(cacheDir);
            return cacheDir;
        }

        private static string CachePath(string _url)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(_url));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return Path.Combine(CacheDir(), sb.ToString() + ".img");
            }
        }

        private static byte[] ReadDiskCache(string _url)
        {
            try
            {
                var path = CachePath(_url);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch { return null; }
        }

        private static void WriteDiskCache(string _url, byte[] _data)
        {
            try { File.WriteAllBytes(CachePath(_url), _data); }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Disk cache write failed: {e.Message}"); }
        }
    }
}
