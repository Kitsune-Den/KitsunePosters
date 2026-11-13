using System;
using System.Collections.Generic;
using System.IO;

namespace KitsunePosters
{
    /// <summary>
    /// Posters from image files on the player's own machine, no hosting needed.
    /// The sign text holds a bare file name ("cat.png"); the file is looked up
    /// in the player's Images folder, then in the mod's own Images folder (so a
    /// server pack can ship pictures with the mod).
    ///
    /// A name with a hash after it ("cat.png@3f9a1c2b7d10") is a picture a
    /// player shared: everyone gets the same bytes from the server (see
    /// PosterShare), whatever files their own machine has.
    /// </summary>
    public static class PosterLocal
    {
        /// <summary>Internal key prefix: "local:cat.png". Never a real URL scheme we download.</summary>
        public const string Prefix = "local:";

        /// <summary>Internal key prefix for a shared picture: "shared:3f9a1c2b7d10".</summary>
        public const string SharedPrefix = "shared:";

        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

        private static string modImagesDir;
        private static string userImagesDir;

        public static void Init(Mod _modInstance)
        {
            try { modImagesDir = Path.Combine(_modInstance.Path, "Images"); }
            catch { modImagesDir = null; }
            if (GameManager.IsDedicatedServer)
            {
                return; // nothing renders there
            }
            try { Directory.CreateDirectory(UserImagesDir()); }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Could not create the Images folder: {e.Message}"); }
        }

        /// <summary>The player's own folder: survives mod updates.</summary>
        public static string UserImagesDir()
        {
            if (userImagesDir != null)
            {
                return userImagesDir;
            }
            string baseDir;
            try { baseDir = GameIO.GetUserGameDataDir(); }
            catch { baseDir = UnityEngine.Application.persistentDataPath; }
            userImagesDir = Path.Combine(baseDir, "KitsunePosters", "Images");
            return userImagesDir;
        }

        public static string ModImagesDir()
        {
            return modImagesDir;
        }

        public static bool IsLocal(string _key)
        {
            return _key != null && _key.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static bool IsShared(string _key)
        {
            return _key != null && _key.StartsWith(SharedPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// "cat.png", "local:cat.png" or "file:cat.png" to the key
        /// "local:cat.png", "cat.png@3f9a1c2b7d10" to "shared:3f9a1c2b7d10";
        /// null for anything that isn't a plain image file name. Paths are
        /// refused: a sign must never name a file outside the Images folders.
        /// </summary>
        public static string KeyFromToken(string _token)
        {
            if (string.IsNullOrEmpty(_token))
            {
                return null;
            }
            string name = _token.Trim();
            string hash = null;
            int at = name.LastIndexOf('@');
            if (at >= 0 && PosterShare.IsHash(name.Substring(at + 1).ToLowerInvariant()))
            {
                hash = name.Substring(at + 1).ToLowerInvariant();
                name = name.Substring(0, at);
            }
            string lower = name.ToLowerInvariant();
            if (lower.StartsWith("local:"))
            {
                name = name.Substring(6);
            }
            else if (lower.StartsWith("file:"))
            {
                name = name.Substring(5);
            }
            if (!IsPlainImageName(name))
            {
                return null;
            }
            if (hash != null)
            {
                return SharedPrefix + hash; // the same picture tiles whatever it was called
            }
            // Lower case, so "Cat.PNG" and "cat.png" on neighbouring posters tile.
            return Prefix + name.ToLowerInvariant();
        }

        private static bool IsPlainImageName(string _name)
        {
            if (_name.Length < 5 || _name.Length > 120 || _name[0] == '.')
            {
                return false;
            }
            foreach (char c in _name)
            {
                if (c == '/' || c == '\\' || c == ':' || c < ' ')
                {
                    return false;
                }
            }
            if (_name.Contains(".."))
            {
                return false;
            }
            string lower = _name.ToLowerInvariant();
            foreach (var ext in Extensions)
            {
                if (lower.EndsWith(ext, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Full path of the file a key names, or null when no folder has it.</summary>
        public static string Resolve(string _key)
        {
            if (!IsLocal(_key))
            {
                return null;
            }
            string name = _key.Substring(Prefix.Length);
            if (!IsPlainImageName(name))
            {
                return null;
            }
            return FindIn(UserImagesDir(), name) ?? FindIn(modImagesDir, name);
        }

        /// <summary>The file of that name in the player's own folder (not the mod's), or null.</summary>
        public static string FindInUserFolder(string _name)
        {
            return IsPlainImageName(_name) ? FindIn(UserImagesDir(), _name) : null;
        }

        // Matched by name, ignoring case, so the same sign text works on Linux
        // clients; only files directly in the folder are ever considered.
        private static string FindIn(string _dir, string _name)
        {
            try
            {
                if (string.IsNullOrEmpty(_dir) || !Directory.Exists(_dir))
                {
                    return null;
                }
                foreach (var path in Directory.GetFiles(_dir))
                {
                    if (string.Equals(Path.GetFileName(path), _name, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Image file names available on this machine, user folder first.</summary>
        public static List<string> List()
        {
            var names = new List<string>();
            foreach (var dir in new[] { UserImagesDir(), modImagesDir })
            {
                try
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    {
                        continue;
                    }
                    foreach (var path in Directory.GetFiles(dir))
                    {
                        string name = Path.GetFileName(path);
                        if (IsPlainImageName(name) && !names.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            names.Add(name);
                        }
                    }
                }
                catch { }
            }
            return names;
        }
    }
}
