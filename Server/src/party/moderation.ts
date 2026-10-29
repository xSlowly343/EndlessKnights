const PartyFlagsCollection = "party_flags";
const PartyReportsCollection = "party_reports";
const PartyModerationDays = 30;
const PartyModerationLimit = 50;
const PartyModerationMaxObjects = 20000;

interface PartyFlagRecord {
    userId: string;
    playerNumber: string;
    flags: number;
    total: number;
    matchId: string;
    at: number;
}

function partyNoteFlags(nk: nkruntime.Nakama, userId: string, playerNumber: string, flags: number, matchId: string, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: PartyFlagsCollection, key: userId, userId: PartySystemUser }]);
    const previous = objects.length > 0 ? objects[0].value as PartyFlagRecord : null;
    const record: PartyFlagRecord = {
        userId: userId, playerNumber: playerNumber, flags: flags, matchId: matchId, at: nowUnixMs,
        total: (previous ? previous.total || 0 : 0) + PartyServerRules.flagsBeforeReport
    };
    nk.storageWrite([{ collection: PartyFlagsCollection, key: userId, userId: PartySystemUser, value: record as any, permissionRead: 0, permissionWrite: 0 }]);
}

function partyListAll(nk: nkruntime.Nakama, collection: string): nkruntime.StorageObject[] {
    const all: nkruntime.StorageObject[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(PartySystemUser, collection, 100, cursor);
        for (const object of page.objects || [])
            all.push(object);
        cursor = page.cursor || undefined;
    } while (cursor && all.length < PartyModerationMaxObjects);
    return all;
}

function partyNames(nk: nkruntime.Nakama, ids: string[]): { [userId: string]: string } {
    const names: { [userId: string]: string } = {};
    if (ids.length === 0)
        return names;
    for (const user of nk.usersGetId(ids))
        names[user.userId] = user.username || "";
    return names;
}

function partyModerationReport(nk: nkruntime.Nakama, days: number, limit: number, nowUnixMs: number): { [key: string]: any } {
    const since = nowUnixMs - days * 86400000;
    const flags = partyListAll(nk, PartyFlagsCollection).map((object) => object.value as PartyFlagRecord)
        .filter((record) => record.at >= since)
        .sort((a, b) => b.total - a.total || b.at - a.at);
    const targets: { [userId: string]: { userId: string; count: number; reporters: string[]; reasons: { [reason: string]: number }; lastAt: number } } = {};
    for (const object of partyListAll(nk, PartyReportsCollection)) {
        const report = object.value as { target: string; reporter: string; reason: string; at: number };
        if (!(report.at >= since))
            continue;
        const target = targets[report.target] || (targets[report.target] = { userId: report.target, count: 0, reporters: [], reasons: {}, lastAt: 0 });
        target.count++;
        if (target.reporters.indexOf(report.reporter) < 0)
            target.reporters.push(report.reporter);
        target.reasons[report.reason] = (target.reasons[report.reason] || 0) + 1;
        target.lastAt = Math.max(target.lastAt, report.at);
    }
    const reported = Object.keys(targets).map((id) => targets[id]).sort((a, b) => b.reporters.length - a.reporters.length || b.lastAt - a.lastAt);
    const shownFlags = flags.slice(0, limit);
    const shownReports = reported.slice(0, limit);
    const ids: string[] = [];
    for (const record of shownFlags)
        if (ids.indexOf(record.userId) < 0)
            ids.push(record.userId);
    for (const target of shownReports)
        if (ids.indexOf(target.userId) < 0)
            ids.push(target.userId);
    const names = partyNames(nk, ids);
    return {
        days: days,
        flagged: shownFlags.map((record) => ({ userId: record.userId, name: names[record.userId] || "", playerNumber: record.playerNumber, total: record.total, lastMatch: record.flags, lastAt: record.at })),
        flaggedTotal: flags.length,
        reported: shownReports.map((target) => ({ userId: target.userId, name: names[target.userId] || "", reports: target.count, reporters: target.reporters.length, reasons: target.reasons, lastAt: target.lastAt })),
        reportedTotal: reported.length
    };
}

function partyModerationRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    if (ctx.userId)
        throw { message: "not available to players", code: 7 };
    let request: any = {};
    try {
        request = payload ? JSON.parse(payload) : {};
    } catch (error) {
        throw { message: "bad request", code: 3 };
    }
    const days = typeof request.days === "number" && request.days > 0 ? Math.min(365, Math.floor(request.days)) : PartyModerationDays;
    const limit = typeof request.limit === "number" && request.limit > 0 ? Math.min(500, Math.floor(request.limit)) : PartyModerationLimit;
    return JSON.stringify(partyModerationReport(nk, days, limit, Date.now()));
}
