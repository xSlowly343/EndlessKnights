using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ResourceRules
    {
        private readonly Dictionary<string, ClassResource> _classes;

        public ResourceRules(double max, ClassResource fallback, Dictionary<string, ClassResource> classes, double allyHitStatSeconds = 0)
        {
            Max = max;
            Fallback = fallback;
            AllyHitStatSeconds = allyHitStatSeconds;
            _classes = classes;
        }

        public double Max { get; }
        public double AllyHitStatSeconds { get; }
        public ClassResource Fallback { get; }
        public IReadOnlyCollection<string> Classes => _classes.Keys;

        public ClassResource For(string className) => _classes.TryGetValue(className, out var resource) ? resource : Fallback;

        public static ResourceRules Read(ContentNode node)
        {
            var classes = new Dictionary<string, ClassResource>(StringComparer.Ordinal);
            var classesNode = node.Field("classes");
            foreach (var name in classesNode.FieldNames())
                classes[name] = ClassResource.Read(classesNode.Field(name));
            return new ResourceRules(node.Field("max").AsNumber(1, 1e6), ClassResource.Read(node.Field("default")), classes,
                node.HasField("allyHitStatSeconds") ? node.Field("allyHitStatSeconds").AsNumber(0, 3600) : 0);
        }
    }

    public sealed class ClassResource
    {
        public ClassResource(double perKey, double mousePxPerPoint, double decayPerSecond, double decayAfterKeylessSeconds, bool mouseHoldsDecay, double perSecond = 0, double perFastKillSecond = 0, double inverseDecayPerSecond = 0, double overheatSeconds = 0, bool inverse = false, double perKill = 0, double overloadMultiplier = 0, double perClick = 0, double readingPerSecond = 0, double readingAfterKeylessSeconds = 0)
        {
            ReadingPerSecond = readingPerSecond;
            ReadingAfterKeylessSeconds = readingAfterKeylessSeconds;
            PerClick = perClick;
            OverloadMultiplier = overloadMultiplier;
            PerKill = perKill;
            Inverse = inverse;
            InverseDecayPerSecond = inverseDecayPerSecond;
            OverheatSeconds = overheatSeconds;
            PerKey = perKey;
            PerFastKillSecond = perFastKillSecond;
            PerSecond = perSecond;
            MousePxPerPoint = mousePxPerPoint;
            DecayPerSecond = decayPerSecond;
            DecayAfterKeylessSeconds = decayAfterKeylessSeconds;
            MouseHoldsDecay = mouseHoldsDecay;
        }

        public double PerKey { get; }
        public double PerSecond { get; }
        public double PerFastKillSecond { get; }
        public double PerKill { get; }
        public double PerClick { get; }
        public double ReadingPerSecond { get; }
        public double ReadingAfterKeylessSeconds { get; }
        public double OverloadMultiplier { get; }
        public bool Inverse { get; }
        public double InverseDecayPerSecond { get; }
        public double OverheatSeconds { get; }
        public double MousePxPerPoint { get; }
        public double DecayPerSecond { get; }
        public double DecayAfterKeylessSeconds { get; }
        public bool MouseHoldsDecay { get; }
        public double PerAllyHit { get; private set; }
        public double PerCircle { get; private set; }

        public static ClassResource Read(ContentNode node)
        {
            var resource = ReadBase(node);
            resource.PerAllyHit = node.HasField("perAllyHit") ? node.Field("perAllyHit").AsNumber(0, 1000) : 0;
            resource.PerCircle = node.HasField("perCircle") ? node.Field("perCircle").AsNumber(0, 1000) : 0;
            return resource;
        }

        private static ClassResource ReadBase(ContentNode node)
        {
            var perKey = node.HasField("perKey") ? node.Field("perKey").AsNumber(0, 1000) : 0;
            var mousePxPerPoint = node.HasField("mousePxPerPoint") ? node.Field("mousePxPerPoint").AsNumber(1, 1e6) : 0;
            var perSecond = node.HasField("perSecond") ? node.Field("perSecond").AsNumber(0, 1000) : 0;
            var perFastKillSecond = node.HasField("perFastKillSecond") ? node.Field("perFastKillSecond").AsNumber(0, 1000) : 0;
            var perKill = node.HasField("perKill") ? node.Field("perKill").AsNumber(0, 1000) : 0;
            var perClick = node.HasField("perClick") ? node.Field("perClick").AsNumber(0, 1000) : 0;
            var readingPerSecond = node.HasField("reading") ? node.Field("reading").Field("perSecond").AsNumber(0, 1000) : 0;
            var readingAfter = node.HasField("reading") ? node.Field("reading").Field("afterKeylessSeconds").AsNumber(0, 3600) : 0;
            var overload = node.HasField("overload") ? node.Field("overload").Field("multiplier").AsNumber(0, 1e6) : 0;
            if (node.HasField("inverse"))
            {
                if (node.HasField("decay"))
                    throw node.Field("decay").Error("an inverse resource decays through inverse.decayPerSecond");
                var inverse = node.Field("inverse");
                return new ClassResource(perKey, mousePxPerPoint, 0, 0, false, perSecond, perFastKillSecond,
                    inverse.Field("decayPerSecond").AsNumber(0, 1e6), inverse.Field("overheatSeconds").AsNumber(0, 3600), true, perKill, perClick: perClick);
            }
            if (!node.HasField("decay"))
                return new ClassResource(perKey, mousePxPerPoint, 0, 0, false, perSecond, perFastKillSecond, perKill: perKill, overloadMultiplier: overload, perClick: perClick, readingPerSecond: readingPerSecond, readingAfterKeylessSeconds: readingAfter);
            var decay = node.Field("decay");
            return new ClassResource(
                perKey,
                mousePxPerPoint,
                decay.Field("perSecond").AsNumber(0, 1e6),
                decay.Field("afterKeylessSeconds").AsNumber(0, 3600),
                decay.HasField("mouseHolds") && decay.Field("mouseHolds").AsBool(),
                perSecond,
                perFastKillSecond,
                perKill: perKill,
                overloadMultiplier: overload,
                perClick: perClick,
                readingPerSecond: readingPerSecond,
                readingAfterKeylessSeconds: readingAfter);
        }
    }
}
