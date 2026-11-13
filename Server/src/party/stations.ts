interface HideoutStationRule {
    materials: string[];
    branches: string[];
    guild: string;
}

interface HideoutStation {
    level: number;
    branch: string;
}

interface HideoutStationRules {
    workshop: { [id: string]: HideoutStationRule };
    stationMaxLevel: number;
    stationAmounts: number[][];
    stationBranchLevel: number;
    stationChangeAmounts: number[];
}

function hideoutStationRules(): HideoutStationRules {
    return HideoutServerRules as unknown as HideoutStationRules;
}

function hideoutStationOf(value: HideoutValue, id: string): HideoutStation {
    return value.stations[id] || { level: 1, branch: "" };
}

function hideoutStationRule(id: unknown): HideoutStationRule {
    const rule = typeof id === "string" ? hideoutStationRules().workshop[id] : undefined;
    if (!rule)
        throw new Error("no such station");
    return rule;
}

function hideoutPay(hideout: HideoutValue, userId: string, station: string, materials: string[], amounts: number[], nowUnixMs: number): void {
    if (!hideout.buildings["storage"])
        throw new Error("the storage is not built");
    for (let i = 0; i < materials.length; i++)
        if ((hideout.storage[materials[i]] || 0) < amounts[i])
            throw new Error("not enough in the storage");
    for (let i = 0; i < materials.length; i++) {
        hideout.storage[materials[i]] -= amounts[i];
        if (hideout.storage[materials[i]] === 0)
            delete hideout.storage[materials[i]];
        hideoutJournal(hideout, { at: nowUnixMs, userId: userId, item: materials[i], amount: -amounts[i], build: station });
    }
}

function hideoutStationUp(nk: nkruntime.Nakama, userId: string, id: unknown, nowUnixMs: number): { [key: string]: any } {
    const rule = hideoutStationRule(id);
    const rules = hideoutStationRules();
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (!hideoutGuildReady(nk, rule.guild))
            throw new Error("the guild hall is not built in the town");
        const station = hideoutStationOf(hideout, id as string);
        if (station.level >= rules.stationMaxLevel)
            throw new Error("already at the top level");
        hideoutPay(hideout, userId, id as string, rule.materials, rules.stationAmounts[station.level - 1], nowUnixMs);
        hideout.stations[id as string] = { level: station.level + 1, branch: station.branch };
    }, nowUnixMs);
}

function hideoutStationBranch(nk: nkruntime.Nakama, userId: string, id: unknown, branch: unknown, nowUnixMs: number): { [key: string]: any } {
    const rule = hideoutStationRule(id);
    if (typeof branch !== "string" || rule.branches.indexOf(branch) < 0)
        throw new Error("no such branch");
    const rules = hideoutStationRules();
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        const station = hideoutStationOf(hideout, id as string);
        if (station.level < rules.stationBranchLevel)
            throw new Error("the station level is too low");
        if (station.branch === branch)
            throw new Error("this branch is already chosen");
        if (station.branch.length > 0)
            hideoutPay(hideout, userId, id as string, rule.materials, rules.stationChangeAmounts, nowUnixMs);
        hideout.stations[id as string] = { level: station.level, branch: branch };
    }, nowUnixMs);
}

function hideoutStationUpRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutStationUp(nk, partyCaller(ctx), partyRequest(payload).station, Date.now()));
}

function hideoutStationBranchRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutStationBranch(nk, partyCaller(ctx), request.station, request.branch, Date.now()));
}
