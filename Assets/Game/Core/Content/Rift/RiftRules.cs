using System;
using System.Collections.Generic;
using Game.Core.Content.Synergy;
using Game.Core.Stats;

namespace Game.Core.Content.Rift
{
    public sealed class RiftRules
    {
        public const string FileName = "rift.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, DayOfWeek> Days = new Dictionary<string, DayOfWeek>(StringComparer.Ordinal)
        {
            ["monday"] = DayOfWeek.Monday,
            ["tuesday"] = DayOfWeek.Tuesday,
            ["wednesday"] = DayOfWeek.Wednesday,
            ["thursday"] = DayOfWeek.Thursday,
            ["friday"] = DayOfWeek.Friday,
            ["saturday"] = DayOfWeek.Saturday,
            ["sunday"] = DayOfWeek.Sunday
        };

        private RiftRules(RiftSchedule schedule, double hpMultiplier, int wallPeriod, int wallZoneLength, double wallRate, double bossTimerSeconds,
            long bossShards, long guardianShards, ExponentialCurve unlockCost, ExponentialCurve levelCost, int modifiersPerWindow,
            IReadOnlyList<RiftModifier> modifiers, long partyJoinMs, long partyDurationMs, RiftBoardRules board, RiftMilestoneRules milestones)
        {
            Milestones = milestones;
            PartyJoinMs = partyJoinMs;
            Board = board;
            PartyDurationMs = partyDurationMs;
            Schedule = schedule;
            HpMultiplier = hpMultiplier;
            WallPeriod = wallPeriod;
            WallZoneLength = wallZoneLength;
            WallRate = wallRate;
            BossTimerSeconds = bossTimerSeconds;
            BossShards = bossShards;
            GuardianShards = guardianShards;
            UnlockCost = unlockCost;
            LevelCost = levelCost;
            ModifiersPerWindow = modifiersPerWindow;
            Modifiers = modifiers;
        }

        public RiftSchedule Schedule { get; }
        public long PartyJoinMs { get; }
        public long PartyDurationMs { get; }
        public RiftBoardRules Board { get; }
        public RiftMilestoneRules Milestones { get; }
        public double HpMultiplier { get; }
        public int WallPeriod { get; }
        public int WallZoneLength { get; }
        public double WallRate { get; }
        public double BossTimerSeconds { get; }
        public long BossShards { get; }
        public long GuardianShards { get; }
        public ExponentialCurve UnlockCost { get; }
        public ExponentialCurve LevelCost { get; }
        public int ModifiersPerWindow { get; }
        public IReadOnlyDictionary<Feature, long> Ladder { get; private set; } = new Dictionary<Feature, long>();

        public RiftGiftRules Gifts { get; private set; } = RiftGiftRules.None;
        public RiftWindowSetRules WindowSet { get; private set; } = new RiftWindowSetRules(Items.ItemRarity.Rare, 1);

        public long StageOf(Feature feature) => Ladder.TryGetValue(feature, out var stage) ? stage : 1;
        public IReadOnlyList<RiftModifier> Modifiers { get; }

        public IReadOnlyList<RiftModifier> ModifiersFor(long windowId)
        {
            var picked = RiftSchedule.PickModifiers(windowId, Modifiers.Count, ModifiersPerWindow);
            var result = new RiftModifier[picked.Count];
            for (var i = 0; i < picked.Count; i++)
                result[i] = Modifiers[picked[i]];
            return result;
        }

        public bool TryGetModifier(string id, out RiftModifier modifier)
        {
            foreach (var candidate in Modifiers)
            {
                if (candidate.Id == id)
                {
                    modifier = candidate;
                    return true;
                }
            }
            modifier = null!;
            return false;
        }

        public void RequireRoles(SynergyRules synergy)
        {
            foreach (var modifier in Modifiers)
            {
                foreach (var role in modifier.UnlessRoles)
                {
                    if (synergy.RoleMask(role) == 0)
                        throw new ContentException(FileName, "modifiers." + modifier.Id, $"unknown role '{role}', expected one of {string.Join(", ", synergy.Roles)} from {SynergyRules.FileName}");
                }
            }
        }

        public static RiftRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var schedule = root.Field("schedule");
            var starts = new List<(DayOfWeek, int)>();
            foreach (var item in schedule.Field("windows").Items())
            {
                var dayNode = item.Field("day");
                if (!Days.TryGetValue(dayNode.AsString(), out var day))
                    throw dayNode.Error($"unknown day '{dayNode.AsString()}', expected monday … sunday");
                starts.Add((day, item.Field("hourUtc").AsInt(0, 23)));
            }
            if (starts.Count == 0)
                throw schedule.Field("windows").Error("at least one window is required");

            var world = root.Field("world");
            var wall = world.Field("softWall");
            var period = wall.Field("period").AsInt(1);
            var zone = wall.Field("zoneLength").AsInt(0);
            if (zone >= period)
                throw wall.Field("zoneLength").Error($"must be shorter than period ({period})");
            var party = root.Field("party");
            var shards = root.Field("shards");
            var artifacts = root.Field("artifacts");
            var modifiers = new List<RiftModifier>();
            foreach (var node in root.Field("modifiers").Items())
                modifiers.Add(new RiftModifier(node.Field("id").AsString(), Strings(node, "unlessRoles"), ModifierReader.ReadList(node.Field("modifiers"))));
            var perWindow = root.Field("modifiersPerWindow").AsInt(0, 10);
            if (perWindow > modifiers.Count)
                throw root.Field("modifiersPerWindow").Error($"there are only {modifiers.Count} modifiers");
            return new RiftRules(
                new RiftSchedule(starts, schedule.Field("durationHours").AsNumber(1, 72), schedule.Field("announceMinutes").AsNumber(0, 1440)),
                world.Field("hpMultiplier").AsNumber(1, 1e6), period, zone, wall.Field("rate").AsNumber(1, 100),
                world.Field("bossTimerSeconds").AsNumber(1, 3600),
                shards.Field("boss").AsLong(0, 1000), shards.Field("guardian").AsLong(0, 1000),
                ExponentialCurve.Read(artifacts.Field("unlockCost")), ExponentialCurve.Read(artifacts.Field("levelCost")),
                perWindow, modifiers,
                (long)System.Math.Round(party.Field("joinMinutes").AsNumber(1, 1440) * 60_000), (long)System.Math.Round(party.Field("durationHours").AsNumber(1, 168) * 3_600_000), RiftBoardRules.Read(root.Field("board")), RiftMilestoneRules.Read(root.Field("milestones")))
            {
                Ladder = ReadLadder(root),
                Gifts = RiftGiftRules.Read(root.Field("gifts")),
                WindowSet = RiftWindowSetRules.Read(root.Field("windowSet"))
            };
        }

        private static IReadOnlyDictionary<Feature, long> ReadLadder(ContentNode root)
        {
            var ladder = new Dictionary<Feature, long>();
            var steps = root.Field("ladder");
            foreach (var step in steps.Items())
            {
                var name = step.Field("feature").AsString();
                if (!UnlockRules.FeatureNames.TryGetValue(name, out var feature))
                    throw step.Field("feature").Error($"unknown feature '{name}', expected one from {UnlockRules.FileName}");
                if (ladder.ContainsKey(feature))
                    throw step.Field("feature").Error($"feature '{name}' is listed twice");
                ladder[feature] = step.Field("stage").AsLong(1);
            }
            foreach (var (name, feature) in UnlockRules.FeatureNames)
            {
                if (!ladder.ContainsKey(feature))
                    throw steps.Error($"the ladder misses '{name}': every feature opens by Rift stages");
            }
            return ladder;
        }

        private static IReadOnlyList<string> Strings(ContentNode node, string field)
        {
            var values = new List<string>();
            if (!node.HasField(field))
                return values;
            foreach (var item in node.Field(field).Items())
                values.Add(item.AsString());
            return values;
        }
    }
}
