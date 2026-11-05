interface ClanWarFrontRule {
    id: string;
    counter: string;
    layer: string;
}

interface ClanWarRules {
    minActive: number;
    pairDay: number;
    warDay: number;
    fronts: ClanWarFrontRule[];
    perMemberSeconds: number;
    moralePerChallenge: number;
    moraleMax: number;
    sealsPerMember: number;
    sealsWin: number;
    sealsDraw: number;
    sealsLoss: number;
    ratingWin: number;
    ratingDraw: number;
    ratingLoss: number;
    leagues: { id: string; from: number }[];
    seasonWeeks: number;
}

interface ClanWarEntry {
    clanId: string;
    week: number;
    active: string[];
    challenges: number;
    out: boolean;
}

interface ClanWarPair {
    a: string;
    b: string;
    shadow: { [front: string]: number } | null;
}

interface ClanWarPairs {
    week: number;
    pairs: ClanWarPair[];
}

interface ClanWarScore {
    clanId: string;
    week: number;
    fronts: { [front: string]: number };
    members: { [userId: string]: { [front: string]: number } };
}

interface ClanWarResult {
    week: number;
    clanId: string;
    opponent: string;
    shadow: boolean;
    ours: { [front: string]: number };
    theirs: { [front: string]: number };
    points: number;
    theirPoints: number;
    outcome: string;
    seals: number;
    rating: number;
    league: string;
}

interface ClanWarClan {
    rating: number;
    season: number;
    lastWeek: number;
    wins: number;
}

declare const ClanWarServerRules: ClanWarRules;

const ClanWarEntryCollection = "clan_war_entry";
const ClanWarIndexCollection = "clan_war_index";
const ClanWarPairsCollection = "clan_war_pairs";
const ClanWarScoreCollection = "clan_war_score";
const ClanWarResultCollection = "clan_war_result";
const ClanWarClanCollection = "clan_war_clan";
const ClanWarWriteAttempts = 4;

function clanWarDayOf(nowUnixMs: number): number {
    return Math.floor((nowUnixMs - raidWeekStart(raidWeekOf(nowUnixMs))) / PartyDayMs);
}

function clanWarRead<T>(nk: nkruntime.Nakama, collection: string, key: string): { value: T | null; version: string } {
    const objects = nk.storageRead([{ collection: collection, key: key, userId: PartySystemUser }]);
    return objects.length > 0 ? { value: objects[0].value as T, version: objects[0].version } : { value: null, version: "*" };
}

function clanWarChange<T>(nk: nkruntime.Nakama, collection: string, key: string, empty: () => T, change: (value: T) => boolean): T {
    for (let attempt = 1; attempt <= ClanWarWriteAttempts; attempt++) {
        const stored = clanWarRead<T>(nk, collection, key);
        const value = stored.value || empty();
        if (!change(value))
            return value;
        try {
            nk.storageWrite([{ collection: collection, key: key, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return value;
        } catch (error) {
            if (attempt === ClanWarWriteAttempts)
                throw new Error("clan war is busy, try again");
        }
    }
    throw new Error("clan war is busy, try again");
}

function clanWarKey(week: number, clanId: string): string {
    return week + ":" + clanId;
}

function clanWarEmptyEntry(clanId: string, week: number): ClanWarEntry {
    return { clanId: clanId, week: week, active: [], challenges: 0, out: false };
}

function clanWarNoteEntry(nk: nkruntime.Nakama, clanId: string, week: number, change: (entry: ClanWarEntry) => boolean): ClanWarEntry {
    let created = false;
    const entry = clanWarChange<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, clanId), () => {
        created = true;
        return clanWarEmptyEntry(clanId, week);
    }, (value) => change(value) || created);
    if (created)
        clanWarChange<{ clans: string[] }>(nk, ClanWarIndexCollection, String(week), () => ({ clans: [] }), (index) => {
            if (index.clans.indexOf(clanId) >= 0)
                return false;
            index.clans.push(clanId);
            return true;
        });
    return entry;
}

function clanWarMorale(entry: ClanWarEntry | null): number {
    return entry ? Math.min(ClanWarServerRules.moraleMax, entry.challenges * ClanWarServerRules.moralePerChallenge) : 0;
}

function clanWarLeague(rating: number): string {
    let league = ClanWarServerRules.leagues[0].id;
    for (const rule of ClanWarServerRules.leagues)
        if (rating >= rule.from)
            league = rule.id;
    return league;
}

function clanWarSeasonOf(week: number): number {
    return Math.floor(week / ClanWarServerRules.seasonWeeks);
}

function clanWarClanOf(nk: nkruntime.Nakama, clanId: string, week: number): ClanWarClan {
    const stored = clanWarRead<ClanWarClan>(nk, ClanWarClanCollection, clanId).value;
    const value = stored || { rating: 0, season: clanWarSeasonOf(week), lastWeek: -1, wins: 0 };
    return clanWarSeasonTurn(value, clanWarSeasonOf(week));
}

function clanWarSeasonTurn(value: ClanWarClan, season: number): ClanWarClan {
    if (season <= value.season)
        return value;
    const leagues = ClanWarServerRules.leagues;
    let index = 0;
    for (let i = 0; i < leagues.length; i++)
        if (value.rating >= leagues[i].from)
            index = i;
    return { rating: leagues[Math.max(0, index - 1)].from, season: season, lastWeek: value.lastWeek, wins: value.wins };
}

function clanWarPairUp(entries: ClanWarEntry[], ratings: { [clanId: string]: number }, previous: ClanWarResult[]): ClanWarPair[] {
    const rules = ClanWarServerRules;
    const eligible = entries.filter((entry) => !entry.out && entry.active.length >= rules.minActive)
        .sort((x, y) => (ratings[y.clanId] || 0) - (ratings[x.clanId] || 0) || y.active.length - x.active.length || (x.clanId < y.clanId ? -1 : 1));
    const pairs: ClanWarPair[] = [];
    for (let i = 0; i + 1 < eligible.length; i += 2)
        pairs.push({ a: eligible[i].clanId, b: eligible[i + 1].clanId, shadow: null });
    if (eligible.length % 2 === 1) {
        const odd = eligible[eligible.length - 1].clanId;
        const own = ratings[odd] || 0;
        const near = previous.filter((result) => result.clanId !== odd)
            .sort((x, y) => Math.abs((ratings[x.clanId] || 0) - own) - Math.abs((ratings[y.clanId] || 0) - own))[0];
        pairs.push({ a: odd, b: near ? near.clanId : "", shadow: near ? near.ours : {} });
    }
    return pairs;
}

function clanWarPairsOf(nk: nkruntime.Nakama, logger: nkruntime.Logger, week: number, nowUnixMs: number): ClanWarPairs | null {
    const stored = clanWarRead<ClanWarPairs>(nk, ClanWarPairsCollection, String(week)).value;
    if (stored)
        return stored;
    const index = clanWarRead<{ clans: string[] }>(nk, ClanWarIndexCollection, String(week)).value;
    const clans = index ? index.clans : [];
    const entries: ClanWarEntry[] = [];
    const ratings: { [clanId: string]: number } = {};
    for (const clanId of clans) {
        const entry = clanWarRead<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, clanId)).value;
        if (entry)
            entries.push(entry);
        ratings[clanId] = clanWarClanOf(nk, clanId, week).rating;
    }
    const previous: ClanWarResult[] = [];
    const lastIndex = clanWarRead<{ clans: string[] }>(nk, ClanWarIndexCollection, String(week - 1)).value;
    for (const clanId of lastIndex ? lastIndex.clans : []) {
        const result = clanWarSettle(nk, logger, clanId, week - 1, nowUnixMs);
        if (result && !result.shadow)
            previous.push(result);
    }
    const value: ClanWarPairs = { week: week, pairs: clanWarPairUp(entries, ratings, previous) };
    try {
        nk.storageWrite([{ collection: ClanWarPairsCollection, key: String(week), userId: PartySystemUser, value: value as any, version: "*", permissionRead: 0, permissionWrite: 0 }]);
        return value;
    } catch (error) {
        return clanWarRead<ClanWarPairs>(nk, ClanWarPairsCollection, String(week)).value;
    }
}

function clanWarPairOf(pairs: ClanWarPairs | null, clanId: string): { opponent: string; shadow: { [front: string]: number } | null } | null {
    for (const pair of pairs ? pairs.pairs : []) {
        if (pair.a === clanId)
            return { opponent: pair.b, shadow: pair.shadow };
        if (pair.b === clanId && pair.shadow === null)
            return { opponent: pair.a, shadow: null };
    }
    return null;
}

function clanWarUnits(counts: { [counter: string]: number }, shield: number): { [front: string]: number } {
    const units: { [front: string]: number } = {};
    for (const front of ClanWarServerRules.fronts) {
        if (front.layer)
            units[front.id] = shield / RaidServerRules.maxSharesPerSecond;
        else
            units[front.id] = (counts[front.counter] || 0) / ChallengeServerRules.capsPerSecond[front.counter];
    }
    return units;
}

function clanWarAdd(score: ClanWarScore, userId: string, units: { [front: string]: number }): void {
    const mine = score.members[userId] || {};
    for (const front of ClanWarServerRules.fronts) {
        const had = mine[front.id] || 0;
        const add = Math.max(0, Math.min(units[front.id] || 0, ClanWarServerRules.perMemberSeconds - had));
        mine[front.id] = had + add;
        score.fronts[front.id] = (score.fronts[front.id] || 0) + add;
    }
    score.members[userId] = mine;
}

function clanWarEmptyScore(clanId: string, week: number): ClanWarScore {
    return { clanId: clanId, week: week, fronts: {}, members: {} };
}

function clanWarNote(nk: nkruntime.Nakama, logger: nkruntime.Logger, group: nkruntime.Group, userId: string, counts: { [counter: string]: number }, shield: number, finishedChallenges: number, nowUnixMs: number): void {
    const week = raidWeekOf(nowUnixMs);
    const day = clanWarDayOf(nowUnixMs);
    const rules = ClanWarServerRules;
    if (day < rules.pairDay) {
        clanWarNoteEntry(nk, group.id, week, (entry) => {
            let changed = false;
            if (entry.active.indexOf(userId) < 0) {
                entry.active.push(userId);
                changed = true;
            }
            if (finishedChallenges > 0) {
                entry.challenges += finishedChallenges;
                changed = true;
            }
            return changed;
        });
        return;
    }
    if (day !== rules.warDay || !clanWarPairOf(clanWarPairsOf(nk, logger, week, nowUnixMs), group.id))
        return;
    const units = clanWarUnits(counts, shield);
    clanWarChange<ClanWarScore>(nk, ClanWarScoreCollection, clanWarKey(week, group.id), () => clanWarEmptyScore(group.id, week), (score) => {
        clanWarAdd(score, userId, units);
        return true;
    });
}

function clanWarFinal(score: ClanWarScore | null, morale: number): { [front: string]: number } {
    const fronts: { [front: string]: number } = {};
    for (const front of ClanWarServerRules.fronts)
        fronts[front.id] = (score ? score.fronts[front.id] || 0 : 0) * (1 + morale);
    return fronts;
}

function clanWarOutcome(ours: { [front: string]: number }, theirs: { [front: string]: number }, ourMorale: number, theirMorale: number): { points: number; theirPoints: number; outcome: string } {
    let points = 0;
    let theirPoints = 0;
    for (const front of ClanWarServerRules.fronts) {
        const a = ours[front.id] || 0;
        const b = theirs[front.id] || 0;
        if (a > b)
            points++;
        else if (b > a)
            theirPoints++;
    }
    const outcome = points > theirPoints ? "win" : theirPoints > points ? "loss"
        : ourMorale > theirMorale ? "win" : theirMorale > ourMorale ? "loss" : "draw";
    return { points: points, theirPoints: theirPoints, outcome: outcome };
}

function clanWarSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, clanId: string, week: number, nowUnixMs: number): ClanWarResult | null {
    const existing = clanWarRead<ClanWarResult>(nk, ClanWarResultCollection, clanWarKey(week, clanId)).value;
    if (existing)
        return existing;
    const pair = clanWarPairOf(clanWarRead<ClanWarPairs>(nk, ClanWarPairsCollection, String(week)).value, clanId);
    if (!pair)
        return null;
    const rules = ClanWarServerRules;
    const entry = clanWarRead<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, clanId)).value;
    const score = clanWarRead<ClanWarScore>(nk, ClanWarScoreCollection, clanWarKey(week, clanId)).value;
    const morale = clanWarMorale(entry);
    const ours = clanWarFinal(score, morale);
    let theirs: { [front: string]: number };
    let theirMorale = 0;
    if (pair.shadow !== null) {
        theirs = pair.shadow;
    } else {
        const theirEntry = clanWarRead<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, pair.opponent)).value;
        theirMorale = clanWarMorale(theirEntry);
        theirs = clanWarFinal(clanWarRead<ClanWarScore>(nk, ClanWarScoreCollection, clanWarKey(week, pair.opponent)).value, theirMorale);
    }
    const bye = pair.shadow !== null && pair.opponent === "";
    const decided = bye ? { points: 0, theirPoints: 0, outcome: "draw" } : clanWarOutcome(ours, theirs, morale, theirMorale);
    const fighters = score ? Object.keys(score.members).filter((userId) => ClanWarServerRules.fronts.some((front) => (score.members[userId][front.id] || 0) > 0)) : [];
    const factor = decided.outcome === "win" ? rules.sealsWin : decided.outcome === "draw" ? rules.sealsDraw : rules.sealsLoss;
    const seals = Math.round(rules.sealsPerMember * fighters.length * factor);
    const ratingDelta = decided.outcome === "win" ? rules.ratingWin : decided.outcome === "draw" ? rules.ratingDraw : rules.ratingLoss;
    const clan = clanWarClanOf(nk, clanId, week);
    const rating = Math.max(0, clan.rating + ratingDelta);
    const result: ClanWarResult = {
        week: week, clanId: clanId, opponent: pair.opponent, shadow: pair.shadow !== null, ours: ours, theirs: theirs,
        points: decided.points, theirPoints: decided.theirPoints, outcome: decided.outcome, seals: seals, rating: rating, league: clanWarLeague(rating)
    };
    try {
        nk.storageWrite([{ collection: ClanWarResultCollection, key: clanWarKey(week, clanId), userId: PartySystemUser, value: result as any, version: "*", permissionRead: 0, permissionWrite: 0 }]);
    } catch (error) {
        return clanWarRead<ClanWarResult>(nk, ClanWarResultCollection, clanWarKey(week, clanId)).value;
    }
    nk.storageWrite([{ collection: ClanWarClanCollection, key: clanId, userId: PartySystemUser, value: { rating: rating, season: clan.season, lastWeek: week, wins: clan.wins + (result.outcome === "win" ? 1 : 0) } as any, permissionRead: 0, permissionWrite: 0 }]);
    try {
        clanCosmeticNoteWar(nk, clanId, result.outcome, result.league, (week + 1) % rules.seasonWeeks === 0, nowUnixMs);
    } catch (error) {
        logger.warn("clan %s: war banners were not saved: %s", clanId, String(error));
    }
    if (seals > 0) {
        try {
            clanTreeChange(nk, clanId, (tree) => {
                tree.treasury += seals;
                clanTreeLog(tree, { at: nowUnixMs, userId: "", kind: "war", amount: seals, node: "" });
                return true;
            });
        } catch (error) {
            logger.warn("clan %s: war seals were not added to the treasury: %s", clanId, String(error));
        }
    }
    const group = clanGroupById(nk, clanId, nowUnixMs);
    const opponentGroup = pair.opponent ? clanGroupById(nk, pair.opponent, nowUnixMs) : null;
    clanChronicleNote(nk, logger, clanId, result.outcome === "win" ? { warsWon: 1 } : {},
        { at: nowUnixMs, kind: "war_" + result.outcome, name: opponentGroup ? clanWarName(opponentGroup) : "", feat: "", level: 0, detail: pair.opponent }, group ? clanMetaOf(group).members : []);
    logger.info("clan %s: war of week %d settled, %s %d:%d, %d seals", clanId, week, result.outcome, result.points, result.theirPoints, seals);
    return result;
}

function clanWarName(group: nkruntime.Group): string {
    const meta = clanMetaOf(group);
    return meta.adjective + " " + meta.noun;
}

function clanWarSettleDue(nk: nkruntime.Nakama, logger: nkruntime.Logger, clanId: string, nowUnixMs: number): void {
    const week = raidWeekOf(nowUnixMs);
    const day = clanWarDayOf(nowUnixMs);
    const due: number[] = [];
    if (day > ClanWarServerRules.warDay)
        due.push(week);
    due.push(week - 1);
    for (const target of due) {
        if (target < 0)
            continue;
        if (!clanWarRead<ClanWarPairs>(nk, ClanWarPairsCollection, String(target)).value && target < week)
            continue;
        clanWarSettle(nk, logger, clanId, target, nowUnixMs);
    }
}

function clanWarView(nk: nkruntime.Nakama, logger: nkruntime.Logger, group: nkruntime.Group, userId: string, nowUnixMs: number): { [key: string]: any } {
    const rules = ClanWarServerRules;
    const week = raidWeekOf(nowUnixMs);
    const day = clanWarDayOf(nowUnixMs);
    clanWarSettleDue(nk, logger, group.id, nowUnixMs);
    const entry = clanWarRead<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, group.id)).value;
    const pairs = day >= rules.pairDay ? clanWarPairsOf(nk, logger, week, nowUnixMs) : null;
    const pair = clanWarPairOf(pairs, group.id);
    const phase = day < rules.pairDay ? "gather" : day < rules.warDay ? "paired" : day === rules.warDay ? "war" : "results";
    const clan = clanWarClanOf(nk, group.id, week);
    const morale = clanWarMorale(entry);
    let opponent: { [key: string]: any } | null = null;
    let fronts: { [key: string]: any } | null = null;
    if (pair) {
        const other = pair.opponent ? clanGroupById(nk, pair.opponent, nowUnixMs) : null;
        const otherEntry = pair.opponent && pair.shadow === null ? clanWarRead<ClanWarEntry>(nk, ClanWarEntryCollection, clanWarKey(week, pair.opponent)).value : null;
        opponent = {
            clanId: pair.opponent, name: other ? clanWarName(other) : "", shadow: pair.shadow !== null, bye: pair.shadow !== null && pair.opponent === "",
            active: otherEntry ? otherEntry.active.length : 0, morale: clanWarMorale(otherEntry)
        };
        if (day >= rules.warDay) {
            const score = clanWarRead<ClanWarScore>(nk, ClanWarScoreCollection, clanWarKey(week, group.id)).value;
            const ours = clanWarFinal(score, morale);
            const theirs = pair.shadow !== null ? pair.shadow : clanWarFinal(clanWarRead<ClanWarScore>(nk, ClanWarScoreCollection, clanWarKey(week, pair.opponent)).value, opponent.morale);
            const mine = score && score.members[userId] ? score.members[userId] : {};
            fronts = { ours: ours, theirs: theirs, mine: mine, cap: rules.perMemberSeconds };
        }
    }
    const result = clanWarRead<ClanWarResult>(nk, ClanWarResultCollection, clanWarKey(day > rules.warDay ? week : week - 1, group.id)).value;
    return {
        week: week, phase: phase, day: day, warStartsAt: raidWeekStart(week) + rules.warDay * PartyDayMs, warEndsAt: raidWeekStart(week) + (rules.warDay + 1) * PartyDayMs,
        joined: !(entry && entry.out), active: entry ? entry.active.length : 0, minActive: rules.minActive, morale: morale,
        rating: clan.rating, league: clanWarLeague(clan.rating), season: clan.season, wins: clan.wins,
        opponent: opponent, fronts: fronts,
        last: result ? { week: result.week, outcome: result.outcome, points: result.points, theirPoints: result.theirPoints, seals: result.seals, rating: result.rating, league: result.league } : null
    };
}

function clanWarViewRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    return JSON.stringify(clanWarView(nk, logger, clanOf(nk, userId, now), userId, now));
}

function clanWarJoin(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, join: boolean, nowUnixMs: number): { [key: string]: any } {
    const group = clanOf(nk, userId, nowUnixMs);
    const role = clanRoleOf(clanMetaOf(group), userId);
    if (role !== ClanLeader && role !== ClanOfficer)
        throw new Error("only the leader or an officer can do this");
    if (clanWarDayOf(nowUnixMs) >= ClanWarServerRules.pairDay)
        throw new Error("pairs are already made this week");
    clanWarNoteEntry(nk, group.id, raidWeekOf(nowUnixMs), (entry) => {
        if (entry.out === !join)
            return false;
        entry.out = !join;
        return true;
    });
    return clanWarView(nk, logger, group, userId, nowUnixMs);
}

function clanWarJoinRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.join !== "boolean")
        throw new Error("bad request");
    return JSON.stringify(clanWarJoin(nk, logger, partyCaller(ctx), request.join, Date.now()));
}
