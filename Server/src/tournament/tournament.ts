interface TournamentFormatRule {
    id: string;
    maxScore: number;
}

interface TournamentRules {
    groupSize: number;
    startDays: number[];
    joinClosesMs: number;
    formats: TournamentFormatRule[];
    bands: { from: number; scale: number }[];
    rewards: { upTo: number; ingots: number; essence: number }[];
    maxRecord: number;
    kept: number;
}

interface TournamentEntry {
    index: number;
    format: string;
    band: number;
    group: number;
    rewarded: boolean;
}

interface TournamentMineValue {
    entries: TournamentEntry[];
}

interface TournamentRow {
    rank: number;
    userId: string;
    name: string;
    score: number;
}

declare const TournamentServerRules: TournamentRules;

const TournamentCollection = "tournaments";
const TournamentKey = "mine";
const TournamentGroupCollection = "tournament_groups";
const TournamentBoardPrefix = "tour_";
const TournamentWriteAttempts = 4;

function tournamentIndexOf(nowUnixMs: number): number {
    const days = TournamentServerRules.startDays;
    const week = weeklyWeekOf(nowUnixMs);
    const day = Math.floor((nowUnixMs - seasonPassWeekStart(week)) / PartyDayMs);
    let slot = -1;
    for (let i = 0; i < days.length; i++)
        if (days[i] <= day)
            slot = i;
    return slot < 0 ? week * days.length - 1 : week * days.length + slot;
}

function tournamentStartsAt(index: number): number {
    const days = TournamentServerRules.startDays;
    const week = Math.floor(index / days.length);
    return seasonPassWeekStart(week) + days[index - week * days.length] * PartyDayMs;
}

function tournamentEndsAt(index: number): number {
    return tournamentStartsAt(index + 1);
}

function tournamentFormat(index: number): TournamentFormatRule {
    const formats = TournamentServerRules.formats;
    return formats[((index % formats.length) + formats.length) % formats.length];
}

function tournamentBandOf(record: number): number {
    let band = 0;
    TournamentServerRules.bands.forEach((entry, i) => {
        if (record >= entry.from)
            band = i;
    });
    return band;
}

function tournamentReward(place: number, band: number): { ingots: number; essence: number } | null {
    const scale = TournamentServerRules.bands[band] ? TournamentServerRules.bands[band].scale : 0;
    for (const reward of TournamentServerRules.rewards)
        if (place >= 1 && place <= reward.upTo)
            return { ingots: Math.round(reward.ingots * scale), essence: Math.round(reward.essence * scale) };
    return null;
}

function tournamentBoardId(entry: TournamentEntry): string {
    return TournamentBoardPrefix + entry.index + "_" + entry.band + "_" + entry.group;
}

function tournamentRead(nk: nkruntime.Nakama, userId: string): { value: TournamentMineValue; version: string } {
    const objects = nk.storageRead([{ collection: TournamentCollection, key: TournamentKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { entries: [] }, version: "*" };
    const value = objects[0].value as TournamentMineValue;
    return { value: { entries: value.entries || [] }, version: objects[0].version };
}

function tournamentChange<T>(nk: nkruntime.Nakama, userId: string, change: (value: TournamentMineValue) => T): T {
    for (let attempt = 1; attempt <= TournamentWriteAttempts; attempt++) {
        const stored = tournamentRead(nk, userId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: TournamentCollection, key: TournamentKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === TournamentWriteAttempts)
                throw new Error("tournaments are busy, try again");
        }
    }
    throw new Error("tournaments are busy, try again");
}

function tournamentTakeGroup(nk: nkruntime.Nakama, index: number, band: number): number {
    const key = String(index);
    for (let attempt = 1; attempt <= TournamentWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: TournamentGroupCollection, key: key, userId: PartySystemUser }]);
        const counts: { [band: string]: number } = objects.length > 0 ? (objects[0].value as any).counts || {} : {};
        const taken = counts[String(band)] || 0;
        counts[String(band)] = taken + 1;
        try {
            nk.storageWrite([{ collection: TournamentGroupCollection, key: key, userId: PartySystemUser, value: { counts: counts } as any, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0 }]);
            return Math.floor(taken / TournamentServerRules.groupSize);
        } catch (error) {
            if (attempt === TournamentWriteAttempts)
                throw new Error("tournaments are busy, try again");
        }
    }
    throw new Error("tournaments are busy, try again");
}

function tournamentRow(record: nkruntime.LeaderboardRecord): TournamentRow {
    return { rank: record.rank, userId: record.ownerId, name: record.username || "", score: record.score };
}

function tournamentStanding(nk: nkruntime.Nakama, userId: string, entry: TournamentEntry): { top: TournamentRow[]; mine: TournamentRow | null } {
    const id = tournamentBoardId(entry);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    const list = nk.leaderboardRecordsList(id, [userId], TournamentServerRules.groupSize);
    const own = (list.ownerRecords || []).filter((record) => record.ownerId === userId)[0];
    return { top: (list.records || []).map(tournamentRow), mine: own ? tournamentRow(own) : null };
}

function tournamentSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, nowUnixMs: number): string[] {
    const due = tournamentRead(nk, userId).value.entries.filter((entry) => !entry.rewarded && tournamentEndsAt(entry.index) <= nowUnixMs);
    const letters: string[] = [];
    for (const entry of due) {
        const marked = tournamentChange(nk, userId, (value) => {
            const stored = value.entries.filter((item) => item.index === entry.index)[0];
            if (!stored || stored.rewarded)
                return false;
            stored.rewarded = true;
            return true;
        });
        if (!marked)
            continue;
        const standing = tournamentStanding(nk, userId, entry);
        if (!standing.mine)
            continue;
        const reward = tournamentReward(standing.mine.rank, entry.band);
        if (!reward)
            continue;
        const id = "tournament-" + entry.index;
        const sent = mailSend(nk, userId, {
            id: id, kind: "tournament", at: tournamentEndsAt(entry.index), place: standing.mine.rank, of: standing.top.length, depth: standing.mine.score,
            title: entry.format, reward: { shards: 0, ingots: reward.ingots, essence: reward.essence }, claimed: false, claimedAt: 0
        });
        if (sent)
            letters.push(id);
        else
            logger.warn("tournament %d reward letter for %s was not written", entry.index, userId);
    }
    return letters;
}

function tournamentView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, fresh: string[]): { [key: string]: any } {
    const index = tournamentIndexOf(nowUnixMs);
    const endsAt = tournamentEndsAt(index);
    const entry = tournamentRead(nk, userId).value.entries.filter((item) => item.index === index)[0] || null;
    const standing = entry ? tournamentStanding(nk, userId, entry) : { top: [], mine: null };
    return {
        index: index, format: tournamentFormat(index).id, startsAt: tournamentStartsAt(index), endsAt: endsAt,
        joinUntil: endsAt - TournamentServerRules.joinClosesMs, joined: !!entry, band: entry ? entry.band : -1, group: entry ? entry.group : -1,
        top: standing.top, mine: standing.mine, fresh: fresh
    };
}

function tournamentJoin(nk: nkruntime.Nakama, userId: string, record: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof record !== "number" || record !== Math.floor(record) || record < 1 || record > TournamentServerRules.maxRecord)
        throw new Error("bad record");
    const index = tournamentIndexOf(nowUnixMs);
    if (tournamentRead(nk, userId).value.entries.some((item) => item.index === index))
        return tournamentView(nk, userId, nowUnixMs, []);
    if (nowUnixMs >= tournamentEndsAt(index) - TournamentServerRules.joinClosesMs)
        throw new Error("joining is closed");
    const band = tournamentBandOf(record);
    const group = tournamentTakeGroup(nk, index, band);
    const entry: TournamentEntry = { index: index, format: tournamentFormat(index).id, band: band, group: group, rewarded: false };
    tournamentChange(nk, userId, (value) => {
        if (value.entries.some((item) => item.index === index))
            return;
        value.entries.push(entry);
        if (value.entries.length > TournamentServerRules.kept)
            value.entries.splice(0, value.entries.length - TournamentServerRules.kept);
    });
    nk.leaderboardCreate(tournamentBoardId(entry), true, "descending", "best", null, null);
    return tournamentView(nk, userId, nowUnixMs, []);
}

function tournamentSubmit(nk: nkruntime.Nakama, userId: string, request: { index?: unknown; score?: unknown }, nowUnixMs: number): { [key: string]: any } {
    const index = tournamentIndexOf(nowUnixMs);
    if (request.index !== index)
        throw new Error("not this tournament");
    const entry = tournamentRead(nk, userId).value.entries.filter((item) => item.index === index)[0];
    if (!entry)
        throw new Error("not joined");
    const score = request.score;
    const format = tournamentFormat(index);
    if (typeof score !== "number" || score !== Math.floor(score) || score < 0 || score > format.maxScore)
        throw new Error("bad score");
    const id = tournamentBoardId(entry);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    const users = nk.usersGetId([userId]);
    const name = users.length > 0 ? users[0].displayName || users[0].username : "";
    nk.leaderboardRecordWrite(id, userId, name, score, 0, {});
    return tournamentView(nk, userId, nowUnixMs, []);
}

function tournamentMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    return JSON.stringify(tournamentView(nk, userId, now, tournamentSettle(nk, logger, userId, now)));
}

function tournamentJoinRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(tournamentJoin(nk, partyCaller(ctx), partyRequest(payload).record, Date.now()));
}

function tournamentSubmitRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(tournamentSubmit(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}
