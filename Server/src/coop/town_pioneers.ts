interface TownPioneerRules {
    fish: string[];
    plants: string[];
    maxClaims: number;
}

declare const TownPioneerServerRules: TownPioneerRules;

const TownPioneerCollection = "town_pioneers";
const TownConsentKey = "consent";
const TownPioneerFish = "fish";
const TownPioneerPlant = "plant";

function townPioneerValid(key: unknown): key is string {
    if (typeof key !== "string")
        return false;
    const mark = key.indexOf(":");
    if (mark < 0)
        return false;
    const kind = key.substring(0, mark);
    const id = key.substring(mark + 1);
    return (kind === TownPioneerFish && TownPioneerServerRules.fish.indexOf(id) >= 0) || (kind === TownPioneerPlant && TownPioneerServerRules.plants.indexOf(id) >= 0);
}

function townConsent(nk: nkruntime.Nakama, userId: string): boolean {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownConsentKey, userId: userId }]);
    return objects.length > 0 && objects[0].value.show === true;
}

function townConsentSet(nk: nkruntime.Nakama, userId: string, show: unknown): { [key: string]: any } {
    if (typeof show !== "boolean")
        throw new Error("bad request");
    nk.storageWrite([{ collection: TownCollection, key: TownConsentKey, userId: userId, value: { show: show }, permissionRead: 1, permissionWrite: 0 }]);
    return { show: show };
}

function townPioneerClaim(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, claims: unknown, nowUnixMs: number): { [key: string]: any } {
    if (!Array.isArray(claims) || claims.length === 0 || claims.length > TownPioneerServerRules.maxClaims)
        throw new Error("bad request");
    const firsts: string[] = [];
    const seen: string[] = [];
    for (const key of claims) {
        if (!townPioneerValid(key))
            throw new Error("bad request");
        if (seen.indexOf(key) >= 0)
            continue;
        seen.push(key);
        try {
            nk.storageWrite([{ collection: TownPioneerCollection, key: key, userId: PartySystemUser, value: { userId: userId, at: nowUnixMs }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
            firsts.push(key);
            logger.info("town: %s is the first on the server to find %s", userId, key);
        } catch (error) {
            continue;
        }
    }
    return { firsts: firsts };
}

function townPioneerList(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const records = partyListAll(nk, TownPioneerCollection).map((object) => ({ key: object.key, userId: object.value.userId as string, at: object.value.at as number }));
    const names = partyNames(nk, records.map((record) => record.userId).filter((id, index, all) => all.indexOf(id) === index));
    const consent: { [userId: string]: boolean } = {};
    for (const record of records)
        if (consent[record.userId] === undefined)
            consent[record.userId] = townConsent(nk, record.userId);
    records.sort((a, b) => a.at - b.at || (a.key < b.key ? -1 : 1));
    return {
        show: townConsent(nk, userId),
        pioneers: records.map((record) => ({ key: record.key, at: record.at, name: consent[record.userId] ? names[record.userId] || "" : "", yours: record.userId === userId }))
    };
}

function townPioneerClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townPioneerClaim(nk, logger, partyCaller(ctx), partyRequest(payload).claims, Date.now()));
}

function townPioneersRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townPioneerList(nk, partyCaller(ctx)));
}

function townConsentRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townConsentSet(nk, partyCaller(ctx), partyRequest(payload).show));
}
