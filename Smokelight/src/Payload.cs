using System.Net.Sockets;
using System.Text;

namespace Smokelight;

public class Payload : IEquatable<Payload> {
    public enum PayloadType : int { Text = 0, Binary = 1, }

    internal PayloadType type;
    internal string name;
    internal byte[] data;

    public PayloadType Type => type;
    public string Name => name;
    public string? TextData {
        get {
            if (type == PayloadType.Binary) return null;
            return Encoding.UTF8.GetString(data);
        }
    }
    public byte[] BinaryData => data;

    public Payload(string name, string text) {
        this.name = name;
        type = PayloadType.Text;
        data = Encoding.UTF8.GetBytes(text);
    }

    public Payload(string name, Span<byte> binary) {
        this.name = name;
        type = PayloadType.Binary;
        data = binary.ToArray();
    }

    internal Payload(string name, PayloadType type, byte[] data) {
        this.name = name;
        this.type = type;
        this.data = data;
    }

    /*
        Serialization structures:

        Payload {
            s32 type
            s32 name_len
            u8[] name
            s32 data_len
            u8[] data
        }

        PackedPayload {
            u8[4] magic = "SLPK"u8
            s32 version = 0
            s32 total_len
            s32 payload_count
            Payload[] payloads
            u8 terminator = '\0'
        }
    */

    internal static byte[] Pack(Payload[] payloads) {
        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, true);

        int totalLength = 16;       // magic (4) + version (4) + total_len (4) + payload_count (4)
        for (int i = 0; i < payloads.Length; i++) {
            totalLength += 12;      // type (4) + name_len (4) + data_len (4)
            totalLength += Encoding.UTF8.GetByteCount(payloads[i].name);
            totalLength += payloads[i].data.Length;
        }
        totalLength += 1;           // terminator (1)

        // magic
        bw.Write("SLPK"u8);

        // version (should be 0 for now)
        bw.Write(0);

        // total size of the pack
        bw.Write(totalLength);
        
        // number of payloads
        bw.Write(payloads.Length);

        for (int i = 0; i < payloads.Length; i++) {
            // type of payload
            bw.Write((int)payloads[i].type);

            // size of name
            byte[] nameUtf8 = Encoding.UTF8.GetBytes(payloads[i].name);
            bw.Write(nameUtf8.Length);

            // name
            bw.Write(nameUtf8);

            // size of data
            bw.Write(payloads[i].data.Length);

            // data
            bw.Write(payloads[i].data);
        }

        // null terminate
        bw.Write('\0');

        byte[] ret = ms.ToArray();
        return ret;
    }

    internal static async Task<Payload[]?> TryUnpackFromStream(NetworkStream stream) {
        if (stream.DataAvailable) {
            try {
                byte[] magic = new byte[4];
                var magicRead = await stream.ReadAsync(magic);
                if (magicRead != 4 || !"SLPK"u8.SequenceEqual(magic)) {
                    return null;
                }

                byte[] buf = new byte[4];
                await stream.ReadExactlyAsync(buf);
                int version = BitConverter.ToInt32(buf);
                if (version != 0) {
                    return null;
                }
                await stream.ReadExactlyAsync(buf);
                int totalLength = BitConverter.ToInt32(buf);
                await stream.ReadExactlyAsync(buf);
                int payloadCount = BitConverter.ToInt32(buf);

                byte[] remainingBuf = new byte[totalLength - 16];
                int remainingRead = await stream.ReadAsync(remainingBuf);
                if (remainingRead != totalLength - 16) {
                    return null;
                }

                var ret = new Payload[payloadCount];

                using (var ms = new MemoryStream(remainingBuf))
                using (var br = new BinaryReader(ms)) {
                    for (int i = 0; i < payloadCount; i++) {
                        int payloadType = br.ReadInt32();
                        int payloadNameLength = br.ReadInt32();
                        byte[] payloadName = br.ReadBytes(payloadNameLength);
                        int payloadDataLength = br.ReadInt32();
                        byte[] payloadData = br.ReadBytes(payloadDataLength);
                        ret[i] = new Payload(Encoding.UTF8.GetString(payloadName), (PayloadType)payloadType, payloadData);
                    }
                    if (br.ReadByte() != (byte)0) {
                        return null;
                    }
                }

                return ret;
            } catch {
                return null;
            }
        } else {
            return null;
        }
    }

    public override bool Equals(object? obj) => this.Equals(obj as Payload);

    public bool Equals(Payload? p) {
        if (p is null) return false;
        if (Object.ReferenceEquals(this, p)) return true;
        if (this.GetType() != p.GetType()) return false;

        return (this.name == p.name) && (this.type == p.type) && data.SequenceEqual(p.data);
    }

    public override int GetHashCode() {
        return (name, data).GetHashCode();
    }

    public static bool operator ==(Payload lhs, Payload rhs) {
        if (lhs is null) if (rhs is null) return true; else return false;
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Payload lhs, Payload rhs) => !(lhs == rhs);
}