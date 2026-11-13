interface PartyColossusRules {
    mandatoryEvery: number;
    optionalMax: number;
    optionalTrophyBonus: number;
    hardshipSoften: number;
    logSize: number;
    classes: number;
    hardships: PartyColossusHardshipRule[];
    soften: PartyTrialTreeLine[];
    switchOff: PartyTrialTreeLine[];
    noHeal: PartyTrialTreeLine[];
    hpBase: number;
    hpGrowth: number;
    ceilingBase: number;
    ceilingStep: number;
    maxPointsPerSecond: number;
    maxOfflinePointsPerSecond: number;
    reportSeconds: number;
    experiencePerPercent: number;
}

interface PartyColossusHardshipRule {
    id: string;
    [key: string]: any;
}

interface PartyColossusMember {
    at: number;
    day: number;
    offlineSeconds: number;
}

interface PartyColossusKill {
    tier: number;
    at: number;
    hardships: string[];
    trophyBonus: number;
}

interface PartyColossusValue {
    tier: number;
    hp: number;
    waiting: boolean;
    kills: number;
    week: number;
    shares: { [userId: string]: number };
    members: { [userId: string]: PartyColossusMember };
    optional: string[];
    disabled: string;
    touched: boolean;
    shield: number;
    shieldAt: number;
    lastHit: number;
    furyAt: number;
    regenAt: number;
    hitters: { day: number; ids: string[] };
    log: PartyColossusKill[];
}

interface PartyColossusSituation {
    ceiling: number;
    activeSeconds: number;
    soften: boolean;
    switchOff: boolean;
    noHeal: boolean;
    leader: boolean;
}

const PartyColossusCollection = "party_colossus";
const PartyColossusAttempts = 4;
const PartyColossusHourMs = 3600000;
const PartyColossusKinds = 4;

function partyColossusRules(): PartyColossusRules {
    return PartyGrowthServerRules.colossus;
}

function partyColossusBaseHp(tier: number): number {
    return partyColossusRules().hpBase * Math.pow(partyColossusRules().hpGrowth, Math.max(1, tier) - 1);
}

function partyColossusHp(tier: number): number {
    return partyColossusBaseHp(tier);
}

function partyColossusCeiling(level: number): number {
    const unlocks = PartyGrowthServerRules.unlocks;
    if (!unlocks.some((unlock) => unlock.kind === "colossus" && unlock.level <= level))
        return 0;
    const steps = unlocks.filter((unlock) => unlock.kind === "colossusTier" && unlock.level <= level).length;
    return partyColossusRules().ceilingBase + partyColossusRules().ceilingStep * steps;
}

function partyColossusHardship(id: string): PartyColossusHardshipRule | undefined {
    return partyColossusRules().hardships.filter((hardship) => hardship.id === id)[0];
}

function partyColossusMandatory(tier: number): string {
    const rules = partyColossusRules();
    if (rules.hardships.length === 0 || tier % rules.mandatoryEvery !== 0)
        return "";
    return rules.hardships[(tier / rules.mandatoryEvery - 1) % rules.hardships.length].id;
}

function partyColossusActive(value: PartyColossusValue, situation: PartyColossusSituation): string[] {
    const mandatory = partyColossusMandatory(value.tier);
    if (mandatory)
        return situation.switchOff && value.disabled === mandatory ? [] : [mandatory];
    return value.optional.slice();
}

function partyColossusFactor(value: PartyColossusValue, situation: PartyColossusSituation, id: string, key: string, fallback: number): number {
    if (partyColossusActive(value, situation).indexOf(id) < 0)
        return fallback;
    const rule = partyColossusHardship(id);
    const number = rule && typeof rule[key] === "number" ? rule[key] as number : fallback;
    const softened = situation.soften && partyColossusMandatory(value.tier) === id;
    return softened ? 1 + (number - 1) * (1 - partyColossusRules().hardshipSoften) : number;
}

function partyColossusFull(value: PartyColossusValue, situation: PartyColossusSituation): number {
    return partyColossusBaseHp(value.tier) * partyColossusFactor(value, situation, "limitArmor", "hp", 1);
}

function partyColossusShieldFull(value: PartyColossusValue, situation: PartyColossusSituation): number {
    return partyColossusFull(value, situation) * partyColossusFactor(value, situation, "skillShield", "share", 0);
}

function partyColossusEmpty(): PartyColossusValue {
    return {
        tier: 1, hp: partyColossusBaseHp(1), waiting: false, kills: 0, week: -1, shares: {}, members: {},
        optional: [], disabled: "", touched: false, shield: 0, shieldAt: 0, lastHit: 0, furyAt: 0, regenAt: 0,
        hitters: { day: -1, ids: [] }, log: []
    };
}

function partyColossusRead(nk: nkruntime.Nakama, partyId: string): { value: PartyColossusValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyColossusCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: partyColossusEmpty(), version: "*" };
    const stored = objects[0].value as PartyColossusValue;
    const value = partyColossusEmpty();
    for (const key of Object.keys(value))
        if ((stored as any)[key] !== undefined)
            (value as any)[key] = (stored as any)[key];
    return { value: value, version: objects[0].version };
}

function partyColossusChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyColossusValue) => T): T {
    for (let attempt = 1; attempt <= PartyColossusAttempts; attempt++) {
        const stored = partyColossusRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyColossusCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyColossusNewTier(value: PartyColossusValue, situation: PartyColossusSituation, nowUnixMs: number): void {
    const mandatory = partyColossusMandatory(value.tier);
    value.optional = value.optional.filter((id) => id !== mandatory);
    value.touched = false;
    value.hp = partyColossusFull(value, situation);
    value.shield = partyColossusShieldFull(value, situation);
    value.shieldAt = nowUnixMs;
}

function partyColossusHeal(value: PartyColossusValue, situation: PartyColossusSituation, nowUnixMs: number): void {
    if (situation.noHeal || value.lastHit <= 0 || value.waiting)
        return;
    const full = partyColossusFull(value, situation);
    const fury = partyColossusActive(value, situation).indexOf("fury") >= 0 ? partyColossusHardship("fury") : undefined;
    if (fury) {
        const quietMs = fury.quietHours * PartyColossusHourMs;
        const from = Math.max(value.lastHit, value.furyAt);
        const periods = Math.floor((nowUnixMs - from) / quietMs);
        if (periods > 0) {
            value.hp = Math.min(full, value.hp + periods * partyColossusFactor(value, situation, "fury", "heal", 0) * full);
            value.furyAt = from + periods * quietMs;
        }
    }
    const regeneration = partyColossusActive(value, situation).indexOf("regeneration") >= 0 ? partyColossusHardship("regeneration") : undefined;
    if (regeneration) {
        const from = Math.max(value.lastHit + regeneration.quietHours * PartyColossusHourMs, value.regenAt);
        const hours = Math.floor((nowUnixMs - from) / PartyColossusHourMs);
        if (hours > 0) {
            value.hp = Math.min(full, value.hp + hours * partyColossusFactor(value, situation, "regeneration", "perHour", 0) * full);
            value.regenAt = from + hours * PartyColossusHourMs;
        }
    }
}

function partyColossusSettle(value: PartyColossusValue, situation: PartyColossusSituation, nowUnixMs: number): void {
    const week = partyTrialWeek(nowUnixMs);
    if (value.week !== week) {
        value.week = week;
        value.shares = {};
    }
    if (value.waiting && value.tier < situation.ceiling) {
        value.waiting = false;
        value.tier++;
        partyColossusNewTier(value, situation, nowUnixMs);
    }
    partyColossusHeal(value, situation, nowUnixMs);
    const shield = partyColossusActive(value, situation).indexOf("skillShield") >= 0 ? partyColossusHardship("skillShield") : undefined;
    if (shield && nowUnixMs >= value.shieldAt + shield.everyHours * PartyColossusHourMs) {
        value.shield = partyColossusShieldFull(value, situation);
        value.shieldAt = nowUnixMs;
    }
}

function partyColossusSituation(nk: nkruntime.Nakama, partyId: string, userId: string, nowUnixMs: number): PartyColossusSituation {
    const growth = partyGrowthRead(nk, partyId).value;
    const member = growth.members[userId];
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    const levels = partyTreasuryRead(nk, partyId).value.levels;
    const group = partyGroupOfUser(nk, userId);
    return {
        ceiling: partyColossusCeiling(partyGrowthLevel(growth.experience).level),
        activeSeconds: member && member.day === today ? member.minutes * 60 + member.carry : 0,
        soften: partyTrialTreeValue(levels, partyColossusRules().soften) !== 0,
        switchOff: partyTrialTreeValue(levels, partyColossusRules().switchOff) > 0,
        noHeal: partyTrialTreeValue(levels, partyColossusRules().noHeal) > 0,
        leader: !!group && partyMetaOf(group).leader === userId
    };
}

function partyColossusWeakClass(value: PartyColossusValue, nowUnixMs: number): number {
    const day = Math.floor(nowUnixMs / PartyGrowthDayMs);
    return (day + value.tier * 7) % partyColossusRules().classes;
}

function partyColossusAtNight(nowUnixMs: number): boolean {
    return chronicleAtNight(nowUnixMs, ChronicleServerRules);
}

function partyColossusView(nk: nkruntime.Nakama, partyId: string, value: PartyColossusValue, userId: string, situation: PartyColossusSituation, nowUnixMs: number, accepted: { [key: string]: any } | null): { [key: string]: any } {
    const ids = Object.keys(value.shares).sort((a, b) => value.shares[b] - value.shares[a]);
    const names = partyTreasuryNames(nk, ids);
    const total = ids.reduce((sum, id) => sum + value.shares[id], 0);
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    return {
        partyId: partyId, opened: situation.ceiling > 0, tier: value.tier, hp: Math.max(0, value.hp), hpFull: partyColossusFull(value, situation),
        ceiling: situation.ceiling, waiting: value.waiting, kills: value.kills,
        mandatory: partyColossusMandatory(value.tier), optional: value.optional, active: partyColossusActive(value, situation),
        optionalAllowed: !partyColossusMandatory(value.tier), touched: value.touched, disabled: value.disabled, canSwitchOff: situation.switchOff,
        softened: situation.soften, shield: value.shield, shieldFull: partyColossusShieldFull(value, situation),
        weakClass: partyColossusWeakClass(value, nowUnixMs), night: partyColossusAtNight(nowUnixMs),
        hittersToday: value.hitters.day === today ? value.hitters.ids.length : 0, leader: situation.leader,
        shares: ids.map((id) => ({ userId: id, name: names[id] || "", points: Math.floor(value.shares[id]), share: total > 0 ? value.shares[id] / total : 0 })),
        yours: Math.floor(value.shares[userId] || 0), reportSeconds: partyColossusRules().reportSeconds,
        accepted: accepted
    };
}

function partyColossusNumber(raw: unknown): number {
    if (raw === undefined)
        return 0;
    if (typeof raw !== "number" || !(raw >= 0) || raw > 1e12)
        throw new Error("bad request");
    return raw;
}

function partyColossusKinds(raw: unknown): number[] {
    if (raw === undefined)
        return [0, 0, 0, 0];
    if (!Array.isArray(raw) || raw.length !== PartyColossusKinds)
        throw new Error("bad request");
    return (raw as unknown[]).map(partyColossusNumber);
}

function partyColossusOffline(member: PartyColossusMember, points: number, seconds: number, activeSeconds: number, nowUnixMs: number): number {
    if (!(points > 0) || !(seconds > 0))
        return 0;
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    if (member.day !== today) {
        member.day = today;
        member.offlineSeconds = 0;
    }
    const allowed = Math.min(seconds, Math.max(0, activeSeconds - member.offlineSeconds));
    member.offlineSeconds += allowed;
    return Math.min(points * allowed / seconds, allowed * partyColossusRules().maxOfflinePointsPerSecond);
}

function partyColossusLiveScale(member: PartyColossusMember, raw: number, nowUnixMs: number): number {
    const rules = partyColossusRules();
    const elapsed = member.at > 0 ? Math.max(0, nowUnixMs - member.at) / 1000 : rules.reportSeconds;
    const cap = elapsed * rules.maxPointsPerSecond;
    return raw > cap ? cap / raw : 1;
}

function partyColossusNoteHitter(value: PartyColossusValue, userId: string, nowUnixMs: number): number {
    const today = Math.floor(nowUnixMs / PartyGrowthDayMs);
    if (value.hitters.day !== today)
        value.hitters = { day: today, ids: [] };
    if (value.hitters.ids.indexOf(userId) < 0)
        value.hitters.ids.push(userId);
    return value.hitters.ids.length;
}

function partyColossusShape(value: PartyColossusValue, situation: PartyColossusSituation, userId: string, heroClass: number, direct: number[], background: number[], offline: number, nowUnixMs: number): { points: number[]; offline: number } {
    const keys = partyColossusFactor(value, situation, "carapace", "keys", 1);
    const mouse = partyColossusFactor(value, situation, "shadow", "mouse", 1);
    const rift = partyColossusFactor(value, situation, "rift", "background", 1);
    let all = 1;
    if (partyColossusActive(value, situation).indexOf("night") >= 0)
        all *= partyColossusAtNight(nowUnixMs) ? partyColossusFactor(value, situation, "night", "night", 1) : partyColossusFactor(value, situation, "night", "day", 1);
    if (partyColossusActive(value, situation).indexOf("solitude") >= 0)
        all *= partyColossusNoteHitter(value, userId, nowUnixMs) > 1 ? partyColossusFactor(value, situation, "solitude", "together", 1) : partyColossusFactor(value, situation, "solitude", "alone", 1);
    else
        partyColossusNoteHitter(value, userId, nowUnixMs);
    if (partyColossusActive(value, situation).indexOf("mirrored") >= 0)
        all *= heroClass === partyColossusWeakClass(value, nowUnixMs) ? partyColossusFactor(value, situation, "mirrored", "weak", 1) : partyColossusFactor(value, situation, "mirrored", "other", 1);
    const kind = [keys, mouse, 1, 1];
    const points = [0, 1, 2, 3].map((i) => (direct[i] + background[i] * rift) * kind[i] * all);
    return { points: points, offline: offline * rift * all };
}

function partyColossusHit(value: PartyColossusValue, situation: PartyColossusSituation, points: number, nowUnixMs: number): { dealt: number; killed: number[] } {
    const killed: number[] = [];
    let left = points;
    let dealt = 0;
    while (left > 0 && !value.waiting) {
        const full = partyColossusFull(value, situation);
        const hit = Math.min(left, value.hp);
        value.hp -= hit;
        value.touched = true;
        left -= hit;
        dealt += hit / full;
        if (value.hp > 1e-9)
            break;
        killed.push(value.tier);
        value.kills++;
        const active = partyColossusActive(value, situation);
        value.log.push({ tier: value.tier, at: nowUnixMs, hardships: active, trophyBonus: partyColossusMandatory(value.tier) ? 0 : value.optional.length * partyColossusRules().optionalTrophyBonus });
        if (value.log.length > partyColossusRules().logSize)
            value.log.splice(0, value.log.length - partyColossusRules().logSize);
        if (value.tier >= situation.ceiling) {
            value.hp = 0;
            value.waiting = true;
            break;
        }
        value.tier++;
        partyColossusNewTier(value, situation, nowUnixMs);
    }
    return { dealt: dealt, killed: killed };
}

function partyColossusStrike(value: PartyColossusValue, situation: PartyColossusSituation, shaped: { points: number[]; offline: number }, nowUnixMs: number): { dealt: number; killed: number[]; total: number } {
    let skills = shaped.points[2];
    let others = shaped.points[0] + shaped.points[1] + shaped.points[3] + shaped.offline;
    if (value.shield > 0 && !value.waiting) {
        const absorbed = Math.min(value.shield, skills);
        value.shield -= absorbed;
        skills -= absorbed;
        others = 0;
        value.touched = value.touched || absorbed > 0;
    }
    const total = skills + others;
    const result = partyColossusHit(value, situation, total, nowUnixMs);
    return { dealt: result.dealt, killed: result.killed, total: total + (shaped.points[2] - skills) };
}

function partyColossusAct(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, act: (value: PartyColossusValue, situation: PartyColossusSituation) => { [key: string]: any } | null): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const situation = partyColossusSituation(nk, group.id, userId, nowUnixMs);
    if (situation.ceiling <= 0)
        throw new Error("the colossus is not open yet");
    return partyColossusChange(nk, group.id, (value) => {
        partyColossusSettle(value, situation, nowUnixMs);
        const accepted = act(value, situation);
        return partyColossusView(nk, group.id, value, userId, situation, nowUnixMs, accepted);
    });
}

function partyColossusReport(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const direct = partyColossusKinds(request.direct);
    const background = partyColossusKinds(request.background);
    const offlinePoints = partyColossusNumber(request.offlinePoints);
    const offlineSeconds = partyColossusNumber(request.offlineSeconds);
    const heroClass = request.heroClass === undefined ? -1 : request.heroClass;
    if (typeof heroClass !== "number" || heroClass !== Math.floor(heroClass) || heroClass < -1 || heroClass >= partyColossusRules().classes)
        throw new Error("bad request");
    let gained = { dealt: 0, killed: [] as number[], total: 0 };
    const view = partyColossusAct(nk, userId, nowUnixMs, (value, situation) => {
        const member = value.members[userId] || { at: 0, day: 0, offlineSeconds: 0 };
        value.members[userId] = member;
        const raw = direct.concat(background).reduce((sum, points) => sum + points, 0);
        const scale = partyColossusLiveScale(member, raw, nowUnixMs);
        const live = raw * scale;
        const offline = partyColossusOffline(member, offlinePoints, offlineSeconds, situation.activeSeconds, nowUnixMs);
        member.at = nowUnixMs;
        if (value.waiting)
            return { live: live, offline: offline, counted: 0, killed: [] };
        const shaped = partyColossusShape(value, situation, userId, heroClass, direct.map((p) => p * scale), background.map((p) => p * scale), offline, nowUnixMs);
        gained = partyColossusStrike(value, situation, shaped, nowUnixMs);
        if (gained.total > 0) {
            value.shares[userId] = (value.shares[userId] || 0) + gained.total;
            value.lastHit = nowUnixMs;
        }
        return { live: live, offline: offline, counted: gained.total, killed: gained.killed };
    });
    partyGrowthAdd(nk, view.partyId, gained.dealt * 100 * partyColossusRules().experiencePerPercent);
    for (const tier of gained.killed)
        partyColossusChronicle(nk, view.partyId, tier, nowUnixMs);
    return view;
}

function partyColossusRefit(value: PartyColossusValue, situation: PartyColossusSituation, change: () => void): void {
    const before = partyColossusFull(value, situation);
    const shieldBefore = partyColossusShieldFull(value, situation);
    change();
    const after = partyColossusFull(value, situation);
    if (!value.waiting)
        value.hp = before > 0 ? value.hp * after / before : after;
    const shieldAfter = partyColossusShieldFull(value, situation);
    value.shield = shieldBefore > 0 ? value.shield * shieldAfter / shieldBefore : value.touched ? 0 : shieldAfter;
}

function partyColossusChoose(nk: nkruntime.Nakama, userId: string, ids: unknown, nowUnixMs: number): { [key: string]: any } {
    if (!Array.isArray(ids) || ids.some((id) => typeof id !== "string" || !partyColossusHardship(id as string)))
        throw new Error("bad request");
    const unique = (ids as string[]).filter((id, index, all) => all.indexOf(id) === index);
    if (unique.length > partyColossusRules().optionalMax)
        throw new Error("at most " + partyColossusRules().optionalMax + " hardships");
    return partyColossusAct(nk, userId, nowUnixMs, (value, situation) => {
        if (!situation.leader)
            throw new Error("only the leader can do that");
        if (value.touched && !partyColossusMandatory(value.tier))
            throw new Error("the tier has already been hit");
        partyColossusRefit(value, situation, () => {
            value.optional = unique;
        });
        return null;
    });
}

function partyColossusSwitchOff(nk: nkruntime.Nakama, userId: string, id: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof id !== "string" || (id !== "" && !partyColossusHardship(id)))
        throw new Error("bad request");
    return partyColossusAct(nk, userId, nowUnixMs, (value, situation) => {
        if (!situation.leader)
            throw new Error("only the leader can do that");
        if (!situation.switchOff)
            throw new Error("the party tree does not allow that yet");
        partyColossusRefit(value, situation, () => {
            value.disabled = id;
        });
        return null;
    });
}

function partyColossusChronicle(nk: nkruntime.Nakama, partyId: string, tier: number, nowUnixMs: number): void {
    try {
        chronicleUpdate(nk, partyId, (value) => chronicleRecord(value, { at: nowUnixMs, kind: "colossus", name: "", feat: "", level: tier }, ChronicleServerRules));
    } catch (error) {
        return;
    }
}

function partyColossusMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const situation = partyColossusSituation(nk, group.id, userId, nowUnixMs);
    const value = partyColossusRead(nk, group.id).value;
    partyColossusSettle(value, situation, nowUnixMs);
    return partyColossusView(nk, group.id, value, userId, situation, nowUnixMs, null);
}

function partyColossusRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusMine(nk, partyCaller(ctx), Date.now()));
}

function partyColossusReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusReport(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyColossusChooseRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusChoose(nk, partyCaller(ctx), partyRequest(payload).ids, Date.now()));
}

function partyColossusSwitchOffRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyColossusSwitchOff(nk, partyCaller(ctx), partyRequest(payload).id, Date.now()));
}
