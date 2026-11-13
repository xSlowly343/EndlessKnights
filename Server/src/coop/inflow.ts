interface InflowRules {
    perDay: { prefix: string; amount: number }[];
    other: number;
}

interface InflowValue {
    day: number;
    used: { [group: string]: number };
    held: { [item: string]: number };
}

declare const InflowServerRules: InflowRules;

const InflowCollection = "inflow";
const InflowKey = "ledger";
const InflowOther = "*";
const InflowWriteAttempts = 4;

function inflowGroup(item: string): string {
    const entry = InflowServerRules.perDay.filter((rule) => item.indexOf(rule.prefix) === 0)[0];
    return entry ? entry.prefix : InflowOther;
}

function inflowLimit(group: string): number {
    const entry = InflowServerRules.perDay.filter((rule) => rule.prefix === group)[0];
    return entry ? entry.amount : InflowServerRules.other;
}

function inflowRead(nk: nkruntime.Nakama, userId: string, day: number): { value: InflowValue; version: string } {
    const objects = nk.storageRead([{ collection: InflowCollection, key: InflowKey, userId: userId }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    return {
        value: {
            day: day,
            used: stored.day === day ? townBuildCounts(stored.used) : {},
            held: townBuildCounts(stored.held)
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function inflowChange<T>(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, change: (value: InflowValue) => T): T {
    for (let attempt = 1; attempt <= InflowWriteAttempts; attempt++) {
        const stored = inflowRead(nk, userId, walletDayOf(nowUnixMs));
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: InflowCollection, key: InflowKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the ledger is busy, try again");
}

function inflowRoom(value: InflowValue, item: string): number {
    const group = inflowGroup(item);
    return (value.held[item] || 0) + Math.max(0, inflowLimit(group) - (value.used[group] || 0));
}

interface InflowReceipt {
    day: number;
    items: { [item: string]: { held: number; fresh: number } };
}

function inflowUse(value: InflowValue, item: string, amount: number, receipt: InflowReceipt): void {
    const held = Math.min(value.held[item] || 0, amount);
    if (held > 0)
        value.held[item] -= held;
    if (value.held[item] === 0)
        delete value.held[item];
    const group = inflowGroup(item);
    if (amount > held)
        value.used[group] = (value.used[group] || 0) + amount - held;
    const line = receipt.items[item] || { held: 0, fresh: 0 };
    line.held += held;
    line.fresh += amount - held;
    receipt.items[item] = line;
}

function inflowTaken(receipt: InflowReceipt): { [item: string]: number } {
    const taken: { [item: string]: number } = {};
    for (const item of Object.keys(receipt.items))
        taken[item] = receipt.items[item].held + receipt.items[item].fresh;
    return taken;
}

function inflowTake(nk: nkruntime.Nakama, userId: string, items: { [item: string]: number }, nowUnixMs: number): InflowReceipt {
    return inflowChange(nk, userId, nowUnixMs, (value) => {
        const receipt: InflowReceipt = { day: value.day, items: {} };
        for (const item of Object.keys(items)) {
            const amount = Math.min(items[item], inflowRoom(value, item));
            if (amount > 0)
                inflowUse(value, item, amount, receipt);
        }
        return receipt;
    });
}

function inflowSpend(nk: nkruntime.Nakama, userId: string, item: string, amount: number, nowUnixMs: number): InflowReceipt {
    return inflowChange(nk, userId, nowUnixMs, (value) => {
        if (inflowRoom(value, item) < amount)
            throw new Error("more than you could have gathered today");
        const receipt: InflowReceipt = { day: value.day, items: {} };
        inflowUse(value, item, amount, receipt);
        return receipt;
    });
}

function inflowGrant(nk: nkruntime.Nakama, userId: string, items: { [item: string]: number }, nowUnixMs: number): void {
    const known = Object.keys(items).filter((item) => items[item] > 0);
    if (known.length === 0)
        return;
    inflowChange(nk, userId, nowUnixMs, (value) => {
        for (const item of known)
            value.held[item] = (value.held[item] || 0) + items[item];
    });
}

function inflowRevert(nk: nkruntime.Nakama, userId: string, receipt: InflowReceipt, nowUnixMs: number): void {
    try {
        inflowChange(nk, userId, nowUnixMs, (value) => {
            for (const item of Object.keys(receipt.items)) {
                const line = receipt.items[item];
                if (line.held > 0)
                    value.held[item] = (value.held[item] || 0) + line.held;
                const group = inflowGroup(item);
                if (line.fresh > 0 && value.day === receipt.day)
                    value.used[group] = Math.max(0, (value.used[group] || 0) - line.fresh);
            }
        });
    } catch (error) {
        return;
    }
}

function inflowGuard<T>(nk: nkruntime.Nakama, userId: string, receipt: InflowReceipt, nowUnixMs: number, work: () => T): T {
    try {
        return work();
    } catch (error) {
        inflowRevert(nk, userId, receipt, nowUnixMs);
        throw error;
    }
}

function inflowLeft(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [group: string]: number } {
    const value = inflowRead(nk, userId, walletDayOf(nowUnixMs)).value;
    const left: { [group: string]: number } = {};
    for (const rule of InflowServerRules.perDay)
        left[rule.prefix] = Math.max(0, rule.amount - (value.used[rule.prefix] || 0));
    left[InflowOther] = Math.max(0, InflowServerRules.other - (value.used[InflowOther] || 0));
    return left;
}
