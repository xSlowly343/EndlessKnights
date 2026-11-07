using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum MountResult
    {
        Done,
        BadSlot,
        Maxed,
        Locked,
        NoScan,
        NoGold
    }

    public sealed class MountSystem
    {
        public const int WeaponSlot = 0;

        private readonly GameState _state;
        private readonly StageTable _stages;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private MountRules _rules;
        private double _appliedFull = -1;

        public MountSystem(MountRules rules, GameState state, StageTable stages, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _stages = stages;
            _layer = new ModifierLayer(hero);
            Refresh();
        }

        public MountRules Rules => _rules;
        public MountState State => _state.Mount;
        public System.Action? Changed { get; set; }

        public void UseRules(MountRules rules)
        {
            _rules = rules;
            _appliedFull = -1;
            Refresh();
        }

        public static int SlotOf(GearSlot slot) => (int)slot + 1;

        public int Level(int slot) => slot >= 0 && slot < MountRules.Slots ? State.Levels[slot] : 0;

        public double Factor(int slot) => _rules.Factor(Level(slot));

        public int Unlocked => _rules.Unlocked(_state.RecordStage);

        public int Lowest
        {
            get
            {
                var lowest = int.MaxValue;
                for (var i = 0; i < MountRules.Slots; i++)
                    lowest = System.Math.Min(lowest, State.Levels[i]);
                return lowest;
            }
        }

        public long ScanCost(int slot) => _rules.ScanCost(Level(slot));

        public BigNumber GoldCost(int slot)
        {
            var level = Level(slot);
            var stage = System.Math.Max(1, (long)(level + 1) * _rules.StagesPerLevel);
            return _stages.EnemyGold(stage) * (_rules.GoldKills * System.Math.Pow(_rules.GoldGrowth, level));
        }

        public MountResult Check(int slot)
        {
            if (slot < 0 || slot >= MountRules.Slots)
                return MountResult.BadSlot;
            var level = Level(slot);
            if (level >= _rules.MaxLevel)
                return MountResult.Maxed;
            if (level >= Unlocked)
                return MountResult.Locked;
            if (State.Scan + 1e-9 < ScanCost(slot))
                return MountResult.NoScan;
            if (_state.Gold < GoldCost(slot))
                return MountResult.NoGold;
            return MountResult.Done;
        }

        public MountResult Raise(int slot)
        {
            var result = Check(slot);
            if (result != MountResult.Done)
                return result;
            State.Scan -= ScanCost(slot);
            _state.Gold -= GoldCost(slot);
            State.Levels[slot]++;
            State.Cutters += _rules.CuttersPerLevel;
            Refresh();
            Changed?.Invoke();
            return MountResult.Done;
        }

        public void NoteSalvage(ItemDrop drop)
        {
            var rarity = drop.Weapon?.Rarity ?? drop.Gear?.Rarity;
            if (rarity != null)
                State.Scan += _rules.ScanFor((int)rarity.Value);
        }

        public void Refresh()
        {
            var full = _rules.FullDamage(Lowest);
            if (full == _appliedFull)
                return;
            _appliedFull = full;
            _modifiers.Clear();
            if (full > 0)
                _modifiers.Add(new Modifier(CombatStats.AdditionalDamage, ModifierKind.Flat, full, StatTags.None));
            _layer.Set(_modifiers);
        }
    }
}
