using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SynergyStrikes
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly SynergySystem _synergy;
        private readonly DotSystem _dots;
        private readonly ResourceSystem _resource;
        private long _critsSeen;
        private int _ticksToSpark = GameLoop.TicksPerSecond;

        public SynergyStrikes(GameState state, StatSheet hero, SynergySystem synergy, DotSystem dots, ResourceSystem resource)
        {
            _state = state;
            _hero = hero;
            _synergy = synergy;
            _dots = dots;
            _resource = resource;
            _critsSeen = state.Crits;
        }

        public long ElectrolysisStrikes { get; private set; }
        public long Sparks { get; private set; }
        public double OmensFromCrits { get; private set; }

        public void Tick()
        {
            Electrolysis();
            Spark();
            SignOfFate();
        }

        private void Electrolysis()
        {
            var share = _hero.Value(CombatStats.LightningPoisonBurst, 0, StatTags.DoT);
            if (share <= 0 || !_state.EnemyAlive)
                return;
            var casts = LightningCastsThisTick();
            if (casts == 0)
                return;
            ElectrolysisStrikes += casts;
            _dots.HitWith(_dots.PoisonHits(_synergy.Rules.AllyPoisonStacks) * share * casts);
        }

        private int LightningCastsThisTick()
        {
            var lightning = _synergy.Rules.LightningMarkMask;
            var skills = _state.Skills;
            var casts = 0;
            for (var i = 0; i < skills.Count; i++)
            {
                if (skills[i].LastCastTick == _state.Tick && (_synergy.Rules.MaskOf(skills[i].Skill) & lightning) != 0)
                    casts++;
            }
            return casts;
        }

        private void SignOfFate()
        {
            var crits = _state.Crits - _critsSeen;
            _critsSeen = _state.Crits;
            if (crits <= 0)
                return;
            var perCrit = _hero.Value(CombatStats.OmenPerPartyCrit, 0);
            if (perCrit <= 0)
                return;
            OmensFromCrits += crits * perCrit;
            _resource.Gain(crits * perCrit);
        }

        private void Spark()
        {
            if (--_ticksToSpark > 0)
                return;
            _ticksToSpark = GameLoop.TicksPerSecond;
            var stacks = _hero.Value(CombatStats.BurnStacksFromAllyLightning, 0, StatTags.DoT);
            if (stacks <= 0 || !_state.EnemyAlive || _state.BurnStacks <= 0)
                return;
            Sparks++;
            _dots.AddBurn(stacks);
        }
    }
}
