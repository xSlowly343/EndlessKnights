interface WalletSource {
    id: string;
    currency: string;
    amount: number;
    dailyCap: number;
    perFriendDailyCap: number;
}

interface WalletRules {
    currencies: string[];
    dayUtcOffsetMinutes: number;
    ledgerSize: number;
    sources: WalletSource[];
}

interface WalletEntry {
    at: number;
    source: string;
    currency: string;
    amount: number;
}

interface WalletValue {
    balances: { [currency: string]: number };
    day: number;
    today: { [source: string]: number };
    ledger: WalletEntry[];
}

declare const WalletServerRules: WalletRules;

const WalletCollection = "wallet";
const WalletKey = "coop";
const WalletWriteAttempts = 3;
const WalletMinuteMs = 60000;

function walletDayOf(unixMs: number): number {
    return Math.floor((unixMs + WalletServerRules.dayUtcOffsetMinutes * WalletMinuteMs) / PartyDayMs);
}

function walletEmpty(): WalletValue {
    const balances: { [currency: string]: number } = {};
    for (const currency of WalletServerRules.currencies)
        balances[currency] = 0;
    return { balances: balances, day: 0, today: {}, ledger: [] };
}

function walletSource(id: string): WalletSource {
    for (const source of WalletServerRules.sources)
        if (source.id === id)
            return source;
    throw new Error("unknown source " + id);
}

function walletRead(nk: nkruntime.Nakama, userId: string): { value: WalletValue; version: string } {
    const objects = nk.storageRead([{ collection: WalletCollection, key: WalletKey, userId: userId }]);
    if (objects.length === 0)
        return { value: walletEmpty(), version: "*" };
    const value = JSON.parse(JSON.stringify(objects[0].value)) as WalletValue;
    for (const currency of WalletServerRules.currencies)
        if (typeof value.balances[currency] !== "number")
            value.balances[currency] = 0;
    return { value: value, version: objects[0].version };
}

function walletChange<T>(nk: nkruntime.Nakama, userId: string, change: (value: WalletValue) => T): T {
    for (let attempt = 1; attempt <= WalletWriteAttempts; attempt++) {
        const stored = walletRead(nk, userId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: WalletCollection, key: WalletKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            if (attempt === WalletWriteAttempts)
                throw error;
        }
    }
    throw new Error("wallet is busy, try again");
}

function walletRollDay(value: WalletValue, nowUnixMs: number): void {
    const day = walletDayOf(nowUnixMs);
    if (value.day === day)
        return;
    value.day = day;
    value.today = {};
}

function walletNote(value: WalletValue, entry: WalletEntry): void {
    const last = value.ledger.length > 0 ? value.ledger[0] : null;
    if (last && last.source === entry.source && walletDayOf(last.at) === walletDayOf(entry.at))
        last.amount += entry.amount;
    else
        value.ledger.unshift(entry);
    if (value.ledger.length > WalletServerRules.ledgerSize)
        value.ledger.length = WalletServerRules.ledgerSize;
}

function walletGrant(nk: nkruntime.Nakama, userId: string, sourceId: string, times: number, nowUnixMs: number): number {
    const source = walletSource(sourceId);
    if (!(times > 0))
        return 0;
    return walletChange(nk, userId, (value) => {
        walletRollDay(value, nowUnixMs);
        const already = value.today[source.id] || 0;
        const amount = Math.max(0, Math.min(Math.floor(times) * source.amount, source.dailyCap - already));
        if (amount === 0)
            return 0;
        value.today[source.id] = already + amount;
        value.balances[source.currency] += amount;
        walletNote(value, { at: nowUnixMs, source: source.id, currency: source.currency, amount: amount });
        return amount;
    });
}

function walletSpend(nk: nkruntime.Nakama, userId: string, currency: string, amount: number, reason: string, nowUnixMs: number): number {
    if (WalletServerRules.currencies.indexOf(currency) < 0 || !(amount > 0) || amount !== Math.floor(amount))
        throw new Error("bad request");
    return walletChange(nk, userId, (value) => {
        if (value.balances[currency] < amount)
            throw new Error("not enough " + currency);
        value.balances[currency] -= amount;
        walletNote(value, { at: nowUnixMs, source: reason, currency: currency, amount: -amount });
        return value.balances[currency];
    });
}

function walletView(value: WalletValue, nowUnixMs: number): { [key: string]: any } {
    const today = walletDayOf(nowUnixMs) === value.day ? value.today : {};
    return {
        balances: value.balances,
        today: WalletServerRules.sources.map((source) => ({ source: source.id, currency: source.currency, earned: today[source.id] || 0, cap: source.dailyCap })),
        ledger: value.ledger
    };
}

function walletGrantBosses(nk: nkruntime.Nakama, logger: nkruntime.Logger, bosses: { [userId: string]: number }, nowUnixMs: number): void {
    for (const userId of Object.keys(bosses)) {
        try {
            walletGrant(nk, userId, "boss_together", bosses[userId], nowUnixMs);
        } catch (error) {
            logger.warn("friendship for bosses was not granted to %s: %s", userId, String(error));
        }
    }
}

function coopWalletRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const now = Date.now();
    return JSON.stringify(walletView(walletRead(nk, partyCaller(ctx)).value, now));
}
