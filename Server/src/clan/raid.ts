interface RaidLayerRule {
    id: string;
    input: string;
    perMember: number;
}

interface RaidRules {
    boss: string;
    weekStartDay: number;
    weekUtcOffsetMinutes: number;
    pushSeconds: number;
    maxSharesPerSecond: number;
    minMembers: number;
    spillover: number;
    layers: RaidLayerRule[];
    sealsPerMember: number;
    killBonus: number;
    minSeals: number;
}

interface RaidLayer {
    id: string;
    hp: number;
    left: number;
}

interface RaidMemberTally {
    dealt: number;
    layers: { [layerId: string]: number };
    lastPush: number;
    lastSeconds: number;
    capped: number;
}

interface RaidValue {
    clanId: string;
    week: number;
    boss: string;
    members: number;
    layers: RaidLayer[];
    tally: { [userId: string]: RaidMemberTally };
    killedAt: number;
}

interface RaidResult {
    week: number;
    killed: boolean;
    removed: number;
    seals: { [userId: string]: number };
    places: string[];
}

declare const RaidServerRules: RaidRules;

const RaidCollection = "clan_raid";
const RaidLastCollection = "clan_raid_last";
const RaidWriteAttempts = 4;
const RaidIndexCollection = "clan_raid_index";
const RaidIndexKey = "pending";
const RaidSettlePerCall = 20;
const RaidPushSlackSeconds = 5;

function raidWeekOf(unixMs: number): number {
    const rules = RaidServerRules;
    const days = Math.floor((unixMs + rules.weekUtcOffsetMinutes * ClanMinuteMs) / PartyDayMs);
    return Math.floor((days + 4 - rules.weekStartDay) / 7);
}

function raidWeekStart(week: number): number {
    const rules = RaidServerRules;
    return (week * 7 - 4 + rules.weekStartDay) * PartyDayMs - rules.weekUtcOffsetMinutes * ClanMinuteMs;
}

function raidCreate(clanId: string, week: number, members: number): RaidValue {
    const rules = RaidServerRules;
    const counted = Math.max(rules.minMembers, members);
    return {
        clanId: clanId, week: week, boss: rules.boss, members: counted,
        layers: rules.layers.map((layer) => ({ id: layer.id, hp: layer.perMember * counted, left: layer.perMember * counted })),
        tally: {}, killedAt: 0
    };
}

function raidLeftShare(raid: RaidValue): number {
    let hp = 0;
    let left = 0;
    for (const layer of raid.layers) {
        hp += layer.hp;
        left += layer.left;
    }
    return hp > 0 ? left / hp : 0;
}

function raidHit(raid: RaidValue, layerId: string, amount: number): number {
    const layer = raid.layers.filter((candidate) => candidate.id === layerId)[0];
    if (!layer || !(amount > 0))
        return 0;
    const direct = Math.min(layer.left, amount);
    layer.left -= direct;
    const spare = (amount - direct) * RaidServerRules.spillover;
    const open = raid.layers.filter((candidate) => candidate.left > 0);
    let spilled = 0;
    for (const other of open) {
        const part = Math.min(other.left, spare / open.length);
        other.left -= part;
        spilled += part;
    }
    return direct + spilled;
}

function raidApply(raid: RaidValue, userId: string, push: { [layerId: string]: number }, seconds: number, nowUnixMs: number): number {
    const rules = RaidServerRules;
    const tally: RaidMemberTally = raid.tally[userId] || { dealt: 0, layers: {}, lastPush: 0, lastSeconds: 0, capped: 0 };
    const since = tally.lastPush > 0 ? (nowUnixMs - tally.lastPush) / 1000 + RaidPushSlackSeconds : 4 * rules.pushSeconds;
    const allowedSeconds = Math.max(0, Math.min(seconds, since, 4 * rules.pushSeconds));
    let total = 0;
    for (const layer of rules.layers)
        total += push[layer.id] || 0;
    const allowed = rules.maxSharesPerSecond * allowedSeconds;
    const scale = total > allowed ? allowed / total : 1;
    if (scale < 1)
        tally.capped++;
    let dealt = 0;
    if (raid.killedAt === 0) {
        for (const layer of rules.layers) {
            const done = raidHit(raid, layer.id, (push[layer.id] || 0) * scale);
            tally.layers[layer.id] = (tally.layers[layer.id] || 0) + done;
            dealt += done;
        }
        if (raidLeftShare(raid) <= 0)
            raid.killedAt = nowUnixMs;
    }
    tally.dealt += dealt;
    tally.lastPush = nowUnixMs;
    tally.lastSeconds = allowedSeconds;
    raid.tally[userId] = tally;
    return dealt;
}

function raidResult(raid: RaidValue): RaidResult {
    const rules = RaidServerRules;
    const killed = raid.killedAt > 0;
    const removed = 1 - raidLeftShare(raid);
    const places = Object.keys(raid.tally).filter((userId) => raid.tally[userId].dealt > 0)
        .sort((a, b) => raid.tally[b].dealt - raid.tally[a].dealt);
    let total = 0;
    for (const userId of places)
        total += raid.tally[userId].dealt;
    const pool = rules.sealsPerMember * raid.members * removed * (killed ? 1 + rules.killBonus : 1);
    const seals: { [userId: string]: number } = {};
    for (const userId of places)
        seals[userId] = Math.max(rules.minSeals, Math.round(pool * raid.tally[userId].dealt / total));
    return { week: raid.week, killed: killed, removed: removed, seals: seals, places: places };
}

function raidRead(nk: nkruntime.Nakama, clanId: string): { value: RaidValue | null; version: string } {
    const objects = nk.storageRead([{ collection: RaidCollection, key: clanId, userId: PartySystemUser }]);
    return objects.length > 0 ? { value: objects[0].value as RaidValue, version: objects[0].version } : { value: null, version: "*" };
}

function raidSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, raid: RaidValue, nowUnixMs: number): void {
    const result = raidResult(raid);
    try {
        nk.storageWrite([{ collection: RaidLastCollection, key: raid.clanId + ":" + raid.week, userId: PartySystemUser, value: result as any, version: "*", permissionRead: 0, permissionWrite: 0 }]);
    } catch (error) {
        return;
    }
    nk.storageWrite([{ collection: RaidLastCollection, key: raid.clanId, userId: PartySystemUser, value: result as any, permissionRead: 0, permissionWrite: 0 }]);
    for (const userId of result.places) {
        try {
            walletGrant(nk, userId, "clan_raid", result.seals[userId], nowUnixMs);
        } catch (error) {
            logger.warn("raid seals were not granted to %s: %s", userId, String(error));
        }
    }
    if (result.killed)
        clanChronicleNote(nk, logger, raid.clanId, { raidsKilled: 1 }, { at: nowUnixMs, kind: "raid", name: "", feat: "", level: 0, detail: raid.boss }, result.places);
    logger.info("clan %s: raid of week %d settled, killed %s, %d players", raid.clanId, raid.week, String(result.killed), result.places.length);
}

interface RaidIndex {
    raids: { clanId: string; week: number }[];
}

function raidIndexChange(nk: nkruntime.Nakama, change: (index: RaidIndex) => boolean): void {
    for (let attempt = 1; attempt <= RaidWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: RaidIndexCollection, key: RaidIndexKey, userId: PartySystemUser }]);
        const index = objects.length > 0 ? objects[0].value as RaidIndex : { raids: [] };
        if (!change(index))
            return;
        try {
            nk.storageWrite([{
                collection: RaidIndexCollection, key: RaidIndexKey, userId: PartySystemUser, value: index as any,
                version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return;
        } catch (error) {
            if (attempt === RaidWriteAttempts)
                return;
        }
    }
}

function raidNoteCreated(nk: nkruntime.Nakama, clanId: string, week: number): void {
    raidIndexChange(nk, (index) => {
        for (const raid of index.raids)
            if (raid.clanId === clanId && raid.week === week)
                return false;
        index.raids.push({ clanId: clanId, week: week });
        return true;
    });
}

function raidSettleDue(nk: nkruntime.Nakama, logger: nkruntime.Logger, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: RaidIndexCollection, key: RaidIndexKey, userId: PartySystemUser }]);
    if (objects.length === 0)
        return;
    const week = raidWeekOf(nowUnixMs);
    const due = (objects[0].value as RaidIndex).raids.filter((raid) => raid.week < week).slice(0, RaidSettlePerCall);
    if (due.length === 0)
        return;
    for (const entry of due) {
        const raid = raidRead(nk, entry.clanId).value;
        if (raid && raid.week === entry.week)
            raidSettle(nk, logger, raid, nowUnixMs);
    }
    raidIndexChange(nk, (index) => {
        index.raids = index.raids.filter((raid) => !due.some((done) => done.clanId === raid.clanId && done.week === raid.week));
        return true;
    });
}

function raidChange<T>(nk: nkruntime.Nakama, logger: nkruntime.Logger, group: nkruntime.Group, nowUnixMs: number, change: (raid: RaidValue) => T): T {
    const week = raidWeekOf(nowUnixMs);
    const members = clanMetaOf(group).members.length;
    for (let attempt = 1; attempt <= RaidWriteAttempts; attempt++) {
        const stored = raidRead(nk, group.id);
        let raid = stored.value;
        if (raid && raid.week !== week) {
            raidSettle(nk, logger, raid, nowUnixMs);
            raid = null;
        }
        const created = !raid;
        if (!raid)
            raid = raidCreate(group.id, week, members);
        const result = change(raid);
        try {
            nk.storageWrite([{ collection: RaidCollection, key: group.id, userId: PartySystemUser, value: raid as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            if (created)
                raidNoteCreated(nk, group.id, week);
            return result;
        } catch (error) {
            if (attempt === RaidWriteAttempts)
                throw new Error("raid is busy, try again");
        }
    }
    throw new Error("raid is busy, try again");
}

function raidView(nk: nkruntime.Nakama, raid: RaidValue, userId: string, last: RaidResult | null, nowUnixMs: number): { [key: string]: any } {
    const result = raidResult(raid);
    let total = 0;
    for (const id of result.places)
        total += raid.tally[id].dealt;
    const users = result.places.length > 0 ? nk.usersGetId(result.places) : [];
    const names: { [userId: string]: string } = {};
    for (const user of users)
        names[user.userId] = user.displayName || user.username;
    const own = raid.tally[userId];
    return {
        boss: raid.boss, week: raid.week, endsAt: raidWeekStart(raid.week + 1), members: raid.members, killedAt: raid.killedAt,
        left: raidLeftShare(raid),
        layers: raid.layers.map((layer) => ({ id: layer.id, hp: layer.hp, left: layer.left })),
        own: { share: own && total > 0 ? own.dealt / total : 0, place: result.places.indexOf(userId) + 1, players: result.places.length, seals: result.seals[userId] || 0 },
        top: result.places.map((id, index) => ({ place: index + 1, userId: id, name: names[id] || "", share: total > 0 ? raid.tally[id].dealt / total : 0 })),
        last: last ? { week: last.week, killed: last.killed, removed: last.removed, seals: last.seals[userId] || 0, place: last.places.indexOf(userId) + 1, players: last.places.length } : null
    };
}

function raidLast(nk: nkruntime.Nakama, clanId: string): RaidResult | null {
    const objects = nk.storageRead([{ collection: RaidLastCollection, key: clanId, userId: PartySystemUser }]);
    return objects.length > 0 ? objects[0].value as RaidResult : null;
}

function raidNumber(value: any): number {
    if (value === undefined)
        return 0;
    if (typeof value !== "number" || !isFinite(value) || value < 0)
        throw new Error("bad request");
    return value;
}

function raidPushRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const push: { [layerId: string]: number } = {};
    for (const layer of RaidServerRules.layers)
        push[layer.id] = raidNumber(request[layer.id]);
    const seconds = raidNumber(request.seconds);
    const reported = challengeCounts(request.counts);
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    const raid = raidChange(nk, logger, group, now, (value) => {
        raidApply(value, userId, push, seconds, now);
        return value;
    });
    const counts = challengeCap(reported, raid.tally[userId].lastSeconds);
    const daily = challengeChange(nk, logger, group, now, (value) => challengeAdd(value, userId, counts, now));
    const view = raidView(nk, raid, userId, raidLast(nk, group.id), now);
    view.daily = challengeView(daily, userId);
    return JSON.stringify(view);
}

function raidViewRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    raidSettleDue(nk, logger, now);
    const group = clanOf(nk, userId, now);
    const stored = raidRead(nk, group.id).value;
    const raid = stored && stored.week === raidWeekOf(now) ? stored : raidChange(nk, logger, group, now, (value) => value);
    return JSON.stringify(raidView(nk, raid, userId, raidLast(nk, group.id), now));
}
