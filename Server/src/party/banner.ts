interface PartyBannerValue {
    slots: string[];
}

const PartyBannerCollection = "party_banner";
const PartyBannerAttempts = 4;

function partyBannerRead(nk: nkruntime.Nakama, partyId: string): { value: PartyBannerValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyBannerCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { slots: [] }, version: "*" };
    const stored = objects[0].value as PartyBannerValue;
    return { value: { slots: stored.slots || [] }, version: objects[0].version };
}

function partyBannerChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyBannerValue) => T): T {
    for (let attempt = 1; attempt <= PartyBannerAttempts; attempt++) {
        const stored = partyBannerRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyBannerCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyBannerSlots(level: number): number {
    const unlocks = PartyGrowthServerRules.unlocks;
    if (!unlocks.some((unlock) => unlock.kind === "banner" && unlock.level <= level))
        return 0;
    return 1 + unlocks.filter((unlock) => unlock.kind === "bannerSlot" && unlock.level <= level).length;
}

function partyBannerAll(): string[] {
    const rules = PartyGrowthServerRules.banner;
    const ids: string[] = [];
    for (const trial of rules.trials)
        for (const step of rules.trial)
            ids.push(trial + "_" + step);
    for (const step of rules.colossus)
        ids.push("colossus_" + step);
    return ids;
}

function partyBannerEarned(nk: nkruntime.Nakama, partyId: string): string[] {
    const rules = PartyGrowthServerRules.banner;
    const best = partyTrialRead(nk, partyId).value.best;
    const colossus = partyColossusRead(nk, partyId).value.best;
    const earned: string[] = [];
    for (const trial of rules.trials)
        for (const step of rules.trial)
            if ((best[trial] || 0) >= step)
                earned.push(trial + "_" + step);
    for (const step of rules.colossus)
        if (colossus >= step)
            earned.push("colossus_" + step);
    return earned;
}

function partyBannerSituation(nk: nkruntime.Nakama, userId: string): { partyId: string; leader: boolean; slots: number; earned: string[] } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    return {
        partyId: group.id,
        leader: partyMetaOf(group).leader === userId,
        slots: partyBannerSlots(partyGrowthLevel(partyGrowthRead(nk, group.id).value.experience).level),
        earned: partyBannerEarned(nk, group.id)
    };
}

function partyBannerPlaced(value: PartyBannerValue, slots: number, earned: string[]): string[] {
    const placed: string[] = [];
    for (let i = 0; i < slots; i++) {
        const crest = value.slots[i] || "";
        placed.push(crest && earned.indexOf(crest) >= 0 ? crest : "");
    }
    return placed;
}

function partyBannerView(situation: { partyId: string; leader: boolean; slots: number; earned: string[] }, value: PartyBannerValue): { [key: string]: any } {
    return {
        partyId: situation.partyId, opened: situation.slots > 0, leader: situation.leader, slots: partyBannerPlaced(value, situation.slots, situation.earned),
        earned: situation.earned
    };
}

function partyBannerMine(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const situation = partyBannerSituation(nk, userId);
    return partyBannerView(situation, partyBannerRead(nk, situation.partyId).value);
}

function partyBannerSet(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }): { [key: string]: any } {
    const slot = request.slot;
    const crest = request.crest;
    if (typeof slot !== "number" || slot !== Math.floor(slot) || slot < 0 || typeof crest !== "string" || (crest !== "" && partyBannerAll().indexOf(crest) < 0))
        throw new Error("bad request");
    const situation = partyBannerSituation(nk, userId);
    if (situation.slots <= 0)
        throw new Error("the banner is not open yet");
    if (!situation.leader)
        throw new Error("only the leader can do that");
    if (slot >= situation.slots)
        throw new Error("this place on the banner is not open yet");
    if (crest !== "" && situation.earned.indexOf(crest) < 0)
        throw new Error("the party has not earned this crest yet");
    return partyBannerChange(nk, situation.partyId, (value) => {
        const slots = partyBannerPlaced(value, situation.slots, situation.earned);
        for (let i = 0; i < slots.length; i++)
            if (crest !== "" && slots[i] === crest)
                slots[i] = "";
        slots[slot] = crest;
        value.slots = slots;
        return partyBannerView(situation, value);
    });
}

function partyBannerRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyBannerMine(nk, partyCaller(ctx)));
}

function partyBannerSetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyBannerSet(nk, partyCaller(ctx), partyRequest(payload)));
}
