interface ClanRules {
    maxMembers: number;
    maxOfficers: number;
    codeLength: number;
    listLimit: number;
    activeMinutes: number;
    inactiveDays: number;
    leaderAwayDays: number;
    adjectives: string[];
    nouns: string[];
    languages: string[];
}

interface ClanMeta {
    kind: string;
    code: string;
    access: string;
    adjective: string;
    noun: string;
    language: string;
    leader: string;
    officers: string[];
    members: string[];
}

declare const ClanServerRules: ClanRules;

const ClanKind = "clan";
const ClanGroupPrefix = "clan-";
const ClanCodes = "clan_codes";
const ClanCollection = "clan";
const ClanCurrentKey = "current";
const ClanMinuteMs = 60000;
const ClanLeader = "leader";
const ClanOfficer = "officer";
const ClanMember = "member";

function clanIsClan(group: nkruntime.Group | null | undefined): boolean {
    return !!group && !!group.metadata && group.metadata.kind === ClanKind;
}

function clanMetaOf(group: nkruntime.Group): ClanMeta {
    return group.metadata as ClanMeta;
}

function clanLoad(nk: nkruntime.Nakama, source: nkruntime.Group, now: number): nkruntime.Group | null {
    const group = partyNative(source);
    const meta = clanMetaOf(group);
    const present = (nk.groupUsersList(group.id, 100).groupUsers || []).map((entry) => entry.user.userId);
    const hint = Array.isArray(meta.members) ? meta.members : [];
    const members = hint.filter((userId) => present.indexOf(userId) >= 0);
    for (const userId of present)
        if (members.indexOf(userId) < 0)
            members.push(userId);
    if (members.length === 0) {
        clanDissolve(nk, group.id, meta.code);
        return null;
    }
    meta.members = members;
    meta.officers = (Array.isArray(meta.officers) ? meta.officers : []).filter((userId) => members.indexOf(userId) >= 0 && userId !== meta.leader);
    const leader = clanRightfulLeader(meta, clanLastSeen(nk, members), now);
    if (leader !== meta.leader)
        clanHandOver(nk, group, meta, leader);
    return group;
}

function clanRightfulLeader(meta: ClanMeta, seen: { [userId: string]: number }, now: number): string {
    const awayBefore = now - ClanServerRules.leaderAwayDays * PartyDayMs;
    const current = meta.members.indexOf(meta.leader) >= 0 ? meta.leader : "";
    if (current && (seen[current] || 0) >= awayBefore)
        return current;
    const officers = meta.officers.filter((userId) => (seen[userId] || 0) >= awayBefore);
    const pool = officers.length > 0 ? officers : meta.members.filter((userId) => userId !== current && (seen[userId] || 0) >= awayBefore);
    if (pool.length === 0)
        return current || meta.officers[0] || meta.members[0];
    return pool.reduce((best, userId) => ((seen[userId] || 0) > (seen[best] || 0) ? userId : best), pool[0]);
}

function clanHandOver(nk: nkruntime.Nakama, group: nkruntime.Group, meta: ClanMeta, heir: string): void {
    const previous = meta.leader;
    meta.officers = meta.officers.filter((userId) => userId !== heir);
    if (meta.members.indexOf(previous) >= 0 && previous !== heir && meta.officers.length < ClanServerRules.maxOfficers)
        meta.officers.push(previous);
    meta.leader = heir;
    if (meta.members.indexOf(previous) >= 0)
        partyHandOverOwner(nk, group.id, previous, heir);
    clanSaveMeta(nk, group, meta);
}

function clanDissolve(nk: nkruntime.Nakama, groupId: string, code: string): void {
    nk.storageDelete([{ collection: ClanCodes, key: code, userId: PartySystemUser }]);
    nk.groupDelete(groupId);
}

function clanSaveMeta(nk: nkruntime.Nakama, group: nkruntime.Group, meta: ClanMeta): void {
    nk.groupUpdate(group.id, null, null, null, meta.language, null, null, meta.access === PartyOpen, meta, null);
    group.metadata = meta;
    group.open = meta.access === PartyOpen;
    group.langTag = meta.language;
}

function clanGroupById(nk: nkruntime.Nakama, groupId: string, now: number): nkruntime.Group | null {
    const groups = nk.groupsGetId([groupId]);
    return groups.length > 0 && clanIsClan(groups[0]) ? clanLoad(nk, groups[0], now) : null;
}

function clanGroupOfUser(nk: nkruntime.Nakama, userId: string, now: number): nkruntime.Group | null {
    const list = nk.userGroupsList(userId, 100);
    for (const entry of list.userGroups || [])
        if (clanIsClan(entry.group))
            return clanLoad(nk, entry.group!, now);
    return null;
}

function clanReserveCode(nk: nkruntime.Nakama, groupId: string): string {
    for (let attempt = 0; attempt < 20; attempt++) {
        const code = partyRandomCode(ClanServerRules.codeLength);
        try {
            nk.storageWrite([{ collection: ClanCodes, key: code, userId: PartySystemUser, value: { clanId: groupId }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
            return code;
        } catch (error) {
            continue;
        }
    }
    throw new Error("no free clan code");
}

function clanGroupByCode(nk: nkruntime.Nakama, code: string, now: number): nkruntime.Group | null {
    const objects = nk.storageRead([{ collection: ClanCodes, key: code, userId: PartySystemUser }]);
    return objects.length > 0 ? clanGroupById(nk, objects[0].value.clanId, now) : null;
}

function clanNoteSeen(nk: nkruntime.Nakama, userId: string, clanId: string, now: number): void {
    nk.storageWrite([{ collection: ClanCollection, key: ClanCurrentKey, userId: userId, value: { clanId: clanId, seen: now }, permissionRead: 1, permissionWrite: 0 }]);
}

function clanLastSeen(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: number } {
    const seen: { [userId: string]: number } = {};
    if (userIds.length === 0)
        return seen;
    const objects = nk.storageRead(userIds.map((userId) => ({ collection: ClanCollection, key: ClanCurrentKey, userId: userId })));
    for (const object of objects)
        seen[object.userId] = Number(object.value.seen) || 0;
    return seen;
}

function clanRoleOf(meta: ClanMeta, userId: string): string {
    if (userId === meta.leader)
        return ClanLeader;
    return meta.officers.indexOf(userId) >= 0 ? ClanOfficer : ClanMember;
}

function clanActiveCount(seen: { [userId: string]: number }, userIds: string[], now: number): number {
    const since = now - ClanServerRules.activeMinutes * ClanMinuteMs;
    return userIds.filter((userId) => (seen[userId] || 0) >= since).length;
}

function clanRoleOrder(role: string): number {
    return role === ClanLeader ? 0 : role === ClanOfficer ? 1 : 2;
}

function clanView(nk: nkruntime.Nakama, group: nkruntime.Group, now: number): { [key: string]: any } {
    const meta = clanMetaOf(group);
    const users = nk.usersGetId(meta.members);
    const byId: { [userId: string]: nkruntime.User } = {};
    for (const user of users)
        byId[user.userId] = user;
    const seen = clanLastSeen(nk, meta.members);
    const members = meta.members.map((userId) => {
        const user = byId[userId];
        return { userId: userId, name: user ? user.displayName || user.username : "", steamId: user ? user.steamId || "" : "", role: clanRoleOf(meta, userId), seen: seen[userId] || 0 };
    });
    members.sort((a, b) => clanRoleOrder(a.role) - clanRoleOrder(b.role) || b.seen - a.seen);
    return {
        id: group.id, code: meta.code, access: meta.access, adjective: meta.adjective, noun: meta.noun, language: meta.language, leader: meta.leader,
        maxMembers: ClanServerRules.maxMembers, maxOfficers: ClanServerRules.maxOfficers, activeNow: clanActiveCount(seen, meta.members, now), members: members
    };
}

function clanEnsureFree(nk: nkruntime.Nakama, userId: string, now: number): void {
    if (clanGroupOfUser(nk, userId, now))
        throw new Error("already in a clan");
}

function clanCreateFor(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, now: number): nkruntime.Group {
    clanEnsureFree(nk, userId, now);
    const rules = ClanServerRules;
    const meta: ClanMeta = {
        kind: ClanKind, code: "",
        access: partyPick(request.access, [PartyOpen, PartyInvite], PartyInvite),
        adjective: partyPick(request.adjective, rules.adjectives, rules.adjectives[Math.floor(Math.random() * rules.adjectives.length)]),
        noun: partyPick(request.noun, rules.nouns, rules.nouns[Math.floor(Math.random() * rules.nouns.length)]),
        language: partyPick(request.language, rules.languages, rules.languages[0]),
        leader: userId, officers: [], members: [userId]
    };
    const group = partyNative(nk.groupCreate(userId, ClanGroupPrefix + nk.uuidv4(), userId, meta.language, "", "", meta.access === PartyOpen, meta, rules.maxMembers));
    meta.code = clanReserveCode(nk, group.id);
    clanSaveMeta(nk, group, meta);
    clanNoteSeen(nk, userId, group.id, now);
    return group;
}

function clanAdd(nk: nkruntime.Nakama, group: nkruntime.Group, userId: string, now: number): nkruntime.Group {
    const meta = clanMetaOf(group);
    if (meta.members.indexOf(userId) >= 0)
        return group;
    clanEnsureFree(nk, userId, now);
    if (meta.members.length >= ClanServerRules.maxMembers)
        throw new Error("clan is full");
    try {
        nk.groupUsersAdd(group.id, [userId]);
    } catch (error) {
        throw new Error("clan is full");
    }
    meta.members.push(userId);
    clanSaveMeta(nk, group, meta);
    clanNoteSeen(nk, userId, group.id, now);
    return group;
}

function clanRemove(nk: nkruntime.Nakama, group: nkruntime.Group, userId: string): void {
    const meta = clanMetaOf(group);
    const index = meta.members.indexOf(userId);
    if (index < 0)
        throw new Error("not a member");
    meta.members.splice(index, 1);
    meta.officers = meta.officers.filter((officer) => officer !== userId);
    nk.storageDelete([{ collection: ClanCollection, key: ClanCurrentKey, userId: userId }]);
    if (meta.members.length === 0) {
        clanDissolve(nk, group.id, meta.code);
        return;
    }
    if (meta.leader === userId) {
        meta.leader = meta.officers.length > 0 ? meta.officers.shift()! : meta.members[0];
        partyHandOverOwner(nk, group.id, userId, meta.leader);
    }
    nk.groupUsersKick(group.id, [userId]);
    clanSaveMeta(nk, group, meta);
}

function clanOf(nk: nkruntime.Nakama, userId: string, now: number): nkruntime.Group {
    const group = clanGroupOfUser(nk, userId, now);
    if (!group)
        throw new Error("not in a clan");
    return group;
}

function clanLedBy(nk: nkruntime.Nakama, userId: string, now: number): nkruntime.Group {
    const group = clanOf(nk, userId, now);
    if (clanMetaOf(group).leader !== userId)
        throw new Error("only the leader can do that");
    return group;
}

function clanTarget(meta: ClanMeta, request: { [key: string]: any }, caller: string): string {
    const target = request.userId;
    if (typeof target !== "string" || target === caller)
        throw new Error("bad request");
    if (meta.members.indexOf(target) < 0)
        throw new Error("not a member");
    return target;
}

function clanPruneInactive(nk: nkruntime.Nakama, group: nkruntime.Group, now: number): boolean {
    const meta = clanMetaOf(group);
    if (meta.access !== PartyOpen)
        return true;
    const seen = clanLastSeen(nk, meta.members);
    const limit = now - ClanServerRules.inactiveDays * PartyDayMs;
    for (const userId of meta.members.slice()) {
        if ((seen[userId] || 0) >= limit)
            continue;
        clanRemove(nk, group, userId);
        if (meta.members.length === 0)
            return false;
    }
    return true;
}

interface ClanCandidate { group: nkruntime.Group; card: { [key: string]: any }; }

function clanCandidates(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, now: number): ClanCandidate[] {
    const rules = ClanServerRules;
    const language = request.language === undefined ? undefined : partyPick(request.language, rules.languages, rules.languages[0]);
    const listed = nk.groupsList(undefined, language, true, undefined, 100).groups || [];
    const groups: nkruntime.Group[] = [];
    for (const source of listed) {
        const group = clanIsClan(source) ? clanLoad(nk, source, now) : null;
        if (group && clanMetaOf(group).members.indexOf(userId) < 0)
            groups.push(group);
    }
    const everyone: string[] = [userId];
    for (const group of groups)
        for (const member of clanMetaOf(group).members)
            everyone.push(member);
    const blocks = partyBlocksOf(nk, everyone);
    const mine = blocks[userId] || [];
    const result: ClanCandidate[] = [];
    for (const group of groups) {
        if (!clanPruneInactive(nk, group, now))
            continue;
        const meta = clanMetaOf(group);
        if (meta.members.length >= rules.maxMembers)
            continue;
        if (meta.members.some((member) => mine.indexOf(member) >= 0 || (blocks[member] || []).indexOf(userId) >= 0))
            continue;
        const activeNow = clanActiveCount(clanLastSeen(nk, meta.members), meta.members, now);
        result.push({
            group: group,
            card: { id: group.id, adjective: meta.adjective, noun: meta.noun, language: meta.language, members: meta.members.length, maxMembers: rules.maxMembers, activeNow: activeNow }
        });
    }
    result.sort((a, b) => b.card.activeNow - a.card.activeNow || b.card.members - a.card.members);
    return result;
}

function clanCreateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const now = Date.now();
    return JSON.stringify(clanView(nk, clanCreateFor(nk, partyCaller(ctx), partyRequest(payload), now), now));
}

function clanMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanGroupOfUser(nk, userId, now);
    if (!group)
        return JSON.stringify({ clan: null });
    clanNoteSeen(nk, userId, group.id, now);
    return JSON.stringify({ clan: clanView(nk, group, now) });
}

function clanJoinRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    let group: nkruntime.Group | null;
    if (request.code !== undefined) {
        group = clanGroupByCode(nk, partyNormalizeCode(request.code), now);
        if (!group)
            throw new Error("unknown code");
    } else if (typeof request.clanId === "string") {
        group = clanGroupById(nk, request.clanId, now);
        if (!group || clanMetaOf(group).access !== PartyOpen)
            throw new Error("clan not found");
    } else {
        throw new Error("bad request");
    }
    return JSON.stringify(clanView(nk, clanAdd(nk, group, userId, now), now));
}

function clanLeaveRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    clanRemove(nk, clanOf(nk, userId, Date.now()), userId);
    return JSON.stringify({ clan: null });
}

function clanKickRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    const meta = clanMetaOf(group);
    const target = clanTarget(meta, partyRequest(payload), userId);
    const mayKick = userId === meta.leader || (clanRoleOf(meta, userId) === ClanOfficer && clanRoleOf(meta, target) === ClanMember);
    if (!mayKick)
        throw new Error("only the leader can do that");
    clanRemove(nk, group, target);
    return JSON.stringify(clanView(nk, group, now));
}

function clanPromoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanLedBy(nk, userId, now);
    const meta = clanMetaOf(group);
    const target = clanTarget(meta, partyRequest(payload), userId);
    if (meta.officers.indexOf(target) < 0) {
        if (meta.officers.length >= ClanServerRules.maxOfficers)
            throw new Error("too many officers");
        meta.officers.push(target);
        clanSaveMeta(nk, group, meta);
    }
    return JSON.stringify(clanView(nk, group, now));
}

function clanDemoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanLedBy(nk, userId, now);
    const meta = clanMetaOf(group);
    const target = clanTarget(meta, partyRequest(payload), userId);
    meta.officers = meta.officers.filter((officer) => officer !== target);
    clanSaveMeta(nk, group, meta);
    return JSON.stringify(clanView(nk, group, now));
}

function clanLeadRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanLedBy(nk, userId, now);
    const meta = clanMetaOf(group);
    clanHandOver(nk, group, meta, clanTarget(meta, partyRequest(payload), userId));
    return JSON.stringify(clanView(nk, group, now));
}

function clanUpdateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const rules = ClanServerRules;
    const request = partyRequest(payload);
    const now = Date.now();
    const group = clanLedBy(nk, partyCaller(ctx), now);
    const meta = clanMetaOf(group);
    meta.access = partyPick(request.access, [PartyOpen, PartyInvite], meta.access);
    meta.adjective = partyPick(request.adjective, rules.adjectives, meta.adjective);
    meta.noun = partyPick(request.noun, rules.nouns, meta.noun);
    meta.language = partyPick(request.language, rules.languages, meta.language);
    clanSaveMeta(nk, group, meta);
    return JSON.stringify(clanView(nk, group, now));
}

function clanListRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const candidates = clanCandidates(nk, partyCaller(ctx), partyRequest(payload), Date.now());
    return JSON.stringify({ clans: candidates.slice(0, ClanServerRules.listLimit).map((candidate) => candidate.card) });
}

function clanQuickRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    const current = clanGroupOfUser(nk, userId, now);
    if (current)
        return JSON.stringify({ clan: clanView(nk, current, now) });
    const language = partyPick(request.language, ClanServerRules.languages, ClanServerRules.languages[0]);
    for (const candidate of clanCandidates(nk, userId, { language: language }, now)) {
        try {
            return JSON.stringify({ clan: clanView(nk, clanAdd(nk, candidate.group, userId, now), now) });
        } catch (error) {
            continue;
        }
    }
    return JSON.stringify({ clan: null });
}
