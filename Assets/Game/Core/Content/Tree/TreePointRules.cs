using System;
using System.Collections.Generic;

namespace Game.Core.Content.Tree
{
    public enum PointCounter
    {
        Keys,
        Clicks,
        MouseKm,
        Prestiges,
        RecordStage,
        Trials,
        Gates,
        CoopHelp,
        IdleHours
    }

    public sealed class PointLadder
    {
        public PointLadder(string id, PointCounter counter, IReadOnlyList<double> steps)
        {
            Id = id;
            Counter = counter;
            Steps = steps;
        }

        public string Id { get; }
        public PointCounter Counter { get; }
        public IReadOnlyList<double> Steps { get; }

        public int PointsAt(double value)
        {
            var points = 0;
            for (var i = 0; i < Steps.Count && value >= Steps[i]; i++)
                points++;
            return points;
        }
    }

    public sealed class TreePointRules
    {
        public const string FileName = "tree_points.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, PointCounter> Counters = new Dictionary<string, PointCounter>(StringComparer.Ordinal)
        {
            ["keys"] = PointCounter.Keys,
            ["clicks"] = PointCounter.Clicks,
            ["mouseKm"] = PointCounter.MouseKm,
            ["prestiges"] = PointCounter.Prestiges,
            ["recordStage"] = PointCounter.RecordStage,
            ["trials"] = PointCounter.Trials,
            ["gates"] = PointCounter.Gates,
            ["coopHelp"] = PointCounter.CoopHelp,
            ["idleHours"] = PointCounter.IdleHours
        };

        private TreePointRules(IReadOnlyList<PointLadder> ladders, double idleAfterSeconds, int classChoiceLevel)
        {
            Ladders = ladders;
            IdleAfterSeconds = idleAfterSeconds;
            ClassChoiceLevel = classChoiceLevel;
        }

        public double IdleAfterSeconds { get; }
        public int ClassChoiceLevel { get; }

        public IReadOnlyList<PointLadder> Ladders { get; }

        public int MaxPoints
        {
            get
            {
                var points = 0;
                foreach (var ladder in Ladders)
                    points += ladder.Steps.Count;
                return points;
            }
        }

        public static TreePointRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var ladders = new List<PointLadder>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("ladders").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"ladder id '{id}' is used twice");
                var counterNode = item.Field("counter");
                if (!Counters.TryGetValue(counterNode.AsString(), out var counter))
                    throw counterNode.Error($"unknown counter '{counterNode.AsString()}', expected keys, clicks, mouseKm, prestiges, recordStage, trials, gates, coopHelp or idleHours");
                ladders.Add(new PointLadder(id, counter, ReadSteps(item.Field("steps"))));
            }
            return new TreePointRules(ladders, root.Field("idleAfterSeconds").AsNumber(0, 3600), root.Field("classChoiceLevel").AsInt(1, 100));
        }

        private static List<double> ReadSteps(ContentNode node)
        {
            var steps = new List<double>();
            foreach (var item in node.Items())
            {
                var step = item.AsNumber(0, 1e18);
                if (steps.Count > 0 && step <= steps[steps.Count - 1])
                    throw item.Error("steps must grow");
                steps.Add(step);
            }
            if (steps.Count == 0)
                throw node.Error("a ladder needs at least one step");
            return steps;
        }
    }
}
