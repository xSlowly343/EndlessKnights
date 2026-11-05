interface WeeklyRules {
    trials: string[];
    count: number;
    boardSize: number;
    maxFriends: number;
    classes: number;
    maxStage: number;
}

declare const WeeklyServerRules: WeeklyRules;

interface WeeklyRow {
    rank: number;
    userId: string;
    name: string;
    stage: number;
    heroClass: number;
}

const WeeklyDayMs = 86400000;
const WeeklyBoardPrefix = "weekly_";

function weeklyWeekOf(unixMs: number): number {
    const day = Math.floor((unixMs + WalletServerRules.dayUtcOffsetMinutes * 60000) / WeeklyDayMs);
    return Math.floor((day + 3) / 7);
}

function weeklyTrials(week: number): string[] {
    const all = WeeklyServerRules.trials;
    const count = Math.min(WeeklyServerRules.count, all.length);
    const picked: string[] = [];
    for (let i = 0; i < count; i++)
        picked.push(all[((week * WeeklyServerRules.count + i) % all.length + all.length) % all.length]);
    return picked;
}

function weeklyBoardId(week: number, trial: string): string {
    return WeeklyBoardPrefix + week + "_" + trial;
}

function weeklyEnsureBoard(nk: nkruntime.Nakama, id: string): void {
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
}

function weeklyRow(record: nkruntime.LeaderboardRecord): WeeklyRow {
    const metadata = record.metadata || {};
    return {
        rank: record.rank,
        userId: record.ownerId,
        name: record.username || "",
        stage: record.score,
        heroClass: typeof metadata.heroClass === "number" ? metadata.heroClass : -1
    };
}

function weeklySubmit(nk: nkruntime.Nakama, userId: string, request: { week?: number; trial?: string; stage?: number; heroClass?: number }, nowUnixMs: number): WeeklyRow {
    const now = weeklyWeekOf(nowUnixMs);
    const week = request.week;
    if (typeof week !== "number" || week !== Math.floor(week) || Math.abs(week - now) > 1)
        throw new Error("not this week");
    const trial = request.trial;
    if (typeof trial !== "string" || weeklyTrials(week).indexOf(trial) < 0)
        throw new Error("not a trial of this week");
    const stage = request.stage;
    if (typeof stage !== "number" || stage !== Math.floor(stage) || stage < 1 || stage > WeeklyServerRules.maxStage)
        throw new Error("bad stage");
    const heroClass = request.heroClass;
    if (typeof heroClass !== "number" || heroClass !== Math.floor(heroClass) || heroClass < 0 || heroClass >= WeeklyServerRules.classes)
        throw new Error("bad class");
    const id = weeklyBoardId(week, trial);
    weeklyEnsureBoard(nk, id);
    const users = nk.usersGetId([userId]);
    const name = users.length > 0 ? users[0].displayName || users[0].username : "";
    return weeklyRow(nk.leaderboardRecordWrite(id, userId, name, stage, 0, { heroClass: heroClass }));
}

function weeklyBoard(nk: nkruntime.Nakama, userId: string, request: { trial?: string; friends?: string[] }, nowUnixMs: number) {
    const week = weeklyWeekOf(nowUnixMs);
    const trials = weeklyTrials(week);
    const trial = request.trial;
    if (trial === undefined || trial === null)
        return { week: week, trials: trials };
    if (typeof trial !== "string" || trials.indexOf(trial) < 0)
        throw new Error("not a trial of this week");
    const friends = Array.isArray(request.friends) ? request.friends.filter((id) => typeof id === "string").slice(0, WeeklyServerRules.maxFriends) : [];
    const id = weeklyBoardId(week, trial);
    weeklyEnsureBoard(nk, id);
    const top = nk.leaderboardRecordsList(id, [], WeeklyServerRules.boardSize);
    const owners = nk.leaderboardRecordsList(id, [userId].concat(friends), 0);
    const ownerRows = (owners.ownerRecords || []).map(weeklyRow);
    let mine: WeeklyRow | null = null;
    const friendRows: WeeklyRow[] = [];
    for (const row of ownerRows) {
        if (row.userId === userId)
            mine = row;
        friendRows.push(row);
    }
    friendRows.sort((a, b) => b.stage - a.stage || a.rank - b.rank);
    return { week: week, trials: trials, trial: trial, top: (top.records || []).map(weeklyRow), mine: mine, friends: friendRows };
}

function weeklySubmitRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = JSON.parse(payload || "{}");
    return JSON.stringify(weeklySubmit(nk, partyCaller(ctx), request, Date.now()));
}

function weeklyBoardRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = JSON.parse(payload || "{}");
    return JSON.stringify(weeklyBoard(nk, partyCaller(ctx), request, Date.now()));
}
