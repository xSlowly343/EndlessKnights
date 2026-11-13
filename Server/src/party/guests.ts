interface GuestRules {
    inboxLimit: number;
    textLimit: number;
    listLimit: number;
}

interface GuestSnapshotValue {
    theme: string;
    decor: string[];
    monuments: { [id: string]: number };
    title: string;
    trophies: string[];
    brand: string;
    at: number;
}

interface GuestAction {
    id: string;
    from: string;
    fromName: string;
    act: string;
    bed: number;
    at: number;
}

interface GuestVisits {
    day: number;
    done: { [host: string]: string[] };
}

const GuestServerRules: GuestRules = { inboxLimit: 50, textLimit: 64, listLimit: 32 };
const GuestCollection = "hideout_home";
const GuestSnapshotKey = "snapshot";
const GuestVisitsKey = "visits";
const GuestActsCollection = "hideout_guests";
const GuestActs = ["gift", "water", "hammer"];
const GuestWriteAttempts = 4;

function guestText(value: any): string {
    if (value === undefined || value === null)
        return "";
    if (typeof value !== "string" || value.length > GuestServerRules.textLimit)
        throw new Error("bad request");
    return value;
}

function guestTexts(value: any): string[] {
    if (value === undefined || value === null)
        return [];
    if (!Array.isArray(value) || value.length > GuestServerRules.listLimit)
        throw new Error("bad request");
    return value.map(guestText);
}

function hideoutPublish(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const monuments: { [id: string]: number } = {};
    const raw = request && request.monuments;
    if (raw !== undefined && raw !== null) {
        if (typeof raw !== "object" || Array.isArray(raw) || Object.keys(raw).length > GuestServerRules.listLimit)
            throw new Error("bad request");
        for (const id of Object.keys(raw)) {
            const level = raw[id];
            if (typeof level !== "number" || level !== Math.floor(level) || level < 0 || level > 100)
                throw new Error("bad request");
            monuments[guestText(id)] = level;
        }
    }
    const value: GuestSnapshotValue = {
        theme: guestText(request && request.theme),
        decor: guestTexts(request && request.decor),
        monuments: monuments,
        title: guestText(request && request.title),
        trophies: guestTexts(request && request.trophies),
        brand: guestText(request && request.brand),
        at: nowUnixMs
    };
    nk.storageWrite([{ collection: GuestCollection, key: GuestSnapshotKey, userId: userId, value: value as any, permissionRead: 2, permissionWrite: 0 }]);
    return { published: true };
}

function guestSnapshot(nk: nkruntime.Nakama, host: string): GuestSnapshotValue | null {
    const objects = nk.storageRead([{ collection: GuestCollection, key: GuestSnapshotKey, userId: host }]);
    return objects.length === 0 ? null : objects[0].value as GuestSnapshotValue;
}

function guestVisits(nk: nkruntime.Nakama, userId: string, day: number): { value: GuestVisits; version: string } {
    const objects = nk.storageRead([{ collection: GuestCollection, key: GuestVisitsKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { day: day, done: {} }, version: "*" };
    const value = objects[0].value as GuestVisits;
    return { value: value.day === day ? value : { day: day, done: {} }, version: objects[0].version };
}

function guestHost(nk: nkruntime.Nakama, userId: string, host: any, nowUnixMs: number): GuestSnapshotValue {
    if (typeof host !== "string" || host.length === 0)
        throw new Error("bad request");
    if (host === userId)
        throw new Error("not your own hideout");
    const snapshot = guestSnapshot(nk, host);
    if (!snapshot)
        throw new Error("no such hideout");
    if (!circleClose(nk, userId, host, nowUnixMs))
        throw new Error("guests come only from friends, clan and party");
    return snapshot;
}

function guestLeft(done: string[]): string[] {
    return GuestActs.filter((act) => done.indexOf(act) < 0);
}

function hideoutVisit(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const host = request && request.host;
    const snapshot = guestHost(nk, userId, host, nowUnixMs);
    const visits = guestVisits(nk, userId, walletDayOf(nowUnixMs)).value;
    return { host: host, hideout: snapshot, left: guestLeft(visits.done[host] || []) };
}

function guestInboxCount(nk: nkruntime.Nakama, host: string): number {
    let count = 0;
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(host, GuestActsCollection, 100, cursor);
        count += (page.objects || []).length;
        cursor = page.cursor || undefined;
    } while (cursor);
    return count;
}

function hideoutGuest(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const host = request && request.host;
    const act = request && request.act;
    guestHost(nk, userId, host, nowUnixMs);
    if (GuestActs.indexOf(act) < 0)
        throw new Error("bad request");
    const bed = request.bed === undefined ? -1 : request.bed;
    if (typeof bed !== "number" || bed !== Math.floor(bed) || bed < -1 || bed > 100)
        throw new Error("bad request");
    if (act !== "gift" && guestInboxCount(nk, host) >= GuestServerRules.inboxLimit)
        throw new Error("the hideout is busy");
    const day = walletDayOf(nowUnixMs);
    let left: string[] = [];
    for (let attempt = 1; attempt <= GuestWriteAttempts; attempt++) {
        const stored = guestVisits(nk, userId, day);
        const done = stored.value.done[host] || [];
        if (done.indexOf(act) >= 0)
            throw new Error("already done today");
        stored.value.done[host] = done.concat([act]);
        try {
            nk.storageWrite([{ collection: GuestCollection, key: GuestVisitsKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            left = guestLeft(stored.value.done[host]);
            break;
        } catch (error) {
            if (attempt === GuestWriteAttempts)
                throw new Error("the hideout is busy, try again");
        }
    }
    let friendship = 0;
    if (act === "gift") {
        friendship = walletGrant(nk, userId, "camp_gift", 1, nowUnixMs);
        walletGrant(nk, host, "camp_gift", 1, nowUnixMs);
    }
    const users = nk.usersGetId([userId]);
    const action: GuestAction = {
        id: userId + "_" + day + "_" + act,
        from: userId,
        fromName: users.length > 0 ? users[0].displayName || users[0].username : "",
        act: act,
        bed: bed,
        at: nowUnixMs
    };
    nk.storageWrite([{ collection: GuestActsCollection, key: action.id, userId: host, value: action as any, version: "*", permissionRead: 1, permissionWrite: 0 }]);
    return { left: left, friendship: friendship };
}

function hideoutGuestsClaim(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const actions: GuestAction[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, GuestActsCollection, 100, cursor);
        for (const object of page.objects || [])
            actions.push(object.value as GuestAction);
        cursor = page.cursor || undefined;
    } while (cursor);
    actions.sort((a, b) => a.at - b.at || (a.id < b.id ? -1 : 1));
    if (actions.length > 0)
        nk.storageDelete(actions.map((action) => ({ collection: GuestActsCollection, key: action.id, userId: userId })));
    return { guests: actions };
}

function hideoutPublishRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutPublish(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function hideoutVisitRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutVisit(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function hideoutGuestRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutGuest(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function hideoutGuestsClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutGuestsClaim(nk, partyCaller(ctx)));
}
