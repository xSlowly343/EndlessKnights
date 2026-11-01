using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public struct AllySkillPlace
    {
        public int Position;
        public SkillSlotRole Role;
        public GemDef? Skill;
        public int Level;
        public int Combo;

        public bool Empty => Skill == null;
    }
}
