interface CrystalChangeRules {
    price: number;
    reasons: string[];
}

declare const CrystalChangeServerRules: CrystalChangeRules;

function crystalChangeSpend(nk: nkruntime.Nakama, userId: string, reason: string, nowUnixMs: number): { [key: string]: any } {
    const rules = CrystalChangeServerRules;
    if (rules.reasons.indexOf(reason) < 0)
        throw new Error("bad request");
    const left = walletSpend(nk, userId, "crystals", rules.price, reason, nowUnixMs);
    return { crystals: left, spent: rules.price };
}

function crystalChangeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.reason !== "string")
        throw new Error("bad request");
    return JSON.stringify(crystalChangeSpend(nk, partyCaller(ctx), request.reason, Date.now()));
}
