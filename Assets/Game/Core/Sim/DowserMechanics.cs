using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DowserMechanics : IKillRewardListener
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private DowserRules _rules;
        private DropSystem? _drops;
        private LootSystem? _loot;

        public DowserMechanics(DowserRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(DowserRules rules) => _rules = rules;

        public void UseLoot(DropSystem drops, LootSystem loot)
        {
            _drops = drops;
            _loot = loot;
        }

        private DowserState S => _state.Dowser;
        private bool On => _state.Is(HeroClass.Dowser);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        public bool Vein => On && VeinPower > 0;
        public bool Luck => On && LuckPower > 0;
        public bool Artel => On && ArtelPower > 0;

        public double VeinPower => Positive(DowserStats.VeinPower);
        public double LuckPower => Positive(DowserStats.LuckPower);
        public double ArtelPower => Positive(DowserStats.ArtelPower);

        public bool VeinHere => Vein && (!_state.FightingBoss || Flag(DowserStats.VeinBoss));

        public int VeinRange => Flag(DowserStats.VeinNear) ? _rules.VeinNearRange : _rules.VeinRange;

        public double VeinGold => VeinPower * (Flag(DowserStats.VeinTwice) ? 0.5 : 1) * (_state.FightingBoss ? 2 : 1);

        public double VeinCloseness => !VeinHere || S.VeinsStruck >= Veins ? 0 : System.Math.Max(0, 1 - (NextVein - S.Hits) / VeinRange);

        public double LuckChance => System.Math.Min(_rules.LuckMax, S.Luck * (Flag(DowserStats.LuckBoss) && _state.FightingBoss ? _rules.LuckBoss : 1));

        public int ArtelNuggets => System.Math.Max(_rules.ArtelNuggetsMin, (int)System.Math.Round(_rules.ArtelNuggets + _hero.Value(DowserStats.ArtelNuggets, 0)));

        public double ArtelChest => ArtelPower * (Flag(DowserStats.ArtelItem) ? 0.7 : 1);

        public double ArtelItemChance => Flag(DowserStats.ArtelItem) ? _rules.ArtelItemRich : _rules.ArtelItemChance;

        private int Veins => Flag(DowserStats.VeinTwice) ? 2 : 1;

        private int NextVein => S.VeinsStruck == 0 ? S.VeinAt : S.SecondVeinAt;

        public double StrikeMultiplier() => Artel ? 1 - System.Math.Min(0.9, System.Math.Max(0, _rules.ArtelPenalty + _hero.Value(DowserStats.ArtelPenalty, 0))) : 1;

        public bool ForcesCrit => Vein && S.CritTicks > 0;

        public void AfterStrike()
        {
            if (!On)
                return;
            if (VeinHere)
                AddHits(1);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.VeinHits > 0 && VeinHere)
                AddHits(strike.VeinHits);
            if (strike.Luck > 0 && Luck)
                AddLuck(strike.Luck);
            if (strike.Nuggets > 0 && Artel)
                AddNuggets(strike.Nuggets, ArtelNuggets, ArtelChest, ArtelItemChance);
            if (strike.GoldHit > 0)
                TraitHit(strike.GoldHit * System.Math.Min(10, System.Math.Log10(System.Math.Max(10, _state.Gold.ToDouble())) / 10));
            if (strike.VeinGold > 0 && _state.EnemyAlive)
                _state.EnemyGoldBonus += strike.VeinGold;
        }

        public void Tick()
        {
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            if (S.CritTicks > 0)
                S.CritTicks--;
            if (Luck && Flag(DowserStats.LuckHour) && ++S.HourTicks >= Ticks(_rules.LuckHourSeconds))
            {
                S.HourTicks = 0;
                _drops?.GiveLimitUnique(_state.Stage);
            }
            if (Luck)
                _state.TraitShareGold = Flag(DowserStats.LuckGold) ? S.LuckGold : 0;
        }

        public void OnKillRewarded()
        {
            if (!On)
                return;
            if (Artel)
                AddNuggets(_state.FightingBoss && Flag(DowserStats.ArtelBoss) ? 2 : 1, ArtelNuggets, ArtelChest, ArtelItemChance);
            if (!Luck)
                return;
            if (_state.Rng.Chance(LuckChance))
                Find();
            else
                AddLuck(LuckPower * (1 + Positive(DowserStats.LuckParty)));
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            S.Hits = 0;
            S.VeinsStruck = 0;
            S.VeinAt = _state.Rng.NextInt(1, VeinRange + 1);
            S.SecondVeinAt = Flag(DowserStats.VeinTwice) ? _state.Rng.NextInt(S.VeinAt, VeinRange + 1) : 0;
        }

        private void AddHits(double hits)
        {
            if (!_state.EnemyAlive || S.VeinsStruck >= Veins)
                return;
            S.Hits += hits;
            while (S.VeinsStruck < Veins && S.Hits >= NextVein)
                StrikeVein();
        }

        private void StrikeVein()
        {
            S.VeinsStruck++;
            S.Veins++;
            if (Flag(DowserStats.VeinStrike))
                TraitHit(_rules.VeinStrike);
            if (Flag(DowserStats.VeinCrit))
                S.CritTicks = Ticks(_rules.VeinCritSeconds);
            if (Flag(DowserStats.VeinGem) && _state.Rng.Chance(_rules.VeinGemChance))
            {
                _drops?.GiveFind(_state.Stage);
                return;
            }
            var party = Flag(DowserStats.VeinParty);
            _state.EnemyGoldBonus += VeinGold * (party ? 0.5 : 1);
        }

        private void AddLuck(double amount)
        {
            S.Luck = System.Math.Min(_rules.LuckMax, S.Luck + amount);
            if (Flag(DowserStats.LuckGold))
                S.LuckGold = System.Math.Min(_rules.LuckGoldMax, S.LuckGold + _rules.LuckGoldPerKill);
        }

        private void Find()
        {
            S.Finds++;
            if (Flag(DowserStats.LuckUnique) && S.Finds % _rules.LuckUniqueEvery == 0)
                _drops?.GiveLimitUnique(_state.Stage);
            else
                _drops?.GiveFind(_state.Stage);
            if (Flag(DowserStats.LuckShards))
                _drops?.GiveShards(_rules.LuckShards);
            ResetLuck();
        }

        private void ResetLuck()
        {
            S.Luck = Flag(DowserStats.LuckKeep) ? S.Luck * 0.5 : 0;
            S.LuckGold = 0;
        }

        private void AddNuggets(int count, int every, double chest, double itemChance)
        {
            var nuggets = S.Nuggets + count;
            var chests = 0;
            while (nuggets >= System.Math.Max(1, every))
            {
                nuggets -= System.Math.Max(1, every);
                chests++;
            }
            S.Nuggets = nuggets;
            for (var i = 0; i < chests; i++)
                OpenChest(chest, itemChance);
        }

        private void OpenChest(double chest, double itemChance)
        {
            S.Chests++;
            _loot?.Chest(_state.Stage, chest);
            if (_state.Rng.Chance(itemChance))
                _drops?.GiveFind(_state.Stage);
            if (Flag(DowserStats.ArtelGem))
                _drops?.GiveTrophy();
            if (Flag(DowserStats.ArtelShards))
                _drops?.GiveShards(_rules.ArtelShards);
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Strike);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
