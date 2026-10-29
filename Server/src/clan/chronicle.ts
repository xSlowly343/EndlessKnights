interface ClanFeatRule {
    id: string;
    kind: string;
    levels: number[];
    title: string | null;
}

interface ClanChronicleRules {
    maxRecords: number;
    feats: ClanFeatRule[];
}

interface ClanChronicleRecord {
    at: number;
    kind: string;
    name: string;
    feat: string;
    level: number;
    detail: string;
}

interface ClanChronicleValue {
    stats: { [kind: string]: number };
    feats: { [id: string]: { level: number; at: number } };
    records: ClanChronicleRecord[];
}

declare const ClanChronicleServerRules: ClanChronicleRules;

const ClanChronicleCollection = "clan_chronicle";
const ClanFeatsCollection = "clan_feats";
const ClanChronicleWriteAttempts = 3;
const ClanChronicleHighest = ["membersMax"];

function clanChronicleRead(nk: nkruntime.Nakama, clanId: string): { value: ClanChronicleValue; version: string } {
    const objects = nk.storageRead([{ collection: ClanChronicleCollection, key: clanId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { stats: {}, feats: {}, records: [] }, version: "*" };
    const value = objects[0].value as ClanChronicleValue;
    return { value: { stats: value.stats || {}, feats: value.feats || {}, records: value.records || [] }, version: objects[0].version };
}

function clanChronicleRecord(value: ClanChronicleValue, record: ClanChronicleRecord): void {
    value.records.push(record);
    if (value.records.length > ClanChronicleServerRules.maxRecords)
        value.records.splice(0, value.records.length - ClanChronicleServerRules.maxRecords);
}

function clanChronicleApply(value: ClanChronicleValue, delta: { [kind: string]: number }, now: number): { feat: string; level: number }[] {
    for (const kind of Object.keys(delta)) {
        const before = value.stats[kind] || 0;
        value.stats[kind] = ClanChronicleHighest.indexOf(kind) >= 0 ? Math.max(before, delta[kind]) : before + delta[kind];
    }
    const earned: { feat: string; level: number }[] = [];
    for (const feat of ClanChronicleServerRules.feats) {
        const progress = value.stats[feat.kind] || 0;
        const had = value.feats[feat.id] ? value.feats[feat.id].level : 0;
        let reached = had;
        while (reached < feat.levels.length && progress >= feat.levels[reached])
            reached++;
        for (let level = had + 1; level <= reached; level++) {
            earned.push({ feat: feat.id, level: level });
            clanChronicleRecord(value, { at: now, kind: "feat", name: "", feat: feat.id, level: level, detail: "" });
        }
        if (reached > had)
            value.feats[feat.id] = { level: reached, at: now };
    }
    return earned;
}

function clanChronicleNote(nk: nkruntime.Nakama, logger: nkruntime.Logger | null, clanId: string, delta: { [kind: string]: number }, record: ClanChronicleRecord | null, members: string[]): void {
    const now = Date.now();
    let earned: { feat: string; level: number }[] = [];
    try {
        for (let attempt = 1; ; attempt++) {
            const stored = clanChronicleRead(nk, clanId);
            if (record)
                clanChronicleRecord(stored.value, record);
            earned = clanChronicleApply(stored.value, delta, now);
            try {
                nk.storageWrite([{ collection: ClanChronicleCollection, key: clanId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
                break;
            } catch (error) {
                if (attempt >= ClanChronicleWriteAttempts)
                    throw error;
            }
        }
    } catch (error) {
        if (logger)
            logger.warn("clan %s: chronicle was not saved: %s", clanId, String(error));
        return;
    }
    for (const userId of earned.length > 0 ? members : []) {
        try {
            const objects = nk.storageRead([{ collection: ClanFeatsCollection, key: clanId, userId: userId }]);
            const kept: { feats: { [id: string]: { level: number; at: number } } } = objects.length > 0 ? objects[0].value as any : { feats: {} };
            for (const got of earned)
                if (!kept.feats[got.feat] || kept.feats[got.feat].level < got.level)
                    kept.feats[got.feat] = { level: got.level, at: now };
            nk.storageWrite([{ collection: ClanFeatsCollection, key: clanId, userId: userId, value: kept, permissionRead: 1, permissionWrite: 0 }]);
        } catch (error) {
            continue;
        }
    }
}

function clanChronicleEvent(nk: nkruntime.Nakama, group: nkruntime.Group, kind: string, userId: string | null, detail: string, delta: { [kind: string]: number }): void {
    const name = userId === null ? "" : chronicleNameOf(nk, userId);
    clanChronicleNote(nk, null, group.id, delta, { at: Date.now(), kind: kind, name: name, feat: "", level: 0, detail: detail }, clanMetaOf(group).members);
}

function clanChronicleView(clanId: string, value: ClanChronicleValue): { [key: string]: any } {
    const titles: string[] = [];
    for (const feat of ClanChronicleServerRules.feats)
        if (feat.title !== null && value.feats[feat.id] && value.feats[feat.id].level > 0)
            titles.push(feat.title);
    return {
        clanId: clanId,
        titles: titles,
        stats: value.stats,
        feats: ClanChronicleServerRules.feats.map((feat) => {
            const got = value.feats[feat.id];
            return { id: feat.id, kind: feat.kind, tracked: true, levels: feat.levels, level: got ? got.level : 0, at: got ? got.at : 0, progress: value.stats[feat.kind] || 0 };
        }),
        records: value.records.slice().reverse()
    };
}

function clanChronicleRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const group = clanOf(nk, partyCaller(ctx), Date.now());
    return JSON.stringify(clanChronicleView(group.id, clanChronicleRead(nk, group.id).value));
}
