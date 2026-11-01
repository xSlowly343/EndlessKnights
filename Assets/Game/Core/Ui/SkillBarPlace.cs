using Game.Core.Sim;

namespace Game.Core.Ui
{
    public struct SkillBarPlace
    {
        public int Position;
        public SkillSlotRole Role;
        public int Row;
        public bool Open;
        public long UnlockStage;
        public int UnlockLevel;

        public bool Empty => Row == SkillLoadout.NoSlot;
        public bool CastsByItself => SkillLoadout.CastsByItself(Role);
    }
}
