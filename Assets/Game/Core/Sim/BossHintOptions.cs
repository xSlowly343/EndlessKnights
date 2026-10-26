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
        FarmStage = 16
    }
}
