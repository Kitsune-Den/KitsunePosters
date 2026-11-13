using System;

namespace KitsunePosters
{
    /// <summary>
    /// The settings window's wire format, after KitsuneUI's shelved
    /// trader-jobs pattern: Ask (client: what is the allowlist?), Set (client
    /// admin: change it) and Tell (server: here it is). The server checks the
    /// sender's permission level before applying a Set, then tells everyone;
    /// a denied Set gets a Tell with the denied flag so the window can say so.
    /// </summary>
    public class NetPackageKitsunePosters : NetPackage
    {
        public enum Kind : byte { Ask, Set, Tell }
        public enum Op : byte { Add, Remove, AllowAll, UploadsOn, UploadsOff }

        private Kind kind;
        private Op op;
        private string text = "";
        private bool denied;
        private bool uploads;

        public NetPackageKitsunePosters Setup(Kind _kind, Op _op, string _text, bool _denied = false)
        {
            kind = _kind;
            op = _op;
            text = _text ?? "";
            denied = _denied;
            uploads = PosterSettings.AllowUploads; // only the server's copy is read
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            var reader = (System.IO.BinaryReader)_reader;
            kind = (Kind)reader.ReadByte();
            op = (Op)reader.ReadByte();
            text = reader.ReadString();
            denied = reader.ReadBoolean();
            uploads = reader.ReadBoolean();
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            var writer = (System.IO.BinaryWriter)_writer;
            writer.Write((byte)kind);
            writer.Write((byte)op);
            writer.Write(text ?? "");
            writer.Write(denied);
            writer.Write(uploads);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm.IsServer)
            {
                if (kind == Kind.Set)
                {
                    bool admin = Sender != null && _callbacks?.adminTools != null
                        && _callbacks.adminTools.Users.GetUserPermissionLevel(Sender) <= 0;
                    if (!admin)
                    {
                        Sender?.SendPackage(NetPackageManager.GetPackage<NetPackageKitsunePosters>()
                            .Setup(Kind.Tell, op, PosterNet.LocalCsv(), _denied: true));
                        return;
                    }
                    switch (op)
                    {
                        case Op.Add: Log.Out($"[KitsunePosters] {Sender.playerName}: {PosterSettings.AddDomain(text)}"); break;
                        case Op.Remove: Log.Out($"[KitsunePosters] {Sender.playerName}: {PosterSettings.RemoveDomain(text)}"); break;
                        case Op.AllowAll: PosterSettings.AllowAll(); Log.Out($"[KitsunePosters] {Sender.playerName}: every site allowed."); break;
                        case Op.UploadsOn:
                        case Op.UploadsOff:
                            PosterSettings.SetAllowUploads(op == Op.UploadsOn);
                            Log.Out($"[KitsunePosters] {Sender.playerName}: shared images {(op == Op.UploadsOn ? "on" : "off")}.");
                            break;
                    }
                    // Everyone's window (and local cache) learns the new list.
                    cm.SendPackage(NetPackageManager.GetPackage<NetPackageKitsunePosters>()
                        .Setup(Kind.Tell, op, PosterNet.LocalCsv()));
                    return;
                }
                if (kind == Kind.Ask)
                {
                    Sender?.SendPackage(NetPackageManager.GetPackage<NetPackageKitsunePosters>()
                        .Setup(Kind.Tell, op, PosterNet.LocalCsv()));
                }
                return;
            }
            if (kind == Kind.Tell)
            {
                PosterNet.ReceiveTell(text, denied, uploads);
            }
        }
    }

    /// <summary>Which allowlist the window is looking at: this game's, or the server's.</summary>
    public static class PosterNet
    {
        /// <summary>The server's list as last told to this client; null = not asked yet.</summary>
        public static string ServerDomains;
        public static bool ServerUploads;
        public static bool LastDenied;

        public static event Action Changed;

        /// <summary>Playing on someone else's server (not single player, not hosting).</summary>
        public static bool Remote
        {
            get
            {
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
        }

        public static string LocalCsv()
        {
            return string.Join(",", PosterSettings.AllowedDomains);
        }

        public static string CurrentCsv => Remote ? ServerDomains : LocalCsv();

        /// <summary>Whether the server takes shared pictures (as last told, when it's someone else's).</summary>
        public static bool CurrentUploads => Remote ? ServerUploads : PosterSettings.AllowUploads;

        public static void Ask()
        {
            if (Remote)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                    NetPackageManager.GetPackage<NetPackageKitsunePosters>()
                        .Setup(NetPackageKitsunePosters.Kind.Ask, NetPackageKitsunePosters.Op.Add, ""));
            }
        }

        public static void Send(NetPackageKitsunePosters.Op _op, string _domain)
        {
            if (Remote)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                    NetPackageManager.GetPackage<NetPackageKitsunePosters>()
                        .Setup(NetPackageKitsunePosters.Kind.Set, _op, _domain));
                return;
            }
            // Single player or hosting: this machine IS the server.
            switch (_op)
            {
                case NetPackageKitsunePosters.Op.Add: PosterSettings.AddDomain(_domain); break;
                case NetPackageKitsunePosters.Op.Remove: PosterSettings.RemoveDomain(_domain); break;
                case NetPackageKitsunePosters.Op.AllowAll: PosterSettings.AllowAll(); break;
                case NetPackageKitsunePosters.Op.UploadsOn: PosterSettings.SetAllowUploads(true); break;
                case NetPackageKitsunePosters.Op.UploadsOff: PosterSettings.SetAllowUploads(false); break;
            }
            RaiseChanged();
        }

        internal static void ReceiveTell(string _csv, bool _denied, bool _uploads)
        {
            ServerDomains = _csv;
            ServerUploads = _uploads;
            LastDenied = _denied;
            RaiseChanged();
        }

        internal static void RaiseChanged()
        {
            try { Changed?.Invoke(); }
            catch (Exception e) { Log.Warning($"[KitsunePosters] Settings window refresh failed: {e.Message}"); }
        }
    }
}
