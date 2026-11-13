interface TownWeekReward {
    decor: string;
    frame: string;
}

interface TownWeekRules {
    perPlayer: number;
    minGoal: number;
    minLabour: number;
    rewards: TownWeekReward[];
}

interface TownWeekValue {
    week: number;
    goal: number;
    total: number;
    done: boolean;
    decor: string[];
}

interface TownWeekMine {
    week: number;
    labour: number;
    framed: boolean;
}

declare const TownWeekServerRules: TownWeekRules;

const TownWeekKey = "week";
const TownWeekMineKey = "week_mine";

function townWeekRewardOf(week: number): TownWeekReward {
    const rewards = TownWeekServerRules.rewards;
    return rewards[((week % rewards.length) + rewards.length) % rewards.length];
}

function townWeekRead(nk: nkruntime.Nakama): { value: TownWeekValue; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownWeekKey, userId: PartySystemUser }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    return {
        value: {
            week: typeof stored.week === "number" ? stored.week : -1,
            goal: typeof stored.goal === "number" ? stored.goal : 0,
            total: typeof stored.total === "number" ? stored.total : 0,
            done: stored.done === true,
            decor: Array.isArray(stored.decor) ? stored.decor.filter((id: unknown) => typeof id === "string") : []
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function townWeekMineRead(nk: nkruntime.Nakama, userId: string, week: number): { value: TownWeekMine; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownWeekMineKey, userId: userId }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    const same = stored.week === week;
    return {
        value: { week: week, labour: same && typeof stored.labour === "number" ? stored.labour : 0, framed: same && stored.framed === true },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function townWeekCatchUp(value: TownWeekValue, week: number, players: number): boolean {
    if (value.week === week)
        return false;
    value.week = week;
    value.goal = Math.max(TownWeekServerRules.minGoal, Math.round(players * TownWeekServerRules.perPlayer));
    value.total = 0;
    value.done = false;
    return true;
}

function townWeekAdd(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, labour: number, days: { [day: string]: number }, nowUnixMs: number): void {
    const week = weeklyWeekOf(nowUnixMs);
    const players = townBuildPlayers(days, walletDayOf(nowUnixMs));
    let town: TownWeekValue | null = null;
    for (let attempt = 1; attempt <= TownWriteAttempts && town === null; attempt++) {
        const stored = townWeekRead(nk);
        const value = stored.value;
        let changed = townWeekCatchUp(value, week, players);
        if (labour > 0) {
            value.total += labour;
            changed = true;
        }
        if (!value.done && value.total >= value.goal) {
            value.done = true;
            const decor = townWeekRewardOf(week).decor;
            if (value.decor.indexOf(decor) < 0)
                value.decor.push(decor);
            logger.info("town: the rebuild of week %d is done with %d actions", week, value.total);
        }
        if (!changed) {
            town = value;
            break;
        }
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownWeekKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            town = value;
        } catch (error) {
            continue;
        }
    }
    if (town === null)
        throw new Error("the town is busy, try again");
    const own = townWeekMineRead(nk, userId, week);
    const mine = own.value;
    let mineChanged = labour > 0;
    mine.labour += labour;
    if (town.done && !mine.framed && mine.labour >= TownWeekServerRules.minLabour) {
        mine.framed = true;
        mineChanged = true;
        cosmeticGive(nk, userId, townWeekRewardOf(week).frame);
    }
    if (mineChanged)
        nk.storageWrite([{ collection: TownCollection, key: TownWeekMineKey, userId: userId, value: mine as any, version: own.version, permissionRead: 1, permissionWrite: 0 }]);
}

function townWeekView(nk: nkruntime.Nakama, userId: string, days: { [day: string]: number }, nowUnixMs: number): { [key: string]: any } {
    const week = weeklyWeekOf(nowUnixMs);
    const value = townWeekRead(nk).value;
    townWeekCatchUp(value, week, townBuildPlayers(days, walletDayOf(nowUnixMs)));
    const mine = townWeekMineRead(nk, userId, week).value;
    const reward = townWeekRewardOf(week);
    return {
        week: week,
        goal: value.goal,
        total: value.total,
        done: value.done,
        yours: mine.labour,
        minLabour: TownWeekServerRules.minLabour,
        framed: mine.framed,
        decor: reward.decor,
        frame: reward.frame,
        decorations: value.decor
    };
}
