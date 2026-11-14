using Game.Core.Content.Rift;
using System;
using System.Collections.Generic;
using Game.Core.Content;
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
        private readonly List<UniqueWeaponDef> _uniqueWeapons = new List<UniqueWeaponDef>();
        private readonly List<WeaponBase> _bases = new List<WeaponBase>();
        private readonly PendingDrop[] _pending = new PendingDrop[PendingCapacity];
        private int _pendingCount;
        private DropRules _rules = null!;
        private ItemCatalog _items = null!;
        private StageTable _stages = null!;

        public DropSystem(DropRules rules, StageTable stages, ItemCatalog items, WeaponSystem weapons, GearSystem gear, GameState state, StatSheet hero, EventQueue events)
        {
            _state = state;
            _hero = hero;
            _events = events;
            _weapons = weapons;
            _gear = gear;
            UseData(rules, stages, items);
        }

        public DropRules Rules => _rules;
        public int DailyLimit => (int)System.Math.Min(_rules.Daily.MaxLimit, System.Math.Floor(_hero.Value(CombatStats.DailyItems, _rules.Daily.Limit)));
        public int DailyLeft => System.Math.Max(0, DailyLimit - _state.Drops.DailyItems);
        public int BagCount => _state.Weapons.Bag.Count + _state.Drops.Gear.Count;
        public bool BagFull => BagCount >= _rules.BagSize;

        public int BagRoom => System.Math.Max(0, _rules.BagSize - BagCount);

        public void UseData(DropRules rules, StageTable stages, ItemCatalog items)
        {
            _rules = rules;
            _items = items;
            _stages = stages;
        }

        public long Shards => _state.Drops.Shards;
        public int DailyShardsLeft => System.Math.Max(0, _rules.Shards.DailyLimit - _state.Drops.DailyShards);

        public double CraftCost(SetDef set) => _items.CraftCost(set, _gear.CollectedPieces(set), _gear.Collected(set));

        public CraftResult CraftSetPiece(string setId, ItemSlot? chosen = null, string? weaponType = null, bool secondRing = false)
        {
            if (!_items.TryGetSet(setId, out var set))
                return CraftResult.UnknownSet;
            if (!set.Ordinary)
                return CraftResult.NotCraftable;
            if (Unlocks?.IsOpen(Feature.SetCraft) == false)
                return CraftResult.Locked;
            if (set.Rarity > _rules.CeilingAt(System.Math.Max(1, CraftDepth)))
                return CraftResult.AboveCeiling;
            if (BagFull)
                return CraftResult.BagFull;
            var complete = _gear.Collected(set);
            SetPiece piece;
            if (complete)
            {
                if (chosen == null)
                    return CraftResult.NeedsSlot;
                if (!set.Has(chosen.Value) || (secondRing && !(chosen.Value == ItemSlot.Ring && set.HasSecondRing)))
                    return CraftResult.WrongSlot;
                piece = new SetPiece(chosen.Value, secondRing);
            }
            else
            {
                var missing = new List<SetPiece>();
                foreach (var part in set.Pieces)
                {
                    if (!_state.SetCollection.Contains(SetDef.PieceKey(set.Id, part)))
                        missing.Add(part);
                }
                piece = missing[_state.Drops.Rng.NextInt(0, missing.Count)];
            }
            var slot = piece.Slot;
            var jewelry = ItemCatalog.IsJewelry(slot);
            var cost = (long)System.Math.Ceiling(CraftCost(set) * (jewelry ? _items.JewelryCraftMultiplier * set.CraftMultiplier : 1));
            if (Unaffordable(cost, jewelry) is { } lacking)
                return lacking;
            if (slot == ItemSlot.Weapon)
            {
                var weapon = CraftSetWeapon(set, weaponType);
                if (weapon == null)
                    return CraftResult.WrongSlot;
                PayCraft(cost, false);
                _weapons.Give(weapon);
                _gear.NoteFound(set, ItemSlot.Weapon);
                _state.Drops.ItemsFound++;
                _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, weapon, null, false, 0)));
                return CraftResult.Crafted;
            }
            PayCraft(cost, jewelry);
            var item = _items.RollSetPiece(set, slot, null, set.Rarity, BiomeNumber(System.Math.Max(1, _state.RecordStage)), _state.Drops.Rng, piece.SecondRing);
            _gear.NoteFound(item);
            _state.Drops.Gear.Add(item);
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, null, item, false, 0)));
            return CraftResult.Crafted;
        }

        public bool CraftsForSparks => _state.Rift.Inside;

        private long CraftDepth => _state.Rift.Inside ? _state.HighestStage : _state.RecordStage;

        private CraftResult? Unaffordable(long cost, bool jewelry)
        {
            if (CraftsForSparks)
                return _state.Rift.Sparks < cost ? CraftResult.NotEnoughSparks : null;
            if (jewelry)
                return _state.Drops.Essence < cost ? CraftResult.NotEnoughEssence : null;
            return _state.Drops.Ingots < cost ? CraftResult.NotEnoughIngots : null;
        }

        private void PayCraft(long cost, bool essence)
        {
            if (CraftsForSparks)
                _state.Rift.Sparks -= cost;
            else if (essence)
                _state.Drops.Essence -= cost;
            else
                _state.Drops.Ingots -= cost;
            _state.Drops.CraftSpent += cost;
            _gear.Rebuild();
        }

        public bool UniqueCraftOpen => _gear.MasteryLevel >= _items.UniqueCraftMastery;

        public long UniqueCraftCost(ItemSlot slot) =>
            (long)System.Math.Ceiling(_items.UniqueCraftCost * (ItemCatalog.IsJewelry(slot) ? _items.JewelryCraftMultiplier : 1));

        public CraftResult CraftUnique(string id)
        {
            if (Unlocks?.IsOpen(Feature.SetCraft) == false || !UniqueCraftOpen)
                return CraftResult.MasteryTooLow;
            if (!_state.UniquesFound.Contains(id))
                return CraftResult.NotFound;
            if (BagFull)
                return CraftResult.BagFull;
            var level = BiomeNumber(System.Math.Max(1, _state.RecordStage));
            if (_items.TryGetUnique(id, out var gearUnique))
            {
                var jewelry = ItemCatalog.IsJewelry(gearUnique.Slot);
                var cost = UniqueCraftCost(gearUnique.Slot);
                if (Unaffordable(cost, jewelry) is { } lacking)
                    return lacking;
                PayCraft(cost, jewelry);
                var item = new GearItem(gearUnique, level);
                _state.Drops.Gear.Add(item);
                _state.Drops.ItemsFound++;
                _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, null, item, false, 0)));
                return CraftResult.Crafted;
            }
            if (_weapons.Catalog.TryGetUnique(id, out var weaponUnique))
            {
                if (weaponUnique.Base.Type != OwnType)
                    return CraftResult.WrongSlot;
                var cost = UniqueCraftCost(ItemSlot.Weapon);
                if (Unaffordable(cost, false) is { } lacking)
                    return lacking;
                PayCraft(cost, false);
                var weapon = _weapons.Give(_weapons.Catalog.RollUnique(weaponUnique, level));
                _state.Drops.ItemsFound++;
                _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Craft, _state.Stage, weapon, null, false, 0)));
                return CraftResult.Crafted;
            }
            return CraftResult.NotFound;
        }

        private void RollPartySet(long stage)
        {
            if (!PartySetsOpen)
                return;
            var dungeon = _state.Dungeons.Active;
            var source = dungeon != null ? "dungeons" : _state.Rift.Inside ? "rift" : null;
            if (source == null || !_items.PartySetDrops.TryGetValue(source, out var drop) || !_state.Drops.Rng.Chance(drop.Chance))
                return;
            var depth = dungeon != null ? dungeon.Tier : (int)System.Math.Min(int.MaxValue, stage);
            var rarity = drop.Roll(depth, _state.Drops.Rng.NextDouble());
            var candidates = new List<SetDef>();
            foreach (var set in _items.Sets)
            {
                if (set.Party && set.PartySource == source && set.Rarity == rarity)
                    candidates.Add(set);
            }
            if (candidates.Count == 0)
                return;
            var chosen = candidates[_state.Drops.Rng.NextInt(0, candidates.Count)];
            var piece = chosen.Pieces[_state.Drops.Rng.NextInt(0, chosen.Pieces.Count)];
            GivePartySetPiece(chosen.Id, SetDef.PieceName(piece));
        }

        public bool GivePartySetPiece(string setId, string pieceName)
        {
            if (!_items.TryGetSet(setId, out var set) || !set.Party || BagFull)
                return false;
            SetPiece? found = null;
            foreach (var piece in set.Pieces)
            {
                if (SetDef.PieceName(piece) == pieceName)
                    found = piece;
            }
            if (found == null)
                return false;
            var stage = System.Math.Max(1, _state.RecordStage);
            if (found.Value.Slot == ItemSlot.Weapon)
            {
                var weapon = RollWeapon(OwnType, set.Rarity, stage, _state.Drops.Rng).InSet(set);
                _weapons.Give(weapon);
                _gear.NoteFound(set, ItemSlot.Weapon);
                _state.Drops.ItemsFound++;
                _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Party, _state.Stage, weapon, null, false, 0)));
                return true;
            }
            var item = _items.RollSetPiece(set, found.Value.Slot, null, set.Rarity, BiomeNumber(stage), _state.Drops.Rng, found.Value.SecondRing);
            _gear.NoteFound(item);
            _state.Drops.Gear.Add(item);
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Party, _state.Stage, null, item, false, 0)));
            return true;
        }

        public bool GiveEventPiece(string eventId)
        {
            if (BagFull)
                return false;
            var missing = new List<(SetDef Set, SetPiece Piece)>();
            foreach (var set in _items.Sets)
            {
                if (set.Event != eventId)
                    continue;
                foreach (var part in set.Pieces)
                {
                    if (!_state.SetCollection.Contains(SetDef.PieceKey(set.Id, part)))
                        missing.Add((set, part));
                }
            }
            if (missing.Count == 0)
                return false;
            var (target, piece) = missing[_state.Drops.Rng.NextInt(0, missing.Count)];
            var item = _items.RollSetPiece(target, piece.Slot, null, target.Rarity, BiomeNumber(System.Math.Max(1, _state.RecordStage)), _state.Drops.Rng, piece.SecondRing);
            _gear.NoteFound(item);
            _state.Drops.Gear.Add(item);
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(DropSource.Event, _state.Stage, null, item, false, 0)));
            return true;
        }

        private void NoteUnique(WeaponItem? weapon, GearItem? gear)
        {
            if (weapon?.Unique != null)
                _state.UniquesFound.Add(weapon.Unique.Id);
            if (gear?.Unique != null)
                _state.UniquesFound.Add(gear.Unique.Id);
        }

        private WeaponItem? CraftSetWeapon(SetDef set, string? chosenType)
        {
            var type = OwnType;
            if (!set.AllowsType(type.Id) || (chosenType != null && chosenType != type.Id))
                return null;
            var stage = System.Math.Max(1, _state.RecordStage);
            return RollWeapon(type, set.Rarity, stage, _state.Drops.Rng).InSet(set);
        }

        public DungeonRules? Dungeons { get; set; }
        public SlateSystem? Slates { get; set; }

        private void RollKey(long stage, EnemyType type)
        {
            var rules = Dungeons;
            if (rules == null || stage < rules.KeysFromStage || Unlocks?.IsOpen(Feature.Dungeons) == false)
                return;
            var chance = type == EnemyType.Guardian ? rules.GuardianKeyChance : _rules.IsSpecialBoss(stage) ? rules.SpecialBossKeyChance : rules.BossKeyChance;
            if (chance <= 0)
                return;
            var divisor = rules.PartyDivisor(_state.PartyAllies);
            var dungeons = _state.Dungeons;
            dungeons.BossesWithoutKey++;
            var guaranteed = rules.KeyPity > 0 && dungeons.BossesWithoutKey >= rules.KeyPity * divisor;
            if (!_state.Drops.Rng.Chance(chance / divisor) && !guaranteed)
                return;
            dungeons.BossesWithoutKey = 0;
            Push(PendingDrop.Key(stage));
        }

        public RiftRules? Rift { get; set; }

        private void RollRiftKey(long stage, EnemyType type)
        {
            var rules = Dungeons;
            if (rules == null || !_state.Rift.Inside || _state.Dungeons.Inside || _state.Events.FightInside || stage < rules.Rift.FromStage
                || (Unlocks != null ? !Unlocks.IsOpen(Feature.Dungeons) : Rift != null && _state.HighestStage < Rift.StageOf(Feature.Dungeons)))
                return;
            var chance = (type == EnemyType.Guardian ? rules.Rift.GuardianKeyChance : rules.Rift.BossKeyChance) / rules.PartyDivisor(_state.PartyAllies);
            if (chance > 0 && _state.Drops.Rng.Chance(chance))
                Push(PendingDrop.RiftKey(stage));
        }

        private void DeliverKey(PendingDrop drop)
        {
            var rules = Dungeons!;
            if (drop.IsRiftKey)
            {
                if (_state.Rift.WindowId == 0)
                    return;
                var riftTier = rules.Rift.TierAt(drop.Stage);
                _state.Dungeons.Keys.Add(MakeKey(riftTier, drop.Stage, _state.Rift.WindowId));
                _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeyDropped, _state.Tick, riftTier));
                return;
            }
            var highest = rules.HighestTierFor(drop.Stage, _state.GatesCleared.Count > 0);
            if (highest < 1)
                return;
            var tier = System.Math.Max(1, highest - _state.Drops.Rng.NextInt(0, 2));
            _state.Dungeons.Keys.Add(MakeKey(tier, drop.Stage));
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeyDropped, _state.Tick, tier));
        }

        public DungeonKey MakeKey(int tier, long stage, long rift = 0)
        {
            var rules = Dungeons!;
            var rng = _state.Drops.Rng;
            var pool = new List<DungeonModifier>(rules.Modifiers);
            var count = System.Math.Min(pool.Count, rules.MaxModifiers < 1 ? 0 : rng.NextInt(1, rules.MaxModifiers + 1));
            var chosen = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var index = rng.NextInt(0, pool.Count);
                chosen.Add(pool[index].Id);
                pool.RemoveAt(index);
            }
            return new DungeonKey(tier, chosen, stage, rules.KeyAttempts, rift);
        }

        public void SalvageKey(DungeonKey key)
        {
            var tier = key.Tier;
            if (key.ForRift)
                _state.Rift.Shards += BigNumber.FromDouble(Dungeons!.Rift.Shards(tier) * Dungeons.SalvageShare);
            else
                _state.Drops.Shards += Dungeons!.SalvageShards(tier);
            _state.Dungeons.Salvaged++;
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeySalvaged, _state.Tick, tier));
        }

        public void GiveDungeonLoot(DungeonTier tier, long stage, double bonus)
        {
            var rng = _state.Drops.Rng;
            var scale = 1 + System.Math.Max(0, bonus);
            var amount = tier.Loot.Items * scale;
            var items = (int)System.Math.Floor(amount) + (rng.NextDouble() < amount - System.Math.Floor(amount) ? 1 : 0);
            for (var i = 0; i < items; i++)
                Push(Rolled(tier.Loot, stage, DropSource.Dungeon));
            Flush();
            if (tier.UniqueChance > 0 && rng.NextDouble() < System.Math.Min(1, tier.UniqueChance * scale))
                GiveUnique(stage, DropSource.Dungeon);
            _state.Drops.Shards += (long)System.Math.Round(tier.Shards * scale);
            Slates?.OnDungeon();
        }

        public void GiveLimitUnique(long stage) => GiveUnique(stage, DropSource.Limit);

        public void GiveFind(long stage)
        {
            Push(Rolled(_rules.Guardian, stage, DropSource.Find));
            Flush();
        }

        public void GiveShards(long shards) => AddShards(shards);

        public void GiveOddityShards(double share)
        {
            var oddities = _state.Oddities;
            oddities.ShardCarry += System.Math.Max(0, _hero.Value(CombatStats.ShardDrop, _rules.Shards.SpecialBoss * share));
            var whole = (long)System.Math.Floor(oddities.ShardCarry + 1e-9);
            oddities.ShardCarry = System.Math.Max(0, oddities.ShardCarry - whole);
            AddShards(whole);
        }

        public void GiveTrophy() => _state.PendingTrophies++;

        public void GiveSpiritLoot(int trophies, long shards)
        {
            _state.PendingTrophies += System.Math.Max(0, trophies);
            AddShards(shards);
        }

        private void GiveUnique(long stage, DropSource source)
        {
            var gear = _items.Uniques;
            var weapons = _uniqueWeapons;
            weapons.Clear();
            foreach (var unique in _weapons.Catalog.Uniques)
            {
                if (unique.Base.Type == OwnType)
                    weapons.Add(unique);
            }
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
                weapon = _weapons.Give(_weapons.Catalog.RollUnique(weapons[pick - gear.Count], BiomeNumber(stage)));
            }
            NoteUnique(weapon, item);
            _state.Drops.ItemsFound++;
            _events.Add(GameEvent.ForDrop(_state.Tick, new ItemDrop(source, stage, weapon, item, false, 0)));
        }

        public Unlocks? Unlocks { get; set; }

        public void SetDay(long day)
        {
            var drops = _state.Drops;
            if (day <= drops.Day)
                return;
            drops.Day = day;
            drops.DailyItems = 0;
            drops.DailyShards = 0;
        }

        public int BiomeNumber(long stage) => (int)System.Math.Min(int.MaxValue, (stage - 1) / _stages.BiomeLength + 1);

        public System.Action<long, bool>? GuardianKilled { get; set; }

        public bool PartySetsOpen { get; set; }

        public void OnBossDefeated(long stage, EnemyType type, long recordBefore, bool raisesRecord)
        {
            if (!raisesRecord)
            {
                RollRiftKey(stage, type);
                RollPartySet(stage);
                return;
            }
            if (type == EnemyType.Guardian)
                GuardianKilled?.Invoke(stage, stage >= recordBefore);
            GiveShards(stage, type, recordBefore);
            if (stage >= recordBefore)
            {
                GiveRecordItems(stage, type);
                Slates?.OnRecordBoss(stage, type, BiomeNumber(stage));
            }
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

        public System.Action<ItemDrop>? Salvaged { get; set; }

        public void Flush()
        {
            for (var i = 0; i < _pendingCount; i++)
            {
                var drop = _pending[i];
                _pending[i] = default;
                if (drop.IsKey)
                    DeliverKey(drop);
                else
                    Deliver(drop);
            }
            _pendingCount = 0;
            Slates?.Flush();
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
            if (_state.ClassChosen)
                return OwnType;
            var types = _weapons.Catalog.Types;
            return types[rng.NextInt(0, types.Count)];
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

        public static int QualityRoll(long found, long stage)
        {
            var mixed = (ulong)found * 0x9E3779B97F4A7C15UL ^ (ulong)stage * 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 31;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 29;
            return (int)(mixed % 101);
        }

        private WeaponItem WeaponFor(PendingDrop drop, ItemRarity rarity, Rng rng)
        {
            var chance = _rules.UniqueChanceFor(drop.Source);
            if (chance > 0 && rng.NextDouble() < chance)
            {
                var unique = _weapons.Catalog.PickUnique(_rules.CeilingAt(drop.Stage), drop.Type!, rng);
                if (unique != null)
                    return _weapons.Catalog.RollUnique(unique, BiomeNumber(drop.Stage));
            }
            var setChance = _rules.SetChanceFor(drop.Source);
            if (setChance > 0 && rng.NextDouble() < setChance)
            {
                var set = _items.PickSet(ItemSlot.Weapon, rarity, rng);
                if (set != null && set.AllowsType(drop.Type!.Id))
                    return RollWeapon(drop.Type!, rarity, drop.Stage, rng).InSet(set);
            }
            return RollWeapon(drop.Type!, rarity, drop.Stage, rng);
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
                return Rolled(catalog.StarterFor(type.HeroClass), rarity, biome, rng);
            _bases.Sort((a, b) => a.FromBiome.CompareTo(b.FromBiome));
            var total = 0.0;
            for (var i = 0; i < _bases.Count; i++)
                total += BaseWeight(catalog, i);
            var roll = rng.NextDouble() * total;
            for (var i = 0; i < _bases.Count; i++)
            {
                roll -= BaseWeight(catalog, i);
                if (roll < 0)
                    return Rolled(_bases[i], rarity, biome, rng);
            }
            return Rolled(_bases[_bases.Count - 1], rarity, biome, rng);
        }

        private WeaponItem Rolled(WeaponBase weaponBase, ItemRarity rarity, int itemLevel, Rng rng) =>
            _weapons.Catalog.Roll(weaponBase, rarity, itemLevel, _items.RollAffixes(ItemSlot.Weapon, rarity, itemLevel, rng));

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
                var color = guarantee.Color == GuaranteeColor.Own && slot != ItemSlot.Amulet && Array.IndexOf(GearColors, OwnType.Color) >= 0 ? OwnType.Color : RandomColor(slot, rng);
                return PendingDrop.Gear(DropSource.Guarantee, stage, slot, rarity, color);
            }
            var type = guarantee.Color switch
            {
                GuaranteeColor.Own => OwnType,
                _ => guarantee.ToEmptySlot ? OwnType : PickType(rng)
            };
            return PendingDrop.Weapon(DropSource.Guarantee, stage, rarity, type);
        }

        private ItemSlot EmptySlot(Rng rng)
        {
            var weapons = _state.Weapons;
            if (weapons.Held == null)
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

        private void Deliver(PendingDrop drop)
        {
            var drops = _state.Drops;
            drops.ItemsFound++;
            var rarity = drop.Rarity;
            var weapon = drop.Type != null ? WeaponFor(drop, rarity, drops.Rng) : null;
            if (weapon != null)
                weapon.Quality = QualityRoll(drops.ItemsFound, drop.Stage);
            var gear = weapon == null ? RollGear(drop, rarity, drops.Rng) : null;
            if (gear != null)
                _gear.NoteFound(gear);
            NoteUnique(weapon, gear);
            if (weapon?.Set != null)
                _gear.NoteFound(weapon.Set, ItemSlot.Weapon);
            var salvaged = BagFull || ShouldSalvage(weapon, gear, rarity);
            var dust = salvaged ? _rules.DustFor(rarity) * System.Math.Max(0, _hero.Value(CombatStats.DustGain, 1)) : 0;
            drops.Dust += dust;
            if (salvaged)
                drops.SalvagedItems++;
            else if (weapon != null)
                _weapons.Give(weapon);
            else
                drops.Gear.Add(gear!);
            var delivered = new ItemDrop(drop.Source, drop.Stage, weapon, gear, salvaged, dust);
            if (salvaged)
                Salvaged?.Invoke(delivered);
            _events.Add(GameEvent.ForDrop(_state.Tick, delivered));
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
            return weapons.Held == null || rarity > weapons.Held.Rarity;
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
    }
}
