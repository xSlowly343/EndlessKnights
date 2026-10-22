using System;
using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Core.Save
{
    public sealed class SaveMigrations
    {
        public static readonly SaveMigrations Default = new SaveMigrations(3,
            new SaveMigration(1, "skill slots and chain damage (phase 8)", payload =>
                JsonEdit.Update(payload, "state", state =>
                    JsonEdit.Set(JsonEdit.Set(state, "skills", JsonValue.Array(new JsonValue[0])), "chainDamage", JsonValue.Of("0e0")))),
            new SaveMigration(2, "passive tree (phase 9)", payload =>
                JsonEdit.Update(payload, "state", state =>
                    JsonEdit.Set(state, "tree", EmptyTree(state.Fields["hero"].Fields["class"])))));

        private static JsonValue EmptyTree(JsonValue heroClass) =>
            JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["startClass"] = heroClass,
                ["pointsEarned"] = JsonValue.Of(0),
                ["allocated"] = JsonValue.Array(new JsonValue[0])
            });

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
    }
}
