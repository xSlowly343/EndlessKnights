interface TownBuildRules {
    startBuilt: string[];
    order: string[];
    actionsPerPlayerDay: number;
    buildDays: number;
    minTarget: number;
    dailyCap: number;
    keptDays: number;
}

interface TownBuildValue {
    built: string[];
    project: number;
    progress: number;
    target: number;
    days: { [day: string]: number };
}

interface TownBuildSent {
    day: number;
    sent: number;
}

declare const TownBuildServerRules: TownBuildRules;

const TownBuildSentKey = "build";

function townBuildRead(nk: nkruntime.Nakama): { value: TownBuildValue; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { built: [], project: 0, progress: 0, target: 0, days: {} }, version: "*" };
    const value = objects[0].value as TownBuildValue;
    return {
        value: { built: value.built || [], project: value.project || 0, progress: value.progress || 0, target: value.target || 0, days: value.days || {} },
        version: objects[0].version
    };
}

function townBuildTarget(days: { [day: string]: number }, today: number): number {
    const rules = TownBuildServerRules;
    let total = 0;
    let counted = 0;
    for (let day = today - rules.buildDays; day < today; day++) {
        if (days[String(day)] !== undefined) {
            total += days[String(day)];
            counted++;
        }
    }
    const daily = counted > 0 ? total / counted : Math.max(1, days[String(today)] || 0);
    return Math.max(rules.minTarget, Math.round(daily * rules.actionsPerPlayerDay * rules.buildDays));
}

function townBuildView(value: TownBuildValue, sent: number, accepted: number): { [key: string]: any } {
    const rules = TownBuildServerRules;
    return {
        startBuilt: rules.startBuilt, built: value.built, project: value.project < rules.order.length ? rules.order[value.project] : "",
        progress: value.progress, target: value.target, accepted: accepted, sentToday: sent, dailyCap: rules.dailyCap
    };
}

function townBuildTakeSent(nk: nkruntime.Nakama, userId: string, actions: number, day: number): { accepted: number; sent: number; first: boolean } {
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: TownCollection, key: TownBuildSentKey, userId: userId }]);
        const stored = objects.length > 0 ? objects[0].value as TownBuildSent : { day: day, sent: 0 };
        const sent = stored.day === day ? stored.sent : 0;
        const accepted = Math.max(0, Math.min(actions, TownBuildServerRules.dailyCap - sent));
        if (accepted === 0)
            return { accepted: 0, sent: sent, first: false };
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownBuildSentKey, userId: userId, value: { day: day, sent: sent + accepted } as any, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 1, permissionWrite: 0 }]);
            return { accepted: accepted, sent: sent + accepted, first: sent === 0 };
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildAdd(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, actions: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof actions !== "number" || actions !== Math.floor(actions) || actions < 1 || actions > 1000000)
        throw new Error("bad request");
    const rules = TownBuildServerRules;
    const day = walletDayOf(nowUnixMs);
    const taken = townBuildTakeSent(nk, userId, actions, day);
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townBuildRead(nk);
        const value = stored.value;
        if (taken.first)
            value.days[String(day)] = (value.days[String(day)] || 0) + 1;
        for (const key of Object.keys(value.days))
            if (Number(key) < day - rules.keptDays)
                delete value.days[key];
        if (value.project < rules.order.length && taken.accepted > 0) {
            if (value.target <= 0)
                value.target = townBuildTarget(value.days, day);
            value.progress += taken.accepted;
            while (value.project < rules.order.length && value.progress >= value.target) {
                value.built.push(rules.order[value.project]);
                logger.info("town: guild hall %s is built", rules.order[value.project]);
                value.progress -= value.target;
                value.project++;
                value.target = value.project < rules.order.length ? townBuildTarget(value.days, day) : 0;
            }
            if (value.project >= rules.order.length)
                value.progress = 0;
        }
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser, value: value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return townBuildView(value, taken.sent, taken.accepted);
        } catch (error) {
            continue;
        }
    }
    throw new Error("the town is busy, try again");
}

function townBuildRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const objects = nk.storageRead([{ collection: TownCollection, key: TownBuildSentKey, userId: userId }]);
    const stored = objects.length > 0 ? objects[0].value as TownBuildSent : null;
    const sent = stored && stored.day === walletDayOf(Date.now()) ? stored.sent : 0;
    return JSON.stringify(townBuildView(townBuildRead(nk).value, sent, 0));
}

function townBuildAddRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townBuildAdd(nk, logger, partyCaller(ctx), partyRequest(payload).actions, Date.now()));
}
