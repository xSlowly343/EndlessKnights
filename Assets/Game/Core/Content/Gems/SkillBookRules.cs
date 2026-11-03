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
        private readonly int[] _auraSlotLevels;
        private readonly Dictionary<string, IReadOnlyList<GemDef>> _startingGems;
        private readonly int[] _skillUnlockLevels;

        private SkillBookRules(int baseSlots, long[] slotUnlockStages, int baseSockets, long[] socketRecordStages, int socketRebirths, int maxSockets,
            Dictionary<string, IReadOnlyList<GemDef>> startingGems, int[] auraSlotLevels, int[] skillUnlockLevels)
        {
            _skillUnlockLevels = skillUnlockLevels;
            _auraSlotLevels = auraSlotLevels;
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

        public int SlotsFor(long recordStage, bool earlySlot = false)
        {
            var slots = BaseSlots + (earlySlot ? 1 : 0);
            foreach (var stage in _slotUnlockStages)
            {
                if (recordStage > stage)
                    slots++;
            }
            return System.Math.Min(MaxSlots, slots);
        }

        public long UnlockStageOf(int slot, bool earlySlot = false)
        {
            var step = slot - BaseSlots - (earlySlot ? 1 : 0);
            return step < 0 || slot >= MaxSlots ? 0 : _slotUnlockStages[step];
        }

        public int AuraSlotsFor(int heroLevel)
        {
            var slots = 0;
            foreach (var level in _auraSlotLevels)
            {
                if (heroLevel >= level)
                    slots++;
            }
            return slots;
        }

        public int SkillUnlockLevel(SkillTier tier) => _skillUnlockLevels[(int)tier];

        public int AuraSlotLevel(int slot) => slot >= 0 && slot < _auraSlotLevels.Length ? _auraSlotLevels[slot] : 0;

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

            var unlocks = ReadStages(slots.Field("unlockAfterStages"));
            var baseSlots = slots.Field("base").AsInt(Sim.SkillLoadout.HandSlots, Sim.SkillLoadout.HandSlots + Sim.SkillLoadout.AutoSlots);
            if (baseSlots + unlocks.Length != Sim.SkillLoadout.HandSlots + Sim.SkillLoadout.AutoSlots)
                throw slots.Error($"base slots and unlocks give {baseSlots + unlocks.Length}, expected {Sim.SkillLoadout.HandSlots} hands and {Sim.SkillLoadout.AutoSlots} automatic slots");
            return new SkillBookRules(
                baseSlots,
                unlocks,
                sockets.Field("base").AsInt(0, 20),
                ReadStages(sockets.Field("recordStages")),
                sockets.Field("rebirths").AsInt(0, 20),
                sockets.Field("max").AsInt(1, Sim.SkillSlot.BuiltInSocket - 1),
                starting,
                ReadAuraSlotLevels(root.Field("auraSlots").Field("unlockLevels")),
                ReadSkillUnlockLevels(root.Field("skillUnlockLevels")));
        }

        private static int[] ReadSkillUnlockLevels(ContentNode node)
        {
            var tiers = GemCatalog.Tiers;
            var levels = new int[tiers.Count];
            var seen = new bool[tiers.Count];
            foreach (var name in node.FieldNames())
            {
                if (!tiers.TryGetValue(name, out var tier))
                    throw node.Field(name).Error($"unknown skill tier '{name}', expected common, rare, epic, legendary or mythic");
                levels[(int)tier] = node.Field(name).AsInt(1, 1000);
                seen[(int)tier] = true;
            }
            for (var i = 0; i < levels.Length; i++)
            {
                if (!seen[i])
                    throw node.Error($"tier '{((SkillTier)i).ToString().ToLowerInvariant()}' has no unlock level");
                if (i > 0 && levels[i] < levels[i - 1])
                    throw node.Field(((SkillTier)i).ToString().ToLowerInvariant()).Error("a stronger tier must not open earlier");
            }
            if (levels[0] != 1)
                throw node.Field("common").Error("common skills must open at level 1");
            return levels;
        }

        private static int[] ReadAuraSlotLevels(ContentNode node)
        {
            var items = node.Items();
            if (items.Count != Sim.SkillLoadout.AuraSlots)
                throw node.Error($"expected {Sim.SkillLoadout.AuraSlots} aura slot levels, got {items.Count}");
            var levels = new int[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                levels[i] = items[i].AsInt(1, 1000);
                if (i > 0 && levels[i] < levels[i - 1])
                    throw items[i].Error("aura slot levels must not go down");
            }
            return levels;
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
