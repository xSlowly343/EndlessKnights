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

    public sealed class PartyMascotCallLine
    {
        internal PartyMascotCallLine(Modifier? modifier, bool bossTimerStop, bool focusFull, string waits)
        {
            Modifier = modifier;
            BossTimerStop = bossTimerStop;
            FocusFull = focusFull;
            Waits = waits;
        }

        public Modifier? Modifier { get; }
        public bool BossTimerStop { get; }
        public bool FocusFull { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0;
    }

    public sealed class PartyMascotRite
    {
        internal PartyMascotRite(int branch, int index, PartyMascotNamed named, Modifier modifier)
        {
            Branch = branch;
            Index = index;
            Id = named.Id;
            NameRu = named.NameRu;
            NameEn = named.NameEn;
            Modifier = modifier;
        }

        public int Branch { get; }
        public int Index { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public Modifier Modifier { get; }
    }

    public sealed class PartyMascotTrial
    {
        internal PartyMascotTrial(string stage, PartyMascotNamed named, int need)
        {
            Stage = stage;
            Id = named.Id;
            NameRu = named.NameRu;
            NameEn = named.NameEn;
            Need = need;
        }

        public string Stage { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int Need { get; }
    }

    public sealed class PartyMascotBeast
    {
        internal PartyMascotBeast(int index, PartyMascotNamed named, string biome, string favourite, IReadOnlyList<PartyMascotAuraLine> aura, IReadOnlyList<PartyMascotNamed> branches,
            IReadOnlyList<PartyMascotCallLine> call, IReadOnlyList<string> gestures, string gift)
        {
            Call = call;
            Gestures = gestures;
            Gift = gift;
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
        public IReadOnlyList<PartyMascotCallLine> Call { get; }
        public IReadOnlyList<string> Gestures { get; }
        public string Gift { get; }
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
        public int RitePointEvery { get; private set; } = 5;
        public int RiteMaxRank { get; private set; }
        public int RiteSignsPerRank { get; private set; }
        public IReadOnlyList<PartyMascotNamed> RiteBranches => _riteBranches;
        public IReadOnlyList<PartyMascotRite> Rites => _rites;
        public double CallGauge { get; private set; }
        public double CallSeconds { get; private set; }
        public double CallMouseStep { get; private set; } = 100;
        public double CallReportSeconds { get; private set; } = 300;
        public IReadOnlyList<PartyMascotTrial> Trials => _trials;
        public double OfflineShare { get; private set; }
        public double GiftPerLevel { get; private set; }
        public double SitAfterSeconds { get; private set; }
        public double SleepAfterSeconds { get; private set; }
        public double CheerSeconds { get; private set; }
        public double GestureMinSeconds { get; private set; }
        public double GestureMaxSeconds { get; private set; }
        private readonly List<PartyMascotNamed> _riteBranches = new List<PartyMascotNamed>();
        private readonly List<PartyMascotRite> _rites = new List<PartyMascotRite>();
        private readonly List<PartyMascotTrial> _trials = new List<PartyMascotTrial>();
        private readonly Dictionary<string, IReadOnlyList<string>> _temperGestures = new Dictionary<string, IReadOnlyList<string>>();

        public PartyMascotRite? Rite(string id)
        {
            foreach (var rite in _rites)
            {
                if (rite.Id == id)
                    return rite;
            }
            return null;
        }

        public int RitePoints(int level) => System.Math.Max(0, level) / RitePointEvery;

        public double Gift(int level) => GiftPerLevel * System.Math.Max(0, level);

        public double CallActions(double keys, int clicks, double mousePx) => System.Math.Max(0, keys) + System.Math.Max(0, clicks) + System.Math.Max(0, mousePx) / CallMouseStep;

        public IReadOnlyList<string> TemperGestures(string temper) =>
            _temperGestures.TryGetValue(temper, out var gestures) ? gestures : System.Array.Empty<string>();

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
            if (node.HasField("rites"))
                ReadExtras(node, rules);
            foreach (var item in node.Field("beasts").Items())
                rules._beasts.Add(ReadBeast(item, rules));
            return rules;
        }

        private static void ReadExtras(ContentNode node, PartyMascotRules rules)
        {
            var rites = node.Field("rites");
            rules.RitePointEvery = rites.Field("pointEvery").AsInt(1, 1000);
            rules.RiteMaxRank = rites.Field("maxRank").AsInt(1, 100);
            rules.RiteSignsPerRank = rites.Field("signsPerRank").AsInt(0, 1_000_000);
            foreach (var branch in rites.Field("branches").Items())
            {
                var branchIndex = rules._riteBranches.Count;
                rules._riteBranches.Add(PartyMascotNamed.Read(branch));
                var index = 0;
                foreach (var item in branch.Field("nodes").Items())
                    rules._rites.Add(new PartyMascotRite(branchIndex, index++, PartyMascotNamed.Read(item), ModifierReader.Read(item)));
            }
            var call = node.Field("call");
            rules.CallGauge = call.Field("gauge").AsNumber(1, 1e9);
            rules.CallSeconds = call.Field("seconds").AsNumber(1, 3600);
            rules.CallMouseStep = call.Field("mouseStep").AsNumber(1, 1e6);
            rules.CallReportSeconds = call.Field("reportSeconds").AsNumber(10, 86400);
            foreach (var item in node.Field("trials").Items())
            {
                var stage = item.Field("stage").AsString();
                var known = false;
                foreach (var candidate in rules._stages)
                    known |= candidate.Id == stage;
                if (!known)
                    throw item.Field("stage").Error($"unknown stage '{stage}'");
                rules._trials.Add(new PartyMascotTrial(stage, PartyMascotNamed.Read(item), item.Field("need").AsInt(1, 1_000_000)));
            }
            rules.OfflineShare = node.Field("offline").Field("share").AsNumber(0, 1);
            rules.GiftPerLevel = node.Field("giftPerLevel").AsNumber(0, 1);
            var strip = node.Field("strip");
            rules.SitAfterSeconds = strip.Field("sitAfterSeconds").AsNumber(0, 3600);
            rules.SleepAfterSeconds = strip.Field("sleepAfterSeconds").AsNumber(rules.SitAfterSeconds, 36000);
            rules.CheerSeconds = strip.Field("cheerSeconds").AsNumber(0, 60);
            rules.GestureMinSeconds = strip.Field("gestureMinSeconds").AsNumber(1, 3600);
            rules.GestureMaxSeconds = strip.Field("gestureMaxSeconds").AsNumber(rules.GestureMinSeconds, 3600);
            var tempers = strip.Field("tempers");
            foreach (var temper in rules._tempers)
            {
                var gestures = new List<string>();
                if (tempers.HasField(temper))
                {
                    foreach (var gesture in tempers.Field(temper).Items())
                        gestures.Add(gesture.AsString());
                }
                rules._temperGestures[temper] = gestures;
            }
        }

        private static IReadOnlyList<PartyMascotCallLine> ReadCall(ContentNode item)
        {
            var lines = new List<PartyMascotCallLine>();
            if (!item.HasField("call"))
                return lines;
            foreach (var line in item.Field("call").Items())
            {
                var waits = line.HasField("waits") ? line.Field("waits").AsString() : string.Empty;
                var stop = line.HasField("bossTimerStop") && line.Field("bossTimerStop").AsBool();
                var focus = line.HasField("focusFull") && line.Field("focusFull").AsBool();
                lines.Add(new PartyMascotCallLine(line.HasField("stat") ? ModifierReader.Read(line) : (Modifier?)null, stop, focus, waits));
            }
            return lines;
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
            var gestures = new List<string>();
            if (item.HasField("gestures"))
            {
                foreach (var gesture in item.Field("gestures").Items())
                    gestures.Add(gesture.AsString());
            }
            return new PartyMascotBeast(rules._beasts.Count, PartyMascotNamed.Read(item), item.Field("biome").AsString(), favourite, aura, branches,
                ReadCall(item), gestures, item.HasField("gift") ? item.Field("gift").AsString() : string.Empty);
        }

        private static Modifier AuraModifier(ContentNode line)
        {
            var tags = line.HasField("tags") ? ModifierReader.ReadTags(line.Field("tags")) : StatTags.None;
            var when = line.HasField("when") ? ModifierReader.ReadCondition(line.Field("when")) : ModifierCondition.Always;
            return new Modifier(new StatId(line.Field("stat").AsString()), ModifierReader.ReadKind(line.Field("kind")), 1, tags, ModifierPool.General, when);
        }
    }
}
