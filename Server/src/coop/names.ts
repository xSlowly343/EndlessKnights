interface SocialChannelRule {
    id: string;
    history: number;
    perMinute: number;
    cooldownMs: number;
}

interface SocialRules {
    nameMin: number;
    nameMax: number;
    tickets: { id: string; price: number }[];
    roles: { id: string; muteHours: number; grantsRoles: boolean }[];
    banned: string[];
    chatMax: number;
    chatDays: number;
    pollSeconds: number;
    removedKept: number;
    channels: SocialChannelRule[];
    reportReasons: string[];
    autoMuteReporters: number;
    autoMuteDays: number;
    autoMuteHours: number;
    maxMuteHours: number;
}

declare const SocialServerRules: SocialRules;

const SocialDigits: { [symbol: string]: string } = { "0": "o", "1": "i", "3": "e", "4": "a", "5": "s", "7": "t", "@": "a", "$": "s", "ё": "е" };
const SocialTwins: { [symbol: string]: string } = { a: "а", b: "в", c: "с", e: "е", h: "н", k: "к", m: "м", o: "о", p: "р", t: "т", x: "х", y: "у" };
const SocialLetter = /[a-z\u00df-\u024f\u0400-\u04ff]/;
const SocialNameShape = /^[A-Za-z0-9\u00c0-\u024f\u0400-\u04ff]+(?:[ '\-][A-Za-z0-9\u00c0-\u024f\u0400-\u04ff]+)*$/;

function socialFold(text: string, twins: boolean): string {
    const lower = text.toLowerCase();
    let folded = "";
    for (let i = 0; i < lower.length; i++) {
        let symbol = lower.charAt(i);
        symbol = SocialDigits[symbol] || symbol;
        if (twins)
            symbol = SocialTwins[symbol] || symbol;
        if (SocialLetter.test(symbol))
            folded += symbol;
    }
    return folded;
}

function socialBanned(text: string): boolean {
    const plain = socialFold(text, false);
    const twin = socialFold(text, true);
    for (const word of SocialServerRules.banned)
        if (plain.indexOf(word) >= 0 || twin.indexOf(word) >= 0)
            return true;
    return false;
}

function socialTidy(text: string): string {
    return text.replace(/[\u0000-\u001f\u007f\u200b-\u200f\u2028-\u202e\ufeff]/g, " ").replace(/\s+/g, " ").replace(/^ | $/g, "");
}

function socialName(value: any): string {
    if (typeof value !== "string")
        throw new Error("bad request");
    const name = socialTidy(value);
    if (name === "")
        return "";
    if (name.length < SocialServerRules.nameMin || name.length > SocialServerRules.nameMax || !SocialNameShape.test(name))
        throw new Error("bad name");
    if (socialBanned(name))
        throw new Error("name not allowed");
    return name;
}

function socialRename(nk: nkruntime.Nakama, userId: string, meta: { name?: string; namedAt?: number }, value: any, ticket: string, nowUnixMs: number): boolean {
    if (value === undefined || value === null)
        return false;
    const name = socialName(value);
    if (name === (meta.name || ""))
        return false;
    meta.name = name;
    if (name === "")
        return false;
    const paid = !!meta.namedAt;
    if (paid)
        ticketSpend(nk, userId, ticket);
    meta.namedAt = nowUnixMs;
    return paid;
}

function socialMask(text: string): string {
    const words = text.split(" ");
    for (let i = 0; i < words.length; i++)
        if (socialBanned(words[i]))
            words[i] = "***";
    return words.join(" ");
}


const TicketCollection = "tickets";
const TicketKey = "current";
const TicketPartyRename = "party_rename";
const TicketClanRename = "clan_rename";
const TicketWriteAttempts = 5;

function ticketRule(id: any): { id: string; price: number } {
    for (const ticket of SocialServerRules.tickets)
        if (ticket.id === id)
            return ticket;
    throw new Error("bad request");
}

function ticketChange(nk: nkruntime.Nakama, userId: string, change: (owned: { [id: string]: number }) => void): { [id: string]: number } {
    for (let attempt = 1; attempt <= TicketWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: TicketCollection, key: TicketKey, userId: userId }]);
        const owned: { [id: string]: number } = objects.length > 0 ? objects[0].value.owned || {} : {};
        change(owned);
        try {
            nk.storageWrite([{ collection: TicketCollection, key: TicketKey, userId: userId, value: { owned: owned }, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 1, permissionWrite: 0 }]);
            return owned;
        } catch (error) {
            continue;
        }
    }
    throw new Error("tickets are busy, try again");
}

function ticketView(owned: { [id: string]: number }): { [key: string]: any } {
    return { tickets: SocialServerRules.tickets.map((ticket) => ({ id: ticket.id, price: ticket.price, owned: owned[ticket.id] || 0 })) };
}

function ticketGive(nk: nkruntime.Nakama, userId: string, id: string): { [id: string]: number } {
    return ticketChange(nk, userId, (owned) => { owned[id] = (owned[id] || 0) + 1; });
}

function ticketSpend(nk: nkruntime.Nakama, userId: string, id: string): void {
    ticketChange(nk, userId, (owned) => {
        if (!(owned[id] > 0))
            throw new Error("no ticket");
        owned[id]--;
    });
}

function ticketBuy(nk: nkruntime.Nakama, userId: string, id: any, nowUnixMs: number): { [key: string]: any } {
    const ticket = ticketRule(id);
    const reason = "ticket_" + ticket.id;
    walletSpend(nk, userId, "crystals", ticket.price, reason, nowUnixMs);
    try {
        return ticketView(ticketGive(nk, userId, ticket.id));
    } catch (error) {
        walletRefund(nk, userId, "crystals", ticket.price, reason, nowUnixMs);
        throw error;
    }
}

function ticketMineRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(ticketView(ticketChange(nk, partyCaller(ctx), () => undefined)));
}

function ticketBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(ticketBuy(nk, partyCaller(ctx), partyRequest(payload).id, Date.now()));
}
