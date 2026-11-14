using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public static class PartySetReader
    {
        public const string Prefix = "party_";
        public const string Armor = "armor";
        public const string Jewelry = "jewelry";

        private sealed class Scale
        {
            public readonly List<ItemRarity> Rarities = new List<ItemRarity>();
            public readonly List<double> Main = new List<double>();
            public readonly List<double> More = new List<double>();
            public double SecondShare;
            public ItemRarity SecondFrom;
            public ItemRarity MoreFrom;
            public double MoreTwoShare;
            public ItemRarity MoreTwoFrom;
            public ItemRarity SpecialFrom;
            public ItemRarity SpecialTwoFrom;
        }

        private readonly struct LineTemplate
        {
            public LineTemplate(Modifier modifier, IReadOnlyList<double> values)
            {
                Modifier = modifier;
                Values = values;
            }

            public Modifier Modifier { get; }
            public IReadOnlyList<double> Values { get; }
        }

        public static string IdOf(string line, string kind, ItemRarity rarity) => Prefix + line + "_" + kind + "_" + ItemRarityNames.Of(rarity);

        public sealed class Drop
        {
            internal Drop(double chance, List<(int UpTo, List<(ItemRarity Rarity, double Weight)> Weights)> steps)
            {
                Chance = chance;
                Steps = steps;
            }

            public double Chance { get; }
            public IReadOnlyList<(int UpTo, List<(ItemRarity Rarity, double Weight)> Weights)> Steps { get; }

            public ItemRarity Roll(int depth, double roll)
            {
                var step = Steps[Steps.Count - 1];
                foreach (var candidate in Steps)
                {
                    if (candidate.UpTo == 0 || depth <= candidate.UpTo)
                    {
                        step = candidate;
                        break;
                    }
                }
                var total = 0.0;
                foreach (var weight in step.Weights)
                    total += weight.Weight;
                var left = roll * total;
                foreach (var weight in step.Weights)
                {
                    left -= weight.Weight;
                    if (left < 0)
                        return weight.Rarity;
                }
                return step.Weights[step.Weights.Count - 1].Rarity;
            }
        }

        internal static Dictionary<string, Drop> ReadDrops(ContentNode node)
        {
            var drops = new Dictionary<string, Drop>(StringComparer.Ordinal);
            if (!node.HasField("drops"))
                return drops;
            var field = node.Field("drops");
            foreach (var source in field.FieldNames())
            {
                var entry = field.Field(source);
                var steps = new List<(int, List<(ItemRarity, double)>)>();
                foreach (var step in entry.Field("steps").Items())
                {
                    var weights = new List<(ItemRarity, double)>();
                    var rarities = step.Field("rarities");
                    foreach (var name in rarities.FieldNames())
                    {
                        if (!ItemRarityNames.ByName.TryGetValue(name, out var rarity))
                            throw rarities.Field(name).Error($"unknown rarity '{name}'");
                        weights.Add((rarity, rarities.Field(name).AsNumber(0, 1e6)));
                    }
                    steps.Add((step.HasField("upTo") ? step.Field("upTo").AsInt(1, 10_000_000) : 0, weights));
                }
                drops[source] = new Drop(entry.Field("chance").AsNumber(0, 1), steps);
            }
            return drops;
        }

        internal static Dictionary<string, string> ReadSourceLinks(ContentNode node)
        {
            var links = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!node.HasField("sourceOf"))
                return links;
            var field = node.Field("sourceOf");
            foreach (var name in field.FieldNames())
                links[name] = field.Field(name).AsString();
            return links;
        }

        internal static void Read(ContentNode node, List<SetDef> sets, HashSet<string> ids)
        {
            var scale = ReadScale(node);
            var pieces = node.Field("pieces");
            foreach (var line in node.Field("lines").Items())
            {
                var lineId = line.Field("id").AsString();
                var from = line.HasField("from") ? Rarity(line.Field("from")) : scale.Rarities[0];
                var name = line.Field("name");
                foreach (var kind in new[] { Armor, Jewelry })
                {
                    var templates = line.Field(kind);
                    var other = line.Field(kind == Armor ? Jewelry : Armor);
                    for (var i = 0; i < scale.Rarities.Count; i++)
                    {
                        var rarity = scale.Rarities[i];
                        if (rarity < from)
                            continue;
                        var source = line.Field("source").AsString();
                        var links = ReadSourceLinks(node);
                        var set = Build(lineId, kind, rarity, i, name, templates, other, pieces.Field(kind), scale, links.TryGetValue(source, out var linked) ? linked : source);
                        if (!ids.Add(set.Id))
                            throw line.Field("id").Error($"set id '{set.Id}' is used twice");
                        sets.Add(set);
                    }
                }
            }
        }

        private static SetDef Build(string line, string kind, ItemRarity rarity, int index, ContentNode name, ContentNode templates, ContentNode other, ContentNode slots, Scale scale, string source)
        {
            var pieces = new Dictionary<ItemSlot, IReadOnlyList<Modifier>>();
            IReadOnlyList<Modifier>? secondRing = null;
            foreach (var slotNode in slots.Items())
            {
                var slotName = slotNode.AsString();
                if (slotName == SetDef.SecondRingKey)
                {
                    secondRing = Array.Empty<Modifier>();
                    continue;
                }
                if (!ItemSlotNames.ByName.TryGetValue(slotName, out var slot))
                    throw slotNode.Error($"unknown slot '{slotName}'");
                pieces[slot] = Array.Empty<Modifier>();
            }
            var kindRu = kind == Armor ? "броня" : "бижутерия";
            var kindEn = kind == Armor ? "armour" : "jewelry";
            var set = new SetDef(IdOf(line, kind, rarity), name.Field("ru").AsString() + " — " + kindRu, name.Field("en").AsString() + " — " + kindEn, rarity, null, pieces,
                Array.Empty<Modifier>(), Prefix + line + "_" + kind, null, secondRing)
            {
                PartyLine = line,
                PartyKind = kind,
                PartySource = source,
                AnyWeapon = pieces.ContainsKey(ItemSlot.Weapon),
                PartyBonus = Bonus(rarity, index, templates, other, scale)
            };
            return set;
        }

        private static IReadOnlyList<Modifier> Bonus(ItemRarity rarity, int index, ContentNode templates, ContentNode other, Scale scale)
        {
            var bonus = new List<Modifier>();
            var main = scale.Main[index];
            var mainLine = Template(templates.Field("main"));
            bonus.Add(mainLine.Modifier.WithValue(main));
            if (rarity >= scale.SecondFrom)
                bonus.Add(Template(templates.Field("second")).Modifier.WithValue(main * scale.SecondShare));
            if (rarity >= scale.MoreFrom)
                bonus.Add(Template(templates.Field("more")).Modifier.WithValue(scale.More[index]));
            if (rarity >= scale.MoreTwoFrom && mainLine.Modifier.Kind != ModifierKind.Flat)
                bonus.Add(new Modifier(mainLine.Modifier.Stat, ModifierKind.More, scale.More[index] * scale.MoreTwoShare, mainLine.Modifier.Tags, ModifierPool.General, mainLine.Modifier.When));
            if (rarity >= scale.SpecialFrom)
            {
                var special = Template(templates.Field("special"));
                var step = (int)rarity - (int)scale.SpecialFrom;
                var value = special.Values.Count > 0 ? special.Values[System.Math.Min(step, special.Values.Count - 1)] : main * 0.5;
                bonus.Add(special.Modifier.WithValue(value));
            }
            if (rarity >= scale.SpecialTwoFrom)
                bonus.Add(Template(other.Field("main")).Modifier.WithValue(main * 0.5));
            return bonus;
        }

        private static LineTemplate Template(ContentNode node)
        {
            var tags = node.HasField("tags") ? ModifierReader.ReadTags(node.Field("tags")) : StatTags.None;
            var when = node.HasField("when") ? ModifierReader.ReadCondition(node.Field("when")) : ModifierCondition.Always;
            var values = new List<double>();
            if (node.HasField("values"))
            {
                foreach (var value in node.Field("values").Items())
                    values.Add(value.AsNumber(-10, 10));
            }
            return new LineTemplate(new Modifier(new StatId(node.Field("stat").AsString()), ModifierReader.ReadKind(node.Field("kind")), 0, tags, ModifierPool.General, when), values);
        }

        private static Scale ReadScale(ContentNode node)
        {
            var scale = new Scale();
            foreach (var item in node.Field("rarities").Items())
                scale.Rarities.Add(Rarity(item));
            foreach (var item in node.Field("main").Items())
                scale.Main.Add(item.AsNumber(0, 10));
            foreach (var item in node.Field("more").Items())
                scale.More.Add(item.AsNumber(0, 10));
            if (scale.Main.Count != scale.Rarities.Count || scale.More.Count != scale.Rarities.Count)
                throw node.Field("main").Error("main and more need a value for every rarity");
            scale.SecondShare = node.Field("secondShare").AsNumber(0, 10);
            scale.SecondFrom = Rarity(node.Field("secondFrom"));
            scale.MoreFrom = Rarity(node.Field("moreFrom"));
            scale.MoreTwoShare = node.Field("moreTwoShare").AsNumber(0, 10);
            scale.MoreTwoFrom = Rarity(node.Field("moreTwoFrom"));
            scale.SpecialFrom = Rarity(node.Field("specialFrom"));
            scale.SpecialTwoFrom = Rarity(node.Field("specialTwoFrom"));
            return scale;
        }

        private static ItemRarity Rarity(ContentNode node)
        {
            if (!ItemRarityNames.ByName.TryGetValue(node.AsString(), out var rarity))
                throw node.Error($"unknown rarity '{node.AsString()}'");
            return rarity;
        }
    }
}
