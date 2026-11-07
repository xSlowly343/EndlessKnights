interface ChestRule {
    id: string;
    price: number;
    ingots: number;
    essence: number;
    perWeek: number;
}

interface ChestRules {
    currency: string;
    chests: ChestRule[];
}

interface ChestBuysValue {
    week: number;
    bought: { [id: string]: number };
}

declare const ChestServerRules: ChestRules;

const ChestCollection = "chest_buys";
const ChestKey = "week";
const ChestReason = "chest";

function chestRule(id: string): ChestRule | null {
    return ChestServerRules.chests.filter((chest) => chest.id === id)[0] || null;
}

function chestRead(nk: nkruntime.Nakama, userId: string, week: number): { value: ChestBuysValue; version: string } {
    const objects = nk.storageRead([{ collection: ChestCollection, key: ChestKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { week: week, bought: {} }, version: "*" };
    const value = objects[0].value as ChestBuysValue;
    return { value: value.week === week ? { week: week, bought: value.bought || {} } : { week: week, bought: {} }, version: objects[0].version };
}

function chestWrite(nk: nkruntime.Nakama, userId: string, value: ChestBuysValue, version: string): void {
    nk.storageWrite([{ collection: ChestCollection, key: ChestKey, userId: userId, value: value as any, version: version, permissionRead: 1, permissionWrite: 0 }]);
}

function chestView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, letter: string): { [key: string]: any } {
    const week = weeklyWeekOf(nowUnixMs);
    return { week: week, bought: chestRead(nk, userId, week).value.bought, crystals: walletRead(nk, userId).value.balances[ChestServerRules.currency] || 0, letter: letter };
}

function chestUndo(nk: nkruntime.Nakama, userId: string, week: number, id: string): void {
    const stored = chestRead(nk, userId, week);
    stored.value.bought[id] = Math.max(0, (stored.value.bought[id] || 0) - 1);
    chestWrite(nk, userId, stored.value, stored.version);
}

function chestBuy(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, id: string, nowUnixMs: number): { [key: string]: any } {
    const chest = chestRule(id);
    if (!chest)
        throw new Error("no such chest");
    const week = weeklyWeekOf(nowUnixMs);
    const stored = chestRead(nk, userId, week);
    const count = (stored.value.bought[id] || 0) + 1;
    if (count > chest.perWeek)
        throw new Error("weekly limit reached");
    walletSpend(nk, userId, ChestServerRules.currency, chest.price, ChestReason, nowUnixMs);
    stored.value.bought[id] = count;
    try {
        chestWrite(nk, userId, stored.value, stored.version);
    } catch (error) {
        walletRefund(nk, userId, ChestServerRules.currency, chest.price, ChestReason, nowUnixMs);
        throw new Error("the shop is busy, try again");
    }
    const letterId = "chest-" + week + "-" + id + "-" + count;
    const sent = mailSend(nk, userId, {
        id: letterId, kind: "chest", at: nowUnixMs, place: 0, of: 0, depth: 0, title: id,
        reward: { shards: 0, ingots: chest.ingots, essence: chest.essence }, claimed: false, claimedAt: 0
    });
    if (!sent) {
        walletRefund(nk, userId, ChestServerRules.currency, chest.price, ChestReason, nowUnixMs);
        try {
            chestUndo(nk, userId, week, id);
        } catch (error) {
            logger.warn("chest purchase of %s was refunded but the weekly count stayed for %s", id, userId);
        }
        throw new Error("the shop is busy, try again");
    }
    return chestView(nk, userId, nowUnixMs, letterId);
}

function chestShopRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(chestView(nk, partyCaller(ctx), Date.now(), ""));
}

function chestBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.id !== "string")
        throw new Error("bad request");
    return JSON.stringify(chestBuy(nk, logger, partyCaller(ctx), request.id, Date.now()));
}
