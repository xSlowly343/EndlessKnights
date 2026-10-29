namespace Game.Core.Party.Wire
{
    public enum PartyMessageKind : byte
    {
        None,
        Hello,
        DamageBatch,
        CastEffect,
        Presence,
        EncounterState,
        EnemyKilled,
        EncounterMoment,
        Roster,
        PartyOffer,
        PartyReaction,
        WorldRequest,
        FocusRelay,
        PartyResource,
        AllyCast,
        Synergy,
        BossRequest,
        Marks
    }
}
