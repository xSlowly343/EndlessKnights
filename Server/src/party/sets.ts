interface PartySetsValue {
    members: { [userId: string]: string[] };
}

const PartySetsCollection = "party_sets";
const PartySetsAttempts = 4;

function partySetsRead(nk: nkruntime.Nakama, partyId: string): { value: PartySetsValue; version: string } {
    const objects = nk.storageRead([{ collection: PartySetsCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { members: {} }, version: "*" };
    const stored = objects[0].value as PartySetsValue;
    return { value: { members: stored.members || {} }, version: objects[0].version };
}

function partySetsChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartySetsValue) => T): T {
    for (let attempt = 1; attempt <= PartySetsAttempts; attempt++) {
        const stored = partySetsRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartySetsCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partySetsKnown(id: string): boolean {
    return PartyGrowthServerRules.partySets.sets.some((set) => set.id === id);
}

function partySetsOpened(nk: nkruntime.Nakama, partyId: string): boolean {
    return partyGrowthOpened(nk, partyId, "partySets");
}

function partySetsCounts(nk: nkruntime.Nakama, partyId: string): { [setId: string]: number } {
    const counts: { [setId: string]: number } = {};
    const group = nk.groupsGetId([partyId])[0];
    if (!group || !partySetsOpened(nk, partyId))
        return counts;
    const members = partyMetaOf(group).members;
    const value = partySetsRead(nk, partyId).value;
    for (const userId of members)
        for (const id of value.members[userId] || [])
            counts[id] = (counts[id] || 0) + 1;
    return counts;
}

function partySetsSum(nk: nkruntime.Nakama, partyId: string, stat: string): number {
    const counts = partySetsCounts(nk, partyId);
    let total = 0;
    for (const set of PartyGrowthServerRules.partySets.sets) {
        const count = counts[set.id] || 0;
        if (count <= 0)
            continue;
        for (const line of set.server)
            if (line.stat === stat)
                total += line.value * count;
    }
    return total;
}

function partySetsView(nk: nkruntime.Nakama, partyId: string, userId: string, meta: PartyMeta): { [key: string]: any } {
    const value = partySetsRead(nk, partyId).value;
    const names = partyTreasuryNames(nk, meta.members);
    return {
        partyId: partyId, opened: partySetsOpened(nk, partyId), counts: partySetsCounts(nk, partyId),
        yours: value.members[userId] || [],
        members: meta.members.map((id) => ({ userId: id, name: names[id] || "", sets: value.members[id] || [] }))
    };
}

function partySetsMine(nk: nkruntime.Nakama, userId: string): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    return partySetsView(nk, group.id, userId, partyMetaOf(group));
}

function partySetsReport(nk: nkruntime.Nakama, userId: string, sets: unknown): { [key: string]: any } {
    if (!Array.isArray(sets) || sets.length > 1000 || sets.some((id) => typeof id !== "string" || !partySetsKnown(id as string)))
        throw new Error("bad request");
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const unique = (sets as string[]).filter((id, index, all) => all.indexOf(id) === index).sort();
    const pieces = partySetsProvenPieces(nk, userId);
    const proven = unique.filter((id) => partySetsCollectedIn(id, pieces));
    partySetsChange(nk, group.id, (value) => {
        value.members[userId] = proven;
    });
    const view = partySetsView(nk, group.id, userId, partyMetaOf(group));
    view.refused = unique.filter((id) => proven.indexOf(id) < 0);
    return view;
}

function partySetsProvenPieces(nk: nkruntime.Nakama, userId: string): { [key: string]: boolean } {
    const trusted = cloudTrustedRead(nk, userId);
    const pieces: { [key: string]: boolean } = {};
    for (const key of (trusted && trusted.facts.partyPieces) || [])
        pieces[key] = true;
    return pieces;
}

function partySetsCollectedIn(id: string, pieces: { [key: string]: boolean }): boolean {
    const set = PartyGrowthServerRules.partySets.sets.filter((each) => each.id === id)[0];
    return !!set && set.pieces.every((piece) => pieces[id + ":" + piece] === true);
}

function partySetsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partySetsMine(nk, partyCaller(ctx)));
}

function partySetsReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partySetsReport(nk, partyCaller(ctx), partyRequest(payload).sets));
}
