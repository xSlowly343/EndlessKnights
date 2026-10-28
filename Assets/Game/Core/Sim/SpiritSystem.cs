using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SpiritSystem
    {
        public const int Capacity = 64;

        private readonly GameState _state;
        private readonly CombatSystem _combat;
        private readonly double[] _progress = new double[Capacity];
        private SpiritCatalog _catalog;
        private long _seenKills;
        private long _seenCrits;
        private long _seenCasts;

        public SpiritSystem(SpiritCatalog catalog, GameState state, CombatSystem combat)
        {
            _catalog = catalog;
            _state = state;
            _combat = combat;
            _seenKills = state.Kills;
            _seenCrits = state.Crits;
            _seenCasts = state.SkillCasts;
        }

        public SpiritCatalog Catalog => _catalog;
        public long Strikes { get; private set; }

        public int Level(string id) => _state.Spirits.TryGetValue(id, out var level) ? level : 0;

        public BigNumber Cost(SpiritDef spirit)
        {
            var level = Level(spirit.Id);
            return level == 0 ? BigNumber.FromDouble(spirit.AwakenCost) : spirit.LevelCost.At(level);
        }

        public void UseCatalog(SpiritCatalog catalog)
        {
            _catalog = catalog;
            System.Array.Clear(_progress, 0, Capacity);
        }

        public SpiritResult Raise(string id)
        {
            if (!_catalog.TryGet(id, out var spirit))
                return SpiritResult.UnknownSpirit;
            var level = Level(id);
            if (level >= spirit.MaxLevel)
                return SpiritResult.MaxLevel;
            var cost = Cost(spirit);
            if (_state.Echo < cost)
                return SpiritResult.NotEnoughEcho;
            _state.Echo -= cost;
            _state.Spirits[id] = level + 1;
            return level == 0 ? SpiritResult.Awakened : SpiritResult.Raised;
        }

        public void Tick(in EffectiveInput input)
        {
            var kills = _state.Kills - _seenKills;
            var crits = _state.Crits - _seenCrits;
            var casts = _state.SkillCasts - _seenCasts;
            _seenKills = _state.Kills;
            _seenCrits = _state.Crits;
            _seenCasts = _state.SkillCasts;
            var spirits = _catalog.All;
            for (var i = 0; i < spirits.Count && i < Capacity; i++)
            {
                var spirit = spirits[i];
                var level = Level(spirit.Id);
                if (level <= 0)
                    continue;
                _progress[i] += Amount(spirit.Trigger, input, kills, crits, casts);
                if (spirit.Trigger == SpiritTrigger.IdleSeconds && input.HasInput)
                    _progress[i] = 0;
                while (_progress[i] >= spirit.Every - 1e-9)
                {
                    _progress[i] -= spirit.Every;
                    Strike(spirit.MultiplierAt(level));
                }
            }
        }

        private void Strike(double multiplier)
        {
            if (!_state.EnemyAlive)
                return;
            Strikes++;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier, StatTags.Skill);
        }

        private static double Amount(SpiritTrigger trigger, in EffectiveInput input, long kills, long crits, long casts) => trigger switch
        {
            SpiritTrigger.Keys => input.Keys,
            SpiritTrigger.MousePx => input.MousePx,
            SpiritTrigger.Clicks => input.LeftClicks + input.RightClicks + input.MiddleClicks,
            SpiritTrigger.Casts => casts,
            SpiritTrigger.IdleSeconds => input.HasInput ? 0 : GameLoop.TickSeconds,
            SpiritTrigger.Kills => kills,
            SpiritTrigger.Crits => crits,
            _ => GameLoop.TickSeconds
        };
    }
}
