using System.Collections.Generic;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class MentorBonus
    {
        private readonly LevelSystem _levels;
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>(1);
        private readonly List<string> _marks = new List<string>();
        private MentorRules _rules;

        public MentorBonus(MentorRules rules, StatSheet hero, LevelSystem levels, GameState state)
        {
            _rules = rules;
            _levels = levels;
            _state = state;
            _layer = new ModifierLayer(hero);
        }

        public bool Active { get; private set; }
        public IReadOnlyList<string> Marks => _marks;

        public void UseRules(MentorRules rules)
        {
            _rules = rules;
            if (Active)
            {
                Active = false;
                Set(true);
            }
        }

        public void Set(bool active)
        {
            if (active == Active)
                return;
            Active = active;
            _modifiers.Clear();
            if (active)
                _modifiers.Add(new Modifier(CombatStats.Gold, ModifierKind.More, _rules.GoldBonus, StatTags.None));
            _layer.Set(_modifiers);
            _levels.XpMultiplier = active ? 1 + _rules.XpBonus : 1;
        }

        public void UseMarks(IReadOnlyList<string> marks)
        {
            _marks.Clear();
            for (var i = 0; i < marks.Count && i < _rules.MaxMarks; i++)
                _marks.Add(marks[i]);
        }

        public bool IsMarked(string nodeId) => _marks.Contains(nodeId);

        public void UsePerks(IReadOnlyList<string> perks, int coopPoints)
        {
            for (var i = 0; i < perks.Count; i++)
            {
                if (perks[i] == MentorRules.EarlyBookSlotPerk)
                    _state.EarlyBookSlot = true;
            }
            if (coopPoints <= _state.MentorCoopPoints)
                return;
            _state.MentorCoopPoints = coopPoints;
            _levels.SyncTreePoints();
        }

        public static bool FightsBeside(PartyClient party, ulong mentorPlayer) => FightsBeside(party.Status, party.Roster, party.OwnSlot, mentorPlayer);

        public static bool FightsBeside(PartyLink status, in RosterMessage roster, int ownSlot, ulong mentorPlayer)
        {
            if (status != PartyLink.Linked || mentorPlayer == 0)
                return false;
            var slot = roster.SlotOf(mentorPlayer);
            return slot >= 0 && ownSlot >= 0 && slot != ownSlot && roster.Presence(slot) == PartyPresence.Fighting && roster.Presence(ownSlot) == PartyPresence.Fighting;
        }
    }
}
