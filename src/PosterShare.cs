using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitsunePosters
{
    /// <summary>
    /// Shares a player's own image files with everyone on the server.
    ///
    /// When the sign window saves a poster whose text names a file in the
    /// player's Images folder ("cat.png"), the client shrinks the picture
    /// (2048px at most; JPG, or PNG when it has transparency), names the
    /// result by its SHA-256 and writes "cat.png@3f9a1c2b7d10" into the sign
    /// text instead. The bytes go to the server, which keeps them with the
    /// world save; other players fetch them from the server once and keep a
    /// copy. The hash is checked at every hop, so nobody can swap a different
    /// picture in under someone else's hash.
    ///
    /// Files in the mod's own Images folder (a server pack) are left as bare
    /// names: every player already has them.
    /// </summary>
    public static class PosterShare
    {
        public const int HashLength = 12;
        public const int MaxSide = 2048;
        private const int SmallSide = 1024;
        public const int MaxBytes = 4 * 1024 * 1024;
        // Some of the game's connections stage packages in 32 KB; half that
        // leaves room for the header on every one of them.
        public const int ChunkSize = 16 * 1024;
        private const int ChunksPerTick = 2;        // every 0.05 s: about 640 KB/s
        private const float TickSeconds = 0.05f;
        private const float WaitSeconds = 90f;      // for an upload that hasn't started yet
        private const float StallSeconds = 30f;     // between pieces of one that has
        private const int MaxUploadsPerPlayer = 4;  // at once

        /// <summary>Set while the sign window saves: the only time a bare file name is shared.</summary>
        public static bool LocalEdit;

        private static ConnectionManager Cm => SingletonMonoBehaviour<ConnectionManager>.Instance;

        private static bool IsServer => Cm != null && Cm.IsServer;

        private static bool Remote => PosterNet.Remote;

        public static bool IsHash(string _s)
        {
            if (_s == null || _s.Length != HashLength)
            {
                return false;
            }
            foreach (char c in _s)
            {
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                {
                    return false;
                }
            }
            return true;
        }

        public static string HashOf(byte[] _data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(_data);
                var sb = new StringBuilder(HashLength);
                for (int i = 0; sb.Length < HashLength; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString(0, HashLength);
            }
        }

        private static bool IsImage(byte[] _data)
        {
            if (_data == null || _data.Length < 8)
            {
                return false;
            }
            bool png = _data[0] == 0x89 && _data[1] == 0x50 && _data[2] == 0x4E && _data[3] == 0x47;
            bool jpg = _data[0] == 0xFF && _data[1] == 0xD8 && _data[2] == 0xFF;
            return png || jpg;
        }

        // --- stamping: "cat.png" becomes "cat.png@<hash>" -------------------

        /// <summary>
        /// Shares every bare file name in the text that comes from the
        /// player's own Images folder, and returns the text with their hashes
        /// added. Anything else is returned unchanged.
        /// </summary>
        public static string Stamp(string _text)
        {
            if (string.IsNullOrEmpty(_text))
            {
                return _text;
            }
            return System.Text.RegularExpressions.Regex.Replace(_text, @"\S+", m => StampToken(m.Value));
        }

        private static string StampToken(string _token)
        {
            if (_token.IndexOf('@') >= 0)
            {
                OfferToken(_token); // already shared; make sure the server has it
                return _token;
            }
            string key = PosterLocal.KeyFromToken(_token);
            if (key == null)
            {
                return _token;
            }
            string name = key.Substring(PosterLocal.Prefix.Length);
            string path = PosterLocal.FindInUserFolder(name);
            if (path == null)
            {
                return _token; // a server-pack file, or no such file
            }
            if (!Prepare(path, out byte[] data, out string error))
            {
                Log.Warning($"[KitsunePosters] Not sharing {name}: {error}. Only this PC shows it.");
                Tell($"{name} can't be shared ({error}). Only you see it.");
                return _token;
            }
            string hash = HashOf(data);
            WriteCached(hash, data);
            string shownName = _token.Substring(_token.Length - name.Length); // the player's spelling, no prefix
            if (IsServer)
            {
                StoreAdd(hash, data, LocalPlayerName(), LocalPlayerId(), shownName);
            }
            else
            {
                Offer(hash, data.Length, shownName);
            }
            Log.Out($"[KitsunePosters] Shared {path} as {shownName}@{hash} ({data.Length / 1024} KB).");
            return shownName + "@" + hash;
        }

        private static void OfferToken(string _token)
        {
            string key = PosterLocal.KeyFromToken(_token);
            if (!PosterLocal.IsShared(key) || !Remote)
            {
                return;
            }
            string hash = key.Substring(PosterLocal.SharedPrefix.Length);
            byte[] data = ReadCached(hash);
            if (data != null)
            {
                Offer(hash, data.Length, _token.Substring(0, _token.LastIndexOf('@')));
            }
        }

        /// <summary>The picture as it will be shared: at most 2048px (1024px when that's still over 4 MB), JPG or PNG.</summary>
        private static bool Prepare(string _path, out byte[] _data, out string _error)
        {
            _data = null;
            byte[] raw;
            try
            {
                long size = new FileInfo(_path).Length;
                if (size > PosterTextureManager.MaxLocalBytes)
                {
                    _error = $"over {PosterTextureManager.MaxLocalBytes / (1024 * 1024)} MB";
                    return false;
                }
                raw = File.ReadAllBytes(_path);
            }
            catch (Exception e)
            {
                _error = "unreadable: " + e.Message;
                return false;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, raw))
                {
                    _error = "not a PNG or JPG the game can read";
                    return false;
                }
                foreach (int side in new[] { MaxSide, SmallSide })
                {
                    bool shrink = tex.width > side || tex.height > side;
                    Texture2D work = shrink ? PosterTextureManager.Downscale(tex, side) : tex;
                    if (work == null)
                    {
                        _error = "couldn't be scaled down";
                        return false;
                    }
                    try
                    {
                        // Transparency only matters on graffiti, and only a
                        // PNG keeps it; everything else travels as a JPG.
                        _data = HasTransparency(work) ? work.EncodeToPNG() : work.EncodeToJPG(90);
                    }
                    finally
                    {
                        if (shrink)
                        {
                            UnityEngine.Object.Destroy(work);
                        }
                    }
                    if (_data != null && _data.Length <= MaxBytes)
                    {
                        _error = null;
                        return true;
                    }
                }
                _error = $"still over {MaxBytes / (1024 * 1024)} MB at {SmallSide}px";
                _data = null;
                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static bool HasTransparency(Texture2D _tex)
        {
            if (_tex.format != TextureFormat.RGBA32 && _tex.format != TextureFormat.ARGB32)
            {
                return false;
            }
            foreach (var px in _tex.GetPixels32())
            {
                if (px.a < 250)
                {
                    return true;
                }
            }
            return false;
        }

        // --- this PC's copies -------------------------------------------------

        private static string cacheDir;

        private static string CacheDir()
        {
            if (cacheDir == null)
            {
                string baseDir;
                try { baseDir = GameIO.GetUserGameDataDir(); }
                catch { baseDir = Application.persistentDataPath; }
                cacheDir = Path.Combine(baseDir, "KitsunePosters", "Shared");
            }
            return cacheDir;
        }

        private static byte[] ReadCached(string _hash)
        {
            try
            {
                string path = Path.Combine(CacheDir(), _hash + ".img");
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch { return null; }
        }

        private static void WriteCached(string _hash, byte[] _data)
        {
            if (GameManager.IsDedicatedServer)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(CacheDir());
                File.WriteAllBytes(Path.Combine(CacheDir(), _hash + ".img"), _data);
            }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Couldn't keep a copy of a shared image: {e.Message}"); }
        }

        private static void DeleteCached(string _hash)
        {
            try { File.Delete(Path.Combine(CacheDir(), _hash + ".img")); }
            catch { }
        }

        // --- the server's store, kept with the world save --------------------

        public class Entry
        {
            public string Hash;
            public long Bytes;
            public string When;
            public string Who;
            public string WhoId;
            public string Name;
        }

        private static string storeDir;
        private static readonly List<Entry> index = new List<Entry>();
        private static readonly HashSet<string> blocked = new HashSet<string>();

        private static string StoreDir()
        {
            string dir;
            try { dir = Path.Combine(GameIO.GetSaveGameDir(), "KitsunePosters"); }
            catch { return null; }
            if (dir != storeDir)
            {
                storeDir = dir;
                LoadIndex();
            }
            return storeDir;
        }

        private static void LoadIndex()
        {
            index.Clear();
            blocked.Clear();
            try
            {
                string path = Path.Combine(storeDir, "index.txt");
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var f = line.Split('\t');
                        if (f.Length >= 6 && IsHash(f[0]) && File.Exists(Path.Combine(storeDir, f[0] + ".img")))
                        {
                            long.TryParse(f[1], out long bytes);
                            index.Add(new Entry { Hash = f[0], Bytes = bytes, When = f[2], Who = f[3], WhoId = f[4], Name = f[5] });
                        }
                    }
                }
                string blockPath = Path.Combine(storeDir, "removed.txt");
                if (File.Exists(blockPath))
                {
                    foreach (var line in File.ReadAllLines(blockPath))
                    {
                        if (IsHash(line.Trim()))
                        {
                            blocked.Add(line.Trim());
                        }
                    }
                }
            }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Shared image list not readable: {e.Message}"); }
        }

        private static void SaveIndex()
        {
            try
            {
                Directory.CreateDirectory(storeDir);
                var lines = new List<string>
                {
                    "# Images players shared on posters: hash, bytes, when (UTC), who, their id, file name. kpadmin images lists them."
                };
                foreach (var e in index)
                {
                    lines.Add(string.Join("\t", e.Hash, e.Bytes.ToString(), e.When, Clean(e.Who), Clean(e.WhoId), Clean(e.Name)));
                }
                File.WriteAllLines(Path.Combine(storeDir, "index.txt"), lines);
                File.WriteAllLines(Path.Combine(storeDir, "removed.txt"), blocked);
            }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Shared image list not writable: {e.Message}"); }
        }

        private static string Clean(string _s)
        {
            return (_s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        public static List<Entry> Entries()
        {
            StoreDir();
            return new List<Entry>(index);
        }

        public static long StoreUsed()
        {
            StoreDir();
            long used = 0;
            foreach (var e in index)
            {
                used += e.Bytes;
            }
            return used;
        }

        private static byte[] ReadStore(string _hash)
        {
            string dir = StoreDir();
            if (dir == null || blocked.Contains(_hash))
            {
                return null;
            }
            try
            {
                string path = Path.Combine(dir, _hash + ".img");
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch { return null; }
        }

        private static bool StoreHas(string _hash)
        {
            string dir = StoreDir();
            return dir != null && index.Exists(e => e.Hash == _hash);
        }

        private static void StoreAdd(string _hash, byte[] _data, string _who, string _whoId, string _name)
        {
            string dir = StoreDir();
            if (dir == null || blocked.Contains(_hash))
            {
                return;
            }
            if (!index.Exists(e => e.Hash == _hash))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, _hash + ".img"), _data);
                }
                catch (Exception e)
                {
                    Log.Warning($"[KitsunePosters] Couldn't store shared image {_hash}: {e.Message}");
                    return;
                }
                index.Add(new Entry
                {
                    Hash = _hash,
                    Bytes = _data.Length,
                    When = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                    Who = _who,
                    WhoId = _whoId,
                    Name = _name,
                });
                SaveIndex();
                Log.Out($"[KitsunePosters] Stored shared image {_name}@{_hash} ({_data.Length / 1024} KB) from {_who}.");
            }
            // Players who asked while it was on its way, and anyone whose
            // poster gave up waiting, can have it now.
            if (waiters.TryGetValue(_hash, out var list))
            {
                waiters.Remove(_hash);
                foreach (var ci in list)
                {
                    Queue(ci, _hash, _data);
                }
            }
            Broadcast(NetPackageKitsunePosterImage.Kind.Ready, _hash);
            PosterTextureManager.Retry(PosterLocal.SharedPrefix + _hash);
        }

        /// <summary>Removes a stored image for good: deleted, refused if offered again, and taken off every poster.</summary>
        public static bool StoreRemove(string _hash)
        {
            string dir = StoreDir();
            if (dir == null)
            {
                return false;
            }
            bool had = index.RemoveAll(e => e.Hash == _hash) > 0;
            blocked.Add(_hash);
            try { File.Delete(Path.Combine(dir, _hash + ".img")); }
            catch { }
            SaveIndex();
            uploads.Remove(_hash);
            Broadcast(NetPackageKitsunePosterImage.Kind.Revoke, _hash);
            Revoked(_hash);
            return had;
        }

        // --- fetching a shared image for a poster ----------------------------

        public class Result
        {
            public byte[] Data;
            public string Error;
        }

        private class Wait
        {
            public float Since;
            public byte[] Data;
            public string Error;
        }

        private static readonly Dictionary<string, Wait> waits = new Dictionary<string, Wait>();

        /// <summary>The bytes of a shared image: this PC's copy, the store (when this PC is the server), else the server's.</summary>
        public static IEnumerator Fetch(string _hash, Result _result)
        {
            byte[] data = ReadCached(_hash);
            if (data == null && IsServer)
            {
                data = ReadStore(_hash);
            }
            if (data != null)
            {
                _result.Data = data;
                yield break;
            }
            if (!Remote)
            {
                StoreDir();
                _result.Error = blocked.Contains(_hash) ? "an admin removed it" : "nobody shared it in this world";
                yield break;
            }
            var wait = new Wait { Since = Time.realtimeSinceStartup };
            waits[_hash] = wait;
            Cm.SendToServer(Get().Setup(NetPackageKitsunePosterImage.Kind.Get, _hash));
            while (wait.Data == null && wait.Error == null)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                float last = downloads.TryGetValue(_hash, out var piece) ? piece.Last : wait.Since;
                float limit = piece != null ? StallSeconds : WaitSeconds;
                if (Time.realtimeSinceStartup - last > limit)
                {
                    wait.Error = "the server didn't send it in time";
                }
            }
            waits.Remove(_hash);
            downloads.Remove(_hash);
            _result.Data = wait.Data;
            _result.Error = wait.Error;
        }

        // --- pieces in flight ---------------------------------------------------

        private class Incoming
        {
            public byte[] Data;
            public bool[] Got;
            public int Left;
            public float Last;
            public ClientInfo From;
            public string Name;
        }

        private class Outgoing
        {
            public ClientInfo To; // null: to the server
            public string Hash;
            public byte[] Data;
            public int Next;
        }

        private static readonly Dictionary<string, Incoming> uploads = new Dictionary<string, Incoming>();   // server side
        private static readonly Dictionary<string, Incoming> downloads = new Dictionary<string, Incoming>(); // client side
        private static readonly Dictionary<string, List<ClientInfo>> waiters = new Dictionary<string, List<ClientInfo>>();
        private static readonly HashSet<string> myOffers = new HashSet<string>();
        private static readonly List<Outgoing> outgoing = new List<Outgoing>();
        private static bool pumping;
        private static int turn;

        private static NetPackageKitsunePosterImage Get()
        {
            return NetPackageManager.GetPackage<NetPackageKitsunePosterImage>();
        }

        /// <summary>Tells every connected player (none in single player).</summary>
        private static void Broadcast(NetPackageKitsunePosterImage.Kind _kind, string _hash)
        {
            if (Cm != null && Cm.IsServer && Cm.Clients != null && Cm.Clients.Count > 0)
            {
                Cm.SendPackage(Get().Setup(_kind, _hash));
            }
        }

        private static void Offer(string _hash, int _size, string _name)
        {
            if (!Remote)
            {
                return;
            }
            myOffers.Add(_hash);
            Cm.SendToServer(Get().Setup(NetPackageKitsunePosterImage.Kind.Offer, _hash, _size, _name));
        }

        private static void Queue(ClientInfo _to, string _hash, byte[] _data)
        {
            if (outgoing.Exists(o => o.To == _to && o.Hash == _hash))
            {
                return;
            }
            outgoing.Add(new Outgoing { To = _to, Hash = _hash, Data = _data });
            if (!pumping)
            {
                pumping = true;
                GameManager.Instance.StartCoroutine(Pump());
            }
        }

        /// <summary>Sends queued pieces a couple at a time, so a big picture never crowds out the game's own traffic.</summary>
        private static IEnumerator Pump()
        {
            try
            {
                while (outgoing.Count > 0)
                {
                    for (int n = 0; n < ChunksPerTick && outgoing.Count > 0; n++)
                    {
                        turn = (turn + 1) % outgoing.Count;
                        var o = outgoing[turn];
                        bool gone = o.To == null
                            ? Cm == null || !Cm.IsClient || Cm.IsServer
                            : Cm == null || !Cm.Clients.Contains(o.To) || o.To.disconnecting;
                        if (gone)
                        {
                            outgoing.RemoveAt(turn);
                            continue;
                        }
                        int total = (o.Data.Length + ChunkSize - 1) / ChunkSize;
                        int start = o.Next * ChunkSize;
                        var piece = new byte[Math.Min(ChunkSize, o.Data.Length - start)];
                        Buffer.BlockCopy(o.Data, start, piece, 0, piece.Length);
                        var package = Get().Chunk(o.Hash, o.Next, total, o.Data.Length, piece);
                        if (o.To == null)
                        {
                            Cm.SendToServer(package);
                        }
                        else
                        {
                            o.To.SendPackage(package);
                        }
                        if (++o.Next >= total)
                        {
                            outgoing.RemoveAt(turn);
                        }
                    }
                    yield return new WaitForSecondsRealtime(TickSeconds);
                }
            }
            finally
            {
                pumping = false;
            }
        }

        /// <summary>Adds one piece; the whole image once it's complete, else null.</summary>
        private static byte[] Assemble(Dictionary<string, Incoming> _set, NetPackageKitsunePosterImage _p, ClientInfo _from)
        {
            int total = (_p.size + ChunkSize - 1) / ChunkSize;
            if (_p.data == null || _p.size <= 0 || _p.size > MaxBytes || _p.total != total
                || _p.index < 0 || _p.index >= total)
            {
                return null;
            }
            int start = _p.index * ChunkSize;
            if (_p.data.Length != Math.Min(ChunkSize, _p.size - start))
            {
                return null;
            }
            if (!_set.TryGetValue(_p.hash, out var inc))
            {
                if (_set == uploads)
                {
                    return null; // the server only takes pieces it asked for
                }
                inc = new Incoming { Data = new byte[_p.size], Got = new bool[total], Left = total };
                _set[_p.hash] = inc;
            }
            if (inc.Data.Length != _p.size || (inc.From != null && !inc.From.Equals(_from)))
            {
                return null;
            }
            inc.Last = Time.realtimeSinceStartup;
            if (!inc.Got[_p.index])
            {
                inc.Got[_p.index] = true;
                inc.Left--;
                Buffer.BlockCopy(_p.data, 0, inc.Data, start, _p.data.Length);
            }
            if (inc.Left > 0)
            {
                return null;
            }
            _set.Remove(_p.hash);
            if (!IsImage(inc.Data) || HashOf(inc.Data) != _p.hash)
            {
                Log.Warning($"[KitsunePosters] Shared image {_p.hash} arrived damaged or not matching its name; dropped.");
                return Array.Empty<byte>();
            }
            return inc.Data;
        }

        // --- the server's side -----------------------------------------------

        public static void OnServer(NetPackageKitsunePosterImage _p, ClientInfo _from)
        {
            string hash = _p.hash;
            switch (_p.kind)
            {
                case NetPackageKitsunePosterImage.Kind.Offer:
                {
                    StoreDir();
                    string refusal = null;
                    if (StoreHas(hash))
                    {
                        return; // already here
                    }
                    if (!PosterSettings.AllowUploads)
                    {
                        refusal = "this server doesn't take shared images";
                    }
                    else if (blocked.Contains(hash))
                    {
                        refusal = "an admin removed that image";
                    }
                    else if (_p.size <= 0 || _p.size > MaxBytes)
                    {
                        refusal = "it's too big";
                    }
                    else if (StoreUsed() + _p.size > PosterSettings.UploadSpaceMB * 1024L * 1024L)
                    {
                        refusal = "the server's space for shared images is full";
                    }
                    else if (uploads.ContainsKey(hash))
                    {
                        return; // someone's already sending it
                    }
                    else if (CountFrom(_from) >= MaxUploadsPerPlayer)
                    {
                        refusal = "too many at once, try again in a moment";
                    }
                    if (refusal != null)
                    {
                        _from.SendPackage(Get().Setup(NetPackageKitsunePosterImage.Kind.Missing, hash, 0, refusal));
                        Log.Out($"[KitsunePosters] Refused shared image {hash} from {_from.playerName}: {refusal}.");
                        return;
                    }
                    int total = (_p.size + ChunkSize - 1) / ChunkSize;
                    uploads[hash] = new Incoming
                    {
                        Data = new byte[_p.size],
                        Got = new bool[total],
                        Left = total,
                        Last = Time.realtimeSinceStartup,
                        From = _from,
                        Name = PosterLocal.KeyFromToken(_p.text) != null ? _p.text : "?",
                    };
                    _from.SendPackage(Get().Setup(NetPackageKitsunePosterImage.Kind.Need, hash));
                    return;
                }
                case NetPackageKitsunePosterImage.Kind.Chunk:
                {
                    string name = uploads.TryGetValue(hash, out var upload) ? upload.Name : "?";
                    byte[] data = Assemble(uploads, _p, _from);
                    if (data == null)
                    {
                        return;
                    }
                    if (data.Length == 0)
                    {
                        FailWaiters(hash, "it arrived damaged");
                        return;
                    }
                    StoreAdd(hash, data, _from.playerName, _from.PlatformId?.CombinedString, name);
                    return;
                }
                case NetPackageKitsunePosterImage.Kind.Get:
                {
                    byte[] data = ReadStore(hash);
                    if (data != null)
                    {
                        Queue(_from, hash, data);
                        return;
                    }
                    if (uploads.ContainsKey(hash))
                    {
                        if (!waiters.TryGetValue(hash, out var list))
                        {
                            waiters[hash] = list = new List<ClientInfo>();
                        }
                        if (!list.Contains(_from) && list.Count < 64)
                        {
                            list.Add(_from);
                        }
                        return;
                    }
                    StoreDir();
                    _from.SendPackage(Get().Setup(NetPackageKitsunePosterImage.Kind.Missing, hash, 0,
                        blocked.Contains(hash) ? "an admin removed it" : "nobody shared it on this server"));
                    return;
                }
            }
        }

        private static int CountFrom(ClientInfo _from)
        {
            int n = 0;
            float now = Time.realtimeSinceStartup;
            var stale = new List<string>();
            foreach (var kv in uploads)
            {
                if (now - kv.Value.Last > StallSeconds * 2)
                {
                    stale.Add(kv.Key);
                }
                else if (kv.Value.From != null && kv.Value.From.Equals(_from))
                {
                    n++;
                }
            }
            foreach (var hash in stale)
            {
                uploads.Remove(hash);
                FailWaiters(hash, "the upload stopped part way");
            }
            return n;
        }

        private static void FailWaiters(string _hash, string _why)
        {
            if (!waiters.TryGetValue(_hash, out var list))
            {
                return;
            }
            waiters.Remove(_hash);
            foreach (var ci in list)
            {
                ci.SendPackage(Get().Setup(NetPackageKitsunePosterImage.Kind.Missing, _hash, 0, _why));
            }
        }

        // --- a client's side ---------------------------------------------------

        public static void OnClient(NetPackageKitsunePosterImage _p)
        {
            string hash = _p.hash;
            switch (_p.kind)
            {
                case NetPackageKitsunePosterImage.Kind.Need:
                {
                    byte[] data = ReadCached(hash);
                    if (data != null)
                    {
                        Queue(null, hash, data);
                    }
                    return;
                }
                case NetPackageKitsunePosterImage.Kind.Chunk:
                {
                    if (!waits.TryGetValue(hash, out var wait))
                    {
                        return; // not asked for
                    }
                    byte[] data = Assemble(downloads, _p, null);
                    if (data == null)
                    {
                        return;
                    }
                    if (data.Length == 0)
                    {
                        wait.Error = "it arrived damaged";
                        return;
                    }
                    WriteCached(hash, data);
                    wait.Data = data;
                    return;
                }
                case NetPackageKitsunePosterImage.Kind.Missing:
                {
                    if (waits.TryGetValue(hash, out var wait))
                    {
                        wait.Error = _p.text;
                    }
                    if (myOffers.Remove(hash))
                    {
                        Log.Out($"[KitsunePosters] The server didn't take shared image {hash}: {_p.text}.");
                        Tell($"Only you see that poster: {_p.text}.");
                    }
                    return;
                }
                case NetPackageKitsunePosterImage.Kind.Ready:
                    myOffers.Remove(hash);
                    PosterTextureManager.Retry(PosterLocal.SharedPrefix + hash);
                    return;
                case NetPackageKitsunePosterImage.Kind.Revoke:
                    Revoked(hash);
                    return;
            }
        }

        /// <summary>
        /// Server: a player who was away when an admin removed a picture still
        /// has it cached, and the posters still name it. Every removal goes to
        /// them on joining; one they don't have costs nothing.
        /// </summary>
        public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData _data)
        {
            if (_data.ClientInfo == null || !IsServer
                || (_data.RespawnType != RespawnType.JoinMultiplayer && _data.RespawnType != RespawnType.EnterMultiplayer))
            {
                return;
            }
            if (StoreDir() == null)
            {
                return;
            }
            foreach (string hash in blocked)
            {
                _data.ClientInfo.SendPackage(Get().Setup(NetPackageKitsunePosterImage.Kind.Revoke, hash));
            }
        }

        private static void Revoked(string _hash)
        {
            if (File.Exists(Path.Combine(CacheDir(), _hash + ".img")))
            {
                Log.Out($"[KitsunePosters] Shared image {_hash} was removed by an admin; dropped this PC's copy.");
            }
            DeleteCached(_hash);
            PosterTextureManager.Forget(PosterLocal.SharedPrefix + _hash);
        }

        // --- odds and ends ---------------------------------------------------

        private static void Tell(string _message)
        {
            try
            {
                var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player != null)
                {
                    GameManager.ShowTooltip(player, _message);
                }
            }
            catch { }
        }

        private static string LocalPlayerName()
        {
            try { return GameManager.Instance.World.GetPrimaryPlayer()?.EntityName ?? "host"; }
            catch { return "host"; }
        }

        private static string LocalPlayerId()
        {
            try { return Platform.PlatformManager.NativePlatform?.User?.PlatformUserId?.CombinedString ?? ""; }
            catch { return ""; }
        }
    }
}
