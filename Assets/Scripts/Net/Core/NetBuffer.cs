using System;
using System.Text;

namespace IBlameYou.Net
{
    // 네트워크 메시지를 바이트로 쓰고 읽는다 (리틀 엔디언, 정수는 가변 길이). 순수 C# — EditMode 테스트 대상.
    public sealed class NetWriter
    {
        private byte[] data;
        private int length;

        public NetWriter(int capacity = 256)
        {
            data = new byte[Math.Max(16, capacity)];
        }

        public int Length => length;

        public void Clear() => length = 0;

        public byte[] ToArray()
        {
            var copy = new byte[length];
            Buffer.BlockCopy(data, 0, copy, 0, length);
            return copy;
        }

        private void Ensure(int extra)
        {
            int need = length + extra;
            if (need <= data.Length) return;
            int size = data.Length * 2;
            while (size < need) size *= 2;
            Array.Resize(ref data, size);
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            data[length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            data[length++] = (byte)value;
            data[length++] = (byte)(value >> 8);
        }

        public void WriteUInt32(uint value)
        {
            Ensure(4);
            data[length++] = (byte)value;
            data[length++] = (byte)(value >> 8);
            data[length++] = (byte)(value >> 16);
            data[length++] = (byte)(value >> 24);
        }

        public void WriteVarUInt(uint value)
        {
            Ensure(5);
            while (value >= 0x80)
            {
                data[length++] = (byte)(value | 0x80);
                value >>= 7;
            }
            data[length++] = (byte)value;
        }

        // 지그재그 (작은 음수도 짧게)
        public void WriteVarInt(int value) => WriteVarUInt((uint)((value << 1) ^ (value >> 31)));

        public void WriteFloat(float value)
        {
            var bits = new FloatBits { Float = value };
            WriteUInt32(bits.UInt);
        }

        // 이미 쓴 자리(position)의 float 4바이트를 바꾼다. 나중에야 아는 값(레이아웃 스크롤 칸 자리)을 채울 때.
        public void PatchFloat(int position, float value)
        {
            if (position < 0 || position + 4 > length) throw new ArgumentOutOfRangeException(nameof(position));
            uint v = new FloatBits { Float = value }.UInt;
            data[position] = (byte)v;
            data[position + 1] = (byte)(v >> 8);
            data[position + 2] = (byte)(v >> 16);
            data[position + 3] = (byte)(v >> 24);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteVarUInt(0);
                return;
            }
            int count = Encoding.UTF8.GetByteCount(value);
            WriteVarUInt((uint)count + 1);
            Ensure(count);
            Encoding.UTF8.GetBytes(value, 0, value.Length, data, length);
            length += count;
        }

        public void WriteBytes(byte[] value)
        {
            if (value == null)
            {
                WriteVarUInt(0);
                return;
            }
            WriteVarUInt((uint)value.Length + 1);
            WriteRaw(value, 0, value.Length);
        }

        public void WriteRaw(byte[] source, int offset, int count)
        {
            Ensure(count);
            Buffer.BlockCopy(source, offset, data, length, count);
            length += count;
        }

        // 다른 writer의 내용을 그대로 이어 붙인다.
        public void WriteRaw(NetWriter other) => WriteRaw(other.data, 0, other.length);

        // 내용 해시 (FNV-1a 64비트). 같은 내용이면 같은 값 — 스타일을 객체가 아니라 모양으로 묶을 때 (HudRecorder).
        public ulong Hash()
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < length; i++)
            {
                h ^= data[i];
                h *= 1099511628211UL;
            }
            return h;
        }

        // 같은 내용인지 (스타일 등 바뀌었는지 비교할 때)
        public bool SameAs(byte[] other)
        {
            if (other == null || other.Length != length) return false;
            for (int i = 0; i < length; i++)
                if (other[i] != data[i]) return false;
            return true;
        }
    }

    public sealed class NetReader
    {
        private readonly byte[] data;
        private int position;
        private readonly int end;

        public NetReader(byte[] data) : this(data, 0, data.Length) { }

        public NetReader(byte[] data, int offset, int count)
        {
            this.data = data;
            position = offset;
            end = offset + count;
        }

        public int Remaining => end - position;
        public bool AtEnd => position >= end;

        private void Need(int count)
        {
            // count > end - position: position + count가 넘쳐 음수가 되어 검사를 지나가지 않게 (버그 점검 2026-11-22)
            if (count < 0 || count > end - position) throw new FormatException("네트워크 메시지가 중간에 끊겼다");
        }

        public byte ReadByte()
        {
            Need(1);
            return data[position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUInt16()
        {
            Need(2);
            ushort value = (ushort)(data[position] | (data[position + 1] << 8));
            position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Need(4);
            uint value = (uint)(data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24));
            position += 4;
            return value;
        }

        public uint ReadVarUInt()
        {
            uint value = 0;
            int shift = 0;
            while (true)
            {
                byte b = ReadByte();
                value |= (uint)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
                if (shift > 28) throw new FormatException("가변 길이 정수가 너무 길다");
            }
        }

        public int ReadVarInt()
        {
            uint raw = ReadVarUInt();
            return (int)(raw >> 1) ^ -(int)(raw & 1);
        }

        public float ReadFloat()
        {
            var bits = new FloatBits { UInt = ReadUInt32() };
            return bits.Float;
        }

        public string ReadString()
        {
            uint header = ReadVarUInt();
            if (header == 0) return null;
            int count = (int)(header - 1);
            Need(count);
            string value = Encoding.UTF8.GetString(data, position, count);
            position += count;
            return value;
        }

        public byte[] ReadBytes()
        {
            uint header = ReadVarUInt();
            if (header == 0) return null;
            int count = (int)(header - 1);
            Need(count);
            var value = new byte[count];
            Buffer.BlockCopy(data, position, value, 0, count);
            position += count;
            return value;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    internal struct FloatBits
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public float Float;
        [System.Runtime.InteropServices.FieldOffset(0)] public uint UInt;
    }
}
