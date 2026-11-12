using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum MenagerieStat
    {
        None,
        TripTime,
        TripItems,
        TripShards,
        TripRare,
        TripLoot,
        HomeTraits,
        FoodExperience,
        FoodCap,
        JointLoot,
        FriendsLoot,
        HatchTime,
        PetBond,
        Souls,
        AllExperience,
        StarCost,
        OwnTraits,
        OwnBiomeLoot,
        TreatExperience,
        BurrowLoot,
        HelpTime,
        HomeSlot,
        Perk,
        TripHerbs,
        TripFish,
        TalentPoints,
        TemperPower,
        TemperMinus,
        CoalWarm,
        EarlyHatch,
        WeeklySouls,
        ChoiceSparks,
        FriendWarm,
        FormCost,
        TraitCost,
        GearCost,
        PetsPerDay,
        LastHeartSouls,
        CircleCost,
        FreeStar,
        FavouriteFood,
        HeartsExperience,
        HeartsTraits,
        DevotedExperience,
        DevotedTraits,
        DevotedStarCost,
        MentorShare,
        ChoiceLetters,
        LetterBond,
        Sparks,
        RepeatSpark,
        TripSpark,
        PerCompanion,
        PackLoot,
        PackFamily,
        BurrowTries,
        Count
    }

    public enum MenagerieTheme
    {
        Trails,
        Hearth,
        Pack,
        Nest,
        Bonds,
        Underside,
        Peak
    }

    public sealed class MenagerieLine
    {
        public MenagerieLine(MenagerieStat stat, double value, string waits, MuseumPerk perk = default)
        {
            Stat = stat;
            Value = value;
            Waits = waits;
            Perk = perk;
        }

        public MuseumPerk Perk { get; }

        public MenagerieStat Stat { get; }
        public double Value { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0 && Stat != MenagerieStat.None;
    }

    public sealed class MenagerieNode
    {
        public MenagerieNode(int number, int row, int parent, MenagerieTheme theme, string nameRu, string nameEn, IReadOnlyList<MenagerieLine> a, IReadOnlyList<MenagerieLine> b, IReadOnlyList<MenagerieLine> c, bool keystone)
        {
            Number = number;
            Row = row;
            Parent = parent;
            Theme = theme;
            NameRu = nameRu;
            NameEn = nameEn;
            A = a;
            B = b;
            C = c;
            Keystone = keystone;
            Id = "menagerie_" + number;
        }

        public string Id { get; }
        public int Number { get; }
        public int Row { get; }
        public int Parent { get; }
        public MenagerieTheme Theme { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<MenagerieLine> A { get; }
        public IReadOnlyList<MenagerieLine> B { get; }
        public IReadOnlyList<MenagerieLine> C { get; }
        public bool Keystone { get; }

        public double AMultiplier(int rank) => rank <= 0 ? 0 : Keystone || rank == 1 ? 1 : rank < 4 ? 2 : 3;
    }

    public sealed class MenagerieMajor
    {
        public MenagerieMajor(string id, string nameRu, string nameEn, IReadOnlyList<MenagerieLine> lines)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Lines = lines;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<MenagerieLine> Lines { get; }
    }

    public sealed class MenagerieMajorSlot
    {
        public MenagerieMajorSlot(int at, IReadOnlyList<MenagerieMajor> options)
        {
            At = at;
            Options = options;
        }

        public int At { get; }
        public IReadOnlyList<MenagerieMajor> Options { get; }
    }

    public sealed class MenagerieTreeRules
    {
        public const int MaxRank = 5;

        public IReadOnlyList<MenagerieNode> Nodes { get; private set; } = Array.Empty<MenagerieNode>();
        public IReadOnlyList<int> RowThresholds { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<MenagerieMajorSlot> Majors { get; private set; } = Array.Empty<MenagerieMajorSlot>();
        public IReadOnlyList<MenagerieLine> BarPassives { get; private set; } = Array.Empty<MenagerieLine>();
        public int BarEvery { get; private set; } = 10;
        public int MaxPoints { get; private set; }
        public int MaxLevel { get; private set; }
        public double ExperienceShare { get; private set; }
        public double ExperiencePerLevel { get; private set; }
        public int EvolutionPoints { get; private set; }
        public int EvolutionsCounted { get; private set; }
        public int AlbumStars { get; private set; }
        public int AlbumPoints { get; private set; }
        public int RefundSouls { get; private set; }
        public int FreeResetDays { get; private set; } = 1;
        public int HeartsForExperience { get; private set; } = int.MaxValue;
        public int HeartsForTraits { get; private set; } = int.MaxValue;

        public double ExperienceFor(int level) => level <= 1 ? 0 : ExperiencePerLevel * level;

        public MenagerieNode? Node(int number) => number >= 1 && number <= Nodes.Count ? Nodes[number - 1] : null;

        public MenagerieNode? Node(string id)
        {
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id)
                    return Nodes[i];
            }
            return null;
        }

        public int Threshold(int row) => row >= 1 && row <= RowThresholds.Count ? RowThresholds[row - 1] : int.MaxValue;

        private static readonly Dictionary<string, MenagerieStat> Stats = new Dictionary<string, MenagerieStat>(StringComparer.Ordinal)
        {
            ["none"] = MenagerieStat.None,
            ["tripTime"] = MenagerieStat.TripTime,
            ["tripItems"] = MenagerieStat.TripItems,
            ["tripShards"] = MenagerieStat.TripShards,
            ["tripRare"] = MenagerieStat.TripRare,
            ["tripLoot"] = MenagerieStat.TripLoot,
            ["homeTraits"] = MenagerieStat.HomeTraits,
            ["foodExperience"] = MenagerieStat.FoodExperience,
            ["foodCap"] = MenagerieStat.FoodCap,
            ["jointLoot"] = MenagerieStat.JointLoot,
            ["friendsLoot"] = MenagerieStat.FriendsLoot,
            ["hatchTime"] = MenagerieStat.HatchTime,
            ["petBond"] = MenagerieStat.PetBond,
            ["souls"] = MenagerieStat.Souls,
            ["allExperience"] = MenagerieStat.AllExperience,
            ["starCost"] = MenagerieStat.StarCost,
            ["ownTraits"] = MenagerieStat.OwnTraits,
            ["ownBiomeLoot"] = MenagerieStat.OwnBiomeLoot,
            ["treatExperience"] = MenagerieStat.TreatExperience,
            ["burrowLoot"] = MenagerieStat.BurrowLoot,
            ["helpTime"] = MenagerieStat.HelpTime,
            ["homeSlot"] = MenagerieStat.HomeSlot,
            ["perk"] = MenagerieStat.Perk,
            ["tripHerbs"] = MenagerieStat.TripHerbs,
            ["tripFish"] = MenagerieStat.TripFish,
            ["talentPoints"] = MenagerieStat.TalentPoints,
            ["temperPower"] = MenagerieStat.TemperPower,
            ["temperMinus"] = MenagerieStat.TemperMinus,
            ["coalWarm"] = MenagerieStat.CoalWarm,
            ["earlyHatch"] = MenagerieStat.EarlyHatch,
            ["weeklySouls"] = MenagerieStat.WeeklySouls,
            ["choiceSparks"] = MenagerieStat.ChoiceSparks,
            ["friendWarm"] = MenagerieStat.FriendWarm,
            ["formCost"] = MenagerieStat.FormCost,
            ["traitCost"] = MenagerieStat.TraitCost,
            ["gearCost"] = MenagerieStat.GearCost,
            ["petsPerDay"] = MenagerieStat.PetsPerDay,
            ["lastHeartSouls"] = MenagerieStat.LastHeartSouls,
            ["circleCost"] = MenagerieStat.CircleCost,
            ["freeStar"] = MenagerieStat.FreeStar,
            ["favouriteFood"] = MenagerieStat.FavouriteFood,
            ["heartsExperience"] = MenagerieStat.HeartsExperience,
            ["heartsTraits"] = MenagerieStat.HeartsTraits,
            ["devotedExperience"] = MenagerieStat.DevotedExperience,
            ["devotedTraits"] = MenagerieStat.DevotedTraits,
            ["devotedStarCost"] = MenagerieStat.DevotedStarCost,
            ["mentorShare"] = MenagerieStat.MentorShare,
            ["choiceLetters"] = MenagerieStat.ChoiceLetters,
            ["letterBond"] = MenagerieStat.LetterBond,
            ["sparks"] = MenagerieStat.Sparks,
            ["repeatSpark"] = MenagerieStat.RepeatSpark,
            ["tripSpark"] = MenagerieStat.TripSpark,
            ["perCompanion"] = MenagerieStat.PerCompanion,
            ["packLoot"] = MenagerieStat.PackLoot,
            ["packFamily"] = MenagerieStat.PackFamily,
            ["burrowTries"] = MenagerieStat.BurrowTries
        };

        private static readonly Dictionary<string, MenagerieTheme> Themes = new Dictionary<string, MenagerieTheme>(StringComparer.Ordinal)
        {
            ["trails"] = MenagerieTheme.Trails,
            ["hearth"] = MenagerieTheme.Hearth,
            ["pack"] = MenagerieTheme.Pack,
            ["nest"] = MenagerieTheme.Nest,
            ["bonds"] = MenagerieTheme.Bonds,
            ["underside"] = MenagerieTheme.Underside,
            ["peak"] = MenagerieTheme.Peak
        };

        internal static List<MenagerieLine> Lines(ContentNode node)
        {
            var lines = new List<MenagerieLine>();
            foreach (var item in node.Items())
            {
                var name = item.Field("stat").AsString();
                if (!Stats.TryGetValue(name, out var stat))
                    throw item.Field("stat").Error($"unknown menagerie stat '{name}'");
                var waits = item.HasField("waits") ? item.Field("waits").AsString() : string.Empty;
                var perk = default(MuseumPerk);
                if (stat == MenagerieStat.Perk && !MuseumRules.TryPerk(item.Field("perk").AsString(), out perk))
                    throw item.Field("perk").Error($"unknown perk '{item.Field("perk").AsString()}'");
                lines.Add(new MenagerieLine(stat, item.HasField("value") ? item.Field("value").AsNumber(-1000, 1000) : 0, waits, perk));
            }
            return lines;
        }

        internal static MenagerieTreeRules Load(ContentNode node)
        {
            var nodes = new List<MenagerieNode>();
            foreach (var item in node.Field("nodes").Items())
            {
                var number = nodes.Count + 1;
                var row = item.Field("row").AsInt(1, 7);
                var parent = item.HasField("parent") ? item.Field("parent").AsInt(1, number - 1) : 0;
                if (parent > 0 && nodes[parent - 1].Row >= row)
                    throw item.Field("parent").Error("a parent must stand in a lower row");
                var theme = item.Field("theme").AsString();
                if (!Themes.TryGetValue(theme, out var kind))
                    throw item.Field("theme").Error($"unknown menagerie theme '{theme}'");
                var name = item.Field("name");
                nodes.Add(new MenagerieNode(number, row, parent, kind, name.Field("ru").AsString(), name.Field("en").AsString(),
                    Lines(item.Field("a")), Lines(item.Field("b")), Lines(item.Field("c")), item.HasField("keystone") && item.Field("keystone").AsBool()));
            }
            var thresholds = new List<int>();
            foreach (var item in node.Field("rows").Items())
                thresholds.Add(item.AsInt(0, 10000));
            var majors = new List<MenagerieMajorSlot>();
            foreach (var slot in node.Field("majors").Items())
            {
                var options = new List<MenagerieMajor>();
                foreach (var option in slot.Field("options").Items())
                {
                    var name = option.Field("name");
                    options.Add(new MenagerieMajor(option.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), Lines(option.Field("lines"))));
                }
                majors.Add(new MenagerieMajorSlot(slot.Field("at").AsInt(1, 10000), options));
            }
            var points = node.Field("points");
            var level = points.Field("level");
            var respec = node.Field("respec");
            return new MenagerieTreeRules
            {
                Nodes = nodes,
                RowThresholds = thresholds,
                Majors = majors,
                BarEvery = node.Field("bar").Field("every").AsInt(1, 1000),
                BarPassives = Lines(node.Field("bar").Field("passives")),
                MaxPoints = points.Field("max").AsInt(1, 10000),
                MaxLevel = level.Field("max").AsInt(1, 10000),
                ExperienceShare = level.Field("share").AsNumber(0, 1),
                ExperiencePerLevel = level.Field("experiencePerLevel").AsNumber(1, 1e9),
                EvolutionPoints = points.Field("evolution").AsInt(0, 100),
                EvolutionsCounted = points.Field("evolutionsCounted").AsInt(0, 10000),
                AlbumStars = points.Field("albumStars").AsInt(0, 100000),
                AlbumPoints = points.Field("album").AsInt(0, 100),
                RefundSouls = respec.Field("refundSouls").AsInt(0, 100000),
                FreeResetDays = respec.Field("freeResetDays").AsInt(1, 365),
                HeartsForExperience = node.Field("hearts").Field("experience").AsInt(1, 100),
                HeartsForTraits = node.Field("hearts").Field("traits").AsInt(1, 100)
            };
        }
    }
}
