function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer): void {
    initializer.registerMatch<PartyMatchState>(PartyMatchName, {
        matchInit: partyMatchInit,
        matchJoinAttempt: partyMatchJoinAttempt,
        matchJoin: partyMatchJoin,
        matchLeave: partyMatchLeave,
        matchLoop: partyMatchLoop,
        matchTerminate: partyMatchTerminate,
        matchSignal: partyMatchSignal
    });
    initializer.registerRpc("party_create", partyCreateRpc);
    initializer.registerRpc("rift_window", riftWindowRpc);
    logger.info("party module loaded");
}
