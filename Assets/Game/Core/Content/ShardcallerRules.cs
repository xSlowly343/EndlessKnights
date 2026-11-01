namespace Game.Core.Content
{
    public sealed class ShardcallerRules
    {
        public static readonly ShardcallerRules None = new ShardcallerRules();

        public int ShardEvery { get; private set; } = 8;
        public int ShardEveryMin { get; private set; } = 2;
        public int ShardMax { get; private set; } = 10;
        public double ShardArmor { get; private set; } = 0.03;
        public double ShardOnKillShare { get; private set; } = 0.5;
        public double ShardEchoKeep { get; private set; } = 0.5;
        public int GardenEvery { get; private set; } = 16;
        public int GardenEveryMin { get; private set; } = 2;
        public int GardenMax { get; private set; } = 10;
        public double GardenGrowSeconds { get; private set; } = 3;
        public double GardenGrowMinSeconds { get; private set; } = 0.5;
        public int GardenSizeMax { get; private set; } = 5;
        public double GardenArmor { get; private set; } = 0.006;
        public double GardenQueueShare { get; private set; } = 1;
        public double GardenBloomMultiplier { get; private set; } = 5;
        public double ShellPenalty { get; private set; } = 0.2;
        public double ShellEverySeconds { get; private set; } = 5;
        public double ShellEveryMinSeconds { get; private set; } = 1;
        public double ShellCrackSeconds { get; private set; } = 1;
        public double ShellPartyDamage { get; private set; }
        public int ShellShatterEvery { get; private set; } = 5;
        public double ShellShatterMultiplier { get; private set; } = 2;
        public double ShellCritHasteSeconds { get; private set; } = 0.5;
        public double PrisonMultiplier { get; private set; } = 1.5;
        public double HeartPartyDamage { get; private set; }

        public static ShardcallerRules Read(ContentNode node)
        {
            var shards = node.Field("shards");
            var garden = node.Field("garden");
            var shell = node.Field("shell");
            var skills = node.Field("skills");
            return new ShardcallerRules
            {
                ShardEvery = shards.Field("every").AsInt(1, 1000),
                ShardEveryMin = shards.Field("everyMin").AsInt(1, 1000),
                ShardMax = shards.Field("max").AsInt(1, 100),
                ShardArmor = shards.Field("armor").AsNumber(0, 1),
                ShardOnKillShare = shards.Field("onKillShare").AsNumber(0, 10),
                ShardEchoKeep = shards.Field("echoKeep").AsNumber(0, 1),
                GardenEvery = garden.Field("every").AsInt(1, 1000),
                GardenEveryMin = garden.Field("everyMin").AsInt(1, 1000),
                GardenMax = garden.Field("max").AsInt(1, 100),
                GardenGrowSeconds = garden.Field("growSeconds").AsNumber(0.1, 600),
                GardenGrowMinSeconds = garden.Field("growMinSeconds").AsNumber(0.1, 600),
                GardenSizeMax = garden.Field("sizeMax").AsInt(1, 100),
                GardenArmor = garden.Field("armor").AsNumber(0, 1),
                GardenQueueShare = garden.Field("queueShare").AsNumber(0, 10),
                GardenBloomMultiplier = garden.Field("bloomMultiplier").AsNumber(0, 100),
                ShellPenalty = shell.Field("penalty").AsNumber(0, 0.9),
                ShellEverySeconds = shell.Field("everySeconds").AsNumber(0.1, 600),
                ShellEveryMinSeconds = shell.Field("everyMinSeconds").AsNumber(0.1, 600),
                ShellCrackSeconds = shell.Field("crackSeconds").AsNumber(0.1, 600),
                ShellPartyDamage = shell.Field("partyDamage").AsNumber(0, 10),
                ShellShatterEvery = shell.Field("shatterEvery").AsInt(1, 100),
                ShellShatterMultiplier = shell.Field("shatterMultiplier").AsNumber(1, 100),
                ShellCritHasteSeconds = shell.Field("critHasteSeconds").AsNumber(0, 600),
                PrisonMultiplier = skills.Field("prisonMultiplier").AsNumber(1, 100),
                HeartPartyDamage = skills.Field("heartPartyDamage").AsNumber(0, 10)
            };
        }
    }
}
