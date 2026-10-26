using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public sealed class SkillBook
    {
        private readonly GameState _state;
        private readonly SkillSystem _skills;
        private readonly StatSheet _hero;
        private SkillBookRules _rules;

        public SkillBook(SkillBookRules rules, GameState state, SkillSystem skills, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _skills = skills;
            _hero = hero;
        }

        public double Attribute(string attribute) => _hero.Value(PassiveTree.AttributeStat(attribute), 0);

        public bool MeetsRequirements(GemDef gem)
        {
            foreach (var requirement in gem.Requires)
            {
                if (Attribute(requirement.Key) < requirement.Value)
                    return false;
            }
            return true;
        }

        public int ColoredSupportLimit(string attribute) => (int)System.Math.Floor(Attribute(attribute) / _rules.AttributePerColoredSupport);

        public void UseRules(SkillBookRules rules) => _rules = rules;

        public int SlotCount => _rules.SlotsFor(_state.RecordStage);
        public int MaxSlots => _rules.MaxSlots;

        public long UnlockStageOf(int slot) => _rules.UnlockStageOf(slot);

        public int FreeCopies(GemDef gem)
        {
            var owned = 0;
            foreach (var item in _state.OwnedGems)
            {
                if (ReferenceEquals(item, gem))
                    owned++;
            }
            foreach (var slot in _state.Skills)
            {
                if (ReferenceEquals(slot.Skill, gem))
                    owned--;
                if (ReferenceEquals(slot.Trigger, gem))
                    owned--;
            }
            return owned;
        }

        public BookResult Equip(GemDef skill)
        {
            if (skill.Kind != GemKind.Skill)
                return BookResult.WrongKind;
            if (FreeCopies(skill) <= 0)
                return BookResult.NotOwned;
            if (skill.Locked)
                return BookResult.WaitsForMechanic;
            if (skill.Ascendancy != null && skill.Ascendancy != _state.Ascendancy.Subclass)
                return BookResult.WrongPath;
            if (!MeetsRequirements(skill))
                return BookResult.AttributesTooLow;
            if (_state.Skills.Count >= SlotCount)
                return BookResult.NoFreeSlot;
            _skills.Equip(skill);
            return BookResult.Done;
        }

        public BookResult Unequip(int index)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            _skills.Unequip(index);
            return BookResult.Done;
        }

        public BookResult SetTrigger(int index, GemDef? trigger)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            if (trigger != null && trigger.Kind != GemKind.Trigger)
                return BookResult.WrongKind;
            var slot = _state.Skills[index];
            if (trigger != null && !ReferenceEquals(slot.Trigger, trigger) && FreeCopies(trigger) <= 0)
                return BookResult.NotOwned;
            if (trigger != null && !MeetsRequirements(trigger))
                return BookResult.AttributesTooLow;
            slot.Trigger = trigger;
            return BookResult.Done;
        }

        public bool GrantStartingGems()
        {
            if (_state.OwnedGems.Count > 0)
                return false;
            _state.OwnedGems.AddRange(_rules.StartingGems(HeroClassNames.Of(_state.Class)));
            foreach (var gem in _state.OwnedGems)
            {
                if (gem.Kind == GemKind.Skill)
                    Equip(gem);
            }
            foreach (var gem in _state.OwnedGems)
            {
                if (gem.Kind == GemKind.Trigger)
                    AttachToFirstPaidSkill(gem);
            }
            return true;
        }

        private void AttachToFirstPaidSkill(GemDef trigger)
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (slot.Trigger == null && slot.Def.ResourceCost > 0)
                {
                    SetTrigger(index, trigger);
                    return;
                }
            }
        }

        public BookResult Move(int from, int to)
        {
            if (from < 0 || from >= _state.Skills.Count || to < 0 || to >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            _skills.Move(from, to);
            return BookResult.Done;
        }
    }
}
