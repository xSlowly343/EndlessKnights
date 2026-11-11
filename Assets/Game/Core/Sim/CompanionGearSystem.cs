using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum GearResult
    {
        Done,
        NoForge,
        BadRarity,
        UnknownSet,
        UnknownBiome,
        NoShards,
        NoBones,
        UnknownGear,
        UnknownCompanion,
        SlotLocked,
        NotReady,
        Used
    }

    public sealed class CompanionGearSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly CompanionSystem _companions;
        private readonly ExpeditionSystem _expeditions;

        public CompanionGearSystem(GameState state, CampSystem camp, CompanionSystem companions, ExpeditionSystem expeditions)
        {
            _state = state;
            _camp = camp;
            _companions = companions;
            _expeditions = expeditions;
            companions.Gear = this;
        }

        private CompanionGearRules Rules => _companions.Rules.Gear;
        private CompanionState State => _companions.State;

        public IReadOnlyList<CompanionGearItem> Items => State.Gear;

        public GearResult Craft(CompanionGearSlot slot, int rarity, string biome, string set = "")
        {
            var rules = Rules;
            if (_camp.Slots(rules.Building) <= 0)
                return GearResult.NoForge;
            if (rarity < 1 || rarity > rules.MaxRarity)
                return GearResult.BadRarity;
            if (set.Length > 0 && rules.Set(set) == null)
                return GearResult.UnknownSet;
            var place = _companions.Rules.BiomeOf(biome);
            if (place == null)
                return GearResult.UnknownBiome;
            var shards = (long)System.Math.Ceiling((rules.ShardsPerRarity * rarity + (set.Length > 0 ? rules.SetShards : 0)) * System.Math.Max(0, 1 - _companions.HomeOwn(OwnEffectKind.GearPrice) - _companions.TreeStat(MenagerieStat.GearCost)) - 1e-9);
            var bones = rules.BonesPerRarity * rarity;
            if (_state.Drops.Shards < shards)
                return GearResult.NoShards;
            if (_camp.Count(place.Bone) < bones)
                return GearResult.NoBones;
            _state.Drops.Shards -= shards;
            _camp.Take(place.Bone, bones);
            var item = new CompanionGearItem { Id = State.NextGearId++, Slot = slot, Rarity = rarity, Set = set };
            Roll(item);
            State.Gear.Add(item);
            return GearResult.Done;
        }

        private void Roll(CompanionGearItem item)
        {
            var rules = Rules;
            var pool = new List<GearAffixRule>(rules.Affixes);
            var count = rules.AffixCount(item.Rarity);
            var scale = 1 + (item.Rarity - 1) * rules.RarityStep;
            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var pick = (int)(_state.Rng.NextDouble() * pool.Count);
                var affix = pool[System.Math.Min(pick, pool.Count - 1)];
                pool.Remove(affix);
                var value = (affix.Min + (affix.Max - affix.Min) * _state.Rng.NextDouble()) * scale;
                item.Affixes.Add(new CompanionGearAffix { Stat = affix.Stat, Value = System.Math.Round(value, 4) });
            }
        }

        public CompanionGearItem? Item(int id)
        {
            for (var i = 0; i < State.Gear.Count; i++)
            {
                if (State.Gear[i].Id == id)
                    return State.Gear[i];
            }
            return null;
        }

        public bool SlotOpen(string companion, CompanionGearSlot slot)
        {
            var rule = Rules.Slot(slot);
            return rule != null && _companions.StageOf(companion) >= rule.Stage && _companions.StarsOf(companion) >= rule.Stars && (rule.Unlock.Length == 0 || _companions.Unlocked(companion, rule.Unlock));
        }

        public GearResult Equip(string companion, int id)
        {
            var item = Item(id);
            if (item == null)
                return GearResult.UnknownGear;
            if (!_companions.Known(companion))
                return GearResult.UnknownCompanion;
            if (!SlotOpen(companion, item.Slot))
                return GearResult.SlotLocked;
            var worn = Worn(companion, item.Slot);
            if (worn != null)
                worn.Owner = string.Empty;
            item.Owner = companion;
            _companions.ApplyPerks();
            return GearResult.Done;
        }

        public GearResult Unequip(int id)
        {
            var item = Item(id);
            if (item == null)
                return GearResult.UnknownGear;
            item.Owner = string.Empty;
            _companions.ApplyPerks();
            return GearResult.Done;
        }

        public CompanionGearItem? Worn(string companion, CompanionGearSlot slot)
        {
            for (var i = 0; i < State.Gear.Count; i++)
            {
                if (State.Gear[i].Owner == companion && State.Gear[i].Slot == slot)
                    return State.Gear[i];
            }
            return null;
        }

        public double Stat(string companion, CompanionGearStat stat)
        {
            var total = 0.0;
            for (var i = 0; i < State.Gear.Count; i++)
            {
                var item = State.Gear[i];
                if (item.Owner != companion)
                    continue;
                for (var a = 0; a < item.Affixes.Count; a++)
                {
                    if (item.Affixes[a].Stat == stat)
                        total += item.Affixes[a].Value;
                }
            }
            var sets = Rules.Sets;
            for (var s = 0; s < sets.Count; s++)
            {
                var pieces = Pieces(companion, sets[s].Id);
                for (var b = 0; b < sets[s].Bonuses.Count; b++)
                {
                    var bonus = sets[s].Bonuses[b];
                    if (pieces >= bonus.Pieces && bonus.Stat == stat)
                        total += bonus.Value;
                }
            }
            return total;
        }

        public int Pieces(string companion, string set)
        {
            var pieces = 0;
            for (var i = 0; i < State.Gear.Count; i++)
            {
                if (State.Gear[i].Owner == companion && State.Gear[i].Set == set)
                    pieces++;
            }
            return pieces;
        }

        public bool HasPower(string companion, CompanionSetPower power)
        {
            var sets = Rules.Sets;
            for (var s = 0; s < sets.Count; s++)
            {
                var pieces = Pieces(companion, sets[s].Id);
                for (var b = 0; b < sets[s].Bonuses.Count; b++)
                {
                    if (sets[s].Bonuses[b].Power == power && pieces >= sets[s].Bonuses[b].Pieces)
                        return true;
                }
            }
            return false;
        }

        public int HomeSlotPowers()
        {
            var count = 0;
            for (var i = 0; i < State.Home.Count; i++)
            {
                if (HasPower(State.Home[i], CompanionSetPower.HomeSlot))
                    count++;
            }
            return count;
        }

        public GearResult InstantReturn(string companion)
        {
            if (!HasPower(companion, CompanionSetPower.InstantTrip))
                return GearResult.NotReady;
            var progress = _companions.Progress(companion);
            if (progress.InstantDay == State.Day)
                return GearResult.Used;
            var active = _state.Expeditions.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].Companion != companion)
                    continue;
                progress.InstantDay = State.Day;
                _expeditions.Finish(i);
                return GearResult.Done;
            }
            return GearResult.NotReady;
        }
    }
}
