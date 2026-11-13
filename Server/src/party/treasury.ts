interface PartyTreasuryVote {
    node: number;
    level: number;
}

interface PartyTreasuryReset {
    by: string;
    at: number;
    until: number;
    yes: string[];
    no: string[];
}

interface PartyTreasuryValue {
    treasury: number;
    donated: { [userId: string]: number };
    levels: { [node: string]: number };
    spent: number;
    votes: { [userId: string]: PartyTreasuryVote[] };
    reset: PartyTreasuryReset | null;
    freeResetAt: number;
}

const PartyTreasuryCollection = "party_treasury";
const PartyTreasuryAttempts = 4;

function partyTreasuryRules(): PartyTreasuryRules {
    return PartyGrowthServerRules.treasury;
}

function partyTreasuryEmpty(): PartyTreasuryValue {
    return { treasury: 0, donated: {}, levels: {}, spent: 0, votes: {}, reset: null, freeResetAt: 0 };
}

function partyTreasuryRead(nk: nkruntime.Nakama, partyId: string): { value: PartyTreasuryValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyTreasuryCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: partyTreasuryEmpty(), version: "*" };
    const stored = objects[0].value as PartyTreasuryValue;
    const value = partyTreasuryEmpty();
    value.treasury = stored.treasury || 0;
    value.donated = stored.donated || {};
    value.levels = stored.levels || {};
    value.spent = stored.spent || 0;
    value.votes = stored.votes || {};
    value.reset = stored.reset || null;
    value.freeResetAt = stored.freeResetAt || 0;
    return { value: value, version: objects[0].version };
}

function partyTreasuryChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyTreasuryValue) => T): T {
    for (let attempt = 1; attempt <= PartyTreasuryAttempts; attempt++) {
        const stored = partyTreasuryRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyTreasuryCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyTreasuryLevel(value: PartyTreasuryValue, node: number): number {
    return value.levels[String(node)] || 0;
}

function partyTreasurySettle(value: PartyTreasuryValue, members: string[], nowUnixMs: number): void {
    for (const userId of Object.keys(value.votes)) {
        const kept = members.indexOf(userId) < 0 ? [] : value.votes[userId].filter((vote) => partyTreasuryLevel(value, vote.node) <= vote.level);
        if (kept.length > 0)
            value.votes[userId] = kept;
        else
            delete value.votes[userId];
    }
    const reset = value.reset;
    if (!reset)
        return;
    if (nowUnixMs >= reset.until || members.indexOf(reset.by) < 0) {
        value.reset = null;
        return;
    }
    reset.yes = reset.yes.filter((userId) => members.indexOf(userId) >= 0);
    reset.no = reset.no.filter((userId) => members.indexOf(userId) >= 0);
}

function partyTreasuryFree(value: PartyTreasuryValue, nowUnixMs: number): boolean {
    return value.freeResetAt === 0 || nowUnixMs >= value.freeResetAt + partyTreasuryRules().freeResetMs;
}

function partyTreasuryResetFee(value: PartyTreasuryValue, nowUnixMs: number): number {
    return partyTreasuryFree(value, nowUnixMs) ? 0 : Math.ceil(value.spent * partyTreasuryRules().resetFeeShare);
}

function partyTreasuryDecide(value: PartyTreasuryValue, members: string[], nowUnixMs: number): string {
    const reset = value.reset;
    if (!reset)
        return "";
    if (reset.yes.length * 2 > members.length) {
        const fee = partyTreasuryResetFee(value, nowUnixMs);
        if (fee === 0)
            value.freeResetAt = nowUnixMs;
        value.treasury += value.spent - fee;
        value.spent = 0;
        value.levels = {};
        value.votes = {};
        value.reset = null;
        return "passed";
    }
    if (reset.no.length * 2 >= members.length) {
        value.reset = null;
        return "rejected";
    }
    return "";
}

function partyTreasuryNames(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: string } {
    const names: { [userId: string]: string } = {};
    for (const user of userIds.length > 0 ? nk.usersGetId(userIds) : [])
        names[user.userId] = user.displayName || user.username;
    return names;
}

function partyTreasuryView(nk: nkruntime.Nakama, partyId: string, value: PartyTreasuryValue, userId: string, meta: PartyMeta, nowUnixMs: number, outcome: string): { [key: string]: any } {
    const donors = Object.keys(value.donated).sort((a, b) => value.donated[b] - value.donated[a]);
    const names = partyTreasuryNames(nk, donors);
    const votes: { [node: string]: string[] } = {};
    for (const voter of Object.keys(value.votes))
        for (const vote of value.votes[voter])
            (votes[String(vote.node)] = votes[String(vote.node)] || []).push(voter);
    const mine = (value.votes[userId] || []).map((vote) => vote.node);
    const reset = value.reset;
    return {
        partyId: partyId, opened: partyGrowthOpened(nk, partyId, "tree"), leader: meta.leader === userId,
        treasury: value.treasury, yours: value.donated[userId] || 0, spent: value.spent, levels: value.levels,
        donors: donors.map((id) => ({ userId: id, name: names[id] || "", amount: value.donated[id] })),
        votes: votes, mine: mine, votesLeft: partyTreasuryRules().maxVotes - mine.length,
        resetFee: partyTreasuryResetFee(value, nowUnixMs), freeResetAt: value.freeResetAt === 0 ? 0 : value.freeResetAt + partyTreasuryRules().freeResetMs,
        reset: reset ? { by: reset.by, until: reset.until, yes: reset.yes, no: reset.no, members: meta.members.length } : null,
        outcome: outcome
    };
}

function partyTreasuryAct(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, act: (value: PartyTreasuryValue, meta: PartyMeta, partyId: string) => string): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const meta = partyMetaOf(group);
    return partyTreasuryChange(nk, group.id, (value) => {
        partyTreasurySettle(value, meta.members, nowUnixMs);
        const outcome = act(value, meta, group.id);
        return partyTreasuryView(nk, group.id, value, userId, meta, nowUnixMs, outcome);
    });
}

function partyTreasuryMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const meta = partyMetaOf(group);
    const value = partyTreasuryRead(nk, group.id).value;
    partyTreasurySettle(value, meta.members, nowUnixMs);
    return partyTreasuryView(nk, group.id, value, userId, meta, nowUnixMs, "");
}

function partyTreasuryDonate(nk: nkruntime.Nakama, userId: string, amount: unknown, nowUnixMs: number): { [key: string]: any } {
    const rules = partyTreasuryRules();
    if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < rules.minDonation || amount > 1e9)
        throw new Error("bad request");
    if (!partyGroupOfUser(nk, userId))
        throw new Error("not in a party");
    walletSpend(nk, userId, rules.currency, amount, "party_treasury", nowUnixMs);
    try {
        return partyTreasuryAct(nk, userId, nowUnixMs, (value) => {
            value.treasury += amount;
            value.donated[userId] = (value.donated[userId] || 0) + amount;
            return "";
        });
    } catch (error) {
        walletRefund(nk, userId, rules.currency, amount, "party_treasury_back", nowUnixMs);
        throw error;
    }
}

function partyTreasuryNode(node: unknown): number {
    if (typeof node !== "number" || node !== Math.floor(node) || node < 1 || node > partyTreasuryRules().nodes)
        throw new Error("bad request");
    return node;
}

function partyTreasuryRequireTree(nk: nkruntime.Nakama, partyId: string): void {
    if (!partyGrowthOpened(nk, partyId, "tree"))
        throw new Error("the party tree is not open yet");
}

function partyTreasuryVote(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const node = partyTreasuryNode(request.node);
    const add = request.add !== false;
    return partyTreasuryAct(nk, userId, nowUnixMs, (value, meta, partyId) => {
        partyTreasuryRequireTree(nk, partyId);
        const mine = value.votes[userId] || [];
        const held = mine.filter((vote) => vote.node === node).length > 0;
        if (add && !held) {
            if (mine.length >= partyTreasuryRules().maxVotes)
                throw new Error("no votes left");
            mine.push({ node: node, level: partyTreasuryLevel(value, node) });
        }
        const kept = add ? mine : mine.filter((vote) => vote.node !== node);
        if (kept.length > 0)
            value.votes[userId] = kept;
        else
            delete value.votes[userId];
        return "";
    });
}

function partyTreasuryPropose(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return partyTreasuryAct(nk, userId, nowUnixMs, (value, meta, partyId) => {
        partyTreasuryRequireTree(nk, partyId);
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (value.reset)
            throw new Error("a reset vote is already open");
        if (value.spent <= 0)
            throw new Error("nothing to reset");
        value.reset = { by: userId, at: nowUnixMs, until: nowUnixMs + partyTreasuryRules().resetVoteMs, yes: [userId], no: [] };
        return partyTreasuryDecide(value, meta.members, nowUnixMs);
    });
}

function partyTreasuryResetVote(nk: nkruntime.Nakama, userId: string, yes: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof yes !== "boolean")
        throw new Error("bad request");
    return partyTreasuryAct(nk, userId, nowUnixMs, (value, meta) => {
        const reset = value.reset;
        if (!reset)
            throw new Error("no reset vote is open");
        reset.yes = reset.yes.filter((id) => id !== userId);
        reset.no = reset.no.filter((id) => id !== userId);
        (yes ? reset.yes : reset.no).push(userId);
        return partyTreasuryDecide(value, meta.members, nowUnixMs);
    });
}

function partyTreasuryCancel(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return partyTreasuryAct(nk, userId, nowUnixMs, (value, meta) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        if (!value.reset)
            throw new Error("no reset vote is open");
        value.reset = null;
        return "cancelled";
    });
}

function partyTreasuryRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryMine(nk, partyCaller(ctx), Date.now()));
}

function partyTreasuryDonateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryDonate(nk, partyCaller(ctx), partyRequest(payload).amount, Date.now()));
}

function partyTreasuryVoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryVote(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyTreasuryResetProposeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryPropose(nk, partyCaller(ctx), Date.now()));
}

function partyTreasuryResetVoteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryResetVote(nk, partyCaller(ctx), partyRequest(payload).yes, Date.now()));
}

function partyTreasuryResetCancelRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTreasuryCancel(nk, partyCaller(ctx), Date.now()));
}
