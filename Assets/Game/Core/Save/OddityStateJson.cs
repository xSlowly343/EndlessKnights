using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public static class OddityStateJson
    {
        public static JsonValue Write(OddityState oddities)
        {
            var (rng0, rng1) = oddities.Rng.State;
            var map = new Dictionary<string, JsonValue>
            {
                ["rng"] = JsonValue.Array(new[] { SaveValues.Of(rng0), SaveValues.Of(rng1) }),
                ["kind"] = JsonValue.Of((int)oddities.Kind),
                ["dummyTicks"] = JsonValue.Of(oddities.DummyTicks),
                ["dummyNormalHp"] = SaveValues.Of(oddities.DummyNormalHp),
                ["dummyKills"] = JsonValue.Of(oddities.DummyKills),
                ["partsLeft"] = JsonValue.Of(oddities.PartsLeft),
                ["splitTicks"] = JsonValue.Of(oddities.SplitTicks),
                ["partHp"] = SaveValues.Of(oddities.PartHp),
                ["trailStages"] = JsonValue.Of(oddities.TrailStages),
                ["crowdStages"] = JsonValue.Of(oddities.CrowdStages),
                ["sproutStacks"] = JsonValue.Of(oddities.SproutStacks),
                ["pendingResourceShare"] = JsonValue.Of(oddities.PendingResourceShare),
                ["pendingGemKills"] = JsonValue.Of(oddities.PendingGemKills),
                ["wispCooldownTicks"] = JsonValue.Of(oddities.WispCooldownTicks),
                ["wispTicks"] = JsonValue.Of(oddities.WispTicks),
                ["wispActions"] = JsonValue.Of(oddities.WispActions),
                ["goldSpreeTicks"] = JsonValue.Of(oddities.GoldSpreeTicks),
                ["goldSpreeMultiplier"] = JsonValue.Of(oddities.GoldSpreeMultiplier),
                ["damageSpreeTicks"] = JsonValue.Of(oddities.DamageSpreeTicks),
                ["damageSpreeMultiplier"] = JsonValue.Of(oddities.DamageSpreeMultiplier),
                ["shardCarry"] = JsonValue.Of(oddities.ShardCarry)
            };
            return JsonValue.Object(map);
        }

        public static void Read(ContentNode node, OddityState oddities)
        {
            var rng = node.Field("rng").Items();
            if (rng.Count != 2)
                throw node.Field("rng").Error("expected two state words");
            var rng0 = SaveValues.ReadULong(rng[0]);
            var rng1 = SaveValues.ReadULong(rng[1]);
            if (rng0 == 0 && rng1 == 0)
                throw node.Field("rng").Error("state words must not both be zero");
            oddities.Rng = Rng.FromState(rng0, rng1);
            oddities.Kind = (OddityKind)node.Field("kind").AsInt(0, (int)OddityKind.Sprout);
            oddities.DummyTicks = node.Field("dummyTicks").AsInt(0, 1000000);
            oddities.DummyNormalHp = SaveValues.ReadBigNumber(node.Field("dummyNormalHp"));
            oddities.DummyKills = node.Field("dummyKills").AsNumber(0, 1e9);
            oddities.PartsLeft = node.Field("partsLeft").AsInt(0, 1000);
            oddities.SplitTicks = node.Field("splitTicks").AsInt(0, 1000000);
            oddities.PartHp = SaveValues.ReadBigNumber(node.Field("partHp"));
            oddities.TrailStages = node.Field("trailStages").AsInt(0, 1000000);
            oddities.CrowdStages = node.Field("crowdStages").AsInt(0, 1000000);
            oddities.SproutStacks = node.Field("sproutStacks").AsInt(0, 1000000);
            oddities.PendingResourceShare = node.Field("pendingResourceShare").AsNumber(0, 1e6);
            oddities.PendingGemKills = node.Field("pendingGemKills").AsNumber(0, 1e9);
            oddities.WispCooldownTicks = node.Field("wispCooldownTicks").AsInt(-1, 100000000);
            oddities.WispTicks = node.Field("wispTicks").AsInt(0, 100000000);
            oddities.WispActions = node.Field("wispActions").AsNumber(0, 1e9);
            oddities.GoldSpreeTicks = node.Field("goldSpreeTicks").AsInt(0, 100000000);
            oddities.GoldSpreeMultiplier = node.Field("goldSpreeMultiplier").AsNumber(0, 1e6);
            oddities.DamageSpreeTicks = node.Field("damageSpreeTicks").AsInt(0, 100000000);
            oddities.DamageSpreeMultiplier = node.Field("damageSpreeMultiplier").AsNumber(0, 1e6);
            oddities.ShardCarry = node.Field("shardCarry").AsNumber(0, 1e6);
        }
    }
}
