using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed partial class StageSystem
    {
        private OddityRules Oddities => Encounters.Oddities;

        public double OddityChanceMultiplier => System.Math.Max(0, _hero.Value(CombatStats.OddityChance, 1));

        private double CrowdShare => _state.Oddities.CrowdStages > 0 ? 1 - Oddities.CrowdCut : 1;

        public bool OdditiesAllowed(long stage) => !Scripted && !Linked && !Oddities.Excludes(stage) && _stages.GateAt(stage) == null;

        private bool TrySpawnOddity(long stage, EnemyDef? enemy, double cut)
        {
            var order = Oddities.Order;
            if (order.Length == 0 || !OdditiesAllowed(stage))
                return false;
            var perSpawn = OddityChanceMultiplier * _stages.Waves.Count / System.Math.Max(1, EnemiesOnStage(stage));
            if (!(perSpawn > 0))
                return false;
            var roll = _state.Oddities.Rng.NextDouble();
            for (var i = 0; i < order.Length; i++)
            {
                var def = order[i];
                if (!Eligible(def, stage))
                    continue;
                var chance = def.Chance * perSpawn;
                if (roll < chance)
                {
                    SpawnOddity(def, stage, enemy, cut);
                    return true;
                }
                roll -= chance;
            }
            return false;
        }

        private bool Eligible(OddityDef def, long stage)
        {
            if (!(def.Chance > 0) || stage < def.MinStage)
                return false;
            var oddities = _state.Oddities;
            switch (def.Kind)
            {
                case OddityKind.Miser:
                    return oddities.TrailStages <= 0;
                case OddityKind.Splitter:
                    return oddities.CrowdStages <= 0;
                case OddityKind.Sprout:
                    return oddities.SproutStacks < Oddities.SproutMaxStacks;
                default:
                    return true;
            }
        }

        private void SpawnOddity(OddityDef def, long stage, EnemyDef? enemy, double cut)
        {
            var normalHp = _stages.NormalHp(stage) * Multiplier(enemy);
            var hpCut = def.Kind == OddityKind.Dummy ? 0 : cut;
            Spawn(enemy, EnemyType.Normal, normalHp * (def.HpMultiplier * (1 - hpCut)));
            var oddities = _state.Oddities;
            oddities.Kind = def.Kind;
            if (def.Kind == OddityKind.Dummy)
            {
                oddities.DummyTicks = (int)System.Math.Round(Oddities.DummySeconds * GameLoop.TicksPerSecond);
                oddities.DummyNormalHp = normalHp;
                oddities.DummyKills = 0;
            }
            if (def.Kind == OddityKind.Splitter)
                oddities.PartHp = normalHp * (Oddities.SplitterPartHp * (1 - cut));
            _events.Add(GameEvent.WithAmount(GameEventKind.OddityAppeared, _state.Tick, BigNumber.FromDouble((int)def.Kind), stage));
        }

        private double OddityGold()
        {
            if (_state.EnemyType != EnemyType.Normal)
                return 1;
            var oddities = _state.Oddities;
            if (oddities.Kind == OddityKind.SplitterPart)
                return Oddities.SplitterPartHp;
            return oddities.TrailStages > 0 && oddities.Kind == OddityKind.None ? TrailGold : 1;
        }

        private double TrailGold => Oddities.TrailGold * System.Math.Max(0, _hero.Value(CombatStats.EncounterGold, 1, StatTags.Gold));

        private bool RewardOddity(OddityKind kind)
        {
            var oddities = _state.Oddities;
            switch (kind)
            {
                case OddityKind.Miser:
                    oddities.TrailStages = Oddities.TrailStages + (int)System.Math.Floor(System.Math.Max(0, _hero.Value(CombatStats.TrailStages, 0)) + 1e-9);
                    break;
                case OddityKind.Barrel:
                    Drops?.GiveOddityShards(Oddities.BarrelShardShare);
                    break;
                case OddityKind.Bee:
                    oddities.PendingResourceShare += Oddities.BeeResourceShare;
                    break;
                case OddityKind.Sprout:
                    oddities.SproutStacks = System.Math.Min(Oddities.SproutMaxStacks, oddities.SproutStacks + 1);
                    break;
                case OddityKind.Splitter:
                    oddities.PartsLeft = Oddities.SplitterParts;
                    oddities.SplitTicks = (int)System.Math.Round(Oddities.SplitterSeconds * GameLoop.TicksPerSecond);
                    _events.Add(GameEvent.WithAmount(GameEventKind.OddityRewarded, _state.Tick, BigNumber.FromDouble((int)kind), _state.Stage));
                    SpawnPart();
                    return true;
                default:
                    return false;
            }
            _events.Add(GameEvent.WithAmount(GameEventKind.OddityRewarded, _state.Tick, BigNumber.FromDouble((int)kind), _state.Stage));
            return false;
        }

        private void SpawnPart()
        {
            var oddities = _state.Oddities;
            var hp = oddities.PartHp;
            var normals = _enemies.Normals(BiomeRotation.Current(_stages, _state, _state.Stage));
            var enemy = normals.Count == 0 ? null : normals[_state.Oddities.Rng.NextInt(0, normals.Count)];
            Spawn(enemy, EnemyType.Normal, hp.Sign > 0 ? hp : _stages.NormalHp(_state.Stage) * Oddities.SplitterPartHp);
            oddities.Kind = OddityKind.SplitterPart;
        }

        private void OnPartKilled()
        {
            var oddities = _state.Oddities;
            oddities.PartsLeft--;
            if (oddities.PartsLeft > 0 && oddities.SplitTicks > 0)
            {
                SpawnPart();
                return;
            }
            if (oddities.PartsLeft <= 0 && oddities.SplitTicks > 0)
            {
                oddities.CrowdStages = Oddities.CrowdStages;
                _events.Add(GameEvent.WithAmount(GameEventKind.CrowdThinned, _state.Tick, BigNumber.FromDouble(oddities.CrowdStages), _state.Stage));
            }
            EndSplit();
        }

        private void EndSplit()
        {
            var oddities = _state.Oddities;
            oddities.PartsLeft = 0;
            oddities.SplitTicks = 0;
            oddities.PartHp = BigNumber.Zero;
            SpawnNext();
        }

        private void TickOddities()
        {
            var oddities = _state.Oddities;
            if (oddities.Kind == OddityKind.Dummy)
                TickDummy();
            if (oddities.SplitTicks > 0 && --oddities.SplitTicks == 0 && oddities.Kind == OddityKind.SplitterPart)
                EndSplit();
        }

        private void TickDummy()
        {
            var oddities = _state.Oddities;
            if (oddities.DummyDamage.Sign > 0 && oddities.DummyNormalHp.Sign > 0)
            {
                var room = System.Math.Max(0, Oddities.DummyMaxKills - oddities.DummyKills);
                var kills = System.Math.Min(room, (oddities.DummyDamage / oddities.DummyNormalHp).ToDouble());
                oddities.DummyDamage = BigNumber.Zero;
                if (kills > 0)
                {
                    oddities.DummyKills += kills;
                    oddities.PendingGemKills += kills;
                    _levels.OnKill(EnemyType.Normal, kills);
                }
            }
            if (--oddities.DummyTicks > 0 || !_state.EnemyAlive)
                return;
            _events.Add(GameEvent.WithAmount(GameEventKind.OddityRewarded, _state.Tick, BigNumber.FromDouble((int)OddityKind.Dummy), _state.Stage));
            SpawnNormal();
        }

        private void CountOddityStages()
        {
            var oddities = _state.Oddities;
            if (oddities.TrailStages > 0)
                oddities.TrailStages--;
            if (oddities.CrowdStages > 0)
                oddities.CrowdStages--;
        }
    }
}
