using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace KitsunePosters
{
    /// <summary>
    /// Settings.xml (per install, read on both sides) plus the client's own
    /// on/off preference, persisted across sessions in the user data dir.
    /// </summary>
    public static class PosterSettings
    {
        /// <summary>Host suffixes posters may load from; "*" allows every domain.</summary>
        public static readonly List<string> AllowedDomains = new List<string> { "*" };

        private static bool showRemoteImages = true;
        private static string modPath;

        public static bool ShowRemoteImages => showRemoteImages;

        public static void Load(Mod _mod)
        {
            modPath = _mod.Path;
            LoadSettingsXml();
            LoadClientPref();
            Log.Out($"[KitsunePosters] AllowedDomains: {string.Join(", ", AllowedDomains)}; ShowRemoteImages: {showRemoteImages}");
        }

        public static void Reload()
        {
            LoadSettingsXml();
            Log.Out($"[KitsunePosters] Settings reloaded. AllowedDomains: {string.Join(", ", AllowedDomains)}");
        }

        private static void LoadSettingsXml()
        {
            try
            {
                var path = Path.Combine(modPath, "Settings.xml");
                if (!File.Exists(path))
                {
                    return; // defaults stand
                }
                var doc = new XmlDocument();
                doc.Load(path);

                var domains = doc.SelectSingleNode("//property[@name='AllowedDomains']")?.Attributes?["value"]?.Value;
                if (!string.IsNullOrEmpty(domains))
                {
                    AllowedDomains.Clear();
                    foreach (var d in domains.Split(','))
                    {
                        var trimmed = d.Trim().ToLowerInvariant();
                        if (trimmed.Length > 0)
                        {
                            AllowedDomains.Add(trimmed);
                        }
                    }
                }

                var show = doc.SelectSingleNode("//property[@name='ShowRemoteImages']")?.Attributes?["value"]?.Value;
                if (show != null)
                {
                    showRemoteImages = !show.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[KitsunePosters] Settings.xml not readable ({e.Message}); using defaults.");
            }
        }

        /// <summary>True when the URL's host is covered by the allowlist.</summary>
        public static bool IsDomainAllowed(string _url)
        {
            Uri uri;
            try { uri = new Uri(_url); }
            catch { return false; }
            var host = uri.Host.ToLowerInvariant();
            if (host.Length == 0)
            {
                return false;
            }
            foreach (var entry in AllowedDomains)
            {
                if (entry == "*" || host == entry || host.EndsWith("." + entry, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // --- the client's own on/off switch --------------------------------

        public static void SetShowRemoteImages(bool _on)
        {
            showRemoteImages = _on;
            try
            {
                var dir = PrefDir();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "prefs.txt"), _on ? "images=on" : "images=off");
            }
            catch (Exception e)
            {
                Log.Warning($"[KitsunePosters] Could not save preference: {e.Message}");
            }
        }

        private static void LoadClientPref()
        {
            try
            {
                var path = Path.Combine(PrefDir(), "prefs.txt");
                if (File.Exists(path))
                {
                    showRemoteImages = !File.ReadAllText(path).Contains("images=off");
                }
            }
            catch { /* defaults stand */ }
        }

        private static string PrefDir()
        {
            string baseDir;
            try { baseDir = GameIO.GetUserGameDataDir(); }
            catch { baseDir = Application.persistentDataPath; }
            return Path.Combine(baseDir, "KitsunePosters");
        }
    }
}
