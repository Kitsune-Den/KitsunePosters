namespace KitsunePosters
{
    /// <summary>
    /// Moves shared poster images between players and the server, in pieces
    /// small enough for any of the game's connections (see PosterShare).
    ///
    /// Uploading (client to server): Offer, the server answers Need (send
    /// it) or Missing (it won't take it, and why), then Chunks follow.
    /// Downloading: Get, the server answers with Chunks or Missing.
    /// Ready tells everyone a new image arrived; Revoke that an admin
    /// removed one.
    /// </summary>
    public class NetPackageKitsunePosterImage : NetPackage
    {
        public enum Kind : byte { Offer, Need, Get, Chunk, Missing, Ready, Revoke }

        public Kind kind;
        public string hash = "";
        public int index;
        public int total;
        public int size;
        public string text = "";
        public byte[] data;

        public NetPackageKitsunePosterImage Setup(Kind _kind, string _hash, int _size = 0, string _text = "")
        {
            kind = _kind;
            hash = _hash ?? "";
            index = 0;
            total = 0;
            size = _size;
            text = _text ?? "";
            data = null;
            return this;
        }

        public NetPackageKitsunePosterImage Chunk(string _hash, int _index, int _total, int _size, byte[] _data)
        {
            Setup(Kind.Chunk, _hash, _size);
            index = _index;
            total = _total;
            data = _data;
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            var reader = (System.IO.BinaryReader)_reader;
            kind = (Kind)reader.ReadByte();
            hash = reader.ReadString();
            index = reader.ReadInt32();
            total = reader.ReadInt32();
            size = reader.ReadInt32();
            text = reader.ReadString();
            int length = reader.ReadInt32();
            // Never trust a length off the wire with an allocation.
            data = length > 0 && length <= PosterShare.ChunkSize ? reader.ReadBytes(length) : null;
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            var writer = (System.IO.BinaryWriter)_writer;
            writer.Write((byte)kind);
            writer.Write(hash ?? "");
            writer.Write(index);
            writer.Write(total);
            writer.Write(size);
            writer.Write(text ?? "");
            if (data == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (!PosterShare.IsHash(hash))
            {
                return;
            }
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                if (Sender != null)
                {
                    PosterShare.OnServer(this, Sender);
                }
                return;
            }
            PosterShare.OnClient(this);
        }
    }
}
