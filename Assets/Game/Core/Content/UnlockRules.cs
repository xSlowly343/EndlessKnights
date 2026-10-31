using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum Feature
    {
        Artifacts,
        Cutting,
        Resonance,
        Spirits,
        Atlas,
        Chronicle,
        SetCraft,
        Dungeons
    }

    public readonly struct UnlockCondition
    {
        public UnlockCondition(long stage, int prestiges)
        {
            Stage = stage;
            Prestiges = prestiges;
        }

        public long Stage { get; }
        public int Prestiges { get; }
    }

    public sealed class UnlockRules
    {
        public const string FileName = "unlocks.json";
        public const int SupportedVersion = 1;

        public static readonly IReadOnlyDictionary<string, Feature> FeatureNames = new Dictionary<string, Feature>(StringComparer.Ordinal)
        {
            ["artifacts"] = Feature.Artifacts,
            ["cutting"] = Feature.Cutting,
            ["resonance"] = Feature.Resonance,
            ["spirits"] = Feature.Spirits,
            ["atlas"] = Feature.Atlas,
            ["chronicle"] = Feature.Chronicle,
            ["setCraft"] = Feature.SetCraft,
            ["dungeons"] = Feature.Dungeons
        };

        private readonly UnlockCondition[] _features;
        private readonly long[] _artifactGroups;

        private UnlockRules(UnlockCondition[] features, long[] artifactGroups)
        {
            _features = features;
            _artifactGroups = artifactGroups;
        }

        public static UnlockRules AllOpen => new UnlockRules(new UnlockCondition[Enum.GetValues(typeof(Feature)).Length], Array.Empty<long>());

        public UnlockCondition For(Feature feature) => _features[(int)feature];

        public long ArtifactGroupStage(int group) => group >= 1 && group <= _artifactGroups.Length ? _artifactGroups[group - 1] : 0;

        public static UnlockRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var features = new UnlockCondition[Enum.GetValues(typeof(Feature)).Length];
            var node = root.Field("features");
            foreach (var name in node.FieldNames())
            {
                if (!FeatureNames.TryGetValue(name, out var feature))
                    throw node.Field(name).Error($"unknown feature '{name}'");
                var item = node.Field(name);
                features[(int)feature] = new UnlockCondition(
                    item.HasField("stage") ? item.Field("stage").AsLong(0) : 0,
                    item.HasField("prestiges") ? item.Field("prestiges").AsInt(0) : 0);
            }
            var groups = new List<long>();
            foreach (var item in root.Field("artifactGroups").Items())
                groups.Add(item.AsLong(0));
            return new UnlockRules(features, groups.ToArray());
        }
    }
}
