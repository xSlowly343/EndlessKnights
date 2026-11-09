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
            GrantCasts();
            GrantTrophies();
            var kills = _state.Kills - _seenKills + _state.Oddities.PendingGemKills;
            _seenKills = _state.Kills;
            _state.Oddities.PendingGemKills = 0;
            if (kills <= 0)
                return;
            var amount = kills * _levels.ExperiencePerKill * GemMultiplier;
            if (amount <= 0)
                return;
            GemDef? lowest = null;
            var lowestGained = 0.0;
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                var gained = amount * SupportEffects.ExperienceMultiplier(slot, _state);
                Note(slot.Skill, gained, ref lowest, ref lowestGained);
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (slot.Supports[i] != null)
                        Note(slot.Supports[i]!, gained * SupportMultiplier, ref lowest, ref lowestGained);
                }
            }
            var bonus = _hero.Value(CombatStats.LowestGemExperience, 0);
            if (lowest != null && bonus > 0)
                Grant(lowest, lowestGained * bonus);
        }

        private void Note(GemDef gem, double gained, ref GemDef? lowest, ref double lowestGained)
        {
            if (lowest == null || _state.LevelOf(gem) < _state.LevelOf(lowest))
            {
                lowest = gem;
                lowestGained = gained;
            }
            Grant(gem, gained);
        }

        private double GemMultiplier => System.Math.Max(0, _hero.Value(CombatStats.GemExperience, 1));

        private double SupportMultiplier => System.Math.Max(0, _hero.Value(CombatStats.SupportExperience, 1));

        private void GrantTrophies()
        {
            var trophies = _state.PendingTrophies;
            if (trophies <= 0)
                return;
            _state.PendingTrophies = 0;
            var amount = trophies * _levels.ExperiencePerTrophy * GemMultiplier;
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                var gained = amount * SupportEffects.ExperienceMultiplier(slot, _state);
                Grant(slot.Skill, gained);
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (slot.Supports[i] != null)
                        Grant(slot.Supports[i]!, gained * SupportMultiplier);
                }
            }
        }

        private void GrantCasts()
        {
            if (_levels.ExperiencePerCast <= 0)
                return;
            var amount = _levels.ExperiencePerCast * GemMultiplier;
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (slot.CastOn(_state.Tick))
                    Grant(slot.Skill, amount * SupportEffects.ExperienceMultiplier(slot, _state));
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
                if (OpensTier(gem, progress.Level))
                    _skills.RefreshBranches();
            }
            if (progress.Level >= _levels.Max)
                progress.Experience = 0;
        }

        private static bool OpensTier(GemDef gem, int level)
        {
            var tiers = gem.Branches;
            for (var i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Level == level)
                    return true;
            }
            return false;
        }
    }
}
