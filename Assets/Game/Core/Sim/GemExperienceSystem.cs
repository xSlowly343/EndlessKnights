using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GemExperienceSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly SkillSystem _skills;
        private readonly EventQueue _events;
        private GemLevels _levels;
        private long _seenKills;

        public GemExperienceSystem(GemLevels levels, GameState state, StatSheet hero, SkillSystem skills, EventQueue events)
        {
            _levels = levels;
            _state = state;
            _hero = hero;
            _skills = skills;
            _events = events;
            _seenKills = state.Kills;
            foreach (var slot in state.Skills)
            {
                state.ProgressOf(slot.Skill);
                if (slot.Trigger != null)
                    state.ProgressOf(slot.Trigger);
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (slot.Supports[i] != null)
                        state.ProgressOf(slot.Supports[i]!);
                }
            }
        }

        public void UseLevels(GemLevels levels) => _levels = levels;

        public void Tick()
        {
            var kills = _state.Kills - _seenKills;
            _seenKills = _state.Kills;
            if (kills <= 0)
                return;
            var amount = kills * _levels.ExperiencePerKill * System.Math.Max(0, _hero.Value(CombatStats.GemExperience, 1));
            if (amount <= 0)
                return;
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                if (!_skills.Armed(index))
                    continue;
                var slot = _state.Skills[index];
                var gained = amount * SupportEffects.ExperienceMultiplier(slot);
                Grant(slot.Skill, gained);
                if (slot.Trigger != null)
                    Grant(slot.Trigger, gained);
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (slot.Supports[i] != null)
                        Grant(slot.Supports[i]!, gained);
                }
            }
        }

        public void Grant(GemDef gem, double amount)
        {
            if (!_state.GemProgress.TryGetValue(gem.Id, out var progress) || progress.Level >= _levels.Max)
                return;
            progress.Experience += amount;
            while (progress.Level < _levels.Max && progress.Experience >= _levels.ToNext(progress.Level))
            {
                progress.Experience -= _levels.ToNext(progress.Level);
                progress.Level++;
                _events.Add(GameEvent.ForSkill(GameEventKind.GemLevelUp, _state.Tick, gem));
            }
            if (progress.Level >= _levels.Max)
                progress.Experience = 0;
        }
    }
}
