using Game.Core.Sim;

namespace Game.Core.Party.Wire
{
    public struct PartyLoadoutMessage
    {
        public byte Slot;
        public LoadoutSkill Left;
        public LoadoutSkill Right;
        public LoadoutSkill Auto0;
        public LoadoutSkill Auto1;
        public LoadoutSkill Auto2;
        public LoadoutSkill Aura0;
        public LoadoutSkill Aura1;
        public LoadoutSkill Aura2;
        public LoadoutSkill Aura3;

        public LoadoutSkill At(int position) => position switch
        {
            0 => Left,
            1 => Right,
            2 => Auto0,
            3 => Auto1,
            4 => Auto2,
            5 => Aura0,
            6 => Aura1,
            7 => Aura2,
            8 => Aura3,
            _ => default
        };

        public void Set(int position, LoadoutSkill skill)
        {
            switch (position)
            {
                case 0: Left = skill; break;
                case 1: Right = skill; break;
                case 2: Auto0 = skill; break;
                case 3: Auto1 = skill; break;
                case 4: Auto2 = skill; break;
                case 5: Aura0 = skill; break;
                case 6: Aura1 = skill; break;
                case 7: Aura2 = skill; break;
                case 8: Aura3 = skill; break;
            }
        }

        public static int PositionOf(SkillSlotRole role, int indexInRole) => role switch
        {
            SkillSlotRole.Left => 0,
            SkillSlotRole.Right => 1,
            SkillSlotRole.Auto => indexInRole < SkillLoadout.AutoSlots ? 2 + indexInRole : -1,
            _ => indexInRole < SkillLoadout.AuraSlots ? 2 + SkillLoadout.AutoSlots + indexInRole : -1
        };

        public bool SameAs(in PartyLoadoutMessage other)
        {
            for (var i = 0; i < SkillLoadout.MaxSlots; i++)
            {
                var a = At(i);
                var b = other.At(i);
                if (a.GemNetId != b.GemNetId || a.Level != b.Level || a.Combo != b.Combo)
                    return false;
            }
            return true;
        }

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            for (var i = 0; i < SkillLoadout.MaxSlots; i++)
                At(i).Write(ref writer);
        }

        public static PartyLoadoutMessage Read(ref PartyReader reader)
        {
            var message = new PartyLoadoutMessage { Slot = reader.Byte() };
            for (var i = 0; i < SkillLoadout.MaxSlots; i++)
                message.Set(i, LoadoutSkill.Read(ref reader));
            return message;
        }
    }
}
