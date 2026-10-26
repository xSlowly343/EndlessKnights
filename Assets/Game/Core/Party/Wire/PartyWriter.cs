using System;

namespace Game.Core.Party.Wire
{
    public struct PartyWriter
    {
        private readonly byte[] _buffer;

        public PartyWriter(byte[] buffer)
        {
            _buffer = buffer;
            Length = 0;
        }

        public int Length { get; private set; }

        public void Byte(byte value)
        {
            if (Length >= _buffer.Length)
                throw new InvalidOperationException($"party packet is larger than {_buffer.Length} bytes");
            _buffer[Length++] = value;
        }

        public void Bool(bool value) => Byte(value ? (byte)1 : (byte)0);

        public void UInt(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
                Byte((byte)(value >> shift));
        }

        public void Int(int value) => UInt(unchecked((uint)value));

        public void ULong(ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                Byte((byte)(value >> shift));
        }

        public void Long(long value) => ULong(unchecked((ulong)value));

        public void Double(double value) => Long(BitConverter.DoubleToInt64Bits(value));

        public void Float(float value) => Int(BitConverter.SingleToInt32Bits(value));
    }
}
