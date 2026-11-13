interface MuseumFirst {
    userId: string;
    at: number;
}

const MuseumFirstCollection = "museum_first";
const MuseumFoundLimit = 64;

function museumIds(raw: unknown): string[] {
    if (!Array.isArray(raw) || raw.length === 0 || raw.length > MuseumFoundLimit)
        throw new Error("bad request");
    const known = TownServerRules.museum;
    const ids: string[] = [];
    for (const id of raw) {
        if (typeof id !== "string" || known.indexOf(id) < 0)
            throw new Error("bad request");
        if (ids.indexOf(id) < 0)
            ids.push(id);
    }
    return ids;
}

function museumFirsts(nk: nkruntime.Nakama, userId: string): { [key: string]: any }[] {
    const ids = TownServerRules.museum;
    const objects = nk.storageRead(ids.map((id) => ({ collection: MuseumFirstCollection, key: id, userId: PartySystemUser })));
    return objects.map((object) => {
        const first = object.value as MuseumFirst;
        return { id: object.key, name: consentShownName(nk, first.userId, ConsentMuseum), mine: first.userId === userId, at: first.at };
    }).sort((a, b) => a.at - b.at);
}

function museumFound(nk: nkruntime.Nakama, userId: string, raw: unknown, nowUnixMs: number): { [key: string]: any } {
    const ids = museumIds(raw);
    const found = cloudFound(nk, userId);
    for (const id of ids)
        if (found.indexOf(id) < 0)
            throw new Error("not found by the last cloud save");
    const won: string[] = [];
    for (const id of ids) {
        try {
            nk.storageWrite([{ collection: MuseumFirstCollection, key: id, userId: PartySystemUser, value: { userId: userId, at: nowUnixMs } as any, version: "*", permissionRead: 2, permissionWrite: 0 }]);
            won.push(id);
        } catch (error) {
            continue;
        }
    }
    return { reported: ids, won: won, firsts: museumFirsts(nk, userId) };
}

function museumFoundRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(museumFound(nk, partyCaller(ctx), partyRequest(payload).ids, Date.now()));
}

function museumFirstsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify({ firsts: museumFirsts(nk, partyCaller(ctx)) });
}
