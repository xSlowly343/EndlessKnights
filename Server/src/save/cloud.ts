interface CloudRules {
    maxBytes: number;
    backups: number;
    playSlackSeconds: number;
    playRate: number;
    stagesPerHour: number;
    stageSlack: number;
}

interface CloudFacts {
    record: number;
    playSeconds: number;
    savedAt: number;
    bag: { [item: string]: number };
    found: string[];
    partyPieces?: string[];
}

interface CloudMeta {
    at: number;
    record: number;
    playSeconds: number;
    savedAt: number;
    bytes: number;
    trusted: boolean;
    reason: string;
}

declare const CloudServerRules: CloudRules;

const CloudCollection = "cloud_save";
const CloudSaveKey = "save";
const CloudTrustedKey = "trusted";
const CloudBackupKey = "backup";

function cloudNumber(value: unknown): number {
    return typeof value === "number" && value >= 0 && value < 1e15 ? value : 0;
}

function cloudList(value: unknown): any[] {
    return Array.isArray(value) ? value : [];
}

function cloudIds(value: unknown, field: string | null): string[] {
    return cloudList(value).map((item) => field === null ? item : item && item[field]).filter((id) => typeof id === "string");
}

function cloudFacts(text: string): CloudFacts {
    let root: any;
    try {
        root = JSON.parse(text);
    } catch (error) {
        throw new Error("the save is not readable");
    }
    const payload = root && root.payload;
    const state = payload && payload.state;
    if (!state || typeof state !== "object")
        throw new Error("the save is not readable");
    const bag: { [item: string]: number } = {};
    const add = (item: string, count: number) => {
        if (count > 0)
            bag[item] = (bag[item] || 0) + count;
    };
    for (const entry of cloudList(state.camp && state.camp.bag))
        if (entry && typeof entry.item === "string")
            add(entry.item, cloudNumber(entry.count));
    for (const meal of cloudIds(state.cooking && state.cooking.meals, null))
        add("dish:" + meal, 1);
    for (const potion of cloudList(state.alchemy && state.alchemy.potions))
        if (potion && typeof potion.recipe === "string" && potion.unknown !== true)
            add("potion:" + potion.recipe + (potion.fine === true ? ":fine" : ""), 1);
    const found = cloudIds(state.fishing && state.fishing.album, "id")
        .concat(cloudIds(state.garden && state.garden.album, null), cloudIds(state.cooking && state.cooking.book, null),
            cloudIds(state.alchemy && state.alchemy.known, null), cloudIds(state.digs && state.digs.skeletons, null),
            cloudIds(state.expeditions && state.expeditions.finds, null), cloudIds(state.museum && state.museum.done, null));
    return {
        record: cloudNumber(state.stage && state.stage.record),
        playSeconds: cloudNumber(state.inputStats && state.inputStats.playSeconds),
        savedAt: cloudNumber(payload.savedAt),
        bag: bag,
        found: found.filter((id, index) => found.indexOf(id) === index),
        partyPieces: cloudIds(state.setCollection, null).filter((key) => key.indexOf("party_") === 0)
    };
}

function cloudTrustedRead(nk: nkruntime.Nakama, userId: string): { facts: CloudFacts; at: number } | null {
    const objects = nk.storageRead([{ collection: CloudCollection, key: CloudTrustedKey, userId: userId }]);
    return objects.length > 0 ? objects[0].value as { facts: CloudFacts; at: number } : null;
}

function cloudAccountMs(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): number {
    const users = nk.usersGetId([userId]);
    const created = users.length > 0 && typeof (users[0] as any).createTime === "number" ? (users[0] as any).createTime * 1000 : nowUnixMs;
    return Math.max(0, nowUnixMs - created);
}

function cloudDoubt(nk: nkruntime.Nakama, userId: string, facts: CloudFacts, nowUnixMs: number): string {
    const rules = CloudServerRules;
    const trusted = cloudTrustedRead(nk, userId);
    const prev: CloudFacts = trusted ? trusted.facts : { record: 0, playSeconds: 0, savedAt: 0, bag: {}, found: [] };
    const elapsedMs = trusted ? nowUnixMs - trusted.at : Math.max(cloudAccountMs(nk, userId, nowUnixMs), facts.playSeconds * 1000);
    const played = facts.playSeconds - prev.playSeconds;
    if (trusted && played > elapsedMs / 1000 * rules.playRate + rules.playSlackSeconds)
        return "more play time than real time";
    const hours = Math.max(played, 0) / 3600;
    if (facts.record - prev.record > rules.stagesPerHour * hours + rules.stageSlack)
        return "the record grew too fast";
    const ledger = inflowRead(nk, userId).value;
    const days = Math.max(1, Math.ceil(elapsedMs / 86400000));
    const grown: { [group: string]: number } = {};
    for (const item of Object.keys(facts.bag)) {
        const before = trusted ? (prev.bag[item] || 0) - (ledger.spent[item] || 0) + (ledger.held[item] || 0) : 0;
        const more = facts.bag[item] - Math.max(0, before);
        if (more > 0)
            grown[inflowGroup(item)] = (grown[inflowGroup(item)] || 0) + more;
    }
    for (const group of Object.keys(grown))
        if (grown[group] > inflowLimit(group) * days)
            return "more " + group + " than could be gathered";
    return "";
}

function cloudMetaOf(nk: nkruntime.Nakama, userId: string): CloudMeta | null {
    const objects = nk.storageRead([{ collection: CloudCollection, key: CloudSaveKey, userId: userId }]);
    return objects.length > 0 ? (objects[0].value as { meta: CloudMeta }).meta : null;
}

function cloudPut(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const text = request && request.save;
    const force = request && request.force === true;
    if (typeof text !== "string" || text.length === 0)
        throw new Error("bad request");
    if (text.length > CloudServerRules.maxBytes)
        throw new Error("the save is too big");
    const facts = cloudFacts(text);
    const current = cloudMetaOf(nk, userId);
    if (current && !force && facts.playSeconds + CloudServerRules.playSlackSeconds < current.playSeconds)
        return { status: "older", server: current };
    const reason = cloudDoubt(nk, userId, facts, nowUnixMs);
    const meta: CloudMeta = { at: nowUnixMs, record: facts.record, playSeconds: facts.playSeconds, savedAt: facts.savedAt, bytes: text.length, trusted: reason.length === 0, reason: reason };
    const objects: nkruntime.StorageWriteRequest[] = [];
    const stored = nk.storageRead([{ collection: CloudCollection, key: CloudSaveKey, userId: userId }]);
    if (stored.length > 0) {
        const slot = Math.floor(nowUnixMs / 1000) % CloudServerRules.backups;
        objects.push({ collection: CloudCollection, key: CloudBackupKey + slot, userId: userId, value: stored[0].value, permissionRead: 1, permissionWrite: 0 });
    }
    objects.push({ collection: CloudCollection, key: CloudSaveKey, userId: userId, value: { meta: meta, text: text } as any, permissionRead: 1, permissionWrite: 0 });
    if (meta.trusted)
        objects.push({ collection: CloudCollection, key: CloudTrustedKey, userId: userId, value: { facts: facts, at: nowUnixMs } as any, permissionRead: 0, permissionWrite: 0 });
    nk.storageWrite(objects);
    if (meta.trusted)
        inflowBaseline(nk, userId, facts.bag);
    else
        logger.warn("cloud: %s uploaded a doubtful save: %s", userId, reason);
    return { status: meta.trusted ? "saved" : "doubtful", server: meta };
}

function cloudGet(nk: nkruntime.Nakama, userId: string, backup: unknown): { [key: string]: any } {
    const key = typeof backup === "number" && backup >= 0 && backup < CloudServerRules.backups ? CloudBackupKey + backup : CloudSaveKey;
    const objects = nk.storageRead([{ collection: CloudCollection, key: key, userId: userId }]);
    if (objects.length === 0)
        return { save: null, server: null };
    const value = objects[0].value as { meta: CloudMeta; text: string };
    return { save: value.text, server: value.meta };
}

function cloudFound(nk: nkruntime.Nakama, userId: string): string[] {
    const trusted = cloudTrustedRead(nk, userId);
    return trusted ? trusted.facts.found : [];
}

function cloudPutRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(cloudPut(nk, logger, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function cloudGetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(cloudGet(nk, partyCaller(ctx), payload ? partyRequest(payload).backup : undefined));
}

function cloudMetaRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify({ server: cloudMetaOf(nk, partyCaller(ctx)) });
}
