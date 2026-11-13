interface RiftSupplyRules {
    dishes: string[];
    recipes: string[];
    supplySends: number;
    supplySource: string;
}

interface RiftSupplyValue {
    window: number;
    from: string;
    fromName: string;
    item: string;
    at: number;
}

const RiftSupplyCollection = "rift_supply";
const RiftSupplyKey = "received";
const RiftSupplySentKey = "sent";

function riftSupplyRules(): RiftSupplyRules {
    return HideoutServerRules as unknown as RiftSupplyRules;
}

function riftSupplyKnown(item: unknown): boolean {
    if (typeof item !== "string")
        return false;
    const rules = riftSupplyRules();
    if (item.indexOf("dish:") === 0)
        return rules.dishes.indexOf(item.substring(5)) >= 0;
    if (item.indexOf("potion:") !== 0)
        return false;
    const recipe = item.substring(7).replace(/:fine$/, "");
    return rules.recipes.indexOf(recipe) >= 0;
}

function riftSupplyTeam(nk: nkruntime.Nakama, userId: string, other: string, nowUnixMs: number): boolean {
    return circleSameGroup(partyGroupOfUser(nk, userId), partyGroupOfUser(nk, other))
        || circleSameGroup(clanGroupOfUser(nk, userId, nowUnixMs), clanGroupOfUser(nk, other, nowUnixMs));
}

function riftSupplyOf(nk: nkruntime.Nakama, userId: string, windowId: number): RiftSupplyValue | null {
    const objects = nk.storageRead([{ collection: RiftSupplyCollection, key: RiftSupplyKey, userId: userId }]);
    if (objects.length === 0)
        return null;
    const value = objects[0].value as RiftSupplyValue;
    return value.window === windowId ? value : null;
}

function riftSupplySend(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const to = request && request.to;
    const item = request && request.item;
    if (typeof to !== "string" || to.length === 0 || !riftSupplyKnown(item))
        throw new Error("bad request");
    if (to === userId)
        throw new Error("not to yourself");
    const window = riftWindowAt(RiftServerRules, nowUnixMs);
    if (!window)
        throw new Error("the rift is closed");
    if (!riftSupplyTeam(nk, userId, to, nowUnixMs))
        throw new Error("supplies go only to the party and the clan");
    const sent = nk.storageRead([{ collection: RiftSupplyCollection, key: RiftSupplySentKey, userId: userId }]);
    const sentValue = sent.length > 0 ? sent[0].value as { window: number; count: number } : { window: 0, count: 0 };
    const count = sentValue.window === window.id ? sentValue.count : 0;
    if (count >= riftSupplyRules().supplySends)
        throw new Error("no more supplies this window");
    const received = nk.storageRead([{ collection: RiftSupplyCollection, key: RiftSupplyKey, userId: to }]);
    if (received.length > 0 && (received[0].value as RiftSupplyValue).window === window.id)
        throw new Error("already supplied this window");
    const users = nk.usersGetId([userId]);
    const value: RiftSupplyValue = { window: window.id, from: userId, fromName: users.length > 0 ? users[0].displayName || users[0].username : "", item: item, at: nowUnixMs };
    nk.storageWrite([
        { collection: RiftSupplyCollection, key: RiftSupplyKey, userId: to, value: value as any, version: received.length > 0 ? received[0].version : "*", permissionRead: 1, permissionWrite: 0 },
        { collection: RiftSupplyCollection, key: RiftSupplySentKey, userId: userId, value: { window: window.id, count: count + 1 } as any, version: sent.length > 0 ? sent[0].version : "*", permissionRead: 1, permissionWrite: 0 }
    ]);
    const friendship = walletGrant(nk, userId, riftSupplyRules().supplySource, 1, nowUnixMs);
    return { window: window.id, sentThisWindow: count + 1, friendship: friendship };
}

function riftSupplyMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const window = riftWindowAt(RiftServerRules, nowUnixMs);
    if (!window)
        return { window: 0, supply: null };
    return { window: window.id, supply: riftSupplyOf(nk, userId, window.id) };
}

function riftSupplySendRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(riftSupplySend(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function riftSupplyMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(riftSupplyMine(nk, partyCaller(ctx), Date.now()));
}
