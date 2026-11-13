interface HideoutStationRule {
    materials: string[];
    branches: string[];
    guild: string;
}

interface HideoutStation {
    level: number;
    branch: string;
}

interface HideoutModuleRule {
    building: string;
    materials: string[];
    counts: number[];
}

interface HideoutStationRules {
    workshop: { [id: string]: HideoutStationRule };
    modules: { [id: string]: HideoutModuleRule };
    moduleSockets: number[];
    street: string[];
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

const HideoutModuleStored = -1;

function hideoutModuleRule(id: unknown): HideoutModuleRule {
    const rule = typeof id === "string" ? hideoutStationRules().modules[id] : undefined;
    if (!rule)
        throw new Error("no such module");
    return rule;
}

function hideoutSockets(value: HideoutValue, building: string): number {
    const sockets = hideoutStationRules().moduleSockets;
    return sockets[Math.min(sockets.length, hideoutStationOf(value, building).level) - 1] || 0;
}

function hideoutModuleMake(nk: nkruntime.Nakama, userId: string, id: unknown, nowUnixMs: number): { [key: string]: any } {
    const rule = hideoutModuleRule(id);
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (hideout.modules[id as string] !== undefined)
            throw new Error("already made");
        hideoutPay(hideout, userId, id as string, rule.materials, rule.counts, nowUnixMs);
        hideout.modules[id as string] = HideoutModuleStored;
    }, nowUnixMs);
}

function hideoutModuleInstall(nk: nkruntime.Nakama, userId: string, id: unknown, socket: unknown, nowUnixMs: number): { [key: string]: any } {
    const rule = hideoutModuleRule(id);
    if (typeof socket !== "number" || socket !== Math.floor(socket) || socket < HideoutModuleStored)
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (hideout.modules[id as string] === undefined)
            throw new Error("not made yet");
        if (socket >= hideoutSockets(hideout, rule.building))
            throw new Error("no such socket");
        for (const other of Object.keys(hideout.modules))
            if (socket !== HideoutModuleStored && other !== id && hideout.modules[other] === socket && hideoutStationRules().modules[other].building === rule.building)
                hideout.modules[other] = HideoutModuleStored;
        hideout.modules[id as string] = socket;
    }, nowUnixMs);
}

function hideoutModuleMakeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutModuleMake(nk, partyCaller(ctx), partyRequest(payload).module, Date.now()));
}

function hideoutModuleInstallRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutModuleInstall(nk, partyCaller(ctx), request.module, request.socket, Date.now()));
}

function hideoutStreet(nk: nkruntime.Nakama, userId: string, order: unknown, nowUnixMs: number): { [key: string]: any } {
    const street = hideoutStationRules().street;
    if (!Array.isArray(order) || order.length !== street.length || street.some((id) => order.indexOf(id) < 0))
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        hideout.street = (order as string[]).slice();
    }, nowUnixMs);
}

function hideoutStreetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutStreet(nk, partyCaller(ctx), partyRequest(payload).order, Date.now()));
}
