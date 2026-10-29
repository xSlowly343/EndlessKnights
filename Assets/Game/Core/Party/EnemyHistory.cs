using Game.Core.Party.Wire;

namespace Game.Core.Party
{
    public struct EnemyHistory
    {
        public const int Depth = 32;
        private const int SlotsPerWord = 8;
        private const int BitsPerSlot = 4;

        public uint Killed;
        public uint Bosses;
        public uint Rewards0;
        public uint Rewards1;
        public uint Rewards2;
        public uint Rewards3;

        public bool KilledAt(int age) => age >= 0 && age < Depth && (Killed & (1u << age)) != 0;

        public bool BossAt(int age) => age >= 0 && age < Depth && (Bosses & (1u << age)) != 0;

        public int RewardedAt(int age) =>
            age >= 0 && age < Depth ? (int)((Word(age / SlotsPerWord) >> (age % SlotsPerWord * BitsPerSlot)) & 0xF) : 0;

        public bool RewardedFor(int age, int slot) => slot >= 0 && slot < PartyRules.MaxSlots && (RewardedAt(age) & (1 << slot)) != 0;

        public uint Word(int index) => index switch
        {
            0 => Rewards0,
            1 => Rewards1,
            2 => Rewards2,
            3 => Rewards3,
            _ => 0
        };

        public void Push(bool killed, bool boss, byte rewarded)
        {
            Killed = (Killed << 1) | (killed ? 1u : 0u);
            Bosses = (Bosses << 1) | (boss ? 1u : 0u);
            Rewards3 = (Rewards3 << BitsPerSlot) | (Rewards2 >> 28);
            Rewards2 = (Rewards2 << BitsPerSlot) | (Rewards1 >> 28);
            Rewards1 = (Rewards1 << BitsPerSlot) | (Rewards0 >> 28);
            Rewards0 = (Rewards0 << BitsPerSlot) | (killed ? (uint)(rewarded & 0xF) : 0u);
        }

        public void Write(ref PartyWriter writer)
        {
            writer.UInt(Killed);
            writer.UInt(Bosses);
            writer.UInt(Rewards0);
            writer.UInt(Rewards1);
            writer.UInt(Rewards2);
            writer.UInt(Rewards3);
        }

        public static EnemyHistory Read(ref PartyReader reader) => new EnemyHistory
        {
            Killed = reader.UInt(),
            Bosses = reader.UInt(),
            Rewards0 = reader.UInt(),
            Rewards1 = reader.UInt(),
            Rewards2 = reader.UInt(),
            Rewards3 = reader.UInt()
        };
    }
}
