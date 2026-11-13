using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyMascotNamed
    {
        internal PartyMascotNamed(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }

        internal static PartyMascotNamed Read(ContentNode node)
        {
            var name = node.Field("name");
            return new PartyMascotNamed(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString());
        }
    }

    public sealed class PartyMascotAuraLine
    {
        internal PartyMascotAuraLine(Modifier modifier, string waits)
        {
            Modifier = modifier;
            Waits = waits;
        }

        public Modifier Modifier { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0;
    }

    public sealed class PartyMascotBeast
    {
        internal PartyMascotBeast(int index, PartyMascotNamed named, string biome, string favourite, IReadOnlyList<PartyMascotAuraLine> aura, IReadOnlyList<PartyMascotNamed> branches)
        {
            Index = index;
            Id = named.Id;
            NameRu = named.NameRu;
            NameEn = named.NameEn;
            Biome = biome;
            Favourite = favourite;
            Aura = aura;
            Branches = branches;
        }

        public int Index { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Biome { get; }
        public string Favourite { get; }
        public IReadOnlyList<PartyMascotAuraLine> Aura { get; }
        public IReadOnlyList<PartyMascotNamed> Branches { get; }
    }

    public sealed class PartyMascotStage
    {
        internal PartyMascotStage(int index, int from, PartyMascotNamed named)
        {
            Index = index;
            From = from;
            Id = named.Id;
            NameRu = named.NameRu;
            NameEn = named.NameEn;
        }

        public int Index { get; }
        public int From { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class PartyMascotFood
    {
        internal PartyMascotFood(string id, string prefix, double experience)
        {
            Id = id;
            Prefix = prefix;
            Experience = experience;
        }

        public string Id { get; }
        public string Prefix { get; }
        public double Experience { get; }
        public bool IsDish => Prefix.Length == 0;
    }

    public sealed class PartyMascotGear
    {
        internal PartyMascotGear(PartyMascotNamed named, string slot, int level, int colossusTier)
        {
            Id = named.Id;
            NameRu = named.NameRu;
            NameEn = named.NameEn;
            Slot = slot;
            Level = level;
            ColossusTier = colossusTier;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Slot { get; }
        public int Level { get; }
        public int ColossusTier { get; }
    }

    public sealed class PartyMascotRules
    {
        public static readonly PartyMascotRules None = new PartyMascotRules();

        private readonly List<PartyMascotBeast> _beasts = new List<PartyMascotBeast>();
        private readonly List<PartyMascotStage> _stages = new List<PartyMascotStage>();
        private readonly List<PartyMascotFood> _foods = new List<PartyMascotFood>();
        private readonly List<PartyMascotNamed> _coats = new List<PartyMascotNamed>();
        private readonly List<PartyMascotGear> _gear = new List<PartyMascotGear>();
        private readonly List<string> _tempers = new List<string>();
        private readonly List<double> _memberScale = new List<double>();

        public IReadOnlyList<PartyMascotBeast> Beasts => _beasts;
        public IReadOnlyList<PartyMascotStage> Stages => _stages;
        public IReadOnlyList<PartyMascotFood> Foods => _foods;
        public IReadOnlyList<PartyMascotNamed> Coats => _coats;
        public IReadOnlyList<PartyMascotGear> Gear => _gear;
        public IReadOnlyList<string> Tempers => _tempers;
        public int MaxLevel { get; private set; } = 1;
        public double LevelBase { get; private set; } = 1;
        public double LevelStep { get; private set; }
        public double PerMinute { get; private set; }
        public int BranchFrom { get; private set; }
        public double FavouriteMultiplier { get; private set; } = 1;
        public int FeedPerDay { get; private set; }
        public double ChangeDays { get; private set; }
        public double ChangeKeep { get; private set; }
        public double AuraMin { get; private set; }
        public double AuraMax { get; private set; }
        public int Hearts { get; private set; }
        public double PerHeart { get; private set; } = 1;

        public PartyMascotBeast? Beast(string id)
        {
            foreach (var beast in _beasts)
            {
                if (beast.Id == id)
                    return beast;
            }
            return null;
        }

        public PartyMascotFood? FoodOf(string item)
        {
            foreach (var food in _foods)
            {
                if (!food.IsDish && item.StartsWith(food.Prefix, System.StringComparison.Ordinal))
                    return food;
            }
            return null;
        }

        public PartyMascotFood? Food(string id)
        {
            foreach (var food in _foods)
            {
                if (food.Id == id)
                    return food;
            }
            return null;
        }

        public double Need(int level) => level >= MaxLevel ? 0 : LevelBase + LevelStep * (level - 1);

        public PartyLevel LevelOf(double experience)
        {
            var level = 1;
            var left = System.Math.Max(0, experience);
            while (level < MaxLevel && left >= Need(level))
            {
                left -= Need(level);
                level++;
            }
            return new PartyLevel(level, level >= MaxLevel ? 0 : left, Need(level));
        }

        public PartyMascotStage StageOf(int level, int lastEarly = 0)
        {
            var stage = _stages[0];
            foreach (var candidate in _stages)
            {
                var from = candidate.Index == _stages.Count - 1 ? candidate.From - System.Math.Max(0, lastEarly) : candidate.From;
                if (level >= from)
                    stage = candidate;
            }
            return stage;
        }

        public bool BranchOpen(int level) => level >= BranchFrom;

        public double AuraAt(int level, int members)
        {
            if (level <= 0)
                return 0;
            var share = MaxLevel <= 1 ? 1 : (double)(System.Math.Min(level, MaxLevel) - 1) / (MaxLevel - 1);
            var scale = _memberScale.Count == 0 ? 1 : _memberScale[System.Math.Min(_memberScale.Count, System.Math.Max(1, members)) - 1];
            return (AuraMin + (AuraMax - AuraMin) * share) * scale;
        }

        public int HeartsOf(double bond) => System.Math.Min(Hearts, (int)System.Math.Floor(System.Math.Max(0, bond) / PerHeart + 1e-9));

        public static PartyMascotRules Read(ContentNode node)
        {
            var rules = new PartyMascotRules
            {
                MaxLevel = node.Field("maxLevel").AsInt(2, 1000),
                LevelBase = node.Field("levelBase").AsNumber(1, 1e9),
                LevelStep = node.Field("levelStep").AsNumber(0, 1e9),
                PerMinute = node.Field("perMinute").AsNumber(0, 1000),
                BranchFrom = node.Field("branchFrom").AsInt(1, 1000),
                FavouriteMultiplier = node.Field("favouriteMultiplier").AsNumber(1, 100),
                FeedPerDay = node.Field("feedPerDay").AsInt(0, 10000),
                ChangeDays = node.Field("changeDays").AsNumber(0, 365),
                ChangeKeep = node.Field("changeKeep").AsNumber(0, 1)
            };
            foreach (var item in node.Field("stages").Items())
            {
                var from = item.Field("from").AsInt(1, rules.MaxLevel);
                if (rules._stages.Count == 0 ? from != 1 : from <= rules._stages[rules._stages.Count - 1].From)
                    throw item.Field("from").Error("stages start on level 1 and go up");
                rules._stages.Add(new PartyMascotStage(rules._stages.Count, from, PartyMascotNamed.Read(item)));
            }
            foreach (var item in node.Field("foods").Items())
                rules._foods.Add(new PartyMascotFood(item.Field("id").AsString(), item.HasField("prefix") ? item.Field("prefix").AsString() : string.Empty,
                    item.Field("experience").AsNumber(0, 1e6)));
            var aura = node.Field("aura");
            rules.AuraMin = aura.Field("min").AsNumber(0, 10);
            rules.AuraMax = aura.Field("max").AsNumber(rules.AuraMin, 10);
            foreach (var scale in aura.Field("members").Items())
                rules._memberScale.Add(scale.AsNumber(0, 100));
            foreach (var temper in node.Field("tempers").Items())
                rules._tempers.Add(temper.AsString());
            var bond = node.Field("bond");
            rules.Hearts = bond.Field("hearts").AsInt(1, 100);
            rules.PerHeart = bond.Field("perHeart").AsNumber(1, 1e6);
            foreach (var item in node.Field("coats").Items())
                rules._coats.Add(PartyMascotNamed.Read(item));
            foreach (var item in node.Field("gear").Items())
                rules._gear.Add(new PartyMascotGear(PartyMascotNamed.Read(item), item.Field("slot").AsString(),
                    item.HasField("level") ? item.Field("level").AsInt(1, rules.MaxLevel) : 0,
                    item.HasField("colossusTier") ? item.Field("colossusTier").AsInt(1, 100_000) : 0));
            foreach (var item in node.Field("beasts").Items())
                rules._beasts.Add(ReadBeast(item, rules));
            return rules;
        }

        private static PartyMascotBeast ReadBeast(ContentNode item, PartyMascotRules rules)
        {
            var favourite = item.Field("favourite").AsString();
            if (rules.Food(favourite) == null)
                throw item.Field("favourite").Error($"unknown food '{favourite}'");
            var aura = new List<PartyMascotAuraLine>();
            foreach (var line in item.Field("aura").Items())
            {
                aura.Add(new PartyMascotAuraLine(AuraModifier(line), line.HasField("waits") ? line.Field("waits").AsString() : string.Empty));
            }
            var branches = new List<PartyMascotNamed>();
            foreach (var branch in item.Field("branches").Items())
                branches.Add(PartyMascotNamed.Read(branch));
            if (branches.Count != 2)
                throw item.Field("branches").Error("a beast has two branches");
            return new PartyMascotBeast(rules._beasts.Count, PartyMascotNamed.Read(item), item.Field("biome").AsString(), favourite, aura, branches);
        }

        private static Modifier AuraModifier(ContentNode line)
        {
            var tags = line.HasField("tags") ? ModifierReader.ReadTags(line.Field("tags")) : StatTags.None;
            var when = line.HasField("when") ? ModifierReader.ReadCondition(line.Field("when")) : ModifierCondition.Always;
            return new Modifier(new StatId(line.Field("stat").AsString()), ModifierReader.ReadKind(line.Field("kind")), 1, tags, ModifierPool.General, when);
        }
    }
}
