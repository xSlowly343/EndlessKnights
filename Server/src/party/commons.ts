interface HideoutPlantRule {
    id: string;
    input: string;
    starter: boolean;
    growSeconds: number;
    secondsPerUnit: number;
    herb: string;
    yield: number;
}

interface HideoutCommonsRules {
    fireSignMs: number;
    togetherBoost: number;
    bedsPerRow: number;
    gardenRows: string[];
    tendPerMark: { [input: string]: number };
    plants: HideoutPlantRule[];
    storagePrefixes: string[];
    storageItems: string[];
    storageNever: string[];
    takePerDay: number;
    journalSize: number;
    stations: { [station: string]: string };
    brewsPerDay: number;
    recipes: string[];
}

interface HideoutBed {
    plant: string;
    grown: number;
    ready: boolean;
    by: { [userId: string]: number };
}

interface HideoutJournalEntry {
    at: number;
    userId: string;
    item: string;
    amount: number;
}

interface HideoutTend {
    keys: number;
    clicks: number;
    mouse: number;
    idle: number;
}

const HideoutTendInputs: { [input: string]: string } = { keys: "keys", clicks: "clicks", mouse: "mouseMeters", idle: "idleSeconds" };

function hideoutCommons(): HideoutCommonsRules {
    return HideoutServerRules as any as HideoutCommonsRules;
}

function hideoutPlantRule(id: string): HideoutPlantRule | null {
    return hideoutCommons().plants.filter((plant) => plant.id === id)[0] || null;
}

function hideoutBedCount(value: HideoutValue): number {
    let rows = 0;
    for (const row of hideoutCommons().gardenRows)
        if ((value.buildings[row] || 0) > 0)
            rows++;
    return rows * hideoutCommons().bedsPerRow;
}

function hideoutEnsureBeds(value: HideoutValue): void {
    const count = hideoutBedCount(value);
    while (value.garden.length < count)
        value.garden.push({ plant: "", grown: 0, ready: false, by: {} });
}

function hideoutGrowBed(bed: HideoutBed, seconds: number): void {
    const rule = hideoutPlantRule(bed.plant);
    if (!rule || bed.ready || !(seconds > 0))
        return;
    bed.grown += seconds;
    if (bed.grown >= rule.growSeconds)
        bed.ready = true;
}

function hideoutGrowGarden(value: HideoutValue, together: boolean, nowUnixMs: number): void {
    hideoutEnsureBeds(value);
    const seconds = value.gardenAt > 0 ? Math.max(0, nowUnixMs - value.gardenAt) / 1000 : 0;
    value.gardenAt = nowUnixMs;
    const boost = together ? 1 + hideoutCommons().togetherBoost : 1;
    for (const bed of value.garden)
        if (bed.plant)
            hideoutGrowBed(bed, seconds * boost);
}

function hideoutReadTend(raw: unknown): HideoutTend | null {
    if (raw === undefined || raw === null)
        return null;
    if (typeof raw !== "object" || Array.isArray(raw))
        throw new Error("bad request");
    const source = raw as { [key: string]: unknown };
    const tend: HideoutTend = { keys: 0, clicks: 0, mouse: 0, idle: 0 };
    for (const input of Object.keys(HideoutTendInputs)) {
        const amount = source[HideoutTendInputs[input]];
        if (amount === undefined)
            continue;
        if (typeof amount !== "number" || !(amount >= 0))
            throw new Error("bad request");
        (tend as any)[input] = Math.min(amount, hideoutCommons().tendPerMark[HideoutTendInputs[input]] || 0);
    }
    return tend;
}

function hideoutTendGarden(value: HideoutValue, userId: string, tend: HideoutTend, together: boolean): void {
    hideoutEnsureBeds(value);
    const boost = together ? 1 + hideoutCommons().togetherBoost : 1;
    for (const bed of value.garden) {
        const rule = hideoutPlantRule(bed.plant);
        if (!rule || bed.ready)
            continue;
        const units = (tend as any)[rule.input] || 0;
        if (units <= 0)
            continue;
        hideoutGrowBed(bed, units * rule.secondsPerUnit * boost);
        bed.by[userId] = (bed.by[userId] || 0) + 1;
    }
}

function hideoutFireSigns(value: HideoutValue, here: string[], stepSeconds: number, signs: { [userId: string]: number }): void {
    const every = hideoutCommons().fireSignMs / 1000;
    if (!(stepSeconds > 0) || !(every > 0))
        return;
    for (const id of here) {
        let sat = (value.fireBy[id] || 0) + stepSeconds;
        while (sat >= every) {
            sat -= every;
            signs[id] = (signs[id] || 0) + 1;
        }
        value.fireBy[id] = sat;
    }
}

function hideoutOwe(value: HideoutValue, userId: string, item: string, amount: number): void {
    const owed = value.owed[userId] || {};
    owed[item] = (owed[item] || 0) + amount;
    value.owed[userId] = owed;
}

function hideoutWaiting(value: HideoutValue, userId: string): { [item: string]: number } {
    const waiting: { [item: string]: number } = {};
    const claim = value.claims[userId];
    for (const source of [claim ? claim.items : {}, value.owed[userId] || {}])
        for (const item of Object.keys(source))
            waiting[item] = (waiting[item] || 0) + source[item];
    return waiting;
}

function hideoutStorable(item: unknown): boolean {
    const rules = hideoutCommons();
    if (typeof item !== "string" || item.length === 0 || item.length > 64 || rules.storageNever.indexOf(item) >= 0)
        return false;
    return rules.storageItems.indexOf(item) >= 0 || rules.storagePrefixes.some((prefix) => item.indexOf(prefix) === 0);
}

function hideoutJournal(value: HideoutValue, entry: HideoutJournalEntry): void {
    value.journal.push(entry);
    const size = hideoutCommons().journalSize;
    if (value.journal.length > size)
        value.journal = value.journal.slice(value.journal.length - size);
}

function hideoutTakenToday(value: HideoutValue, userId: string, nowUnixMs: number): number {
    const taken = value.taken[userId];
    return taken && taken.day === walletDayOf(nowUnixMs) ? taken.count : 0;
}

function hideoutCommonsView(nk: nkruntime.Nakama, value: HideoutValue, members: string[], userId: string, nowUnixMs: number): { [key: string]: any } {
    const writers = value.journal.map((entry) => entry.userId).filter((id, index, all) => all.indexOf(id) === index);
    const users = writers.length > 0 ? nk.usersGetId(writers) : [];
    const nameOf = (id: string) => {
        const user = users.filter((entry) => entry.userId === id)[0];
        return user ? user.displayName || user.username : "";
    };
    return {
        together: hideoutHere(value, members, nowUnixMs).length >= 2,
        garden: value.garden.map((bed) => {
            const rule = hideoutPlantRule(bed.plant);
            return { plant: bed.plant, share: rule ? Math.min(1, bed.grown / rule.growSeconds) : 0, ready: bed.ready, tended: bed.by[userId] !== undefined };
        }),
        owed: hideoutWaiting(value, userId),
        storage: value.buildings["storage"] ? value.storage : null,
        journal: value.journal.slice().reverse().map((entry) => ({ at: entry.at, name: nameOf(entry.userId), item: entry.item, amount: entry.amount })),
        takenToday: hideoutTakenToday(value, userId, nowUnixMs), takePerDay: hideoutCommons().takePerDay
    };
}

function hideoutPlant(nk: nkruntime.Nakama, userId: string, bed: unknown, plant: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof bed !== "number" || bed !== Math.floor(bed) || bed < 0 || typeof plant !== "string" || !hideoutPlantRule(plant))
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout) => {
        hideoutEnsureBeds(hideout);
        if (bed >= hideout.garden.length)
            throw new Error("no such bed");
        if (hideout.garden[bed].plant)
            throw new Error("the bed is taken");
        hideout.garden[bed] = { plant: plant, grown: 0, ready: false, by: {} };
        hideout.garden[bed].by[userId] = 0;
    }, nowUnixMs);
}

function hideoutHarvest(nk: nkruntime.Nakama, userId: string, bed: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof bed !== "number" || bed !== Math.floor(bed) || bed < 0)
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        hideoutEnsureBeds(hideout);
        const target = hideout.garden[bed];
        if (!target || !target.plant || !target.ready)
            throw new Error("not ready");
        const rule = hideoutPlantRule(target.plant);
        if (rule) {
            target.by[userId] = target.by[userId] || 0;
            for (const id of Object.keys(target.by))
                if (meta.members.indexOf(id) >= 0)
                    hideoutOwe(hideout, id, rule.herb, rule.yield);
        }
        hideout.counters["gardenHarvests"] = (hideout.counters["gardenHarvests"] || 0) + 1;
        hideout.garden[bed] = { plant: "", grown: 0, ready: false, by: {} };
    }, nowUnixMs);
}

function hideoutClaim(nk: nkruntime.Nakama, userId: string, ack: unknown, nowUnixMs: number): { [key: string]: any } {
    let items: { [item: string]: number } = {};
    let seq = 0;
    const view = hideoutUpdate(nk, userId, (hideout) => {
        let claim = hideout.claims[userId] || { seq: 0, items: {} };
        if (typeof ack === "number" && ack === claim.seq)
            claim = { seq: claim.seq, items: {} };
        if (Object.keys(claim.items).length === 0 && hideout.owed[userId]) {
            claim = { seq: Math.max(nowUnixMs, claim.seq + 1), items: hideout.owed[userId] };
            delete hideout.owed[userId];
        }
        hideout.claims[userId] = claim;
        items = claim.items;
        seq = claim.seq;
    }, nowUnixMs);
    return { seq: seq, items: items, hideout: view };
}

function hideoutStore(nk: nkruntime.Nakama, userId: string, item: unknown, amount: unknown, put: boolean, nowUnixMs: number): { [key: string]: any } {
    if (!hideoutStorable(item))
        throw new Error("this does not go into the storage");
    if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < 1 || amount > HideoutServerRules.maxGive)
        throw new Error("bad amount");
    const id = item as string;
    return hideoutUpdate(nk, userId, (hideout) => {
        if (!hideout.buildings["storage"])
            throw new Error("the storage is not built");
        if (put) {
            hideout.storage[id] = (hideout.storage[id] || 0) + amount;
        } else {
            if ((hideout.storage[id] || 0) < amount)
                throw new Error("not enough in the storage");
            const taken = hideoutTakenToday(hideout, userId, nowUnixMs);
            if (taken + amount > hideoutCommons().takePerDay)
                throw new Error("the daily take limit is reached");
            hideout.taken[userId] = { day: walletDayOf(nowUnixMs), count: taken + amount };
            hideout.storage[id] -= amount;
            if (hideout.storage[id] === 0)
                delete hideout.storage[id];
            hideoutOwe(hideout, userId, id, amount);
        }
        hideoutJournal(hideout, { at: nowUnixMs, userId: userId, item: id, amount: put ? amount : -amount });
    }, nowUnixMs);
}

function hideoutPlantRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutPlant(nk, partyCaller(ctx), request.bed, request.plant, Date.now()));
}

function hideoutHarvestRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutHarvest(nk, partyCaller(ctx), partyRequest(payload).bed, Date.now()));
}

function hideoutClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutClaim(nk, partyCaller(ctx), payload ? partyRequest(payload).ack : undefined, Date.now()));
}

function hideoutStorePutRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutStore(nk, partyCaller(ctx), request.item, request.amount, true, Date.now()));
}

function hideoutStoreTakeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutStore(nk, partyCaller(ctx), request.item, request.amount, false, Date.now()));
}

function hideoutReadStation(raw: unknown): string | null {
    if (raw === undefined || raw === null || raw === "")
        return null;
    if (typeof raw !== "string" || !hideoutCommons().stations[raw])
        throw new Error("bad request");
    return raw;
}

function hideoutStationBuilt(value: HideoutValue, station: string): boolean {
    const building = hideoutCommons().stations[station];
    return !!building && (value.buildings[building] || 0) > 0;
}

function hideoutAtStation(value: HideoutValue, members: string[], station: string, nowUnixMs: number): string[] {
    return hideoutHere(value, members, nowUnixMs).filter((id) => value.crafting[id] === station);
}

function hideoutCraftView(value: HideoutValue, members: string[], userId: string, nowUnixMs: number, nameOf: (id: string) => string): { [station: string]: string[] } {
    const partners: { [station: string]: string[] } = {};
    for (const station of Object.keys(hideoutCommons().stations)) {
        const at = hideoutAtStation(value, members, station, nowUnixMs);
        partners[station] = hideoutStationBuilt(value, station) && at.indexOf(userId) >= 0 ? at.filter((id) => id !== userId).map(nameOf) : [];
    }
    return partners;
}

function hideoutBrewed(nk: nkruntime.Nakama, userId: string, recipe: unknown, fine: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof recipe !== "string" || hideoutCommons().recipes.indexOf(recipe) < 0 || typeof fine !== "boolean")
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (!hideoutStationBuilt(hideout, "cauldron"))
            throw new Error("the shared cauldron is not built");
        const partners = hideoutAtStation(hideout, meta.members, "cauldron", nowUnixMs).filter((id) => id !== userId);
        if (hideout.crafting[userId] !== "cauldron" || partners.length === 0)
            throw new Error("nobody brews with you");
        const day = walletDayOf(nowUnixMs);
        const brews = hideout.brews[userId] && hideout.brews[userId].day === day ? hideout.brews[userId].count : 0;
        if (brews >= hideoutCommons().brewsPerDay)
            throw new Error("the daily brew limit is reached");
        hideout.brews[userId] = { day: day, count: brews + 1 };
        for (const id of partners)
            hideoutOwe(hideout, id, "potion:" + recipe + (fine ? ":fine" : ""), 1);
    }, nowUnixMs);
}

function hideoutBrewedRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutBrewed(nk, partyCaller(ctx), request.recipe, request.fine, Date.now()));
}
