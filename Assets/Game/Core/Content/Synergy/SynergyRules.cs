using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Synergy
{
    public sealed class SynergyRules
    {
        public const string FileName = "synergy.json";
        public const int SupportedVersion = 1;
        public const int MaxMarks = 30;
        public const int MaxReactions = 32;
        public const string AuraMark = "aura";
        public const string LightningMark = "lightning";

        private readonly Dictionary<string, int> _markIndex;
        private readonly int[] _roleMask;

        private readonly int[] _markTicks;

        private SynergyRules(List<string> marks, Dictionary<string, int> markIndex, List<string> roles, int[] roleMask, StatId secondResponseStat,
            List<SynergyResponse> responses, List<SynergyReaction> reactions, List<SynergyConstellation> constellations, int[] markTicks, SynergyStateMarks stateMarks,
            double allyPoisonStacks)
        {
            AllyPoisonStacks = allyPoisonStacks;
            _markTicks = markTicks;
            StateMarks = stateMarks;
            Marks = marks;
            _markIndex = markIndex;
            Roles = roles;
            _roleMask = roleMask;
            SecondResponseStat = secondResponseStat;
            Responses = responses;
            Reactions = reactions;
            Constellations = constellations;
        }

        public IReadOnlyList<string> Marks { get; }
        public IReadOnlyList<string> Roles { get; }
        public StatId SecondResponseStat { get; }
        public IReadOnlyList<SynergyResponse> Responses { get; }
        public IReadOnlyList<SynergyReaction> Reactions { get; }
        public IReadOnlyList<SynergyConstellation> Constellations { get; }

        public SynergyStateMarks StateMarks { get; }

        public double AllyPoisonStacks { get; }

        public int MarkTicks(int mark) => _markTicks[mark];

        public int AuraMarkMask => _markIndex.TryGetValue(AuraMark, out var aura) ? 1 << aura : 0;

        public int LightningMarkMask => _markIndex.TryGetValue(LightningMark, out var lightning) ? 1 << lightning : 0;

        public int RoleMaskOf(HeroClass heroClass) => _roleMask[(int)heroClass];

        public int MaskOf(IReadOnlyList<string> marks)
        {
            var mask = 0;
            for (var i = 0; i < marks.Count; i++)
            {
                if (_markIndex.TryGetValue(marks[i], out var index))
                    mask |= 1 << index;
            }
            return mask;
        }

        public IReadOnlyList<string> IdsOf(int mask)
        {
            var ids = new List<string>();
            for (var i = 0; i < Marks.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                    ids.Add(Marks[i]);
            }
            return ids;
        }

        public int MaskOf(GemDef gem)
        {
            if (gem.Skill == null)
                return 0;
            var mask = MaskOf(gem.Skill.Marks);
            if (gem.Skill.Effect is AuraEffect aura && aura.Party)
                mask |= AuraMarkMask;
            return mask;
        }

        public void RequireGemMarks(GemCatalog gems)
        {
            foreach (var gem in gems.All)
            {
                if (gem.Skill == null)
                    continue;
                foreach (var mark in gem.Skill.Marks)
                {
                    if (!_markIndex.ContainsKey(mark))
                        throw new ContentException(GemCatalog.FileName, gem.Id, $"unknown mark '{mark}', expected one of {string.Join(", ", Marks)} from {FileName}");
                }
            }
        }

        public static SynergyRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var marks = new List<string>();
            var markIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in root.Field("marks").Items())
            {
                var mark = item.AsString();
                if (markIndex.ContainsKey(mark))
                    throw item.Error($"mark '{mark}' is listed twice");
                if (marks.Count == MaxMarks)
                    throw item.Error($"no more than {MaxMarks} marks");
                markIndex[mark] = marks.Count;
                marks.Add(mark);
            }

            var roles = new List<string>();
            var roleMask = new int[Enum.GetValues(typeof(HeroClass)).Length];
            var rolesNode = root.Field("roles");
            foreach (var className in rolesNode.FieldNames())
            {
                var heroClass = ClassOf(rolesNode.Field(className), className);
                var role = rolesNode.Field(className).AsString();
                if (!roles.Contains(role))
                    roles.Add(role);
                roleMask[(int)heroClass] = 1 << roles.IndexOf(role);
            }
            foreach (HeroClass heroClass in Enum.GetValues(typeof(HeroClass)))
            {
                if (roleMask[(int)heroClass] == 0)
                    throw rolesNode.Error($"class '{HeroClassNames.Of(heroClass)}' has no role");
            }

            var responses = new List<SynergyResponse>();
            foreach (var node in root.Field("responses").Items())
                responses.Add(ReadResponse(node, markIndex));
            var reactions = new List<SynergyReaction>();
            foreach (var node in root.Field("reactions").Items())
            {
                if (reactions.Count == MaxReactions)
                    throw node.Error($"no more than {MaxReactions} reactions fit in one party message");
                reactions.Add(ReadReaction(node, markIndex));
            }
            var constellations = new List<SynergyConstellation>();
            foreach (var node in root.Field("constellations").Items())
                constellations.Add(ReadConstellation(node, markIndex, roles));
            return new SynergyRules(marks, markIndex, roles, roleMask, new StatId(root.Field("secondResponseStat").AsString()), responses, reactions, constellations,
                ReadMarkTicks(root, markIndex, marks.Count), ReadStateMarks(root, markIndex),
                root.HasField("allyPoisonStacks") ? root.Field("allyPoisonStacks").AsNumber(0, 100000) : 0);
        }

        private static int[] ReadMarkTicks(ContentNode root, Dictionary<string, int> markIndex, int count)
        {
            var ticks = new int[count];
            if (!root.HasField("markSeconds"))
                return ticks;
            var node = root.Field("markSeconds");
            foreach (var name in node.FieldNames())
            {
                if (!markIndex.TryGetValue(name, out var index))
                    throw node.Field(name).Error($"unknown mark '{name}'");
                ticks[index] = (int)System.Math.Round(node.Field(name).AsNumber(0, 600) * GameLoop.TicksPerSecond);
            }
            return ticks;
        }

        private static SynergyStateMarks ReadStateMarks(ContentNode root, Dictionary<string, int> markIndex)
        {
            if (!root.HasField("stateMarks"))
                return default;
            var node = root.Field("stateMarks");
            int Mask(string field) => node.HasField(field) ? 1 << MarkOf(node.Field(field), markIndex) : 0;
            foreach (var name in node.FieldNames())
            {
                if (name != SynergyStateMarks.PoisonField && name != SynergyStateMarks.BurnField && name != SynergyStateMarks.MinionsField && name != SynergyStateMarks.StunField && name != SynergyStateMarks.HunterMarkField && name != SynergyStateMarks.BlindField)
                    throw node.Field(name).Error($"unknown state '{name}', expected {SynergyStateMarks.PoisonField}, {SynergyStateMarks.BurnField}, {SynergyStateMarks.MinionsField}, {SynergyStateMarks.StunField}, {SynergyStateMarks.HunterMarkField} or {SynergyStateMarks.BlindField}");
            }
            return new SynergyStateMarks(Mask(SynergyStateMarks.PoisonField), Mask(SynergyStateMarks.BurnField), Mask(SynergyStateMarks.MinionsField), Mask(SynergyStateMarks.StunField), Mask(SynergyStateMarks.HunterMarkField), Mask(SynergyStateMarks.BlindField));
        }

        private static SynergyResponse ReadResponse(ContentNode node, Dictionary<string, int> markIndex)
        {
            var markNode = node.Field("mark");
            var mark = markNode.AsString() == "any" ? SynergyResponse.AnyMark : MarkOf(markNode, markIndex);
            var perMark = node.HasField("perMark") && node.Field("perMark").AsBool();
            return new SynergyResponse(
                node.Field("id").AsString(),
                ClassOf(node.Field("class"), node.Field("class").AsString()),
                mark,
                node.HasField("second") && node.Field("second").AsBool(),
                perMark,
                perMark ? node.Field("maxMarks").AsInt(1, MaxMarks) : 1,
                ModifierReader.ReadList(node.Field("modifiers")));
        }

        private static SynergyReaction ReadReaction(ContentNode node, Dictionary<string, int> markIndex)
        {
            var pair = node.Field("marks").Items();
            if (pair.Count != 2)
                throw node.Field("marks").Error("a reaction needs exactly two marks");
            var first = MarkOf(pair[0], markIndex);
            var second = MarkOf(pair[1], markIndex);
            if (first == second)
                throw node.Field("marks").Error("a reaction needs two different marks");
            return new SynergyReaction(node.Field("id").AsString(), first, second,
                node.Field("seconds").AsNumber(0.1, 600), node.Field("cooldownSeconds").AsNumber(0, 3600),
                ModifierReader.ReadList(node.Field("modifiers")));
        }

        private static SynergyConstellation ReadConstellation(ContentNode node, Dictionary<string, int> markIndex, List<string> roles)
        {
            var classes = new List<HeroClass>();
            if (node.HasField("classes"))
            {
                foreach (var item in node.Field("classes").Items())
                    classes.Add(ClassOf(item, item.AsString()));
            }
            var groups = new List<int>();
            if (node.HasField("roles"))
            {
                foreach (var group in node.Field("roles").Items())
                {
                    var mask = 0;
                    foreach (var item in group.Items())
                    {
                        var index = roles.IndexOf(item.AsString());
                        if (index < 0)
                            throw item.Error($"unknown role '{item.AsString()}', expected one of {string.Join(", ", roles)}");
                        mask |= 1 << index;
                    }
                    groups.Add(mask);
                }
            }
            var marks = 0;
            if (node.HasField("marks"))
            {
                foreach (var item in node.Field("marks").Items())
                    marks |= 1 << MarkOf(item, markIndex);
            }
            var constellation = new SynergyConstellation(node.Field("id").AsString(), classes, groups, marks,
                node.HasField("noReactionCooldown") && node.Field("noReactionCooldown").AsBool(), ModifierReader.ReadList(node.Field("modifiers")));
            if (constellation.Members > 4)
                throw node.Error("a constellation can ask for at most 4 members");
            if (constellation.Members == 0 && marks == 0)
                throw node.Error("a constellation needs classes, roles or marks");
            return constellation;
        }

        private static int MarkOf(ContentNode node, Dictionary<string, int> markIndex)
        {
            if (!markIndex.TryGetValue(node.AsString(), out var index))
                throw node.Error($"unknown mark '{node.AsString()}'");
            return index;
        }

        private static HeroClass ClassOf(ContentNode node, string name)
        {
            if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                throw node.Error($"unknown class '{name}'");
            return heroClass;
        }
    }
}
