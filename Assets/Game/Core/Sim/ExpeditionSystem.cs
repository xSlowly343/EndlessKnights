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
        NoChoice
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

        public List<string> Companions()
        {
            var companions = new List<string>();
            foreach (var companion in _rules.Companions)
                companions.Add(companion.Id);
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
            foreach (var free in _rules.Companions)
            {
                if (free.Id == companion)
                    return _rules.Trait(free.Trait);
            }
            return IsOwnedCompanion(companion) ? _rules.Trait(_rules.CosmeticTrait) : null;
        }

        public bool Busy(string companion)
        {
            var active = State.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].Companion == companion)
                    return true;
            }
            return false;
        }

        public bool BiomeOpen(string biome)
        {
            var rotation = _stages.Rotation;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (rotation[i] == biome)
                    return _state.RecordStage > (long)i * _stages.BiomeLength;
            }
            return false;
        }

        public ExpeditionResult Send(string companion, string biome, int hours)
        {
            var trait = TraitOf(companion);
            if (trait == null)
                return ExpeditionResult.UnknownCompanion;
            if (Busy(companion))
                return ExpeditionResult.CompanionBusy;
            var place = _rules.Biome(biome);
            if (place == null)
                return ExpeditionResult.UnknownBiome;
            if (!BiomeOpen(biome))
                return ExpeditionResult.BiomeClosed;
            if (!Contains(_rules.Hours, hours))
                return ExpeditionResult.BadHours;
            State.Active.Add(new Expedition
            {
                Companion = companion,
                Biome = biome,
                Hours = hours,
                Seconds = hours * SecondsPerHour,
                Items = new long[place.Items.Count]
            });
            return ExpeditionResult.Done;
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
                expedition.Seconds += expedition.Hours * SecondsPerHour * _rules.WaitExtend;
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
                if (!expedition.LetterSent && expedition.Elapsed * 2 >= expedition.Hours * SecondsPerHour)
                    SendLetter(expedition, i);
                if (expedition.Elapsed >= expedition.Seconds)
                    Return(i);
            }
        }

        private void SendLetter(Expedition expedition, int index)
        {
            expedition.Letter = _rules.Letters.Count == 0 ? 0 : (int)(_state.Rng.NextDouble() * _rules.Letters.Count) % _rules.Letters.Count;
            expedition.Choice = _state.Rng.NextDouble() < _rules.ChoiceChance;
            _events.Add(GameEvent.ForStage(GameEventKind.ExpeditionLetter, _state.Tick, index));
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
            State.Returned++;
            State.Reports.Insert(0, expedition);
            if (State.Reports.Count > ExpeditionState.ReportsKept)
                State.Reports.RemoveAt(State.Reports.Count - 1);
            _events.Add(GameEvent.ForStage(GameEventKind.ExpeditionReturned, _state.Tick, expedition.Rare.Length > 0 ? 1 : 0));
        }

        private void Loot(Expedition expedition, ExpeditionBiome place, CompanionTrait trait)
        {
            var hours = expedition.Elapsed / SecondsPerHour;
            if (expedition.Items.Length != place.Items.Count)
                expedition.Items = new long[place.Items.Count];
            for (var i = 0; i < place.Items.Count; i++)
            {
                var count = (long)System.Math.Floor(place.Items[i].PerHour * hours * trait.ItemMultiplier * (1 + _state.Museum.Perk(MuseumPerk.ExpeditionLoot)));
                expedition.Items[i] = count;
                _camp.Add(place.Items[i].Item, count);
            }
            expedition.Shards = (long)System.Math.Floor(place.ShardsPerHour * hours * trait.ShardsMultiplier);
            _state.Drops.Shards += expedition.Shards;
            var chance = place.RareChancePerHour * hours * trait.RareMultiplier * (1 + _state.Museum.Perk(MuseumPerk.RareFind)) * (expedition.Waited ? _rules.WaitRareMultiplier : 1);
            if (_state.Rng.NextDouble() < chance)
            {
                expedition.Rare = place.Rare;
                _camp.Add(place.Rare, 1);
                State.Finds.Add(place.Rare);
            }
        }

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
