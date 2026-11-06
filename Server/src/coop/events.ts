interface SeasonalEventRule {
    id: string;
    month: number;
    day: number;
    shop: { cosmetic: string; price: number }[];
    clanEmblem: string;
}

interface SeasonalEventRules {
    windowDays: number;
    shopGraceDays: number;
    dailyTokenCap: number;
    goals: string[];
    goalTokens: number;
    goalPassPoints: number;
    allGoalsCrystals: number;
    clanGuestsPerMember: number;
    clanTarget: number;
    championTop: number;
    championFrame: string;
    boardSize: number;
    events: SeasonalEventRule[];
}

interface SeasonalWindow {
    id: string;
    event: SeasonalEventRule;
    firstDay: number;
    endDay: number;
}

interface EventValue {
    window: string;
    tokens: number;
    day: number;
    today: number;
    goals: string[];
    crystalsGiven: boolean;
    clanGuests: number;
    championChecked: string[];
}

interface EventClanValue {
    guests: number;
    tally: { [userId: string]: number };
    done: boolean;
}

declare const SeasonalEventServerRules: SeasonalEventRules;

const EventCollection = "seasonal_event";
const EventKey = "state";
const EventClanCollection = "seasonal_event_clan";
const EventBoardPrefix = "event_";
const EventWriteAttempts = 4;
const EventChampionKept = 8;

function eventCenterDay(year: number, month: number, day: number): number {
    return Math.floor(Date.UTC(year, month - 1, day) / PartyDayMs);
}

function eventWindowOf(event: SeasonalEventRule, year: number): SeasonalWindow {
    const first = eventCenterDay(year, event.month, event.day) - Math.floor(SeasonalEventServerRules.windowDays / 2);
    return { id: event.id + "_" + year, event: event, firstDay: first, endDay: first + SeasonalEventServerRules.windowDays };
}

function eventWindowsNear(day: number): SeasonalWindow[] {
    const year = new Date(day * PartyDayMs).getUTCFullYear();
    const windows: SeasonalWindow[] = [];
    for (const event of SeasonalEventServerRules.events)
        for (let shift = -1; shift <= 1; shift++)
            windows.push(eventWindowOf(event, year + shift));
    return windows;
}

function eventActiveWindow(day: number): SeasonalWindow | null {
    return eventWindowsNear(day).filter((window) => day >= window.firstDay && day < window.endDay)[0] || null;
}

function eventShopWindow(day: number): SeasonalWindow | null {
    return eventWindowsNear(day).filter((window) => day >= window.firstDay && day < window.endDay + SeasonalEventServerRules.shopGraceDays)[0] || null;
}

function eventLastEnded(day: number): SeasonalWindow | null {
    const ended = eventWindowsNear(day).filter((window) => window.endDay <= day);
    ended.sort((a, b) => b.endDay - a.endDay);
    return ended[0] || null;
}

function eventAcceptedWindow(id: string, day: number): SeasonalWindow | null {
    for (const near of [day - 1, day, day + 1]) {
        const window = eventActiveWindow(near);
        if (window && window.id === id)
            return window;
    }
    return null;
}

function eventEmpty(window: string): EventValue {
    return { window: window, tokens: 0, day: 0, today: 0, goals: [], crystalsGiven: false, clanGuests: 0, championChecked: [] };
}

function eventRead(nk: nkruntime.Nakama, userId: string): { value: EventValue; version: string } {
    const objects = nk.storageRead([{ collection: EventCollection, key: EventKey, userId: userId }]);
    if (objects.length === 0)
        return { value: eventEmpty(""), version: "*" };
    const value = objects[0].value as EventValue;
    value.championChecked = value.championChecked || [];
    return { value: value, version: objects[0].version };
}

function eventChange<T>(nk: nkruntime.Nakama, userId: string, change: (value: EventValue) => T): T {
    for (let attempt = 1; attempt <= EventWriteAttempts; attempt++) {
        const stored = eventRead(nk, userId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: EventCollection, key: EventKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === EventWriteAttempts)
                throw new Error("event is busy, try again");
        }
    }
    throw new Error("event is busy, try again");
}

function eventEnter(value: EventValue, window: string): void {
    if (value.window === window)
        return;
    const checked = value.championChecked;
    const fresh = eventEmpty(window);
    for (const key of Object.keys(fresh))
        (value as any)[key] = (fresh as any)[key];
    value.championChecked = checked;
}

function eventBoardId(window: string): string {
    return EventBoardPrefix + window;
}

function eventClanCredit(nk: nkruntime.Nakama, userId: string, window: SeasonalWindow, guests: number, nowUnixMs: number): void {
    if (!(guests > 0))
        return;
    let clanId: string;
    try {
        clanId = clanOf(nk, userId, nowUnixMs).id;
    } catch (error) {
        return;
    }
    const rules = SeasonalEventServerRules;
    const key = clanId + "_" + window.id;
    for (let attempt = 1; attempt <= EventWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: EventClanCollection, key: key, userId: PartySystemUser }]);
        const value: EventClanValue = objects.length > 0 ? objects[0].value as EventClanValue : { guests: 0, tally: {}, done: false };
        const already = value.tally[userId] || 0;
        const added = Math.max(0, Math.min(guests, rules.clanGuestsPerMember - already));
        if (added === 0)
            return;
        value.tally[userId] = already + added;
        value.guests += added;
        const reached = !value.done && value.guests >= rules.clanTarget;
        if (reached)
            value.done = true;
        try {
            nk.storageWrite([{ collection: EventClanCollection, key: key, userId: PartySystemUser, value: value as any, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0 }]);
        } catch (error) {
            if (attempt === EventWriteAttempts)
                throw new Error("clan is busy, try again");
            continue;
        }
        if (reached)
            clanCosmeticGive(nk, clanId, window.event.clanEmblem);
        return;
    }
}

function eventChampion(nk: nkruntime.Nakama, userId: string, day: number): string[] {
    const ended = eventLastEnded(day);
    if (!ended || eventRead(nk, userId).value.championChecked.indexOf(ended.id) >= 0)
        return [];
    const marked = eventChange(nk, userId, (value) => {
        if (value.championChecked.indexOf(ended.id) >= 0)
            return false;
        value.championChecked.push(ended.id);
        if (value.championChecked.length > EventChampionKept)
            value.championChecked.splice(0, value.championChecked.length - EventChampionKept);
        return true;
    });
    if (!marked)
        return [];
    let rank = 0;
    try {
        const list = nk.leaderboardRecordsList(eventBoardId(ended.id), [userId], 0);
        const own = (list.ownerRecords || []).filter((record) => record.ownerId === userId)[0];
        rank = own ? own.rank : 0;
    } catch (error) {
        rank = 0;
    }
    const frame = SeasonalEventServerRules.championFrame;
    return rank >= 1 && rank <= SeasonalEventServerRules.championTop && cosmeticGive(nk, userId, frame) ? [frame] : [];
}

function eventView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, extra: { [key: string]: any }): { [key: string]: any } {
    const day = walletDayOf(nowUnixMs);
    const active = eventActiveWindow(day);
    const shop = eventShopWindow(day);
    const value = eventRead(nk, userId).value;
    const current = shop && value.window === shop.id ? value : eventEmpty(shop ? shop.id : "");
    const view: { [key: string]: any } = {
        window: active ? active.id : "",
        shopWindow: shop ? shop.id : "",
        endsDay: active ? active.endDay : 0,
        shopEndsDay: shop ? shop.endDay + SeasonalEventServerRules.shopGraceDays : 0,
        tokens: current.tokens,
        today: current.day === day ? current.today : 0,
        dailyCap: SeasonalEventServerRules.dailyTokenCap,
        goals: current.goals,
        owned: cosmeticRead(nk, userId).value.owned.filter((id) => shop !== null && shop.event.shop.some((item) => item.cosmetic === id))
    };
    for (const key of Object.keys(extra))
        view[key] = extra[key];
    return view;
}

function eventProgress(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const rules = SeasonalEventServerRules;
    const day = walletDayOf(nowUnixMs);
    if (typeof request.window !== "string")
        throw new Error("bad request");
    const window = eventAcceptedWindow(request.window, day);
    if (!window)
        throw new Error("event is over");
    const tokens = request.tokens || 0;
    const guests = request.guests || 0;
    const score = request.score || 0;
    const goals: string[] = Array.isArray(request.goals) ? request.goals : [];
    for (const number of [tokens, guests, score])
        if (typeof number !== "number" || !(number >= 0) || number !== Math.floor(number))
            throw new Error("bad request");
    if (goals.some((goal) => typeof goal !== "string" || rules.goals.indexOf(goal) < 0))
        throw new Error("bad goal");
    const result = eventChange(nk, userId, (value) => {
        eventEnter(value, window.id);
        if (value.day !== day) {
            value.day = day;
            value.today = 0;
        }
        const accepted = Math.max(0, Math.min(tokens, rules.dailyTokenCap - value.today));
        value.today += accepted;
        value.tokens += accepted;
        const fresh = goals.filter((goal, index) => goals.indexOf(goal) === index && value.goals.indexOf(goal) < 0);
        for (const goal of fresh)
            value.goals.push(goal);
        value.tokens += fresh.length * rules.goalTokens;
        const allDone = !value.crystalsGiven && rules.goals.every((goal) => value.goals.indexOf(goal) >= 0);
        if (allDone)
            value.crystalsGiven = true;
        const clanGuests = Math.max(0, Math.min(guests, rules.clanGuestsPerMember - value.clanGuests));
        value.clanGuests += clanGuests;
        return { accepted: accepted, fresh: fresh, allDone: allDone, clanGuests: clanGuests };
    });
    if (result.fresh.length > 0 && rules.goalPassPoints > 0)
        seasonPassBonus(nk, userId, result.fresh.length * rules.goalPassPoints, nowUnixMs);
    let crystals = 0;
    if (result.allDone)
        crystals = walletGrant(nk, userId, "event_goals", rules.allGoalsCrystals, nowUnixMs);
    eventClanCredit(nk, userId, window, result.clanGuests, nowUnixMs);
    if (score > 0) {
        const id = eventBoardId(window.id);
        nk.leaderboardCreate(id, true, "descending", "best", null, null);
        const users = nk.usersGetId([userId]);
        const name = users.length > 0 ? users[0].displayName || users[0].username : "";
        nk.leaderboardRecordWrite(id, userId, name, Math.min(score, 1e9), 0, {});
    }
    return eventView(nk, userId, nowUnixMs, { accepted: result.accepted, freshGoals: result.fresh, freshCrystals: crystals });
}

function eventBuy(nk: nkruntime.Nakama, userId: string, cosmetic: string, nowUnixMs: number): { [key: string]: any } {
    const day = walletDayOf(nowUnixMs);
    const shop = eventShopWindow(day);
    const item = shop ? shop.event.shop.filter((entry) => entry.cosmetic === cosmetic)[0] : undefined;
    if (!shop || !item)
        throw new Error("not for sale");
    if (cosmeticRead(nk, userId).value.owned.indexOf(cosmetic) >= 0)
        throw new Error("already owned");
    eventChange(nk, userId, (value) => {
        if (value.window !== shop.id || value.tokens < item.price)
            throw new Error("not enough tokens");
        value.tokens -= item.price;
        return true;
    });
    try {
        if (!cosmeticGive(nk, userId, cosmetic))
            throw new Error("already owned");
    } catch (error) {
        eventChange(nk, userId, (value) => {
            if (value.window === shop.id)
                value.tokens += item.price;
            return true;
        });
        throw error;
    }
    return eventView(nk, userId, nowUnixMs, {});
}

function eventBoard(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const shop = eventShopWindow(walletDayOf(nowUnixMs));
    if (!shop)
        return { window: "", top: [], mine: null };
    const id = eventBoardId(shop.id);
    nk.leaderboardCreate(id, true, "descending", "best", null, null);
    const row = (record: nkruntime.LeaderboardRecord) => ({ rank: record.rank, userId: record.ownerId, name: record.username || "", score: record.score });
    const top = nk.leaderboardRecordsList(id, [], SeasonalEventServerRules.boardSize);
    const own = (nk.leaderboardRecordsList(id, [userId], 0).ownerRecords || []).filter((record) => record.ownerId === userId)[0];
    return { window: shop.id, top: (top.records || []).map(row), mine: own ? row(own) : null };
}

function eventMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const now = Date.now();
    const fresh = eventChampion(nk, userId, walletDayOf(now));
    return JSON.stringify(eventView(nk, userId, now, { fresh: fresh }));
}

function eventProgressRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(eventProgress(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function eventBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    if (typeof request.cosmetic !== "string")
        throw new Error("bad request");
    return JSON.stringify(eventBuy(nk, partyCaller(ctx), request.cosmetic, Date.now()));
}

function eventBoardRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(eventBoard(nk, partyCaller(ctx), Date.now()));
}
