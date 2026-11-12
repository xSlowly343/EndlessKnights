using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum Feature
    {
        Artifacts,
        Resonance,
        Spirits,
        Atlas,
        Chronicle,
        SetCraft,
        Dungeons,
        Slates
    }

    public enum UnlockNeedKind
    {
        Stage,
        Prestiges,
        HeroLevel,
        DungeonTier,
        Trials,
        Limits,
        RiftDepth,
        CampTier,
        CampBuilding,
        HideoutTier,
        HideoutBuilding
    }

    public readonly struct UnlockNeed
    {
        public UnlockNeed(UnlockNeedKind kind, string id, long value)
        {
            Kind = kind;
            Id = id;
            Value = value;
        }

        public UnlockNeedKind Kind { get; }
        public string Id { get; }
        public long Value { get; }
    }

    public sealed class UnlockCondition
    {
        public const string AnyOfField = "anyOf";

        public static readonly UnlockCondition None = new UnlockCondition(Array.Empty<UnlockNeed[]>());

        public static readonly IReadOnlyDictionary<string, UnlockNeedKind> KindNames = new Dictionary<string, UnlockNeedKind>(StringComparer.Ordinal)
        {
            ["stage"] = UnlockNeedKind.Stage,
            ["prestiges"] = UnlockNeedKind.Prestiges,
            ["heroLevel"] = UnlockNeedKind.HeroLevel,
            ["dungeonTier"] = UnlockNeedKind.DungeonTier,
            ["trials"] = UnlockNeedKind.Trials,
            ["limits"] = UnlockNeedKind.Limits,
            ["riftDepth"] = UnlockNeedKind.RiftDepth,
            ["campTier"] = UnlockNeedKind.CampTier,
            ["campBuilding"] = UnlockNeedKind.CampBuilding,
            ["hideoutTier"] = UnlockNeedKind.HideoutTier,
            ["hideoutBuilding"] = UnlockNeedKind.HideoutBuilding
        };

        private readonly UnlockNeed[][] _routes;

        private UnlockCondition(UnlockNeed[][] routes)
        {
            _routes = routes;
        }

        public int Routes => _routes.Length;
        public bool Always => _routes.Length == 0;
        public long Stage => Lowest(UnlockNeedKind.Stage);
        public int Prestiges => (int)Lowest(UnlockNeedKind.Prestiges);

        public IReadOnlyList<UnlockNeed> Route(int index) => _routes[index];

        public long Lowest(UnlockNeedKind kind)
        {
            var lowest = long.MaxValue;
            for (var r = 0; r < _routes.Length; r++)
            {
                long value = 0;
                for (var n = 0; n < _routes[r].Length; n++)
                {
                    if (_routes[r][n].Kind == kind)
                        value = System.Math.Max(value, _routes[r][n].Value);
                }
                lowest = System.Math.Min(lowest, value);
            }
            return lowest == long.MaxValue ? 0 : lowest;
        }

        public static UnlockCondition OfStage(long stage) =>
            stage <= 0 ? None : new UnlockCondition(new[] { new[] { new UnlockNeed(UnlockNeedKind.Stage, string.Empty, stage) } });

        public static UnlockCondition Read(ContentNode node)
        {
            var routes = new List<UnlockNeed[]>();
            if (node.HasField(AnyOfField))
            {
                foreach (var name in node.FieldNames())
                {
                    if (name != AnyOfField)
                        throw node.Field(name).Error($"'{AnyOfField}' lists whole routes, move '{name}' into each of them");
                }
                foreach (var item in node.Field(AnyOfField).Items())
                    routes.Add(ReadRoute(item));
                if (routes.Count == 0)
                    throw node.Field(AnyOfField).Error("at least one route is required");
            }
            else
            {
                routes.Add(ReadRoute(node));
            }
            foreach (var route in routes)
            {
                if (route.Length == 0)
                    return None;
            }
            return new UnlockCondition(routes.ToArray());
        }

        private static UnlockNeed[] ReadRoute(ContentNode node)
        {
            var needs = new List<UnlockNeed>();
            foreach (var name in node.FieldNames())
            {
                if (!KindNames.TryGetValue(name, out var kind))
                    throw node.Field(name).Error($"unknown condition '{name}', expected one of: {string.Join(", ", KindNames.Keys)}");
                var field = node.Field(name);
                if (kind == UnlockNeedKind.CampBuilding || kind == UnlockNeedKind.HideoutBuilding)
                {
                    foreach (var building in field.FieldNames())
                        needs.Add(new UnlockNeed(kind, building, field.Field(building).AsLong(1)));
                }
                else
                {
                    var value = field.AsLong(0);
                    if (value > 0)
                        needs.Add(new UnlockNeed(kind, string.Empty, value));
                }
            }
            return needs.ToArray();
        }
    }

    public sealed class UnlockRules
    {
        public const string FileName = "unlocks.json";
        public const int SupportedVersion = 1;

        public static readonly IReadOnlyDictionary<string, Feature> FeatureNames = new Dictionary<string, Feature>(StringComparer.Ordinal)
        {
            ["artifacts"] = Feature.Artifacts,
            ["resonance"] = Feature.Resonance,
            ["spirits"] = Feature.Spirits,
            ["atlas"] = Feature.Atlas,
            ["chronicle"] = Feature.Chronicle,
            ["setCraft"] = Feature.SetCraft,
            ["dungeons"] = Feature.Dungeons,
            ["slates"] = Feature.Slates
        };

        public static readonly int FeatureCount = Enum.GetValues(typeof(Feature)).Length;

        private readonly UnlockCondition[] _features;
        private readonly long[] _artifactGroups;

        private UnlockRules(UnlockCondition[] features, long[] artifactGroups)
        {
            _features = features;
            _artifactGroups = artifactGroups;
        }

        public static UnlockRules AllOpen => new UnlockRules(NoConditions(), Array.Empty<long>());

        public UnlockCondition For(Feature feature) => _features[(int)feature];

        public long ArtifactGroupStage(int group) => group >= 1 && group <= _artifactGroups.Length ? _artifactGroups[group - 1] : 0;

        public void CheckBuildings(CampRules camp, HideoutRules hideout)
        {
            foreach (var condition in _features)
            {
                for (var r = 0; r < condition.Routes; r++)
                {
                    foreach (var need in condition.Route(r))
                    {
                        if (need.Kind == UnlockNeedKind.CampBuilding && camp.Building(need.Id) == null)
                            throw new ContentException(FileName, "features", $"unknown camp building '{need.Id}'");
                        if (need.Kind == UnlockNeedKind.HideoutBuilding && hideout.Building(need.Id) == null)
                            throw new ContentException(FileName, "features", $"unknown hideout building '{need.Id}'");
                    }
                }
            }
        }

        public static UnlockRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var features = NoConditions();
            var node = root.Field("features");
            foreach (var name in node.FieldNames())
            {
                if (!FeatureNames.TryGetValue(name, out var feature))
                    throw node.Field(name).Error($"unknown feature '{name}'");
                features[(int)feature] = UnlockCondition.Read(node.Field(name));
            }
            var groups = new List<long>();
            foreach (var item in root.Field("artifactGroups").Items())
                groups.Add(item.AsLong(0));
            return new UnlockRules(features, groups.ToArray());
        }

        private static UnlockCondition[] NoConditions()
        {
            var features = new UnlockCondition[FeatureCount];
            for (var i = 0; i < features.Length; i++)
                features[i] = UnlockCondition.None;
            return features;
        }
    }
}
