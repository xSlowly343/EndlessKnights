interface CompanionRules {
    companions: { [id: string]: string };
    shopIds: string[];
    shopPrice: number;
    shopPrices: { [id: string]: number };
    shopDays: number;
    stages: number;
    nameLength: number;
    pairs: string[][];
    biomes: { [biome: string]: { bone: string; fish: string } };
    petsPerDay: number;
    warmsPerFriendPerDay: number;
    farTripMs: number;
    farTripsPerDay: number;
    farMin: number;
    farMax: number;
    bonesPerCompanion: number;
    rarePerCompanion: number;
    rarePerBiome: number;
    soulsPerTrip: number;
}

interface HideoutPet {
    companion: string;
    name: string;
    stage: number;
}

interface HideoutFarTrip {
    sent: { [userId: string]: string };
    endsAt: number;
    claimed: string[];
}

interface CompanionShopValue {
    week: number;
    bought: string[];
}

declare const CompanionServerRules: CompanionRules;

const CompanionShopCollection = "companion_shop";
const CompanionShopKey = "week";
const CompanionPetItem = "companion_pet:";
const CompanionWarmItem = "companion_warm";
const CompanionSoulItem = "companion_soul";

function companionKnown(id: unknown): id is string {
    return typeof id === "string" && Object.prototype.hasOwnProperty.call(CompanionServerRules.companions, id);
}

function companionCount(record: { day: number; count: number } | undefined, day: number): number {
    return record && record.day === day ? record.count : 0;
}

function companionWarmedToday(value: HideoutValue, userId: string, day: number): string[] {
    const warmed = value.warmed[userId];
    return warmed && warmed.day === day ? warmed.owners : [];
}

function companionPairsHere(value: HideoutValue, members: string[], nowUnixMs: number): string[] {
    const here = hideoutHere(value, members, nowUnixMs);
    if (here.length < 2)
        return [];
    const sleeping = here.filter((id) => !!value.pets[id]).map((id) => value.pets[id].companion);
    const paired = CompanionServerRules.pairs.some((pair) => sleeping.indexOf(pair[0]) >= 0 && sleeping.indexOf(pair[1]) >= 0);
    return paired ? here : [];
}

function companionFarRunning(value: HideoutValue, nowUnixMs: number): boolean {
    return !!value.farTrip && value.farTrip.endsAt > 0 && nowUnixMs < value.farTrip.endsAt;
}

function companionFarBiomes(trip: HideoutFarTrip): string[] {
    const biomes: string[] = [];
    for (const id of Object.keys(trip.sent)) {
        const biome = CompanionServerRules.companions[trip.sent[id]];
        if (biomes.indexOf(biome) < 0)
            biomes.push(biome);
    }
    return biomes.sort();
}

function companionFarLoot(trip: HideoutFarTrip): { [item: string]: number } {
    const rules = CompanionServerRules;
    const loot: { [item: string]: number } = {};
    const sent = Object.keys(trip.sent).map((id) => trip.sent[id]);
    for (const companion of sent) {
        const bone = rules.biomes[rules.companions[companion]].bone;
        loot[bone] = (loot[bone] || 0) + rules.bonesPerCompanion;
    }
    if (rules.soulsPerTrip > 0)
        loot[CompanionSoulItem] = rules.soulsPerTrip;
    const biomes = companionFarBiomes(trip);
    const rare = Math.floor(sent.length * rules.rarePerCompanion * (1 + rules.rarePerBiome * (biomes.length - 1)) + 1e-9);
    for (let i = 0; i < rare; i++) {
        const fish = rules.biomes[biomes[i % biomes.length]].fish;
        loot[fish] = (loot[fish] || 0) + 1;
    }
    return loot;
}

function companionView(value: HideoutValue, meta: PartyMeta, userId: string, nowUnixMs: number, nameOf: (id: string) => string): { [key: string]: any } {
    const day = walletDayOf(nowUnixMs);
    const trip = value.farTrip;
    return {
        pets: Object.keys(value.pets).filter((id) => meta.members.indexOf(id) >= 0).sort().map((id) => ({
            userId: id, owner: nameOf(id), companion: value.pets[id].companion, name: value.pets[id].name, stage: value.pets[id].stage, mine: id === userId
        })),
        petsLeft: Math.max(0, CompanionServerRules.petsPerDay - companionCount(value.petted[userId], day)),
        warmed: companionWarmedToday(value, userId, day),
        far: trip ? {
            sent: Object.keys(trip.sent).sort().map((id) => ({ owner: nameOf(id), companion: trip.sent[id], mine: id === userId })),
            biomes: companionFarBiomes(trip),
            endsAt: trip.endsAt,
            running: companionFarRunning(value, nowUnixMs),
            ready: trip.endsAt > 0 && nowUnixMs >= trip.endsAt && !!trip.sent[userId] && trip.claimed.indexOf(userId) < 0
        } : null,
        farLeft: Math.max(0, CompanionServerRules.farTripsPerDay - companionCount(value.farTrips, day))
    };
}

function companionPlace(nk: nkruntime.Nakama, userId: string, companion: unknown, name: unknown, stage: unknown, nowUnixMs: number): { [key: string]: any } {
    if (companion === null || companion === undefined || companion === "")
        return hideoutUpdate(nk, userId, (hideout) => { delete hideout.pets[userId]; }, nowUnixMs);
    if (!companionKnown(companion))
        throw new Error("unknown companion");
    const text = name === undefined || name === null ? "" : name;
    if (typeof text !== "string" || text.length > CompanionServerRules.nameLength)
        throw new Error("bad name");
    if (typeof stage !== "number" || stage !== Math.floor(stage) || stage < 0 || stage >= CompanionServerRules.stages)
        throw new Error("bad request");
    return hideoutUpdate(nk, userId, (hideout) => {
        hideout.pets[userId] = { companion: companion, name: text, stage: stage };
    }, nowUnixMs);
}

function companionPet(nk: nkruntime.Nakama, userId: string, owner: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof owner !== "string" || owner.length === 0)
        throw new Error("bad request");
    if (owner === userId)
        throw new Error("pet a friend's companion");
    let petted = false;
    const view = hideoutUpdate(nk, userId, (hideout, meta) => {
        const pet = hideout.pets[owner];
        if (!pet || meta.members.indexOf(owner) < 0)
            throw new Error("no companion there");
        const day = walletDayOf(nowUnixMs);
        const count = companionCount(hideout.petted[userId], day);
        if (count >= CompanionServerRules.petsPerDay)
            throw new Error("the daily pet limit is reached");
        hideout.petted[userId] = { day: day, count: count + 1 };
        hideoutOwe(hideout, owner, CompanionPetItem + pet.companion, 1);
        petted = true;
    }, nowUnixMs);
    const granted = petted ? walletGrant(nk, userId, "companion_pet", 1, nowUnixMs) : 0;
    return { granted: granted, hideout: view };
}

function companionWarm(nk: nkruntime.Nakama, userId: string, owner: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof owner !== "string" || owner.length === 0)
        throw new Error("bad request");
    if (owner === userId)
        throw new Error("warm a friend's egg");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        if (meta.members.indexOf(owner) < 0)
            throw new Error("not in your party");
        const day = walletDayOf(nowUnixMs);
        const owners = companionWarmedToday(hideout, userId, day).slice();
        if (owners.filter((id) => id === owner).length >= CompanionServerRules.warmsPerFriendPerDay)
            throw new Error("you warmed this egg today");
        owners.push(owner);
        hideout.warmed[userId] = { day: day, owners: owners };
        hideoutOwe(hideout, owner, CompanionWarmItem, 1);
    }, nowUnixMs);
}

function companionFarJoin(nk: nkruntime.Nakama, userId: string, companion: unknown, nowUnixMs: number): { [key: string]: any } {
    if (companion !== null && !companionKnown(companion))
        throw new Error("unknown companion");
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        const last = hideout.farTrip;
        if (last && last.endsAt > 0) {
            if (companionFarRunning(hideout, nowUnixMs))
                throw new Error("the party trip is on the road");
            const waiting = Object.keys(last.sent).filter((id) => last.claimed.indexOf(id) < 0 && meta.members.indexOf(id) >= 0);
            if (waiting.length > 0 && nowUnixMs < last.endsAt + CompanionServerRules.farTripMs)
                throw new Error("the last party trip is not claimed yet");
            const loot = companionFarLoot(last);
            for (const id of waiting)
                for (const item of Object.keys(loot))
                    hideoutOwe(hideout, id, item, loot[item]);
            hideout.farTrip = null;
        }
        const trip = hideout.farTrip || { sent: {}, endsAt: 0, claimed: [] };
        if (companion === null) {
            delete trip.sent[userId];
        } else {
            if (!trip.sent[userId] && Object.keys(trip.sent).length >= CompanionServerRules.farMax)
                throw new Error("the party trip is full");
            trip.sent[userId] = companion;
        }
        hideout.farTrip = Object.keys(trip.sent).length > 0 ? trip : null;
    }, nowUnixMs);
}

function companionFarStart(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return hideoutUpdate(nk, userId, (hideout, meta) => {
        const trip = hideout.farTrip;
        if (!trip || trip.endsAt > 0)
            throw new Error("nobody is ready to go");
        for (const id of Object.keys(trip.sent))
            if (meta.members.indexOf(id) < 0)
                delete trip.sent[id];
        if (!trip.sent[userId])
            throw new Error("send your companion first");
        if (Object.keys(trip.sent).length < CompanionServerRules.farMin)
            throw new Error("the party trip needs more companions");
        const day = walletDayOf(nowUnixMs);
        const count = companionCount(hideout.farTrips, day);
        if (count >= CompanionServerRules.farTripsPerDay)
            throw new Error("the party trip already went today");
        hideout.farTrips = { day: day, count: count + 1 };
        hideout.counters["partyTrips"] = (hideout.counters["partyTrips"] || 0) + 1;
        trip.endsAt = nowUnixMs + CompanionServerRules.farTripMs;
    }, nowUnixMs);
}

function companionFarClaim(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    let items: { [item: string]: number } = {};
    const view = hideoutUpdate(nk, userId, (hideout) => {
        const trip = hideout.farTrip;
        if (!trip || trip.endsAt === 0 || nowUnixMs < trip.endsAt || !trip.sent[userId])
            throw new Error("nothing to claim");
        if (trip.claimed.indexOf(userId) >= 0)
            throw new Error("already claimed");
        trip.claimed.push(userId);
        items = companionFarLoot(trip);
    }, nowUnixMs);
    const granted = walletGrant(nk, userId, "companion_trip", 1, nowUnixMs);
    return { items: items, granted: granted, hideout: view };
}

function companionWeekOf(nowUnixMs: number): number {
    return Math.floor(walletDayOf(nowUnixMs) / CompanionServerRules.shopDays);
}

function companionShopBuy(nk: nkruntime.Nakama, userId: string, companion: unknown, nowUnixMs: number): { [key: string]: any } {
    if (!companionKnown(companion) || CompanionServerRules.shopIds.indexOf(companion) < 0)
        throw new Error("not in the shop");
    const week = companionWeekOf(nowUnixMs);
    const objects = nk.storageRead([{ collection: CompanionShopCollection, key: CompanionShopKey, userId: userId }]);
    const stored = objects.length > 0 ? objects[0].value as CompanionShopValue : null;
    const value: CompanionShopValue = stored && stored.week === week ? { week: week, bought: stored.bought.slice() } : { week: week, bought: [] };
    if (value.bought.indexOf(companion) >= 0)
        throw new Error("already bought this week");
    const price = CompanionServerRules.shopPrices[companion] || CompanionServerRules.shopPrice;
    const balance = walletSpend(nk, userId, "friendship", price, "companion_shop_buy", nowUnixMs);
    value.bought.push(companion);
    try {
        nk.storageWrite([{ collection: CompanionShopCollection, key: CompanionShopKey, userId: userId, value: value as any, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        walletRefund(nk, userId, "friendship", price, "companion_shop_refund", nowUnixMs);
        throw new Error("the shop is busy, try again");
    }
    return { companion: companion, friendship: balance, bought: value.bought };
}

function companionPlaceRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(companionPlace(nk, partyCaller(ctx), request.companion, request.name, request.stage, Date.now()));
}

function companionPetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionPet(nk, partyCaller(ctx), partyRequest(payload).owner, Date.now()));
}

function companionWarmRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionWarm(nk, partyCaller(ctx), partyRequest(payload).owner, Date.now()));
}

function companionFarJoinRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(companionFarJoin(nk, partyCaller(ctx), request.companion === undefined ? null : request.companion, Date.now()));
}

function companionFarStartRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionFarStart(nk, partyCaller(ctx), Date.now()));
}

function companionFarClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionFarClaim(nk, partyCaller(ctx), Date.now()));
}

function companionShopBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionShopBuy(nk, partyCaller(ctx), partyRequest(payload).companion, Date.now()));
}
