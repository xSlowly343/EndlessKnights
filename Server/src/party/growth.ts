interface PartyGrowthUnlock {
    level: number;
    kind: string;
}

interface PartyGrowthRules {
    maxLevel: number;
    levelBase: number;
    levelStep: number;
    perActiveMinute: number;
    bossTogether: number;
    guardianTogether: number;
    order: number;
    perBuildItem: number;
    dailyMinutesPerMember: number;
    firstReportSeconds: number;
    reportSlackSeconds: number;
    rejoinHours: number;
    kickedRejoinHours: number;
    treasury: PartyTreasuryRules;
    tree: PartyTreeRules;
    trials: PartyTrialRules;
    colossus: PartyColossusRules;
    mascot: PartyMascotRules;
    banner: { trial: number[]; colossus: number[]; trials: string[] };
    partySets: PartySetRules;
    unlocks: PartyGrowthUnlock[];
}

interface PartyTreasuryRules {
    currency: string;
    minDonation: number;
    nodes: number;
    maxVotes: number;
    freeResetMs: number;
    resetFeeShare: number;
    resetVoteMs: number;
}

interface PartyTreeNodeRule {
    row: number;
    parent: number;
    keystone: boolean;
    prices: { basisPoints: number; rows: number[] }[];
}

interface PartyTreeRules {
    maxPoints: number;
    maxRank: number;
    rows: number[];
    rowPrices: number[];
    majorChangeMs: number;
    nodes: PartyTreeNodeRule[];
    majors: { at: number; options: string[] }[];
}

interface PartyGrowthMember {
    day: number;
    minutes: number;
    at: number;
    carry: number;
    joined?: number;
}

interface PartyGrowthValue {
    experience: number;
    members: { [userId: string]: PartyGrowthMember };
    left: { [userId: string]: number };
}

declare const PartyGrowthServerRules: PartyGrowthRules;

const PartyGrowthCollection = "party_growth";
const PartyGrowthAttempts = 4;
const PartyGrowthDayMs = 86400000;

function partyGrowthNeed(level: number): number {
    return PartyGrowthServerRules.levelBase + PartyGrowthServerRules.levelStep * (level - 1);
}

function partyGrowthLevel(experience: number): { level: number; into: number; need: number } {
    const rules = PartyGrowthServerRules;
    let level = 1;
    let left = Math.max(0, experience);
    while (level < rules.maxLevel && left >= partyGrowthNeed(level)) {
        left -= partyGrowthNeed(level);
        level++;
    }
    return { level: level, into: level >= rules.maxLevel ? 0 : left, need: level >= rules.maxLevel ? 0 : partyGrowthNeed(level) };
}

function partyGrowthRead(nk: nkruntime.Nakama, partyId: string): { value: PartyGrowthValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyGrowthCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { experience: 0, members: {}, left: {} }, version: "*" };
    const value = objects[0].value as PartyGrowthValue;
    return { value: { experience: value.experience || 0, members: value.members || {}, left: value.left || {} }, version: objects[0].version };
}

function partyGrowthChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyGrowthValue) => T): T {
    for (let attempt = 1; attempt <= PartyGrowthAttempts; attempt++) {
        const stored = partyGrowthRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyGrowthCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyGrowthAdd(nk: nkruntime.Nakama, partyId: string, amount: number): void {
    if (!partyId || !(amount > 0))
        return;
    try {
        partyGrowthChange(nk, partyId, (value) => {
            value.experience += amount;
        });
    } catch (error) {
        return;
    }
}

function partyGrowthView(partyId: string, value: PartyGrowthValue, userId: string, members: number): { [key: string]: any } {
    const level = partyGrowthLevel(value.experience);
    const member = value.members[userId];
    return {
        partyId: partyId, experience: Math.floor(value.experience), level: level.level, into: Math.floor(level.into), need: level.need,
        members: members, joinedAt: member && member.joined ? member.joined : 0
    };
}

function partyGrowthOpened(nk: nkruntime.Nakama, partyId: string, kind: string): boolean {
    const level = partyGrowthLevel(partyGrowthRead(nk, partyId).value.experience).level;
    return PartyGrowthServerRules.unlocks.some((unlock) => unlock.kind === kind && unlock.level <= level);
}

function partyGrowthMine(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    return partyGrowthView(group.id, partyGrowthRead(nk, group.id).value, userId, partyMetaOf(group).members.length);
}

function partyGrowthJoined(nk: nkruntime.Nakama, partyId: string, userId: string, nowUnixMs: number): void {
    partyGrowthChange(nk, partyId, (value) => {
        const member = value.members[userId] || { day: 0, minutes: 0, at: 0, carry: 0 };
        member.joined = nowUnixMs;
        value.members[userId] = member;
        delete value.left[userId];
    });
}

function partyGrowthLeft(nk: nkruntime.Nakama, partyId: string, userId: string, nowUnixMs: number, blockHours: number): void {
    partyGrowthChange(nk, partyId, (value) => {
        delete value.members[userId];
        if (blockHours > 0)
            value.left[userId] = nowUnixMs + blockHours * 3600000;
        else
            delete value.left[userId];
    });
}

function partyGrowthMayRejoin(nk: nkruntime.Nakama, partyId: string, userId: string, nowUnixMs: number): boolean {
    const until = partyGrowthRead(nk, partyId).value.left[userId];
    return !until || nowUnixMs >= until;
}

function partyGrowthMinutes(member: PartyGrowthMember, activeSeconds: number, nowUnixMs: number): number {
    const rules = PartyGrowthServerRules;
    const day = Math.floor(nowUnixMs / PartyGrowthDayMs);
    if (member.day !== day) {
        member.day = day;
        member.minutes = 0;
    }
    const elapsed = member.at > 0 ? Math.max(0, (nowUnixMs - member.at) / 1000) + rules.reportSlackSeconds : rules.firstReportSeconds;
    member.at = nowUnixMs;
    const seconds = member.carry + Math.min(Math.floor(activeSeconds), elapsed);
    const minutes = Math.min(Math.floor(seconds / 60), Math.max(0, rules.dailyMinutesPerMember - member.minutes));
    member.carry = seconds - Math.floor(seconds / 60) * 60;
    member.minutes += minutes;
    return minutes;
}

function partyGrowthReport(nk: nkruntime.Nakama, userId: string, activeSeconds: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof activeSeconds !== "number" || !(activeSeconds >= 0) || activeSeconds > PartyGrowthDayMs / 1000)
        throw new Error("bad request");
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    let minutes = 0;
    const view = partyGrowthChange(nk, group.id, (value) => {
        const member = value.members[userId] || { day: 0, minutes: 0, at: 0, carry: 0 };
        value.members[userId] = member;
        minutes = partyGrowthMinutes(member, activeSeconds, nowUnixMs);
        value.experience += minutes * PartyGrowthServerRules.perActiveMinute;
        return partyGrowthView(group.id, value, userId, partyMetaOf(group).members.length);
    });
    partyMascotBattle(nk, group.id, userId, minutes);
    return view;
}

function partyGrowthBattle(nk: nkruntime.Nakama, partyId: string, bosses: number, guardians: number): void {
    const rules = PartyGrowthServerRules;
    partyMascotBosses(nk, partyId, Math.max(0, bosses) + Math.max(0, guardians));
    partySetBattle(nk, partyId, Math.max(0, bosses) + Math.max(0, guardians));
    partyGrowthAdd(nk, partyId, Math.max(0, bosses) * rules.bossTogether + Math.max(0, guardians) * rules.guardianTogether);
}

function partyGrowthRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyGrowthMine(nk, partyCaller(ctx)));
}

function partyGrowthReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyGrowthReport(nk, partyCaller(ctx), partyRequest(payload).activeSeconds, Date.now()));
}
