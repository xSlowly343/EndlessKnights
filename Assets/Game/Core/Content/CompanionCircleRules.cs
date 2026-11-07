using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum CircleTrial
    {
        OwnBiomeTrips,
        FavouriteDays,
        Hearts,
        BurrowFloor,
        BiomeTrophy,
        FamilyPack,
        HostKills,
        Trips,
        Stars,
        FarTrips,
        Colossus
    }

    public sealed class CompanionCircle
    {
        public CompanionCircle(CircleTrial trial, int count, long essence, int blooms, int bones, double traits, int levels, string unlock)
        {
            Trial = trial;
            Count = count;
            Essence = essence;
            Blooms = blooms;
            Bones = bones;
            Traits = traits;
            Levels = levels;
            Unlock = unlock;
        }

        public CircleTrial Trial { get; }
        public int Count { get; }
        public long Essence { get; }
        public int Blooms { get; }
        public int Bones { get; }
        public double Traits { get; }
        public int Levels { get; }
        public string Unlock { get; }
    }

    public sealed class CompanionCircleRules
    {
        public IReadOnlyList<CompanionCircle> Circles { get; private set; } = Array.Empty<CompanionCircle>();
        public IReadOnlyList<long> BurrowFloorEssence { get; private set; } = Array.Empty<long>();
        public long HostEssence { get; private set; }
        public long RepeatEssence { get; private set; }

        public CompanionCircle? Circle(int number) => number >= 1 && number <= Circles.Count ? Circles[number - 1] : null;

        public long BurrowEssence(int floors)
        {
            var total = 0L;
            for (var i = 0; i < floors && i < BurrowFloorEssence.Count; i++)
                total += BurrowFloorEssence[i];
            return total;
        }

        private static readonly Dictionary<string, CircleTrial> Trials = new Dictionary<string, CircleTrial>(StringComparer.Ordinal)
        {
            ["ownBiomeTrips"] = CircleTrial.OwnBiomeTrips,
            ["favouriteDays"] = CircleTrial.FavouriteDays,
            ["hearts"] = CircleTrial.Hearts,
            ["burrowFloor"] = CircleTrial.BurrowFloor,
            ["biomeTrophy"] = CircleTrial.BiomeTrophy,
            ["familyPack"] = CircleTrial.FamilyPack,
            ["hostKills"] = CircleTrial.HostKills,
            ["trips"] = CircleTrial.Trips,
            ["stars"] = CircleTrial.Stars,
            ["farTrips"] = CircleTrial.FarTrips,
            ["colossus"] = CircleTrial.Colossus
        };

        internal static CompanionCircleRules Load(ContentNode node)
        {
            var circles = new List<CompanionCircle>();
            foreach (var item in node.Field("list").Items())
            {
                var trial = item.Field("trial");
                var name = trial.Field("kind").AsString();
                if (!Trials.TryGetValue(name, out var kind))
                    throw trial.Field("kind").Error($"unknown circle trial '{name}'");
                var cost = item.Field("cost");
                var reward = item.Field("reward");
                circles.Add(new CompanionCircle(kind, trial.Field("count").AsInt(1, 100000),
                    cost.Field("essence").AsLong(0, 1_000_000),
                    cost.HasField("blooms") ? cost.Field("blooms").AsInt(0, 1000) : 0,
                    cost.HasField("bones") ? cost.Field("bones").AsInt(0, 1000) : 0,
                    reward.HasField("traits") ? reward.Field("traits").AsNumber(0, 10) : 0,
                    reward.HasField("levels") ? reward.Field("levels").AsInt(0, 100) : 0,
                    reward.HasField("unlock") ? reward.Field("unlock").AsString() : string.Empty));
            }
            var floors = new List<long>();
            var essence = node.Field("essence");
            foreach (var floor in essence.Field("burrowFloors").Items())
                floors.Add(floor.AsLong(0, 100000));
            return new CompanionCircleRules
            {
                Circles = circles,
                BurrowFloorEssence = floors,
                HostEssence = essence.Field("host").AsLong(0, 100000),
                RepeatEssence = essence.Field("repeat").AsLong(0, 100000)
            };
        }
    }
}
