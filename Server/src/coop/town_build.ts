interface TownBuildResource {
    id: string;
    dailyCap: number;
    guild: string;
    kind: string;
}

interface TownBuildLevel {
    afterBuilt: number;
    needs: { [resource: string]: number };
}

interface TownBuildBuilding {
    id: string;
    levels: TownBuildLevel[];
}

interface TownBuildReward {
    id: string;
    points: number;
    shards: number;
    friendship: number;
}

interface TownBuildRules {
    buildings: TownBuildBuilding[];
    stages: number[];
    stageShards: number;
    voteMs: number;
    minBuildMs: number;
    gatherDays: number;
    openMs: number;
    activeDays: number;
    minPlayers: number;
    paceBurst: number;
    paceFillMs: number;
    resources: TownBuildResource[];
    rewards: TownBuildReward[];
    topSize: number;
    topShards: number;
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
    buildFrom: number;
    halls: { [round: string]: string };
}

interface TownBuildMine {
    day: number;
    sent: { [resource: string]: number };
    round: number;
    vote: string;
    given: { [resource: string]: number };
    pace: { [resource: string]: number };
    paceAt: number;
    thanked: number;
}

declare const TownBuildServerRules: TownBuildRules;

const TownBuildSentKey = "build";
const TownBuildVote = "vote";
const TownBuildGather = "gather";
const TownBuildOpening = "build";
const TownBuildDone = "done";
const TownBuildMaxSteps = 64;
const TownBuildPointsPerDay = 1000;
const TownBuildMailKind = "town";
const TownBuildLevelMark = ":";
const TownBuildLabourId = "labour";

function townBuildProjectId(building: string, level: number): string {
    return level <= 1 ? building : building + TownBuildLevelMark + level;
}

function townBuildProject(project: string): { building: TownBuildBuilding; level: number } | null {
    const mark = project.indexOf(TownBuildLevelMark);
    const id = mark < 0 ? project : project.substring(0, mark);
    const level = mark < 0 ? 1 : Number(project.substring(mark + 1));
    const building = TownBuildServerRules.buildings.filter((entry) => entry.id === id)[0];
    if (building === undefined || !(level >= 1) || level !== Math.floor(level) || level > building.levels.length || (mark >= 0 && level < 2))
        return null;
    return { building: building, level: level };
}

function townBuildLevelOf(value: TownBuildValue, building: TownBuildBuilding): number {
    let level = 0;
    while (level < building.levels.length && value.built.indexOf(townBuildProjectId(building.id, level + 1)) >= 0)
        level++;
    return level;
}

function townBuildStageRaised(builtCount: number): boolean {
    return builtCount > 0 && TownBuildServerRules.stages.indexOf(builtCount) >= 0;
}

function townBuildBoardId(round: number): string {
    return "town_build_" + round;
}

function townBuildPoints(given: { [resource: string]: number }): number {
    let points = 0;
    for (const resource of TownBuildServerRules.resources)
        points += (given[resource.id] || 0) * TownBuildPointsPerDay / resource.dailyCap;
    return Math.floor(points);
}

function townBuildTier(points: number): TownBuildReward | null {
    let reached: TownBuildReward | null = null;
    for (const tier of TownBuildServerRules.rewards)
        if (points >= tier.points)
            reached = tier;
    return reached;
}

function townBuildRank(nk: nkruntime.Nakama, userId: string, round: number): number {
    const id = townBuildBoardId(round);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    const own = (nk.leaderboardRecordsList(id, [userId], 0).ownerRecords || []).filter((record) => record.ownerId === userId)[0];
    return own ? own.rank : 0;
}

function townBuildTop(nk: nkruntime.Nakama, round: number): { rank: number; name: string; points: number }[] {
    const id = townBuildBoardId(round);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    const top = nk.leaderboardRecordsList(id, [], TownBuildServerRules.topSize);
    return (top.records || []).map((record) => ({ rank: record.rank, name: record.username || "", points: record.score }));
}

function townBuildScore(nk: nkruntime.Nakama, userId: string, round: number, points: number): void {
    if (points <= 0)
        return;
    const id = townBuildBoardId(round);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    nk.leaderboardRecordWrite(id, userId, consentShownName(nk, userId, ConsentTownTop), Math.min(points, 1e9), 0, {});
}

function townBuildRename(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, nowUnixMs: number): void {
    const value = townBuildRead(nk).value;
    const points = townBuildPoints(townBuildMineRead(nk, userId, walletDayOf(nowUnixMs), value.round).value.given);
    if (value.round <= 0 || points <= 0)
        return;
    const id = townBuildBoardId(value.round);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    nk.leaderboardRecordDelete(id, userId);
    townBuildScore(nk, userId, value.round, points);
    logger.info("town: %s changed the name shown in the builders' top", userId);
}

function townBuildSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, value: TownBuildValue, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownBuildSentKey, userId: userId }]);
    if (objects.length === 0)
        return;
    const stored = objects[0].value as { [key: string]: any };
    const round = typeof stored.round === "number" ? stored.round : 0;
    const hall = value.halls[String(round)];
    if (round <= 0 || hall === undefined || stored.thanked === round)
        return;
    const points = townBuildPoints(townBuildCounts(stored.given));
    const tier = townBuildTier(points);
    const rank = points > 0 ? townBuildRank(nk, userId, round) : 0;
    const inTop = tier !== null && rank > 0 && rank <= TownBuildServerRules.topSize;
    const raised = townBuildStageRaised(value.built.indexOf(hall) + 1);
    stored.thanked = round;
    try {
        nk.storageWrite([{ collection: TownCollection, key: TownBuildSentKey, userId: userId, value: stored, version: objects[0].version, permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        return;
    }
    if (tier === null)
        return;
    const sent = mailSend(nk, userId, {
        id: "town-build-" + round,
        kind: TownBuildMailKind,
        at: nowUnixMs,
        place: inTop ? rank : 0,
        of: 0,
        depth: points,
        title: hall,
        reward: { shards: tier.shards + (inTop ? TownBuildServerRules.topShards : 0) + (raised ? TownBuildServerRules.stageShards : 0), ingots: 0, friendship: tier.friendship },
        claimed: false,
        claimedAt: 0
    });
    if (sent)
        logger.info("town: %s is thanked for hall %s with %d points", userId, hall, points);
}

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

function townBuildHalls(source: unknown): { [round: string]: string } {
    const halls: { [round: string]: string } = {};
    if (source === null || typeof source !== "object")
        return halls;
    for (const key of Object.keys(source as object)) {
        const hall = (source as { [round: string]: unknown })[key];
        if (typeof hall === "string")
            halls[key] = hall;
    }
    return halls;
}

function townBuildStands(value: TownBuildValue, guild: string): boolean {
    return guild.length === 0 || value.built.indexOf(guild) >= 0;
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
            opensAt: typeof stored.opensAt === "number" ? stored.opensAt : 0,
            buildFrom: typeof stored.buildFrom === "number" ? stored.buildFrom : 0,
            halls: townBuildHalls(stored.halls)
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
            given: sameRound ? townBuildCounts(stored.given) : {},
            pace: townBuildCounts(stored.pace),
            paceAt: typeof stored.paceAt === "number" ? stored.paceAt : 0,
            thanked: typeof stored.thanked === "number" ? stored.thanked : 0
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function townBuildCandidates(value: TownBuildValue): string[] {
    const candidates: string[] = [];
    const built = value.built.filter((id) => townBuildProject(id) !== null).length;
    let deepest = 0;
    for (const building of TownBuildServerRules.buildings)
        deepest = Math.max(deepest, building.levels.length);
    for (let level = 1; level <= deepest; level++)
        for (const building of TownBuildServerRules.buildings)
            if (level <= building.levels.length && townBuildLevelOf(value, building) === level - 1 && built >= building.levels[level - 1].afterBuilt)
                candidates.push(townBuildProjectId(building.id, level));
    return candidates;
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

function townBuildNeed(value: TownBuildValue, project: string, today: number): { [resource: string]: number } {
    const rules = TownBuildServerRules;
    const found = townBuildProject(project);
    const perPlayerDay = found === null ? {} : found.building.levels[found.level - 1].needs;
    const players = townBuildPlayers(value.days, today);
    const need: { [resource: string]: number } = {};
    for (const resource of rules.resources)
        if (perPlayerDay[resource.id] > 0 && townBuildStands(value, resource.guild))
            need[resource.id] = Math.max(1, Math.round(perPlayerDay[resource.id] * players * rules.gatherDays));
    return need;
}

function townBuildOpensAt(value: TownBuildValue, gatheredAt: number): number {
    return Math.max(gatheredAt + TownBuildServerRules.openMs, value.buildFrom + TownBuildServerRules.minBuildMs);
}

function townBuildVotes(value: TownBuildValue): number {
    let votes = 0;
    for (const guild of townBuildCandidates(value))
        votes += value.votes[guild] || 0;
    return votes;
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
    value.buildFrom = 0;
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
        } else if (value.phase === TownBuildVote && nowUnixMs >= value.voteEnds && townBuildVotes(value) === 0) {
            value.voteEnds += (Math.floor((nowUnixMs - value.voteEnds) / rules.voteMs) + 1) * rules.voteMs;
            logger.info("town: nobody voted in round %d, the vote goes on", value.round);
        } else if (value.phase === TownBuildVote && nowUnixMs >= value.voteEnds) {
            value.project = townBuildWinner(value);
            value.need = townBuildNeed(value, value.project, walletDayOf(value.voteEnds));
            value.votes = {};
            value.buildFrom = value.voteEnds;
            value.phase = TownBuildGather;
            logger.info("town: the vote of round %d chose %s", value.round, value.project);
            if (townBuildGathered(value)) {
                value.phase = TownBuildOpening;
                value.opensAt = townBuildOpensAt(value, value.voteEnds);
            }
        } else if (value.phase === TownBuildGather && townBuildGathered(value)) {
            value.phase = TownBuildOpening;
            value.opensAt = townBuildOpensAt(value, nowUnixMs);
        } else if (value.phase === TownBuildOpening && nowUnixMs >= value.opensAt) {
            const openedAt = value.opensAt;
            value.built.push(value.project);
            value.halls[String(value.round)] = value.project;
            logger.info("town: %s is built", value.project);
            townBuildSpend(value);
            townBuildOpenVote(value, openedAt);
        } else {
            return changed;
        }
        changed = true;
    }
    return changed;
}

function townBuildView(nk: nkruntime.Nakama, userId: string, value: TownBuildValue, mine: TownBuildMine, accepted: { [resource: string]: number }, nowUnixMs: number): { [key: string]: any } {
    const rules = TownBuildServerRules;
    const points = townBuildPoints(mine.given);
    const counting = value.phase !== TownBuildDone;
    return {
        points: points,
        rank: counting && points > 0 ? townBuildRank(nk, userId, value.round) : 0,
        top: counting ? townBuildTop(nk, value.round) : [],
        built: value.built,
        round: value.round,
        phase: value.phase,
        voteEnds: value.voteEnds,
        candidates: value.phase === TownBuildVote ? townBuildCandidates(value).map((project) => ({ project: project, votes: value.votes[project] || 0 })) : [],
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
            dailyCap: resource.dailyCap,
            room: Math.max(0, Math.min(resource.dailyCap - (mine.sent[resource.id] || 0), Math.floor(townBuildPaceRoom(mine, resource, nowUnixMs))))
        })),
        accepted: accepted,
        roles: counting ? townRoleTop(nk, value.round) : [],
        week: townWeekView(nk, userId, value.days, nowUnixMs)
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

function townBuildPaceRoom(mine: TownBuildMine, resource: TownBuildResource, nowUnixMs: number): number {
    const rules = TownBuildServerRules;
    const capacity = resource.dailyCap * rules.paceBurst;
    if (mine.paceAt <= 0 || mine.pace[resource.id] === undefined)
        return capacity;
    const refill = resource.dailyCap * Math.max(0, nowUnixMs - mine.paceAt) / rules.paceFillMs;
    return Math.min(capacity, mine.pace[resource.id] + refill);
}

function townBuildPace(mine: TownBuildMine, nowUnixMs: number): void {
    for (const resource of TownBuildServerRules.resources)
        mine.pace[resource.id] = townBuildPaceRoom(mine, resource, nowUnixMs);
    mine.paceAt = nowUnixMs;
}

function townBuildTake(nk: nkruntime.Nakama, userId: string, gains: { [resource: string]: number }, day: number, round: number, nowUnixMs: number): { mine: TownBuildMine; accepted: { [resource: string]: number }; first: boolean } {
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildMineRead(nk, userId, day, round);
        const mine = stored.value;
        const first = Object.keys(mine.sent).length === 0;
        const accepted: { [resource: string]: number } = {};
        townBuildPace(mine, nowUnixMs);
        for (const resource of TownBuildServerRules.resources) {
            const sent = mine.sent[resource.id] || 0;
            const taken = Math.max(0, Math.min(gains[resource.id] || 0, resource.dailyCap - sent, Math.floor(mine.pace[resource.id])));
            if (taken === 0)
                continue;
            accepted[resource.id] = taken;
            mine.sent[resource.id] = sent + taken;
            mine.given[resource.id] = (mine.given[resource.id] || 0) + taken;
            mine.pace[resource.id] -= taken;
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
    townBuildSettle(nk, logger, userId, value, nowUnixMs);
    townWeekAdd(nk, logger, userId, 0, value.days, nowUnixMs);
    return townBuildView(nk, userId, value, townBuildMineRead(nk, userId, walletDayOf(nowUnixMs), value.round).value, {}, nowUnixMs);
}

function townBuildAdd(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, gains: unknown, nowUnixMs: number): { [key: string]: any } {
    const asked = townBuildGains(gains);
    const rules = TownBuildServerRules;
    const day = walletDayOf(nowUnixMs);
    const before = townBuildRead(nk).value;
    townBuildAdvance(before, logger, nowUnixMs);
    townBuildSettle(nk, logger, userId, before, nowUnixMs);
    if (before.phase === TownBuildDone)
        return townBuildView(nk, userId, before, townBuildMineRead(nk, userId, day, before.round).value, {}, nowUnixMs);
    const taken = townBuildTake(nk, userId, asked, day, before.round, nowUnixMs);
    townBuildScore(nk, userId, before.round, townBuildPoints(taken.mine.given));
    townBuildRoles(nk, userId, taken.accepted, nowUnixMs);
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
            townWeekAdd(nk, logger, userId, taken.accepted[TownBuildLabourId] || 0, value.days, nowUnixMs);
            return townBuildView(nk, userId, value, taken.mine, taken.accepted, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildVote(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, project: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof project !== "string")
        throw new Error("bad request");
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildRead(nk);
        const value = stored.value;
        townBuildAdvance(value, logger, nowUnixMs);
        if (value.phase !== TownBuildVote)
            throw new Error("the vote is closed");
        if (townBuildCandidates(value).indexOf(project) < 0)
            throw new Error("bad request");
        townBuildSettle(nk, logger, userId, value, nowUnixMs);
        const own = townBuildMineRead(nk, userId, walletDayOf(nowUnixMs), value.round);
        const mine = own.value;
        if (mine.vote.length > 0 && value.votes[mine.vote] > 0) {
            value.votes[mine.vote]--;
            if (value.votes[mine.vote] === 0)
                delete value.votes[mine.vote];
        }
        value.votes[project] = (value.votes[project] || 0) + 1;
        mine.vote = project;
        try {
            nk.storageWrite([
                { collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 },
                { collection: TownCollection, key: TownBuildSentKey, userId: userId, value: mine as any, version: own.version, permissionRead: 1, permissionWrite: 0 }
            ]);
            return townBuildView(nk, userId, value, mine, {}, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildFinish(nk: nkruntime.Nakama, logger: nkruntime.Logger, by: string, nowUnixMs: number): { [key: string]: any } {
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildRead(nk);
        const value = stored.value;
        townBuildAdvance(value, logger, nowUnixMs);
        if (value.phase !== TownBuildOpening)
            throw new Error("the town has nothing gathered to build");
        value.opensAt = nowUnixMs;
        townBuildAdvance(value, logger, nowUnixMs);
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            logger.warn("town: the build of round %d is finished early by %s", value.round - 1, by);
            return { built: value.built, round: value.round, phase: value.phase };
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildFinishRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const role = ctx.userId ? chatRoleOf(nk, ctx.userId) : ChatOperator;
    if (role === "" || !chatRoleRule(role).grantsRoles)
        throw { message: "not available to players", code: 7 };
    return JSON.stringify(townBuildFinish(nk, logger, ctx.userId || ChatOperator, Date.now()));
}

function townBuildRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildState(nk, logger, partyCaller(ctx), Date.now()));
}

function townBuildAddRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildAdd(nk, logger, partyCaller(ctx), partyRequest(payload).gains, Date.now()));
}

function townBuildVoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(townBuildVote(nk, logger, partyCaller(ctx), request.project !== undefined ? request.project : request.guild, Date.now()));
}

const TownRoleFeeder = "feeder";
const TownRoleLogger = "logger";
const TownRoleCrafter = "crafter";
const TownRoles = [TownRoleLogger, TownRoleFeeder, TownRoleCrafter];
const TownRoleOfKind: { [kind: string]: string } = { raw: TownRoleLogger, craft: TownRoleCrafter };

function townRoleBoard(role: string, round: number): string {
    return "town_role_" + role + "_" + round;
}

function townRoleAdd(nk: nkruntime.Nakama, userId: string, role: string, amount: number, nowUnixMs: number): void {
    if (!(amount > 0))
        return;
    const value = townBuildRead(nk).value;
    if (value.round <= 0 || value.phase === TownBuildDone)
        return;
    const id = townRoleBoard(role, value.round);
    nk.leaderboardCreate(id, true, "descending", "incr", null, null);
    nk.leaderboardRecordWrite(id, userId, consentShownName(nk, userId, ConsentTownTop), Math.min(amount, 1e9), 0, {});
}

function townBuildRoles(nk: nkruntime.Nakama, userId: string, accepted: { [resource: string]: number }, nowUnixMs: number): void {
    const byRole: { [role: string]: number } = {};
    for (const resource of TownBuildServerRules.resources) {
        const role = TownRoleOfKind[resource.kind];
        if (role && accepted[resource.id])
            byRole[role] = (byRole[role] || 0) + accepted[resource.id];
    }
    for (const role of Object.keys(byRole))
        townRoleAdd(nk, userId, role, byRole[role], nowUnixMs);
}

function townRoleTop(nk: nkruntime.Nakama, round: number): { role: string; name: string; amount: number }[] {
    const roles: { role: string; name: string; amount: number }[] = [];
    for (const role of TownRoles) {
        const id = townRoleBoard(role, round);
        nk.leaderboardCreate(id, true, "descending", "incr", null, null);
        const best = (nk.leaderboardRecordsList(id, [], 1).records || [])[0];
        if (best)
            roles.push({ role: role, name: best.username || "", amount: best.score });
    }
    return roles;
}
