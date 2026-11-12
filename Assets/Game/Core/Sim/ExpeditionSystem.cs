using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum ExpeditionResult
    {
        Done,
        UnknownCompanion,
        CompanionBusy,
        UnknownBiome,
        BiomeClosed,
        BadHours,
        NoChoice,
        NoTripSlot
    }

    public sealed class ExpeditionSystem
    {
        public const string CompanionSlot = "companion";
        public const double SecondsPerHour = 3600;

        private readonly GameState _state;
        private readonly StageTable _stages;
        private readonly CosmeticCatalog _cosmetics;
        private readonly CampSystem _camp;
        private readonly EventQueue _events;
        private ExpeditionRules _rules;
        private CompanionSystem? _companions;

        public ExpeditionSystem(ExpeditionRules rules, GameState state, StageTable stages, CosmeticCatalog cosmetics, CampSystem camp, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _stages = stages;
            _cosmetics = cosmetics;
            _camp = camp;
            _events = events;
        }

        public ExpeditionRules Rules => _rules;
        public ExpeditionState State => _state.Expeditions;

        public void UseRules(ExpeditionRules rules) => _rules = rules;

        public void UseCompanions(CompanionSystem companions) => _companions = companions;

        public List<string> Companions()
        {
            var companions = new List<string>();
            foreach (var companion in _rules.Companions)
                companions.Add(companion.Id);
            if (_companions != null)
            {
                var granted = new List<string>(_companions.State.Owned);
                granted.Sort(System.StringComparer.Ordinal);
                companions.AddRange(granted);
            }
            var owned = new List<string>();
            foreach (var id in _state.Cosmetics.Owned)
            {
                if (_cosmetics.TryGet(id, out var cosmetic) && cosmetic.Slot == CompanionSlot)
                    owned.Add(id);
            }
            owned.Sort(System.StringComparer.Ordinal);
            companions.AddRange(owned);
            return companions;
        }

        public CompanionTrait? TraitOf(string companion)
        {
            var free = _rules.Companions;
            for (var i = 0; i < free.Count; i++)
            {
                if (free[i].Id == companion)
                    return _rules.Trait(free[i].Trait);
            }
            if (IsOwnedCompanion(companion))
                return _rules.Trait(_rules.CosmeticTrait);
            return _companions != null && _companions.Owns(companion) ? _rules.Trait(_companions.GrantedTrait(companion)) : null;
        }

        public bool Busy(string companion)
        {
            var active = State.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].Companion == companion || active[i].Partner == companion || active[i].Third == companion)
                    return true;
            }
            return false;
        }

        public int UsedSlots()
        {
            var used = 0;
            var active = State.Active;
            for (var i = 0; i < active.Count; i++)
                used += active[i].Pack && _companions != null ? _companions.Rules.Pack.Slots : 1;
            return used;
        }

        public ExpeditionResult SendPack(string companion, string partner, string third, string biome, int hours)
        {
            if (_companions == null || partner == companion || third == companion || third == partner || TraitOf(partner) == null || TraitOf(third) == null)
                return ExpeditionResult.UnknownCompanion;
            if (Busy(partner) || _companions.Occupied(partner) || Busy(third) || _companions.Occupied(third))
                return ExpeditionResult.CompanionBusy;
            if (UsedSlots() + _companions.Rules.Pack.Slots > _companions.TripSlots)
                return ExpeditionResult.NoTripSlot;
            var result = SendTogether(companion, partner, biome, hours);
            if (result != ExpeditionResult.Done)
                return result;
            var expedition = State.Active[State.Active.Count - 1];
            expedition.Third = third;
            var slowest = System.Math.Max(System.Math.Max(DurationFactor(companion, biome, hours), DurationFactor(partner, biome, hours)), DurationFactor(third, biome, hours));
            expedition.Seconds = hours * SecondsPerHour * slowest * TreatDuration(expedition);
            expedition.Planned = expedition.Seconds;
            return ExpeditionResult.Done;
        }

        public bool BiomeOpen(string biome)
        {
            var rotation = _stages.Rotation;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (rotation[i] == biome)
                    return _state.RecordStage >= _stages.OpensAt(i);
            }
            return false;
        }

        public ExpeditionResult SendTogether(string companion, string partner, string biome, int hours)
        {
            if (_companions == null || partner == companion || TraitOf(partner) == null)
                return ExpeditionResult.UnknownCompanion;
            if (Busy(partner) || _companions.Occupied(partner))
                return ExpeditionResult.CompanionBusy;
            if (!Contains(_rules.Hours, hours) && !_companions.AllowsHours(partner, hours))
                return ExpeditionResult.BadHours;
            var result = Send(companion, biome, hours);
            if (result != ExpeditionResult.Done)
                return result;
            var expedition = State.Active[State.Active.Count - 1];
            expedition.Partner = partner;
            var seconds = hours * SecondsPerHour * System.Math.Max(DurationFactor(companion, biome, hours), DurationFactor(partner, biome, hours)) * TreatDuration(expedition);
            expedition.Seconds = seconds;
            expedition.Planned = seconds;
            return ExpeditionResult.Done;
        }

        public ExpeditionResult Send(string companion, string biome, int hours)
        {
            var trait = TraitOf(companion);
            if (trait == null)
                return ExpeditionResult.UnknownCompanion;
            if (Busy(companion) || _companions?.Occupied(companion) == true)
                return ExpeditionResult.CompanionBusy;
            if (_companions != null && UsedSlots() >= _companions.TripSlots)
                return ExpeditionResult.NoTripSlot;
            var place = _rules.Biome(biome);
            if (place == null)
                return ExpeditionResult.UnknownBiome;
            if (!BiomeOpen(biome))
                return ExpeditionResult.BiomeClosed;
            if (!Contains(_rules.Hours, hours) && _companions?.AllowsHours(companion, hours) != true)
                return ExpeditionResult.BadHours;
            var seconds = hours * SecondsPerHour * DurationFactor(companion, biome, hours);
            State.Active.Add(new Expedition
            {
                Companion = companion,
                Biome = biome,
                Hours = hours,
                Seconds = seconds,
                Planned = seconds,
                Items = new long[place.Items.Count]
            });
            var sent = State.Active[State.Active.Count - 1];
            _companions?.PackTreat(sent);
            sent.Seconds *= TreatDuration(sent) * (1 - (_companions?.FirstTripCut(companion) ?? 0));
            sent.Planned = sent.Seconds;
            return ExpeditionResult.Done;
        }

        public ExpeditionResult SendColossus(string companion, string biome, double seconds)
        {
            var place = _rules.Biome(biome);
            if (TraitOf(companion) == null)
                return ExpeditionResult.UnknownCompanion;
            if (Busy(companion) || _companions?.Occupied(companion) == true)
                return ExpeditionResult.CompanionBusy;
            if (_companions != null && UsedSlots() >= _companions.TripSlots)
                return ExpeditionResult.NoTripSlot;
            if (place == null)
                return ExpeditionResult.UnknownBiome;
            State.Active.Add(new Expedition
            {
                Companion = companion,
                Biome = biome,
                Hours = (int)System.Math.Ceiling(seconds / SecondsPerHour),
                Seconds = seconds,
                Planned = seconds,
                Colossus = true,
                Items = new long[place.Items.Count]
            });
            return ExpeditionResult.Done;
        }

        private double TreatDuration(Expedition expedition) => 1 - (_companions?.TreatBonus(expedition, TreatStat.Duration) ?? 0);

        public double DurationFactor(string companion, string biome = "", int hours = 0)
        {
            var perk = _state.Perk(MuseumPerk.ShorterTrips);
            return (_companions?.DurationFactor(companion, perk, biome, hours) ?? System.Math.Max(0.1, 1 - perk)) * _state.PeacefulBonus.TimeFactor;
        }

        public ExpeditionResult Answer(int index, bool wait)
        {
            if (index < 0 || index >= State.Active.Count || !State.Active[index].Choice)
                return ExpeditionResult.NoChoice;
            var expedition = State.Active[index];
            expedition.Choice = false;
            if (wait)
            {
                expedition.Waited = true;
                expedition.Seconds += PlannedOf(expedition) * _rules.WaitExtend;
            }
            else
            {
                expedition.Seconds = expedition.Elapsed;
                Advance(0);
            }
            return ExpeditionResult.Done;
        }

        public void Tick() => Advance(GameLoop.TickSeconds);

        public void Advance(double seconds)
        {
            var active = State.Active;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var expedition = active[i];
                expedition.Elapsed = System.Math.Min(expedition.Seconds, expedition.Elapsed + System.Math.Max(0, seconds));
                if (!expedition.LetterSent && expedition.Elapsed >= PlannedOf(expedition) * LetterShare(expedition.Companion))
                    SendLetter(expedition, i);
                if (expedition.Elapsed >= expedition.Seconds)
                    Return(i);
            }
        }

        private double LetterShare(string companion) => 0.5 * (1 - System.Math.Min(0.9, _companions?.Own(companion, OwnEffectKind.LetterEarlier) ?? 0));

        private void SendLetter(Expedition expedition, int index)
        {
            expedition.Letter = _rules.Letters.Count == 0 ? 0 : (int)(_state.Rng.NextDouble() * _rules.Letters.Count) % _rules.Letters.Count;
            expedition.Choice = _state.Rng.NextDouble() < (_rules.ChoiceChance + _state.Perk(MuseumPerk.LetterChoice)) * (1 + (_companions?.Own(expedition.Companion, OwnEffectKind.Choice) ?? 0));
            _events.Add(GameEvent.ForStage(GameEventKind.ExpeditionLetter, _state.Tick, index));
        }

        public int Hasten(double seconds)
        {
            var active = State.Active;
            for (var i = 0; i < active.Count; i++)
                active[i].Elapsed = System.Math.Min(active[i].Seconds, active[i].Elapsed + System.Math.Max(0, seconds));
            var count = active.Count;
            Advance(0);
            return count;
        }

        public void FinishWithLoot(int index, double loot)
        {
            _lootFactor = System.Math.Max(0, loot);
            Finish(index);
            _lootFactor = 1;
        }

        private double _lootFactor = 1;

        public void Finish(int index)
        {
            var expedition = State.Active[index];
            expedition.Elapsed = expedition.Seconds;
            if (!expedition.LetterSent)
                SendLetter(expedition, index);
            Return(index);
        }

        private void Return(int index)
        {
            var expedition = State.Active[index];
            State.Active.RemoveAt(index);
            expedition.Choice = false;
            expedition.Done = true;
            var place = _rules.Biome(expedition.Biome);
            var trait = TraitOf(expedition.Companion) ?? _rules.Trait(_rules.CosmeticTrait);
            if (place != null && trait != null)
                Loot(expedition, place, trait);
            _companions?.TripDone(expedition);
            State.Returned++;
            State.Reports.Insert(0, expedition);
            if (State.Reports.Count > ExpeditionState.ReportsKept)
                State.Reports.RemoveAt(State.Reports.Count - 1);
            _events.Add(GameEvent.ForStage(GameEventKind.ExpeditionReturned, _state.Tick, expedition.Rare.Length > 0 ? 1 : 0));
        }

        private void Loot(Expedition expedition, ExpeditionBiome place, CompanionTrait trait)
        {
            var hours = expedition.Elapsed / SecondsPerHour * _lootFactor;
            var level = _companions?.LevelOf(expedition) ?? 1;
            if (expedition.Items.Length != place.Items.Count)
                expedition.Items = new long[place.Items.Count];
            for (var i = 0; i < place.Items.Count; i++)
            {
                var count = (long)System.Math.Floor(place.Items[i].PerHour * hours * (_companions?.StatOf(expedition, TravelStat.Items) ?? trait.ItemMultiplier) * (_companions?.HerbOf(expedition, place.Items[i].Item) ?? 1) * level * (1 + _state.Perk(MuseumPerk.ExpeditionLoot)) * _state.PeacefulBonus.YieldFactor);
                expedition.Items[i] = count;
                _camp.Add(place.Items[i].Item, count);
                _state.Peaceful.Gain(TownResource.Finds, count);
            }
            expedition.Shards = (long)System.Math.Floor(place.ShardsPerHour * hours * (_companions?.StatOf(expedition, TravelStat.Shards) ?? trait.ShardsMultiplier) * level * (1 + _state.Perk(MuseumPerk.ExpeditionShards)) * _state.PeacefulBonus.YieldFactor);
            _state.Drops.Shards += expedition.Shards;
            var chance = place.RareChancePerHour * hours * (_companions?.StatOf(expedition, TravelStat.Rare) ?? trait.RareMultiplier) * (1 + _state.Perk(MuseumPerk.RareFind)) * (_companions?.RareOf(expedition) ?? 1) * (expedition.Waited ? _rules.WaitRareMultiplier : 1) * _state.PeacefulBonus.LuckFactor;
            if (_state.Rng.NextDouble() < chance)
            {
                expedition.Rare = place.Rare;
                _camp.Add(place.Rare, 1);
                _state.Peaceful.Gain(TownResource.Finds, 1);
                State.Finds.Add(place.Rare);
            }
            _companions?.Extras(expedition, place);
        }

        private static double PlannedOf(Expedition expedition) => expedition.Planned > 0 ? expedition.Planned : expedition.Hours * SecondsPerHour;

        private bool IsOwnedCompanion(string id) =>
            _state.Cosmetics.Owned.Contains(id) && _cosmetics.TryGet(id, out var cosmetic) && cosmetic.Slot == CompanionSlot;

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
