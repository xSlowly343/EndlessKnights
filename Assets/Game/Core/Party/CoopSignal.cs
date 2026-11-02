namespace Game.Core.Party
{
    public enum CoopSignal : byte
    {
        None,
        ResourceFill,
        PartyMembers,
        Combo,
        KillStreak,
        Crit,
        Kill,
        BossKill,
        SkillCast,
        KeyHit
    }
}
