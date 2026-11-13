interface HideoutTierRule {
    members: number;
    bosses: number;
    orders: number;
    sitSeconds: number;
    fireLevel: number;
    cornerDecor: number;
    needs: { counter: string; target: number }[];
    opens: string[];
}

interface HideoutBuildingRule {
    id: string;
    guild: string;
    levels: { points: number; hours: number }[];
}

interface HideoutRules {
    hereMs: number;
    maxSitStepMs: number;
    maxCaughtPerMark: number;
    cornerDecor: number;
    frozenMs: number;
    trophyHall: string;
    decorIds: string[];
    tiers: HideoutTierRule[];
    buildings: HideoutBuildingRule[];
    levelTiers: number[];
    materials: { [item: string]: number };
    maxGive: number;
    orderDays: number;
    orderFromTier: number;
    orderSource: string;
    orderGoals: HideoutOrderRule[];
}

interface HideoutProject {
    building: string;
    level: number;
    points: number;
    by: { [userId: string]: number };
    fromStorage?: number;
}

interface HideoutCorner {
    slot: number;
    hero: string;
    path: string;
    decor: string[];
}

interface HideoutValue {
    tier: number;
    sitSeconds: number;
    orders: number;
    here: { [userId: string]: number };
    fishing: { [userId: string]: number };
    sitAt: number;
    buildings: { [id: string]: number };
    project: HideoutProject | null;
    wants: { [userId: string]: string[] };
    counters: { [counter: string]: number };
    corners: { [userId: string]: HideoutCorner };
    former: string[];
    garden: HideoutBed[];
    gardenAt: number;
    storage: { [item: string]: number };
    journal: HideoutJournalEntry[];
    taken: { [userId: string]: { day: number; count: number } };
    owed: { [userId: string]: { [item: string]: number } };
    fireBy: { [userId: string]: number };
    crafting: { [userId: string]: string };
    brews: { [userId: string]: { day: number; count: number } };
    pets: { [userId: string]: HideoutPet };
    petted: { [userId: string]: { day: number; count: number } };
    warmed: { [userId: string]: { day: number; owners: string[] } };
    farTrip: HideoutFarTrip | null;
    farTrips: { day: number; count: number };
    order: HideoutOrder | null;
    claims: { [userId: string]: { seq: number; items: { [item: string]: number } } };
}

interface HideoutChronicle {
    bosses: number;
    hours: number;
    guardiansTogether: number;
    riftDepth: number;
}

declare const HideoutServerRules: HideoutRules;

const HideoutCollection = "party_hideout";
const HideoutWriteAttempts = 4;
const HideoutMaxFormer = 16;
const HideoutMaxCornerText = 40;
const HideoutCornerBuilding = "corner";

function hideoutEmpty(): HideoutValue {
    return { tier: 0, sitSeconds: 0, orders: 0, here: {}, fishing: {}, sitAt: 0, buildings: {}, project: null, wants: {}, counters: {}, corners: {}, former: [], garden: [], gardenAt: 0, storage: {}, journal: [], taken: {}, owed: {}, fireBy: {}, crafting: {}, brews: {}, pets: {}, petted: {}, warmed: {}, farTrip: null, farTrips: { day: 0, count: 0 }, order: null, claims: {} };
}

function hideoutRead(nk: nkruntime.Nakama, partyId: string): { value: HideoutValue; version: string } {
    const objects = nk.storageRead([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: hideoutEmpty(), version: "*" };
    const stored = objects[0].value as HideoutValue;
    const value = hideoutEmpty();
    value.tier = stored.tier || 0;
    value.sitSeconds = stored.sitSeconds || 0;
    value.orders = stored.orders || 0;
    value.here = stored.here || {};
    value.fishing = stored.fishing || {};
    value.sitAt = stored.sitAt || 0;
    value.buildings = stored.buildings || {};
    value.project = stored.project || null;
    value.wants = stored.wants || {};
    value.counters = stored.counters || {};
    value.corners = stored.corners || {};
    value.former = stored.former || [];
    value.garden = stored.garden || [];
    value.gardenAt = stored.gardenAt || 0;
    value.storage = stored.storage || {};
    value.journal = stored.journal || [];
    value.taken = stored.taken || {};
    value.owed = stored.owed || {};
    value.fireBy = stored.fireBy || {};
    value.crafting = stored.crafting || {};
    value.brews = stored.brews || {};
    value.pets = stored.pets || {};
    value.petted = stored.petted || {};
    value.warmed = stored.warmed || {};
    value.farTrip = stored.farTrip || null;
    value.farTrips = stored.farTrips || { day: 0, count: 0 };
    value.order = stored.order || null;
    value.claims = stored.claims || {};
    return { value: value, version: objects[0].version };
}

function hideoutChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: HideoutValue) => T): T {
    for (let attempt = 1; attempt <= HideoutWriteAttempts; attempt++) {
        const stored = hideoutRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the hideout is busy, try again");
}

function hideoutChronicle(nk: nkruntime.Nakama, partyId: string): HideoutChronicle {
    const stats = chronicleRead(nk, partyId).value.stats;
    return { bosses: stats.bossesTogether, hours: stats.ticksTogether / chronicleTicksPerHour(), guardiansTogether: stats.guardiansTogether, riftDepth: stats.riftDepth };
}

function hideoutBuildingRule(id: string): HideoutBuildingRule | null {
    return HideoutServerRules.buildings.filter((building) => building.id === id)[0] || null;
}

function hideoutOpenBuildings(tier: number): string[] {
    const open: string[] = [];
    for (let i = 0; i < tier && i < HideoutServerRules.tiers.length; i++)
        for (const id of HideoutServerRules.tiers[i].opens)
            if (open.indexOf(id) < 0 && hideoutBuildingRule(id))
                open.push(id);
    return open;
}

function hideoutGuildReady(nk: nkruntime.Nakama, guild: string): boolean {
    return guild.length === 0 || townGuilds(nk).indexOf(guild) >= 0;
}

function hideoutAllLevel(nk: nkruntime.Nakama, value: HideoutValue): number {
    const counted = hideoutOpenBuildings(value.tier).filter((id) => hideoutGuildReady(nk, hideoutBuildingRule(id)!.guild));
    if (counted.length === 0)
        return 0;
    return Math.min.apply(null, counted.map((id) => value.buildings[id] || 0));
}

function hideoutCounter(nk: nkruntime.Nakama, value: HideoutValue, chronicle: HideoutChronicle, counter: string): number {
    switch (counter) {
        case "guardiansTogether": return chronicle.guardiansTogether;
        case "riftDepth": return chronicle.riftDepth;
        case "hoursTogether": return chronicle.hours;
        case "allBuildingsLevel": return hideoutAllLevel(nk, value);
        default: return value.counters[counter] || 0;
    }
}

function hideoutHere(value: HideoutValue, members: string[], nowUnixMs: number): string[] {
    return members.filter((userId) => value.here[userId] !== undefined && nowUnixMs - value.here[userId] <= HideoutServerRules.hereMs);
}

function hideoutFishers(value: HideoutValue, members: string[], nowUnixMs: number): string[] {
    return members.filter((userId) => value.fishing[userId] !== undefined && nowUnixMs - value.fishing[userId] <= HideoutServerRules.hereMs);
}

function hideoutReached(nk: nkruntime.Nakama, value: HideoutValue, tier: HideoutTierRule, members: number, chronicle: HideoutChronicle): boolean {
    return members >= tier.members && chronicle.bosses >= tier.bosses && value.orders >= tier.orders && value.sitSeconds >= tier.sitSeconds
        && (value.buildings["bonfire"] || 0) >= tier.fireLevel && tier.needs.every((need) => hideoutCounter(nk, value, chronicle, need.counter) >= need.target);
}

function hideoutRise(nk: nkruntime.Nakama, value: HideoutValue, members: number, chronicle: HideoutChronicle): void {
    const tiers = HideoutServerRules.tiers;
    while (value.tier < tiers.length && hideoutReached(nk, value, tiers[value.tier], members, chronicle))
        value.tier++;
}

function hideoutSit(value: HideoutValue, members: string[], nowUnixMs: number, signs: { [userId: string]: number }): void {
    const here = hideoutHere(value, members, nowUnixMs);
    const together = here.length >= 2;
    if (together && value.sitAt > 0) {
        const step = Math.floor(Math.min(nowUnixMs - value.sitAt, HideoutServerRules.maxSitStepMs) / 1000);
        value.sitSeconds += step;
        hideoutFireSigns(value, here, step, signs);
    }
    value.sitAt = together ? nowUnixMs : 0;
}

function hideoutFinish(value: HideoutValue, chronicle: HideoutChronicle): void {
    const project = value.project;
    if (!project)
        return;
    const rule = hideoutBuildingRule(project.building);
    const cost = rule ? rule.levels[project.level - 1] : null;
    if (!cost || (project.points >= cost.points && chronicle.hours >= cost.hours)) {
        if (cost)
            value.buildings[project.building] = project.level;
        value.project = null;
    }
}

function hideoutCanQueue(nk: nkruntime.Nakama, value: HideoutValue, id: string): string {
    const rule = hideoutBuildingRule(id);
    if (!rule || hideoutOpenBuildings(value.tier).indexOf(id) < 0)
        return "no place for it yet";
    if (!hideoutGuildReady(nk, rule.guild))
        return "the guild hall is not built in the town";
    const level = (value.buildings[id] || 0) + 1;
    if (level > rule.levels.length)
        return "already at the top level";
    if (value.tier < HideoutServerRules.levelTiers[level - 1])
        return "needs a higher tier";
    return "";
}

function hideoutCornerCount(tier: number): number {
    let count = 0;
    for (let i = 0; i < tier && i < HideoutServerRules.tiers.length; i++)
        count += HideoutServerRules.tiers[i].opens.filter((id) => id === HideoutCornerBuilding).length;
    return count;
}

function hideoutCornerDecor(tier: number): number {
    let spots = HideoutServerRules.cornerDecor;
    for (let i = 0; i < tier && i < HideoutServerRules.tiers.length; i++)
        spots = Math.max(spots, HideoutServerRules.tiers[i].cornerDecor || 0);
    return spots;
}

function hideoutNoteFormer(value: HideoutValue, userIds: string[]): void {
    for (const id of userIds)
        if (value.former.indexOf(id) < 0)
            value.former.push(id);
    if (value.former.length > HideoutMaxFormer)
        value.former = value.former.slice(value.former.length - HideoutMaxFormer);
}

function hideoutSeatCorners(value: HideoutValue, members: string[]): void {
    for (const id of Object.keys(value.corners))
        if (members.indexOf(id) < 0)
            delete value.corners[id];
    const count = hideoutCornerCount(value.tier);
    for (const id of members) {
        if (value.corners[id])
            continue;
        const taken = Object.keys(value.corners).map((other) => value.corners[other].slot);
        let slot = 0;
        while (taken.indexOf(slot) >= 0)
            slot++;
        if (slot >= count)
            break;
        value.corners[id] = { slot: slot, hero: "", path: "", decor: [] };
    }
}

function hideoutView(nk: nkruntime.Nakama, group: nkruntime.Group, value: HideoutValue, chronicle: HideoutChronicle, userId: string, nowUnixMs: number): { [key: string]: any } {
    const meta = partyMetaOf(group);
    const here = hideoutHere(value, meta.members, nowUnixMs);
    const fishers = hideoutFishers(value, meta.members, nowUnixMs);
    const cornered = Object.keys(value.corners).filter((id) => meta.members.indexOf(id) >= 0);
    const petOwners = Object.keys(value.pets).filter((id) => meta.members.indexOf(id) >= 0);
    const named = here.concat(fishers, cornered, petOwners, value.farTrip ? Object.keys(value.farTrip.sent) : []).filter((id, index, all) => all.indexOf(id) === index);
    const users = named.length > 0 ? nk.usersGetId(named) : [];
    const nameOf = (id: string) => {
        const user = users.filter((entry) => entry.userId === id)[0];
        return user ? user.displayName || user.username : "";
    };
    const project = value.project;
    const rule = project ? hideoutBuildingRule(project.building) : null;
    const cost = project && rule ? rule.levels[project.level - 1] : null;
    const wants: { [id: string]: number } = {};
    for (const id of Object.keys(value.wants))
        if (meta.members.indexOf(id) >= 0)
            for (const building of value.wants[id])
                wants[building] = (wants[building] || 0) + 1;
    return {
        tier: value.tier, members: meta.members.length, bosses: chronicle.bosses, orders: value.orders, sitSeconds: value.sitSeconds,
        fireLevel: value.buildings["bonfire"] || 0, hoursTogether: Math.floor(chronicle.hours), leader: meta.leader === userId,
        here: here.map((id) => ({ userId: id, name: nameOf(id) })),
        fishers: fishers.map((id) => ({ userId: id, name: nameOf(id) })),
        fishingTogether: fishers.length >= 2 && fishers.indexOf(userId) >= 0, fishTogether: value.counters["fishTogether"] || 0,
        buildings: value.buildings,
        canQueue: hideoutOpenBuildings(value.tier).filter((id) => hideoutCanQueue(nk, value, id) === ""),
        project: project && cost ? { building: project.building, level: project.level, points: project.points, target: cost.points, hours: cost.hours, yours: project.by[userId] || 0, fromStorage: project.fromStorage || 0 } : null,
        wants: wants, mine: value.wants[userId] || [],
        corners: cornered.map((id) => ({ userId: id, name: nameOf(id), slot: value.corners[id].slot, hero: value.corners[id].hero, path: value.corners[id].path, decor: value.corners[id].decor }))
            .sort((a, b) => a.slot - b.slot),
        cornerDecor: hideoutCornerDecor(value.tier), myCorner: !!value.corners[userId] && meta.members.indexOf(userId) >= 0,
        commons: hideoutCommonsView(nk, value, meta.members, userId, nowUnixMs),
        crafts: hideoutCraftView(value, meta.members, userId, nowUnixMs, nameOf),
        trophies: hideoutTrophies(nk, group.id, value, chronicle),
        companions: companionView(value, meta, userId, nowUnixMs, nameOf),
        order: hideoutOrderView(value, chronicle, meta.leader === userId, nowUnixMs)
    };
}

function hideoutUpdate(nk: nkruntime.Nakama, userId: string, change: (value: HideoutValue, meta: PartyMeta, chronicle: HideoutChronicle) => void, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const meta = partyMetaOf(group);
    const chronicle = hideoutChronicle(nk, group.id);
    let signs: { [userId: string]: number } = {};
    let pairs: string[] = [];
    let ordered = false;
    const value = hideoutChange(nk, group.id, (hideout) => {
        signs = {};
        hideoutGrowGarden(hideout, hideoutHere(hideout, meta.members, nowUnixMs).length >= 2, nowUnixMs);
        change(hideout, meta, chronicle);
        for (const id of Object.keys(hideout.here))
            if (meta.members.indexOf(id) < 0 || nowUnixMs - hideout.here[id] > HideoutServerRules.hereMs)
                delete hideout.here[id];
        for (const id of Object.keys(hideout.fishing))
            if (meta.members.indexOf(id) < 0 || nowUnixMs - hideout.fishing[id] > HideoutServerRules.hereMs)
                delete hideout.fishing[id];
        hideoutSit(hideout, meta.members, nowUnixMs, signs);
        pairs = companionPairsHere(hideout, meta.members, nowUnixMs);
        hideoutFinish(hideout, chronicle);
        hideoutRise(nk, hideout, meta.members.length, chronicle);
        ordered = hideoutOrderStep(hideout, chronicle, nowUnixMs);
        hideoutRise(nk, hideout, meta.members.length, chronicle);
        hideoutNoteFormer(hideout, meta.members);
        hideoutSeatCorners(hideout, meta.members);
        return hideout;
    });
    for (const id of Object.keys(signs)) {
        try {
            walletGrant(nk, id, "hideout_fire", signs[id], nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    for (const id of ordered ? meta.members : []) {
        try {
            walletGrant(nk, id, HideoutServerRules.orderSource, 1, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    for (const id of pairs) {
        try {
            walletGrant(nk, id, "companion_pair", 1, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
    return hideoutView(nk, group, value, chronicle, userId, nowUnixMs);
}

function hideoutPresence(nk: nkruntime.Nakama, userId: string, here: boolean | null, nowUnixMs: number, fishing: boolean | null = null, caught: number = 0, tend: HideoutTend | null = null, craft: string | null = null): { [key: string]: any } {
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (here === true)
            hideout.here[userId] = nowUnixMs;
        if (here === true && craft)
            hideout.crafting[userId] = craft;
        else if (here !== null)
            delete hideout.crafting[userId];
        if (here === true && tend)
            hideoutTendGarden(hideout, userId, tend, hideoutHere(hideout, meta.members, nowUnixMs).length >= 2);
        else if (here === false)
            delete hideout.here[userId];
        if (fishing === true)
            hideout.fishing[userId] = nowUnixMs;
        else if (fishing === false)
            delete hideout.fishing[userId];
        if (fishing === true && caught > 0 && hideoutFishers(hideout, meta.members, nowUnixMs).length >= 2)
            hideout.counters["fishTogether"] = (hideout.counters["fishTogether"] || 0) + Math.min(caught, HideoutServerRules.maxCaughtPerMark);
    }, nowUnixMs);
}

function hideoutQueue(nk: nkruntime.Nakama, userId: string, building: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof building !== "string")
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (hideout.project && hideout.project.points > 0)
            throw new Error("another building is under way");
        const reason = hideoutCanQueue(nk, hideout, building);
        if (reason)
            throw new Error(reason);
        hideout.project = { building: building, level: (hideout.buildings[building] || 0) + 1, points: 0, by: {} };
    }, nowUnixMs);
}

function hideoutGive(nk: nkruntime.Nakama, userId: string, item: unknown, amount: unknown, nowUnixMs: number): { [key: string]: any } {
    const points = typeof item === "string" ? HideoutServerRules.materials[item] || 0 : 0;
    if (points <= 0)
        throw new Error("not a building material");
    if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < 1 || amount > HideoutServerRules.maxGive)
        throw new Error("bad amount");
    if (!partyGroupOfUser(nk, userId))
        throw new Error("not in a party");
    const receipt = inflowSpend(nk, userId, item as string, amount, nowUnixMs);
    return inflowGuard(nk, userId, receipt, nowUnixMs, () => hideoutUpdate(nk, userId, (hideout) => {
        if (!hideout.project)
            throw new Error("nothing is being built");
        hideout.project.points += points * amount;
        hideout.project.by[userId] = (hideout.project.by[userId] || 0) + points * amount;
    }, nowUnixMs));
}

function hideoutWant(nk: nkruntime.Nakama, userId: string, building: unknown, on: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof building !== "string" || !hideoutBuildingRule(building) || typeof on !== "boolean")
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout) => {
        const mine = (hideout.wants[userId] || []).filter((id) => id !== building);
        if (on)
            mine.push(building);
        hideout.wants[userId] = mine;
    }, nowUnixMs);
}

function hideoutViewRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutPresence(nk, partyCaller(ctx), null, Date.now()));
}

function hideoutPresenceRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    const here = request.here;
    const fishing = request.fishing === undefined ? null : request.fishing;
    const caught = request.caught === undefined ? 0 : request.caught;
    if (typeof here !== "boolean" || (fishing !== null && typeof fishing !== "boolean"))
        throw new Error("bad request");
    if (typeof caught !== "number" || caught !== Math.floor(caught) || caught < 0)
        throw new Error("bad request");
    return JSON.stringify(hideoutPresence(nk, partyCaller(ctx), here, Date.now(), fishing, caught, hideoutReadTend(request.tend), hideoutReadStation(request.craft)));
}

function hideoutQueueRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutQueue(nk, partyCaller(ctx), partyRequest(payload).building, Date.now()));
}

function hideoutGiveRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutGive(nk, partyCaller(ctx), request.item, request.amount, Date.now()));
}

function hideoutWantRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutWant(nk, partyCaller(ctx), request.building, request.on, Date.now()));
}

function hideoutCornerText(value: unknown): string {
    if (value === undefined || value === null)
        return "";
    if (typeof value !== "string" || value.length > HideoutMaxCornerText)
        throw new Error("bad request");
    return value;
}

function hideoutCorner(nk: nkruntime.Nakama, userId: string, hero: unknown, path: unknown, decor: unknown, nowUnixMs: number): { [key: string]: any } {
    const heroText = hideoutCornerText(hero);
    const pathText = hideoutCornerText(path);
    if (!Array.isArray(decor))
        throw new Error("bad request");
    for (let i = 0; i < decor.length; i++)
        if (typeof decor[i] !== "string" || HideoutServerRules.decorIds.indexOf(decor[i]) < 0 || decor.indexOf(decor[i]) !== i)
            throw new Error("unknown decor");
    return hideoutUpdate(nk, userId, (hideout, meta, chronicle) => {
        hideoutRise(nk, hideout, meta.members.length, chronicle);
        hideoutSeatCorners(hideout, meta.members);
        const corner = hideout.corners[userId];
        if (!corner)
            throw new Error("no free corner");
        if (decor.length > hideoutCornerDecor(hideout.tier))
            throw new Error("the corner is full");
        corner.hero = heroText;
        corner.path = pathText;
        corner.decor = (decor as string[]).slice();
    }, nowUnixMs);
}

function hideoutCornerRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(hideoutCorner(nk, partyCaller(ctx), request.hero, request.path, request.decor, Date.now()));
}

function hideoutLeaving(nk: nkruntime.Nakama, partyId: string, userId: string): void {
    if (nk.storageRead([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser }]).length === 0)
        return;
    hideoutChange(nk, partyId, (hideout) => {
        delete hideout.corners[userId];
        delete hideout.pets[userId];
        delete hideout.here[userId];
        delete hideout.fishing[userId];
        hideoutNoteFormer(hideout, [userId]);
    });
}

function hideoutWorthFreezing(nk: nkruntime.Nakama, partyId: string, former: string[]): boolean {
    const objects = nk.storageRead([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0 || ((objects[0].value as HideoutValue).tier || 0) < 1)
        return false;
    hideoutChange(nk, partyId, (hideout) => {
        hideout.corners = {};
        hideout.here = {};
        hideout.fishing = {};
        hideoutNoteFormer(hideout, former);
    });
    return true;
}

function hideoutFormerOf(nk: nkruntime.Nakama, partyId: string): string[] {
    return hideoutRead(nk, partyId).value.former;
}

function hideoutMove(nk: nkruntime.Nakama, from: string, to: string): void {
    const objects = nk.storageRead([{ collection: HideoutCollection, key: from, userId: PartySystemUser }]);
    if (objects.length > 0)
        nk.storageWrite([{ collection: HideoutCollection, key: to, userId: PartySystemUser, value: objects[0].value, permissionRead: 0, permissionWrite: 0 }]);
    nk.storageDelete([{ collection: HideoutCollection, key: from, userId: PartySystemUser }]);
}

function hideoutForget(nk: nkruntime.Nakama, partyId: string): void {
    nk.storageDelete([{ collection: HideoutCollection, key: partyId, userId: PartySystemUser }]);
}

function hideoutTrophies(nk: nkruntime.Nakama, partyId: string, value: HideoutValue, chronicle: HideoutChronicle): { [key: string]: any } | null {
    if (!(value.buildings[HideoutServerRules.trophyHall] > 0))
        return null;
    const feats = chronicleRead(nk, partyId).value.feats;
    return {
        bosses: chronicle.bosses, guardians: chronicle.guardiansTogether, riftDepth: chronicle.riftDepth, hours: Math.floor(chronicle.hours),
        feats: Object.keys(feats).filter((id) => feats[id].level > 0).sort().map((id) => ({ id: id, level: feats[id].level }))
    };
}
