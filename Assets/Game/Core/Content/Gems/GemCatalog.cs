using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemCatalog
    {
        public const string FileName = "gems.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, GemKind> Kinds = new Dictionary<string, GemKind>(StringComparer.Ordinal)
        {
            ["skill"] = GemKind.Skill,
            ["support"] = GemKind.Support,
            ["trigger"] = GemKind.Trigger
        };

        private static readonly Dictionary<string, GemColor> Colors = new Dictionary<string, GemColor>(StringComparer.Ordinal)
        {
            ["red"] = GemColor.Red,
            ["green"] = GemColor.Green,
            ["blue"] = GemColor.Blue,
            ["white"] = GemColor.White
        };

        private static readonly Dictionary<string, TriggerCondition> Conditions = new Dictionary<string, TriggerCondition>(StringComparer.Ordinal)
        {
            ["mouseDistance"] = TriggerCondition.MouseDistance,
            ["bossTimerLeft"] = TriggerCondition.BossTimerLeft,
            ["idle"] = TriggerCondition.Idle,
            ["bossKilled"] = TriggerCondition.BossKilled,
            ["resourceFull"] = TriggerCondition.ResourceFull,
            ["withLeft"] = TriggerCondition.WithLeft,
            ["withRight"] = TriggerCondition.WithRight,
            ["everyKills"] = TriggerCondition.EveryKills,
            ["bossSpawned"] = TriggerCondition.BossSpawned,
            ["circle"] = TriggerCondition.Circle,
            ["flick"] = TriggerCondition.Flick,
            ["allyHeavyCast"] = TriggerCondition.AllyHeavyCast,
            ["resourceBelow"] = TriggerCondition.ResourceBelow,
            ["every"] = TriggerCondition.Every
        };

        private static readonly HashSet<TriggerCondition> ValuedConditions = new HashSet<TriggerCondition>
        {
            TriggerCondition.MouseDistance,
            TriggerCondition.BossTimerLeft,
            TriggerCondition.Idle,
            TriggerCondition.EveryKills,
            TriggerCondition.Circle,
            TriggerCondition.AllyHeavyCast,
            TriggerCondition.ResourceBelow,
            TriggerCondition.Every
        };

        private static readonly Dictionary<string, SpecialKey> SpecialKeys = new Dictionary<string, SpecialKey>(StringComparer.Ordinal)
        {
            ["space"] = SpecialKey.Space,
            ["backspace"] = SpecialKey.Backspace,
            ["combo"] = SpecialKey.Combo
        };

        private static readonly Dictionary<string, StackTrigger> StackTriggers = new Dictionary<string, StackTrigger>(StringComparer.Ordinal)
        {
            ["kill"] = StackTrigger.Kill,
            ["keyHit"] = StackTrigger.KeyHit
        };

        private static readonly Dictionary<string, MinionKind> MinionKinds = new Dictionary<string, MinionKind>(StringComparer.Ordinal)
        {
            ["cannon"] = MinionKind.Cannon,
            ["skeleton"] = MinionKind.Skeleton
        };

        private static readonly Dictionary<string, DotKind> DotKinds = new Dictionary<string, DotKind>(StringComparer.Ordinal)
        {
            ["poison"] = DotKind.Poison,
            ["burn"] = DotKind.Burn
        };

        private static readonly Dictionary<string, ArmOn> ArmOns = new Dictionary<string, ArmOn>(StringComparer.Ordinal)
        {
            ["flick"] = ArmOn.Flick,
            ["circle"] = ArmOn.Circle
        };

        private readonly Dictionary<string, GemDef> _byId;
        private readonly Dictionary<int, GemDef> _byNetId;
        private readonly Dictionary<string, string> _formerIds;
        private readonly Dictionary<string, GemDef> _byPath = new Dictionary<string, GemDef>(StringComparer.Ordinal);

        private GemCatalog(List<GemDef> gems, GemLevels levels, Dictionary<string, string> formerIds)
        {
            _formerIds = formerIds;
            All = gems;
            Levels = levels;
            _byId = new Dictionary<string, GemDef>(StringComparer.Ordinal);
            _byNetId = new Dictionary<int, GemDef>();
            foreach (var gem in gems)
            {
                _byId.Add(gem.Id, gem);
                _byNetId.Add(gem.NetId, gem);
                if (gem.Ascendancy != null)
                    _byPath.Add(gem.Ascendancy, gem);
            }
        }

        public IReadOnlyList<GemDef> All { get; }
        public GemLevels Levels { get; }

        public GemDef this[string id] => _byId[id];

        public GemDef? ForPath(string? pathId) => pathId != null && _byPath.TryGetValue(pathId, out var gem) ? gem : null;

        public bool TryGet(string id, out GemDef gem) =>
            _byId.TryGetValue(id, out gem!) || (_formerIds.TryGetValue(id, out var current) && _byId.TryGetValue(current, out gem!));

        public bool TryGetByNetId(int netId, out GemDef gem) => _byNetId.TryGetValue(netId, out gem!);

        public static GemCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var gems = new List<GemDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var netIds = new Dictionary<int, string>();
            var formerIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("gems").Items())
            {
                var gem = ReadGem(node);
                ReadFormerIds(node, gem.Id, formerIds);
                if (!ids.Add(gem.Id))
                    throw node.Field("id").Error($"gem id '{gem.Id}' is used twice");
                if (netIds.TryGetValue(gem.NetId, out var clash))
                    throw node.Field("id").Error($"gem id '{gem.Id}' has the same network hash as '{clash}', rename one of them");
                netIds.Add(gem.NetId, gem.Id);
                if (gem.Ascendancy != null && !paths.Add(gem.Ascendancy))
                    throw node.Field("ascendancy").Error($"path '{gem.Ascendancy}' already has a skill");
                gems.Add(gem);
            }
            foreach (var former in formerIds)
            {
                if (ids.Contains(former.Key))
                    throw root.Field("gems").Error($"former id '{former.Key}' of '{former.Value}' is still a gem id");
            }
            return new GemCatalog(gems, GemLevels.Read(root.Field("levels")), formerIds);
        }

        private static void ReadFormerIds(ContentNode node, string id, Dictionary<string, string> formerIds)
        {
            if (!node.HasField("formerIds"))
                return;
            foreach (var former in node.Field("formerIds").Items())
            {
                var formerId = former.AsString();
                if (formerIds.ContainsKey(formerId))
                    throw former.Error($"former id '{formerId}' is used twice");
                formerIds.Add(formerId, id);
            }
        }

        private static GemDef ReadGem(ContentNode node)
        {
            var id = node.Field("id").AsString();
            var kind = Pick(node.Field("kind"), Kinds, "skill, support or trigger");
            var color = Pick(node.Field("color"), Colors, "red, green, blue or white");
            var name = node.Field("name");
            var tags = node.HasField("tags") ? ModifierReader.ReadTags(node.Field("tags")) : StatTags.None;
            var nameRu = name.Field("ru").AsString();
            var nameEn = name.Field("en").AsString();
            var requires = new Dictionary<string, double>(StringComparer.Ordinal);
            if (node.HasField("requires"))
            {
                foreach (var attribute in node.Field("requires").FieldNames())
                    requires[attribute] = node.Field("requires").Field(attribute).AsNumber(0, 10000);
            }
            switch (kind)
            {
                case GemKind.Skill:
                    return ReadSkillGem(node, id, color, nameRu, nameEn, tags, requires);
                case GemKind.Trigger:
                    return new GemDef(id, kind, color, nameRu, nameEn, tags, trigger: ReadTrigger(node), requires: requires);
                default:
                    return new GemDef(id, kind, color, nameRu, nameEn, tags, supportEffect: node.Field("effect").Field("type").AsString(), requires: requires);
            }
        }

        private static GemDef ReadSkillGem(ContentNode node, string id, GemColor color, string nameRu, string nameEn, StatTags tags, Dictionary<string, double> requires)
        {
            var ascendancy = node.HasField("ascendancy") ? node.Field("ascendancy").AsString() : null;
            var locked = node.HasField("locked") && node.Field("locked").AsBool();
            if (ascendancy != null && !node.HasField("weapon"))
                throw node.Field("ascendancy").Error("a path skill is a class skill and needs a weapon");
            if (locked && ascendancy == null)
                throw node.Field("locked").Error("only a path skill can wait for its mechanic");
            if (locked == node.HasField("effect"))
                throw node.Error(locked ? "a locked path skill has no effect yet" : "a skill needs an effect");
            return new GemDef(id, GemKind.Skill, color, nameRu, nameEn, tags, skill: ReadSkill(node, locked), requires: requires, ascendancy: ascendancy, locked: locked);
        }

        private static SkillDef ReadSkill(ContentNode node, bool locked)
        {
            var cost = node.Field("cost");
            var resource = cost.HasField("resource") ? cost.Field("resource").AsNumber(0, 1e6) : 0;
            var duration = node.HasField("durationSeconds") ? node.Field("durationSeconds").AsNumber(0, 3600) : 0;
            var effect = locked ? new PassiveEffect(Array.Empty<Modifier>(), false) : ReadEffect(node.Field("effect"), duration);
            if (effect is AuraEffect aura && !aura.Reserves && duration <= 0)
                throw node.Error("an aura needs durationSeconds or reserve");
            if ((effect is StoreHitsEffect || effect is QueueHitsEffect || effect is KeyRepeatEffect || effect is StreakStrikeEffect || effect is InputHitsEffect || effect is TrailEffect || (effect is StacksEffect stacks && !stacks.Passive)) && duration <= 0)
                throw node.Error($"a {node.Field("effect").Field("type").AsString()} skill needs durationSeconds");
            if ((effect is EnemyDebuffEffect || effect is CurseFlowEffect || (effect is AddStacksEffect flowing && flowing.PerSecond > 0)) && duration <= 0)
                throw node.Error($"a {node.Field("effect").Field("type").AsString()} skill needs durationSeconds");
            var marks = new List<string>();
            if (node.HasField("marks"))
            {
                foreach (var mark in node.Field("marks").Items())
                    marks.Add(mark.AsString());
            }
            var weapon = node.HasField("weapon") ? node.Field("weapon").AsString() : null;
            var native = node.HasField("native") ? ReadNative(node.Field("native")) : null;
            if (native != null && weapon == null)
                throw node.Field("native").Error("a native trait needs a weapon: only class skills have one");
            var family = node.HasField("family") ? node.Field("family").AsString() : null;
            var armOn = node.HasField("armOn") ? Pick(node.Field("armOn"), ArmOns, "flick or circle") : ArmOn.None;
            return new SkillDef(resource, node.Field("cooldownSeconds").AsNumber(0, 3600), duration, effect, marks, weapon, native, family, armOn);
        }

        private static SkillEffect ReadEffect(ContentNode node, double durationSeconds)
        {
            var typeNode = node.Field("type");
            switch (typeNode.AsString())
            {
                case "chainHits":
                    return new ChainHitsEffect(node.Field("hits").AsInt(1, 100000), node.Field("nextEnemyShare").AsNumber(0, 100));
                case "strike":
                    return new StrikeEffect(
                        node.Field("multiplier").AsNumber(0, 1e6),
                        node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0,
                        node.HasField("skipCurrent") && node.Field("skipCurrent").AsBool(),
                        node.HasField("killRefundShare") ? node.Field("killRefundShare").AsNumber(0, 1) : 0,
                        Number(node, "queueMultiplier", 1e6),
                        Number(node, "executeBelow", 1),
                        node.HasField("coinFlip") && node.Field("coinFlip").AsBool());
                case "aura":
                    return new AuraEffect(
                        ModifierReader.ReadList(node.Field("modifiers")),
                        node.HasField("party") && node.Field("party").AsBool(),
                        node.HasField("reserve") ? node.Field("reserve").AsNumber(0, 100) : 0,
                        node.HasField("focusLockAfterSeconds") ? node.Field("focusLockAfterSeconds").AsNumber(0, 3600) : 0,
                        node.HasField("keylessStrike") ? node.Field("keylessStrike").Field("everySeconds").AsNumber(0.1, 3600) : 0,
                        node.HasField("keylessStrike") ? node.Field("keylessStrike").Field("multiplier").AsNumber(0, 1e6) : 0);
                case "keyRepeat":
                    return new KeyRepeatEffect(node.Field("every").AsInt(1, 10000), node.Field("multiplier").AsNumber(0, 1000));
                case "specialKeys":
                {
                    var charges = node.HasField("charges") ? node.Field("charges").AsInt(1, 10000) : 0;
                    return new SpecialKeysEffect(
                        Pick(node.Field("key"), SpecialKeys, "space, backspace or combo"),
                        node.Field("bonus").AsNumber(0, 1000),
                        charges,
                        charges == 0 && durationSeconds <= 0);
                }
                case "streakStrike":
                    return new StreakStrikeEffect(
                        node.Field("hits").AsInt(1, 10000),
                        node.Field("maxGapSeconds").AsNumber(0.1, 3600),
                        node.Field("multiplier").AsNumber(0, 1e6));
                case "flow":
                    return new FlowEffect(
                        node.Field("afterSeconds").AsNumber(0.1, 86400),
                        node.Field("maxGapSeconds").AsNumber(0.1, 3600),
                        ModifierReader.ReadList(node.Field("modifiers")));
                case "storeHits":
                    return new StoreHitsEffect(node.Field("releaseShare").AsNumber(0, 100));
                case "queueHits":
                    return new QueueHitsEffect(node.Field("share").AsNumber(0, 100));
                case "stacks":
                    return new StacksEffect(
                        Pick(node.Field("on"), StackTriggers, "kill or keyHit"),
                        ModifierReader.Read(node.Field("perStack")),
                        node.Field("max").AsNumber(0, 1e6),
                        node.HasField("stackSeconds") ? node.Field("stackSeconds").AsNumber(0.1, 3600) : 0);
                case "inputHits":
                    return new InputHitsEffect(
                        Number(node, "pxPerHit", 1e6),
                        Number(node, "pxShare", 1000),
                        Number(node, "circle", 1000),
                        Number(node, "flick", 1000),
                        Number(node, "reversal", 1000),
                        node.HasField("maxHits") ? node.Field("maxHits").AsInt(1, 10000) : 0);
                case "distanceStacks":
                    return new DistanceStacksEffect(
                        node.Field("pxPerStack").AsNumber(1, 1e9),
                        node.Field("windowSeconds").AsInt(1, 3600),
                        new StacksEffect(StackTrigger.Kill, ModifierReader.Read(node.Field("perStack")), node.Field("max").AsNumber(0, 1e6), 0));
                case "trail":
                    return new TrailEffect(node.Field("pxPerMultiplier").AsNumber(1, 1e9));
                case "bossHunt":
                    return new BossHuntEffect(
                        node.Field("bonus").AsNumber(0, 1000),
                        node.Field("lastSeconds").AsNumber(0, 3600),
                        node.Field("lastBonus").AsNumber(0, 1000));
                case "composure":
                    return new ComposureEffect(node.Field("multiplier").AsNumber(1, 1000));
                case "passive":
                    return new PassiveEffect(
                        ModifierReader.ReadList(node.Field("modifiers")),
                        node.HasField("perPartyMember") && node.Field("perPartyMember").AsBool());
                case "breakBonus":
                    return new BreakBonusEffect(
                        node.Field("breakSeconds").AsNumber(1, 86400),
                        node.Field("seconds").AsNumber(0.1, 86400),
                        ModifierReader.ReadList(node.Field("modifiers")));
                case "gainResource":
                    return new ResourceGainEffect(node.Field("amount").AsNumber(0, 1e6));
                case "echo":
                    return new EchoEffect(node.Field("power").AsNumber(0, 10));
                case "nextKillReward":
                    return new NextKillRewardEffect(
                        node.Field("multiplier").AsNumber(1, 1000),
                        Number(node, "shardChance", 1),
                        Number(node, "shards", 1e6));
                case "enemyDebuff":
                    return new EnemyDebuffEffect(node.Field("damageTaken").AsNumber(0, 10), Number(node, "bossTimerSlow", 0.9));
                case "curseFlow":
                    return new CurseFlowEffect(node.Field("hitsPerSecond").AsNumber(0, 100));
                case "summon":
                    return new SummonEffect(
                        Pick(node.Field("minion"), MinionKinds, "cannon or skeleton"),
                        node.Field("seconds").AsNumber(0.1, 3600),
                        node.Field("hitsPerSecond").AsNumber(0.01, 100),
                        node.Field("multiplier").AsNumber(0, 1e6));
                case "addStacks":
                    return new AddStacksEffect(
                        Pick(node.Field("dot"), DotKinds, "poison or burn"),
                        Number(node, "stacks", 1e6),
                        Number(node, "perSecond", 1e6),
                        Number(node, "multiplier", 1e6));
                case "detonate":
                    return new DetonateEffect(node.Field("seconds").AsNumber(0.1, 3600));
                case "idleStrike":
                    return new IdleStrikeEffect(
                        node.Field("chargePerSecond").AsNumber(0, 1000),
                        node.Field("maxChargeSeconds").AsNumber(0.1, 86400),
                        node.Field("minIdleSeconds").AsNumber(0, 86400));
                default:
                    throw typeNode.Error($"unknown skill effect '{typeNode.AsString()}', expected chainHits, strike, aura, storeHits, queueHits, stacks, keyRepeat, specialKeys, streakStrike, flow, inputHits, distanceStacks, trail, bossHunt, composure, passive, breakBonus, gainResource, echo, nextKillReward, enemyDebuff, curseFlow, summon, addStacks, detonate or idleStrike");
            }
        }

        private static NativeTrait ReadNative(ContentNode node) =>
            new NativeTrait
            {
                Modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>(),
                HoldDecaySeconds = Number(node, "holdDecaySeconds", 3600),
                SpendAllPerPoint = node.HasField("spendAll") ? node.Field("spendAll").Field("perPoint").AsNumber(0, 1000) : 0,
                SpendAllMax = node.HasField("spendAll") ? node.Field("spendAll").Field("max").AsNumber(0, 1e6) : 0,
                ResourcePerKeyHit = Number(node, "resourcePerKeyHit", 100),
                WoundedSpawnResource = Number(node, "woundedSpawnResource", 100),
                HoldStacksAboveResource = Number(node, "holdStacksAboveResource", 100),
                FullResourceDurationMultiplier = Number(node, "fullResourceDurationMultiplier", 100),
                ExtraTargetWhileFamily = node.HasField("extraTargetWhileFamily") ? node.Field("extraTargetWhileFamily").AsString() : null,
                RefreshFamily = node.HasField("refreshFamily") ? node.Field("refreshFamily").AsString() : null
            };

        private static double Number(ContentNode node, string field, double max) =>
            node.HasField(field) ? node.Field(field).AsNumber(0, max) : 0;

        private static TriggerDef ReadTrigger(ContentNode node)
        {
            var condition = node.Field("condition");
            var type = Pick(condition.Field("type"), Conditions, string.Join(", ", Conditions.Keys));
            var value = ValuedConditions.Contains(type) ? condition.Field("value").AsNumber(0.001, 1e9) : 0;
            return new TriggerDef(type, value, node.Field("cooldownMultiplier").AsNumber(1, 100));
        }

        private static T Pick<T>(ContentNode node, Dictionary<string, T> names, string expected)
        {
            var text = node.AsString();
            if (!names.TryGetValue(text, out var value))
                throw node.Error($"unknown value '{text}', expected {expected}");
            return value;
        }
    }
}
