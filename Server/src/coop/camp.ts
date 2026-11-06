interface CampRules {
    inboxLimit: number;
    textLimit: number;
    listLimit: number;
}

interface CampSnapshotValue {
    theme: string;
    decor: string[];
    monuments: { [id: string]: number };
    title: string;
    trophies: string[];
    brand: string;
    at: number;
}

interface CampGuestAction {
    id: string;
    from: string;
    fromName: string;
    act: string;
    bed: number;
    at: number;
}

interface CampVisits {
    day: number;
    done: { [host: string]: string[] };
}

const CampServerRules: CampRules = { inboxLimit: 50, textLimit: 64, listLimit: 32 };
const CampCollection = "camp";
const CampSnapshotKey = "snapshot";
const CampVisitsKey = "visits";
const CampGuestCollection = "camp_guests";
const CampActs = ["gift", "water", "hammer"];
const CampWriteAttempts = 4;

function campText(value: any): string {
    if (value === undefined || value === null)
        return "";
    if (typeof value !== "string" || value.length > CampServerRules.textLimit)
        throw new Error("bad request");
    return value;
}

function campTexts(value: any): string[] {
    if (value === undefined || value === null)
        return [];
    if (!Array.isArray(value) || value.length > CampServerRules.listLimit)
        throw new Error("bad request");
    return value.map(campText);
}

function campPublish(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const monuments: { [id: string]: number } = {};
    const raw = request && request.monuments;
    if (raw !== undefined && raw !== null) {
        if (typeof raw !== "object" || Array.isArray(raw) || Object.keys(raw).length > CampServerRules.listLimit)
            throw new Error("bad request");
        for (const id of Object.keys(raw)) {
            const level = raw[id];
            if (typeof level !== "number" || level !== Math.floor(level) || level < 0 || level > 100)
                throw new Error("bad request");
            monuments[campText(id)] = level;
        }
    }
    const value: CampSnapshotValue = {
        theme: campText(request && request.theme),
        decor: campTexts(request && request.decor),
        monuments: monuments,
        title: campText(request && request.title),
        trophies: campTexts(request && request.trophies),
        brand: campText(request && request.brand),
        at: nowUnixMs
    };
    nk.storageWrite([{ collection: CampCollection, key: CampSnapshotKey, userId: userId, value: value as any, permissionRead: 2, permissionWrite: 0 }]);
    return { published: true };
}

function campSnapshot(nk: nkruntime.Nakama, host: string): CampSnapshotValue | null {
    const objects = nk.storageRead([{ collection: CampCollection, key: CampSnapshotKey, userId: host }]);
    return objects.length === 0 ? null : objects[0].value as CampSnapshotValue;
}

function campVisits(nk: nkruntime.Nakama, userId: string, day: number): { value: CampVisits; version: string } {
    const objects = nk.storageRead([{ collection: CampCollection, key: CampVisitsKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { day: day, done: {} }, version: "*" };
    const value = objects[0].value as CampVisits;
    return { value: value.day === day ? value : { day: day, done: {} }, version: objects[0].version };
}

function campHost(nk: nkruntime.Nakama, userId: string, host: any): CampSnapshotValue {
    if (typeof host !== "string" || host.length === 0)
        throw new Error("bad request");
    if (host === userId)
        throw new Error("not your own camp");
    const snapshot = campSnapshot(nk, host);
    if (!snapshot)
        throw new Error("no such camp");
    return snapshot;
}

function campLeft(done: string[]): string[] {
    return CampActs.filter((act) => done.indexOf(act) < 0);
}

function campVisit(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const host = request && request.host;
    const snapshot = campHost(nk, userId, host);
    const visits = campVisits(nk, userId, walletDayOf(nowUnixMs)).value;
    return { host: host, camp: snapshot, left: campLeft(visits.done[host] || []) };
}

function campInboxCount(nk: nkruntime.Nakama, host: string): number {
    let count = 0;
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(host, CampGuestCollection, 100, cursor);
        count += (page.objects || []).length;
        cursor = page.cursor || undefined;
    } while (cursor);
    return count;
}

function campGuest(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const host = request && request.host;
    const act = request && request.act;
    campHost(nk, userId, host);
    if (CampActs.indexOf(act) < 0)
        throw new Error("bad request");
    const bed = request.bed === undefined ? -1 : request.bed;
    if (typeof bed !== "number" || bed !== Math.floor(bed) || bed < -1 || bed > 100)
        throw new Error("bad request");
    if (act !== "gift" && campInboxCount(nk, host) >= CampServerRules.inboxLimit)
        throw new Error("camp is busy");
    const day = walletDayOf(nowUnixMs);
    let left: string[] = [];
    for (let attempt = 1; attempt <= CampWriteAttempts; attempt++) {
        const stored = campVisits(nk, userId, day);
        const done = stored.value.done[host] || [];
        if (done.indexOf(act) >= 0)
            throw new Error("already done today");
        stored.value.done[host] = done.concat([act]);
        try {
            nk.storageWrite([{ collection: CampCollection, key: CampVisitsKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            left = campLeft(stored.value.done[host]);
            break;
        } catch (error) {
            if (attempt === CampWriteAttempts)
                throw new Error("camp is busy, try again");
        }
    }
    let friendship = 0;
    if (act === "gift") {
        friendship = walletGrant(nk, userId, "camp_gift", 1, nowUnixMs);
        walletGrant(nk, host, "camp_gift", 1, nowUnixMs);
    }
    const users = nk.usersGetId([userId]);
    const action: CampGuestAction = {
        id: userId + "_" + day + "_" + act,
        from: userId,
        fromName: users.length > 0 ? users[0].displayName || users[0].username : "",
        act: act,
        bed: bed,
        at: nowUnixMs
    };
    nk.storageWrite([{ collection: CampGuestCollection, key: action.id, userId: host, value: action as any, version: "*", permissionRead: 1, permissionWrite: 0 }]);
    return { left: left, friendship: friendship };
}

function campGuestsClaim(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const actions: CampGuestAction[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, CampGuestCollection, 100, cursor);
        for (const object of page.objects || [])
            actions.push(object.value as CampGuestAction);
        cursor = page.cursor || undefined;
    } while (cursor);
    actions.sort((a, b) => a.at - b.at || (a.id < b.id ? -1 : 1));
    if (actions.length > 0)
        nk.storageDelete(actions.map((action) => ({ collection: CampGuestCollection, key: action.id, userId: userId })));
    return { guests: actions };
}

function campPublishRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(campPublish(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function campVisitRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(campVisit(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function campGuestRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(campGuest(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function campGuestsClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(campGuestsClaim(nk, partyCaller(ctx)));
}
