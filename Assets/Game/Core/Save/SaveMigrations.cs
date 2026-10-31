using System;
using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Core.Save
{
    public sealed class SaveMigrations
    {
        public static readonly SaveMigrations Default = new SaveMigrations(15,
            new SaveMigration(1, "skill slots and chain damage (phase 8)", payload => JsonEdit.Update(payload, "state", StateToVersion2)),
            new SaveMigration(2, "passive tree (phase 9)", payload => JsonEdit.Update(payload, "state", StateToVersion3)),
            new SaveMigration(3, "hero level, record stage, owned gems (phases 9-10)", payload => JsonEdit.Update(payload, "state", StateToVersion4)),
            new SaveMigration(4, "prestige and artifacts (phase 10)", payload => JsonEdit.Update(payload, "state", StateToVersion5)),
            new SaveMigration(5, "two build presets and input stats (phases 10-11)", payload => JsonEdit.Update(payload, "state", StateToVersion6)),
            new SaveMigration(6, "ascendancy subclasses", payload => JsonEdit.Update(payload, "state", StateToVersion7)),
            new SaveMigration(7, "strike upgrades for gold (10.2)", payload => JsonEdit.Update(payload, "state", StateToVersion8)),
            new SaveMigration(8, "nine RPG classes and the large passive tree", payload => JsonEdit.Update(payload, "state", StateToVersion9)),
            new SaveMigration(9, "fourteen characters, class resources and the new passive tree", payload => JsonEdit.Update(payload, "state", StateToVersion10)),
            new SaveMigration(10, "forty-two ascendancy paths of the fourteen characters", payload => JsonEdit.Update(payload, "state", StateToVersion11)),
            new SaveMigration(11, "class trees of fifty ranked nodes", payload => JsonEdit.Update(payload, "state", StateToVersion12)),
            new SaveMigration(12, "two slots for common trees", payload => JsonEdit.Update(payload, "state", StateToVersion13)),
            new SaveMigration(13, "the old passive tree and its jewels are gone", payload => JsonEdit.Update(payload, "state", StateToVersion14)),
            new SaveMigration(14, "one class attribute instead of three", payload => JsonEdit.Update(payload, "state", WithClassAttributeAffixes)));

        private static readonly IReadOnlyDictionary<string, string> RenamedClasses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["typist"] = "swordsman",
            ["hermit"] = "wanderer"
        };

        private static readonly IReadOnlyDictionary<string, string> Characters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["barbarian"] = "viking",
            ["swordsman"] = "ronin",
            ["ranger"] = "amazon",
            ["sorcerer"] = "stormweaver",
            ["wanderer"] = "nomad",
            ["bard"] = "metalhead",
            ["priest"] = "oracle",
            ["witch"] = "voodoo",
            ["alchemist"] = "plague_doctor"
        };

        private static readonly IReadOnlyDictionary<string, string> AttributeAffixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["armor_strength"] = "armor_attribute",
            ["armor_dexterity"] = "armor_attribute",
            ["armor_intelligence"] = "armor_attribute",
            ["jewel_strength"] = "jewel_attribute",
            ["jewel_dexterity"] = "jewel_attribute",
            ["jewel_intelligence"] = "jewel_attribute",
            ["amulet_all_attributes"] = "jewel_attribute"
        };

        private static readonly IReadOnlyDictionary<string, string> RenamedSubclasses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plague_doctor"] = "plague_bringer"
        };

        private static readonly HashSet<string> KeptSubclasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "berserker", "hermit", "conductor", "seer", "bombardier"
        };

        private readonly SaveMigration[] _steps;

        public SaveMigrations(int currentVersion, params SaveMigration[] steps)
        {
            if (currentVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(currentVersion), currentVersion, "Save versions start at 1.");
            if (steps.Length != currentVersion - 1)
                throw new ArgumentException($"Version {currentVersion} needs {currentVersion - 1} migration steps, got {steps.Length}.", nameof(steps));
            var ordered = new SaveMigration[steps.Length];
            foreach (var step in steps)
            {
                var index = step.FromVersion - 1;
                if (index < 0 || index >= ordered.Length || ordered[index] != null)
                    throw new ArgumentException($"Migration from version {step.FromVersion} is out of range or repeated.", nameof(steps));
                ordered[index] = step;
            }
            CurrentVersion = currentVersion;
            _steps = ordered;
        }

        public int CurrentVersion { get; }

        public IReadOnlyList<SaveMigration> Steps => _steps;

        public JsonValue Upgrade(JsonValue payload, int fromVersion)
        {
            var upgraded = payload;
            for (var version = fromVersion; version < CurrentVersion; version++)
                upgraded = JsonEdit.Set(_steps[version - 1].Upgrade(upgraded), "version", JsonValue.Of(version + 1));
            return upgraded;
        }

        private static JsonValue StateToVersion2(JsonValue state) =>
            JsonEdit.Set(JsonEdit.Set(state, "skills", JsonValue.Array(new JsonValue[0])), "chainDamage", JsonValue.Of("0e0"));

        private static JsonValue StateToVersion3(JsonValue state)
        {
            var tree = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["startClass"] = state.Fields["hero"].Fields["class"],
                ["pointsEarned"] = JsonValue.Of(0),
                ["allocated"] = JsonValue.Array(new JsonValue[0])
            });
            var withTree = JsonEdit.Set(state, "tree", tree);
            return JsonEdit.Update(withTree, "hero", hero => JsonEdit.Set(hero, "keyHits", JsonValue.Of(0)));
        }

        private static JsonValue StateToVersion4(JsonValue state)
        {
            var hero = JsonEdit.Set(JsonEdit.Set(state.Fields["hero"], "level", JsonValue.Of(1)), "xp", JsonValue.Of(0));
            var stage = JsonEdit.Set(state.Fields["stage"], "record", state.Fields["stage"].Fields["highest"]);
            var tree = JsonEdit.Set(state.Fields["tree"], "lastFreeResetUnixMs", JsonValue.Of(0));
            var upgraded = JsonEdit.Set(JsonEdit.Set(JsonEdit.Set(state, "hero", hero), "stage", stage), "tree", tree);
            return JsonEdit.Set(upgraded, "ownedGems", JsonValue.Array(EquippedGems(state.Fields["skills"])));
        }

        private static JsonValue StateToVersion5(JsonValue state)
        {
            var prestige = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["echo"] = JsonValue.Of("0e0"),
                ["echoEarned"] = JsonValue.Of("0e0"),
                ["count"] = JsonValue.Of(0)
            });
            return JsonEdit.Set(JsonEdit.Set(state, "prestige", prestige), "artifacts", JsonValue.Array(new JsonValue[0]));
        }

        private static JsonValue StateToVersion6(JsonValue state)
        {
            var otherTree = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["startClass"] = state.Fields["tree"].Fields["startClass"],
                ["pointsEarned"] = state.Fields["tree"].Fields["pointsEarned"],
                ["lastFreeResetUnixMs"] = JsonValue.Of(0),
                ["allocated"] = JsonValue.Array(new JsonValue[0])
            });
            var presets = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["active"] = JsonValue.Of(0),
                ["otherTree"] = otherTree,
                ["otherSkills"] = JsonValue.Array(new JsonValue[0])
            });
            var inputStats = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["keys"] = JsonValue.Of("0"),
                ["leftClicks"] = JsonValue.Of("0"),
                ["rightClicks"] = JsonValue.Of("0"),
                ["middleClicks"] = JsonValue.Of("0"),
                ["mouseMeters"] = JsonValue.Of(0)
            });
            return JsonEdit.Set(JsonEdit.Set(state, "presets", presets), "inputStats", inputStats);
        }

        private static JsonValue StateToVersion7(JsonValue state)
        {
            var ascendancy = JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["subclass"] = JsonValue.Null(),
                ["lastFreeChangeUnixMs"] = JsonValue.Of(0),
                ["allocated"] = JsonValue.Array(new JsonValue[0]),
                ["activities"] = JsonValue.Array(new JsonValue[0])
            });
            return JsonEdit.Set(state, "ascendancy", ascendancy);
        }

        private static JsonValue StateToVersion8(JsonValue state) =>
            JsonEdit.Update(state, "hero", hero => JsonEdit.Set(hero, "strikeLevel", JsonValue.Of(0)));

        private static JsonValue StateToVersion9(JsonValue state)
        {
            var hero = JsonEdit.Update(state, "hero", h => JsonEdit.Update(h, "class", RenamedClass));
            var tree = JsonEdit.Update(hero, "tree", EmptyTreeOfRenamedClass);
            var presets = JsonEdit.Update(tree, "presets", p => JsonEdit.Update(p, "otherTree", EmptyTreeOfRenamedClass));
            return JsonEdit.Update(presets, "ascendancy", a =>
                JsonEdit.Set(JsonEdit.Set(a, "subclass", JsonValue.Null()), "allocated", JsonValue.Array(new JsonValue[0])));
        }

        private static JsonValue StateToVersion10(JsonValue state)
        {
            var hero = JsonEdit.Update(state, "hero", ToCharacterHero);
            var tree = JsonEdit.Update(hero, "tree", EmptyTreeOfCharacter);
            var presets = JsonEdit.Update(tree, "presets", p => JsonEdit.Update(p, "otherTree", EmptyTreeOfCharacter));
            var carry = JsonEdit.Update(presets, "carry", c => JsonEdit.Remove(c, "clicks"));
            return JsonEdit.Update(carry, "ascendancy", RenamedSubclass);
        }

        private static JsonValue StateToVersion12(JsonValue state)
        {
            var heroClass = state.Fields["hero"].Fields["class"].Text;
            var withTree = JsonEdit.Set(state, "classTree", EmptyClassTree(heroClass));
            return JsonEdit.Update(withTree, "presets", p => JsonEdit.Set(p, "otherClassTree", EmptyClassTree(heroClass)));
        }

        private static JsonValue StateToVersion13(JsonValue state)
        {
            var withSlots = JsonEdit.Set(state, "commonTrees", JsonValue.Array(new JsonValue[0]));
            return JsonEdit.Update(withSlots, "presets", p => JsonEdit.Set(p, "otherCommonTrees", JsonValue.Array(new JsonValue[0])));
        }

        private static JsonValue StateToVersion14(JsonValue state)
        {
            var points = state.Fields["tree"].Fields["pointsEarned"];
            var withoutTree = JsonEdit.Remove(JsonEdit.Set(state, "treePointsEarned", points), "tree");
            var withoutJewels = JsonEdit.Remove(withoutTree, "jewels");
            return JsonEdit.Update(withoutJewels, "presets", p => JsonEdit.Remove(p, "otherTree"));
        }

        private static JsonValue WithClassAttributeAffixes(JsonValue value)
        {
            if (value.Kind == JsonKind.Array)
            {
                var items = new List<JsonValue>(value.Items.Count);
                foreach (var item in value.Items)
                    items.Add(WithClassAttributeAffixes(item));
                return JsonValue.Array(items);
            }
            if (value.Kind != JsonKind.Object)
                return value;
            var fields = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            foreach (var pair in value.Fields)
                fields[pair.Key] = pair.Key == "affixes" ? AttributeAffixesOf(pair.Value) : WithClassAttributeAffixes(pair.Value);
            return JsonValue.Object(fields);
        }

        private static JsonValue AttributeAffixesOf(JsonValue affixes)
        {
            if (affixes.Kind != JsonKind.Array)
                return affixes;
            var items = new List<JsonValue>(affixes.Items.Count);
            var attributeTaken = false;
            foreach (var affix in affixes.Items)
            {
                if (affix.Kind != JsonKind.Object || !affix.Fields.TryGetValue("id", out var id) || id.Kind != JsonKind.String || !AttributeAffixes.TryGetValue(id.Text, out var renamed))
                {
                    items.Add(affix);
                    continue;
                }
                if (attributeTaken)
                    continue;
                attributeTaken = true;
                items.Add(JsonEdit.Set(affix, "id", JsonValue.Of(renamed)));
            }
            return JsonValue.Array(items);
        }

        private static JsonValue EmptyClassTree(string heroClass) =>
            JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["class"] = JsonValue.Of(heroClass),
                ["ranks"] = JsonValue.Array(new JsonValue[0]),
                ["majors"] = JsonValue.Array(new JsonValue[0]),
                ["plan"] = JsonValue.Array(new JsonValue[0])
            });

        private static JsonValue StateToVersion11(JsonValue state) =>
            JsonEdit.Update(state, "ascendancy", KeptSubclassWithoutNodes);

        private static JsonValue KeptSubclassWithoutNodes(JsonValue ascendancy)
        {
            var subclass = ascendancy.Fields["subclass"];
            var kept = subclass.Kind == JsonKind.String && KeptSubclasses.Contains(subclass.Text) ? subclass : JsonValue.Null();
            return JsonEdit.Set(JsonEdit.Set(ascendancy, "subclass", kept), "allocated", JsonValue.Array(new JsonValue[0]));
        }

        private static JsonValue RenamedSubclass(JsonValue ascendancy)
        {
            var subclass = ascendancy.Fields["subclass"];
            if (subclass.Kind != JsonKind.String || !RenamedSubclasses.TryGetValue(subclass.Text, out var renamed))
                return ascendancy;
            var allocated = new List<JsonValue>();
            foreach (var id in ascendancy.Fields["allocated"].Items)
                allocated.Add(id.Text.StartsWith(subclass.Text + "_", StringComparison.Ordinal) ? JsonValue.Of(renamed + id.Text.Substring(subclass.Text.Length)) : id);
            return JsonEdit.Set(JsonEdit.Set(ascendancy, "subclass", JsonValue.Of(renamed)), "allocated", JsonValue.Array(allocated));
        }

        private static JsonValue ToCharacterHero(JsonValue hero)
        {
            var renamed = JsonEdit.Update(hero, "class", Character);
            var resource = JsonEdit.Set(renamed, "resource", hero.Fields["energy"]);
            return JsonEdit.Remove(JsonEdit.Remove(JsonEdit.Set(resource, "keylessTicks", JsonValue.Of(0)), "energy"), "notes");
        }

        private static JsonValue Character(JsonValue name) =>
            Characters.TryGetValue(name.Text, out var character) ? JsonValue.Of(character) : name;

        private static JsonValue EmptyTreeOfCharacter(JsonValue tree) =>
            JsonEdit.Set(JsonEdit.Update(tree, "startClass", Character), "allocated", JsonValue.Array(new JsonValue[0]));

        private static JsonValue RenamedClass(JsonValue name) =>
            RenamedClasses.TryGetValue(name.Text, out var renamed) ? JsonValue.Of(renamed) : name;

        private static JsonValue EmptyTreeOfRenamedClass(JsonValue tree) =>
            JsonEdit.Set(JsonEdit.Update(tree, "startClass", RenamedClass), "allocated", JsonValue.Array(new JsonValue[0]));

        private static JsonValue[] EquippedGems(JsonValue skills)
        {
            var owned = new List<JsonValue>();
            foreach (var slot in skills.Items)
                owned.Add(slot.Fields["gem"]);
            return owned.ToArray();
        }
    }
}
