interface GiftRules {
    dailyLimit: number;
    perFriendDaily: number;
    receiveDailyLimit: number;
    cost: number;
    giftable: string[];
}

interface GiftCounters {
    day: number;
    sent: number;
    received: number;
    perFriend: { [userId: string]: number };
}

declare const GiftServerRules: GiftRules;

const GiftCollection = "gift";
const GiftCountersKey = "today";
const GiftWriteAttempts = 3;
const GiftRefused = "this player does not take gifts from you";

function giftEmpty(day: number): GiftCounters {
    return { day: day, sent: 0, received: 0, perFriend: {} };
}

function giftRead(nk: nkruntime.Nakama, userId: string, day: number): { value: GiftCounters; version: string } {
    const objects = nk.storageRead([{ collection: GiftCollection, key: GiftCountersKey, userId: userId }]);
    if (objects.length === 0)
        return { value: giftEmpty(day), version: "*" };
    const value = JSON.parse(JSON.stringify(objects[0].value)) as GiftCounters;
    return { value: value.day === day ? value : giftEmpty(day), version: objects[0].version };
}

function giftChange(nk: nkruntime.Nakama, userId: string, day: number, change: (value: GiftCounters) => void): void {
    for (let attempt = 1; attempt <= GiftWriteAttempts; attempt++) {
        const stored = giftRead(nk, userId, day);
        change(stored.value);
        try {
            nk.storageWrite([{ collection: GiftCollection, key: GiftCountersKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return;
        } catch (error) {
            if (attempt === GiftWriteAttempts)
                throw error;
        }
    }
}

function giftIsMentor(nk: nkruntime.Nakama, sender: string, target: string, nowUnixMs: number): boolean {
    const profile = mentorRead(nk, [sender])[sender].value;
    return profile.studentId === target && profile.studentUntil > nowUnixMs;
}

function giftCheck(nk: nkruntime.Nakama, sender: string, target: string, gemId: string, mentor: boolean, day: number): void {
    if (GiftServerRules.giftable.indexOf(gemId) < 0)
        throw new Error("this gem cannot be gifted");
    if ((partyBlocksOf(nk, [target])[target] || []).indexOf(sender) >= 0)
        throw new Error(GiftRefused);
    if (mentor)
        return;
    const sent = giftRead(nk, sender, day).value;
    if (sent.sent >= GiftServerRules.dailyLimit)
        throw new Error("no gifts left today");
    if ((sent.perFriend[target] || 0) >= GiftServerRules.perFriendDaily)
        throw new Error("already gifted this friend today");
    if (giftRead(nk, target, day).value.received >= GiftServerRules.receiveDailyLimit)
        throw new Error("this friend got enough gifts today");
}

function giftSendRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const sender = partyCaller(ctx);
    const request = partyRequest(payload);
    const target = request.userId;
    const gemId = request.gemId;
    const now = Date.now();
    if (typeof target !== "string" || typeof gemId !== "string" || target === sender || nk.usersGetId([target]).length === 0)
        throw new Error("bad request");
    const day = walletDayOf(now);
    const mentor = giftIsMentor(nk, sender, target, now);
    giftCheck(nk, sender, target, gemId, mentor, day);
    if (GiftServerRules.cost > 0)
        walletSpend(nk, sender, "friendship", GiftServerRules.cost, "gift_sent", now);
    const sent = mailSend(nk, target, {
        id: "gift-" + nk.uuidv4(), kind: "gift", at: now, place: 0, of: 0, depth: 0, title: chronicleNameOf(nk, sender),
        reward: { shards: 0, uncutGems: 0, ingots: 0 }, claimed: false, claimedAt: 0, gem: gemId
    });
    if (!sent) {
        if (GiftServerRules.cost > 0)
            walletRefund(nk, sender, "friendship", GiftServerRules.cost, "gift_back", now);
        throw new Error("the gift was not sent, try again");
    }
    giftChange(nk, sender, day, (value) => {
        if (mentor)
            return;
        value.sent++;
        value.perFriend[target] = (value.perFriend[target] || 0) + 1;
    });
    giftChange(nk, target, day, (value) => {
        if (!mentor)
            value.received++;
    });
    logger.info("gift %s from %s to %s%s", gemId, sender, target, mentor ? " (mentor)" : "");
    return JSON.stringify({ gemId: gemId, mentor: mentor, left: giftLeft(nk, sender, day) });
}

function giftLeft(nk: nkruntime.Nakama, userId: string, day: number): number {
    return Math.max(0, GiftServerRules.dailyLimit - giftRead(nk, userId, day).value.sent);
}

function giftStatusRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const day = walletDayOf(Date.now());
    return JSON.stringify({ left: giftLeft(nk, userId, day), dailyLimit: GiftServerRules.dailyLimit, perFriendDaily: GiftServerRules.perFriendDaily, cost: GiftServerRules.cost });
}
