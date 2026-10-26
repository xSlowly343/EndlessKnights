using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DropSystem
    {
        private static readonly string[] GearColors = { "red", "green", "blue" };
        private static readonly DropTable AnyRarity = new DropTable(1, ItemRarity.Simple, ItemRarity.Rift);
        private const int PendingCapacity = 512;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly EventQueue _events;
        private readonly WeaponSystem _weapons;
        private readonly double[] _rarityWeights = new double[DropRules.RarityCount];
        private readonly List<GemDef>[] _gemPools = { new List<GemDef>(), new List<GemDef>(), new List<GemDef>() };
        private readonly List<WeaponType> _types = new List<WeaponType>();
        private readonly List<WeaponBase> _bases = new List<WeaponBase>();
        private readonly PendingDrop[] _pending = new PendingDrop[PendingCapacity];
        private int _pendingCount;
        private DropRules _rules = null!;
        private StageTable _stages = null!;

        public DropSystem(DropRules rules, StageTable stages, GemCatalog gems, WeaponSystem weapons, GameState state, StatSheet hero, EventQueue events)
        {
            _state = state;
            _hero = hero;
            _events = events;
            _weapons = weapons;
            UseData(rules, stages, gems);
        }

        public DropRules Rules => _rules;
        public int DailyLimit => (int)System.Math.Min(_rules.Daily.MaxLimit, System.Math.Floor(_hero.Value(CombatStats.DailyItems, _rules.Daily.Limit)));
        public int DailyLeft => System.Math.Max(0, DailyLimit - _state.Drops.DailyItems);
        public int BagCount => _state.Weapons.Bag.Count + _state.Drops.Gear.Count;
        public bool BagFull => BagCount >= _rules.BagSize;

        public void UseData(DropRules rules, StageTable stages, GemCatalog gems)
        {
            _rules = rules;
            _stages = stages;
            foreach (var pool in _gemPools)
                pool.Clear();
            foreach (var gem in gems.All)
            {
                if (Drops(gem))
                    _gemPools[(int)gem.Kind].Add(gem);
            }
        }

        private static bool Drops(GemDef gem) =>
            !gem.Locked && gem.Ascendancy == null && (gem.Kind != GemKind.Skill || gem.Skill?.Weapon == null);

        public void SetDay(long day)
        {
            var drops = _state.Drops;
            if (drops.Day == day)
                return;
            drops.Day = day;
            drops.DailyItems = 0;
        }

        public int BiomeNumber(long stage) => (int)System.Math.Min(int.MaxValue, (stage - 1) / _stages.BiomeLength + 1);

        public void OnKill(EnemyType type)
        {
            var bonus = System.Math.Max(0, _hero.Value(CombatStats.GemDrop, 1));
            RollGem(GemKind.Skill, type, bonus);
            RollGem(GemKind.Support, type, bonus);
            RollGem(GemKind.Trigger, type, bonus);
        }

        private void RollGem(GemKind kind, EnemyType type, double bonus)
        {
            var chance = _rules.GemChance(kind).For(type) * bonus;
            if (chance <= 0 || _gemPools[(int)kind].Count == 0 || !_state.Drops.Rng.Chance(chance))
                return;
            Push(PendingDrop.OfGem(PickGem(_gemPools[(int)kind], preferNew: false)));
        }

        public void OnBossDefeated(long stage, EnemyType type, long recordBefore, bool raisesRecord)
        {
            if (!raisesRecord)
                return;
            if (stage >= recordBefore)
                GiveRecordItems(stage, type);
            if (_rules.Daily.Allows(stage, recordBefore))
                RollDaily(stage, DropSource.Daily, _rules.Daily.Chance);
        }

        public void ApplyOffline(OfflineResult result)
        {
            var record = _state.RecordStage;
            for (var stage = result.StageBefore; stage < result.StageAfter && DailyLeft > 0; stage++)
            {
                if (_rules.Daily.Allows(stage, record))
                    RollDaily(stage, DropSource.Offline, _rules.Daily.Chance * _rules.Daily.OfflineChance);
            }
            Flush();
        }

        public int Pending => _pendingCount;

        public void Flush()
        {
            for (var i = 0; i < _pendingCount; i++)
            {
                var drop = _pending[i];
                _pending[i] = default;
                if (drop.Gem != null)
                    GiveGem(drop.Gem);
                else
                    Deliver(drop);
            }
            _pendingCount = 0;
        }

        private void Push(PendingDrop drop)
        {
            if (_pendingCount == _pending.Length)
                Flush();
            _pending[_pendingCount++] = drop;
        }

        private void GiveRecordItems(long stage, EnemyType type)
        {
            var guarantee = _rules.GuaranteeAt(stage);
            if (guarantee != null)
                Push(Guaranteed(guarantee, stage));
            else
                GiveRolled(_rules.Record, stage, DropSource.Record);
            if (type == EnemyType.Guardian)
                GiveRolled(_rules.Guardian, stage, DropSource.Guardian);
            else if (_rules.IsSpecialBoss(stage))
                GiveRolled(_rules.SpecialBoss, stage, DropSource.SpecialBoss);
            if (stage == _rules.FirstTriggerStage && _gemPools[(int)GemKind.Trigger].Count > 0)
                Push(PendingDrop.OfGem(PickGem(_gemPools[(int)GemKind.Trigger], preferNew: true)));
        }

        private void RollDaily(long stage, DropSource source, double chance)
        {
            if (DailyLeft <= 0)
                return;
            var quantity = System.Math.Max(0, _hero.Value(CombatStats.ItemQuantity, 1));
            if (!_state.Drops.Rng.Chance(System.Math.Min(1, chance * quantity)))
                return;
            _state.Drops.DailyItems++;
            Push(Rolled(AnyRarity, stage, source));
        }

        private void GiveRolled(DropTable table, long stage, DropSource source)
        {
            for (var i = 0; i < table.Items; i++)
                Push(Rolled(table, stage, source));
        }

        private PendingDrop Rolled(DropTable table, long stage, DropSource source)
        {
            var rng = _state.Drops.Rng;
            var rarity = RollRarity(table, BiomeNumber(stage), rng);
            var slot = RollSlot(rng, includeWeapon: true);
            return slot == ItemSlot.Weapon
                ? PendingDrop.Weapon(source, stage, rarity, PickType(rng))
                : PendingDrop.Gear(source, stage, slot, rarity, RandomColor(slot, rng));
        }

        public ItemRarity RollRarity(DropTable table, int biomeNumber, Rng rng)
        {
            var ceiling = _rules.CeilingFor(biomeNumber);
            var top = table.MaxRarity < ceiling ? table.MaxRarity : ceiling;
            var bottom = table.MinRarity > top ? top : table.MinRarity;
            var find = System.Math.Max(0, _hero.Value(CombatStats.ItemFind, 1));
            var total = 0.0;
            for (var rarity = 0; rarity < _rarityWeights.Length; rarity++)
                _rarityWeights[rarity] = 0;
            for (var rarity = (int)bottom; rarity <= (int)table.MaxRarity; rarity++)
            {
                var weight = _rules.RarityWeight((ItemRarity)rarity) * (rarity > (int)ItemRarity.Simple ? find : 1);
                _rarityWeights[System.Math.Min(rarity, (int)top)] += weight;
                total += weight;
            }
            if (total <= 0)
                return bottom;
            var roll = rng.NextDouble() * total;
            for (var rarity = (int)bottom; rarity <= (int)top; rarity++)
            {
                roll -= _rarityWeights[rarity];
                if (roll < 0)
                    return (ItemRarity)rarity;
            }
            return top;
        }

        private ItemSlot RollSlot(Rng rng, bool includeWeapon)
        {
            var weights = _rules.SlotWeights;
            var total = 0.0;
            for (var slot = 0; slot < weights.Count; slot++)
                total += Weight(weights, slot, includeWeapon);
            var roll = rng.NextDouble() * total;
            for (var slot = 0; slot < weights.Count; slot++)
            {
                roll -= Weight(weights, slot, includeWeapon);
                if (roll < 0)
                    return (ItemSlot)slot;
            }
            return ItemSlot.Ring;
        }

        private static double Weight(IReadOnlyList<double> weights, int slot, bool includeWeapon) =>
            !includeWeapon && slot == (int)ItemSlot.Weapon ? 0 : weights[slot];

        private WeaponType PickType(Rng rng)
        {
            var catalog = _weapons.Catalog;
            var types = catalog.Types;
            var own = OwnType;
            var total = 0.0;
            for (var i = 0; i < types.Count; i++)
                total += types[i] == own ? catalog.OwnTypeWeight : catalog.OtherTypeWeight;
            var roll = rng.NextDouble() * total;
            for (var i = 0; i < types.Count; i++)
            {
                roll -= types[i] == own ? catalog.OwnTypeWeight : catalog.OtherTypeWeight;
                if (roll < 0)
                    return types[i];
            }
            return own;
        }

        private WeaponType OwnType
        {
            get
            {
                var types = _weapons.Catalog.Types;
                for (var i = 0; i < types.Count; i++)
                {
                    if (types[i].HeroClass == _state.Class)
                        return types[i];
                }
                return types[0];
            }
        }

        private WeaponItem RollWeapon(WeaponType type, ItemRarity rarity, long stage, Rng rng)
        {
            var catalog = _weapons.Catalog;
            var biome = BiomeNumber(stage);
            _bases.Clear();
            foreach (var weaponBase in catalog.Bases)
            {
                if (weaponBase.Type == type && weaponBase.FromBiome <= biome)
                    _bases.Add(weaponBase);
            }
            if (_bases.Count == 0)
                return catalog.Roll(catalog.StarterFor(type.HeroClass), rarity, rng);
            _bases.Sort((a, b) => a.FromBiome.CompareTo(b.FromBiome));
            var total = 0.0;
            for (var i = 0; i < _bases.Count; i++)
                total += BaseWeight(catalog, i);
            var roll = rng.NextDouble() * total;
            for (var i = 0; i < _bases.Count; i++)
            {
                roll -= BaseWeight(catalog, i);
                if (roll < 0)
                    return catalog.Roll(_bases[i], rarity, rng);
            }
            return catalog.Roll(_bases[_bases.Count - 1], rarity, rng);
        }

        private static double BaseWeight(WeaponCatalog catalog, int index)
        {
            var weights = catalog.BaseWeights;
            return weights.Count == 0 ? 1 : weights[System.Math.Min(index, weights.Count - 1)];
        }

        private static string? RandomColor(ItemSlot slot, Rng rng) =>
            slot == ItemSlot.Amulet ? null : GearColors[rng.NextInt(0, GearColors.Length)];

        private PendingDrop Guaranteed(DropGuarantee guarantee, long stage)
        {
            var rng = _state.Drops.Rng;
            var rarity = guarantee.Rarity;
            var slot = guarantee.Slot ?? EmptySlot(rng);
            if (slot != ItemSlot.Weapon)
            {
                var color = guarantee.Color == GuaranteeColor.Own && slot != ItemSlot.Amulet ? OwnType.Color : RandomColor(slot, rng);
                return PendingDrop.Gear(DropSource.Guarantee, stage, slot, rarity, color);
            }
            var type = guarantee.Color switch
            {
                GuaranteeColor.Own => OwnType,
                GuaranteeColor.OwnColorOtherClass => OtherClassOfOwnColor(rng),
                _ => guarantee.ToEmptySlot ? OwnType : PickType(rng)
            };
            return PendingDrop.Weapon(DropSource.Guarantee, stage, rarity, type);
        }

        private ItemSlot EmptySlot(Rng rng)
        {
            var weapons = _state.Weapons;
            return weapons.Right == null || weapons.Left == null ? ItemSlot.Weapon : RollSlot(rng, includeWeapon: false);
        }

        private WeaponType OtherClassOfOwnColor(Rng rng)
        {
            var own = OwnType;
            var types = _weapons.Catalog.Types;
            _types.Clear();
            for (var i = 0; i < types.Count; i++)
            {
                if (types[i] != own && types[i].Color == own.Color)
                    _types.Add(types[i]);
            }
            if (_types.Count == 0)
            {
                for (var i = 0; i < types.Count; i++)
                {
                    if (types[i] != own)
                        _types.Add(types[i]);
                }
            }
            return _types.Count == 0 ? own : _types[rng.NextInt(0, _types.Count)];
        }

        private void Deliver(PendingDrop drop)
        {
            var drops = _state.Drops;
            drops.ItemsFound++;
            var rarity = drop.Rarity;
            var weapon = drop.Type != null ? RollWeapon(drop.Type, rarity, drop.Stage, drops.Rng) : null;
            var gear = weapon == null ? new GearItem(drop.Slot, rarity, BiomeNumber(drop.Stage), drop.Color) : null;
            var salvaged = BagFull || ShouldSalvage(weapon, rarity);
            var dust = salvaged ? _rules.DustFor(rarity) : 0;
            drops.Dust += dust;
            if (salvaged)
                drops.SalvagedItems++;
            else if (weapon != null)
                _weapons.Give(weapon);
            else
                drops.Gear.Add(gear!);
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(drop.Source, drop.Stage, weapon, gear, salvaged, dust)));
        }

        private bool ShouldSalvage(WeaponItem? weapon, ItemRarity rarity)
        {
            var limit = _state.Drops.SalvageUpTo;
            if (limit == null || rarity > limit.Value)
                return false;
            return weapon == null || !BetterThanHeld(rarity);
        }

        private bool BetterThanHeld(ItemRarity rarity)
        {
            var weapons = _state.Weapons;
            return weapons.Right == null || weapons.Left == null || rarity > weapons.Right.Rarity || rarity > weapons.Left.Rarity;
        }

        private GemDef PickGem(List<GemDef> pool, bool preferNew)
        {
            var rng = _state.Drops.Rng;
            if (preferNew)
            {
                var start = rng.NextInt(0, pool.Count);
                for (var i = 0; i < pool.Count; i++)
                {
                    var gem = pool[(start + i) % pool.Count];
                    if (!_state.OwnedGems.Contains(gem))
                        return gem;
                }
            }
            return pool[rng.NextInt(0, pool.Count)];
        }

        private void GiveGem(GemDef gem)
        {
            _state.OwnedGems.Add(gem);
            _events.Add(GameEvent.ForSkill(GameEventKind.GemDropped, _state.Tick, gem));
        }
    }
}
