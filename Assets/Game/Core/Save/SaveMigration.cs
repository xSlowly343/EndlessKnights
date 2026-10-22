using System;
using Game.Core.Content.Json;

namespace Game.Core.Save
{
    public sealed class SaveMigration
    {
        public SaveMigration(int fromVersion, string description, Func<JsonValue, JsonValue> upgrade)
        {
            FromVersion = fromVersion;
            Description = description;
            Upgrade = upgrade;
        }

        public int FromVersion { get; }
        public string Description { get; }
        public Func<JsonValue, JsonValue> Upgrade { get; }
    }
}
