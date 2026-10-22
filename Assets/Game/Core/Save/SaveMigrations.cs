using System;
using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Core.Save
{
    public sealed class SaveMigrations
    {
        public static readonly SaveMigrations Default = new SaveMigrations(7,
            new SaveMigration(1, "skill slots and chain damage (phase 8)", payload => JsonEdit.Update(payload, "state", StateToVersion2)),
            new SaveMigration(2, "passive tree (phase 9)", payload => JsonEdit.Update(payload, "state", StateToVersion3)),
            new SaveMigration(3, "hero level, record stage, owned gems (phases 9-10)", payload => JsonEdit.Update(payload, "state", StateToVersion4)),
            new SaveMigration(4, "prestige and artifacts (phase 10)", payload => JsonEdit.Update(payload, "state", StateToVersion5)),
            new SaveMigration(5, "two build presets and input stats (phases 10-11)", payload => JsonEdit.Update(payload, "state", StateToVersion6)),
            new SaveMigration(6, "ascendancy subclasses", payload => JsonEdit.Update(payload, "state", StateToVersion7)));

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

        private static JsonValue[] EquippedGems(JsonValue skills)
        {
            var owned = new List<JsonValue>();
            foreach (var slot in skills.Items)
            {
                owned.Add(slot.Fields["gem"]);
                if (slot.Fields["trigger"].Kind == JsonKind.String)
                    owned.Add(slot.Fields["trigger"]);
            }
            return owned.ToArray();
        }
    }
}
