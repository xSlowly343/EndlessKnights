using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ResourceRules
    {
        private readonly Dictionary<string, ClassResource> _classes;

        public ResourceRules(double max, ClassResource fallback, Dictionary<string, ClassResource> classes)
        {
            Max = max;
            Fallback = fallback;
            _classes = classes;
        }

        public double Max { get; }
        public ClassResource Fallback { get; }
        public IReadOnlyCollection<string> Classes => _classes.Keys;

        public ClassResource For(string className) => _classes.TryGetValue(className, out var resource) ? resource : Fallback;

        public static ResourceRules Read(ContentNode node)
        {
            var classes = new Dictionary<string, ClassResource>(StringComparer.Ordinal);
            var classesNode = node.Field("classes");
            foreach (var name in classesNode.FieldNames())
                classes[name] = ClassResource.Read(classesNode.Field(name));
            return new ResourceRules(node.Field("max").AsNumber(1, 1e6), ClassResource.Read(node.Field("default")), classes);
        }
    }

    public sealed class ClassResource
    {
        public ClassResource(double perKey, double mousePxPerPoint, double decayPerSecond, double decayAfterKeylessSeconds, bool mouseHoldsDecay)
        {
            PerKey = perKey;
            MousePxPerPoint = mousePxPerPoint;
            DecayPerSecond = decayPerSecond;
            DecayAfterKeylessSeconds = decayAfterKeylessSeconds;
            MouseHoldsDecay = mouseHoldsDecay;
        }

        public double PerKey { get; }
        public double MousePxPerPoint { get; }
        public double DecayPerSecond { get; }
        public double DecayAfterKeylessSeconds { get; }
        public bool MouseHoldsDecay { get; }

        public static ClassResource Read(ContentNode node)
        {
            var perKey = node.HasField("perKey") ? node.Field("perKey").AsNumber(0, 1000) : 0;
            var mousePxPerPoint = node.HasField("mousePxPerPoint") ? node.Field("mousePxPerPoint").AsNumber(1, 1e6) : 0;
            if (!node.HasField("decay"))
                return new ClassResource(perKey, mousePxPerPoint, 0, 0, false);
            var decay = node.Field("decay");
            return new ClassResource(
                perKey,
                mousePxPerPoint,
                decay.Field("perSecond").AsNumber(0, 1e6),
                decay.Field("afterKeylessSeconds").AsNumber(0, 3600),
                decay.HasField("mouseHolds") && decay.Field("mouseHolds").AsBool());
        }
    }
}
