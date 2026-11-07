using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum CompanionResult
    {
        Done,
        UnknownCompanion,
        UnknownFood,
        NoFood,
        Full,
        LowLevel,
        LastStage,
        NoBones,
        NoShards,
        NoBlooms,
        NoFavourite,
        NoTrophy,
        UnknownTrait,
        NoSlot,
        Repeat,
        Busy,
        NoHomeSlot,
        NotHome,
        TooSoon,
        Locked,
        BadName,
        NoPoints,
        NeedsPrevious,
        Learned,
        NotAwakened,
        MaxStars,
        NoSouls,
        NoIngredients
    }

    public sealed class CompanionSystem
    {
        private readonly GameState _state;
        private readonly CookingRules _cooking;
        private readonly CampSystem _camp;
        private readonly ExpeditionSystem _expeditions;
        private readonly EventQueue _events;
        private CompanionRules _rules;

        private readonly ConditionRules _conditions;

        public CompanionSystem(CompanionRules rules, CookingRules cooking, ConditionRules conditions, GameState state, CampSystem camp, ExpeditionSystem expeditions, EventQueue events)
        {
            _conditions = conditions;
            _rules = rules;
            _cooking = cooking;
            _state = state;
            _camp = camp;
            _expeditions = expeditions;
            _events = events;
            expeditions.UseCompanions(this);
            ApplyPerks();
        }

        public CompanionRules Rules => _rules;
        public CompanionState State => _state.Companions;

        public void UseRules(CompanionRules rules)
        {
            _rules = rules;
            ApplyPerks();
        }

        public bool Known(string companion) => _expeditions.TraitOf(companion) != null;

        public int Stars(string companion)
        {
            if (!Known(companion))
                return 0;
            var coat = State.Progress.TryGetValue(companion, out var progress) && progress.Coat.Length > 0 ? _rules.Coat(progress.Coat)?.Star ?? 0 : 0;
            var forms = progress?.Forms.Count ?? 0;
            return StageOf(companion) + 1 + (Hearts(companion) >= _rules.Album.HeartsStar ? 1 : 0) + coat + forms;
        }

        public int AlbumStars(bool freeOnly = false)
        {
            var total = 0;
            var companions = _rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                if (!freeOnly || !IsPaid(companions[i].Source))
                    total += Stars(companions[i].Id);
            }
            return total;
        }

        public int MaxAlbumStars(bool freeOnly = false)
        {
            var total = 0;
            var companions = _rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                if (!freeOnly || !IsPaid(companions[i].Source))
                    total += _rules.Stages.Count + 1 + _rules.Forms.Forms.Count;
            }
            return total;
        }

        public double AlbumBonus(AlbumBonusKind kind)
        {
            var stars = AlbumStars(true);
            var bonuses = _rules.Album.Bonuses;
            var total = 0.0;
            for (var i = 0; i < bonuses.Count && bonuses[i].Stars <= stars; i++)
            {
                if (bonuses[i].Kind == kind)
                    total += bonuses[i].Value;
            }
            return total;
        }

        public AlbumBonus? NextAlbumBonus()
        {
            var stars = AlbumStars(true);
            var bonuses = _rules.Album.Bonuses;
            for (var i = 0; i < bonuses.Count; i++)
            {
                if (bonuses[i].Stars > stars)
                    return bonuses[i];
            }
            return null;
        }

        public int TripSlots => _rules.Album.BaseTrips == int.MaxValue ? int.MaxValue : _rules.Album.BaseTrips + (int)AlbumBonus(AlbumBonusKind.TripSlot) + Milestones(CompanionMilestone.TripSlot);
        public int HomeSlots => _rules.HomeSlots + Milestones(CompanionMilestone.HomeSlot) + (Gear?.HomeSlotPowers() ?? 0) + (int)TreeStat(MenagerieStat.HomeSlot);
        public bool CampLook => Milestones(CompanionMilestone.CampLook) > 0;

        public int Resonance()
        {
            var total = 0;
            var companions = _rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                if (Known(companions[i].Id))
                    total += LevelOf(companions[i].Id);
            }
            return total;
        }

        public double ResonanceBonus()
        {
            var rules = _rules.Resonance;
            return System.Math.Min(rules.Cap, Resonance() / rules.LevelsPerStep * rules.Step);
        }

        public int Milestones(CompanionMilestone kind)
        {
            var milestones = _rules.Resonance.Milestones;
            if (milestones.Count == 0)
                return 0;
            var resonance = Resonance();
            var count = 0;
            for (var i = 0; i < milestones.Count; i++)
            {
                if (milestones[i].Kind == kind && resonance >= milestones[i].Levels)
                    count++;
            }
            return count;
        }

        public int MasteryOf(string companion)
        {
            var trips = State.Progress.TryGetValue(companion, out var progress) ? progress.Trips : 0;
            var steps = _rules.Resonance.Mastery;
            var mastery = 0;
            while (mastery < steps.Count && trips >= steps[mastery].Trips)
                mastery++;
            return mastery;
        }

        public double MasteryExperience(string companion)
        {
            var mastery = MasteryOf(companion);
            return mastery == 0 ? 0 : _rules.Resonance.Mastery[mastery - 1].Experience;
        }
        public int NestBonusSlots => (int)AlbumBonus(AlbumBonusKind.NestSlot);
        public bool AlbumTitle => _rules.Album.Title.Length > 0 && AlbumBonus(AlbumBonusKind.Title) > 0;

        private bool IsPaid(string source)
        {
            var paid = _rules.Album.PaidSources;
            for (var i = 0; i < paid.Count; i++)
            {
                if (paid[i] == source)
                    return true;
            }
            return false;
        }

        public CompanionProgress Progress(string companion)
        {
            if (!State.Progress.TryGetValue(companion, out var progress))
            {
                progress = new CompanionProgress();
                State.Progress[companion] = progress;
            }
            return progress;
        }

        public int StageOf(string companion) => State.Progress.TryGetValue(companion, out var progress) ? progress.Stage : 0;

        public CompanionStage Stage(string companion) => _rules.StageAt(StageOf(companion));

        public bool AllowsHours(string companion, int hours)
        {
            var nodes = _rules.Talents.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Effect == TalentEffect.LongTrip && (int)nodes[i].Value == hours && StageOf(companion) >= nodes[i].Stage && Talent(companion, TalentEffect.LongTrip) > 0)
                    return true;
            }
            for (var stage = 0; stage <= StageOf(companion) && stage < _rules.Stages.Count; stage++)
            {
                var allowed = _rules.Stages[stage].Hours;
                for (var i = 0; i < allowed.Count; i++)
                {
                    if (allowed[i] == hours)
                        return true;
                }
            }
            return false;
        }

        public double BiomeBonus(string companion, string biome)
        {
            var def = _rules.Companion(companion);
            return def != null && (def.Biome == biome || RawOwn(companion, OwnEffectKind.AnyBiome, biome) > 0) ? _rules.OwnBiomeLoot + TreeStat(MenagerieStat.OwnBiomeLoot) : 0;
        }

        public CompanionStage? NextStage(string companion)
        {
            var next = StageOf(companion) + 1;
            return next < _rules.Stages.Count ? _rules.Stages[next] : null;
        }

        public CompanionResult CanEvolve(string companion)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var next = NextStage(companion);
            if (next == null)
                return CompanionResult.LastStage;
            if (LevelOf(companion) < next.Level)
                return CompanionResult.LowLevel;
            var def = _rules.Companion(companion);
            var place = def == null ? null : _rules.BiomeOf(def.Biome);
            if (next.Bones > 0 && (place == null || _camp.Count(place.Bone) < next.Bones))
                return CompanionResult.NoBones;
            if (_state.Drops.Shards < next.Shards)
                return CompanionResult.NoShards;
            if (next.Blooms > 0 && _camp.Count(_rules.Bloom) < next.Blooms)
                return CompanionResult.NoBlooms;
            if (FavouriteNeed(def, next) > FavouriteHeld(def))
                return CompanionResult.NoFavourite;
            if (next.Trophy && (place == null || !_state.Museum.Done.Contains(place.Collection)))
                return CompanionResult.NoTrophy;
            return CompanionResult.Done;
        }

        public CompanionResult Evolve(string companion)
        {
            var result = CanEvolve(companion);
            if (result != CompanionResult.Done)
                return result;
            var next = NextStage(companion)!;
            var def = _rules.Companion(companion);
            var place = def == null ? null : _rules.BiomeOf(def.Biome);
            if (next.Bones > 0)
                _camp.Take(place!.Bone, next.Bones);
            _state.Drops.Shards -= next.Shards;
            if (next.Blooms > 0)
                _camp.Take(_rules.Bloom, next.Blooms);
            TakeFavourite(def, FavouriteNeed(def, next));
            var progress = Progress(companion);
            progress.Stage++;
            if (Awakened(companion) && def != null)
                progress.Stars = System.Math.Max(progress.Stars, _rules.Stars.StartOf(def.Rarity));
            ApplyPerks();
            _events.Add(GameEvent.ForStage(GameEventKind.CompanionEvolved, _state.Tick, progress.Stage));
            return CompanionResult.Done;
        }

        private bool FavouriteIsDish(CompanionDef? def) => def != null && _cooking.Dish(def.Favourite) != null;

        private int FavouriteNeed(CompanionDef? def, CompanionStage stage)
        {
            if (def == null)
                return 0;
            return FavouriteIsDish(def) ? stage.FavouriteDishes : stage.FavouriteItems;
        }

        private long FavouriteHeld(CompanionDef? def)
        {
            if (def == null)
                return 0;
            if (FavouriteIsDish(def))
            {
                var dishes = 0;
                foreach (var meal in _state.Cooking.Meals)
                {
                    if (meal == def.Favourite)
                        dishes++;
                }
                return dishes;
            }
            var kind = _rules.Food(def.Favourite);
            if (kind == null)
                return 0;
            long held = 0;
            foreach (var item in _state.Camp.Bag)
            {
                if (item.Key.StartsWith(kind.Prefix, System.StringComparison.Ordinal) && item.Key != _rules.Bloom)
                    held += item.Value;
            }
            return held;
        }

        private void TakeFavourite(CompanionDef? def, int need)
        {
            if (def == null || need <= 0)
                return;
            if (FavouriteIsDish(def))
            {
                for (var i = 0; i < need; i++)
                    _state.Cooking.Meals.Remove(def.Favourite);
                return;
            }
            var kind = _rules.Food(def.Favourite)!;
            var ids = new System.Collections.Generic.List<string>();
            foreach (var item in _state.Camp.Bag)
            {
                if (item.Key.StartsWith(kind.Prefix, System.StringComparison.Ordinal) && item.Key != _rules.Bloom)
                    ids.Add(item.Key);
            }
            ids.Sort(System.StringComparer.Ordinal);
            long left = need;
            foreach (var id in ids)
            {
                var take = System.Math.Min(left, _camp.Count(id));
                _camp.Take(id, take);
                left -= take;
                if (left <= 0)
                    return;
            }
        }

        public int LevelOf(string companion) => State.Progress.TryGetValue(companion, out var progress) ? progress.Level : 1;

        public double LootBonus(string companion) => (LevelOf(companion) - 1) * _rules.LootPerLevel;

        public double ShorterCut(string companion) => LevelOf(companion) / _rules.ShorterEveryLevels * _rules.ShorterStep;

        public double DurationFactor(string companion, double perk, string biome = "", int hours = 0)
        {
            var cut = System.Math.Min(ShorterCut(companion) + Own(companion, OwnEffectKind.BiomeShorter, biome) + Talent(companion, TalentEffect.Shorter), System.Math.Max(0, _rules.ShorterCap - perk));
            return System.Math.Max(0.1, 1 - perk - cut) * (1 + Own(companion, OwnEffectKind.Longer)) * (1 - (FormOf(companion)?.Speed ?? 0)) * System.Math.Max(0.5, 1 - GearStat(companion, CompanionGearStat.Duration)) * System.Math.Max(0.5, 1 - TreeStat(MenagerieStat.TripTime))
                * System.Math.Max(0.5, 1 + Temper(companion, TemperStat.TripTime) + (hours > 0 && hours < _rules.Tempers.LongTripHours ? Temper(companion, TemperStat.ShortTripTime) : 0));
        }

        public double FoodLeft(string companion) =>
            System.Math.Max(0, _rules.FoodPerDay * (1 + Temper(companion, TemperStat.FoodCap)) + TreeStat(MenagerieStat.FoodCap) - (State.Progress.TryGetValue(companion, out var progress) ? progress.FedToday : 0));

        public int Hearts(string companion)
        {
            var bond = State.Progress.TryGetValue(companion, out var progress) ? progress.Bond : 0;
            var rules = _rules.Bond;
            var hearts = 0;
            while (hearts < rules.MaxHearts && bond >= rules.PointsTo(hearts + 1))
                hearts++;
            return hearts;
        }

        public void AddBond(string companion, double points)
        {
            if (points <= 0)
                return;
            var before = Hearts(companion);
            var progress = Progress(companion);
            points *= 1 + GearStat(companion, CompanionGearStat.Bond) + HomeOwn(OwnEffectKind.Bond) + Temper(companion, TemperStat.Bond);
            progress.Bond = System.Math.Min(_rules.Bond.PointsTo(_rules.Bond.MaxHearts), progress.Bond + points);
            var after = Hearts(companion);
            if (after > before)
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionHeart, _state.Tick, after));
        }

        public CompanionResult Pet(string companion)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var progress = Progress(companion);
            var rules = _rules.Bond;
            if (progress.PetsToday >= rules.PetsPerDay)
                return CompanionResult.Full;
            if (progress.LastPetTick != long.MinValue && _state.Tick - progress.LastPetTick < rules.PetEveryTicks)
                return CompanionResult.TooSoon;
            progress.PetsToday++;
            progress.LastPetTick = _state.Tick;
            AddBond(companion, rules.PetPoints * (1 + TreeStat(MenagerieStat.PetBond)));
            return CompanionResult.Done;
        }

        public CompanionResult FriendPet(string companion)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var progress = Progress(companion);
            if (progress.FriendPetsToday >= _rules.Bond.FriendPetsPerDay)
                return CompanionResult.Full;
            progress.FriendPetsToday++;
            AddBond(companion, _rules.Bond.FriendPetPoints);
            return CompanionResult.Done;
        }

        public CompanionResult Rename(string companion, string name)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (Hearts(companion) < _rules.Bond.RenameHearts)
                return CompanionResult.Locked;
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length > _rules.Bond.NameLength)
                return CompanionResult.BadName;
            foreach (var symbol in trimmed)
            {
                if (char.IsControl(symbol))
                    return CompanionResult.BadName;
            }
            Progress(companion).Name = trimmed;
            return CompanionResult.Done;
        }

        public CompanionResult ChooseAccessory(string companion, string accessory)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (Hearts(companion) < _rules.Bond.AccessoryHearts)
                return CompanionResult.Locked;
            var known = false;
            foreach (var item in _rules.Bond.Accessories)
                known |= item == accessory;
            if (!known)
                return CompanionResult.UnknownFood;
            Progress(companion).Accessory = accessory;
            return CompanionResult.Done;
        }

        public int StoryUnlocked(string companion)
        {
            var hearts = Hearts(companion);
            var letters = 0;
            var rules = _rules.Bond;
            for (var i = 0; i < rules.ChapterHearts.Count && i < rules.Chapters.Count; i++)
            {
                if (hearts >= rules.ChapterHearts[i])
                    letters += rules.Chapters[i].Count;
            }
            return letters;
        }

        public List<(string Ru, string En)> StoryLetters(string companion)
        {
            var letters = new List<(string, string)>();
            var read = State.Progress.TryGetValue(companion, out var progress) ? progress.Story : 0;
            foreach (var chapter in _rules.Bond.Chapters)
            {
                foreach (var letter in chapter)
                {
                    if (letters.Count >= read)
                        return letters;
                    letters.Add(letter);
                }
            }
            return letters;
        }

        public double Talent(string companion, TalentEffect effect)
        {
            if (!State.Progress.TryGetValue(companion, out var progress) || progress.Talents.Count == 0)
                return 0;
            var total = 0.0;
            var nodes = _rules.Talents.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Effect == effect && progress.Talents.Contains(nodes[i].Id))
                    total += nodes[i].Value;
            }
            return total;
        }

        public int TalentPoints(string companion) => LevelOf(companion) / _rules.Talents.EveryLevels + (int)TreeStat(MenagerieStat.TalentPoints);

        public int FreeTalentPoints(string companion) =>
            TalentPoints(companion) - (State.Progress.TryGetValue(companion, out var progress) ? progress.Talents.Count : 0);

        public CompanionResult Learn(string companion, string talent)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var node = _rules.Talents.Node(talent);
            if (node == null)
                return CompanionResult.UnknownTrait;
            var progress = Progress(companion);
            if (progress.Talents.Contains(talent))
                return CompanionResult.Learned;
            var previous = _rules.Talents.Previous(node);
            if (previous != null && !progress.Talents.Contains(previous.Id))
                return CompanionResult.NeedsPrevious;
            if (StageOf(companion) < node.Stage)
                return CompanionResult.LowLevel;
            if (FreeTalentPoints(companion) <= 0)
                return CompanionResult.NoPoints;
            progress.Talents.Add(talent);
            ApplyPerks();
            return CompanionResult.Done;
        }

        public bool FreeResetReady(string companion)
        {
            var last = State.Progress.TryGetValue(companion, out var progress) ? progress.FreeResetDay : -1;
            return last < 0 || State.Day - last >= _rules.Talents.FreeResetDays;
        }

        public CompanionResult ResetTalents(string companion)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var progress = Progress(companion);
            if (FreeResetReady(companion))
                progress.FreeResetDay = State.Day;
            else if (_state.Drops.Shards >= _rules.Talents.ResetShards)
                _state.Drops.Shards -= _rules.Talents.ResetShards;
            else
                return CompanionResult.NoShards;
            progress.Talents.Clear();
            ApplyPerks();
            return CompanionResult.Done;
        }

        public void SetDay(long day)
        {
            if (day == State.Day)
                return;
            var first = State.Day < 0;
            State.Day = day;
            Work?.SetDay(day);
            if (!first)
                GiveDailyItems();
            foreach (var progress in State.Progress.Values)
            {
                progress.FedToday = 0;
                progress.PetsToday = 0;
                progress.FavouriteBondsToday = 0;
                progress.FriendPetsToday = 0;
            }
        }

        public bool IsFavourite(string companion, string food)
        {
            var def = _rules.Companion(companion);
            if (def == null)
                return false;
            if (def.Favourite == food)
                return true;
            var kind = _rules.FoodOf(food);
            return kind != null && kind.Id == def.Favourite;
        }

        public CompanionResult Feed(string companion, string food)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var treat = _rules.Treats.Treat(food);
            if (treat != null && food.StartsWith(CompanionTreatRules.Prefix, System.StringComparison.Ordinal))
                return FeedTreat(companion, treat);
            var dish = _cooking.Dish(food) != null;
            var kind = dish ? null : _rules.FoodOf(food);
            if (!dish && kind == null)
                return CompanionResult.UnknownFood;
            var left = FoodLeft(companion);
            if (left <= 0)
                return CompanionResult.Full;
            if (dish ? !_state.Cooking.Meals.Remove(food) : !_camp.Take(food, 1))
                return CompanionResult.NoFood;
            var experience = (dish ? _rules.DishExperience : kind!.Experience) * (IsFavourite(companion, food) ? _rules.FavouriteMultiplier : 1)
                * (1 + AlbumBonus(AlbumBonusKind.FoodExperience) + TreeStat(MenagerieStat.FoodExperience) + Temper(companion, TemperStat.FoodExperience));
            experience = System.Math.Min(experience, left);
            var progress = Progress(companion);
            progress.FedToday += experience;
            progress.FavouriteKnown = true;
            progress.Fed++;
            if (IsFavourite(companion, food) && progress.FavouriteBondsToday < _rules.Bond.FavouritePerDay + (int)Talent(companion, TalentEffect.FavouriteBond))
            {
                progress.FavouriteBondsToday++;
                AddBond(companion, _rules.Bond.FavouritePoints);
            }
            Gain(companion, experience);
            return CompanionResult.Done;
        }

        public CompanionResult Cook(string treatId)
        {
            var treat = _rules.Treats.Treat(treatId);
            if (treat == null)
                return CompanionResult.UnknownFood;
            for (var i = 0; i < treat.Ingredients.Count; i++)
            {
                if (_camp.Count(treat.Ingredients[i]) < Needed(treat, treat.Ingredients[i]))
                    return CompanionResult.NoIngredients;
            }
            for (var i = 0; i < treat.Ingredients.Count; i++)
                _camp.Take(treat.Ingredients[i], 1);
            _camp.Add(treat.Item, 1);
            return CompanionResult.Done;
        }

        private static int Needed(CompanionTreat treat, string item)
        {
            var count = 0;
            for (var i = 0; i < treat.Ingredients.Count; i++)
            {
                if (treat.Ingredients[i] == item)
                    count++;
            }
            return count;
        }

        public bool DelicacyReady(string companion) =>
            !State.Progress.TryGetValue(companion, out var progress) || State.Day - progress.DelicacyDay >= _rules.Treats.DelicacyDays;

        private CompanionResult FeedTreat(string companion, CompanionTreat treat)
        {
            var progress = Progress(companion);
            if (treat.Delicacy)
            {
                if (progress.Level >= System.Math.Min(_rules.Treats.DelicacyMaxLevel, MaxLevelOf(companion)))
                    return CompanionResult.Full;
                if (!DelicacyReady(companion))
                    return CompanionResult.TooSoon;
                if (!_camp.Take(treat.Item, 1))
                    return CompanionResult.NoFood;
                progress.DelicacyDay = State.Day;
                progress.Level++;
                progress.Fed++;
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionLevelUp, _state.Tick, progress.Level));
                ApplyPerks();
                return CompanionResult.Done;
            }
            var left = FoodLeft(companion);
            if (left <= 0)
                return CompanionResult.Full;
            if (!_camp.Take(treat.Item, 1))
                return CompanionResult.NoFood;
            var experience = System.Math.Min(treat.Experience * (1 + AlbumBonus(AlbumBonusKind.FoodExperience) + TreeStat(MenagerieStat.TreatExperience)), left);
            progress.FedToday += experience;
            progress.Fed++;
            if (treat.Stat != TreatStat.None)
                progress.Treat = treat.Id;
            Gain(companion, experience);
            return CompanionResult.Done;
        }

        public void PackTreat(Expedition expedition)
        {
            if (!State.Progress.TryGetValue(expedition.Companion, out var progress) || progress.Treat.Length == 0)
                return;
            expedition.Treat = progress.Treat;
            progress.Treat = string.Empty;
        }

        public double TreatBonus(Expedition expedition, TreatStat stat)
        {
            if (expedition.Treat.Length == 0)
                return 0;
            var treat = _rules.Treats.Treat(expedition.Treat);
            return treat != null && treat.Stat == stat ? treat.Value : 0;
        }

        public void TripDone(Expedition expedition)
        {
            Work?.NoteTrip(expedition);
            TripDone(expedition, expedition.Companion);
            if (expedition.Partner.Length > 0)
            {
                TripDone(expedition, expedition.Partner);
                if (Friends(expedition.Companion, expedition.Partner))
                    _events.Add(GameEvent.ForStage(GameEventKind.CompanionPairScene, _state.Tick, PairIndex(expedition.Companion, expedition.Partner)));
            }
        }

        private void TripDone(Expedition expedition, string companion)
        {
            var planned = expedition.Planned > 0 ? expedition.Planned : expedition.Hours * ExpeditionSystem.SecondsPerHour;
            var share = planned <= 0 ? 1 : System.Math.Min(1, expedition.Elapsed / planned);
            Gain(companion, expedition.Hours * share * _rules.ExperiencePerTripHour * OwnMultiplier(companion, OwnEffectKind.Experience));
            NoteTripHours(expedition.Hours * share);
            Progress(companion).Trips++;
            AddBond(companion, _rules.Bond.TripPoints);
            var progress = Progress(companion);
            if (progress.Story < StoryUnlocked(companion))
            {
                progress.Story++;
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionStory, _state.Tick, progress.Story));
            }
            var baseTrait = BaseTrait(companion);
            if (baseTrait.Length > 0)
                AddTrip(companion, progress, baseTrait, false);
            for (var i = 0; i < progress.Traits.Count; i++)
                AddTrip(companion, progress, progress.Traits[i], false);
        }

        public int PairIndex(string a, string b)
        {
            var pairs = _rules.Pairs;
            for (var i = 0; i < pairs.Count; i++)
            {
                if ((pairs[i].A == a && pairs[i].B == b) || (pairs[i].A == b && pairs[i].B == a))
                    return i;
            }
            return -1;
        }

        public bool Friends(string a, string b) => PairIndex(a, b) >= 0;

        public double JointFactor(string companion, string partner) =>
            (Friends(companion, partner) ? _rules.FriendsLoot : _rules.JointLoot) * (1 + Talent(companion, TalentEffect.JointLoot) + Talent(partner, TalentEffect.JointLoot)
                + TreeStat(MenagerieStat.JointLoot) + (Friends(companion, partner) ? TreeStat(MenagerieStat.FriendsLoot) : 0));

        public double StatOf(Expedition expedition, TravelStat stat) =>
            PairStat(expedition, stat) * (1 + TreatBonus(expedition, TreatStatOf(stat))) * (1 + TreeLoot(stat)) * System.Math.Max(0, 1 + TemperTrip(expedition, stat));

        private double TemperTrip(Expedition expedition, TravelStat stat)
        {
            if (stat == TravelStat.Rare)
                return 0;
            var main = expedition.Companion;
            var bonus = expedition.Partner.Length == 0
                ? Temper(main, TemperStat.SoloLoot)
                : Temper(main, TemperStat.JointLoot) + Temper(expedition.Partner, TemperStat.JointLoot);
            if (expedition.Hours >= _rules.Tempers.LongTripHours)
                bonus += Temper(main, TemperStat.LongTripLoot);
            return bonus;
        }

        private double TreeLoot(TravelStat stat) =>
            stat == TravelStat.Items ? TreeStat(MenagerieStat.TripItems) + TreeStat(MenagerieStat.TripLoot)
            : stat == TravelStat.Shards ? TreeStat(MenagerieStat.TripShards) + TreeStat(MenagerieStat.TripLoot)
            : stat == TravelStat.Rare ? TreeStat(MenagerieStat.TripRare) : 0;

        private static TreatStat TreatStatOf(TravelStat stat) =>
            stat == TravelStat.Items ? TreatStat.Items : stat == TravelStat.Shards ? TreatStat.Shards : stat == TravelStat.Rare ? TreatStat.Rare : TreatStat.None;

        private double PairStat(Expedition expedition, TravelStat stat)
        {
            var main = TravelMultiplier(expedition.Companion, stat);
            if (expedition.Partner.Length == 0)
                return main;
            var partner = TravelMultiplier(expedition.Partner, stat);
            var pack = Talent(expedition.Companion, TalentEffect.PackTrait);
            partner = 1 + (partner - 1) * (1 + pack);
            var best = System.Math.Max(main, partner);
            return stat == TravelStat.Rare ? best : best * JointFactor(expedition.Companion, expedition.Partner);
        }

        public double HerbOf(Expedition expedition, string item)
        {
            var main = HerbMultiplier(expedition.Companion, item);
            var best = expedition.Partner.Length == 0 ? main : System.Math.Max(main, HerbMultiplier(expedition.Partner, item));
            return best * (1 + (item.StartsWith("herb_", System.StringComparison.Ordinal) ? TreeStat(MenagerieStat.TripHerbs)
                : item.StartsWith("fish_", System.StringComparison.Ordinal) ? TreeStat(MenagerieStat.TripFish) : 0));
        }

        public double LevelOf(Expedition expedition)
        {
            var main = (1 + LootBonus(expedition.Companion)) * (1 + BiomeBonus(expedition.Companion, expedition.Biome));
            if (expedition.Partner.Length == 0)
                return main;
            return System.Math.Max(main, (1 + LootBonus(expedition.Partner)) * (1 + BiomeBonus(expedition.Partner, expedition.Biome)));
        }

        public double RareOf(Expedition expedition)
        {
            var main = RareMultiplier(expedition.Companion);
            return expedition.Partner.Length == 0 ? main : System.Math.Max(main, RareMultiplier(expedition.Partner));
        }

        public string BaseTrait(string companion)
        {
            var def = _rules.Companion(companion);
            if (def != null && def.Trait.Length > 0 && Known(companion))
                return def.Trait;
            return _expeditions.TraitOf(companion)?.Id ?? string.Empty;
        }

        public bool Owns(string companion) => State.Owned.Contains(companion);

        public string GrantedTrait(string companion)
        {
            var def = _rules.Companion(companion);
            return def != null && def.BaseTrait.Length > 0 ? def.BaseTrait : _expeditions.Rules.CosmeticTrait;
        }

        public bool Grant(string companion)
        {
            if (_rules.Companion(companion) == null || Known(companion))
                return false;
            State.Owned.Add(companion);
            ApplyPerks();
            return true;
        }

        public bool GrantRolled(string companion)
        {
            if (!Grant(companion))
                return false;
            var roll = _state.Rng.NextDouble();
            var coats = _rules.Coats;
            for (var i = coats.Count - 1; i >= 0; i--)
            {
                if (roll < coats[i].Chance)
                {
                    Progress(companion).Coat = coats[i].Id;
                    break;
                }
            }
            return true;
        }

        public CompanionForm? FormOf(string companion) =>
            State.Progress.TryGetValue(companion, out var progress) && progress.Form.Length > 0 ? _rules.Forms.Form(progress.Form) : null;

        public CompanionResult ChooseForm(string companion, string form)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var rule = _rules.Forms.Form(form);
            if (rule == null)
                return CompanionResult.UnknownTrait;
            if (StageOf(companion) < rule.Stage)
                return CompanionResult.LowLevel;
            if (StarsOf(companion) < rule.Stars)
                return CompanionResult.Locked;
            var progress = Progress(companion);
            if (progress.Form == form)
                return CompanionResult.Repeat;
            if (progress.Form.Length > 0)
            {
                if (State.Souls < _rules.Forms.ChangeSouls)
                    return CompanionResult.NoSouls;
                State.Souls -= _rules.Forms.ChangeSouls;
            }
            progress.Form = form;
            if (!progress.Forms.Contains(form))
                progress.Forms.Add(form);
            ApplyPerks();
            _events.Add(GameEvent.ForStage(GameEventKind.CompanionEvolved, _state.Tick, progress.Stage));
            return CompanionResult.Done;
        }

        public double FormLoot(string companion) => FormOf(companion)?.Loot ?? 0;

        public CompanionGearSystem? Gear { get; set; }
        public CompanionWorkSystem? Work { get; set; }

        public bool Occupied(string companion) => AtHome(companion) || Work?.Helping(companion) == true;

        public double GearStat(string companion, CompanionGearStat stat) => Gear?.Stat(companion, stat) ?? 0;

        public MenagerieTreeSystem? Tree { get; internal set; }

        public double TreeStat(MenagerieStat stat) => Tree?.Stat(stat) ?? 0;

        public int StarPrice(int price) => (int)System.Math.Ceiling(price * System.Math.Max(0, 1 - TreeStat(MenagerieStat.StarCost)) - 1e-9);

        public int StarsOf(string companion) => State.Progress.TryGetValue(companion, out var progress) ? progress.Stars : 0;

        public int MaxLevelOf(string companion) => _rules.MaxLevel + StarsOf(companion) * _rules.Stars.LevelsPerStar;

        public double StarFactor(string companion) => 1 + StarsOf(companion) * _rules.Stars.TraitsPerStar;

        public bool Awakened(string companion) => StageOf(companion) >= _rules.Stages.Count - 1;

        public StarCost? NextStarCost(string companion)
        {
            var stars = StarsOf(companion);
            return stars < _rules.Stars.MaxStars ? _rules.Stars.Costs[stars] : null;
        }

        public CompanionResult RaiseStar(string companion, bool souls)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (!Awakened(companion))
                return CompanionResult.NotAwakened;
            var cost = NextStarCost(companion);
            if (cost == null)
                return CompanionResult.MaxStars;
            var progress = Progress(companion);
            if (souls)
            {
                if (State.Souls < StarPrice(cost.Souls))
                    return CompanionResult.NoSouls;
                State.Souls -= StarPrice(cost.Souls);
            }
            else
            {
                if (progress.OwnSparks < StarPrice(cost.Sparks))
                    return CompanionResult.NoSouls;
                progress.OwnSparks -= StarPrice(cost.Sparks);
            }
            progress.Stars++;
            ApplyPerks();
            _events.Add(GameEvent.ForStage(GameEventKind.CompanionEvolved, _state.Tick, progress.Stage));
            return CompanionResult.Done;
        }

        public void AddSouls(long souls) => State.Souls += (long)System.Math.Floor(System.Math.Max(0, souls) * (1 + TreeStat(MenagerieStat.Souls) + HomeOwn(OwnEffectKind.Souls)) + 1e-9);

        public double HomeOwn(OwnEffectKind kind)
        {
            var total = 0.0;
            for (var i = 0; i < State.Home.Count; i++)
                total += Own(State.Home[i], kind) * (1 + Temper(State.Home[i], TemperStat.HomeTraits));
            return total;
        }

        public double FirstTripCut(string companion)
        {
            var cut = Own(companion, OwnEffectKind.FirstTrip);
            if (cut <= 0)
                return 0;
            var progress = Progress(companion);
            if (progress.FirstTripDay == State.Day)
                return 0;
            progress.FirstTripDay = State.Day;
            return System.Math.Min(0.5, cut);
        }

        public double RewindHours()
        {
            var hours = 0.0;
            for (var i = 0; i < State.Home.Count; i++)
                hours = System.Math.Max(hours, Own(State.Home[i], OwnEffectKind.Rewind));
            return hours;
        }

        public CompanionResult Rewind(int index)
        {
            var active = _expeditions.State.Active;
            if (index < 0 || index >= active.Count)
                return CompanionResult.UnknownCompanion;
            var hours = RewindHours();
            if (hours <= 0)
                return CompanionResult.NotHome;
            if (State.RewindDay == State.Day)
                return CompanionResult.TooSoon;
            if ((active[index].Seconds - active[index].Elapsed) / ExpeditionSystem.SecondsPerHour > hours + 1e-9)
                return CompanionResult.Busy;
            State.RewindDay = State.Day;
            _expeditions.Finish(index);
            return CompanionResult.Done;
        }

        public void NoteTripHours(double hours)
        {
            State.SoulHours += hours;
            var every = _rules.Stars.SoulsPerTripHours;
            while (State.SoulHours >= every)
            {
                State.SoulHours -= every;
                State.Souls++;
            }
        }

        public void NoteHost(long day)
        {
            if (State.SoulHostDay == day)
                return;
            State.SoulHostDay = day;
            State.Souls += _rules.Stars.SoulsPerHost;
        }

        public double TraitFactor(string companion)
        {
            var def = _rules.Companion(companion);
            var factor = (def == null ? 1 : _rules.RarityFactor(def.Rarity)) * StarFactor(companion) * (1 + TreeStat(MenagerieStat.OwnTraits));
            if (State.Progress.TryGetValue(companion, out var progress) && progress.Coat.Length > 0)
                factor *= 1 + (_rules.Coat(progress.Coat)?.TraitBonus ?? 0);
            return factor;
        }

        public double AllExperienceBonus()
        {
            var total = 0.0;
            var companions = _rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                if (companions[i].Trait.Length > 0 && Known(companions[i].Id))
                    total += Own(companions[i].Id, OwnEffectKind.AllExperience);
            }
            return total;
        }

        public void GiveDailyItems()
        {
            var companions = _rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                var own = companions[i].Trait.Length > 0 ? _rules.Own(companions[i].Trait) : null;
                if (own == null || !Known(companions[i].Id))
                    continue;
                for (var e = 0; e < own.Effects.Count; e++)
                {
                    if (own.Effects[e].Kind != OwnEffectKind.DailyItem)
                        continue;
                    GiveDaily(own.Effects[e].Item, (long)System.Math.Floor(own.Effects[e].At(RankOf(companions[i].Id, own.Id)) * TraitFactor(companions[i].Id) + 1e-9));
                }
            }
            for (var i = 0; i < companions.Count; i++)
            {
                if (!Known(companions[i].Id))
                    continue;
                var branch = companions[i].Branch;
                var stage = StageOf(companions[i].Id);
                for (var b = 0; b < branch.Count; b++)
                {
                    if (branch[b].Stage <= stage && branch[b].Effect.Kind == OwnEffectKind.DailyItem)
                        GiveDaily(branch[b].Effect.Item, (long)System.Math.Floor(branch[b].Effect.Values[0] * TraitFactor(companions[i].Id) + 1e-9));
                }
            }
        }

        private void GiveDaily(string item, long amount)
        {
            if (amount <= 0)
                return;
            if (item == IngotsItem)
                _state.Drops.Ingots += amount;
            else if (item == ShardsItem)
                _state.Drops.Shards += amount;
            else
                _camp.Add(item, amount);
        }

        public const string ShardsItem = "shards";

        public const string IngotsItem = "ingots";

        private double RawOwn(string companion, OwnEffectKind kind, string biome)
        {
            var trait = BaseTrait(companion);
            var own = trait.Length == 0 ? null : _rules.Own(trait);
            var total = 0.0;
            if (own != null)
            {
                for (var i = 0; i < own.Effects.Count; i++)
                {
                    var effect = own.Effects[i];
                    if (effect.Kind == kind && (effect.Biome.Length == 0 || effect.Biome == biome))
                        total += effect.At(RankOf(companion, trait));
                }
            }
            var branch = _rules.Companion(companion)?.Branch;
            if (branch == null || branch.Count == 0)
                return total;
            var stage = StageOf(companion);
            for (var i = 0; i < branch.Count; i++)
            {
                var effect = branch[i].Effect;
                if (branch[i].Stage > stage || effect.Kind != kind || (effect.Biome.Length > 0 && effect.Biome != biome))
                    continue;
                total = OwnEffect.Multiplies(kind) ? (total > 0 ? total : 1) * effect.Values[0] : total + effect.Values[0];
            }
            return total;
        }

        public double Own(string companion, OwnEffectKind kind, string biome = "") => RawOwn(companion, kind, biome) * TraitFactor(companion);

        public double OwnMultiplier(string companion, OwnEffectKind kind, string biome = "")
        {
            var value = RawOwn(companion, kind, biome);
            return value > 0 ? 1 + (value - 1) * TraitFactor(companion) : 1;
        }

        public bool IsNight => _state.ServerHour >= 0 && !_conditions.IsDay(_state.ServerHour);

        public List<string> TraitsOf(string companion)
        {
            var traits = new List<string>();
            var baseTrait = BaseTrait(companion);
            if (baseTrait.Length > 0)
                traits.Add(baseTrait);
            if (State.Progress.TryGetValue(companion, out var progress))
                traits.AddRange(progress.Traits);
            return traits;
        }

        public int RankOf(string companion, string trait)
        {
            var trips = State.Progress.TryGetValue(companion, out var progress) && progress.TraitTrips.TryGetValue(trait, out var stored) ? stored : 0;
            var rank = 1;
            for (var i = 0; i < _rules.RankTrips.Count; i++)
            {
                if (trips >= _rules.RankTrips[i])
                    rank++;
            }
            return System.Math.Min(rank, Stage(companion).RankCap);
        }

        public double TravelMultiplier(string companion, TravelStat stat)
        {
            var multiplier = 1.0;
            var baseTrait = BaseTrait(companion);
            multiplier *= TravelValue(companion, baseTrait.Length > 0 ? baseTrait : _expeditions.Rules.CosmeticTrait, stat);
            if (State.Progress.TryGetValue(companion, out var progress))
            {
                for (var i = 0; i < progress.Traits.Count; i++)
                    multiplier *= TravelValue(companion, progress.Traits[i], stat);
            }
            var extra = stat == TravelStat.Rare
                ? GearStat(companion, CompanionGearStat.Rare) + Temper(companion, TemperStat.TripRare)
                : FormLoot(companion) + GearStat(companion, CompanionGearStat.Loot) + Temper(companion, TemperStat.TripLoot) + (stat == TravelStat.Items ? Temper(companion, TemperStat.TripItems) : 0);
            return (1 + (multiplier - 1) * StarFactor(companion)) * (1 + extra);
        }

        private double TravelValue(string companion, string trait, TravelStat stat)
        {
            var own = _rules.Own(trait);
            if (own != null)
            {
                return stat switch
                {
                    TravelStat.Items => OwnMultiplier(companion, OwnEffectKind.Loot) * OwnMultiplier(companion, OwnEffectKind.Items),
                    TravelStat.Shards => OwnMultiplier(companion, OwnEffectKind.Loot) * OwnMultiplier(companion, OwnEffectKind.Shards),
                    _ => OwnMultiplier(companion, OwnEffectKind.Rare)
                };
            }
            var ranks = _rules.Ranks(trait);
            if (ranks != null)
                return !ranks.Home && ranks.Stat == stat ? ranks.At(RankOf(companion, trait)) : 1;
            var plain = _expeditions.Rules.Trait(trait);
            if (plain == null)
                return 1;
            return stat switch
            {
                TravelStat.Items => plain.ItemMultiplier,
                TravelStat.Shards => plain.ShardsMultiplier,
                _ => plain.RareMultiplier
            };
        }

        public void Extras(Expedition expedition, ExpeditionBiome place)
        {
            Extras(expedition, place, expedition.Companion);
            if (expedition.Partner.Length > 0)
                Extras(expedition, place, expedition.Partner);
        }

        private void Extras(Expedition expedition, ExpeditionBiome place, string companion)
        {
            var extra = (long)System.Math.Floor(Own(companion, OwnEffectKind.ExtraItems) + Talent(companion, TalentEffect.ExtraItems));
            if (extra > 0 && place.Items.Count > 0)
            {
                expedition.Items[0] += extra;
                _camp.Add(place.Items[0].Item, extra);
            }
            var fish = (long)System.Math.Floor(Own(companion, OwnEffectKind.Fish));
            var fishId = _rules.FishOf(expedition.Biome);
            if (fish > 0 && fishId.Length > 0)
                _camp.Add(fishId, fish);
            var bone = Own(companion, OwnEffectKind.Bone);
            var place2 = _rules.BiomeOf(expedition.Biome);
            if (bone > 0 && place2 != null && _state.Rng.NextDouble() < bone)
                _camp.Add(place2.Bone, 1);
        }

        public double HerbMultiplier(string companion, string item) =>
            item.StartsWith("herb_", System.StringComparison.Ordinal) ? OwnMultiplier(companion, OwnEffectKind.Herbs) : 1;

        public double RareNightMultiplier(string companion) => IsNight ? OwnMultiplier(companion, OwnEffectKind.RareNight) : 1;

        public double RareMultiplier(string companion) => RareNightMultiplier(companion) * (1 + Talent(companion, TalentEffect.Rare));

        public List<string> TraitChoices(string companion)
        {
            var choices = new List<string>();
            var owned = TraitsOf(companion);
            foreach (var ranks in _rules.TraitRanks)
            {
                if (!owned.Contains(ranks.Trait))
                    choices.Add(ranks.Trait);
            }
            return choices;
        }

        public int FreeTraitSlots(string companion)
        {
            var chosen = State.Progress.TryGetValue(companion, out var progress) ? progress.Traits.Count : 0;
            return System.Math.Max(0, Stage(companion).TraitSlots - 1 - chosen);
        }

        public CompanionResult ChooseTrait(string companion, string trait)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (_rules.Ranks(trait) == null)
                return CompanionResult.UnknownTrait;
            if (TraitsOf(companion).Contains(trait))
                return CompanionResult.Repeat;
            if (FreeTraitSlots(companion) <= 0)
                return CompanionResult.NoSlot;
            var progress = Progress(companion);
            progress.Traits.Add(trait);
            progress.TraitTrips.Remove(trait);
            ApplyPerks();
            return CompanionResult.Done;
        }

        public CompanionResult ChangeTrait(string companion, int slot, string trait)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            var progress = Progress(companion);
            if (slot < 0 || slot >= progress.Traits.Count)
                return CompanionResult.NoSlot;
            if (_rules.Ranks(trait) == null)
                return CompanionResult.UnknownTrait;
            if (TraitsOf(companion).Contains(trait))
                return CompanionResult.Repeat;
            var price = (long)System.Math.Ceiling(_rules.TraitChangeShards * System.Math.Max(0, 1 - HomeOwn(OwnEffectKind.TraitPrice)) * System.Math.Max(0, 1 + Temper(companion, TemperStat.TraitChangePrice)) - 1e-9);
            if (_state.Drops.Shards < price)
                return CompanionResult.NoShards;
            _state.Drops.Shards -= price;
            progress.TraitTrips.Remove(progress.Traits[slot]);
            progress.Traits[slot] = trait;
            progress.TraitTrips.Remove(trait);
            ApplyPerks();
            return CompanionResult.Done;
        }

        public CompanionTemper? TemperOf(string companion)
        {
            if (State.Progress.TryGetValue(companion, out var progress) && progress.Temper.Length > 0)
                return _rules.Tempers.Temper(progress.Temper);
            return State.Owned.Contains(companion) ? _rules.Tempers.Born(companion, _state.StartedUnixMs) : null;
        }

        public double Temper(string companion, TemperStat stat)
        {
            var temper = TemperOf(companion);
            if (temper == null)
                return 0;
            var plus = TemperSum(companion, temper.Plus, stat) * (1 + TreeStat(MenagerieStat.TemperPower));
            var minus = TemperSum(companion, temper.Minus, stat) * System.Math.Max(0, 1 - TreeStat(MenagerieStat.TemperMinus));
            return plus + minus;
        }

        private double TemperSum(string companion, IReadOnlyList<TemperLine> lines, TemperStat stat)
        {
            var total = 0.0;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Stat == stat && (lines[i].UntilHearts == 0 || Hearts(companion) < lines[i].UntilHearts))
                    total += lines[i].Value;
            }
            return total;
        }

        public bool TemperFreeReady(string companion) =>
            !State.Progress.TryGetValue(companion, out var progress) || progress.TemperFreeDay < 0 || State.Day < 0 || State.Day - progress.TemperFreeDay >= _rules.Tempers.FreeEveryDays;

        public CompanionResult ChangeTemper(string companion, string temper)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (_rules.Tempers.Temper(temper) == null)
                return CompanionResult.UnknownTrait;
            if (TemperOf(companion)?.Id == temper)
                return CompanionResult.Repeat;
            var free = TemperFreeReady(companion);
            if (!free && State.Souls < _rules.Tempers.ChangeSouls)
                return CompanionResult.NoSouls;
            var progress = Progress(companion);
            if (free)
                progress.TemperFreeDay = State.Day;
            else
                State.Souls -= _rules.Tempers.ChangeSouls;
            progress.Temper = temper;
            ApplyPerks();
            return CompanionResult.Done;
        }

        public bool AtHome(string companion) => State.Home.Contains(companion);

        public CompanionResult StayHome(string companion)
        {
            if (!Known(companion))
                return CompanionResult.UnknownCompanion;
            if (AtHome(companion))
                return CompanionResult.Done;
            if (_expeditions.Busy(companion))
                return CompanionResult.Busy;
            if (State.Home.Count >= HomeSlots)
                return CompanionResult.NoHomeSlot;
            State.Home.Add(companion);
            ApplyPerks();
            return CompanionResult.Done;
        }

        public CompanionResult LeaveHome(string companion)
        {
            if (!State.Home.Remove(companion))
                return CompanionResult.NotHome;
            ApplyPerks();
            return CompanionResult.Done;
        }

        public void Tick() => Advance(GameLoop.TickSeconds);

        public void Advance(double seconds)
        {
            if (seconds <= 0)
                return;
            Work?.Advance(seconds);
            var changed = false;
            var step = _rules.HomeHoursPerTrip * ExpeditionSystem.SecondsPerHour;
            for (var i = 0; i < State.Home.Count; i++)
            {
                var companion = State.Home[i];
                var progress = Progress(companion);
                progress.HomeSeconds += seconds;
                HomeTalents(companion, progress, seconds);
                while (progress.HomeSeconds >= step)
                {
                    progress.HomeSeconds -= step;
                    for (var t = 0; t < progress.Traits.Count; t++)
                        changed |= AddTrip(companion, progress, progress.Traits[t], true);
                }
            }
            if (changed)
                ApplyPerks();
        }

        private void HomeTalents(string companion, CompanionProgress progress, double seconds)
        {
            if (progress.Talents.Count == 0)
                return;
            var lessons = Talent(companion, TalentEffect.HomeExperience);
            if (lessons > 0)
                Gain(companion, lessons * seconds / 86400);
            var forage = Talent(companion, TalentEffect.HomeForage);
            if (forage <= 0)
                return;
            progress.ForageSeconds += seconds;
            while (progress.ForageSeconds >= ExpeditionSystem.SecondsPerHour)
            {
                progress.ForageSeconds -= ExpeditionSystem.SecondsPerHour;
                var item = ForageItem(companion, progress.Forages++);
                if (item.Length > 0)
                    _camp.Add(item, (long)forage);
            }
        }

        private string ForageItem(string companion, long count)
        {
            var def = _rules.Companion(companion);
            if (def == null)
                return string.Empty;
            if (count % 2 == 1)
                return _rules.FishOf(def.Biome);
            var place = _expeditions.Rules.Biome(def.Biome);
            return place == null || place.Items.Count == 0 ? string.Empty : place.Items[0].Item;
        }

        private bool AddTrip(string companion, CompanionProgress progress, string trait, bool home)
        {
            var ranks = _rules.Ranks(trait);
            var travels = ranks == null ? _rules.Own(trait) != null : !ranks.Home;
            if ((ranks == null && !travels) || travels == home)
                return false;
            progress.TraitTrips[trait] = (progress.TraitTrips.TryGetValue(trait, out var trips) ? trips : 0) + System.Math.Max(0.1, 1 + Temper(companion, TemperStat.TraitRanks));
            return true;
        }

        private void HomeOwnPerks(string companion, double multiplier)
        {
            var trait = BaseTrait(companion);
            var own = trait.Length == 0 ? null : _rules.Own(trait);
            if (own == null)
                return;
            var perks = _state.CompanionPerks;
            for (var i = 0; i < own.Effects.Count; i++)
            {
                var effect = own.Effects[i];
                if (effect.Kind != OwnEffectKind.HomePerk)
                    continue;
                var perk = (int)effect.Perk;
                perks[perk] = System.Math.Min(MuseumRules.MaxOf(effect.Perk), perks[perk] + effect.At(RankOf(companion, trait)) * TraitFactor(companion) * multiplier);
            }
        }

        private void BranchPerks(string companion, double multiplier)
        {
            var branch = _rules.Companion(companion)?.Branch;
            if (branch == null)
                return;
            var perks = _state.CompanionPerks;
            var stage = StageOf(companion);
            for (var i = 0; i < branch.Count; i++)
            {
                var effect = branch[i].Effect;
                if (branch[i].Stage > stage || effect.Kind != OwnEffectKind.HomePerk)
                    continue;
                var perk = (int)effect.Perk;
                perks[perk] = System.Math.Min(MuseumRules.MaxOf(effect.Perk), perks[perk] + effect.Values[0] * TraitFactor(companion) * multiplier);
            }
        }

        public void ApplyPerks()
        {
            Tree?.Recalculate();
            var perks = _state.CompanionPerks;
            for (var i = 0; i < perks.Length; i++)
                perks[i] = Tree == null ? 0 : System.Math.Min(MuseumRules.MaxOf((MuseumPerk)i), Tree.Perk((MuseumPerk)i));
            var resonance = ResonanceBonus();
            var resonancePerks = _rules.Resonance.Perks;
            for (var i = 0; i < resonancePerks.Count; i++)
                perks[(int)resonancePerks[i]] = System.Math.Min(MuseumRules.MaxOf(resonancePerks[i]), perks[(int)resonancePerks[i]] + resonance);
            for (var i = 0; i < State.Home.Count; i++)
            {
                var companion = State.Home[i];
                var multiplier = Stage(companion).HomeMultiplier * (1 + Talent(companion, TalentEffect.HomeTraits)) * StarFactor(companion)
                    * (1 + (FormOf(companion)?.Home ?? 0) + GearStat(companion, CompanionGearStat.HomeTraits) + TreeStat(MenagerieStat.HomeTraits) + Temper(companion, TemperStat.HomeTraits));
                HomeOwnPerks(companion, multiplier);
                BranchPerks(companion, multiplier);
                if (!State.Progress.TryGetValue(companion, out var progress))
                    continue;
                for (var t = 0; t < progress.Traits.Count; t++)
                {
                    var ranks = _rules.Ranks(progress.Traits[t]);
                    if (ranks == null || !ranks.Home)
                        continue;
                    var perk = (int)ranks.Perk;
                    perks[perk] = System.Math.Min(MuseumRules.MaxOf(ranks.Perk), perks[perk] + ranks.At(RankOf(companion, progress.Traits[t])) * multiplier);
                }
            }
        }

        public void Gain(string companion, double experience)
        {
            if (experience <= 0)
                return;
            var progress = Progress(companion);
            var top = MaxLevelOf(companion);
            if (progress.Level >= top)
                return;
            var gained = experience * (1 + AllExperienceBonus() + MasteryExperience(companion) + GearStat(companion, CompanionGearStat.Experience) + TreeStat(MenagerieStat.AllExperience));
            progress.Experience += gained;
            Tree?.AddExperience(gained);
            var levelBefore = progress.Level;
            while (progress.Level < top && progress.Experience >= _rules.ExperienceFor(progress.Level + 1))
            {
                progress.Experience -= _rules.ExperienceFor(progress.Level + 1);
                progress.Level++;
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionLevelUp, _state.Tick, progress.Level));
            }
            if (progress.Level >= top)
                progress.Experience = 0;
            if (progress.Level != levelBefore)
                ApplyPerks();
        }
    }
}
