using System;

namespace Game.Core.Party.Wire
{
    public struct PartyReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;
        private int _position;

        public PartyReader(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = System.Math.Min(length, buffer.Length);
            _position = 0;
            Failed = false;
        }

        public bool Failed { get; private set; }

        public bool Complete => !Failed && _position == _length;

        public byte Byte()
        {
            if (_position >= _length)
            {
                Failed = true;
                return 0;
            }
            return _buffer[_position++];
        }

        public bool Bool() => Byte() != 0;

        public uint UInt()
        {
            uint value = 0;
            for (var shift = 0; shift < 32; shift += 8)
                value |= (uint)Byte() << shift;
            return value;
        }

        public int Int() => unchecked((int)UInt());

        public ulong ULong()
        {
            ulong value = 0;
            for (var shift = 0; shift < 64; shift += 8)
                value |= (ulong)Byte() << shift;
            return value;
        }

        public long Long() => unchecked((long)ULong());

        public double Double() => BitConverter.Int64BitsToDouble(Long());

        public float Float() => BitConverter.Int32BitsToSingle(Int());
    }
}
