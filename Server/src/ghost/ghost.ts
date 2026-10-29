interface GhostRules {
    power: number;
    fullHours: number;
    minActivity: number;
    uploadMinutes: number;
    freshHours: number;
    pool: number;
    classes: number;
}

interface GhostRecord {
    heroClass: number;
    shareRate: number;
    marks: string[];
    updatedAt: number;
    hourSeconds: number[];
    hourIndex: number;
}

declare const GhostServerRules: GhostRules;

const GhostCollection = "ghost";
const GhostKey = "snapshot";
const GhostPoolCollection = "ghost_pool";
const GhostPoolKey = "recent";
const GhostHours = 24;
const GhostHourMs = 3600 * 1000;
const GhostMaxMarks = 10;
const GhostWriteAttempts = 3;
const GhostPickedKey = "picked";
const GhostMaxThanks = 100;

function ghostEmptyHours(): number[] {
    const hours: number[] = [];
    for (let i = 0; i < GhostHours; i++)
        hours.push(0);
    return hours;
}

function ghostRoll(record: GhostRecord, nowUnixMs: number): void {
    const hour = Math.floor(nowUnixMs / GhostHourMs);
    const passed = Math.min(GhostHours, Math.max(0, hour - record.hourIndex));
    for (let i = 1; i <= passed; i++)
        record.hourSeconds[(record.hourIndex + i) % GhostHours] = 0;
    record.hourIndex = Math.max(record.hourIndex, hour);
}

function ghostActivity(record: GhostRecord, nowUnixMs: number): number {
    const copy: GhostRecord = JSON.parse(JSON.stringify(record));
    ghostRoll(copy, nowUnixMs);
    let seconds = 0;
    for (const value of copy.hourSeconds)
        seconds += value;
    const rules = GhostServerRules;
    return Math.max(rules.minActivity, Math.min(1, seconds / 3600 / rules.fullHours));
}

function ghostFresh(record: GhostRecord, nowUnixMs: number): boolean {
    return nowUnixMs - record.updatedAt <= GhostServerRules.freshHours * GhostHourMs;
}

function ghostRead(nk: nkruntime.Nakama, userId: string): GhostRecord | null {
    const objects = nk.storageRead([{ collection: GhostCollection, key: GhostKey, userId: userId }]);
    return objects.length > 0 ? objects[0].value as GhostRecord : null;
}

function ghostPoolChange(nk: nkruntime.Nakama, userId: string, keep: boolean): void {
    for (let attempt = 1; attempt <= GhostWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: GhostPoolCollection, key: GhostPoolKey, userId: PartySystemUser }]);
        const users: string[] = objects.length > 0 ? (objects[0].value as { users: string[] }).users.filter((id) => id !== userId) : [];
        if (keep)
            users.push(userId);
        while (users.length > GhostServerRules.pool)
            users.shift();
        try {
            nk.storageWrite([{
                collection: GhostPoolCollection, key: GhostPoolKey, userId: PartySystemUser, value: { users: users },
                version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return;
        } catch (error) {
            continue;
        }
    }
}

function ghostMarks(value: any): string[] {
    if (!Array.isArray(value) || value.length > GhostMaxMarks)
        throw new Error("bad request");
    const marks: string[] = [];
    for (const mark of value) {
        if (typeof mark !== "string" || !/^[A-Za-z]{1,32}$/.test(mark))
            throw new Error("bad request");
        if (marks.indexOf(mark) < 0)
            marks.push(mark);
    }
    return marks;
}

function ghostUploadRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    if (request.share === false) {
        nk.storageDelete([{ collection: GhostCollection, key: GhostKey, userId: userId }]);
        ghostPoolChange(nk, userId, false);
        return JSON.stringify({ shared: false });
    }
    const rules = GhostServerRules;
    const heroClass = request.heroClass;
    const shareRate = request.shareRate;
    const activeSeconds = request.activeSeconds;
    if (typeof heroClass !== "number" || heroClass !== Math.floor(heroClass) || heroClass < 0 || heroClass >= rules.classes)
        throw new Error("bad request");
    if (typeof shareRate !== "number" || !(shareRate >= 0) || shareRate > 100)
        throw new Error("bad request");
    if (typeof activeSeconds !== "number" || !(activeSeconds >= 0))
        throw new Error("bad request");
    const marks = ghostMarks(request.marks);
    const now = Date.now();
    const record: GhostRecord = ghostRead(nk, userId) || { heroClass: heroClass, shareRate: 0, marks: [], updatedAt: now, hourSeconds: ghostEmptyHours(), hourIndex: Math.floor(now / GhostHourMs) };
    const sinceLast = Math.max(0, (now - record.updatedAt) / 1000);
    const allowed = Math.min(2 * rules.uploadMinutes * 60, sinceLast + 60);
    ghostRoll(record, now);
    record.hourSeconds[record.hourIndex % GhostHours] += Math.min(allowed, Math.floor(activeSeconds));
    record.heroClass = heroClass;
    record.shareRate = shareRate;
    record.marks = marks;
    record.updatedAt = now;
    nk.storageWrite([{ collection: GhostCollection, key: GhostKey, userId: userId, value: record as any, permissionRead: 0, permissionWrite: 0 }]);
    ghostPoolChange(nk, userId, true);
    return JSON.stringify({ shared: true, activity: ghostActivity(record, now) });
}

function ghostView(nk: nkruntime.Nakama, userId: string, record: GhostRecord, nowUnixMs: number): { [key: string]: any } {
    return {
        userId: userId, name: chronicleNameOf(nk, userId), heroClass: record.heroClass, shareRate: record.shareRate,
        marks: record.marks, activity: ghostActivity(record, nowUnixMs)
    };
}

function ghostCandidates(nk: nkruntime.Nakama, userId: string): string[] {
    const group = partyGroupOfUser(nk, userId);
    const party = group ? partyMetaOf(group).members.filter((id) => id !== userId) : [];
    if (party.length > 0)
        return party;
    const objects = nk.storageRead([{ collection: GhostPoolCollection, key: GhostPoolKey, userId: PartySystemUser }]);
    return objects.length > 0 ? (objects[0].value as { users: string[] }).users.filter((id) => id !== userId) : [];
}

function ghostPickRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    const blocked = partyBlocksOf(nk, [userId])[userId] || [];
    const wanted = typeof request.userId === "string" && request.userId.length > 0 ? [request.userId] : ghostCandidates(nk, userId);
    const fresh: { id: string; record: GhostRecord }[] = [];
    for (const id of wanted) {
        if (id === userId || blocked.indexOf(id) >= 0)
            continue;
        const record = ghostRead(nk, id);
        if (record !== null && ghostFresh(record, now))
            fresh.push({ id: id, record: record });
    }
    if (fresh.length === 0)
        return JSON.stringify({ ghost: null });
    const picked = fresh[Math.floor(Math.random() * fresh.length)];
    ghostNotePicked(nk, userId, picked.id, now);
    return JSON.stringify({ ghost: ghostView(nk, picked.id, picked.record, now) });
}

function ghostNotePicked(nk: nkruntime.Nakama, userId: string, ownerId: string, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: GhostCollection, key: GhostPickedKey, userId: userId }]);
    const previous = objects.length > 0 ? objects[0].value : null;
    const thanked: { [ownerId: string]: number } = previous && previous.day === walletDayOf(nowUnixMs) && previous.thanked ? previous.thanked : {};
    nk.storageWrite([{
        collection: GhostCollection, key: GhostPickedKey, userId: userId, value: { ownerId: ownerId, at: nowUnixMs, day: walletDayOf(nowUnixMs), thanked: thanked },
        permissionRead: 0, permissionWrite: 0
    }]);
}

function ghostThanksRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const bosses = request.bosses;
    if (typeof request.userId !== "string" || typeof bosses !== "number" || bosses !== Math.floor(bosses) || bosses < 1 || bosses > GhostMaxThanks)
        throw new Error("bad request");
    const now = Date.now();
    const objects = nk.storageRead([{ collection: GhostCollection, key: GhostPickedKey, userId: userId }]);
    const picked = objects.length > 0 ? objects[0].value : null;
    if (!picked || picked.ownerId !== request.userId)
        throw new Error("not your ghost");
    const day = walletDayOf(now);
    const thanked: { [ownerId: string]: number } = picked.day === day && picked.thanked ? picked.thanked : {};
    const source = walletSource("ghost_helped");
    const room = source.perFriendDailyCap > 0 ? Math.max(0, source.perFriendDailyCap - (thanked[picked.ownerId] || 0)) : bosses;
    const counted = Math.min(bosses, room);
    const granted = counted > 0 ? walletGrant(nk, picked.ownerId, source.id, counted, now) : 0;
    thanked[picked.ownerId] = (thanked[picked.ownerId] || 0) + counted;
    nk.storageWrite([{
        collection: GhostCollection, key: GhostPickedKey, userId: userId, value: { ownerId: picked.ownerId, at: picked.at, day: day, thanked: thanked },
        version: objects[0].version, permissionRead: 0, permissionWrite: 0
    }]);
    return JSON.stringify({ granted: granted });
}
