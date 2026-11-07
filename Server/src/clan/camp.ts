interface ClanCampRules {
    maxGive: number;
    dailyPointsPerMember: number;
    materials: { [item: string]: number };
    levels: number[];
    feastFromLevel: number;
    feastDishesPerMember: number;
    feastMinDishes: number;
    feastMaxDishes: number;
    feastPerMemberWeekly: number;
    dishes: string[];
}

interface ClanCampValue {
    points: number;
    given: { [userId: string]: number };
    day: number;
    today: { [userId: string]: number };
    feast: { week: number; dishes: number; by: { [userId: string]: number }; laidAt: number };
    log: { at: number; userId: string; kind: string; amount: number; item: string }[];
}

declare const ClanCampServerRules: ClanCampRules;

const ClanCampCollection = "clan_camp";
const ClanCampWriteAttempts = 4;
const ClanCampLogSize = 20;

function clanCampEmpty(week: number, day: number): ClanCampValue {
    return { points: 0, given: {}, day: day, today: {}, feast: { week: week, dishes: 0, by: {}, laidAt: 0 }, log: [] };
}

function clanCampLevel(points: number): number {
    let level = 0;
    while (level < ClanCampServerRules.levels.length && points >= ClanCampServerRules.levels[level])
        level++;
    return level;
}

function clanCampFeastTarget(members: number): number {
    const rules = ClanCampServerRules;
    return Math.max(rules.feastMinDishes, Math.min(rules.feastMaxDishes, members * rules.feastDishesPerMember));
}

function clanCampRead(nk: nkruntime.Nakama, clanId: string, nowUnixMs: number): { value: ClanCampValue; version: string } {
    const week = raidWeekOf(nowUnixMs);
    const objects = nk.storageRead([{ collection: ClanCampCollection, key: clanId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: clanCampEmpty(week, walletDayOf(nowUnixMs)), version: "*" };
    const value = objects[0].value as ClanCampValue;
    if (!value.feast || value.feast.week !== week)
        value.feast = { week: week, dishes: 0, by: {}, laidAt: 0 };
    if (value.day !== walletDayOf(nowUnixMs)) {
        value.day = walletDayOf(nowUnixMs);
        value.today = {};
    }
    return { value: value, version: objects[0].version };
}

function clanCampChange<T>(nk: nkruntime.Nakama, clanId: string, nowUnixMs: number, change: (value: ClanCampValue) => T): T {
    for (let attempt = 1; attempt <= ClanCampWriteAttempts; attempt++) {
        const stored = clanCampRead(nk, clanId, nowUnixMs);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: ClanCampCollection, key: clanId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("clan is busy, try again");
}

function clanCampLog(value: ClanCampValue, entry: { at: number; userId: string; kind: string; amount: number; item: string }): void {
    value.log.unshift(entry);
    if (value.log.length > ClanCampLogSize)
        value.log.length = ClanCampLogSize;
}

function clanCampView(nk: nkruntime.Nakama, group: nkruntime.Group, value: ClanCampValue, userId: string): { [key: string]: any } {
    const members = clanMetaOf(group).members.length;
    const level = clanCampLevel(value.points);
    const target = clanCampFeastTarget(members);
    const laid = value.feast.laidAt > 0;
    const ids = value.log.map((entry) => entry.userId).filter((id, i, all) => id && all.indexOf(id) === i);
    const names: { [userId: string]: string } = {};
    for (const user of ids.length > 0 ? nk.usersGetId(ids) : [])
        names[user.userId] = user.displayName || user.username;
    return {
        points: value.points, level: level, next: level < ClanCampServerRules.levels.length ? ClanCampServerRules.levels[level] : 0,
        yours: value.given[userId] || 0, today: value.today[userId] || 0, dailyCap: ClanCampServerRules.dailyPointsPerMember,
        feast: {
            week: value.feast.week, dishes: value.feast.dishes, target: target, laid: laid, yours: value.feast.by[userId] || 0,
            open: level >= ClanCampServerRules.feastFromLevel, until: laid ? raidWeekStart(value.feast.week + 1) : 0
        },
        log: value.log.map((entry) => ({ at: entry.at, name: names[entry.userId] || "", kind: entry.kind, amount: entry.amount, item: entry.item }))
    };
}

function clanCampGive(nk: nkruntime.Nakama, userId: string, item: unknown, amount: unknown, nowUnixMs: number): { [key: string]: any } {
    const points = typeof item === "string" ? ClanCampServerRules.materials[item] || 0 : 0;
    if (points <= 0)
        throw new Error("not a camp material");
    if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < 1 || amount > ClanCampServerRules.maxGive)
        throw new Error("bad amount");
    const group = clanOf(nk, userId, nowUnixMs);
    const value = clanCampChange(nk, group.id, nowUnixMs, (camp) => {
        const gain = points * amount;
        if ((camp.today[userId] || 0) + gain > ClanCampServerRules.dailyPointsPerMember)
            throw new Error("daily limit reached");
        camp.points += gain;
        camp.given[userId] = (camp.given[userId] || 0) + gain;
        camp.today[userId] = (camp.today[userId] || 0) + gain;
        clanCampLog(camp, { at: nowUnixMs, userId: userId, kind: "gave", amount: amount, item: item as string });
        return camp;
    });
    return clanCampView(nk, group, value, userId);
}

function clanFeastBring(nk: nkruntime.Nakama, userId: string, dish: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof dish !== "string" || ClanCampServerRules.dishes.indexOf(dish) < 0)
        throw new Error("not a dish");
    const group = clanOf(nk, userId, nowUnixMs);
    const target = clanCampFeastTarget(clanMetaOf(group).members.length);
    const value = clanCampChange(nk, group.id, nowUnixMs, (camp) => {
        if (clanCampLevel(camp.points) < ClanCampServerRules.feastFromLevel)
            throw new Error("the feast table is not built");
        if (camp.feast.laidAt > 0)
            throw new Error("the feast is already laid");
        if ((camp.feast.by[userId] || 0) >= ClanCampServerRules.feastPerMemberWeekly)
            throw new Error("weekly limit reached");
        camp.feast.dishes += 1;
        camp.feast.by[userId] = (camp.feast.by[userId] || 0) + 1;
        clanCampLog(camp, { at: nowUnixMs, userId: userId, kind: "dish", amount: 1, item: dish });
        if (camp.feast.dishes >= target) {
            camp.feast.laidAt = nowUnixMs;
            clanCampLog(camp, { at: nowUnixMs, userId: "", kind: "feast", amount: camp.feast.dishes, item: "" });
        }
        return camp;
    });
    return clanCampView(nk, group, value, userId);
}

function clanCampRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    return JSON.stringify(clanCampView(nk, group, clanCampRead(nk, group.id, now).value, userId));
}

function clanCampGiveRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(clanCampGive(nk, partyCaller(ctx), request.item, request.amount, Date.now()));
}

function clanFeastBringRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(clanFeastBring(nk, partyCaller(ctx), partyRequest(payload).dish, Date.now()));
}
