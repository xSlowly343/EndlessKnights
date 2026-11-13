using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CompanionFood
    {
        public CompanionFood(string id, string prefix, double experience)
        {
            Id = id;
            Prefix = prefix;
            Experience = experience;
        }

        public string Id { get; }
        public string Prefix { get; }
        public double Experience { get; }
    }

    public sealed class CompanionGesture
    {
        public CompanionGesture(string id, double seconds)
        {
            Id = id;
            Seconds = seconds;
        }

        public string Id { get; }
        public double Seconds { get; }
    }

    public sealed class CompanionDef
    {
        public CompanionDef(string id, string biome, string favourite)
        {
            Id = id;
            Biome = biome;
            Favourite = favourite;
        }

        public string Id { get; }
        public string Biome { get; }
        public string Favourite { get; }
        public string NameRu { get; internal set; } = string.Empty;
        public string NameEn { get; internal set; } = string.Empty;
        public string Rarity { get; internal set; } = string.Empty;
        public CompanionUnlock? Unlock { get; internal set; }
        public string Source { get; internal set; } = string.Empty;
        public long ShopPrice { get; internal set; }
        public IReadOnlyList<CompanionBranchNode> Branch { get; internal set; } = Array.Empty<CompanionBranchNode>();
        public string Trait { get; internal set; } = string.Empty;
        public string BaseTrait { get; internal set; } = string.Empty;
        public IReadOnlyList<string> StagesRu { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> StagesEn { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<CompanionGesture> Gestures { get; internal set; } = Array.Empty<CompanionGesture>();
    }

    public enum OwnEffectKind
    {
        Items,
        Shards,
        Rare,
        Loot,
        RareNight,
        Choice,
        LetterEarlier,
        ExtraItems,
        Herbs,
        BiomeShorter,
        Fish,
        Experience,
        Bone,
        HomePerk,
        AllExperience,
        DailyItem,
        Longer,
        FirstTrip,
        Hatch,
        Bond,
        TraitPrice,
        Souls,
        AnyBiome,
        GearPrice,
        Rewind
    }

    public sealed class CompanionBranchNode
    {
        public CompanionBranchNode(int stage, string nameRu, string nameEn, OwnEffect effect)
        {
            Stage = stage;
            NameRu = nameRu;
            NameEn = nameEn;
            Effect = effect;
        }

        public int Stage { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public OwnEffect Effect { get; }
    }

    public sealed class OwnEffect
    {
        public OwnEffect(OwnEffectKind kind, string biome, IReadOnlyList<double> values, MuseumPerk perk = default, string item = "")
        {
            Kind = kind;
            Biome = biome;
            Values = values;
            Perk = perk;
            Item = item;
        }

        public OwnEffectKind Kind { get; }
        public string Biome { get; }
        public MuseumPerk Perk { get; }
        public string Item { get; }
        public IReadOnlyList<double> Values { get; }

        public double At(int rank) => Values[System.Math.Max(0, System.Math.Min(rank, Values.Count) - 1)];

        public static bool Multiplies(OwnEffectKind kind) =>
            kind == OwnEffectKind.Items || kind == OwnEffectKind.Shards || kind == OwnEffectKind.Rare || kind == OwnEffectKind.Loot
            || kind == OwnEffectKind.RareNight || kind == OwnEffectKind.Herbs || kind == OwnEffectKind.Experience;
    }

    public sealed class OwnTrait
    {
        public OwnTrait(string id, string nameRu, string nameEn, IReadOnlyList<OwnEffect> effects)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Effects = effects;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<OwnEffect> Effects { get; }
    }

    public sealed class CompanionStage
    {
        public CompanionStage(string id, int level, int bones, long shards, int blooms, bool trophy, int favouriteItems, int favouriteDishes, int traitSlots, int rankCap, double homeMultiplier, IReadOnlyList<int> hours)
        {
            Id = id;
            Level = level;
            Bones = bones;
            Shards = shards;
            Blooms = blooms;
            Trophy = trophy;
            FavouriteItems = favouriteItems;
            FavouriteDishes = favouriteDishes;
            TraitSlots = traitSlots;
            RankCap = rankCap;
            HomeMultiplier = homeMultiplier;
            Hours = hours;
        }

        public string Id { get; }
        public int Level { get; }
        public int Bones { get; }
        public long Shards { get; }
        public int Blooms { get; }
        public bool Trophy { get; }
        public int FavouriteItems { get; }
        public int FavouriteDishes { get; }
        public int TraitSlots { get; }
        public int RankCap { get; }
        public double HomeMultiplier { get; }
        public IReadOnlyList<int> Hours { get; }
    }

    public sealed class CompanionBiome
    {
        public CompanionBiome(string biome, string bone, string collection, string fish)
        {
            Biome = biome;
            Bone = bone;
            Collection = collection;
            Fish = fish;
        }

        public string Fish { get; }

        public string Biome { get; }
        public string Bone { get; }
        public string Collection { get; }
    }

    public enum TravelStat
    {
        Items,
        Shards,
        Rare
    }

    public sealed class CompanionTraitRanks
    {
        public CompanionTraitRanks(string trait, string nameRu, string nameEn, bool home, TravelStat stat, MuseumPerk perk, IReadOnlyList<double> values)
        {
            Trait = trait;
            NameRu = nameRu;
            NameEn = nameEn;
            Home = home;
            Stat = stat;
            Perk = perk;
            Values = values;
        }

        public string Trait { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public bool Home { get; }
        public TravelStat Stat { get; }
        public MuseumPerk Perk { get; }
        public IReadOnlyList<double> Values { get; }

        public double At(int rank) => Values[System.Math.Max(0, System.Math.Min(rank, Values.Count) - 1)];
    }

    public enum TalentEffect
    {
        Shorter,
        ExtraItems,
        Rare,
        LongTrip,
        HomeTraits,
        HomeExperience,
        FavouriteBond,
        HomeForage,
        JointLoot,
        PartyTripShorter,
        PartyFriendship,
        PackTrait
    }

    public sealed class CompanionTalentNode
    {
        public CompanionTalentNode(string id, string branch, int index, string nameRu, string nameEn, TalentEffect effect, double value, int stage)
        {
            Id = id;
            Branch = branch;
            Index = index;
            NameRu = nameRu;
            NameEn = nameEn;
            Effect = effect;
            Value = value;
            Stage = stage;
        }

        public string Id { get; }
        public string Branch { get; }
        public int Index { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public TalentEffect Effect { get; }
        public double Value { get; }
        public int Stage { get; }
    }

    public sealed class CompanionTalents
    {
        private static readonly Dictionary<string, TalentEffect> EffectNames = new Dictionary<string, TalentEffect>(StringComparer.Ordinal)
        {
            ["shorter"] = TalentEffect.Shorter,
            ["extraItems"] = TalentEffect.ExtraItems,
            ["rare"] = TalentEffect.Rare,
            ["longTrip"] = TalentEffect.LongTrip,
            ["homeTraits"] = TalentEffect.HomeTraits,
            ["homeExperience"] = TalentEffect.HomeExperience,
            ["favouriteBond"] = TalentEffect.FavouriteBond,
            ["homeForage"] = TalentEffect.HomeForage,
            ["jointLoot"] = TalentEffect.JointLoot,
            ["partyTripShorter"] = TalentEffect.PartyTripShorter,
            ["partyFriendship"] = TalentEffect.PartyFriendship,
            ["packTrait"] = TalentEffect.PackTrait
        };

        public int EveryLevels { get; private set; }
        public int FreeResetDays { get; private set; }
        public long ResetShards { get; private set; }
        public IReadOnlyList<CompanionTalentNode> Nodes { get; private set; } = Array.Empty<CompanionTalentNode>();

        public CompanionTalentNode? Node(string id)
        {
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id)
                    return Nodes[i];
            }
            return null;
        }

        public CompanionTalentNode? Previous(CompanionTalentNode node)
        {
            if (node.Index == 0)
                return null;
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Branch == node.Branch && Nodes[i].Index == node.Index - 1)
                    return Nodes[i];
            }
            return null;
        }

        internal static CompanionTalents Load(ContentNode node)
        {
            var nodes = new List<CompanionTalentNode>();
            foreach (var branch in node.Field("branches").Items())
            {
                var branchId = branch.Field("id").AsString();
                var index = 0;
                foreach (var item in branch.Field("nodes").Items())
                {
                    var effectName = item.Field("effect").AsString();
                    if (!EffectNames.TryGetValue(effectName, out var effect))
                        throw item.Field("effect").Error($"unknown talent effect '{effectName}'");
                    var name = item.Field("name");
                    nodes.Add(new CompanionTalentNode(item.Field("id").AsString(), branchId, index++, name.Field("ru").AsString(), name.Field("en").AsString(),
                        effect, item.Field("value").AsNumber(0, 1000), item.HasField("stage") ? item.Field("stage").AsInt(0, 10) : 0));
                }
            }
            return new CompanionTalents
            {
                EveryLevels = node.Field("everyLevels").AsInt(1, 1000),
                FreeResetDays = node.Field("freeResetDays").AsInt(0, 365),
                ResetShards = node.Field("resetShards").AsLong(0),
                Nodes = nodes
            };
        }
    }

    public sealed class CompanionBond
    {
        public int MaxHearts { get; internal set; }
        public double PointsPerHeart { get; internal set; }
        public double PetPoints { get; internal set; }
        public long PetEveryTicks { get; internal set; }
        public int PetsPerDay { get; internal set; }
        public double FavouritePoints { get; internal set; }
        public int FavouritePerDay { get; internal set; }
        public double TripPoints { get; internal set; }
        public double FriendPetPoints { get; internal set; }
        public int FriendPetsPerDay { get; internal set; }
        public int RenameHearts { get; internal set; }
        public int GestureHearts { get; internal set; }
        public int SecondGestureHearts { get; internal set; }
        public int FireSeatHearts { get; internal set; }
        public int AccessoryHearts { get; internal set; }
        public int BestFriendHearts { get; internal set; }
        public IReadOnlyList<int> ChapterHearts { get; internal set; } = Array.Empty<int>();
        public IReadOnlyList<string> Accessories { get; internal set; } = Array.Empty<string>();
        public int NameLength { get; internal set; }
        public IReadOnlyList<IReadOnlyList<(string Ru, string En)>> Chapters { get; internal set; } = Array.Empty<IReadOnlyList<(string, string)>>();

        public double PointsTo(int hearts)
        {
            var total = 0.0;
            for (var heart = 1; heart <= System.Math.Min(hearts, MaxHearts); heart++)
                total += PointsPerHeart * heart;
            return total;
        }

        internal static CompanionBond Load(ContentNode node)
        {
            var pet = node.Field("pet");
            var favourite = node.Field("favourite");
            var friend = node.Field("friendPet");
            var unlocks = node.Field("unlocks");
            var chapterHearts = new List<int>();
            foreach (var item in node.Field("chapterHearts").Items())
                chapterHearts.Add(item.AsInt(0, 100));
            var accessories = new List<string>();
            foreach (var item in node.Field("accessories").Items())
                accessories.Add(item.AsString());
            var chapters = new List<IReadOnlyList<(string, string)>>();
            foreach (var chapter in node.Field("chapters").Items())
            {
                var letters = new List<(string, string)>();
                foreach (var letter in chapter.Items())
                    letters.Add((letter.Field("ru").AsString(), letter.Field("en").AsString()));
                chapters.Add(letters);
            }
            if (chapters.Count != chapterHearts.Count)
                throw node.Field("chapters").Error($"expected {chapterHearts.Count} chapters, one per chapter heart");
            return new CompanionBond
            {
                MaxHearts = node.Field("maxHearts").AsInt(1, 100),
                PointsPerHeart = node.Field("pointsPerHeart").AsNumber(1, 1e6),
                PetPoints = pet.Field("points").AsNumber(0, 1000),
                PetEveryTicks = (long)(pet.Field("everyMinutes").AsNumber(0, 1440) * 60 * 10),
                PetsPerDay = pet.Field("perDay").AsInt(0, 1000),
                FavouritePoints = favourite.Field("points").AsNumber(0, 1000),
                FavouritePerDay = favourite.Field("perDay").AsInt(0, 100),
                TripPoints = node.Field("trip").Field("points").AsNumber(0, 1000),
                FriendPetPoints = friend.Field("points").AsNumber(0, 1000),
                FriendPetsPerDay = friend.Field("perDay").AsInt(0, 100),
                RenameHearts = unlocks.Field("rename").AsInt(0, 100),
                GestureHearts = unlocks.Field("gesture").AsInt(0, 100),
                SecondGestureHearts = unlocks.Field("secondGesture").AsInt(0, 100),
                FireSeatHearts = unlocks.Field("fireSeat").AsInt(0, 100),
                AccessoryHearts = unlocks.Field("accessory").AsInt(0, 100),
                BestFriendHearts = unlocks.Field("bestFriend").AsInt(0, 100),
                ChapterHearts = chapterHearts,
                Accessories = accessories,
                NameLength = node.Field("nameLength").AsInt(1, 64),
                Chapters = chapters
            };
        }
    }

    public sealed class CompanionPair
    {
        public CompanionPair(string a, string b, string sceneRu, string sceneEn)
        {
            A = a;
            B = b;
            SceneRu = sceneRu;
            SceneEn = sceneEn;
        }

        public string A { get; }
        public string B { get; }
        public string SceneRu { get; }
        public string SceneEn { get; }
    }

    public sealed class CompanionRules
    {
        public const string FileName = "companions.json";
        public const int SupportedVersion = 1;

        public int MaxLevel { get; private set; }
        public double ExperiencePerLevel { get; private set; }
        public double ExperiencePerTripHour { get; private set; }
        public IReadOnlyList<CompanionFood> Foods { get; private set; } = Array.Empty<CompanionFood>();
        public double DishExperience { get; private set; }
        public double FavouriteMultiplier { get; private set; }
        public double FoodPerDay { get; private set; }
        public double LootPerLevel { get; private set; }
        public int ShorterEveryLevels { get; private set; }
        public double ShorterStep { get; private set; }
        public double ShorterCap { get; private set; }
        public IReadOnlyList<CompanionDef> Companions { get; private set; } = Array.Empty<CompanionDef>();
        public IReadOnlyList<CompanionStage> Stages { get; private set; } = Array.Empty<CompanionStage>();
        public IReadOnlyList<CompanionBiome> Biomes { get; private set; } = Array.Empty<CompanionBiome>();
        public string Bloom { get; private set; } = string.Empty;
        public double OwnBiomeLoot { get; private set; }
        public IReadOnlyList<int> RankTrips { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<CompanionTraitRanks> TraitRanks { get; private set; } = Array.Empty<CompanionTraitRanks>();
        public int HomeSlots { get; private set; }
        public double HomeHoursPerTrip { get; private set; }
        public long TraitChangeShards { get; private set; }

        public IReadOnlyList<OwnTrait> OwnTraits { get; private set; } = Array.Empty<OwnTrait>();
        public IReadOnlyList<string> Rarities { get; private set; } = Array.Empty<string>();
        public CompanionBond Bond { get; private set; } = new CompanionBond();
        public CompanionTalents Talents { get; private set; } = new CompanionTalents();
        public NestRules Nest { get; private set; } = new NestRules();
        public CompanionAlbumRules Album { get; private set; } = new CompanionAlbumRules();
        public CompanionStripRules Strip { get; private set; } = new CompanionStripRules();
        public CompanionStarRules Stars { get; private set; } = new CompanionStarRules();
        public CompanionFormRules Forms { get; private set; } = new CompanionFormRules();
        public CompanionResonanceRules Resonance { get; private set; } = new CompanionResonanceRules();
        public CompanionGearRules Gear { get; private set; } = new CompanionGearRules();
        public CompanionWorkRules Work { get; private set; } = new CompanionWorkRules();
        public double VisitHours { get; private set; }
        public double VisitExperience { get; private set; }
        public double VisitBond { get; private set; }
        public CompanionTreatRules Treats { get; private set; } = new CompanionTreatRules();
        public MenagerieTreeRules Menagerie { get; private set; } = new MenagerieTreeRules();
        public CompanionFamilyRules Families { get; private set; } = new CompanionFamilyRules();
        public CompanionTemperRules Tempers { get; private set; } = new CompanionTemperRules();
        public CompanionPackRules Pack { get; private set; } = new CompanionPackRules();
        public CompanionCircleRules Circles { get; private set; } = new CompanionCircleRules();
        public CompanionPathRules Paths { get; private set; } = new CompanionPathRules();
        public CompanionAbilityRules Abilities { get; private set; } = new CompanionAbilityRules();
        public CompanionColossusRules Colossus { get; private set; } = new CompanionColossusRules();
        public int TopLevel => MaxLevel + Stars.LevelsPerStar * Stars.MaxStars;
        public IReadOnlyDictionary<string, double> RarityTraitMultiplier { get; private set; } = new Dictionary<string, double>();
        public IReadOnlyList<CompanionCoat> Coats { get; private set; } = Array.Empty<CompanionCoat>();

        public double RarityFactor(string rarity) => RarityTraitMultiplier.TryGetValue(rarity, out var factor) ? factor : 1;

        public CompanionCoat? Coat(string id)
        {
            for (var i = 0; i < Coats.Count; i++)
            {
                if (Coats[i].Id == id)
                    return Coats[i];
            }
            return null;
        }
        public double JointLoot { get; private set; }
        public double FriendsLoot { get; private set; }
        public IReadOnlyList<CompanionPair> Pairs { get; private set; } = Array.Empty<CompanionPair>();

        public int MaxRank => RankTrips.Count + 1;

        public OwnTrait? Own(string trait)
        {
            for (var i = 0; i < OwnTraits.Count; i++)
            {
                if (OwnTraits[i].Id == trait)
                    return OwnTraits[i];
            }
            return null;
        }

        public string FishOf(string biome)
        {
            for (var i = 0; i < Biomes.Count; i++)
            {
                if (Biomes[i].Biome == biome)
                    return Biomes[i].Fish;
            }
            return string.Empty;
        }

        public CompanionTraitRanks? Ranks(string trait)
        {
            for (var i = 0; i < TraitRanks.Count; i++)
            {
                if (TraitRanks[i].Trait == trait)
                    return TraitRanks[i];
            }
            return null;
        }

        public CompanionStage StageAt(int stage) => Stages[System.Math.Max(0, System.Math.Min(stage, Stages.Count - 1))];

        public CompanionBiome? BiomeOf(string biome)
        {
            for (var i = 0; i < Biomes.Count; i++)
            {
                if (Biomes[i].Biome == biome)
                    return Biomes[i];
            }
            return null;
        }

        public double ExperienceFor(int level) => level <= 1 ? 0 : ExperiencePerLevel * level;

        public double ExperienceTo(int level)
        {
            var total = 0.0;
            for (var next = 2; next <= System.Math.Min(level, MaxLevel); next++)
                total += ExperienceFor(next);
            return total;
        }

        private static IReadOnlyList<CompanionGesture> LoadGestures(ContentNode node)
        {
            var gestures = new List<CompanionGesture>();
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (gestures.Exists(g => g.Id == id))
                    throw item.Field("id").Error($"gesture '{id}' is listed twice");
                gestures.Add(new CompanionGesture(id, item.Field("seconds").AsNumber(0.2, 30)));
            }
            return gestures;
        }

        public CompanionDef? Companion(string id)
        {
            for (var i = 0; i < Companions.Count; i++)
            {
                if (Companions[i].Id == id)
                    return Companions[i];
            }
            return null;
        }

        public CompanionFood? FoodOf(string item)
        {
            for (var i = 0; i < Foods.Count; i++)
            {
                if (item.StartsWith(Foods[i].Prefix, StringComparison.Ordinal))
                    return Foods[i];
            }
            return null;
        }

        public CompanionFood? Food(string id)
        {
            for (var i = 0; i < Foods.Count; i++)
            {
                if (Foods[i].Id == id)
                    return Foods[i];
            }
            return null;
        }

        public static CompanionRules Load(string json, IReadOnlyList<string> knownBiomes, CookingRules cooking, string bloom, ExpeditionRules expeditions)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var foods = new List<CompanionFood>();
            foreach (var node in root.Field("foods").Items())
                foods.Add(new CompanionFood(node.Field("id").AsString(), node.Field("prefix").AsString(), node.Field("experience").AsNumber(0, 10_000)));
            var rules = new CompanionRules { Foods = foods };
            var level = root.Field("level");
            var shorter = level.Field("shorter");
            var stages = new List<CompanionStage>();
            foreach (var node in root.Field("stages").Items())
            {
                var hours = new List<int>();
                if (node.HasField("hours"))
                    foreach (var item in node.Field("hours").Items())
                        hours.Add(item.AsInt(1, 48));
                var level2 = node.Field("level").AsInt(1, 1000);
                if (stages.Count > 0 && level2 <= stages[stages.Count - 1].Level)
                    throw node.Field("level").Error("stage levels must grow");
                stages.Add(new CompanionStage(node.Field("id").AsString(), level2, Int(node, "bones"), Int(node, "shards"), Int(node, "blooms"),
                    node.HasField("trophy") && node.Field("trophy").AsBool(), Int(node, "favouriteItems"), Int(node, "favouriteDishes"),
                    node.Field("traitSlots").AsInt(1, 10), node.Field("rankCap").AsInt(1, 10), node.Field("homeMultiplier").AsNumber(0, 100), hours));
            }
            if (stages.Count == 0)
                throw root.Field("stages").Error("at least one stage is needed");
            var biomes = new List<CompanionBiome>();
            foreach (var node in root.Field("biomes").Items())
            {
                var biome = node.Field("biome").AsString();
                if (!Contains(knownBiomes, biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                biomes.Add(new CompanionBiome(biome, node.Field("bone").AsString(), node.Field("collection").AsString(), node.Field("fish").AsString()));
            }
            var traitsNode = root.Field("traits");
            var rankTrips = new List<int>();
            foreach (var item in traitsNode.Field("rankTrips").Items())
            {
                var trips = item.AsInt(1, 1_000_000);
                if (rankTrips.Count > 0 && trips <= rankTrips[rankTrips.Count - 1])
                    throw item.Error("rank trips must grow");
                rankTrips.Add(trips);
            }
            var ranks = new List<CompanionTraitRanks>();
            foreach (var node in traitsNode.Field("travel").Items())
            {
                var trait = node.Field("trait").AsString();
                var known = expeditions.Trait(trait);
                if (known == null)
                    throw node.Field("trait").Error($"unknown trait '{trait}'");
                var stat = node.Field("stat").AsString() switch
                {
                    "items" => TravelStat.Items,
                    "shards" => TravelStat.Shards,
                    "rare" => TravelStat.Rare,
                    var other => throw node.Field("stat").Error($"unknown stat '{other}'")
                };
                ranks.Add(new CompanionTraitRanks(trait, known.NameRu, known.NameEn, false, stat, default, Values(node, rankTrips.Count + 1, 1, 100)));
            }
            foreach (var node in traitsNode.Field("home").Items())
            {
                var perkName = node.Field("perk").AsString();
                if (!MuseumRules.TryPerk(perkName, out var perk))
                    throw node.Field("perk").Error($"unknown perk '{perkName}'");
                var name = node.Field("name");
                ranks.Add(new CompanionTraitRanks(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), true, default, perk, Values(node, rankTrips.Count + 1, 0, MuseumRules.MaxOf(perk))));
            }
            var own = new List<OwnTrait>();
            foreach (var node in traitsNode.Field("own").Items())
            {
                var effects = new List<OwnEffect>();
                foreach (var effect in node.Field("effects").Items())
                    effects.Add(LoadEffect(effect, knownBiomes, Values(effect, rankTrips.Count + 1, 0, 100)));
                var name = node.Field("name");
                own.Add(new OwnTrait(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), effects));
            }
            var rarities = new List<string>();
            foreach (var item in root.Field("rarities").Items())
                rarities.Add(item.AsString());
            var companions = new List<CompanionDef>();
            foreach (var node in root.Field("companions").Items())
            {
                var biome = node.Field("biome").AsString();
                if (!Contains(knownBiomes, biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                var favourite = node.Field("favourite").AsString();
                if (rules.Food(favourite) == null && cooking.Dish(favourite) == null)
                    throw node.Field("favourite").Error($"unknown food '{favourite}'");
                var def = new CompanionDef(node.Field("id").AsString(), biome, favourite);
                var name = node.Field("name");
                def.NameRu = name.Field("ru").AsString();
                def.NameEn = name.Field("en").AsString();
                def.Rarity = node.Field("rarity").AsString();
                if (!rarities.Contains(def.Rarity))
                    throw node.Field("rarity").Error($"unknown rarity '{def.Rarity}'");
                def.Source = node.Field("source").AsString();
                def.ShopPrice = node.HasField("shopPrice") ? node.Field("shopPrice").AsLong(0, 1_000_000) : 0;
                if (node.HasField("unlock"))
                    def.Unlock = CompanionUnlock.Load(node.Field("unlock"));
                if (node.HasField("branch"))
                {
                    var branch = new List<CompanionBranchNode>();
                    foreach (var item in node.Field("branch").Items())
                    {
                        var label = item.Field("name");
                        branch.Add(new CompanionBranchNode(item.Field("stage").AsInt(1, 10), label.Field("ru").AsString(), label.Field("en").AsString(),
                            LoadEffect(item, knownBiomes, new[] { item.Field("value").AsNumber(-100, 1000) })));
                    }
                    def.Branch = branch;
                }
                if (node.HasField("trait"))
                {
                    def.Trait = node.Field("trait").AsString();
                    var found = false;
                    foreach (var trait in own)
                        found |= trait.Id == def.Trait;
                    if (!found)
                        throw node.Field("trait").Error($"unknown own trait '{def.Trait}'");
                }
                if (node.HasField("baseTrait"))
                {
                    def.BaseTrait = node.Field("baseTrait").AsString();
                    if (expeditions.Trait(def.BaseTrait) == null)
                        throw node.Field("baseTrait").Error($"unknown trait '{def.BaseTrait}'");
                }
                if (node.HasField("gestures"))
                    def.Gestures = LoadGestures(node.Field("gestures"));
                var stageNames = node.Field("stages");
                def.StagesRu = Names(stageNames.Field("ru"), stages.Count);
                def.StagesEn = Names(stageNames.Field("en"), stages.Count);
                companions.Add(def);
            }
            var joint = root.Field("joint");
            var pairs = new List<CompanionPair>();
            foreach (var node in joint.Field("pairs").Items())
            {
                var a = node.Field("a").AsString();
                var b = node.Field("b").AsString();
                if (!companions.Exists(c => c.Id == a))
                    throw node.Field("a").Error($"unknown companion '{a}'");
                if (!companions.Exists(c => c.Id == b))
                    throw node.Field("b").Error($"unknown companion '{b}'");
                var scene = node.Field("scene");
                pairs.Add(new CompanionPair(a, b, scene.Field("ru").AsString(), scene.Field("en").AsString()));
            }
            foreach (var stage in stages)
            {
                if (stage.RankCap > rankTrips.Count + 1)
                    throw root.Field("stages").Error($"stage '{stage.Id}' caps rank at {stage.RankCap}, only {rankTrips.Count + 1} ranks exist");
            }
            var bondRules = CompanionBond.Load(root.Field("bond"));
            var families = CompanionFamilyRules.Load(root.Field("families"));
            foreach (var family in families.Families)
            {
                foreach (var member in family.Members)
                {
                    if (!companions.Exists(def => def.Id == member))
                        throw root.Field("families").Error($"family '{family.Id}' names unknown companion '{member}'");
                }
            }
            var abilities = CompanionAbilityRules.Load(root.Field("abilities"));
            foreach (var ability in abilities.Abilities)
            {
                var known = false;
                foreach (var family in families.Families)
                    known |= family.Id == ability.Family;
                if (!known)
                    throw root.Field("abilities").Error($"ability '{ability.NameEn}' names unknown family '{ability.Family}'");
            }
            return new CompanionRules
            {
                Families = families,
                Abilities = abilities,
                Colossus = CompanionColossusRules.Load(root.Field("colossus")),
                Tempers = CompanionTemperRules.Load(root.Field("tempers")),
                Pack = CompanionPackRules.Load(root.Field("pack"), root.Field("mentor")),
                Circles = CompanionCircleRules.Load(root.Field("circles")),
                Paths = CompanionPathRules.Load(root.Field("paths")),
                Bond = bondRules,
                Album = CompanionAlbumRules.Load(root.Field("album"), bondRules.MaxHearts),
                Strip = CompanionStripRules.Load(root.Field("strip")),
                Stars = CompanionStarRules.Load(root.Field("stars"), rarities),
                Treats = CompanionTreatRules.Load(root.Field("treats")),
                Menagerie = MenagerieTreeRules.Load(root.Field("menagerie")),
                Work = CompanionWorkRules.Load(root.Field("work"), knownBiomes),
                VisitHours = root.Field("coop").Field("visit").Field("hours").AsNumber(0.1, 72),
                VisitExperience = root.Field("coop").Field("visit").Field("experience").AsNumber(0, 100000),
                VisitBond = root.Field("coop").Field("visit").Field("bond").AsNumber(0, 1000),
                Gear = CompanionGearRules.Load(root.Field("gear"), stages.Count, root.Field("stars").Field("costs").Items().Count),
                Resonance = CompanionResonanceRules.Load(root.Field("resonance"), root.Field("mastery")),
                Forms = CompanionFormRules.Load(root.Field("forms"), stages.Count, root.Field("stars").Field("costs").Items().Count),
                RarityTraitMultiplier = RarityFactors(root.Field("rarityTraitMultiplier"), rarities),
                Coats = CompanionCoat.LoadAll(root.Field("coats")),
                Talents = CompanionTalents.Load(root.Field("talents")),
                Nest = LoadNest(root.Field("nest"), companions),
                JointLoot = joint.Field("loot").AsNumber(0, 10),
                FriendsLoot = joint.Field("friendsLoot").AsNumber(0, 10),
                Pairs = pairs,
                RankTrips = rankTrips,
                OwnTraits = own,
                Rarities = rarities,
                TraitRanks = ranks,
                HomeSlots = traitsNode.Field("homeSlots").AsInt(0, 10),
                HomeHoursPerTrip = traitsNode.Field("homeHoursPerTrip").AsNumber(0.1, 1000),
                TraitChangeShards = traitsNode.Field("changeShards").AsLong(0),
                Stages = stages,
                Biomes = biomes,
                Bloom = bloom,
                OwnBiomeLoot = root.Field("ownBiomeLoot").AsNumber(0, 10),
                MaxLevel = level.Field("max").AsInt(1, 1000),
                ExperiencePerLevel = level.Field("experiencePerLevel").AsNumber(1, 1e9),
                ExperiencePerTripHour = root.Field("experiencePerTripHour").AsNumber(0, 1e6),
                Foods = foods,
                DishExperience = root.Field("dishExperience").AsNumber(0, 1e6),
                FavouriteMultiplier = root.Field("favouriteMultiplier").AsNumber(1, 100),
                FoodPerDay = root.Field("foodPerDay").AsNumber(0, 1e9),
                LootPerLevel = level.Field("lootPerLevel").AsNumber(0, 1),
                ShorterEveryLevels = shorter.Field("everyLevels").AsInt(1, 1000),
                ShorterStep = shorter.Field("step").AsNumber(0, 1),
                ShorterCap = shorter.Field("cap").AsNumber(0, 0.9),
                Companions = companions
            };
        }

        private static readonly Dictionary<string, OwnEffectKind> OwnKinds = new Dictionary<string, OwnEffectKind>(StringComparer.Ordinal)
        {
            ["items"] = OwnEffectKind.Items,
            ["shards"] = OwnEffectKind.Shards,
            ["rare"] = OwnEffectKind.Rare,
            ["loot"] = OwnEffectKind.Loot,
            ["rareNight"] = OwnEffectKind.RareNight,
            ["choice"] = OwnEffectKind.Choice,
            ["letterEarlier"] = OwnEffectKind.LetterEarlier,
            ["extraItems"] = OwnEffectKind.ExtraItems,
            ["herbs"] = OwnEffectKind.Herbs,
            ["biomeShorter"] = OwnEffectKind.BiomeShorter,
            ["fish"] = OwnEffectKind.Fish,
            ["experience"] = OwnEffectKind.Experience,
            ["bone"] = OwnEffectKind.Bone,
            ["homePerk"] = OwnEffectKind.HomePerk,
            ["allExperience"] = OwnEffectKind.AllExperience,
            ["dailyItem"] = OwnEffectKind.DailyItem,
            ["longer"] = OwnEffectKind.Longer,
            ["firstTrip"] = OwnEffectKind.FirstTrip,
            ["hatch"] = OwnEffectKind.Hatch,
            ["bond"] = OwnEffectKind.Bond,
            ["traitPrice"] = OwnEffectKind.TraitPrice,
            ["souls"] = OwnEffectKind.Souls,
            ["anyBiome"] = OwnEffectKind.AnyBiome,
            ["gearPrice"] = OwnEffectKind.GearPrice,
            ["rewind"] = OwnEffectKind.Rewind
        };

        private static Dictionary<string, double> RarityFactors(ContentNode node, List<string> rarities)
        {
            var factors = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var name in node.FieldNames())
            {
                if (!rarities.Contains(name))
                    throw node.Field(name).Error($"unknown rarity '{name}'");
                factors[name] = node.Field(name).AsNumber(0.1, 10);
            }
            return factors;
        }

        private static OwnEffect LoadEffect(ContentNode effect, IReadOnlyList<string> knownBiomes, IReadOnlyList<double> values)
        {
            var kindName = effect.Field("kind").AsString();
            if (!OwnKinds.TryGetValue(kindName, out var kind))
                throw effect.Field("kind").Error($"unknown effect '{kindName}'");
            var biome = effect.HasField("biome") ? effect.Field("biome").AsString() : string.Empty;
            if (biome.Length > 0 && !Contains(knownBiomes, biome))
                throw effect.Field("biome").Error($"unknown biome '{biome}'");
            MuseumPerk perk = default;
            if (kind == OwnEffectKind.HomePerk)
            {
                var perkName = effect.Field("perk").AsString();
                if (!MuseumRules.TryPerk(perkName, out perk))
                    throw effect.Field("perk").Error($"unknown perk '{perkName}'");
            }
            var item = kind == OwnEffectKind.DailyItem ? effect.Field("item").AsString() : string.Empty;
            return new OwnEffect(kind, biome, values, perk, item);
        }

        private static NestRules LoadNest(ContentNode node, List<CompanionDef> companions)
        {
            var nest = NestRules.Load(node);
            foreach (var trophy in nest.Trophies)
            {
                if (!companions.Exists(c => c.Id == trophy.Companion))
                    throw node.Field("trophies").Error($"unknown companion '{trophy.Companion}'");
            }
            if (!companions.Exists(c => c.Id == nest.HeartCompanion))
                throw node.Field("heart").Error($"unknown companion '{nest.HeartCompanion}'");
            return nest;
        }

        private static IReadOnlyList<string> Names(ContentNode node, int count)
        {
            var names = new List<string>();
            foreach (var item in node.Items())
                names.Add(item.AsString());
            if (names.Count != count)
                throw node.Error($"expected {count} stage names");
            return names;
        }

        private static IReadOnlyList<double> Values(ContentNode node, int count, double min, double max)
        {
            var values = new List<double>();
            foreach (var item in node.Field("values").Items())
                values.Add(item.AsNumber(min, max));
            if (values.Count != count)
                throw node.Field("values").Error($"expected {count} values, one per rank");
            return values;
        }

        private static int Int(ContentNode node, string name) => node.HasField(name) ? node.Field(name).AsInt(0, 1_000_000) : 0;

        private static bool Contains(IReadOnlyList<string> values, string value)
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
