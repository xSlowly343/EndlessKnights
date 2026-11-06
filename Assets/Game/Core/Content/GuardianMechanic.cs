using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum GuardianMechanicKind
    {
        Shield,
        Wet,
        Rhythm,
        Silence
    }

    public sealed class GuardianMechanic
    {
        private static readonly IReadOnlyDictionary<string, GuardianMechanicKind> Kinds = new Dictionary<string, GuardianMechanicKind>(StringComparer.Ordinal)
        {
            ["shield"] = GuardianMechanicKind.Shield,
            ["wet"] = GuardianMechanicKind.Wet,
            ["rhythm"] = GuardianMechanicKind.Rhythm,
            ["silence"] = GuardianMechanicKind.Silence
        };

        public GuardianMechanic(GuardianMechanicKind kind, double value)
        {
            Kind = kind;
            Value = value;
        }

        public GuardianMechanicKind Kind { get; }
        public double Value { get; }

        public static GuardianMechanic Read(ContentNode node)
        {
            var name = node.Field("kind").AsString();
            if (!Kinds.TryGetValue(name, out var kind))
                throw node.Field("kind").Error($"unknown guardian mechanic '{name}', expected shield, wet, rhythm or silence");
            var field = kind switch
            {
                GuardianMechanicKind.Shield => "share",
                GuardianMechanicKind.Rhythm => "meters",
                _ => "seconds"
            };
            var value = kind == GuardianMechanicKind.Shield ? node.Field(field).AsNumber(0.01, 0.99) : node.Field(field).AsNumber(0.1, 1e6);
            return new GuardianMechanic(kind, value);
        }
    }
}
