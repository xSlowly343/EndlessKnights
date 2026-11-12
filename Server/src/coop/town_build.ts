interface TownBuildResource {
    id: string;
    dailyCap: number;
}

interface TownBuildRules {
    startBuilt: string[];
    order: string[];
    voteMs: number;
    gatherDays: number;
    openMs: number;
    activeDays: number;
    minPlayers: number;
    resources: TownBuildResource[];
    needs: { [guild: string]: { [resource: string]: number } };
    keptDays: number;
}

interface TownBuildValue {
    built: string[];
    days: { [day: string]: number };
    round: number;
    phase: string;
    voteEnds: number;
    votes: { [guild: string]: number };
    project: string;
    stock: { [resource: string]: number };
    need: { [resource: string]: number };
    opensAt: number;
}

interface TownBuildMine {
    day: number;
    sent: { [resource: string]: number };
    round: number;
    vote: string;
    given: { [resource: string]: number };
}

declare const TownBuildServerRules: TownBuildRules;

const TownBuildSentKey = "build";
const TownBuildVote = "vote";
const TownBuildGather = "gather";
const TownBuildOpening = "build";
const TownBuildDone = "done";
const TownBuildMaxSteps = 64;

function townBuildCounts(source: unknown): { [id: string]: number } {
    const counts: { [id: string]: number } = {};
    if (source === null || typeof source !== "object")
        return counts;
    for (const key of Object.keys(source as object)) {
        const amount = (source as { [id: string]: unknown })[key];
        if (typeof amount === "number" && amount > 0)
            counts[key] = amount;
    }
    return counts;
}

function townBuildRead(nk: nkruntime.Nakama): { value: TownBuildValue; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    return {
        value: {
            built: Array.isArray(stored.built) ? stored.built : [],
            days: townBuildCounts(stored.days),
            round: typeof stored.round === "number" ? stored.round : 0,
            phase: typeof stored.phase === "string" ? stored.phase : "",
            voteEnds: typeof stored.voteEnds === "number" ? stored.voteEnds : 0,
            votes: townBuildCounts(stored.votes),
            project: typeof stored.project === "string" ? stored.project : "",
            stock: townBuildCounts(stored.stock),
            need: townBuildCounts(stored.need),
            opensAt: typeof stored.opensAt === "number" ? stored.opensAt : 0
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function townBuildMineRead(nk: nkruntime.Nakama, userId: string, day: number, round: number): { value: TownBuildMine; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownBuildSentKey, userId: userId }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    const sameRound = stored.round === round;
    return {
        value: {
            day: day,
            sent: stored.day === day ? townBuildCounts(stored.sent) : {},
            round: round,
            vote: sameRound && typeof stored.vote === "string" ? stored.vote : "",
            given: sameRound ? townBuildCounts(stored.given) : {}
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function townBuildCandidates(value: TownBuildValue): string[] {
    return TownBuildServerRules.order.filter((guild) => value.built.indexOf(guild) < 0);
}

function townBuildPlayers(days: { [day: string]: number }, today: number): number {
    const rules = TownBuildServerRules;
    let total = 0;
    let counted = 0;
    for (let day = today - rules.activeDays; day < today; day++) {
        if (days[String(day)] !== undefined) {
            total += days[String(day)];
            counted++;
        }
    }
    return Math.max(rules.minPlayers, counted > 0 ? total / counted : days[String(today)] || 0);
}

function townBuildNeed(guild: string, days: { [day: string]: number }, today: number): { [resource: string]: number } {
    const rules = TownBuildServerRules;
    const perPlayerDay = rules.needs[guild] || {};
    const players = townBuildPlayers(days, today);
    const need: { [resource: string]: number } = {};
    for (const resource of rules.resources)
        if (perPlayerDay[resource.id] > 0)
            need[resource.id] = Math.max(1, Math.round(perPlayerDay[resource.id] * players * rules.gatherDays));
    return need;
}

function townBuildWinner(value: TownBuildValue): string {
    let winner = "";
    for (const guild of townBuildCandidates(value))
        if (winner.length === 0 || (value.votes[guild] || 0) > (value.votes[winner] || 0))
            winner = guild;
    return winner;
}

function townBuildGathered(value: TownBuildValue): boolean {
    for (const resource of Object.keys(value.need))
        if ((value.stock[resource] || 0) < value.need[resource])
            return false;
    return true;
}

function townBuildOpenVote(value: TownBuildValue, fromUnixMs: number): void {
    value.project = "";
    value.need = {};
    value.votes = {};
    value.opensAt = 0;
    if (townBuildCandidates(value).length === 0) {
        value.phase = TownBuildDone;
        value.voteEnds = 0;
        return;
    }
    value.round++;
    value.phase = TownBuildVote;
    value.voteEnds = fromUnixMs + TownBuildServerRules.voteMs;
}

function townBuildSpend(value: TownBuildValue): void {
    for (const resource of Object.keys(value.need)) {
        const left = (value.stock[resource] || 0) - value.need[resource];
        if (left > 0)
            value.stock[resource] = left;
        else
            delete value.stock[resource];
    }
}

function townBuildAdvance(value: TownBuildValue, logger: nkruntime.Logger, nowUnixMs: number): boolean {
    const rules = TownBuildServerRules;
    let changed = false;
    for (let step = 0; step < TownBuildMaxSteps; step++) {
        if (value.phase !== TownBuildVote && value.phase !== TownBuildGather && value.phase !== TownBuildOpening && value.phase !== TownBuildDone) {
            townBuildOpenVote(value, nowUnixMs);
        } else if (value.phase === TownBuildVote && nowUnixMs >= value.voteEnds) {
            value.project = townBuildWinner(value);
            value.need = townBuildNeed(value.project, value.days, walletDayOf(value.voteEnds));
            value.votes = {};
            value.phase = TownBuildGather;
            logger.info("town: the vote of round %d chose %s", value.round, value.project);
            if (townBuildGathered(value)) {
                value.phase = TownBuildOpening;
                value.opensAt = value.voteEnds + rules.openMs;
            }
        } else if (value.phase === TownBuildGather && townBuildGathered(value)) {
            value.phase = TownBuildOpening;
            value.opensAt = nowUnixMs + rules.openMs;
        } else if (value.phase === TownBuildOpening && nowUnixMs >= value.opensAt) {
            const openedAt = value.opensAt;
            value.built.push(value.project);
            logger.info("town: guild hall %s is built", value.project);
            townBuildSpend(value);
            townBuildOpenVote(value, openedAt);
        } else {
            return changed;
        }
        changed = true;
    }
    return changed;
}

function townBuildView(value: TownBuildValue, mine: TownBuildMine, accepted: { [resource: string]: number }, nowUnixMs: number): { [key: string]: any } {
    const rules = TownBuildServerRules;
    return {
        startBuilt: rules.startBuilt,
        built: value.built,
        round: value.round,
        phase: value.phase,
        voteEnds: value.voteEnds,
        candidates: value.phase === TownBuildVote ? townBuildCandidates(value).map((guild) => ({ guild: guild, votes: value.votes[guild] || 0 })) : [],
        vote: value.phase === TownBuildVote ? mine.vote : "",
        project: value.project,
        opensAt: value.opensAt,
        now: nowUnixMs,
        resources: rules.resources.map((resource) => ({
            id: resource.id,
            stock: value.stock[resource.id] || 0,
            need: value.need[resource.id] || 0,
            yours: mine.given[resource.id] || 0,
            sentToday: mine.sent[resource.id] || 0,
            dailyCap: resource.dailyCap
        })),
        accepted: accepted
    };
}

function townBuildGains(gains: unknown): { [resource: string]: number } {
    if (gains === null || typeof gains !== "object" || Array.isArray(gains))
        throw new Error("bad request");
    const asked = gains as { [resource: string]: unknown };
    const known: { [resource: string]: number } = {};
    for (const resource of Object.keys(asked)) {
        const amount = asked[resource];
        if (!TownBuildServerRules.resources.some((entry) => entry.id === resource))
            throw new Error("bad request");
        if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < 1 || amount > 1000000)
            throw new Error("bad request");
        known[resource] = amount;
    }
    if (Object.keys(known).length === 0)
        throw new Error("bad request");
    return known;
}

function townBuildTake(nk: nkruntime.Nakama, userId: string, gains: { [resource: string]: number }, day: number, round: number): { mine: TownBuildMine; accepted: { [resource: string]: number }; first: boolean } {
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildMineRead(nk, userId, day, round);
        const mine = stored.value;
        const first = Object.keys(mine.sent).length === 0;
        const accepted: { [resource: string]: number } = {};
        for (const resource of TownBuildServerRules.resources) {
            const sent = mine.sent[resource.id] || 0;
            const taken = Math.max(0, Math.min(gains[resource.id] || 0, resource.dailyCap - sent));
            if (taken === 0)
                continue;
            accepted[resource.id] = taken;
            mine.sent[resource.id] = sent + taken;
            mine.given[resource.id] = (mine.given[resource.id] || 0) + taken;
        }
        if (Object.keys(accepted).length === 0)
            return { mine: mine, accepted: accepted, first: false };
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownBuildSentKey, userId: userId, value: mine as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return { mine: mine, accepted: accepted, first: first };
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildState(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, nowUnixMs: number): { [key: string]: any } {
    const stored = townBuildRead(nk);
    const value = stored.value;
    if (townBuildAdvance(value, logger, nowUnixMs)) {
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
        } catch (error) {
            return townBuildState(nk, logger, userId, nowUnixMs);
        }
    }
    return townBuildView(value, townBuildMineRead(nk, userId, walletDayOf(nowUnixMs), value.round).value, {}, nowUnixMs);
}

function townBuildAdd(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, gains: unknown, nowUnixMs: number): { [key: string]: any } {
    const asked = townBuildGains(gains);
    const rules = TownBuildServerRules;
    const day = walletDayOf(nowUnixMs);
    const before = townBuildRead(nk).value;
    townBuildAdvance(before, logger, nowUnixMs);
    if (before.phase === TownBuildDone)
        return townBuildView(before, townBuildMineRead(nk, userId, day, before.round).value, {}, nowUnixMs);
    const taken = townBuildTake(nk, userId, asked, day, before.round);
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildRead(nk);
        const value = stored.value;
        townBuildAdvance(value, logger, nowUnixMs);
        if (taken.first)
            value.days[String(day)] = (value.days[String(day)] || 0) + 1;
        for (const key of Object.keys(value.days))
            if (Number(key) < day - rules.keptDays)
                delete value.days[key];
        for (const resource of Object.keys(taken.accepted))
            value.stock[resource] = (value.stock[resource] || 0) + taken.accepted[resource];
        townBuildAdvance(value, logger, nowUnixMs);
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return townBuildView(value, taken.mine, taken.accepted, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildVote(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, guild: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof guild !== "string")
        throw new Error("bad request");
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildRead(nk);
        const value = stored.value;
        townBuildAdvance(value, logger, nowUnixMs);
        if (value.phase !== TownBuildVote)
            throw new Error("the vote is closed");
        if (townBuildCandidates(value).indexOf(guild) < 0)
            throw new Error("bad request");
        const own = townBuildMineRead(nk, userId, walletDayOf(nowUnixMs), value.round);
        const mine = own.value;
        if (mine.vote.length > 0 && value.votes[mine.vote] > 0) {
            value.votes[mine.vote]--;
            if (value.votes[mine.vote] === 0)
                delete value.votes[mine.vote];
        }
        value.votes[guild] = (value.votes[guild] || 0) + 1;
        mine.vote = guild;
        try {
            nk.storageWrite([
                { collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 },
                { collection: TownCollection, key: TownBuildSentKey, userId: userId, value: mine as any, version: own.version, permissionRead: 1, permissionWrite: 0 }
            ]);
            return townBuildView(value, mine, {}, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildState(nk, logger, partyCaller(ctx), Date.now()));
}

function townBuildAddRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildAdd(nk, logger, partyCaller(ctx), partyRequest(payload).gains, Date.now()));
}

function townBuildVoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildVote(nk, logger, partyCaller(ctx), partyRequest(payload).guild, Date.now()));
}
