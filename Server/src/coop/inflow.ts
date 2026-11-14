interface InflowRules {
    perDay: { prefix: string; amount: number }[];
    other: number;
}

interface InflowValue {
    based: boolean;
    bag: { [item: string]: number };
    spent: { [item: string]: number };
    held: { [item: string]: number };
}

interface InflowReceipt {
    items: { [item: string]: { held: number; base: number } };
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

function inflowRead(nk: nkruntime.Nakama, userId: string): { value: InflowValue; version: string } {
    const objects = nk.storageRead([{ collection: InflowCollection, key: InflowKey, userId: userId }]);
    const stored = (objects.length > 0 ? objects[0].value : {}) as { [key: string]: any };
    return {
        value: {
            based: stored.based === true,
            bag: townBuildCounts(stored.bag),
            spent: townBuildCounts(stored.spent),
            held: townBuildCounts(stored.held)
        },
        version: objects.length > 0 ? objects[0].version : "*"
    };
}

function inflowChange<T>(nk: nkruntime.Nakama, userId: string, change: (value: InflowValue) => T): T {
    for (let attempt = 1; attempt <= InflowWriteAttempts; attempt++) {
        const stored = inflowRead(nk, userId);
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

function inflowFromBag(value: InflowValue, item: string): number {
    return Math.max(0, (value.bag[item] || 0) - (value.spent[item] || 0));
}

function inflowRoom(value: InflowValue, item: string): number {
    return (value.held[item] || 0) + inflowFromBag(value, item);
}

function inflowUse(value: InflowValue, item: string, amount: number, receipt: InflowReceipt): void {
    const held = Math.min(value.held[item] || 0, amount);
    if (held > 0)
        value.held[item] -= held;
    if (value.held[item] === 0)
        delete value.held[item];
    if (amount > held)
        value.spent[item] = (value.spent[item] || 0) + amount - held;
    const line = receipt.items[item] || { held: 0, base: 0 };
    line.held += held;
    line.base += amount - held;
    receipt.items[item] = line;
}

function inflowTaken(receipt: InflowReceipt): { [item: string]: number } {
    const taken: { [item: string]: number } = {};
    for (const item of Object.keys(receipt.items))
        taken[item] = receipt.items[item].held + receipt.items[item].base;
    return taken;
}

function inflowNeedsBase(value: InflowValue): void {
    if (!value.based)
        throw new Error("the cloud save is not uploaded yet");
}

function inflowTake(nk: nkruntime.Nakama, userId: string, items: { [item: string]: number }): InflowReceipt {
    return inflowChange(nk, userId, (value) => {
        inflowNeedsBase(value);
        const receipt: InflowReceipt = { items: {} };
        for (const item of Object.keys(items)) {
            const amount = Math.min(items[item], inflowRoom(value, item));
            if (amount > 0)
                inflowUse(value, item, amount, receipt);
        }
        return receipt;
    });
}

function inflowSpend(nk: nkruntime.Nakama, userId: string, item: string, amount: number): InflowReceipt {
    return inflowChange(nk, userId, (value) => {
        inflowNeedsBase(value);
        if (inflowRoom(value, item) < amount)
            throw new Error("not in your bag by the last cloud save");
        const receipt: InflowReceipt = { items: {} };
        inflowUse(value, item, amount, receipt);
        return receipt;
    });
}

function inflowSpendAll(nk: nkruntime.Nakama, userId: string, items: { [item: string]: number }): InflowReceipt {
    return inflowChange(nk, userId, (value) => {
        inflowNeedsBase(value);
        for (const item of Object.keys(items))
            if (inflowRoom(value, item) < items[item])
                throw new Error("not in your bag by the last cloud save");
        const receipt: InflowReceipt = { items: {} };
        for (const item of Object.keys(items))
            inflowUse(value, item, items[item], receipt);
        return receipt;
    });
}

function inflowGrant(nk: nkruntime.Nakama, userId: string, items: { [item: string]: number }): void {
    const known = Object.keys(items).filter((item) => items[item] > 0);
    if (known.length === 0)
        return;
    inflowChange(nk, userId, (value) => {
        for (const item of known)
            value.held[item] = (value.held[item] || 0) + items[item];
    });
}

function inflowRevert(nk: nkruntime.Nakama, userId: string, receipt: InflowReceipt): void {
    try {
        inflowChange(nk, userId, (value) => {
            for (const item of Object.keys(receipt.items)) {
                const line = receipt.items[item];
                if (line.held > 0)
                    value.held[item] = (value.held[item] || 0) + line.held;
                if (line.base > 0)
                    value.spent[item] = Math.max(0, (value.spent[item] || 0) - line.base);
            }
        });
    } catch (error) {
        return;
    }
}

function inflowGuard<T>(nk: nkruntime.Nakama, userId: string, receipt: InflowReceipt, work: () => T): T {
    try {
        return work();
    } catch (error) {
        inflowRevert(nk, userId, receipt);
        throw error;
    }
}

function inflowBaseline(nk: nkruntime.Nakama, userId: string, bag: { [item: string]: number }): void {
    inflowChange(nk, userId, (value) => {
        value.based = true;
        value.bag = townBuildCounts(bag);
        value.spent = {};
        value.held = {};
    });
}

function inflowAvailable(nk: nkruntime.Nakama, userId: string, items: string[]): { [item: string]: number } {
    const value = inflowRead(nk, userId).value;
    const available: { [item: string]: number } = {};
    for (const item of items)
        available[item] = value.based ? inflowRoom(value, item) : 0;
    return available;
}
