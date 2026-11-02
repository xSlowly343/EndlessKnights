using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class ShardcallerStats
    {
        public static readonly StatId ShardPower = new StatId("shardPower");
        public static readonly StatId ShardEvery = new StatId("shardEvery");
        public static readonly StatId ShardMax = new StatId("shardMax");
        public static readonly StatId ShardOnKill = new StatId("shardOnKill");
        public static readonly StatId ShardEcho = new StatId("shardEcho");
        public static readonly StatId ShardQueue = new StatId("shardQueue");
        public static readonly StatId ShardAvalanche = new StatId("shardAvalanche");

        public static readonly StatId GardenPower = new StatId("gardenPower");
        public static readonly StatId GardenEvery = new StatId("gardenEvery");
        public static readonly StatId GardenMax = new StatId("gardenMax");
        public static readonly StatId GardenGrow = new StatId("gardenGrow");
        public static readonly StatId GardenSize = new StatId("gardenSize");
        public static readonly StatId GardenSeed = new StatId("gardenSeed");
        public static readonly StatId GardenQueue = new StatId("gardenQueue");
        public static readonly StatId GardenTransplant = new StatId("gardenTransplant");
        public static readonly StatId GardenBloom = new StatId("gardenBloom");

        public static readonly StatId ShellPower = new StatId("shellPower");
        public static readonly StatId ShellPenalty = new StatId("shellPenalty");
        public static readonly StatId ShellEvery = new StatId("shellEvery");
        public static readonly StatId ShellCrack = new StatId("shellCrack");
        public static readonly StatId ShellResonance = new StatId("shellResonance");
        public static readonly StatId ShellCritHaste = new StatId("shellCritHaste");
        public static readonly StatId ShellShatter = new StatId("shellShatter");
    }
}
