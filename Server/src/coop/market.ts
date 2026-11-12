interface MarketRules {
    currency: string;
    items: string[];
    maxLots: number;
    maxAmount: number;
    minPrice: number;
    maxPrice: number;
    days: number;
    fee: number;
    shown: number;
    inbox: number;
}

interface MarketLot {
    id: string;
    seller: string;
    sellerName: string;
    item: string;
    amount: number;
    price: number;
    at: number;
    until: number;
}

interface MarketDelivery {
    id: string;
    item: string;
    amount: number;
    reason: string;
    at: number;
}

declare const MarketServerRules: MarketRules;

const MarketLotCollection = "market_lots";
const MarketInboxCollection = "market_inbox";
const MarketBuyReason = "market_buy";
const MarketSaleReason = "market_sale";
const MarketBoughtReason = "bought";
const MarketReturnedReason = "returned";
const MarketNoncePattern = /^[a-z0-9]{1,32}$/;
const MarketPageSize = 100;

function marketLots(nk: nkruntime.Nakama): nkruntime.StorageObject[] {
    const lots: nkruntime.StorageObject[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(PartySystemUser, MarketLotCollection, MarketPageSize, cursor);
        for (const object of page.objects || [])
            lots.push(object);
        cursor = page.cursor || undefined;
    } while (cursor);
    return lots;
}

function marketInbox(nk: nkruntime.Nakama, userId: string): MarketDelivery[] {
    const deliveries: MarketDelivery[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, MarketInboxCollection, MarketPageSize, cursor);
        for (const object of page.objects || [])
            deliveries.push(object.value as MarketDelivery);
        cursor = page.cursor || undefined;
    } while (cursor);
    deliveries.sort((a, b) => a.at - b.at || (a.id < b.id ? -1 : 1));
    return deliveries;
}

function marketFee(total: number): number {
    return Math.min(total - 1, Math.ceil(total * MarketServerRules.fee));
}

function marketView(nk: nkruntime.Nakama, userId: string, item: unknown, nowUnixMs: number, extra: { [key: string]: any }): { [key: string]: any } {
    const rules = MarketServerRules;
    const lots: MarketLot[] = [];
    const mine: MarketLot[] = [];
    for (const object of marketLots(nk)) {
        const lot = object.value as MarketLot;
        if (lot.seller === userId)
            mine.push(lot);
        else if (lot.until > nowUnixMs && (typeof item !== "string" || item.length === 0 || lot.item === item))
            lots.push(lot);
    }
    lots.sort((a, b) => a.price - b.price || a.at - b.at || (a.id < b.id ? -1 : 1));
    mine.sort((a, b) => a.at - b.at || (a.id < b.id ? -1 : 1));
    const view: { [key: string]: any } = {
        now: nowUnixMs,
        currency: rules.currency,
        balance: walletRead(nk, userId).value.balances[rules.currency] || 0,
        fee: rules.fee,
        maxLots: rules.maxLots,
        lots: lots.slice(0, rules.shown),
        mine: mine,
        inbox: marketInbox(nk, userId)
    };
    for (const key in extra)
        view[key] = extra[key];
    return view;
}

function marketDeliver(nk: nkruntime.Nakama, userId: string, lot: MarketLot, reason: string, nowUnixMs: number): MarketDelivery {
    const delivery: MarketDelivery = { id: lot.id + "_" + lot.at, item: lot.item, amount: lot.amount, reason: reason, at: nowUnixMs };
    nk.storageWrite([{ collection: MarketInboxCollection, key: delivery.id, userId: userId, value: delivery as any, permissionRead: 1, permissionWrite: 0 }]);
    return delivery;
}

function marketTake(nk: nkruntime.Nakama, lotId: unknown): { lot: MarketLot; version: string } {
    if (typeof lotId !== "string" || lotId.length === 0 || lotId.length > 128)
        throw new Error("bad request");
    const objects = nk.storageRead([{ collection: MarketLotCollection, key: lotId, userId: PartySystemUser }]);
    if (objects.length === 0)
        throw new Error("lot is gone");
    return { lot: objects[0].value as MarketLot, version: objects[0].version };
}

function marketRemove(nk: nkruntime.Nakama, lotId: string, version: string): boolean {
    try {
        nk.storageDelete([{ collection: MarketLotCollection, key: lotId, userId: PartySystemUser, version: version }]);
        return true;
    } catch (error) {
        return false;
    }
}

function marketSell(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const rules = MarketServerRules;
    const item = request && request.item;
    const amount = request && request.amount;
    const price = request && request.price;
    const nonce = request && request.nonce;
    if (typeof item !== "string" || typeof amount !== "number" || amount !== Math.floor(amount) || typeof price !== "number" || price !== Math.floor(price)
        || typeof nonce !== "string" || !MarketNoncePattern.test(nonce))
        throw new Error("bad request");
    if (rules.items.indexOf(item) < 0)
        throw new Error("not for sale");
    if (amount < 1 || amount > rules.maxAmount)
        throw new Error("bad amount");
    if (price < rules.minPrice || price > rules.maxPrice)
        throw new Error("bad price");
    const id = userId + "_" + nonce;
    const existing = nk.storageRead([{ collection: MarketLotCollection, key: id, userId: PartySystemUser }]);
    if (existing.length > 0)
        return marketView(nk, userId, undefined, nowUnixMs, { listed: existing[0].value });
    let own = 0;
    for (const object of marketLots(nk))
        if ((object.value as MarketLot).seller === userId)
            own++;
    if (own >= rules.maxLots)
        throw new Error("too many lots");
    const seller = nk.usersGetId([userId])[0];
    const lot: MarketLot = {
        id: id,
        seller: userId,
        sellerName: seller ? seller.displayName || seller.username : "",
        item: item,
        amount: amount,
        price: price,
        at: nowUnixMs,
        until: nowUnixMs + rules.days * PartyDayMs
    };
    nk.storageWrite([{ collection: MarketLotCollection, key: id, userId: PartySystemUser, value: lot as any, version: "*", permissionRead: 2, permissionWrite: 0 }]);
    return marketView(nk, userId, undefined, nowUnixMs, { listed: lot });
}

function marketBuy(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const rules = MarketServerRules;
    const found = marketTake(nk, request && request.lot);
    const lot = found.lot;
    if (lot.seller === userId)
        throw new Error("your own lot");
    if (lot.until <= nowUnixMs)
        throw new Error("lot is gone");
    if (marketInbox(nk, userId).length >= rules.inbox)
        throw new Error("inbox is full");
    const total = lot.price * lot.amount;
    walletSpend(nk, userId, rules.currency, total, MarketBuyReason, nowUnixMs);
    if (!marketRemove(nk, lot.id, found.version)) {
        walletRefund(nk, userId, rules.currency, total, MarketBuyReason, nowUnixMs);
        throw new Error("lot is gone");
    }
    const bought = marketDeliver(nk, userId, lot, MarketBoughtReason, nowUnixMs);
    walletRefund(nk, lot.seller, rules.currency, total - marketFee(total), MarketSaleReason, nowUnixMs);
    return marketView(nk, userId, undefined, nowUnixMs, { bought: bought, paid: total });
}

function marketCancel(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const found = marketTake(nk, request && request.lot);
    if (found.lot.seller !== userId)
        throw new Error("not your lot");
    if (!marketRemove(nk, found.lot.id, found.version))
        throw new Error("lot is gone");
    const returned = marketDeliver(nk, userId, found.lot, MarketReturnedReason, nowUnixMs);
    return marketView(nk, userId, undefined, nowUnixMs, { returned: returned });
}

function marketClaim(nk: nkruntime.Nakama, userId: string, ack: unknown, nowUnixMs: number): { [key: string]: any } {
    const taken = Array.isArray(ack) ? ack.filter((id) => typeof id === "string" && id.length > 0 && id.length <= 160).slice(0, MarketServerRules.inbox) : [];
    if (taken.length > 0)
        nk.storageDelete(taken.map((id) => ({ collection: MarketInboxCollection, key: id as string, userId: userId })));
    return marketView(nk, userId, undefined, nowUnixMs, {});
}

function marketListRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(marketView(nk, partyCaller(ctx), partyRequest(payload).item, Date.now(), {}));
}

function marketSellRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(marketSell(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function marketBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(marketBuy(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function marketCancelRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(marketCancel(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function marketClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(marketClaim(nk, partyCaller(ctx), partyRequest(payload).ack, Date.now()));
}
