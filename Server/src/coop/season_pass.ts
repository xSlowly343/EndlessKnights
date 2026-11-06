interface SeasonPassRewardRule {
    level: number;
    cosmetic: string;
    crystals: number;
}

interface SeasonPassSeasonRule {
    id: string;
    free: SeasonPassRewardRule[];
    premium: SeasonPassRewardRule[];
}

interface SeasonPassRules {
    seasonWeeks: number;
    firstWeek: number;
    levels: number;
    pointsPerLevel: number;
    dailyCap: number;
    premiumPrice: number;
    seasons: SeasonPassSeasonRule[];
}

interface SeasonPassValue {
    season: number;
    points: number;
    premium: boolean;
    freeGiven: number;
    premiumGiven: number;
    day: number;
    today: number;
}

interface SeasonPassGift {
    cosmetics: string[];
    crystals: number;
}

declare const SeasonPassServerRules: SeasonPassRules;

const SeasonPassCollection = "season_pass";
const SeasonPassKey = "pass";
const SeasonPassWriteAttempts = 4;

function seasonPassWeekStart(week: number): number {
    return (week * 7 - 3) * PartyDayMs - WalletServerRules.dayUtcOffsetMinutes * WalletMinuteMs;
}

function seasonPassSeasonOf(nowUnixMs: number): number {
    const week = weeklyWeekOf(nowUnixMs);
    const rules = SeasonPassServerRules;
    if (week < rules.firstWeek)
        return -1;
    const season = Math.floor((week - rules.firstWeek) / rules.seasonWeeks);
    return season < rules.seasons.length ? season : -1;
}

function seasonPassEndsAt(season: number): number {
    const rules = SeasonPassServerRules;
    return season < 0 ? 0 : seasonPassWeekStart(rules.firstWeek + (season + 1) * rules.seasonWeeks);
}

function seasonPassEmpty(season: number): SeasonPassValue {
    return { season: season, points: 0, premium: false, freeGiven: 0, premiumGiven: 0, day: 0, today: 0 };
}

function seasonPassRead(nk: nkruntime.Nakama, userId: string, season: number): { value: SeasonPassValue; version: string } {
    const objects = nk.storageRead([{ collection: SeasonPassCollection, key: SeasonPassKey, userId: userId }]);
    if (objects.length === 0)
        return { value: seasonPassEmpty(season), version: "*" };
    const value = objects[0].value as SeasonPassValue;
    return { value: value.season === season ? value : seasonPassEmpty(season), version: objects[0].version };
}

function seasonPassChange<T>(nk: nkruntime.Nakama, userId: string, season: number, change: (value: SeasonPassValue) => T): T {
    for (let attempt = 1; attempt <= SeasonPassWriteAttempts; attempt++) {
        const stored = seasonPassRead(nk, userId, season);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: SeasonPassCollection, key: SeasonPassKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === SeasonPassWriteAttempts)
                throw new Error("season pass is busy, try again");
        }
    }
    throw new Error("season pass is busy, try again");
}

function seasonPassLevel(points: number): number {
    const rules = SeasonPassServerRules;
    return Math.min(rules.levels, Math.floor(points / rules.pointsPerLevel));
}

function seasonPassDue(value: SeasonPassValue): SeasonPassRewardRule[] {
    const season = SeasonPassServerRules.seasons[value.season];
    const level = seasonPassLevel(value.points);
    const due = season.free.filter((reward) => reward.level > value.freeGiven && reward.level <= level);
    if (value.premium)
        due.push(...season.premium.filter((reward) => reward.level > value.premiumGiven && reward.level <= level));
    value.freeGiven = Math.max(value.freeGiven, level);
    if (value.premium)
        value.premiumGiven = Math.max(value.premiumGiven, level);
    return due;
}

function seasonPassGive(nk: nkruntime.Nakama, userId: string, rewards: SeasonPassRewardRule[], nowUnixMs: number): SeasonPassGift {
    const gift: SeasonPassGift = { cosmetics: [], crystals: 0 };
    for (const reward of rewards) {
        if (reward.cosmetic) {
            if (cosmeticGive(nk, userId, reward.cosmetic))
                gift.cosmetics.push(reward.cosmetic);
        } else {
            gift.crystals += walletGrant(nk, userId, "season_pass", reward.crystals, nowUnixMs);
        }
    }
    return gift;
}

function seasonPassView(nk: nkruntime.Nakama, userId: string, value: SeasonPassValue, nowUnixMs: number, accepted: number, gift: SeasonPassGift): { [key: string]: any } {
    const rules = SeasonPassServerRules;
    const today = value.day === walletDayOf(nowUnixMs) ? value.today : 0;
    return {
        season: value.season,
        id: value.season < 0 ? "" : rules.seasons[value.season].id,
        points: value.points,
        level: seasonPassLevel(value.points),
        premium: value.premium,
        today: today,
        dailyCap: rules.dailyCap,
        endsAt: seasonPassEndsAt(value.season),
        accepted: accepted,
        fresh: gift.cosmetics,
        freshCrystals: gift.crystals,
        crystals: walletRead(nk, userId).value.balances["crystals"] || 0
    };
}

function seasonPassAdd(nk: nkruntime.Nakama, userId: string, points: number, nowUnixMs: number): { [key: string]: any } {
    if (typeof points !== "number" || !(points >= 0) || points !== Math.floor(points))
        throw new Error("bad request");
    const season = seasonPassSeasonOf(nowUnixMs);
    if (season < 0)
        return seasonPassView(nk, userId, seasonPassEmpty(-1), nowUnixMs, 0, { cosmetics: [], crystals: 0 });
    const rules = SeasonPassServerRules;
    const result = seasonPassChange(nk, userId, season, (value) => {
        const day = walletDayOf(nowUnixMs);
        if (value.day !== day) {
            value.day = day;
            value.today = 0;
        }
        const accepted = Math.max(0, Math.min(points, rules.dailyCap - value.today, rules.levels * rules.pointsPerLevel - value.points));
        value.today += accepted;
        value.points += accepted;
        return { accepted: accepted, due: seasonPassDue(value), value: value };
    });
    const gift = seasonPassGive(nk, userId, result.due, nowUnixMs);
    return seasonPassView(nk, userId, result.value, nowUnixMs, result.accepted, gift);
}

function seasonPassBonus(nk: nkruntime.Nakama, userId: string, points: number, nowUnixMs: number): number {
    const season = seasonPassSeasonOf(nowUnixMs);
    if (season < 0 || !(points > 0))
        return 0;
    const rules = SeasonPassServerRules;
    const result = seasonPassChange(nk, userId, season, (value) => {
        const accepted = Math.max(0, Math.min(Math.floor(points), rules.levels * rules.pointsPerLevel - value.points));
        value.points += accepted;
        return { accepted: accepted, due: seasonPassDue(value) };
    });
    seasonPassGive(nk, userId, result.due, nowUnixMs);
    return result.accepted;
}

function seasonPassPremium(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const season = seasonPassSeasonOf(nowUnixMs);
    if (season < 0)
        throw new Error("no season");
    if (seasonPassRead(nk, userId, season).value.premium)
        throw new Error("already owned");
    const price = SeasonPassServerRules.premiumPrice;
    walletSpend(nk, userId, "crystals", price, "season_pass", nowUnixMs);
    let result: { due: SeasonPassRewardRule[]; value: SeasonPassValue };
    try {
        result = seasonPassChange(nk, userId, season, (value) => {
            if (value.premium)
                throw new Error("already owned");
            value.premium = true;
            return { due: seasonPassDue(value), value: value };
        });
    } catch (error) {
        walletRefund(nk, userId, "crystals", price, "season_pass", nowUnixMs);
        throw error;
    }
    const gift = seasonPassGive(nk, userId, result.due, nowUnixMs);
    return seasonPassView(nk, userId, result.value, nowUnixMs, 0, gift);
}

function seasonPassMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(seasonPassAdd(nk, partyCaller(ctx), 0, Date.now()));
}

function seasonPassAddRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(seasonPassAdd(nk, partyCaller(ctx), partyRequest(payload).points, Date.now()));
}

function seasonPassPremiumRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(seasonPassPremium(nk, partyCaller(ctx), Date.now()));
}
