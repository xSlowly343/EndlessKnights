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
        private readonly GearSystem _gear;
        private readonly double[] _rarityWeights = new double[DropRules.RarityCount];
        private readonly List<GemDef>[] _gemPools = { new List<GemDef>(), new List<GemDef>(), new List<GemDef>() };
        private readonly List<WeaponType> _types = new List<WeaponType>();
        private readonly List<WeaponBase> _bases = new List<WeaponBase>();
        private readonly PendingDrop[] _pending = new PendingDrop[PendingCapacity];
        private int _pendingCount;
        private DropRules _rules = null!;
        private ItemCatalog _items = null!;
        private StageTable _stages = null!;

        public DropSystem(DropRules rules, StageTable stages, GemCatalog gems, ItemCatalog items, WeaponSystem weapons, GearSystem gear, GameState state, StatSheet hero, EventQueue events)
        {
            _state = state;
            _hero = hero;
            _events = events;
            _weapons = weapons;
            _gear = gear;
            UseData(rules, stages, gems, items);
        }

        public DropRules Rules => _rules;
        public int DailyLimit => (int)System.Math.Min(_rules.Daily.MaxLimit, System.Math.Floor(_hero.Value(CombatStats.DailyItems, _rules.Daily.Limit)));
        public int DailyLeft => System.Math.Max(0, DailyLimit - _state.Drops.DailyItems);
        public int BagCount => _state.Weapons.Bag.Count + _state.Drops.Gear.Count;
        public bool BagFull => BagCount >= _rules.BagSize;

        public void UseData(DropRules rules, StageTable stages, GemCatalog gems, ItemCatalog items)
        {
            _rules = rules;
            _items = items;
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

        public static bool Cuttable(GemDef gem) => Drops(gem);

        public IReadOnlyList<GemDef> PoolOf(GemKind kind) => _gemPools[(int)kind];

        public long Shards => _state.Drops.Shards;
        public int UncutGems => _state.Drops.UncutGems;
        public int DailyShardsLeft => System.Math.Max(0, _rules.Shards.DailyLimit - _state.Drops.DailyShards);
        public double UncutChance => System.Math.Min(1, System.Math.Max(0, _hero.Value(CombatStats.UncutGemChance, _rules.Shards.UncutShare)));

        public double CraftCost(SetDef set) => _items.CraftCost(set, _gear.CollectedPieces(set), _gear.Collected(set));

        public CraftResult CraftSetPiece(string setId, ItemSlot? chosen = null, string? weaponType = null)
        {
            if (!_items.TryGetSet(setId, out var set))
                return CraftResult.UnknownSet;
            if (Unlocks?.IsOpen(Feature.SetCraft) == false)
                return CraftResult.Locked;
            if (set.Rarity > _rules.CeilingAt(System.Math.Max(1, _state.RecordStage)))
                return CraftResult.AboveCeiling;
            if (BagFull)
                return CraftResult.BagFull;
            var complete = _gear.Collected(set);
            ItemSlot slot;
            if (complete)
            {
                if (chosen == null)
                    return CraftResult.NeedsSlot;
                if (!set.Has(chosen.Value))
                    return CraftResult.WrongSlot;
                slot = chosen.Value;
            }
            else
            {
                var missing = new List<ItemSlot>();
                foreach (var piece in set.Slots)
                {
                    if (!_state.SetCollection.Contains(SetDef.PieceKey(set.Id, piece)))
                        missing.Add(piece);
                }
                slot = missing[_state.Drops.Rng.NextInt(0, missing.Count)];
            }
            var cost = (long)System.Math.Ceiling(CraftCost(set));
            if (_state.Drops.Ingots < cost)
                return CraftResult.NotEnoughIngots;
            if (slot == ItemSlot.Weapon)
            {
                var weapon = CraftSetWeapon(set, weaponType);
                if (weapon == null)
                    return CraftResult.WrongSlot;
                _state.Drops.Ingots -= cost;
                _weapons.Give(weapon);
                _gear.NoteFound(set, ItemSlot.Weapon);
                _state.Drops.ItemsFound++;
                _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, weapon, null, false, 0)));
                return CraftResult.Crafted;
            }
            _state.Drops.Ingots -= cost;
            var item = _items.RollSetPiece(set, slot, null, set.Rarity, BiomeNumber(System.Math.Max(1, _state.RecordStage)), _state.Drops.Rng);
            _gear.NoteFound(item);
            _state.Drops.Gear.Add(item);
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, null, item, false, 0)));
            return CraftResult.Crafted;
        }

        private WeaponItem? CraftSetWeapon(SetDef set, string? chosenType)
        {
            var rng = _state.Drops.Rng;
            WeaponType type;
            if (chosenType != null)
            {
                if (!set.AllowsType(chosenType) || !_weapons.Catalog.TryGetType(chosenType, out type))
                    return null;
            }
            else
            {
                type = SetWeaponType(set, OwnType, rng);
            }
            var stage = System.Math.Max(1, _state.RecordStage);
            return RollWeapon(type, set.Rarity, stage, rng).InSet(set);
        }

        public DungeonRules? Dungeons { get; set; }

        private void RollKey(long stage, EnemyType type)
        {
            var rules = Dungeons;
            if (rules == null || stage < rules.KeysFromStage || Unlocks?.IsOpen(Feature.Dungeons) == false)
                return;
            var chance = type == EnemyType.Guardian ? rules.GuardianKeyChance : _rules.IsSpecialBoss(stage) ? rules.SpecialBossKeyChance : rules.BossKeyChance;
            if (chance > 0 && _state.Drops.Rng.Chance(chance))
                Push(PendingDrop.Key(stage));
        }

        private void DeliverKey(PendingDrop drop)
        {
            var rules = Dungeons!;
            var keys = _state.Dungeons.Keys;
            if (keys.Count >= rules.KeyBag)
                return;
            var rng = _state.Drops.Rng;
            var highest = rules.HighestTierFor(drop.Stage, _state.GatesCleared.Count > 0);
            if (highest < 1)
                return;
            var tier = System.Math.Max(1, highest - rng.NextInt(0, 2));
            var count = rng.NextInt(0, rules.MaxModifiers + 1);
            var chosen = new List<string>();
            for (var i = 0; i < count && chosen.Count < rules.Modifiers.Count; i++)
            {
                var modifier = rules.Modifiers[rng.NextInt(0, rules.Modifiers.Count)];
                if (!chosen.Contains(modifier.Id))
                    chosen.Add(modifier.Id);
            }
            keys.Add(new DungeonKey(tier, chosen));
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeyDropped, _state.Tick, tier));
        }

        public void GiveDungeonLoot(DungeonTier tier, double bonus)
        {
            var rng = _state.Drops.Rng;
            var scale = 1 + System.Math.Max(0, bonus);
            var amount = tier.Loot.Items * scale;
            var items = (int)System.Math.Floor(amount) + (rng.NextDouble() < amount - System.Math.Floor(amount) ? 1 : 0);
            for (var i = 0; i < items; i++)
                Push(Rolled(tier.Loot, tier.Stage, DropSource.Dungeon));
            Flush();
            if (tier.UniqueChance > 0 && rng.NextDouble() < System.Math.Min(1, tier.UniqueChance * scale))
                GiveUnique(tier.Stage, DropSource.Dungeon);
            _state.Drops.Shards += (long)System.Math.Round(tier.Shards * scale);
        }

        public void GiveLimitUnique(long stage) => GiveUnique(stage, DropSource.Limit);

        private void GiveUnique(long stage, DropSource source)
        {
            var gear = _items.Uniques;
            var weapons = _weapons.Catalog.Uniques;
            var total = gear.Count + weapons.Count;
            if (total == 0)
                return;
            var rng = _state.Drops.Rng;
            var pick = rng.NextInt(0, total);
            WeaponItem? weapon = null;
            GearItem? item = null;
            if (pick < gear.Count)
            {
                item = new GearItem(gear[pick], BiomeNumber(stage));
                _state.Drops.Gear.Add(item);
            }
            else
            {
                weapon = _weapons.Give(_weapons.Catalog.RollUnique(weapons[pick - gear.Count], rng));
            }
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(source, stage, weapon, item, false, 0)));
        }

        public bool BuyUncut()
        {
            if (Unlocks?.IsOpen(Feature.Cutting) == false)
                return false;
            var drops = _state.Drops;
            var cost = _rules.Shards.UncutCost;
            if (drops.Shards < cost)
                return false;
            drops.Shards -= cost;
            drops.UncutGems++;
            return true;
        }

        public Unlocks? Unlocks { get; set; }

        public CutResult Cut(GemDef gem)
        {
            if (Unlocks?.IsOpen(Feature.Cutting) == false)
                return CutResult.Locked;
            if (_state.Drops.UncutGems <= 0)
                return CutResult.NoUncut;
            if (!Cuttable(gem))
                return CutResult.NotCuttable;
            _state.Drops.UncutGems--;
            GiveGem(gem);
            return CutResult.Cut;
        }

        public void SetDay(long day)
        {
            var drops = _state.Drops;
            if (drops.Day == day)
                return;
            drops.Day = day;
            drops.DailyItems = 0;
            drops.DailyShards = 0;
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
            if (_state.Drops.Rng.Chance(UncutChance))
            {
                GiveUncut();
                return;
            }
            Push(PendingDrop.OfGem(PickGem(_gemPools[(int)kind], preferNew: false)));
        }

        public void OnBossDefeated(long stage, EnemyType type, long recordBefore, bool raisesRecord)
        {
            if (!raisesRecord)
                return;
            GiveShards(stage, type, recordBefore);
            if (stage >= recordBefore)
                GiveRecordItems(stage, type);
            RollKey(stage, type);
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
            for (var stage = result.StageBefore; stage < result.StageAfter && DailyShardsLeft > 0; stage++)
            {
                if (_rules.Daily.Allows(stage, record) && _state.Drops.Rng.Chance(_rules.Daily.OfflineChance))
                    GiveDailyShards(ShardsFor(stage, EnemyType.Boss));
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
                else if (drop.IsJewel)
                    DeliverJewel(drop);
                else if (drop.IsKey)
                    DeliverKey(drop);
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
            {
                GiveRolled(_rules.Guardian, stage, DropSource.Guardian);
                RollJewel(_rules.Guardian, stage, DropSource.Guardian);
            }
            else if (_rules.IsSpecialBoss(stage))
            {
                GiveRolled(_rules.SpecialBoss, stage, DropSource.SpecialBoss);
                RollJewel(_rules.SpecialBoss, stage, DropSource.SpecialBoss);
            }
            if (stage == _rules.FirstTriggerStage && _gemPools[(int)GemKind.Trigger].Count > 0)
                Push(PendingDrop.OfGem(PickGem(_gemPools[(int)GemKind.Trigger], preferNew: true)));
        }

        private void RollJewel(DropTable table, long stage, DropSource source)
        {
            var chance = _rules.JewelChanceFor(source);
            var rng = _state.Drops.Rng;
            if (chance <= 0 || Unlocks?.IsOpen(Feature.Jewels) == false || !rng.Chance(chance))
                return;
            Push(PendingDrop.Jewel(source, stage, RollRarity(table, stage, rng)));
        }

        private void DeliverJewel(PendingDrop drop)
        {
            var jewel = _items.RollJewel(drop.Rarity, BiomeNumber(drop.Stage), _state.Drops.Rng);
            var jewels = _state.Jewels;
            jewels.Found++;
            if (jewels.Bag.Count >= _rules.JewelBagSize)
                _state.Drops.Dust += _rules.DustFor(drop.Rarity);
            else
                jewels.Bag.Add(jewel);
            _events.Add(GameEvent.WithAmount(GameEventKind.JewelDropped, _state.Tick, BigNumber.FromDouble((int)drop.Rarity), drop.Stage));
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
            var rarity = RollRarity(table, stage, rng);
            var slot = RollSlot(rng, includeWeapon: true);
            return slot == ItemSlot.Weapon
                ? PendingDrop.Weapon(source, stage, rarity, PickType(rng))
                : PendingDrop.Gear(source, stage, slot, rarity, RandomColor(slot, rng));
        }

        public ItemRarity RollRarity(DropTable table, long stage, Rng rng)
        {
            var ceiling = _rules.CeilingAt(stage);
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
            WeaponType? own = _state.ClassChosen ? OwnType : null;
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
            return own ?? types[types.Count - 1];
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

        private WeaponItem WeaponFor(PendingDrop drop, ItemRarity rarity, Rng rng)
        {
            var chance = _rules.UniqueChanceFor(drop.Source);
            if (chance > 0 && rng.NextDouble() < chance)
            {
                var unique = _weapons.Catalog.PickUnique(_rules.CeilingAt(drop.Stage), rng);
                if (unique != null)
                    return _weapons.Catalog.RollUnique(unique, rng);
            }
            var setChance = _rules.SetChanceFor(drop.Source);
            if (setChance > 0 && rng.NextDouble() < setChance)
            {
                var set = _items.PickSet(ItemSlot.Weapon, rarity, rng);
                if (set != null)
                    return RollWeapon(SetWeaponType(set, drop.Type!, rng), rarity, drop.Stage, rng).InSet(set);
            }
            return RollWeapon(drop.Type!, rarity, drop.Stage, rng);
        }

        private WeaponType SetWeaponType(SetDef set, WeaponType rolled, Rng rng)
        {
            if (set.AllowsType(OwnType.Id))
                return OwnType;
            if (set.AllowsType(rolled.Id))
                return rolled;
            var types = _weapons.Catalog.Types;
            _types.Clear();
            for (var i = 0; i < types.Count; i++)
            {
                if (set.AllowsType(types[i].Id))
                    _types.Add(types[i]);
            }
            return _types.Count == 0 ? rolled : _types[rng.NextInt(0, _types.Count)];
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
            if (weapons.Right == null || weapons.Left == null)
                return ItemSlot.Weapon;
            var worn = _state.Gear.Worn;
            var weakest = -1;
            for (var i = 0; i < worn.Length; i++)
            {
                if (worn[i] == null)
                    return ItemOf((GearSlot)i);
                if (weakest < 0 || worn[i]!.Rarity < worn[weakest]!.Rarity)
                    weakest = i;
            }
            return weakest < 0 ? RollSlot(rng, includeWeapon: false) : ItemOf((GearSlot)weakest);
        }

        private static ItemSlot ItemOf(GearSlot slot) => slot switch
        {
            GearSlot.Helmet => ItemSlot.Helmet,
            GearSlot.Chest => ItemSlot.Chest,
            GearSlot.Gloves => ItemSlot.Gloves,
            GearSlot.Boots => ItemSlot.Boots,
            GearSlot.Amulet => ItemSlot.Amulet,
            _ => ItemSlot.Ring
        };

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
            var weapon = drop.Type != null ? WeaponFor(drop, rarity, drops.Rng) : null;
            var gear = weapon == null ? RollGear(drop, rarity, drops.Rng) : null;
            if (gear != null)
                _gear.NoteFound(gear);
            if (weapon?.Set != null)
                _gear.NoteFound(weapon.Set, ItemSlot.Weapon);
            var salvaged = BagFull || ShouldSalvage(weapon, gear, rarity);
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

        private GearItem RollGear(PendingDrop drop, ItemRarity rarity, Rng rng)
        {
            var itemLevel = BiomeNumber(drop.Stage);
            var uniqueChance = _rules.UniqueChanceFor(drop.Source);
            if (uniqueChance > 0 && rng.NextDouble() < uniqueChance)
            {
                var unique = _items.PickUnique(drop.Slot, _rules.CeilingAt(drop.Stage), rng);
                if (unique != null)
                    return new GearItem(unique, itemLevel);
            }
            var chance = _rules.SetChanceFor(drop.Source);
            if (chance > 0 && rng.NextDouble() < chance)
            {
                var set = _items.PickSet(drop.Slot, rarity, rng);
                if (set != null)
                    return _items.RollSetPiece(set, drop.Slot, drop.Color, rarity, itemLevel, rng);
            }
            return _items.Roll(drop.Slot, drop.Color, rarity, itemLevel, rng);
        }

        private bool ShouldSalvage(WeaponItem? weapon, GearItem? gear, ItemRarity rarity)
        {
            var limit = _state.Drops.SalvageUpTo;
            if (limit == null || rarity > limit.Value)
                return false;
            if (weapon != null)
                return weapon.Unique == null && !BetterThanHeld(rarity);
            return gear == null || (gear.Unique == null && !BetterThanWorn(gear));
        }

        private bool BetterThanWorn(GearItem gear)
        {
            if (gear.Slot == ItemSlot.Ring)
                return Weaker(_state.Gear.In(GearSlot.RingLeft), gear) || Weaker(_state.Gear.In(GearSlot.RingRight), gear);
            return Weaker(_state.Gear.In(GearState.SlotFor(gear.Slot)), gear);
        }

        private static bool Weaker(GearItem? worn, GearItem candidate) => worn == null || candidate.Rarity > worn.Rarity;

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

        private void GiveShards(long stage, EnemyType type, long recordBefore)
        {
            var amount = ShardsFor(stage, type);
            if (stage >= recordBefore)
                AddShards(amount);
            else if (_rules.Daily.Allows(stage, recordBefore))
                GiveDailyShards(amount);
        }

        private long ShardsFor(long stage, EnemyType type)
        {
            var drops = _state.Drops;
            var amount = _rules.Shards.ForBoss(type, _rules.IsSpecialBoss(stage));
            drops.ShardCarry += System.Math.Max(0, _hero.Value(CombatStats.ShardDrop, amount));
            var whole = (long)System.Math.Floor(drops.ShardCarry);
            drops.ShardCarry -= whole;
            return whole;
        }

        private void GiveDailyShards(long amount)
        {
            var given = System.Math.Min(amount, DailyShardsLeft);
            _state.Drops.DailyShards += (int)given;
            AddShards(given);
        }

        private void AddShards(long amount)
        {
            if (amount <= 0)
                return;
            _state.Drops.Shards += amount;
            _state.Drops.ShardsFound += amount;
            _events.Add(GameEvent.WithAmount(GameEventKind.ShardsGained, _state.Tick, BigNumber.FromDouble(amount), _state.Stage));
        }

        private void GiveUncut()
        {
            _state.Drops.UncutGems++;
            _state.Drops.UncutFound++;
            _events.Add(GameEvent.ForStage(GameEventKind.UncutGemDropped, _state.Tick, _state.Stage));
        }

        private void GiveGem(GemDef gem)
        {
            _state.OwnedGems.Add(gem);
            _events.Add(GameEvent.ForSkill(GameEventKind.GemDropped, _state.Tick, gem));
        }
    }
}
