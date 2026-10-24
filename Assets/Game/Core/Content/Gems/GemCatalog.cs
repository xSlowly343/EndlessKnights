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
            ["enter"] = TriggerCondition.Enter
        };

        private readonly Dictionary<string, GemDef> _byId;
        private readonly Dictionary<int, GemDef> _byNetId;

        private GemCatalog(List<GemDef> gems, GemLevels levels)
        {
            All = gems;
            Levels = levels;
            _byId = new Dictionary<string, GemDef>(StringComparer.Ordinal);
            _byNetId = new Dictionary<int, GemDef>();
            foreach (var gem in gems)
            {
                _byId.Add(gem.Id, gem);
                _byNetId.Add(gem.NetId, gem);
            }
        }

        public IReadOnlyList<GemDef> All { get; }
        public GemLevels Levels { get; }

        public GemDef this[string id] => _byId[id];

        public bool TryGet(string id, out GemDef gem) => _byId.TryGetValue(id, out gem!);

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
            foreach (var node in root.Field("gems").Items())
            {
                var gem = ReadGem(node);
                if (!ids.Add(gem.Id))
                    throw node.Field("id").Error($"gem id '{gem.Id}' is used twice");
                if (netIds.TryGetValue(gem.NetId, out var clash))
                    throw node.Field("id").Error($"gem id '{gem.Id}' has the same network hash as '{clash}', rename one of them");
                netIds.Add(gem.NetId, gem.Id);
                gems.Add(gem);
            }
            return new GemCatalog(gems, GemLevels.Read(root.Field("levels")));
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
                    return new GemDef(id, kind, color, nameRu, nameEn, tags, skill: ReadSkill(node), requires: requires);
                case GemKind.Trigger:
                    return new GemDef(id, kind, color, nameRu, nameEn, tags, trigger: ReadTrigger(node), requires: requires);
                default:
                    return new GemDef(id, kind, color, nameRu, nameEn, tags, supportEffect: node.Field("effect").Field("type").AsString(), requires: requires);
            }
        }

        private static SkillDef ReadSkill(ContentNode node)
        {
            var cost = node.Field("cost");
            var resource = cost.HasField("resource") ? cost.Field("resource").AsNumber(0, 1e6) : 0;
            var duration = node.HasField("durationSeconds") ? node.Field("durationSeconds").AsNumber(0, 3600) : 0;
            var effect = ReadEffect(node.Field("effect"));
            if (effect is AuraEffect && duration <= 0)
                throw node.Error("an aura needs durationSeconds");
            if (effect is EnemyDebuffEffect && duration <= 0)
                throw node.Error("an enemy debuff needs durationSeconds");
            var marks = new List<string>();
            if (node.HasField("marks"))
            {
                foreach (var mark in node.Field("marks").Items())
                    marks.Add(mark.AsString());
            }
            var weapon = node.HasField("weapon") ? node.Field("weapon").AsString() : null;
            return new SkillDef(resource, node.Field("cooldownSeconds").AsNumber(0, 3600), duration, effect, marks, weapon);
        }

        private static SkillEffect ReadEffect(ContentNode node)
        {
            var typeNode = node.Field("type");
            switch (typeNode.AsString())
            {
                case "chainHits":
                    return new ChainHitsEffect(node.Field("hits").AsInt(1, 100000), node.Field("nextEnemyShare").AsNumber(0, 100));
                case "strike":
                    return new StrikeEffect(node.Field("multiplier").AsNumber(0, 1e6));
                case "aura":
                    return new AuraEffect(ModifierReader.ReadList(node.Field("modifiers")), node.HasField("party") && node.Field("party").AsBool());
                case "enemyDebuff":
                    return new EnemyDebuffEffect(node.Field("damageTaken").AsNumber(0, 10));
                case "idleStrike":
                    return new IdleStrikeEffect(
                        node.Field("chargePerSecond").AsNumber(0, 1000),
                        node.Field("maxChargeSeconds").AsNumber(0.1, 86400),
                        node.Field("minIdleSeconds").AsNumber(0, 86400));
                default:
                    throw typeNode.Error($"unknown skill effect '{typeNode.AsString()}', expected chainHits, strike, aura, enemyDebuff or idleStrike");
            }
        }

        private static TriggerDef ReadTrigger(ContentNode node) =>
            new TriggerDef(
                Pick(node.Field("condition").Field("type"), Conditions, "enter"),
                node.Field("cooldownMultiplier").AsNumber(1, 100));

        private static T Pick<T>(ContentNode node, Dictionary<string, T> names, string expected)
        {
            var text = node.AsString();
            if (!names.TryGetValue(text, out var value))
                throw node.Error($"unknown value '{text}', expected {expected}");
            return value;
        }
    }
}
