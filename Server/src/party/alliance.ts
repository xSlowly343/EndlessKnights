interface PartyAllianceValue {
    ally: string;
    since: number;
    incoming: { [partyId: string]: number };
    outgoing: string;
    helpWeek: number;
}

const PartyAllianceCollection = "party_alliance";

function partyAllianceRules(): { helpShare: number; signs: number; proposalMs: number } {
    return PartyGrowthServerRules.alliance;
}

function partyAllianceEmpty(): PartyAllianceValue {
    return { ally: "", since: 0, incoming: {}, outgoing: "", helpWeek: -1 };
}

function partyAllianceRead(nk: nkruntime.Nakama, partyId: string): PartyAllianceValue {
    const objects = nk.storageRead([{ collection: PartyAllianceCollection, key: partyId, userId: PartySystemUser }]);
    const value = partyAllianceEmpty();
    if (objects.length === 0)
        return value;
    const stored = objects[0].value as PartyAllianceValue;
    for (const key of Object.keys(value))
        if ((stored as any)[key] !== undefined)
            (value as any)[key] = (stored as any)[key];
    return value;
}

function partyAllianceWrite(nk: nkruntime.Nakama, values: { [partyId: string]: PartyAllianceValue }): void {
    nk.storageWrite(Object.keys(values).map((partyId) => ({
        collection: PartyAllianceCollection, key: partyId, userId: PartySystemUser, value: values[partyId] as any, permissionRead: 0, permissionWrite: 0
    })));
}

function partyAllianceSettle(value: PartyAllianceValue, nowUnixMs: number): void {
    for (const id of Object.keys(value.incoming))
        if (nowUnixMs > value.incoming[id] + partyAllianceRules().proposalMs)
            delete value.incoming[id];
}

function partyAllianceGroup(nk: nkruntime.Nakama, partyId: string): nkruntime.Group | undefined {
    return partyId ? nk.groupsGetId([partyId])[0] : undefined;
}

function partyAllianceTitle(nk: nkruntime.Nakama, partyId: string): { partyId: string; name: string; level: number; members: number } | null {
    const group = partyAllianceGroup(nk, partyId);
    if (!group)
        return null;
    const meta = partyMetaOf(group);
    return { partyId: partyId, name: meta.name || meta.title || "", level: partyGrowthLevel(partyGrowthRead(nk, partyId).value.experience).level, members: meta.members.length };
}

function partyAllianceOwn(nk: nkruntime.Nakama, userId: string): { group: nkruntime.Group; meta: PartyMeta } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    return { group: group, meta: partyMetaOf(group) };
}

function partyAllianceView(nk: nkruntime.Nakama, partyId: string, userId: string, meta: PartyMeta, nowUnixMs: number, result: { [key: string]: any } | null): { [key: string]: any } {
    const value = partyAllianceRead(nk, partyId);
    partyAllianceSettle(value, nowUnixMs);
    return {
        partyId: partyId, opened: partyGrowthOpened(nk, partyId, "alliance"), leader: meta.leader === userId,
        ally: value.ally ? partyAllianceTitle(nk, value.ally) : null, since: value.since,
        incoming: Object.keys(value.incoming).map((id) => partyAllianceTitle(nk, id)).filter((title) => title !== null),
        outgoing: value.outgoing ? partyAllianceTitle(nk, value.outgoing) : null,
        helpReady: !!value.ally && value.helpWeek !== partyTrialWeek(nowUnixMs),
        helpPoints: Math.floor(partyColossusRead(nk, partyId).value.backgroundWeek * partyAllianceRules().helpShare),
        result: result
    };
}

function partyAllianceRequireOpen(nk: nkruntime.Nakama, partyId: string): void {
    if (!partyGrowthOpened(nk, partyId, "alliance"))
        throw new Error("the alliance opens on party level 60");
}

function partyAlliancePropose(nk: nkruntime.Nakama, userId: string, code: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof code !== "string" || code.length === 0 || code.length > 32)
        throw new Error("bad request");
    const own = partyAllianceOwn(nk, userId);
    if (own.meta.leader !== userId)
        throw new Error("only the leader can do that");
    partyAllianceRequireOpen(nk, own.group.id);
    const found = nk.storageRead([{ collection: "party_codes", key: code.toUpperCase(), userId: PartySystemUser }]);
    const targetId = found.length > 0 ? (found[0].value as { partyId: string }).partyId : "";
    if (!targetId || !partyAllianceGroup(nk, targetId))
        throw new Error("unknown code");
    if (targetId === own.group.id)
        throw new Error("a party cannot ally with itself");
    if (!partyGrowthOpened(nk, targetId, "alliance"))
        throw new Error("that party has not reached level 60");
    const mine = partyAllianceRead(nk, own.group.id);
    const theirs = partyAllianceRead(nk, targetId);
    if (mine.ally || theirs.ally)
        throw new Error("one of the parties already has an ally");
    mine.outgoing = targetId;
    theirs.incoming[own.group.id] = nowUnixMs;
    partyAllianceWrite(nk, { [own.group.id]: mine, [targetId]: theirs });
    return partyAllianceView(nk, own.group.id, userId, own.meta, nowUnixMs, null);
}

function partyAllianceAnswer(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const fromId = request.partyId;
    if (typeof fromId !== "string" || typeof request.accept !== "boolean")
        throw new Error("bad request");
    const own = partyAllianceOwn(nk, userId);
    if (own.meta.leader !== userId)
        throw new Error("only the leader can do that");
    const mine = partyAllianceRead(nk, own.group.id);
    partyAllianceSettle(mine, nowUnixMs);
    if (mine.incoming[fromId] === undefined)
        throw new Error("no such proposal");
    delete mine.incoming[fromId];
    const theirs = partyAllianceRead(nk, fromId);
    if (theirs.outgoing === own.group.id)
        theirs.outgoing = "";
    if (request.accept) {
        if (mine.ally || theirs.ally)
            throw new Error("one of the parties already has an ally");
        mine.ally = fromId;
        theirs.ally = own.group.id;
        mine.since = nowUnixMs;
        theirs.since = nowUnixMs;
        mine.outgoing = "";
        mine.incoming = {};
        theirs.incoming = {};
    }
    partyAllianceWrite(nk, { [own.group.id]: mine, [fromId]: theirs });
    return partyAllianceView(nk, own.group.id, userId, own.meta, nowUnixMs, null);
}

function partyAllianceBreak(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const own = partyAllianceOwn(nk, userId);
    if (own.meta.leader !== userId)
        throw new Error("only the leader can do that");
    const mine = partyAllianceRead(nk, own.group.id);
    if (!mine.ally)
        throw new Error("the party has no ally");
    const theirs = partyAllianceRead(nk, mine.ally);
    const allyId = mine.ally;
    mine.ally = "";
    theirs.ally = "";
    partyAllianceWrite(nk, { [own.group.id]: mine, [allyId]: theirs });
    return partyAllianceView(nk, own.group.id, userId, own.meta, nowUnixMs, null);
}

function partyAllianceHelp(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const own = partyAllianceOwn(nk, userId);
    const mine = partyAllianceRead(nk, own.group.id);
    if (!mine.ally || !partyAllianceGroup(nk, mine.ally))
        throw new Error("the party has no ally");
    const week = partyTrialWeek(nowUnixMs);
    if (mine.helpWeek === week)
        throw new Error("the party has already helped its ally this week");
    const points = partyColossusRead(nk, own.group.id).value.backgroundWeek * partyAllianceRules().helpShare;
    if (!(points > 0))
        throw new Error("nothing to send yet: fight first");
    mine.helpWeek = week;
    partyAllianceWrite(nk, { [own.group.id]: mine });
    const allyId = mine.ally;
    const allyMeta = partyMetaOf(partyAllianceGroup(nk, allyId)!);
    const situation = partyColossusSituation(nk, allyId, allyMeta.leader, nowUnixMs);
    let killed: number[] = [];
    let signs = 0;
    if (situation.ceiling > 0)
        partyColossusChange(nk, allyId, (value) => {
            partyColossusSettle(value, situation, nowUnixMs);
            const hit = partyColossusHit(value, situation, points, nowUnixMs);
            killed = hit.killed;
            value.pendingSigns += hit.signs;
            if (!value.waiting)
                value.lastHit = nowUnixMs;
        });
    for (const tier of killed)
        partyColossusChronicle(nk, allyId, tier, nowUnixMs);
    signs = partyAllianceRules().signs;
    for (const id of [own.group.id, allyId])
        partyTreasuryChange(nk, id, (value) => {
            value.treasury += signs;
        });
    return partyAllianceView(nk, own.group.id, userId, own.meta, nowUnixMs, { points: Math.floor(points), killed: killed, signs: signs });
}

function partyAllianceMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const own = partyAllianceOwn(nk, userId);
    return partyAllianceView(nk, own.group.id, userId, own.meta, nowUnixMs, null);
}

function partyAllianceRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyAllianceMine(nk, partyCaller(ctx), Date.now()));
}

function partyAllianceProposeRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyAlliancePropose(nk, partyCaller(ctx), partyRequest(payload).code, Date.now()));
}

function partyAllianceAnswerRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyAllianceAnswer(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyAllianceBreakRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyAllianceBreak(nk, partyCaller(ctx), Date.now()));
}

function partyAllianceHelpRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyAllianceHelp(nk, partyCaller(ctx), Date.now()));
}
