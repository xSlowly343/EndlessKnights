function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer): void {
    initializer.registerMatch(PartyMatchName, partyMatchHandler);
    initializer.registerRpc("party_create", (rpcCtx, rpcLogger, rpcNk, payload) => {
        const request = payload ? JSON.parse(payload) : {};
        const seed = partyDecimalOrNull(request.seed);
        return JSON.stringify({ matchId: rpcNk.matchCreate(PartyMatchName, seed ? { seed: seed } : {}) });
    });
    logger.info("party module loaded");
}
