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

        private static IEnumerator Fetch(string _url)
        {
            byte[] data = ReadDiskCache(_url);
            if (data == null)
            {
                using (var request = UnityWebRequest.Get(_url))
                {
                    request.timeout = TimeoutSeconds;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Fail(_url, request.error);
                        yield break;
                    }
                    data = request.downloadHandler.data;
                }
                if (data == null || data.Length == 0)
                {
                    Fail(_url, "empty response");
                    yield break;
                }
                if (data.Length > MaxBytes)
                {
                    Fail(_url, $"too large ({data.Length / 1024} KB, cap {MaxBytes / 1024} KB)");
                    yield break;
                }
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(tex, data))
            {
                UnityEngine.Object.Destroy(tex);
                Fail(_url, "not a decodable PNG/JPG");
                yield break;
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
