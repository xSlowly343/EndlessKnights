interface IntakeConfig {
    telemetry: { retentionDays: number; maxPacketsPerDay: number };
    errors: { retentionDays: number; maxReportsPerDay: number; maxMessageChars: number; maxStackChars: number; maxLines: number; maxLineChars: number };
    summary: { maxObjects: number; retentionMarks: number[] };
    characters: string[];
}

declare const IntakeRules: IntakeConfig;

const IntakeSystemUser = "00000000-0000-0000-0000-000000000000";
const IntakeDayMs = 86400000;
const IntakeTelemetry = "telemetry";
const IntakeErrors = "error_reports";
const IntakeMaxTrackedDay = 365;
const IntakeTelemetryFields = ["schema", "installId", "game", "session", "closed", "sessionSeconds", "keysPerMinute", "clicksPerMinute", "mouseMeters",
    "totalPlaySeconds", "firstPrestigeSeconds", "prestiges", "recordStage", "character", "daysSinceFirst", "returnDays"];
const IntakeInvalidArgument = 3;
const IntakeResourceExhausted = 8;
const IntakePermissionDenied = 7;
const IntakeErrorFields = ["schema", "kind", "game", "os", "createdUtc", "message", "stackTrace", "lines"];

function intakeRefuse(message: string, code: number = IntakeInvalidArgument): never {
    throw { message: message, code: code };
}

function intakeServerOnly(ctx: nkruntime.Context): void {
    if (ctx.userId)
        intakeRefuse("not available to players", IntakePermissionDenied);
}

function intakeParse(payload: string, fields: string[]): { [key: string]: any } {
    let value: any;
    try {
        value = JSON.parse(payload);
    } catch (error) {
        intakeRefuse("bad packet");
    }
    if (value === null || typeof value !== "object" || Array.isArray(value))
        intakeRefuse("bad packet");
    for (const key of Object.keys(value))
        if (fields.indexOf(key) < 0)
            intakeRefuse("unknown field " + key.substring(0, 40));
    for (const key of fields)
        if (!(key in value))
            intakeRefuse("missing field " + key);
    return value;
}

function intakeInt(value: any, min: number, max: number, name: string): number {
    if (typeof value !== "number" || Math.floor(value) !== value || value < min || value > max)
        intakeRefuse("bad " + name);
    return value;
}

function intakeNumber(value: any, min: number, max: number, name: string): number {
    if (typeof value !== "number" || !(value >= min && value <= max))
        intakeRefuse("bad " + name);
    return value;
}

function intakeText(value: any, maxChars: number, pattern: RegExp | null, name: string): string {
    if (typeof value !== "string" || value.length > maxChars || (pattern !== null && !pattern.test(value)))
        intakeRefuse("bad " + name);
    return value;
}

function intakeTelemetryPacket(payload: string): { [key: string]: any } {
    const raw = intakeParse(payload, IntakeTelemetryFields);
    const days: number[] = [];
    if (!Array.isArray(raw.returnDays) || raw.returnDays.length > IntakeMaxTrackedDay + 1)
        intakeRefuse("bad returnDays");
    for (const day of raw.returnDays) {
        const checked = intakeInt(day, 0, IntakeMaxTrackedDay, "returnDays");
        if (days.indexOf(checked) < 0)
            days.push(checked);
    }
    days.sort((a, b) => a - b);
    if (raw.character !== "" && IntakeRules.characters.indexOf(raw.character) < 0)
        intakeRefuse("bad character");
    if (typeof raw.closed !== "boolean")
        intakeRefuse("bad closed");
    return {
        schema: intakeInt(raw.schema, 1, 1, "schema"),
        installId: intakeText(raw.installId, 32, /^[0-9a-f]{32}$/, "installId"),
        game: intakeText(raw.game, 32, /^[0-9A-Za-z._+-]*$/, "game"),
        session: intakeInt(raw.session, 0, 10000000, "session"),
        closed: raw.closed,
        sessionSeconds: intakeInt(raw.sessionSeconds, 0, 7 * 86400, "sessionSeconds"),
        keysPerMinute: intakeNumber(raw.keysPerMinute, 0, 2000, "keysPerMinute"),
        clicksPerMinute: intakeNumber(raw.clicksPerMinute, 0, 2000, "clicksPerMinute"),
        mouseMeters: intakeNumber(raw.mouseMeters, 0, 1000000, "mouseMeters"),
        totalPlaySeconds: intakeInt(raw.totalPlaySeconds, 0, 1000000000, "totalPlaySeconds"),
        firstPrestigeSeconds: raw.firstPrestigeSeconds === null ? null : intakeInt(raw.firstPrestigeSeconds, 0, 1000000000, "firstPrestigeSeconds"),
        prestiges: intakeInt(raw.prestiges, 0, 1000000, "prestiges"),
        recordStage: intakeInt(raw.recordStage, 1, 1000000000, "recordStage"),
        character: raw.character,
        daysSinceFirst: intakeInt(raw.daysSinceFirst, 0, IntakeMaxTrackedDay, "daysSinceFirst"),
        returnDays: days
    };
}

function intakeScrub(text: string): string {
    return text
        .replace(/([A-Za-z]:[\\/]+(?:Users|Documents and Settings)[\\/]+)[^\\/\s"'<>|:*?]+/gi, "$1%USER%")
        .replace(/(\/(?:home|Users)\/)[^\/\s"'<>|:*?]+/g, "$1%USER%");
}

function intakeCut(text: string, maxChars: number): string {
    return text.length > maxChars ? text.substring(0, maxChars) : text;
}

function intakeErrorReport(payload: string): { [key: string]: any } {
    const rules = IntakeRules.errors;
    const raw = intakeParse(payload, IntakeErrorFields);
    if (!Array.isArray(raw.lines))
        intakeRefuse("bad lines");
    const lines: string[] = [];
    for (const line of raw.lines.slice(-rules.maxLines)) {
        if (typeof line !== "string")
            intakeRefuse("bad lines");
        lines.push(intakeCut(intakeScrub(line), rules.maxLineChars));
    }
    if (typeof raw.message !== "string" || typeof raw.stackTrace !== "string")
        intakeRefuse("bad message");
    const kind = intakeText(raw.kind, 10, /^(error|crash)$/, "kind");
    const message = intakeCut(intakeScrub(raw.message), rules.maxMessageChars);
    const stackTrace = intakeCut(intakeScrub(raw.stackTrace), rules.maxStackChars);
    return {
        schema: intakeInt(raw.schema, 1, 1, "schema"),
        kind: kind,
        game: intakeText(raw.game, 32, /^[0-9A-Za-z._+-]*$/, "game"),
        os: intakeCut(intakeText(raw.os, 200, null, "os"), 100),
        createdUtc: intakeText(raw.createdUtc, 40, /^[0-9T:.\-+Z ]*$/, "createdUtc"),
        message: message,
        stackTrace: stackTrace,
        lines: lines,
        fingerprint: intakeFingerprint(kind + "\n" + message.split("\n")[0] + "\n" + stackTrace.split("\n")[0])
    };
}

function intakeFingerprint(text: string): string {
    let hash = 5381;
    for (let i = 0; i < text.length; i++)
        hash = ((hash * 33) ^ text.charCodeAt(i)) >>> 0;
    return ("00000000" + hash.toString(16)).slice(-8);
}

function intakeDay(now: number): string {
    return new Date(now).toISOString().substring(0, 10);
}

function intakeAdmit(nk: nkruntime.Nakama, collection: string, limit: number, retentionDays: number, now: number): void {
    const key = collection + ":" + intakeDay(now);
    const objects = nk.storageRead([{ collection: "intake_days", key: key, userId: IntakeSystemUser }]);
    const count = objects.length > 0 ? Number(objects[0].value.count) || 0 : 0;
    if (count >= limit)
        intakeRefuse("daily limit reached", IntakeResourceExhausted);
    nk.storageWrite([{ collection: "intake_days", key: key, userId: IntakeSystemUser, value: { count: count + 1 }, permissionRead: 0, permissionWrite: 0 }]);
    if (objects.length === 0)
        intakeSweep(nk, collection, now - retentionDays * IntakeDayMs);
}

function intakeSweep(nk: nkruntime.Nakama, collection: string, before: number): number {
    let removed = 0;
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(IntakeSystemUser, collection, 100, cursor);
        const old = (page.objects || []).filter((object) => !(Number(object.value.receivedAt) >= before));
        if (old.length > 0)
            nk.storageDelete(old.map((object) => ({ collection: collection, key: object.key, userId: IntakeSystemUser })));
        removed += old.length;
        cursor = page.cursor || undefined;
    } while (cursor);
    return removed;
}

function intakeAll(nk: nkruntime.Nakama, collection: string): nkruntime.StorageObject[] {
    const all: nkruntime.StorageObject[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(IntakeSystemUser, collection, 100, cursor);
        for (const object of page.objects || [])
            all.push(object);
        cursor = page.cursor || undefined;
    } while (cursor && all.length < IntakeRules.summary.maxObjects);
    return all;
}

function intakePercentile(values: number[], share: number): number | null {
    if (values.length === 0)
        return null;
    const sorted = values.slice().sort((a, b) => a - b);
    const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(share * sorted.length) - 1));
    return sorted[index];
}

function intakeSpread(values: number[]): { [key: string]: any } {
    return { count: values.length, p25: intakePercentile(values, 0.25), median: intakePercentile(values, 0.5), p75: intakePercentile(values, 0.75), p90: intakePercentile(values, 0.9) };
}

function telemetryPushRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    intakeServerOnly(ctx);
    const packet = intakeTelemetryPacket(payload);
    const now = Date.now();
    const key = packet.installId + ":" + packet.session;
    const existing = nk.storageRead([{ collection: IntakeTelemetry, key: key, userId: IntakeSystemUser }]);
    if (existing.length > 0 && existing[0].value.closed === true && !packet.closed)
        return JSON.stringify({ ok: true });
    intakeAdmit(nk, IntakeTelemetry, IntakeRules.telemetry.maxPacketsPerDay, IntakeRules.telemetry.retentionDays, now);
    packet.receivedAt = now;
    nk.storageWrite([{ collection: IntakeTelemetry, key: key, userId: IntakeSystemUser, value: packet, permissionRead: 0, permissionWrite: 0 }]);
    return JSON.stringify({ ok: true });
}

function errorReportPushRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    intakeServerOnly(ctx);
    const report = intakeErrorReport(payload);
    const now = Date.now();
    intakeAdmit(nk, IntakeErrors, IntakeRules.errors.maxReportsPerDay, IntakeRules.errors.retentionDays, now);
    report.receivedAt = now;
    nk.storageWrite([{ collection: IntakeErrors, key: now + ":" + nk.uuidv4().substring(0, 8), userId: IntakeSystemUser, value: report, permissionRead: 0, permissionWrite: 0 }]);
    return JSON.stringify({ ok: true });
}

function telemetrySummaryRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    intakeServerOnly(ctx);
    const packets = intakeAll(nk, IntakeTelemetry).map((object) => object.value);
    const latest: { [installId: string]: { [key: string]: any } } = {};
    for (const packet of packets) {
        const current = latest[packet.installId];
        if (!current || packet.session > current.session)
            latest[packet.installId] = packet;
    }
    const installs = Object.keys(latest).map((id) => latest[id]);
    const retention: { [key: string]: any } = {};
    for (const day of IntakeRules.summary.retentionMarks) {
        const eligible = installs.filter((install) => install.daysSinceFirst >= day);
        const returned = eligible.filter((install) => install.returnDays.indexOf(day) >= 0);
        retention["d" + day] = { eligible: eligible.length, returned: returned.length, share: eligible.length > 0 ? returned.length / eligible.length : null };
    }
    const closed = packets.filter((packet) => packet.closed && packet.sessionSeconds >= 60);
    const versions: { [game: string]: number } = {};
    const characters: { [character: string]: number } = {};
    for (const install of installs) {
        versions[install.game] = (versions[install.game] || 0) + 1;
        if (install.character)
            characters[install.character] = (characters[install.character] || 0) + 1;
    }
    const prestiged = installs.filter((install) => install.firstPrestigeSeconds !== null).map((install) => install.firstPrestigeSeconds);
    return JSON.stringify({
        installs: installs.length,
        sessions: packets.length,
        retention: retention,
        firstPrestigeSeconds: intakeSpread(prestiged),
        withoutPrestige: installs.length - prestiged.length,
        keysPerMinute: intakeSpread(closed.map((packet) => packet.keysPerMinute)),
        clicksPerMinute: intakeSpread(closed.map((packet) => packet.clicksPerMinute)),
        sessionSeconds: intakeSpread(closed.map((packet) => packet.sessionSeconds)),
        recordStage: intakeSpread(installs.map((install) => install.recordStage)),
        versions: versions,
        characters: characters
    });
}

function errorReportListRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    intakeServerOnly(ctx);
    const request = payload ? JSON.parse(payload) : {};
    const limit = typeof request.limit === "number" && request.limit > 0 ? Math.min(200, Math.floor(request.limit)) : 50;
    const groups: { [fingerprint: string]: { [key: string]: any } } = {};
    for (const object of intakeAll(nk, IntakeErrors)) {
        const report = object.value;
        const group = groups[report.fingerprint] || (groups[report.fingerprint] = { fingerprint: report.fingerprint, kind: report.kind, count: 0, firstAt: report.receivedAt, lastAt: 0, games: [] });
        group.count++;
        group.firstAt = Math.min(group.firstAt, report.receivedAt);
        if (group.games.indexOf(report.game) < 0)
            group.games.push(report.game);
        if (report.receivedAt >= group.lastAt) {
            group.lastAt = report.receivedAt;
            group.message = report.message;
            group.stackTrace = report.stackTrace;
            group.os = report.os;
        }
    }
    const list = Object.keys(groups).map((key) => groups[key]).sort((a, b) => b.count - a.count || b.lastAt - a.lastAt);
    return JSON.stringify({ groups: list.slice(0, limit), total: list.length });
}
