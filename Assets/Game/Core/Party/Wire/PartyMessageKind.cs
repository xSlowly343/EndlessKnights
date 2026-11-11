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
        BossRequest = 16,
        PartyLoadout = 18,
        PartyKeys = 19,
        PartyLook = 20
    }
}
