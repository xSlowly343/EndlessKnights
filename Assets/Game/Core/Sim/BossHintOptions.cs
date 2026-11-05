namespace Game.Core.Sim
{
    [System.Flags]
    public enum BossHintOptions
    {
        None = 0,
        Prestige = 1,
        StrikeUpgrade = 2,
        ChangeBuild = 4,
        CallFriend = 8,
        FarmStage = 16,
        TreePoints = 32,
        PathChoice = 64,
        EmptyGearSlot = 128,
        EmptySocket = 256
    }
}
