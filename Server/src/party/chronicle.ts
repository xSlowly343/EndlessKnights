interface ChronicleFeatRule {
    id: string;
    kind: string;
    levels: number[];
    banner: number[];
    title: string | null;
    tracked: boolean;
}

interface ChronicleRules {
    flushSeconds: number;
    maxRecords: number;
    night: { utcOffsetMinutes: number; fromHour: number; toHour: number };
    feats: ChronicleFeatRule[];
    classRoles: string[];
}

interface ChronicleStats {
    bossesTogether: number;
    ticksTogether: number;
    fullOrchestra: number;
    nightBosses: number;
    riftDepth: number;
    mentorSeconds: number;
    guardiansTogether: number;
    synergiesOnOneEnemy: number;
}

interface ChronicleRecord {
    at: number;
    kind: string;
    name: string;
    feat: string;
    level: number;
}

interface ChronicleValue {
    stats: ChronicleStats;
    feats: { [id: string]: { level: number; at: number } };
    records: ChronicleRecord[];
}

interface ChronicleEarned {
    feat: string;
    level: number;
}

declare const ChronicleServerRules: ChronicleRules;

const ChronicleCollection = "party_chronicle";
const ChronicleFeatsCollection = "party_feats";
const ChronicleWriteAttempts = 3;

function chronicleTicksPerHour(): number {
    return 3600 * PartyTicksPerSecond;
}

function chronicleNoStats(): ChronicleStats {
    return { bossesTogether: 0, ticksTogether: 0, fullOrchestra: 0, nightBosses: 0, riftDepth: 0, mentorSeconds: 0, guardiansTogether: 0, synergiesOnOneEnemy: 0 };
}

function chronicleEmpty(): ChronicleValue {
    return { stats: chronicleNoStats(), feats: {}, records: [] };
}

function chronicleHasStats(stats: ChronicleStats): boolean {
    return stats.bossesTogether > 0 || stats.ticksTogether > 0 || stats.fullOrchestra > 0 || stats.nightBosses > 0 || stats.riftDepth > 0 || stats.mentorSeconds > 0 || stats.guardiansTogether > 0
        || stats.synergiesOnOneEnemy > 0;
}

function chronicleProgress(stats: ChronicleStats, kind: string): number {
    switch (kind) {
        case "bossesTogether": return stats.bossesTogether;
        case "hoursTogether": return stats.ticksTogether / chronicleTicksPerHour();
        case "fullOrchestra": return stats.fullOrchestra;
        case "nightBosses": return stats.nightBosses;
        case "riftDepth": return stats.riftDepth || 0;
        case "mentorHours": return (stats.mentorSeconds || 0) / 3600;
        case "guardiansTogether": return stats.guardiansTogether || 0;
        case "synergiesOnOneEnemy": return stats.synergiesOnOneEnemy || 0;
        default: return 0;
    }
}

function chronicleRecord(value: ChronicleValue, record: ChronicleRecord, rules: ChronicleRules): void {
    value.records.push(record);
    if (value.records.length > rules.maxRecords)
        value.records.splice(0, value.records.length - rules.maxRecords);
}

function chronicleAdd(value: ChronicleValue, delta: ChronicleStats, now: number, rules: ChronicleRules): ChronicleEarned[] {
    value.stats.bossesTogether += delta.bossesTogether;
    value.stats.ticksTogether += delta.ticksTogether;
    value.stats.fullOrchestra += delta.fullOrchestra;
    value.stats.nightBosses += delta.nightBosses;
    value.stats.riftDepth = Math.max(value.stats.riftDepth || 0, delta.riftDepth || 0);
    value.stats.mentorSeconds = (value.stats.mentorSeconds || 0) + (delta.mentorSeconds || 0);
    value.stats.guardiansTogether = (value.stats.guardiansTogether || 0) + (delta.guardiansTogether || 0);
    value.stats.synergiesOnOneEnemy = Math.max(value.stats.synergiesOnOneEnemy || 0, delta.synergiesOnOneEnemy || 0);
    const earned: ChronicleEarned[] = [];
    for (const feat of rules.feats) {
        if (!feat.tracked)
            continue;
        const progress = chronicleProgress(value.stats, feat.kind);
        const had = value.feats[feat.id] ? value.feats[feat.id].level : 0;
        let reached = had;
        while (reached < feat.levels.length && progress >= feat.levels[reached])
            reached++;
        for (let level = had + 1; level <= reached; level++) {
            earned.push({ feat: feat.id, level: level });
            chronicleRecord(value, { at: now, kind: "feat", name: "", feat: feat.id, level: level }, rules);
        }
        if (reached > had)
            value.feats[feat.id] = { level: reached, at: now };
    }
    return earned;
}

function chronicleBanner(value: ChronicleValue, rules: ChronicleRules): number {
    let banner = 0;
    for (const feat of rules.feats) {
        const got = value.feats[feat.id];
        if (got && got.level > 0 && feat.banner.length >= got.level)
            banner = Math.max(banner, feat.banner[got.level - 1]);
    }
    return banner;
}

function chronicleTitles(value: ChronicleValue, rules: ChronicleRules): string[] {
    const titles: string[] = [];
    for (const feat of rules.feats)
        if (feat.title !== null && value.feats[feat.id] && value.feats[feat.id].level > 0)
            titles.push(feat.title);
    return titles;
}

function chronicleView(partyId: string, value: ChronicleValue, rules: ChronicleRules): { [key: string]: any } {
    return {
        partyId: partyId,
        banner: chronicleBanner(value, rules),
        titles: chronicleTitles(value, rules),
        stats: {
            bossesTogether: value.stats.bossesTogether,
            hoursTogether: Math.floor(value.stats.ticksTogether / chronicleTicksPerHour() * 10) / 10,
            fullOrchestra: value.stats.fullOrchestra,
            nightBosses: value.stats.nightBosses,
            riftDepth: value.stats.riftDepth || 0,
            mentorHours: Math.floor((value.stats.mentorSeconds || 0) / 360) / 10,
            guardiansTogether: value.stats.guardiansTogether || 0,
            synergiesOnOneEnemy: value.stats.synergiesOnOneEnemy || 0
        },
        feats: rules.feats.map((feat) => {
            const got = value.feats[feat.id];
            return {
                id: feat.id, kind: feat.kind, tracked: feat.tracked, levels: feat.levels,
                level: got ? got.level : 0, at: got ? got.at : 0,
                progress: feat.tracked ? chronicleProgress(value.stats, feat.kind) : 0
            };
        }),
        records: value.records.slice().reverse()
    };
}

function chronicleRead(nk: nkruntime.Nakama, partyId: string): { value: ChronicleValue; version: string } {
    const objects = nk.storageRead([{ collection: ChronicleCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: chronicleEmpty(), version: "*" };
    const value = objects[0].value as ChronicleValue;
    return {
        value: { stats: Object.assign(chronicleNoStats(), value.stats || {}), feats: value.feats || {}, records: value.records || [] },
        version: objects[0].version
    };
}

function chronicleUpdate<T>(nk: nkruntime.Nakama, partyId: string, change: (value: ChronicleValue) => T): T {
    for (let attempt = 1; ; attempt++) {
        const stored = chronicleRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{
                collection: ChronicleCollection, key: partyId, userId: PartySystemUser, value: stored.value, version: stored.version,
                permissionRead: 0, permissionWrite: 0
            }]);
            return result;
        } catch (error) {
            if (attempt >= ChronicleWriteAttempts)
                throw error;
        }
    }
}

function chronicleNameOf(nk: nkruntime.Nakama, userId: string): string {
    const users = nk.usersGetId([userId]);
    return users.length > 0 ? users[0].displayName || users[0].username || "" : "";
}

function chronicleNote(nk: nkruntime.Nakama, partyId: string, kind: string, userId: string | null): void {
    const name = userId === null ? "" : chronicleNameOf(nk, userId);
    const now = Date.now();
    try {
        chronicleUpdate(nk, partyId, (value) => chronicleRecord(value, { at: now, kind: kind, name: name, feat: "", level: 0 }, ChronicleServerRules));
    } catch (error) {
        return;
    }
}

function chronicleForget(nk: nkruntime.Nakama, partyId: string): void {
    nk.storageDelete([{ collection: ChronicleCollection, key: partyId, userId: PartySystemUser }]);
}

function chronicleFlush(nk: nkruntime.Nakama, logger: nkruntime.Logger, partyId: string, delta: ChronicleStats, memberIds: string[]): ChronicleEarned[] {
    if (!partyId || !chronicleHasStats(delta))
        return [];
    const now = Date.now();
    let earned: ChronicleEarned[] = [];
    try {
        earned = chronicleUpdate(nk, partyId, (value) => chronicleAdd(value, delta, now, ChronicleServerRules));
    } catch (error) {
        logger.warn("party %s: chronicle was not saved: %s", partyId, String(error));
        return [];
    }
    if (earned.length > 0)
        chronicleKeepForMembers(nk, logger, partyId, earned, memberIds, now);
    return earned;
}

function chronicleKeepForMembers(nk: nkruntime.Nakama, logger: nkruntime.Logger, partyId: string, earned: ChronicleEarned[], memberIds: string[], now: number): void {
    for (const userId of memberIds) {
        try {
            const objects = nk.storageRead([{ collection: ChronicleFeatsCollection, key: partyId, userId: userId }]);
            const kept: { feats: { [id: string]: { level: number; at: number } } } = objects.length > 0 ? objects[0].value as any : { feats: {} };
            for (const got of earned)
                if (!kept.feats[got.feat] || kept.feats[got.feat].level < got.level)
                    kept.feats[got.feat] = { level: got.level, at: now };
            nk.storageWrite([{ collection: ChronicleFeatsCollection, key: partyId, userId: userId, value: kept, permissionRead: 1, permissionWrite: 0 }]);
        } catch (error) {
            logger.warn("party %s: feats were not copied to %s: %s", partyId, userId, String(error));
        }
    }
}

function chronicleRoleOf(heroClass: number): string {
    const roles = ChronicleServerRules.classRoles;
    return heroClass >= 0 && heroClass < roles.length ? roles[heroClass] : "";
}

function chronicleAtNight(unixMs: number, rules: ChronicleRules): boolean {
    const hour = Math.floor((unixMs + rules.night.utcOffsetMinutes * 60000) / 3600000) % 24;
    return unixMs > 0 && hour >= rules.night.fromHour && hour < rules.night.toHour;
}

function chronicleFullOrchestra(roles: string[]): boolean {
    const has = (role: string) => roles.indexOf(role) >= 0;
    return has("damage") && has("buffer") && has("debuffer") && (has("dot") || has("summoner"));
}

function partyChronicleRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const group = partyGroupOfUser(nk, partyCaller(ctx));
    if (!group)
        throw new Error("not in a party");
    return JSON.stringify(chronicleView(group.id, chronicleRead(nk, group.id).value, ChronicleServerRules));
}
