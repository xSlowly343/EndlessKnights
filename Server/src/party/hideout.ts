interface HideoutTierRule {
    members: number;
    bosses: number;
    orders: number;
    sitSeconds: number;
    fireLevel: number;
}

interface HideoutRules {
    hereMs: number;
    maxSitStepMs: number;
    tiers: HideoutTierRule[];
}

interface HideoutValue {
    tier: number;
    sitSeconds: number;
    orders: number;
    fireLevel: number;
    here: { [userId: string]: number };
    sitAt: number;
}

declare const HideoutServerRules: HideoutRules;

const HideoutCollection = "party_hideout";
const HideoutWriteAttempts = 4;

function hideoutEmpty(): HideoutValue {
    return { tier: 0, sitSeconds: 0, orders: 0, fireLevel: 0, here: {}, sitAt: 0 };
}

function hideoutRead(nk: nkruntime.Nakama, partyId: string): { value: HideoutValue; version: string } {
    const objects = nk.storageRead([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: hideoutEmpty(), version: "*" };
    const value = objects[0].value as HideoutValue;
    return { value: { tier: value.tier || 0, sitSeconds: value.sitSeconds || 0, orders: value.orders || 0, fireLevel: value.fireLevel || 0, here: value.here || {}, sitAt: value.sitAt || 0 }, version: objects[0].version };
}

function hideoutChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: HideoutValue) => T): T {
    for (let attempt = 1; attempt <= HideoutWriteAttempts; attempt++) {
        const stored = hideoutRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the hideout is busy, try again");
}

function hideoutHere(value: HideoutValue, members: string[], nowUnixMs: number): string[] {
    return members.filter((userId) => value.here[userId] !== undefined && nowUnixMs - value.here[userId] <= HideoutServerRules.hereMs);
}

function hideoutReached(value: HideoutValue, tier: HideoutTierRule, members: number, bosses: number): boolean {
    return members >= tier.members && bosses >= tier.bosses && value.orders >= tier.orders && value.sitSeconds >= tier.sitSeconds && value.fireLevel >= tier.fireLevel;
}

function hideoutRise(value: HideoutValue, members: number, bosses: number): void {
    const tiers = HideoutServerRules.tiers;
    while (value.tier < tiers.length && hideoutReached(value, tiers[value.tier], members, bosses))
        value.tier++;
}

function hideoutSit(value: HideoutValue, members: string[], nowUnixMs: number): void {
    const together = hideoutHere(value, members, nowUnixMs).length >= 2;
    if (together && value.sitAt > 0)
        value.sitSeconds += Math.floor(Math.min(nowUnixMs - value.sitAt, HideoutServerRules.maxSitStepMs) / 1000);
    value.sitAt = together ? nowUnixMs : 0;
}

function hideoutView(nk: nkruntime.Nakama, group: nkruntime.Group, value: HideoutValue, bosses: number, nowUnixMs: number): { [key: string]: any } {
    const members = partyMetaOf(group).members;
    const here = hideoutHere(value, members, nowUnixMs);
    const users = here.length > 0 ? nk.usersGetId(here) : [];
    return {
        tier: value.tier, members: members.length, bosses: bosses, orders: value.orders, sitSeconds: value.sitSeconds, fireLevel: value.fireLevel,
        here: here.map((userId) => {
            const user = users.filter((entry) => entry.userId === userId)[0];
            return { userId: userId, name: user ? user.displayName || user.username : "" };
        })
    };
}

function hideoutUpdate(nk: nkruntime.Nakama, userId: string, here: boolean | null, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const members = partyMetaOf(group).members;
    const bosses = chronicleRead(nk, group.id).value.stats.bossesTogether;
    const value = hideoutChange(nk, group.id, (hideout) => {
        if (here === true)
            hideout.here[userId] = nowUnixMs;
        else if (here === false)
            delete hideout.here[userId];
        for (const id of Object.keys(hideout.here))
            if (members.indexOf(id) < 0 || nowUnixMs - hideout.here[id] > HideoutServerRules.hereMs)
                delete hideout.here[id];
        hideoutSit(hideout, members, nowUnixMs);
        hideoutRise(hideout, members.length, bosses);
        return hideout;
    });
    return hideoutView(nk, group, value, bosses, nowUnixMs);
}

function hideoutViewRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutUpdate(nk, partyCaller(ctx), null, Date.now()));
}

function hideoutPresenceRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const here = partyRequest(payload).here;
    if (typeof here !== "boolean")
        throw new Error("bad request");
    return JSON.stringify(hideoutUpdate(nk, partyCaller(ctx), here, Date.now()));
}
