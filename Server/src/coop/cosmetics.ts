interface CosmeticItemRule {
    id: string;
    slot: string;
    price: number;
    source: string;
    clan: boolean;
}

interface CosmeticRules {
    items: CosmeticItemRule[];
    championTop10: string;
    championTop100: string;
    warVictors: string;
    warVictorsDays: number;
    leagueBanners: { [league: string]: string };
}

interface CosmeticValue {
    owned: string[];
    rewardedWeeks: number[];
}

interface ClanCosmeticValue {
    owned: string[];
    banner: string;
    emblem: string;
    victorUntil: number;
}

declare const CosmeticServerRules: CosmeticRules;

const CosmeticCollection = "cosmetics";
const CosmeticKey = "owned";
const ClanCosmeticCollection = "clan_cosmetics";
const CosmeticWriteAttempts = 4;
const CosmeticRewardedWeeksKept = 8;

function cosmeticRule(id: string): CosmeticItemRule | null {
    return CosmeticServerRules.items.filter((item) => item.id === id)[0] || null;
}

function cosmeticRead(nk: nkruntime.Nakama, userId: string): { value: CosmeticValue; version: string } {
    const objects = nk.storageRead([{ collection: CosmeticCollection, key: CosmeticKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { owned: [], rewardedWeeks: [] }, version: "*" };
    const value = objects[0].value as CosmeticValue;
    return { value: { owned: value.owned || [], rewardedWeeks: value.rewardedWeeks || [] }, version: objects[0].version };
}

function cosmeticChange<T>(nk: nkruntime.Nakama, userId: string, change: (value: CosmeticValue) => T): T {
    for (let attempt = 1; attempt <= CosmeticWriteAttempts; attempt++) {
        const stored = cosmeticRead(nk, userId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: CosmeticCollection, key: CosmeticKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === CosmeticWriteAttempts)
                throw new Error("cosmetics are busy, try again");
        }
    }
    throw new Error("cosmetics are busy, try again");
}

function cosmeticGive(nk: nkruntime.Nakama, userId: string, id: string): boolean {
    return cosmeticChange(nk, userId, (value) => {
        if (value.owned.indexOf(id) >= 0)
            return false;
        value.owned.push(id);
        return true;
    });
}

function cosmeticBuy(nk: nkruntime.Nakama, userId: string, id: string, nowUnixMs: number): { [key: string]: any } {
    const item = cosmeticRule(id);
    if (!item || item.source !== "shop" || !(item.price > 0))
        throw new Error("not for sale");
    if (item.clan)
        throw new Error("clan items are bought for the clan");
    if (cosmeticRead(nk, userId).value.owned.indexOf(id) >= 0)
        throw new Error("already owned");
    walletSpend(nk, userId, "crystals", item.price, "cosmetic", nowUnixMs);
    try {
        if (!cosmeticGive(nk, userId, id))
            throw new Error("already owned");
    } catch (error) {
        walletRefund(nk, userId, "crystals", item.price, "cosmetic", nowUnixMs);
        throw error;
    }
    return cosmeticMine(nk, userId, nowUnixMs);
}

function cosmeticWeeklyRank(nk: nkruntime.Nakama, userId: string, week: number, trial: string): number {
    try {
        const list = nk.leaderboardRecordsList(weeklyBoardId(week, trial), [userId], 0);
        const own = (list.ownerRecords || []).filter((record) => record.ownerId === userId)[0];
        return own ? own.rank : 0;
    } catch (error) {
        return 0;
    }
}

function cosmeticWeeklyRewards(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): string[] {
    const week = weeklyWeekOf(nowUnixMs) - 1;
    if (week < 0 || cosmeticRead(nk, userId).value.rewardedWeeks.indexOf(week) >= 0)
        return [];
    const marked = cosmeticChange(nk, userId, (value) => {
        if (value.rewardedWeeks.indexOf(week) >= 0)
            return false;
        value.rewardedWeeks.push(week);
        if (value.rewardedWeeks.length > CosmeticRewardedWeeksKept)
            value.rewardedWeeks.splice(0, value.rewardedWeeks.length - CosmeticRewardedWeeksKept);
        return true;
    });
    if (!marked)
        return [];
    const given: string[] = [];
    for (const trial of weeklyTrials(week)) {
        const rank = cosmeticWeeklyRank(nk, userId, week, trial);
        if (rank < 1 || rank > 100)
            continue;
        const top10 = rank <= 10;
        walletGrant(nk, userId, top10 ? "weekly_top10" : "weekly_top100", 1, nowUnixMs);
        const frame = top10 ? CosmeticServerRules.championTop10 : CosmeticServerRules.championTop100;
        if (cosmeticGive(nk, userId, frame))
            given.push(frame);
    }
    return given;
}

function cosmeticMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const fresh = cosmeticWeeklyRewards(nk, userId, nowUnixMs);
    return { owned: cosmeticRead(nk, userId).value.owned, crystals: walletRead(nk, userId).value.balances["crystals"] || 0, fresh: fresh };
}

function cosmeticsMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(cosmeticMine(nk, partyCaller(ctx), Date.now()));
}

function cosmeticBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.id !== "string")
        throw new Error("bad request");
    return JSON.stringify(cosmeticBuy(nk, partyCaller(ctx), request.id, Date.now()));
}

function clanCosmeticRead(nk: nkruntime.Nakama, clanId: string): { value: ClanCosmeticValue; version: string } {
    const objects = nk.storageRead([{ collection: ClanCosmeticCollection, key: clanId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { owned: [], banner: "", emblem: "", victorUntil: 0 }, version: "*" };
    return { value: objects[0].value as ClanCosmeticValue, version: objects[0].version };
}

function clanCosmeticChange<T>(nk: nkruntime.Nakama, clanId: string, change: (value: ClanCosmeticValue) => T): T {
    for (let attempt = 1; attempt <= CosmeticWriteAttempts; attempt++) {
        const stored = clanCosmeticRead(nk, clanId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: ClanCosmeticCollection, key: clanId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === CosmeticWriteAttempts)
                throw new Error("clan is busy, try again");
        }
    }
    throw new Error("clan is busy, try again");
}

function clanCosmeticGive(nk: nkruntime.Nakama, clanId: string, id: string): void {
    clanCosmeticChange(nk, clanId, (value) => {
        if (value.owned.indexOf(id) < 0)
            value.owned.push(id);
        return true;
    });
}

function clanCosmeticNoteWar(nk: nkruntime.Nakama, clanId: string, outcome: string, league: string, seasonEnds: boolean, nowUnixMs: number): void {
    clanCosmeticChange(nk, clanId, (value) => {
        if (outcome === "win")
            value.victorUntil = nowUnixMs + CosmeticServerRules.warVictorsDays * PartyDayMs;
        const banner = CosmeticServerRules.leagueBanners[league];
        if (seasonEnds && banner && value.owned.indexOf(banner) < 0)
            value.owned.push(banner);
        return true;
    });
}

function clanCosmeticView(value: ClanCosmeticValue, nowUnixMs: number): { [key: string]: any } {
    const victor = value.victorUntil > nowUnixMs;
    return { owned: value.owned, banner: value.banner, emblem: value.emblem, victor: victor, victorUntil: victor ? value.victorUntil : 0, victorBanner: CosmeticServerRules.warVictors };
}

function clanCosmeticBuy(nk: nkruntime.Nakama, userId: string, id: string, nowUnixMs: number): { [key: string]: any } {
    const group = clanOf(nk, userId, nowUnixMs);
    if (clanRoleOf(clanMetaOf(group), userId) !== ClanLeader)
        throw new Error("only the leader can do that");
    const item = cosmeticRule(id);
    if (!item || !item.clan || item.source !== "shop" || !(item.price > 0))
        throw new Error("not for sale");
    if (clanCosmeticRead(nk, group.id).value.owned.indexOf(id) >= 0)
        throw new Error("already owned");
    walletSpend(nk, userId, "crystals", item.price, "clan_cosmetic", nowUnixMs);
    try {
        clanCosmeticGive(nk, group.id, id);
    } catch (error) {
        walletRefund(nk, userId, "crystals", item.price, "clan_cosmetic", nowUnixMs);
        throw error;
    }
    return clanCosmeticView(clanCosmeticRead(nk, group.id).value, nowUnixMs);
}

function clanCosmeticSet(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const group = clanOf(nk, userId, nowUnixMs);
    if (clanRoleOf(clanMetaOf(group), userId) !== ClanLeader)
        throw new Error("only the leader can do that");
    const value = clanCosmeticChange(nk, group.id, (clan) => {
        for (const field of ["banner", "emblem"]) {
            const id = request[field];
            if (id === undefined)
                continue;
            if (typeof id !== "string")
                throw new Error("bad request");
            const item = id === "" ? null : cosmeticRule(id);
            if (id !== "" && (!item || item.slot !== (field === "banner" ? "clanBanner" : "clanEmblem") || clan.owned.indexOf(id) < 0))
                throw new Error("not owned");
            if (field === "banner")
                clan.banner = id;
            else
                clan.emblem = id;
        }
        return clan;
    });
    return clanCosmeticView(value, nowUnixMs);
}

function clanCosmeticsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const now = Date.now();
    const group = clanOf(nk, partyCaller(ctx), now);
    return JSON.stringify(clanCosmeticView(clanCosmeticRead(nk, group.id).value, now));
}

function clanCosmeticBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.id !== "string")
        throw new Error("bad request");
    return JSON.stringify(clanCosmeticBuy(nk, partyCaller(ctx), request.id, Date.now()));
}

function clanCosmeticSetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(clanCosmeticSet(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}
