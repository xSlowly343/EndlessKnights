interface ClanRequestEntry {
    userId: string;
    at: number;
}

interface ClanRequestList {
    requests: ClanRequestEntry[];
}

interface ClanApplicationList {
    clans: { clanId: string; at: number }[];
}

const ClanRequestsCollection = "clan_requests";
const ClanApplicationsKey = "applications";
const ClanRequestNotificationCode = 121;
const ClanAnswerNotificationCode = 122;
const ClanRequestWriteAttempts = 4;

function clanRequestFresh(at: number, now: number): boolean {
    return now - at < ClanServerRules.requestDays * PartyDayMs;
}

function clanRequestsChange<T>(nk: nkruntime.Nakama, clanId: string, now: number, change: (list: ClanRequestList) => T): T {
    for (let attempt = 1; attempt <= ClanRequestWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: ClanRequestsCollection, key: clanId, userId: PartySystemUser }]);
        const list = objects.length > 0 ? objects[0].value as ClanRequestList : { requests: [] };
        list.requests = list.requests.filter((request) => clanRequestFresh(request.at, now));
        const result = change(list);
        try {
            nk.storageWrite([{
                collection: ClanRequestsCollection, key: clanId, userId: PartySystemUser, value: list as any,
                version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return result;
        } catch (error) {
            if (attempt === ClanRequestWriteAttempts)
                throw new Error("clan is busy, try again");
        }
    }
    throw new Error("clan is busy, try again");
}

function clanApplicationsOf(nk: nkruntime.Nakama, userId: string, now: number): ClanApplicationList {
    const objects = nk.storageRead([{ collection: ClanCollection, key: ClanApplicationsKey, userId: userId }]);
    const list = objects.length > 0 ? objects[0].value as ClanApplicationList : { clans: [] };
    list.clans = list.clans.filter((entry) => clanRequestFresh(entry.at, now));
    return list;
}

function clanSaveApplications(nk: nkruntime.Nakama, userId: string, list: ClanApplicationList): void {
    nk.storageWrite([{ collection: ClanCollection, key: ClanApplicationsKey, userId: userId, value: list as any, permissionRead: 1, permissionWrite: 0 }]);
}

function clanDropApplication(nk: nkruntime.Nakama, userId: string, clanId: string, now: number): void {
    const list = clanApplicationsOf(nk, userId, now);
    list.clans = list.clans.filter((entry) => entry.clanId !== clanId);
    clanSaveApplications(nk, userId, list);
}

function clanForgetApplications(nk: nkruntime.Nakama, userId: string, now: number): void {
    const list = clanApplicationsOf(nk, userId, now);
    if (list.clans.length === 0)
        return;
    for (const entry of list.clans) {
        try {
            clanRequestsChange(nk, entry.clanId, now, (requests) => {
                requests.requests = requests.requests.filter((request) => request.userId !== userId);
                return true;
            });
        } catch (error) {
            continue;
        }
    }
    clanSaveApplications(nk, userId, { clans: [] });
}

function clanStaffOf(meta: ClanMeta): string[] {
    return [meta.leader].concat(meta.officers);
}

function clanStaffed(nk: nkruntime.Nakama, userId: string, now: number): nkruntime.Group {
    const group = clanOf(nk, userId, now);
    if (clanRoleOf(clanMetaOf(group), userId) === ClanMember)
        throw new Error("only officers can do that");
    return group;
}

function clanBlocksBetween(nk: nkruntime.Nakama, userId: string, members: string[]): boolean {
    const blocks = partyBlocksOf(nk, [userId].concat(members));
    const mine = blocks[userId] || [];
    return members.some((member) => mine.indexOf(member) >= 0 || (blocks[member] || []).indexOf(userId) >= 0);
}

function clanApplyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    if (typeof request.clanId !== "string")
        throw new Error("bad request");
    clanEnsureFree(nk, userId, now);
    const group = clanGroupById(nk, request.clanId, now);
    if (!group || clanBlocksBetween(nk, userId, clanMetaOf(group).members))
        throw new Error("clan not found");
    const meta = clanMetaOf(group);
    if (meta.access === PartyOpen)
        throw new Error("clan is open");
    if (meta.members.length >= ClanServerRules.maxMembers)
        throw new Error("clan is full");
    const applications = clanApplicationsOf(nk, userId, now);
    const already = applications.clans.some((entry) => entry.clanId === group.id);
    if (!already && applications.clans.length >= ClanServerRules.requestsPerPlayer)
        throw new Error("too many requests");
    const added = clanRequestsChange(nk, group.id, now, (list) => {
        if (list.requests.some((entry) => entry.userId === userId))
            return false;
        if (list.requests.length >= ClanServerRules.requestsPerClan)
            throw new Error("clan has too many requests");
        list.requests.push({ userId: userId, at: now });
        return true;
    });
    if (!already) {
        applications.clans.push({ clanId: group.id, at: now });
        clanSaveApplications(nk, userId, applications);
    }
    if (added)
        for (const staff of clanStaffOf(meta)) {
            try {
                nk.notificationSend(staff, "clan_request", { clanId: group.id, from: userId }, ClanRequestNotificationCode, userId, false);
            } catch (error) {
                logger.warn("clan request was not announced to %s: %s", staff, String(error));
            }
        }
    return JSON.stringify({ clanId: group.id, adjective: meta.adjective, noun: meta.noun, until: now + ClanServerRules.requestDays * PartyDayMs });
}

function clanRequestsView(nk: nkruntime.Nakama, group: nkruntime.Group, now: number): { [key: string]: any } {
    const list = clanRequestsChange(nk, group.id, now, (value) => value);
    const ids = list.requests.map((entry) => entry.userId);
    const users = ids.length > 0 ? nk.usersGetId(ids) : [];
    const names: { [userId: string]: string } = {};
    for (const user of users)
        names[user.userId] = user.displayName || user.username;
    return {
        clanId: group.id, days: ClanServerRules.requestDays,
        requests: list.requests.map((entry) => ({ userId: entry.userId, name: names[entry.userId] || "", at: entry.at, until: entry.at + ClanServerRules.requestDays * PartyDayMs }))
    };
}

function clanRequestsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const now = Date.now();
    return JSON.stringify(clanRequestsView(nk, clanStaffed(nk, partyCaller(ctx), now), now));
}

function clanAnswerRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    if (typeof request.userId !== "string" || typeof request.accept !== "boolean")
        throw new Error("bad request");
    const group = clanStaffed(nk, userId, now);
    const found = clanRequestsChange(nk, group.id, now, (list) => {
        const before = list.requests.length;
        list.requests = list.requests.filter((entry) => entry.userId !== request.userId);
        return list.requests.length < before;
    });
    if (!found)
        throw new Error("no such request");
    clanDropApplication(nk, request.userId, group.id, now);
    if (request.accept)
        clanAdd(nk, group, request.userId, now);
    try {
        nk.notificationSend(request.userId, "clan_answer", { clanId: group.id, accepted: request.accept }, ClanAnswerNotificationCode, userId, true);
    } catch (error) {
        logger.warn("clan answer was not sent to %s: %s", request.userId, String(error));
    }
    return JSON.stringify(clanRequestsView(nk, group, now));
}

function clanApplicationsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const list = clanApplicationsOf(nk, userId, now);
    const clans: { [key: string]: any }[] = [];
    for (const entry of list.clans) {
        const group = clanGroupById(nk, entry.clanId, now);
        if (!group)
            continue;
        const meta = clanMetaOf(group);
        clans.push({ clanId: group.id, adjective: meta.adjective, noun: meta.noun, members: meta.members.length, at: entry.at, until: entry.at + ClanServerRules.requestDays * PartyDayMs });
    }
    return JSON.stringify({ clans: clans });
}

function clanWithdrawRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    if (typeof request.clanId !== "string")
        throw new Error("bad request");
    clanRequestsChange(nk, request.clanId, now, (list) => {
        list.requests = list.requests.filter((entry) => entry.userId !== userId);
        return true;
    });
    clanDropApplication(nk, userId, request.clanId, now);
    return clanApplicationsRpc(ctx, logger, nk, "{}");
}
