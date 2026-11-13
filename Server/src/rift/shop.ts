interface RiftShopRule {
    id: string;
    price: number;
    sparks: number;
    perWindow: number;
}

interface RiftShopValue {
    bought: { [id: string]: number };
    sparks: { [riftId: string]: number };
}

const RiftShopCollection = "rift_shop";
const RiftShopReason = "rift_shop";

function riftShopWindow(riftId: number): RiftWindow | null {
    if (!(riftId > 0) || riftId !== Math.floor(riftId))
        return null;
    return riftWindowAt(RiftServerRules, riftId * 1000);
}

function riftShopOpen(riftId: number, nowUnixMs: number): boolean {
    const window = riftShopWindow(riftId);
    return window !== null && nowUnixMs >= riftId * 1000 && nowUnixMs < Math.max(window.endUnixMs, riftPartyEnd(riftId));
}

function riftShopRead(nk: nkruntime.Nakama, userId: string, windowId: number): { value: RiftShopValue; version: string } {
    const objects = nk.storageRead([{ collection: RiftShopCollection, key: "w" + windowId, userId: userId }]);
    if (objects.length === 0)
        return { value: { bought: {}, sparks: {} }, version: "*" };
    const value = objects[0].value as RiftShopValue;
    return { value: { bought: value.bought || {}, sparks: value.sparks || {} }, version: objects[0].version };
}

function riftShopView(nk: nkruntime.Nakama, userId: string, riftId: number): { [key: string]: any } {
    const window = riftShopWindow(riftId);
    const stored = window ? riftShopRead(nk, userId, window.id).value : { bought: {}, sparks: {} } as RiftShopValue;
    return { riftId: riftId, bought: stored.bought, sparks: stored.sparks[String(riftId)] || 0, crystals: walletRead(nk, userId).value.balances[RiftServerRules.shopCurrency] || 0 };
}

function riftShopBuy(nk: nkruntime.Nakama, userId: string, riftId: number, id: string, nowUnixMs: number): { [key: string]: any } {
    const offer = RiftServerRules.shop.filter((rule) => rule.id === id)[0];
    if (!offer)
        throw new Error("no such offer");
    if (!riftShopOpen(riftId, nowUnixMs))
        throw new Error("the rift is closed");
    const windowId = riftShopWindow(riftId)!.id;
    const stored = riftShopRead(nk, userId, windowId);
    const count = (stored.value.bought[id] || 0) + 1;
    if (count > offer.perWindow)
        throw new Error("window limit reached");
    walletSpend(nk, userId, RiftServerRules.shopCurrency, offer.price, RiftShopReason, nowUnixMs);
    stored.value.bought[id] = count;
    stored.value.sparks[String(riftId)] = (stored.value.sparks[String(riftId)] || 0) + offer.sparks;
    try {
        nk.storageWrite([{ collection: RiftShopCollection, key: "w" + windowId, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        walletRefund(nk, userId, RiftServerRules.shopCurrency, offer.price, RiftShopReason, nowUnixMs);
        throw new Error("the shop is busy, try again");
    }
    return riftShopView(nk, userId, riftId);
}

function riftShopRiftId(payload: string): number {
    const request = partyRequest(payload);
    if (typeof request.riftId !== "number")
        throw new Error("bad request");
    return request.riftId;
}

function riftShopRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(riftShopView(nk, partyCaller(ctx), riftShopRiftId(payload)));
}

function riftShopBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.id !== "string")
        throw new Error("bad request");
    return JSON.stringify(riftShopBuy(nk, partyCaller(ctx), riftShopRiftId(payload), request.id, Date.now()));
}
