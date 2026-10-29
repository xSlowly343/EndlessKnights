interface ClanTreeNodeRule {
    id: string;
    cost: number;
    requires: string[];
}

interface ClanTreeRules {
    minDonation: number;
    nodes: ClanTreeNodeRule[];
}

interface ClanTreeValue {
    treasury: number;
    taken: string[];
    donated: { [userId: string]: number };
    log: { at: number; userId: string; kind: string; amount: number; node: string }[];
}

declare const ClanTreeServerRules: ClanTreeRules;

const ClanTreeCollection = "clan_tree";
const ClanTreeWriteAttempts = 4;
const ClanTreeLogSize = 20;
const ClanTreeCurrency = "seals";

function clanTreeEmpty(): ClanTreeValue {
    return { treasury: 0, taken: [], donated: {}, log: [] };
}

function clanTreeNode(id: string): ClanTreeNodeRule | null {
    return ClanTreeServerRules.nodes.filter((node) => node.id === id)[0] || null;
}

function clanTreeRead(nk: nkruntime.Nakama, clanId: string): { value: ClanTreeValue; version: string } {
    const objects = nk.storageRead([{ collection: ClanTreeCollection, key: clanId, userId: PartySystemUser }]);
    return objects.length > 0 ? { value: objects[0].value as ClanTreeValue, version: objects[0].version } : { value: clanTreeEmpty(), version: "*" };
}

function clanTreeChange<T>(nk: nkruntime.Nakama, clanId: string, change: (value: ClanTreeValue) => T): T {
    for (let attempt = 1; attempt <= ClanTreeWriteAttempts; attempt++) {
        const stored = clanTreeRead(nk, clanId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: ClanTreeCollection, key: clanId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("clan is busy, try again");
}

function clanTreeLog(value: ClanTreeValue, entry: { at: number; userId: string; kind: string; amount: number; node: string }): void {
    value.log.unshift(entry);
    if (value.log.length > ClanTreeLogSize)
        value.log.length = ClanTreeLogSize;
}

function clanTreeOpen(value: ClanTreeValue, node: ClanTreeNodeRule): boolean {
    return node.requires.length === 0 || node.requires.some((parent) => value.taken.indexOf(parent) >= 0);
}

function clanTreeView(nk: nkruntime.Nakama, value: ClanTreeValue, userId: string): { [key: string]: any } {
    const donors = Object.keys(value.donated).sort((a, b) => value.donated[b] - value.donated[a]);
    const names: { [userId: string]: string } = {};
    for (const user of donors.length > 0 ? nk.usersGetId(donors) : [])
        names[user.userId] = user.displayName || user.username;
    return {
        treasury: value.treasury, taken: value.taken, yours: value.donated[userId] || 0,
        donors: donors.map((id) => ({ userId: id, name: names[id] || "", amount: value.donated[id] })),
        log: value.log.map((entry) => ({ at: entry.at, name: names[entry.userId] || chronicleNameOf(nk, entry.userId), kind: entry.kind, amount: entry.amount, node: entry.node }))
    };
}

function clanTreeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const group = clanOf(nk, userId, Date.now());
    return JSON.stringify(clanTreeView(nk, clanTreeRead(nk, group.id).value, userId));
}

function clanDonateRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const amount = partyRequest(payload).amount;
    if (typeof amount !== "number" || amount !== Math.floor(amount) || amount < ClanTreeServerRules.minDonation || amount > 1e9)
        throw new Error("bad request");
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    walletSpend(nk, userId, ClanTreeCurrency, amount, "clan_donation", now);
    try {
        const value = clanTreeChange(nk, group.id, (tree) => {
            tree.treasury += amount;
            tree.donated[userId] = (tree.donated[userId] || 0) + amount;
            clanTreeLog(tree, { at: now, userId: userId, kind: "donated", amount: amount, node: "" });
            return tree;
        });
        clanChronicleNote(nk, logger, group.id, { sealsDonated: amount }, null, clanMetaOf(group).members);
        return JSON.stringify(clanTreeView(nk, value, userId));
    } catch (error) {
        walletRefund(nk, userId, ClanTreeCurrency, amount, "clan_donation_back", now);
        throw error;
    }
}

function clanTreeBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const nodeId = partyRequest(payload).node;
    const node = typeof nodeId === "string" ? clanTreeNode(nodeId) : null;
    if (!node)
        throw new Error("bad request");
    const now = Date.now();
    const group = clanOf(nk, userId, now);
    if (clanRoleOf(clanMetaOf(group), userId) === ClanMember)
        throw new Error("only the leader can do that");
    const value = clanTreeChange(nk, group.id, (tree) => {
        if (tree.taken.indexOf(node.id) >= 0)
            throw new Error("already taken");
        if (!clanTreeOpen(tree, node))
            throw new Error("not connected");
        if (tree.treasury < node.cost)
            throw new Error("not enough seals");
        tree.treasury -= node.cost;
        tree.taken.push(node.id);
        clanTreeLog(tree, { at: now, userId: userId, kind: "bought", amount: node.cost, node: node.id });
        return tree;
    });
    logger.info("clan %s: tree node %s bought by %s", group.id, node.id, userId);
    clanChronicleEvent(nk, group, "node", userId, node.id, { treeNodes: 1 });
    return JSON.stringify(clanTreeView(nk, value, userId));
}
