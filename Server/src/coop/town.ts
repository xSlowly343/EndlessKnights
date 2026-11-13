interface TownRules {
    museum: string[];
    streetSeconds: number;
    streetShown: number;
    giftsPerDay: number;
    giftMaxAmount: number;
    giftInbox: number;
    giftItems: string[];
}

interface TownGift {
    id: string;
    from: string;
    fromName: string;
    item: string;
    amount: number;
    at: number;
}

interface TownSent {
    day: number;
    sent: number;
}

declare const TownServerRules: TownRules;

const TownCollection = "town";
const TownGiftCollection = "town_gifts";
const TownSentKey = "sent";
const TownGuildsKey = "guilds";
const TownWriteAttempts = 4;

function townGuilds(nk: nkruntime.Nakama): string[] {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownGuildsKey, userId: PartySystemUser }]);
    if (objects.length === 0)
        return [];
    const built = (objects[0].value as { built?: string[] }).built;
    return Array.isArray(built) ? built.slice() : [];
}

function townSentToday(nk: nkruntime.Nakama, userId: string, day: number): { value: TownSent; version: string } {
    const objects = nk.storageRead([{ collection: TownCollection, key: TownSentKey, userId: userId }]);
    if (objects.length === 0)
        return { value: { day: day, sent: 0 }, version: "*" };
    const value = objects[0].value as TownSent;
    return { value: value.day === day ? value : { day: day, sent: 0 }, version: objects[0].version };
}

function townInbox(nk: nkruntime.Nakama, userId: string): TownGift[] {
    const gifts: TownGift[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, TownGiftCollection, 100, cursor);
        for (const object of page.objects || [])
            gifts.push(object.value as TownGift);
        cursor = page.cursor || undefined;
    } while (cursor);
    gifts.sort((a, b) => a.at - b.at || (a.id < b.id ? -1 : 1));
    return gifts;
}

function townView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, extra: { [key: string]: any }): { [key: string]: any } {
    const rules = TownServerRules;
    const sent = townSentToday(nk, userId, walletDayOf(nowUnixMs)).value.sent;
    const view: { [key: string]: any } = {
        guilds: townGuilds(nk),
        sentToday: sent,
        giftsPerDay: rules.giftsPerDay,
        inbox: townInbox(nk, userId),
        gatherLeft: inflowLeft(nk, userId, nowUnixMs)
    };
    for (const key in extra)
        view[key] = extra[key];
    return view;
}

function townMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return townView(nk, userId, nowUnixMs, {});
}

function townGiftSend(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const rules = TownServerRules;
    const to = request && request.to;
    const item = request && request.item;
    const amount = request && request.amount;
    if (typeof to !== "string" || to.length === 0 || typeof item !== "string" || typeof amount !== "number" || amount !== Math.floor(amount))
        throw new Error("bad request");
    if (rules.giftItems.indexOf(item) < 0)
        throw new Error("not giftable");
    if (amount < 1 || amount > rules.giftMaxAmount)
        throw new Error("bad amount");
    if (to === userId)
        throw new Error("not to yourself");
    const users = nk.usersGetId([to, userId]);
    const recipient = users.filter((user) => user.userId === to)[0];
    if (!recipient)
        throw new Error("no such player");
    if (!circleClose(nk, userId, to, nowUnixMs))
        throw new Error("gifts go only to friends, clan and party");
    if (townInbox(nk, to).length >= rules.giftInbox)
        throw new Error("inbox is full");
    const receipt = inflowSpend(nk, userId, item, amount, nowUnixMs);
    const count = inflowGuard(nk, userId, receipt, nowUnixMs, () => townCountGift(nk, userId, nowUnixMs));
    const sender = users.filter((user) => user.userId === userId)[0];
    const gift: TownGift = {
        id: userId + "_" + nowUnixMs + "_" + count,
        from: userId,
        fromName: sender ? sender.displayName || sender.username : "",
        item: item,
        amount: amount,
        at: nowUnixMs
    };
    nk.storageWrite([{ collection: TownGiftCollection, key: gift.id, userId: to, value: gift as any, version: "*", permissionRead: 1, permissionWrite: 0 }]);
    return townView(nk, userId, nowUnixMs, { sent: gift });
}

function townCountGift(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): number {
    const day = walletDayOf(nowUnixMs);
    for (let attempt = 1; attempt <= TownWriteAttempts; attempt++) {
        const stored = townSentToday(nk, userId, day);
        if (stored.value.sent >= TownServerRules.giftsPerDay)
            throw new Error("daily limit");
        stored.value.sent += 1;
        try {
            nk.storageWrite([{ collection: TownCollection, key: TownSentKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return stored.value.sent;
        } catch (error) {
            continue;
        }
    }
    throw new Error("town is busy, try again");
}

function townGiftClaim(nk: nkruntime.Nakama, userId: string, ack: unknown, nowUnixMs: number): { [key: string]: any } {
    const taken = Array.isArray(ack) ? ack.filter((id) => typeof id === "string" && id.length > 0 && id.length <= 128).slice(0, TownServerRules.giftInbox) : [];
    if (taken.length > 0) {
        const received: { [item: string]: number } = {};
        for (const gift of townInbox(nk, userId).filter((entry) => taken.indexOf(entry.id) >= 0))
            received[gift.item] = (received[gift.item] || 0) + gift.amount;
        nk.storageDelete(taken.map((id) => ({ collection: TownGiftCollection, key: id as string, userId: userId })));
        inflowGrant(nk, userId, received, nowUnixMs);
    }
    return townView(nk, userId, nowUnixMs, { claimed: townInbox(nk, userId) });
}

function townMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townMine(nk, partyCaller(ctx), Date.now()));
}

function townGiftSendRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townGiftSend(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function townGiftClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townGiftClaim(nk, partyCaller(ctx), partyRequest(payload).ack, Date.now()));
}
