interface FairRules {
    contests: string[];
    harvestPoints: number;
    mutationPoints: number;
    maxHarvestsPerDay: number;
    maxMutationsPerDay: number;
    rewards: { place: number; friendship: number }[];
    takingPart: number;
    minEntrants: number;
    top: number;
    fish: { [id: string]: { minCm: number; maxCm: number } };
}

interface FairFishEntry {
    fish: string;
    cm: number;
    score: number;
}

interface FairGardenEntry {
    points: number;
    day: number;
    harvests: number;
    mutations: number;
}

interface FairWeek {
    clanId: string;
    week: number;
    fish: { [userId: string]: FairFishEntry };
    garden: { [userId: string]: FairGardenEntry };
    settled: boolean;
}

declare const FairServerRules: FairRules;

const FairCollection = "clan_fair";
const FairWriteAttempts = 4;

function fairKey(clanId: string, week: number): string {
    return clanId + ":" + week;
}

function fairEmpty(clanId: string, week: number): FairWeek {
    return { clanId: clanId, week: week, fish: {}, garden: {}, settled: false };
}

function fairRead(nk: nkruntime.Nakama, clanId: string, week: number): { value: FairWeek; version: string } {
    const objects = nk.storageRead([{ collection: FairCollection, key: fairKey(clanId, week), userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: fairEmpty(clanId, week), version: "*" };
    const stored = objects[0].value as FairWeek;
    return { value: { clanId: clanId, week: week, fish: stored.fish || {}, garden: stored.garden || {}, settled: !!stored.settled }, version: objects[0].version };
}

function fairChange<T>(nk: nkruntime.Nakama, clanId: string, week: number, change: (value: FairWeek) => T): T {
    for (let attempt = 1; attempt <= FairWriteAttempts; attempt++) {
        const stored = fairRead(nk, clanId, week);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: FairCollection, key: fairKey(clanId, week), userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the fair is busy, try again");
}

function fairScoreOf(fish: string, cm: number): number {
    const rule = FairServerRules.fish[fish];
    return Math.round(cm / rule.maxCm * 1000) / 10;
}

function fairStandings(value: FairWeek, contest: string): { userId: string; score: number }[] {
    const rows: { userId: string; score: number }[] = [];
    if (contest === "big_fish")
        for (const userId of Object.keys(value.fish))
            rows.push({ userId: userId, score: value.fish[userId].score });
    else
        for (const userId of Object.keys(value.garden))
            if (value.garden[userId].points > 0)
                rows.push({ userId: userId, score: value.garden[userId].points });
    rows.sort((a, b) => b.score - a.score || (a.userId < b.userId ? -1 : a.userId > b.userId ? 1 : 0));
    return rows;
}

function fairPrize(place: number, entrants: number): number {
    if (entrants < FairServerRules.minEntrants)
        return FairServerRules.takingPart;
    const reward = FairServerRules.rewards.filter((entry) => entry.place === place)[0];
    return reward ? reward.friendship : FairServerRules.takingPart;
}

function fairSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, clanId: string, week: number, nowUnixMs: number): number {
    if (week >= weeklyWeekOf(nowUnixMs))
        return 0;
    const value = fairChange(nk, clanId, week, (fair) => {
        if (fair.settled)
            return null;
        fair.settled = true;
        return fair;
    });
    if (!value)
        return 0;
    let letters = 0;
    for (const contest of FairServerRules.contests) {
        const rows = fairStandings(value, contest);
        rows.forEach((row, i) => {
            const friendship = fairPrize(i + 1, rows.length);
            try {
                walletGrant(nk, row.userId, "clan_fair", friendship, nowUnixMs);
            } catch (error) {
                logger.warn("fair friendship was not granted to %s: %s", row.userId, String(error));
            }
            const sent = mailSend(nk, row.userId, {
                id: "fair-" + contest + "-" + week, kind: "fair", at: seasonPassWeekStart(week + 1), place: i + 1, of: rows.length, depth: Math.round(row.score),
                title: contest, reward: { shards: 0, ingots: 0, friendship: friendship }, claimed: false, claimedAt: 0
            });
            if (sent)
                letters++;
            else
                logger.warn("fair %s letter for %s was not written", contest, row.userId);
        });
    }
    logger.info("clan %s: fair of week %d settled, %d letters", clanId, week, letters);
    return letters;
}

function fairSettleFor(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, nowUnixMs: number): void {
    const group = clanGroupOfUser(nk, userId, nowUnixMs);
    if (group)
        fairSettle(nk, logger, group.id, weeklyWeekOf(nowUnixMs) - 1, nowUnixMs);
}

function fairView(nk: nkruntime.Nakama, group: nkruntime.Group, userId: string, nowUnixMs: number): { [key: string]: any } {
    const week = weeklyWeekOf(nowUnixMs);
    const value = fairRead(nk, group.id, week).value;
    const contests = FairServerRules.contests.map((contest) => ({ contest: contest, rows: fairStandings(value, contest) }));
    const ids: string[] = [];
    for (const entry of contests)
        for (const row of entry.rows.slice(0, FairServerRules.top))
            if (ids.indexOf(row.userId) < 0)
                ids.push(row.userId);
    const users = ids.length > 0 ? nk.usersGetId(ids) : [];
    const nameOf = (id: string) => {
        const user = users.filter((entry) => entry.userId === id)[0];
        return user ? user.displayName || user.username : "";
    };
    return {
        week: week, endsAt: seasonPassWeekStart(week + 1),
        contests: contests.map((entry) => {
            const place = entry.rows.findIndex((row) => row.userId === userId);
            return {
                id: entry.contest,
                top: entry.rows.slice(0, FairServerRules.top).map((row, i) => ({ rank: i + 1, userId: row.userId, name: nameOf(row.userId), score: row.score })),
                mine: place >= 0 ? { rank: place + 1, score: entry.rows[place].score } : null,
                entrants: entry.rows.length
            };
        }),
        fish: value.fish[userId] || null
    };
}

function fairEnterFish(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, fish: unknown, cm: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof fish !== "string" || !FairServerRules.fish[fish] || typeof cm !== "number" || !isFinite(cm))
        throw new Error("bad request");
    const rule = FairServerRules.fish[fish];
    if (cm < rule.minCm || cm > rule.maxCm)
        throw new Error("bad request");
    const group = clanOf(nk, userId, nowUnixMs);
    fairSettle(nk, logger, group.id, weeklyWeekOf(nowUnixMs) - 1, nowUnixMs);
    const score = fairScoreOf(fish, cm);
    fairChange(nk, group.id, weeklyWeekOf(nowUnixMs), (value) => {
        const best = value.fish[userId];
        if (!best || score > best.score)
            value.fish[userId] = { fish: fish, cm: Math.round(cm * 10) / 10, score: score };
    });
    return fairView(nk, group, userId, nowUnixMs);
}

function fairEnterGarden(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, harvests: unknown, mutations: unknown, nowUnixMs: number): { [key: string]: any } {
    const counts = [harvests, mutations].map((count) => count === undefined ? 0 : count);
    if (counts.some((count) => typeof count !== "number" || count !== Math.floor(count) || count < 0))
        throw new Error("bad request");
    const group = clanOf(nk, userId, nowUnixMs);
    fairSettle(nk, logger, group.id, weeklyWeekOf(nowUnixMs) - 1, nowUnixMs);
    const day = Math.floor(nowUnixMs / PartyDayMs);
    fairChange(nk, group.id, weeklyWeekOf(nowUnixMs), (value) => {
        const entry = value.garden[userId] || { points: 0, day: day, harvests: 0, mutations: 0 };
        if (entry.day !== day) {
            entry.day = day;
            entry.harvests = 0;
            entry.mutations = 0;
        }
        const harvested = Math.min(counts[0] as number, FairServerRules.maxHarvestsPerDay - entry.harvests);
        const mutated = Math.min(counts[1] as number, FairServerRules.maxMutationsPerDay - entry.mutations);
        entry.harvests += harvested;
        entry.mutations += mutated;
        entry.points += harvested * FairServerRules.harvestPoints + mutated * FairServerRules.mutationPoints;
        value.garden[userId] = entry;
    });
    return fairView(nk, group, userId, nowUnixMs);
}

function fairViewRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    fairSettle(nk, logger, group.id, weeklyWeekOf(now) - 1, now);
    return JSON.stringify(fairView(nk, group, userId, now));
}

function fairFishRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(fairEnterFish(nk, logger, partyCaller(ctx), request.fish, request.cm, Date.now()));
}

function fairGardenRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(fairEnterGarden(nk, logger, partyCaller(ctx), request.harvests, request.mutations, Date.now()));
}
