using System;
using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public sealed class SkillBookRules
    {
        public const string FileName = "skillbook.json";
        public const int SupportedVersion = 1;

        private readonly long[] _slotUnlockStages;
        private readonly long[] _socketRecordStages;
        private readonly Dictionary<string, IReadOnlyList<GemDef>> _startingGems;

        private SkillBookRules(int baseSlots, long[] slotUnlockStages, int baseSockets, long[] socketRecordStages, int socketRebirths, int maxSockets,
            Dictionary<string, IReadOnlyList<GemDef>> startingGems, double attributePerColoredSupport)
        {
            AttributePerColoredSupport = attributePerColoredSupport;
            BaseSlots = baseSlots;
            _slotUnlockStages = slotUnlockStages;
            BaseSockets = baseSockets;
            _socketRecordStages = socketRecordStages;
            SocketRebirths = socketRebirths;
            MaxSockets = maxSockets;
            _startingGems = startingGems;
        }

        public int BaseSlots { get; }
        public int MaxSlots => BaseSlots + _slotUnlockStages.Length;
        public int BaseSockets { get; }
        public int SocketRebirths { get; }
        public int MaxSockets { get; }
        public double AttributePerColoredSupport { get; }

        public int SlotsFor(long recordStage)
        {
            var slots = BaseSlots;
            foreach (var stage in _slotUnlockStages)
            {
                if (recordStage > stage)
                    slots++;
            }
            return slots;
        }

        public long UnlockStageOf(int slot) =>
            slot < BaseSlots || slot >= MaxSlots ? 0 : _slotUnlockStages[slot - BaseSlots];

        public int SocketsFor(long recordStage, int rebirths)
        {
            var sockets = BaseSockets + System.Math.Min(rebirths, SocketRebirths);
            foreach (var stage in _socketRecordStages)
            {
                if (recordStage >= stage)
                    sockets++;
            }
            return System.Math.Min(sockets, MaxSockets);
        }

        public IReadOnlyList<GemDef> StartingGems(string className) =>
            _startingGems.TryGetValue(className, out var gems) ? gems : Array.Empty<GemDef>();

        public static SkillBookRules Load(string json, GemCatalog gems)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var slots = root.Field("slots");
            var sockets = root.Field("sockets");
            var starting = new Dictionary<string, IReadOnlyList<GemDef>>(StringComparer.Ordinal);
            var startingNode = root.Field("startingGems");
            foreach (var className in startingNode.FieldNames())
            {
                var list = new List<GemDef>();
                foreach (var id in startingNode.Field(className).Items())
                {
                    if (!gems.TryGet(id.AsString(), out var gem))
                        throw id.Error($"unknown gem '{id.AsString()}', it must be listed in {GemCatalog.FileName}");
                    list.Add(gem);
                }
                starting[className] = list;
            }

            return new SkillBookRules(
                slots.Field("base").AsInt(1, 20),
                ReadStages(slots.Field("unlockAfterStages")),
                sockets.Field("base").AsInt(0, 20),
                ReadStages(sockets.Field("recordStages")),
                sockets.Field("rebirths").AsInt(0, 20),
                sockets.Field("max").AsInt(1, Sim.SkillSlot.SocketCapacity),
                starting,
                sockets.Field("attributePerColoredSupport").AsNumber(1, 10000));
        }

        private static long[] ReadStages(ContentNode node)
        {
            var stages = new List<long>();
            foreach (var item in node.Items())
                stages.Add(item.AsLong(1));
            return stages.ToArray();
        }
    }
}
