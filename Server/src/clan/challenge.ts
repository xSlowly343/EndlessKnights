interface ChallengeRule {
    id: string;
    counter: string;
    perMember: number;
    seals: number;
}

interface ChallengeRules {
    perDay: number;
    dayUtcOffsetMinutes: number;
    minMembers: number;
    capsPerSecond: { [counter: string]: number };
    challenges: ChallengeRule[];
}

interface ChallengeDay {
    clanId: string;
    day: number;
    picks: string[];
    targets: { [id: string]: number };
    progress: { [id: string]: number };
    yours: { [userId: string]: { [id: string]: number } };
    done: { [id: string]: number };
}

declare const ChallengeServerRules: ChallengeRules;

const ChallengeCollection = "clan_daily";
const ChallengeWriteAttempts = 4;
const ChallengeCounters = ["keys", "clicks", "mouseMeters", "bosses"];

function challengeDayOf(unixMs: number): number {
    return Math.floor((unixMs + ChallengeServerRules.dayUtcOffsetMinutes * ClanMinuteMs) / PartyDayMs);
}

function challengeHash(text: string): number {
    let hash = 2166136261;
    for (let i = 0; i < text.length; i++) {
        hash ^= text.charCodeAt(i);
        hash = Math.imul(hash, 16777619) >>> 0;
    }
    hash ^= hash >>> 16;
    hash = Math.imul(hash, 0x85ebca6b);
    hash ^= hash >>> 13;
    hash = Math.imul(hash, 0xc2b2ae35);
    hash ^= hash >>> 16;
    return hash >>> 0;
}

function challengePick(clanId: string, day: number): string[] {
    const ids = ChallengeServerRules.challenges.map((challenge) => challenge.id);
    const picks: string[] = [];
    for (let attempt = 0; picks.length < Math.min(ChallengeServerRules.perDay, ids.length); attempt++) {
        const id = ids[challengeHash(clanId + ":" + day + ":" + attempt) % ids.length];
        if (picks.indexOf(id) < 0)
            picks.push(id);
    }
    return picks;
}

function challengeRule(id: string): ChallengeRule {
    return ChallengeServerRules.challenges.filter((challenge) => challenge.id === id)[0];
}

function challengeCreate(clanId: string, day: number, members: number): ChallengeDay {
    const counted = Math.max(ChallengeServerRules.minMembers, members);
    const picks = challengePick(clanId, day);
    const targets: { [id: string]: number } = {};
    const progress: { [id: string]: number } = {};
    for (const id of picks) {
        targets[id] = challengeRule(id).perMember * counted;
        progress[id] = 0;
    }
    return { clanId: clanId, day: day, picks: picks, targets: targets, progress: progress, yours: {}, done: {} };
}

function challengeCounts(value: any): { [counter: string]: number } {
    const counts: { [counter: string]: number } = {};
    if (value === undefined || value === null)
        return counts;
    if (typeof value !== "object")
        throw new Error("bad request");
    for (const counter of ChallengeCounters)
        counts[counter] = raidNumber(value[counter]);
    return counts;
}

function challengeCap(counts: { [counter: string]: number }, seconds: number): { [counter: string]: number } {
    const capped: { [counter: string]: number } = {};
    for (const counter of Object.keys(counts))
        capped[counter] = Math.min(counts[counter], ChallengeServerRules.capsPerSecond[counter] * seconds);
    return capped;
}

function challengeAdd(daily: ChallengeDay, userId: string, counts: { [counter: string]: number }, nowUnixMs: number): string[] {
    const finished: string[] = [];
    for (const id of daily.picks) {
        if (daily.done[id])
            continue;
        const count = counts[challengeRule(id).counter] || 0;
        if (!(count > 0))
            continue;
        daily.progress[id] += count;
        const yours = daily.yours[userId] || {};
        yours[id] = (yours[id] || 0) + count;
        daily.yours[userId] = yours;
        if (daily.progress[id] >= daily.targets[id]) {
            daily.done[id] = nowUnixMs;
            finished.push(id);
        }
    }
    return finished;
}

function challengeRead(nk: nkruntime.Nakama, clanId: string): { value: ChallengeDay | null; version: string } {
    const objects = nk.storageRead([{ collection: ChallengeCollection, key: clanId, userId: PartySystemUser }]);
    return objects.length > 0 ? { value: objects[0].value as ChallengeDay, version: objects[0].version } : { value: null, version: "*" };
}

function challengeChange(nk: nkruntime.Nakama, logger: nkruntime.Logger, group: nkruntime.Group, nowUnixMs: number,
    change: (daily: ChallengeDay) => string[]): ChallengeDay {
    const day = challengeDayOf(nowUnixMs);
    const members = clanMetaOf(group).members.length;
    for (let attempt = 1; attempt <= ChallengeWriteAttempts; attempt++) {
        const stored = challengeRead(nk, group.id);
        const daily = stored.value && stored.value.day === day ? stored.value : challengeCreate(group.id, day, members);
        const finished = change(daily);
        try {
            nk.storageWrite([{ collection: ChallengeCollection, key: group.id, userId: PartySystemUser, value: daily as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
        } catch (error) {
            if (attempt === ChallengeWriteAttempts)
                throw new Error("clan is busy, try again");
            continue;
        }
        for (const id of finished)
            challengeReward(nk, logger, daily, id, nowUnixMs);
        if (finished.length > 0)
            clanChronicleNote(nk, logger, group.id, { challengesDone: finished.length }, null, clanMetaOf(group).members);
        return daily;
    }
    throw new Error("clan is busy, try again");
}

function challengeReward(nk: nkruntime.Nakama, logger: nkruntime.Logger, daily: ChallengeDay, id: string, nowUnixMs: number): void {
    const seals = challengeRule(id).seals;
    for (const userId of Object.keys(daily.yours)) {
        if (!(daily.yours[userId][id] > 0))
            continue;
        try {
            walletGrant(nk, userId, "clan_challenge", seals, nowUnixMs);
        } catch (error) {
            logger.warn("challenge seals were not granted to %s: %s", userId, String(error));
        }
    }
    logger.info("clan %s: challenge %s done on day %d", daily.clanId, id, daily.day);
}

function challengeView(daily: ChallengeDay, userId: string): { [key: string]: any } {
    const yours = daily.yours[userId] || {};
    return {
        day: daily.day,
        endsAt: (daily.day + 1) * PartyDayMs - ChallengeServerRules.dayUtcOffsetMinutes * ClanMinuteMs,
        challenges: daily.picks.map((id) => ({
            id: id, counter: challengeRule(id).counter, target: daily.targets[id], progress: Math.min(daily.progress[id], daily.targets[id]),
            done: daily.done[id] || 0, yours: yours[id] || 0, seals: challengeRule(id).seals
        }))
    };
}

function clanDailyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    const stored = challengeRead(nk, group.id).value;
    const daily = stored && stored.day === challengeDayOf(now) ? stored : challengeChange(nk, logger, group, now, () => []);
    return JSON.stringify(challengeView(daily, userId));
}
