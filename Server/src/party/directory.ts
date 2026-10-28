interface PartyDirectoryConfig {
    maxMembers: number;
    codeLength: number;
    inactiveDays: number;
    listLimit: number;
    titles: string[];
    roles: string[];
    languages: string[];
    reportReasons: string[];
    timeSlots: { id: string; fromHour: number }[];
}

interface PartyMeta {
    kind: string;
    code: string;
    access: string;
    title: string;
    language: string;
    wanted: string;
    leader: string;
    members: string[];
    hours: number[];
    hoursAt: number;
}

declare const PartyDirectoryRules: PartyDirectoryConfig;

const PartyCodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
const PartySystemUser = "00000000-0000-0000-0000-000000000000";
const PartySuperadmin = 0;
const PartyKind = "party";
const PartyGroupPrefix = "party-";
const PartyOpen = "open";
const PartyInvite = "invite";
const PartyAnyRole = "any";
const PartyDayMs = 86400000;
const PartyHoursHalfLifeDays = 7;
const PartyMaxBlocks = 200;

function partyRequest(payload: string): { [key: string]: any } {
    if (!payload)
        return {};
    const parsed = JSON.parse(payload);
    if (parsed === null || typeof parsed !== "object")
        throw new Error("bad request");
    return parsed;
}

function partyCaller(ctx: nkruntime.Context): string {
    if (!ctx.userId)
        throw new Error("no user");
    return ctx.userId;
}

function partyPick(value: any, allowed: string[], fallback: string): string {
    if (value === undefined || value === null)
        return fallback;
    if (typeof value !== "string" || allowed.indexOf(value) < 0)
        throw new Error("bad request");
    return value;
}

function partyIsParty(group: nkruntime.Group | null | undefined): boolean {
    return !!group && !!group.metadata && group.metadata.kind === PartyKind;
}

function partyMetaOf(group: nkruntime.Group): PartyMeta {
    return group.metadata as PartyMeta;
}

function partyNative(group: nkruntime.Group): nkruntime.Group {
    return {
        id: group.id, name: group.name, langTag: group.langTag, open: group.open, edgeCount: group.edgeCount, maxCount: group.maxCount,
        metadata: JSON.parse(JSON.stringify(group.metadata || {}))
    };
}

function partyLoad(nk: nkruntime.Nakama, source: nkruntime.Group): nkruntime.Group | null {
    const group = partyNative(source);
    const meta = partyMetaOf(group);
    const present = (nk.groupUsersList(group.id, 100).groupUsers || []).map((entry) => entry.user.userId);
    const hint = Array.isArray(meta.members) ? meta.members : [];
    const members = hint.filter((userId) => present.indexOf(userId) >= 0);
    for (const userId of present)
        if (members.indexOf(userId) < 0)
            members.push(userId);
    if (members.length === 0) {
        partyDissolve(nk, group.id, meta.code);
        return null;
    }
    meta.members = members;
    if (members.indexOf(meta.leader) < 0)
        meta.leader = members[0];
    return group;
}

function partyDissolve(nk: nkruntime.Nakama, groupId: string, code: string): void {
    nk.storageDelete([
        { collection: "party_codes", key: code, userId: PartySystemUser },
        { collection: "party_match", key: groupId, userId: PartySystemUser }
    ]);
    nk.groupDelete(groupId);
}

function partyGroupById(nk: nkruntime.Nakama, groupId: string): nkruntime.Group | null {
    const groups = nk.groupsGetId([groupId]);
    return groups.length > 0 && partyIsParty(groups[0]) ? partyLoad(nk, groups[0]) : null;
}

function partyGroupOfUser(nk: nkruntime.Nakama, userId: string): nkruntime.Group | null {
    const list = nk.userGroupsList(userId, 100);
    for (const entry of list.userGroups || [])
        if (partyIsParty(entry.group))
            return partyLoad(nk, entry.group!);
    return null;
}

function partySaveMeta(nk: nkruntime.Nakama, group: nkruntime.Group, meta: PartyMeta): void {
    nk.groupUpdate(group.id, null, null, null, meta.language, null, null, meta.access === PartyOpen, meta, null);
    group.metadata = meta;
    group.open = meta.access === PartyOpen;
}

function partyRandomCode(length: number): string {
    let code = "";
    for (let i = 0; i < length; i++)
        code += PartyCodeAlphabet.charAt(Math.floor(Math.random() * PartyCodeAlphabet.length));
    return code;
}

function partyNormalizeCode(value: any): string {
    if (typeof value !== "string")
        throw new Error("bad request");
    return value.toUpperCase().replace(/[^0-9A-Z]/g, "");
}

function partyReserveCode(nk: nkruntime.Nakama, groupId: string): string {
    for (let attempt = 0; attempt < 20; attempt++) {
        const code = partyRandomCode(PartyDirectoryRules.codeLength);
        try {
            nk.storageWrite([{ collection: "party_codes", key: code, userId: PartySystemUser, value: { partyId: groupId }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
            return code;
        } catch (error) {
            continue;
        }
    }
    throw new Error("no free party code");
}

function partyGroupByCode(nk: nkruntime.Nakama, code: string): nkruntime.Group | null {
    const objects = nk.storageRead([{ collection: "party_codes", key: code, userId: PartySystemUser }]);
    return objects.length > 0 ? partyGroupById(nk, objects[0].value.partyId) : null;
}

function partyNoteMember(nk: nkruntime.Nakama, userId: string, partyId: string, matchId: string, slot: number): void {
    nk.storageWrite([{
        collection: "party", key: "current", userId: userId,
        value: { partyId: partyId, matchId: matchId, slot: slot, seen: Date.now() }, permissionRead: 1, permissionWrite: 0
    }]);
}

function partyLastSeen(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: number } {
    const seen: { [userId: string]: number } = {};
    if (userIds.length === 0)
        return seen;
    const objects = nk.storageRead(userIds.map((userId) => ({ collection: "party", key: "current", userId: userId })));
    for (const object of objects)
        seen[object.userId] = Number(object.value.seen) || 0;
    return seen;
}

function partyView(nk: nkruntime.Nakama, group: nkruntime.Group): { [key: string]: any } {
    const meta = partyMetaOf(group);
    const users = meta.members.length > 0 ? nk.usersGetId(meta.members) : [];
    const byId: { [userId: string]: nkruntime.User } = {};
    for (const user of users)
        byId[user.userId] = user;
    const live = partyLiveMatchId(nk, group.id);
    return {
        id: group.id,
        code: meta.code,
        access: meta.access,
        title: meta.title,
        language: meta.language,
        wanted: meta.wanted,
        leader: meta.leader,
        maxMembers: PartyDirectoryRules.maxMembers,
        matchId: live,
        members: meta.members.map((userId) => {
            const user = byId[userId];
            return { userId: userId, name: user ? user.displayName || user.username : "", steamId: user ? user.steamId || "" : "", leader: userId === meta.leader };
        })
    };
}

function partyLiveMatchId(nk: nkruntime.Nakama, partyId: string): string | null {
    const objects = nk.storageRead([{ collection: "party_match", key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return null;
    const matchId = objects[0].value.matchId;
    return typeof matchId === "string" && nk.matchGet(matchId) ? matchId : null;
}

function partyConnectMatch(nk: nkruntime.Nakama, partyId: string): string {
    for (let attempt = 0; attempt < 3; attempt++) {
        const objects = nk.storageRead([{ collection: "party_match", key: partyId, userId: PartySystemUser }]);
        const stored = objects.length > 0 ? objects[0] : null;
        if (stored && typeof stored.value.matchId === "string" && nk.matchGet(stored.value.matchId))
            return stored.value.matchId;
        const matchId = nk.matchCreate(PartyMatchName, { partyId: partyId });
        try {
            nk.storageWrite([{
                collection: "party_match", key: partyId, userId: PartySystemUser, value: { matchId: matchId },
                version: stored ? stored.version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return matchId;
        } catch (error) {
            continue;
        }
    }
    throw new Error("party match is busy, try again");
}

function partyEnsureFree(nk: nkruntime.Nakama, userId: string): void {
    if (partyGroupOfUser(nk, userId))
        throw new Error("already in a party");
}

function partyCreateFor(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }): nkruntime.Group {
    partyEnsureFree(nk, userId);
    const rules = PartyDirectoryRules;
    const access = partyPick(request.access, [PartyOpen, PartyInvite], PartyInvite);
    const title = partyPick(request.title, rules.titles, rules.titles[Math.floor(Math.random() * rules.titles.length)]);
    const language = partyPick(request.language, rules.languages, rules.languages[0]);
    const wanted = partyPick(request.wanted, rules.roles, PartyAnyRole);
    const meta: PartyMeta = {
        kind: PartyKind, code: "", access: access, title: title, language: language, wanted: wanted,
        leader: userId, members: [userId], hours: partyEmptyHours(), hoursAt: Date.now()
    };
    const group = partyNative(nk.groupCreate(userId, PartyGroupPrefix + nk.uuidv4(), userId, language, "", "", access === PartyOpen, meta, rules.maxMembers));
    meta.code = partyReserveCode(nk, group.id);
    partySaveMeta(nk, group, meta);
    partyNoteMember(nk, userId, group.id, "", PartyNoSlot);
    return group;
}

function partyAdd(nk: nkruntime.Nakama, group: nkruntime.Group, userId: string): nkruntime.Group {
    const meta = partyMetaOf(group);
    if (meta.members.indexOf(userId) >= 0)
        return group;
    partyEnsureFree(nk, userId);
    if (meta.members.length >= PartyDirectoryRules.maxMembers)
        throw new Error("party is full");
    try {
        nk.groupUsersAdd(group.id, [userId]);
    } catch (error) {
        throw new Error("party is full");
    }
    meta.members.push(userId);
    partySaveMeta(nk, group, meta);
    partyNoteMember(nk, userId, group.id, "", PartyNoSlot);
    return group;
}

function partyRemove(nk: nkruntime.Nakama, group: nkruntime.Group, userId: string): void {
    const meta = partyMetaOf(group);
    const index = meta.members.indexOf(userId);
    if (index < 0)
        throw new Error("not a member");
    meta.members.splice(index, 1);
    nk.storageDelete([{ collection: "party", key: "current", userId: userId }]);
    const live = partyLiveMatchId(nk, group.id);
    if (live)
        nk.matchSignal(live, "leave:" + userId);
    if (meta.members.length === 0) {
        partyDissolve(nk, group.id, meta.code);
        return;
    }
    if (meta.leader === userId)
        meta.leader = meta.members[0];
    partyHandOverOwner(nk, group.id, userId, meta.leader);
    nk.groupUsersKick(group.id, [userId]);
    partySaveMeta(nk, group, meta);
}

function partyHandOverOwner(nk: nkruntime.Nakama, groupId: string, leaving: string, heir: string): void {
    const users = nk.groupUsersList(groupId, 100).groupUsers || [];
    let leavingOwns = false;
    let otherOwner = false;
    let heirState = PartySuperadmin;
    for (const entry of users) {
        if (entry.state !== PartySuperadmin) {
            if (entry.user.userId === heir)
                heirState = entry.state === undefined ? 2 : entry.state;
            continue;
        }
        if (entry.user.userId === leaving)
            leavingOwns = true;
        else
            otherOwner = true;
    }
    if (!leavingOwns || otherOwner)
        return;
    for (let step = heirState; step > PartySuperadmin; step--)
        nk.groupUsersPromote(groupId, [heir]);
}

function partyLedBy(nk: nkruntime.Nakama, userId: string): nkruntime.Group {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    if (partyMetaOf(group).leader !== userId)
        throw new Error("only the leader can do that");
    return group;
}

function partyEmptyHours(): number[] {
    const hours: number[] = [];
    for (let i = 0; i < 24; i++)
        hours.push(0);
    return hours;
}

function partyNoteHour(meta: PartyMeta, now: number): void {
    const days = Math.max(0, now - (meta.hoursAt || now)) / PartyDayMs;
    const decay = Math.pow(0.5, days / PartyHoursHalfLifeDays);
    const hours = meta.hours && meta.hours.length === 24 ? meta.hours : partyEmptyHours();
    for (let i = 0; i < 24; i++)
        hours[i] *= decay;
    hours[new Date(now).getUTCHours()] += 1;
    meta.hours = hours;
    meta.hoursAt = now;
}

function partyUsualSlot(meta: PartyMeta, utcOffsetMinutes: number): string | null {
    const slots = PartyDirectoryRules.timeSlots;
    const totals: number[] = slots.map(() => 0);
    let any = false;
    for (let utcHour = 0; utcHour < 24; utcHour++) {
        const weight = meta.hours ? meta.hours[utcHour] || 0 : 0;
        if (!(weight > 0))
            continue;
        any = true;
        const local = (((utcHour + Math.round(utcOffsetMinutes / 60)) % 24) + 24) % 24;
        let slot = slots.length - 1;
        for (let i = 0; i < slots.length; i++)
            if (slots[i].fromHour <= local)
                slot = i;
        totals[slot] += weight;
    }
    if (!any)
        return null;
    let best = 0;
    for (let i = 1; i < totals.length; i++)
        if (totals[i] > totals[best])
            best = i;
    return slots[best].id;
}

function partyBlocksOf(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: string[] } {
    const blocks: { [userId: string]: string[] } = {};
    if (userIds.length === 0)
        return blocks;
    const objects = nk.storageRead(userIds.map((userId) => ({ collection: "party_blocks", key: "list", userId: userId })));
    for (const object of objects)
        blocks[object.userId] = Array.isArray(object.value.users) ? object.value.users : [];
    return blocks;
}

function partyPruneInactive(nk: nkruntime.Nakama, group: nkruntime.Group, now: number): boolean {
    const meta = partyMetaOf(group);
    if (meta.access !== PartyOpen)
        return true;
    const seen = partyLastSeen(nk, meta.members);
    const limit = now - PartyDirectoryRules.inactiveDays * PartyDayMs;
    for (const userId of meta.members.slice()) {
        if ((seen[userId] || 0) >= limit)
            continue;
        partyRemove(nk, group, userId);
        if (meta.members.length === 0)
            return false;
    }
    return true;
}

interface PartyCandidate { group: nkruntime.Group; card: { [key: string]: any }; score: number; }

function partyCandidates(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }): PartyCandidate[] {
    const rules = PartyDirectoryRules;
    const language = request.language === undefined ? undefined : partyPick(request.language, rules.languages, rules.languages[0]);
    const role = partyPick(request.role, rules.roles, PartyAnyRole);
    const wanted = request.wanted === undefined ? undefined : partyPick(request.wanted, rules.roles, PartyAnyRole);
    const offset = typeof request.utcOffsetMinutes === "number" && Math.abs(request.utcOffsetMinutes) <= 14 * 60 ? request.utcOffsetMinutes : 0;
    const slot = request.slot === undefined ? undefined : partyPick(request.slot, rules.timeSlots.map((s) => s.id), rules.timeSlots[0].id);
    const now = Date.now();
    const listed = nk.groupsList(undefined, language, true, undefined, 100).groups || [];
    const groups: nkruntime.Group[] = [];
    for (const source of listed) {
        const group = partyIsParty(source) ? partyLoad(nk, source) : null;
        if (group && partyMetaOf(group).members.indexOf(userId) < 0)
            groups.push(group);
    }
    const members: string[] = [userId];
    for (const group of groups)
        for (const member of partyMetaOf(group).members)
            members.push(member);
    const blocks = partyBlocksOf(nk, members);
    const mine = blocks[userId] || [];
    const result: PartyCandidate[] = [];
    for (const group of groups) {
        if (!partyPruneInactive(nk, group, now))
            continue;
        const meta = partyMetaOf(group);
        if (meta.members.length >= rules.maxMembers)
            continue;
        if (wanted !== undefined && meta.wanted !== wanted)
            continue;
        const blocked = meta.members.some((member) => mine.indexOf(member) >= 0 || (blocks[member] || []).indexOf(userId) >= 0);
        if (blocked)
            continue;
        const usual = partyUsualSlot(meta, offset);
        if (slot !== undefined && usual !== slot)
            continue;
        const live = partyLiveMatchId(nk, group.id);
        const match = live ? nk.matchGet(live) : null;
        const activeNow = !!match && match.size > 0;
        const roleWanted = meta.wanted === PartyAnyRole || meta.wanted === role;
        result.push({
            group: group,
            score: (activeNow ? 4 : 0) + (roleWanted && meta.wanted !== PartyAnyRole ? 2 : 0) + (roleWanted ? 1 : 0),
            card: {
                id: group.id, title: meta.title, language: meta.language, wanted: meta.wanted, members: meta.members.length,
                maxMembers: rules.maxMembers, activeNow: activeNow, usuallyOnline: usual, wantsYou: roleWanted
            }
        });
    }
    result.sort((a, b) => b.score - a.score || b.card.members - a.card.members);
    return result;
}

function partyCreateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyView(nk, partyCreateFor(nk, partyCaller(ctx), partyRequest(payload))));
}

function partyMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const group = partyGroupOfUser(nk, partyCaller(ctx));
    return JSON.stringify({ party: group ? partyView(nk, group) : null });
}

function partyConnectRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const group = partyGroupOfUser(nk, partyCaller(ctx));
    if (!group)
        throw new Error("not in a party");
    return JSON.stringify({ matchId: partyConnectMatch(nk, group.id) });
}

function partyJoinRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    let group: nkruntime.Group | null;
    if (request.code !== undefined) {
        group = partyGroupByCode(nk, partyNormalizeCode(request.code));
        if (!group)
            throw new Error("unknown code");
    } else if (typeof request.partyId === "string") {
        group = partyGroupById(nk, request.partyId);
        if (!group || partyMetaOf(group).access !== PartyOpen)
            throw new Error("party not found");
    } else {
        throw new Error("bad request");
    }
    return JSON.stringify(partyView(nk, partyAdd(nk, group, userId)));
}

function partyLeaveRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    partyRemove(nk, group, userId);
    return JSON.stringify({ party: null });
}

function partyKickRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const target = partyRequest(payload).userId;
    const group = partyLedBy(nk, userId);
    if (typeof target !== "string" || target === userId)
        throw new Error("bad request");
    partyRemove(nk, group, target);
    return JSON.stringify(partyView(nk, group));
}

function partyPromoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const target = partyRequest(payload).userId;
    const group = partyLedBy(nk, userId);
    const meta = partyMetaOf(group);
    if (typeof target !== "string" || meta.members.indexOf(target) < 0)
        throw new Error("not a member");
    meta.leader = target;
    partySaveMeta(nk, group, meta);
    return JSON.stringify(partyView(nk, group));
}

function partyUpdateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const rules = PartyDirectoryRules;
    const request = partyRequest(payload);
    const group = partyLedBy(nk, partyCaller(ctx));
    const meta = partyMetaOf(group);
    meta.access = partyPick(request.access, [PartyOpen, PartyInvite], meta.access);
    meta.title = partyPick(request.title, rules.titles, meta.title);
    meta.language = partyPick(request.language, rules.languages, meta.language);
    meta.wanted = partyPick(request.wanted, rules.roles, meta.wanted);
    partySaveMeta(nk, group, meta);
    return JSON.stringify(partyView(nk, group));
}

function partyListRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const candidates = partyCandidates(nk, partyCaller(ctx), partyRequest(payload));
    return JSON.stringify({ parties: candidates.slice(0, PartyDirectoryRules.listLimit).map((candidate) => candidate.card) });
}

function partyQuickRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const current = partyGroupOfUser(nk, userId);
    if (current)
        return JSON.stringify(partyView(nk, current));
    const language = partyPick(request.language, PartyDirectoryRules.languages, PartyDirectoryRules.languages[0]);
    const candidates = partyCandidates(nk, userId, { language: language, role: request.role, utcOffsetMinutes: request.utcOffsetMinutes });
    for (const candidate of candidates) {
        try {
            return JSON.stringify(partyView(nk, partyAdd(nk, candidate.group, userId)));
        } catch (error) {
            continue;
        }
    }
    return JSON.stringify(partyView(nk, partyCreateFor(nk, userId, { access: PartyOpen, language: language })));
}

function partyBlockRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return partyChangeBlocks(ctx, nk, payload, true);
}

function partyUnblockRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return partyChangeBlocks(ctx, nk, payload, false);
}

function partyChangeBlocks(ctx: nkruntime.Context, nk: nkruntime.Nakama, payload: string, block: boolean): string {
    const userId = partyCaller(ctx);
    const target = partyRequest(payload).userId;
    if (typeof target !== "string" || target === userId)
        throw new Error("bad request");
    const users = (partyBlocksOf(nk, [userId])[userId] || []).filter((blocked) => blocked !== target);
    if (block)
        users.push(target);
    if (users.length > PartyMaxBlocks)
        users.splice(0, users.length - PartyMaxBlocks);
    nk.storageWrite([{ collection: "party_blocks", key: "list", userId: userId, value: { users: users }, permissionRead: 1, permissionWrite: 0 }]);
    return JSON.stringify({ blocked: users });
}

function partyReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const reason = partyPick(request.reason, PartyDirectoryRules.reportReasons, "");
    if (typeof request.userId !== "string" || request.userId === userId || reason === "")
        throw new Error("bad request");
    nk.storageWrite([{
        collection: "party_reports", key: request.userId + ":" + userId, userId: PartySystemUser,
        value: { target: request.userId, reporter: userId, reason: reason, at: Date.now() }, permissionRead: 0, permissionWrite: 0
    }]);
    logger.warn("party report: %s reported %s for %s", userId, request.userId, reason);
    return JSON.stringify({ ok: true });
}

function partyGuardGroup(nk: nkruntime.Nakama, groupId: string | undefined): boolean {
    if (!groupId)
        return false;
    const groups = nk.groupsGetId([groupId]);
    return groups.length > 0 && partyIsParty(groups[0]);
}

function partyBeforeCreateGroup(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, data: { name?: string }): any {
    return data.name && data.name.indexOf(PartyGroupPrefix) === 0 ? null : data;
}

function partyBeforeGroupChange(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, data: { groupId?: string }): any {
    return partyGuardGroup(nk, data.groupId) ? null : data;
}

function partyBeforeListGroups(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, data: { [key: string]: any }): any {
    return null;
}

function partyMatchMember(nk: nkruntime.Nakama, partyId: string, userId: string): boolean {
    const group = partyGroupById(nk, partyId);
    return !!group && partyMetaOf(group).members.indexOf(userId) >= 0;
}

function partyNoteVisit(nk: nkruntime.Nakama, logger: nkruntime.Logger, partyId: string, matchId: string, userId: string, slot: number): void {
    try {
        partyNoteMember(nk, userId, partyId, matchId, slot);
        const group = partyGroupById(nk, partyId);
        if (!group)
            return;
        const meta = partyMetaOf(group);
        partyNoteHour(meta, Date.now());
        partySaveMeta(nk, group, meta);
    } catch (error) {
        logger.warn("party visit was not saved for %s: %s", userId, String(error));
    }
}
