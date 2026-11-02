using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DreamcatcherMechanics : IKillRewardListener
    {
        public static readonly int NetEffect = GemDef.NetIdOf("trait_dream_net");
        public static readonly int ProphecyEffect = GemDef.NetIdOf("trait_dream_prophecy");
        public static readonly int HornEffect = GemDef.NetIdOf("trait_dream_horn");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly Dictionary<string, int> _species = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _biomes = new Dictionary<string, int>();
        private readonly List<int>[] _pages = new List<int>[DreamcatcherState.BiomeCapacity];
        private DreamcatcherRules _rules;
        private DropSystem? _drops;
        private LootSystem? _loot;

        public DreamcatcherMechanics(DreamcatcherRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
            for (var i = 0; i < _pages.Length; i++)
                _pages[i] = new List<int>();
        }

        public void UseRules(DreamcatcherRules rules) => _rules = rules;

        public void UseLoot(DropSystem drops, LootSystem loot)
        {
            _drops = drops;
            _loot = loot;
        }

        public void UseEnemies(EnemyCatalog enemies)
        {
            _species.Clear();
            _biomes.Clear();
            foreach (var page in _pages)
                page.Clear();
            foreach (var enemy in enemies.All)
            {
                if (enemy.Type != EnemyType.Normal || _species.Count >= DreamcatcherState.AlbumCapacity)
                    continue;
                if (!_biomes.TryGetValue(enemy.Biome, out var biome))
                {
                    if (_biomes.Count >= DreamcatcherState.BiomeCapacity)
                        continue;
                    biome = _biomes.Count;
                    _biomes[enemy.Biome] = biome;
                }
                var index = _species.Count;
                _species[enemy.Id] = index;
                _pages[biome].Add(index);
            }
        }

        private DreamcatcherState S => _state.Dreamcatcher;
        private bool On => _state.Is(HeroClass.Dreamcatcher);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool Nets => On && NetPower > 0;
        public bool Album => On && AlbumPower > 0;
        public bool Bait => On && SpiritLoot > 0;

        public double NetPower => Positive(DreamcatcherStats.NetPower);
        public double AlbumPower => Positive(DreamcatcherStats.AlbumPower);
        public double SpiritLoot => Positive(DreamcatcherStats.SpiritLoot);

        public double NetPx => System.Math.Max(_rules.NetPxMin, _rules.NetPx + _hero.Value(DreamcatcherStats.NetPx, 0));

        public int NetMax => System.Math.Max(1, (int)System.Math.Round(_rules.NetMax + _hero.Value(DreamcatcherStats.NetMax, 0)));

        public double NetSeconds => System.Math.Max(_rules.NetSecondsMin, _rules.NetSeconds + _hero.Value(DreamcatcherStats.NetSeconds, 0));

        public double NetStrength => NetPower + Positive(DreamcatcherStats.NetParty);

        public double TrophyChance => System.Math.Min(1, System.Math.Max(0, _rules.NetTrophy + _hero.Value(DreamcatcherStats.NetTrophy, 0)));

        public double AlbumMax => System.Math.Max(0, _rules.AlbumMax + _hero.Value(DreamcatcherStats.AlbumMax, 0));

        public int PageMin => System.Math.Max(_rules.PageMinFloor, (int)System.Math.Round(_rules.PageMin + _hero.Value(DreamcatcherStats.PageMin, 0)));

        public double ProphecyPower => System.Math.Max(0, _rules.ProphecyPower + _hero.Value(DreamcatcherStats.ProphecyPower, 0));

        public int BaitHits => System.Math.Max(_rules.BaitHitsMin, (int)System.Math.Round(_rules.BaitHits + _hero.Value(DreamcatcherStats.BaitHits, 0)));

        public double SpiritHp => System.Math.Min(_rules.SpiritHpMax, System.Math.Max(1, _rules.SpiritHp + _hero.Value(DreamcatcherStats.SpiritHp, 0)));

        public int Species => _species.Count;

        public int SpeciesIndex(string id) => _species.TryGetValue(id, out var index) ? index : -1;

        public int BiomeIndex(string id) => _biomes.TryGetValue(id, out var index) ? index : -1;

        public double AlbumBonus(int index)
        {
            if (!Album || index < 0)
                return 0;
            var own = System.Math.Min(AlbumMax, S.Album[index] * AlbumPower);
            if (!Flag(DreamcatcherStats.AlbumAll))
                return own;
            var best = 0;
            for (var i = 0; i < _species.Count; i++)
                best = System.Math.Max(best, S.Album[i]);
            return System.Math.Max(own, _rules.AllShare * System.Math.Min(AlbumMax, best * AlbumPower));
        }

        public bool PageFull(int biome)
        {
            if (biome < 0 || biome >= _pages.Length || _pages[biome].Count == 0)
                return false;
            var page = _pages[biome];
            var min = PageMin;
            for (var i = 0; i < page.Count; i++)
            {
                if (S.Album[page[i]] < min)
                    return false;
            }
            return true;
        }

        public double DamageTaken()
        {
            if (!On)
                return 1;
            var multiplier = S.DozeTicks > 0 ? _rules.DozeMultiplier : 1;
            if (S.LucidTicks > 0)
                multiplier *= 1 + (Album ? AlbumMax : _rules.LucidPower);
            if (_state.SharedEnemy)
                return multiplier;
            if (S.Caught)
                multiplier *= 1 + NetStrength;
            if (S.ProphecyTicks > 0)
                multiplier *= 1 + ProphecyPower;
            if (S.HornTicks > 0)
                multiplier *= 1 + _rules.HornPower;
            return multiplier;
        }

        public bool Dozing => On && (S.DozeTicks > 0 || S.Caught);

        public void AfterStrike()
        {
            if (!Bait)
                return;
            AddBait(1);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (Bait && Flag(DreamcatcherStats.BaitByCast))
                AddBait(_rules.BaitPerCast);
            if (strike.Dream > 0)
                _state.PendingResource += strike.Dream;
            if (strike.CaughtHit > 0 && (Dozing || EnemyCaughtByParty))
                TraitHit(strike.CaughtHit);
            if (strike.Nets > 0)
                AddNets(strike.Nets);
            if (strike.AlbumAdd > 0 && Album && S.EnemyIndex >= 0)
                AddSpecimens(S.EnemyIndex, strike.AlbumAdd);
            if (strike.Bait > 0 && Bait)
                AddBait(strike.Bait);
            if (strike.DozeSeconds > 0)
                S.DozeTicks = System.Math.Max(S.DozeTicks, Ticks(strike.DozeSeconds));
            if (strike.LucidSeconds > 0)
                S.LucidTicks = System.Math.Max(S.LucidTicks, Ticks(strike.LucidSeconds));
        }

        public bool EnemyCaughtByParty { get; set; }

        public void Tick(double mousePx)
        {
            if (!On)
                return;
            WatchPrestige();
            if (S.SpawnPending)
                OnNewEnemy();
            if (Nets && mousePx > 0)
                Weave(mousePx);
            if (S.CaughtTicks > 0)
                S.CaughtTicks--;
            if (S.ProphecyTicks > 0)
                S.ProphecyTicks--;
            if (S.HornTicks > 0)
                S.HornTicks--;
            if (S.DozeTicks > 0)
                S.DozeTicks--;
            if (S.LucidTicks > 0)
                S.LucidTicks--;
            if (!Nets)
            {
                S.Nets = 0;
                S.CaughtTicks = 0;
            }
            if (!Bait)
            {
                S.Bait = 0;
                S.SpiritsPending = 0;
            }
            if (Nets && S.Caught && _state.FightingBoss && Flag(DreamcatcherStats.NetTimer) && !_state.SharedEnemy)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, 2);
            _state.TraitShareDamage = AlbumBonus(S.EnemyIndex) + (Album && S.EnemyIndex >= 0 && S.Album[S.EnemyIndex] > 0 ? Positive(DreamcatcherStats.AlbumParty) : 0);
        }

        public void OnKillRewarded()
        {
            if (!On)
                return;
            if (Album && S.EnemyIndex >= 0)
                CountKill();
            if (Nets && S.Caught && !_state.SharedEnemy)
            {
                if (_state.Rng.Chance(TrophyChance))
                    GrantTrophy();
                if (Flag(DreamcatcherStats.NetReturn))
                    AddNets(1);
            }
            if (!S.SpiritAlive || _state.SharedEnemy)
                return;
            S.SpiritAlive = false;
            GrantSpiritLoot(false, true);
        }

        public void GrantTrophy()
        {
            S.Trophies++;
            _drops?.GiveTrophy();
        }

        public void GrantSpiritLoot(bool rich, bool own)
        {
            S.SpiritKills++;
            var scale = (rich ? _rules.SpiritRich : 1) * (own && Bait ? SpiritLoot : 1);
            _drops?.GiveSpiritLoot((int)System.Math.Round(_rules.SpiritGems * scale), (long)System.Math.Round(_rules.SpiritShards * scale));
            _loot?.Chest(_state.Stage, _rules.SpiritChest * scale);
            if (!own || !Bait || !Flag(DreamcatcherStats.SpiritGift))
                return;
            _state.PendingResource += _rules.SpiritGift;
            _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, _rules.SpiritGift);
        }

        private void WatchPrestige()
        {
            if (S.SeenPrestiges == _state.Prestiges)
                return;
            S.SeenPrestiges = _state.Prestiges;
            if (Flag(DreamcatcherStats.AlbumKeep))
                S.HalveAlbum();
            else
                S.ClearAlbum();
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            S.SpiritAlive = false;
            S.CaughtTicks = 0;
            S.HornTicks = 0;
            S.DozeTicks = 0;
            EnemyCaughtByParty = false;
            var enemy = _state.Enemy;
            S.EnemyIndex = enemy == null ? -1 : SpeciesIndex(enemy.Id);
            S.EnemyBiome = enemy == null ? -1 : BiomeIndex(enemy.Biome);
            if (!_state.EnemyAlive)
                return;
            var spirit = _state.EnemySpirit;
            if (Bait && S.SpiritsPending > 0 && _state.EnemyType == EnemyType.Normal && !_state.SharedEnemy)
            {
                S.SpiritsPending--;
                S.SpiritAlive = true;
                S.Spirits++;
                _state.EnemySpirit = true;
                _state.ScaleEnemyHp(SpiritHp);
                spirit = true;
            }
            if (spirit && Bait && Flag(DreamcatcherStats.SpiritHorn))
                Horn();
            if (Nets && S.Nets > 0 && !spirit && (_state.EnemyType == EnemyType.Normal || Flag(DreamcatcherStats.NetBoss)))
                Catch();
            if (Album && _state.FightingBoss && S.EnemyBiome >= 0 && !S.Prophesied[S.EnemyBiome] && PageFull(S.EnemyBiome))
                Prophesy();
        }

        private void Weave(double px)
        {
            S.NetPx += px;
            var per = NetPx;
            while (S.NetPx >= per)
            {
                S.NetPx -= per;
                if (S.Nets < NetMax)
                    S.Nets++;
            }
            if (S.Nets >= NetMax)
                S.NetPx = 0;
        }

        private void AddNets(int nets)
        {
            if (Nets)
                S.Nets = System.Math.Min(NetMax, S.Nets + nets);
        }

        private void Catch()
        {
            S.Nets--;
            S.Catches++;
            var seconds = NetSeconds;
            S.CaughtTicks = Ticks(seconds);
            var timer = _state.FightingBoss && Flag(DreamcatcherStats.NetTimer) ? 1 : 0;
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(NetEffect, NetStrength, seconds, timer);
        }

        private void Prophesy()
        {
            S.Prophesied[S.EnemyBiome] = true;
            S.Prophecies++;
            S.ProphecyTicks = Ticks(_rules.ProphecySeconds);
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(ProphecyEffect, ProphecyPower, _rules.ProphecySeconds);
        }

        private void Horn()
        {
            S.HornTicks = Ticks(_rules.HornSeconds);
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(HornEffect, _rules.HornPower, _rules.HornSeconds);
        }

        private void CountKill()
        {
            var allies = Flag(DreamcatcherStats.AlbumAllies);
            if (allies && !_state.SharedEnemy)
                return;
            AddSpecimens(S.EnemyIndex, Flag(DreamcatcherStats.AlbumDouble) || allies ? 2 : 1);
        }

        private void AddSpecimens(int index, int count)
        {
            S.Album[index] += count;
            S.Specimens += count;
        }

        private void AddBait(int bait)
        {
            S.Bait += bait;
            var hits = BaitHits;
            while (S.Bait >= hits)
            {
                S.Bait -= hits;
                CallSpirit(Flag(DreamcatcherStats.SpiritTwin) && _state.Rng.Chance(_rules.TwinChance) ? 2 : 1);
            }
        }

        private void CallSpirit(int count)
        {
            if (_state.SharedEnemy)
            {
                _state.TraitSpiritCalls += count;
                _state.TraitSpiritHp = SpiritHp;
                return;
            }
            S.SpiritsPending += count;
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
