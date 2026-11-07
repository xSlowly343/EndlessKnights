using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum BoardTaskKind
    {
        Bring,
        Trip
    }

    public sealed class BoardTask
    {
        public string Id { get; internal set; } = string.Empty;
        public BoardTaskKind Kind { get; internal set; }
        public string Item { get; internal set; } = string.Empty;
        public int Amount { get; internal set; }
        public string Biome { get; internal set; } = string.Empty;
        public string Trait { get; internal set; } = string.Empty;
        public double Experience { get; internal set; }
        public int Souls { get; internal set; }
        public string Food { get; internal set; } = string.Empty;
        public int FoodAmount { get; internal set; }
    }

    public sealed class BurrowFloor
    {
        public BurrowFloor(double power, int souls, long shards, int bones)
        {
            Power = power;
            Souls = souls;
            Shards = shards;
            Bones = bones;
        }

        public double Power { get; }
        public int Souls { get; }
        public long Shards { get; }
        public int Bones { get; }
    }

    public sealed class CompanionWorkRules
    {
        public int BoardTasks { get; private set; }
        public int RerollsPerDay { get; private set; }
        public IReadOnlyList<BoardTask> Pool { get; private set; } = Array.Empty<BoardTask>();
        public double HelpSeconds { get; private set; }
        public double HelpBond { get; private set; }
        public long HelpReputation { get; private set; }
        public IReadOnlyList<string> HelpGuilds { get; private set; } = Array.Empty<string>();
        public int BurrowDays { get; private set; } = 7;
        public int BurrowParty { get; private set; } = 3;
        public double PowerPerStage { get; private set; }
        public double PowerPerStar { get; private set; }
        public double PowerPerRank { get; private set; }
        public IReadOnlyList<BurrowFloor> Floors { get; private set; } = Array.Empty<BurrowFloor>();

        public BoardTask? Task(string id)
        {
            for (var i = 0; i < Pool.Count; i++)
            {
                if (Pool[i].Id == id)
                    return Pool[i];
            }
            return null;
        }

        internal static CompanionWorkRules Load(ContentNode node, IReadOnlyList<string> biomes)
        {
            var board = node.Field("board");
            var pool = new List<BoardTask>();
            foreach (var item in board.Field("pool").Items())
            {
                var kind = item.Field("kind").AsString();
                var task = new BoardTask
                {
                    Id = item.Field("id").AsString(),
                    Experience = item.Field("experience").AsNumber(0, 1e6),
                    Souls = item.HasField("souls") ? item.Field("souls").AsInt(0, 1000) : 0,
                    Food = item.HasField("food") ? item.Field("food").AsString() : string.Empty,
                    FoodAmount = item.HasField("foodAmount") ? item.Field("foodAmount").AsInt(0, 1000) : 0
                };
                if (kind == "bring")
                {
                    task.Kind = BoardTaskKind.Bring;
                    task.Item = item.Field("item").AsString();
                    task.Amount = item.Field("amount").AsInt(1, 10_000);
                }
                else if (kind == "trip")
                {
                    task.Kind = BoardTaskKind.Trip;
                    task.Biome = item.Field("biome").AsString();
                    if (!Contains(biomes, task.Biome))
                        throw item.Field("biome").Error($"unknown biome '{task.Biome}'");
                    task.Trait = item.HasField("trait") ? item.Field("trait").AsString() : string.Empty;
                }
                else
                    throw item.Field("kind").Error($"unknown task '{kind}'");
                pool.Add(task);
            }
            var help = node.Field("help");
            var guilds = new List<string>();
            foreach (var item in help.Field("guilds").Items())
                guilds.Add(item.AsString());
            var burrow = node.Field("burrow");
            var floors = new List<BurrowFloor>();
            foreach (var item in burrow.Field("floors").Items())
                floors.Add(new BurrowFloor(item.Field("power").AsNumber(0, 1e9), item.Field("souls").AsInt(0, 1000), item.Field("shards").AsLong(0), item.HasField("bones") ? item.Field("bones").AsInt(0, 1000) : 0));
            var tasks = board.Field("tasks").AsInt(1, 20);
            if (tasks > pool.Count)
                throw board.Field("tasks").Error($"the pool has only {pool.Count} tasks");
            return new CompanionWorkRules
            {
                BoardTasks = tasks,
                RerollsPerDay = board.Field("rerollsPerDay").AsInt(0, 20),
                Pool = pool,
                HelpSeconds = help.Field("hours").AsNumber(0.1, 100) * 3600,
                HelpBond = help.Field("bond").AsNumber(0, 1000),
                HelpReputation = help.Field("reputation").AsLong(0),
                HelpGuilds = guilds,
                BurrowDays = burrow.Field("days").AsInt(1, 100),
                BurrowParty = burrow.Field("party").AsInt(1, 10),
                PowerPerStage = burrow.Field("powerPerStage").AsNumber(0, 1000),
                PowerPerStar = burrow.Field("powerPerStar").AsNumber(0, 1000),
                PowerPerRank = burrow.Field("powerPerRank").AsNumber(0, 1000),
                Floors = floors
            };
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
