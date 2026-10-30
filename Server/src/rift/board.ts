interface RiftBoardEntry {
    partyId: string;
    title: string;
    cleared: number;
    reachedAt: number;
    members: string[];
}

interface RiftBoard {
    riftId: number;
    endUnixMs: number;
    parties: { [partyId: string]: RiftBoardEntry };
    settled: boolean;
}

const RiftBoardCollection = "rift_board";
const RiftBoardIndexKey = "pending";
const RiftBoardWriteAttempts = 3;

function riftBoardKey(riftId: number): string {
    return String(riftId);
}

function riftUpdate<T>(nk: nkruntime.Nakama, key: string, empty: () => T, change: (value: T) => boolean): T | null {
    for (let attempt = 1; attempt <= RiftBoardWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: RiftBoardCollection, key: key, userId: PartySystemUser }]);
        const value = objects.length > 0 ? objects[0].value as T : empty();
        if (!change(value))
            return value;
        try {
            nk.storageWrite([{
                collection: RiftBoardCollection, key: key, userId: PartySystemUser, value: value as any,
                version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return value;
        } catch (error) {
            if (attempt === RiftBoardWriteAttempts)
                return null;
        }
    }
    return null;
}

function riftEmptyBoard(riftId: number): RiftBoard {
    return { riftId: riftId, endUnixMs: riftPartyEnd(riftId), parties: {}, settled: false };
}

function riftBoardAdd(nk: nkruntime.Nakama, riftId: number, partyId: string, title: string, cleared: number, members: string[], nowUnixMs: number): number {
    let created = false;
    const board = riftUpdate(nk, riftBoardKey(riftId), () => {
        created = true;
        return riftEmptyBoard(riftId);
    }, (value) => {
        if (value.settled)
            return false;
        const entry = value.parties[partyId] || { partyId: partyId, title: title, cleared: 0, reachedAt: nowUnixMs, members: [] };
        if (cleared > 0) {
            entry.cleared += cleared;
            entry.reachedAt = nowUnixMs;
        }
        if (title)
            entry.title = title;
        for (const member of members)
            if (entry.members.indexOf(member) < 0)
                entry.members.push(member);
        value.parties[partyId] = entry;
        return true;
    });
    if (created && board !== null)
        riftUpdate<{ rifts: number[] }>(nk, RiftBoardIndexKey, () => ({ rifts: [] }), (index) => {
            if (index.rifts.indexOf(riftId) >= 0)
                return false;
            index.rifts.push(riftId);
            return true;
        });
    return board !== null && !board.settled && board.parties[partyId] ? 1 + board.parties[partyId].cleared : 0;
}

function riftBoardCleared(nk: nkruntime.Nakama, riftId: number, partyId: string): number {
    const objects = nk.storageRead([{ collection: RiftBoardCollection, key: riftBoardKey(riftId), userId: PartySystemUser }]);
    const board = objects.length > 0 ? objects[0].value as RiftBoard : null;
    return board && board.parties[partyId] ? board.parties[partyId].cleared : 0;
}

function riftSettlesAt(riftId: number): number {
    return riftPartyEnd(riftId) + RiftServerRules.boardUpdateMs;
}

function riftRanking(board: RiftBoard): RiftBoardEntry[] {
    const entries: RiftBoardEntry[] = [];
    for (const partyId of Object.keys(board.parties))
        entries.push(board.parties[partyId]);
    entries.sort((a, b) => b.cleared - a.cleared || a.reachedAt - b.reachedAt || (a.partyId < b.partyId ? -1 : a.partyId > b.partyId ? 1 : 0));
    return entries;
}

function riftRewardFor(place: number): RiftBoardPlace {
    for (const tier of RiftServerRules.boardPlaces)
        if (tier.upTo === 0 || place <= tier.upTo)
            return tier;
    return { upTo: 0, shards: 0, uncutGems: 0, ingots: 0, friendship: 0 };
}

function riftGrantFriendship(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, amount: number, nowUnixMs: number): void {
    if (!(amount > 0))
        return;
    try {
        walletGrant(nk, userId, "rift_place", amount, nowUnixMs);
    } catch (error) {
        logger.warn("friendship for the rift place was not granted to %s: %s", userId, String(error));
    }
}

function riftSettle(nk: nkruntime.Nakama, logger: nkruntime.Logger, riftId: number, nowUnixMs: number): boolean {
    const objects = nk.storageRead([{ collection: RiftBoardCollection, key: riftBoardKey(riftId), userId: PartySystemUser }]);
    if (objects.length === 0)
        return true;
    const board = objects[0].value as RiftBoard;
    if (board.settled)
        return true;
    if (nowUnixMs < riftSettlesAt(riftId))
        return false;
    const ranking = riftRanking(board);
    for (let i = 0; i < ranking.length; i++) {
        const entry = ranking[i];
        const tier = riftRewardFor(i + 1);
        for (const userId of entry.members) {
            const sent = mailSend(nk, userId, {
                id: "rift-" + riftId, kind: "rift", at: nowUnixMs, place: i + 1, of: ranking.length, depth: entry.cleared + 1, title: entry.title,
                reward: { shards: tier.shards, uncutGems: tier.uncutGems, ingots: tier.ingots, friendship: tier.friendship }, claimed: false, claimedAt: 0
            });
            if (sent)
                riftGrantFriendship(nk, logger, userId, tier.friendship, nowUnixMs);
        }
    }
    board.settled = true;
    try {
        nk.storageWrite([{
            collection: RiftBoardCollection, key: riftBoardKey(riftId), userId: PartySystemUser, value: board as any,
            version: objects[0].version, permissionRead: 0, permissionWrite: 0
        }]);
    } catch (error) {
        return false;
    }
    logger.info("rift %d settled: %d parties", riftId, ranking.length);
    return true;
}

function riftSettleDue(nk: nkruntime.Nakama, logger: nkruntime.Logger, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: RiftBoardCollection, key: RiftBoardIndexKey, userId: PartySystemUser }]);
    if (objects.length === 0)
        return;
    const settled: number[] = [];
    for (const riftId of (objects[0].value as { rifts: number[] }).rifts)
        if (riftSettlesAt(riftId) <= nowUnixMs && riftSettle(nk, logger, riftId, nowUnixMs))
            settled.push(riftId);
    if (settled.length > 0)
        riftUpdate<{ rifts: number[] }>(nk, RiftBoardIndexKey, () => ({ rifts: [] }), (index) => {
            index.rifts = index.rifts.filter((riftId) => settled.indexOf(riftId) < 0);
            return true;
        });
}

function riftBoardView(board: RiftBoard, partyId: string): { [key: string]: any } {
    const ranking = riftRanking(board);
    const shown = ranking.slice(0, RiftServerRules.boardShown).map((entry, i) => ({
        place: i + 1, title: entry.title, depth: entry.cleared + 1, members: entry.members.length, yours: entry.partyId === partyId
    }));
    let own: { [key: string]: any } | null = null;
    for (let i = 0; i < ranking.length; i++)
        if (ranking[i].partyId === partyId)
            own = { place: i + 1, depth: ranking[i].cleared + 1, reward: riftRewardFor(i + 1) };
    return { riftId: board.riftId, endUnixMs: board.endUnixMs, settled: board.settled, parties: ranking.length, top: shown, own: own };
}

function riftBoardRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const now = Date.now();
    riftSettleDue(nk, logger, now);
    const request = partyRequest(payload);
    const riftId = typeof request.riftId === "number" && request.riftId > 0 ? Math.floor(request.riftId) : 0;
    if (riftId === 0)
        throw new Error("bad request");
    const objects = nk.storageRead([{ collection: RiftBoardCollection, key: riftBoardKey(riftId), userId: PartySystemUser }]);
    if (objects.length === 0)
        throw new Error("rift not found");
    const group = partyGroupOfUser(nk, partyCaller(ctx));
    return JSON.stringify(riftBoardView(objects[0].value as RiftBoard, group ? group.id : ""));
}

function riftMilestones(nk: nkruntime.Nakama, logger: nkruntime.Logger, riftId: number, title: string, members: string[], before: number, after: number, nowUnixMs: number): number {
    const rules = RiftServerRules;
    let sent = 0;
    for (let stage = (Math.floor(before / rules.milestones.every) + 1) * rules.milestones.every; stage <= after; stage += rules.milestones.every) {
        const step = riftMilestoneStep(rules, stage);
        if (!step)
            continue;
        const gem = step.rareGem ? riftMilestoneGem(rules, riftId, stage) : "";
        for (const userId of members) {
            const letter: MailLetter = {
                id: "riftdepth-" + riftId + "-" + stage, kind: "rift_depth", at: nowUnixMs, place: 0, of: 0, depth: stage, title: title,
                reward: { shards: step.shards, uncutGems: step.uncutGems, ingots: 0, friendship: step.friendship }, claimed: false, claimedAt: 0
            };
            if (gem)
                letter.gem = gem;
            if (!mailSend(nk, userId, letter))
                continue;
            sent++;
            if (step.friendship > 0) {
                try {
                    walletGrant(nk, userId, "rift_depth", step.friendship, nowUnixMs);
                } catch (error) {
                    logger.warn("friendship for the rift milestone was not granted to %s: %s", userId, String(error));
                }
            }
        }
    }
    return sent;
}
