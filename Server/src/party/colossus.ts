interface PartyColossusRules {
    hpBase: number;
    hpGrowth: number;
    ceilingBase: number;
    ceilingStep: number;
    maxPointsPerSecond: number;
    maxOfflinePointsPerSecond: number;
    reportSeconds: number;
    experiencePerPercent: number;
}

interface PartyColossusMember {
    at: number;
    day: number;
    offlineSeconds: number;
}

interface PartyColossusValue {
    tier: number;
    hp: number;
    waiting: boolean;
    kills: number;
    week: number;
    shares: { [userId: string]: number };
    members: { [userId: string]: PartyColossusMember };
}

interface PartyColossusSituation {
    ceiling: number;
    activeSeconds: number;
}

const PartyColossusCollection = "party_colossus";
const PartyColossusAttempts = 4;

function partyColossusRules(): PartyColossusRules {
    return PartyGrowthServerRules.colossus;
}

function partyColossusHp(tier: number): number {
    return partyColossusRules().hpBase * Math.pow(partyColossusRules().hpGrowth, Math.max(1, tier) - 1);
}

function partyColossusCeiling(level: number): number {
    const unlocks = PartyGrowthServerRules.unlocks;
    if (!unlocks.some((unlock) => unlock.kind === "colossus" && unlock.level <= level))
        return 0;
    const steps = unlocks.filter((unlock) => unlock.kind === "colossusTier" && unlock.level <= level).length;
    return partyColossusRules().ceilingBase + partyColossusRules().ceilingStep * steps;
}

function partyColossusEmpty(): PartyColossusValue {
    return { tier: 1, hp: partyColossusHp(1), waiting: false, kills: 0, week: -1, shares: {}, members: {} };
}

function partyColossusRead(nk: nkruntime.Nakama, partyId: string): { value: PartyColossusValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyColossusCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: partyColossusEmpty(), version: "*" };
    const stored = objects[0].value as PartyColossusValue;
    const value = partyColossusEmpty();
    value.tier = stored.tier || 1;
    value.hp = stored.hp === undefined ? partyColossusHp(value.tier) : stored.hp;
    value.waiting = !!stored.waiting;
    value.kills = stored.kills || 0;
    value.week = stored.week === undefined ? -1 : stored.week;
    value.shares = stored.shares || {};
    value.members = stored.members || {};
    return { value: value, version: objects[0].version };
}

function partyColossusChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyColossusValue) => T): T {
    for (let attempt = 1; attempt <= PartyColossusAttempts; attempt++) {
        const stored = partyColossusRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyColossusCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyColossusSettle(value: PartyColossusValue, ceiling: number, nowUnixMs: number): void {
    const week = partyTrialWeek(nowUnixMs);
    if (value.week !== week) {
        value.week = week;
        value.shares = {};
    }
    if (value.waiting && value.tier < ceiling) {
        value.waiting = false;
        value.tier++;
        value.hp = partyColossusHp(value.tier);
    }
}

function partyColossusSituation(nk: nkruntime.Nakama, partyId: string, userId: string, nowUnixMs: number): PartyColossusSituation {
    const growth = partyGrowthRead(nk, partyId).value;
    const member = growth.members[userId];
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    return {
        ceiling: partyColossusCeiling(partyGrowthLevel(growth.experience).level),
        activeSeconds: member && member.day === today ? member.minutes * 60 + member.carry : 0
    };
}

function partyColossusView(nk: nkruntime.Nakama, partyId: string, value: PartyColossusValue, userId: string, situation: PartyColossusSituation, accepted: { [key: string]: any } | null): { [key: string]: any } {
    const ids = Object.keys(value.shares).sort((a, b) => value.shares[b] - value.shares[a]);
    const names = partyTreasuryNames(nk, ids);
    const total = ids.reduce((sum, id) => sum + value.shares[id], 0);
    return {
        partyId: partyId, opened: situation.ceiling > 0, tier: value.tier, hp: Math.max(0, value.hp), hpFull: partyColossusHp(value.tier),
        ceiling: situation.ceiling, waiting: value.waiting, kills: value.kills,
        shares: ids.map((id) => ({ userId: id, name: names[id] || "", points: Math.floor(value.shares[id]), share: total > 0 ? value.shares[id] / total : 0 })),
        yours: Math.floor(value.shares[userId] || 0), reportSeconds: partyColossusRules().reportSeconds,
        accepted: accepted
    };
}

function partyColossusNumber(raw: unknown): number {
    if (raw === undefined)
        return 0;
    if (typeof raw !== "number" || !(raw >= 0) || raw > 1e12)
        throw new Error("bad request");
    return raw;
}

function partyColossusLive(member: PartyColossusMember, points: number, nowUnixMs: number): number {
    const rules = partyColossusRules();
    const elapsed = member.at > 0 ? Math.max(0, nowUnixMs - member.at) / 1000 : rules.reportSeconds;
    return Math.min(points, elapsed * rules.maxPointsPerSecond);
}

function partyColossusOffline(member: PartyColossusMember, points: number, seconds: number, activeSeconds: number, nowUnixMs: number): number {
    if (!(points > 0) || !(seconds > 0))
        return 0;
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    if (member.day !== today) {
        member.day = today;
        member.offlineSeconds = 0;
    }
    const allowed = Math.min(seconds, Math.max(0, activeSeconds - member.offlineSeconds));
    member.offlineSeconds += allowed;
    return Math.min(points * allowed / seconds, allowed * partyColossusRules().maxOfflinePointsPerSecond);
}

function partyColossusHit(value: PartyColossusValue, points: number, ceiling: number): { dealt: number; killed: number[] } {
    const killed: number[] = [];
    let left = points;
    let dealt = 0;
    while (left > 0 && !value.waiting) {
        const hit = Math.min(left, value.hp);
        value.hp -= hit;
        left -= hit;
        dealt += hit / partyColossusHp(value.tier);
        if (value.hp > 1e-9)
            break;
        killed.push(value.tier);
        value.kills++;
        if (value.tier >= ceiling) {
            value.hp = 0;
            value.waiting = true;
            break;
        }
        value.tier++;
        value.hp = partyColossusHp(value.tier);
    }
    return { dealt: dealt, killed: killed };
}

function partyColossusReport(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const points = partyColossusNumber(request.points);
    const offlinePoints = partyColossusNumber(request.offlinePoints);
    const offlineSeconds = partyColossusNumber(request.offlineSeconds);
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const situation = partyColossusSituation(nk, group.id, userId, nowUnixMs);
    if (situation.ceiling <= 0)
        throw new Error("the colossus is not open yet");
    let gained = { dealt: 0, killed: [] as number[] };
    const view = partyColossusChange(nk, group.id, (value) => {
        partyColossusSettle(value, situation.ceiling, nowUnixMs);
        const member = value.members[userId] || { at: 0, day: 0, offlineSeconds: 0 };
        value.members[userId] = member;
        const live = partyColossusLive(member, points, nowUnixMs);
        const offline = partyColossusOffline(member, offlinePoints, offlineSeconds, situation.activeSeconds, nowUnixMs);
        member.at = nowUnixMs;
        const total = live + offline;
        if (!value.waiting)
            value.shares[userId] = (value.shares[userId] || 0) + total;
        gained = partyColossusHit(value, total, situation.ceiling);
        return partyColossusView(nk, group.id, value, userId, situation, { live: live, offline: offline, killed: gained.killed });
    });
    partyGrowthAdd(nk, group.id, gained.dealt * 100 * partyColossusRules().experiencePerPercent);
    for (const tier of gained.killed)
        partyColossusChronicle(nk, group.id, tier, nowUnixMs);
    return view;
}

function partyColossusChronicle(nk: nkruntime.Nakama, partyId: string, tier: number, nowUnixMs: number): void {
    try {
        chronicleUpdate(nk, partyId, (value) => chronicleRecord(value, { at: nowUnixMs, kind: "colossus", name: "", feat: "", level: tier }, ChronicleServerRules));
    } catch (error) {
        return;
    }
}

function partyColossusMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const situation = partyColossusSituation(nk, group.id, userId, nowUnixMs);
    const value = partyColossusRead(nk, group.id).value;
    partyColossusSettle(value, situation.ceiling, nowUnixMs);
    return partyColossusView(nk, group.id, value, userId, situation, null);
}

function partyColossusRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusMine(nk, partyCaller(ctx), Date.now()));
}

function partyColossusReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusReport(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}
