interface PartyMatchState {
    host: PartyHost;
    partyId: string;
    presences: { [userId: string]: nkruntime.Presence };
    pendingNumbers: { [userId: string]: string };
    emptyTicks: number;
    chronicleTicks: number;
    boardRiftId: number;
    boardTicks: number;
    helpInvites?: { [userId: string]: number };
    pendingGuests?: { [userId: string]: boolean };
}

const PartyMatchName = "party";
const PartyEmptyTicksBeforeClose = 600;

function partyRevive(stored: PartyMatchState): PartyMatchState {
    if (stored.host instanceof PartyHost)
        return stored;
    const state: PartyMatchState = JSON.parse(JSON.stringify(stored));
    Object.setPrototypeOf(state.host, PartyHost.prototype);
    Object.setPrototypeOf(state.host.encounter, PartyEncounter.prototype);
    for (const member of state.host.encounter.members)
        Object.setPrototypeOf(member, PartyMember.prototype);
    return state;
}

function partyDecimalOrNull(value: any): string | null {
    return typeof value === "string" && /^[0-9]{1,20}$/.test(value) ? value : null;
}

function partyMatchInit(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, params: { [key: string]: string }) {
    const seed = partyDecimalOrNull(params.seed) || String(Date.now());
    const partyId = typeof params.partyId === "string" ? params.partyId : "";
    const state: PartyMatchState = { host: new PartyHost(PartyServerRules, seed), partyId: partyId, presences: {}, pendingNumbers: {}, emptyTicks: 0, chronicleTicks: 0, boardRiftId: 0, boardTicks: 0 };
    partyRestoreRift(nk, logger, state, Date.now());
    return { state: state, tickRate: PartyTicksPerSecond, label: JSON.stringify({ kind: PartyMatchName, open: true }) };
}

function partyMatchJoinAttempt(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, presence: nkruntime.Presence, metadata: { [key: string]: any }) {
    state = partyRevive(state);
    const host = state.host;
    const known = host.encounter.slotOf(presence.userId) !== PartyNoSlot;
    const guest = known ? host.isGuest(presence.userId) : partyInvitedGuest(state, presence.userId, Date.now());
    if (state.partyId && !guest && !partyMatchMember(nk, state.partyId, presence.userId))
        return { state: state, accept: false, rejectMessage: "not a member of this party" };
    if (!known && host.encounter.memberCount() >= PartyServerRules.maxMembers)
        return { state: state, accept: false, rejectMessage: "party is full" };
    if (!known && partyDecimalOrNull(metadata.playerNumber) === null)
        return { state: state, accept: false, rejectMessage: "playerNumber is required" };
    if (!known)
        state.pendingNumbers[presence.userId] = metadata.playerNumber;
    if (guest && !known) {
        state.pendingGuests = state.pendingGuests || {};
        state.pendingGuests[presence.userId] = true;
    }
    return { state: state, accept: true };
}

function partyMatchJoin(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, presences: nkruntime.Presence[]) {
    state = partyRevive(state);
    const host = state.host;
    for (const presence of presences) {
        state.presences[presence.userId] = presence;
        if (state.pendingGuests && state.pendingGuests[presence.userId]) {
            partyWelcomeGuest(dispatcher, state, presence);
            continue;
        }
        const slot = host.join(presence.userId, state.pendingNumbers[presence.userId] || "0");
        delete state.pendingNumbers[presence.userId];
        if (state.partyId)
            partyNoteVisit(nk, logger, state.partyId, ctx.matchId || "", presence.userId, slot);
        else
            partySaveMembership(nk, logger, ctx.matchId || "", presence.userId, slot);
    }
    partyUpdateLabel(dispatcher, host);
    return { state: state };
}

function partyMatchLeave(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, presences: nkruntime.Presence[]) {
    state = partyRevive(state);
    const host = state.host;
    for (const presence of presences) {
        delete state.presences[presence.userId];
        host.disconnect(presence.userId);
    }
    return { state: state };
}

function partyMatchLoop(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, messages: nkruntime.MatchMessage[]) {
    state = partyRevive(state);
    const host = state.host;
    for (const message of messages)
        host.receive(message.sender.userId, message.data);
    const now = Date.now();
    partyHandleWorld(nk, state, now);
    host.tick(now);
    partyTrackRift(nk, logger, state, now);
    partySeeGuestsOut(dispatcher, state);
    partySend(dispatcher, state, host);
    partyReportFlags(nk, logger, ctx.matchId || "", host);
    state.chronicleTicks = (state.chronicleTicks || 0) + 1;
    if (state.chronicleTicks >= Math.max(1, partyTicks(ChronicleServerRules.flushSeconds))) {
        state.chronicleTicks = 0;
        partyFlushChronicle(nk, logger, state);
    }
    state.emptyTicks = Object.keys(state.presences).length === 0 ? state.emptyTicks + 1 : 0;
    return state.emptyTicks > PartyEmptyTicksBeforeClose ? null : { state: state };
}

function partyMatchTerminate(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, graceSeconds: number) {
    state = partyRevive(state);
    partyFlushChronicle(nk, logger, state);
    return { state: state };
}

function partyInvitedGuest(state: PartyMatchState, userId: string, nowUnixMs: number): boolean {
    return !!state.helpInvites && (state.helpInvites[userId] || 0) > nowUnixMs;
}

function partyWelcomeGuest(dispatcher: nkruntime.MatchDispatcher, state: PartyMatchState, presence: nkruntime.Presence): void {
    const number = state.pendingNumbers[presence.userId] || "0";
    delete state.pendingNumbers[presence.userId];
    delete state.pendingGuests![presence.userId];
    if (state.helpInvites)
        delete state.helpInvites[presence.userId];
    if (state.host.joinGuest(presence.userId, number, HelpServerRules.seconds) !== PartyNoSlot)
        return;
    delete state.presences[presence.userId];
    dispatcher.matchKick([presence]);
}

function partySeeGuestsOut(dispatcher: nkruntime.MatchDispatcher, state: PartyMatchState): void {
    const host = state.host;
    if (host.guestsLeft.length === 0)
        return;
    const leaving: nkruntime.Presence[] = [];
    for (const userId of host.guestsLeft) {
        const presence = state.presences[userId];
        if (!presence)
            continue;
        leaving.push(presence);
        delete state.presences[userId];
    }
    host.guestsLeft.length = 0;
    if (leaving.length > 0)
        dispatcher.matchKick(leaving);
}

function partyInvite(state: PartyMatchState, userId: string, untilUnixMs: number): string {
    const encounter = state.host.encounter;
    if (!userId || !(untilUnixMs > Date.now()))
        return "bad request";
    if (encounter.slotOf(userId) !== PartyNoSlot)
        return "already in your party";
    if (!encounter.fightingBoss)
        return "no boss to help with";
    if (encounter.world !== PartyWorld.Home || encounter.switching)
        return "no calls from the rift";
    if (encounter.memberCount() >= PartyServerRules.maxMembers)
        return "party is full";
    state.helpInvites = state.helpInvites || {};
    for (const invited of Object.keys(state.helpInvites))
        if (state.helpInvites[invited] <= Date.now())
            delete state.helpInvites[invited];
    state.helpInvites[userId] = untilUnixMs;
    return "ok";
}

function partyHandleWorld(nk: nkruntime.Nakama, state: PartyMatchState, nowUnixMs: number): void {
    const host = state.host;
    if (host.worldRequests.length === 0 && host.bossRequests.length === 0)
        return;
    const leader = partyLeaderOf(nk, state.partyId);
    for (const slot of host.bossRequests)
        if (leader === null || host.encounter.members[slot].playerId === leader)
            host.encounter.requestBossFight();
    host.bossRequests.length = 0;
    for (const request of host.worldRequests) {
        if (leader !== null && host.encounter.members[request.slot].playerId !== leader)
            continue;
        if (request.cancel) {
            host.encounter.cancelWorld();
        } else if (request.world === PartyWorld.Home) {
            host.encounter.proposeWorld(PartyWorld.Home, 0);
        } else if (request.world === PartyWorld.Rift && host.encounter.world !== PartyWorld.Rift && !host.encounter.switching) {
            const rift = riftPartyJoin(nk, nowUnixMs);
            if (rift !== null)
                host.encounter.proposeWorld(PartyWorld.Rift, rift.id);
        }
    }
    host.worldRequests.length = 0;
}

function partyRestoreRift(nk: nkruntime.Nakama, logger: nkruntime.Logger, state: PartyMatchState, nowUnixMs: number): void {
    const riftId = riftPartyOf(nk, state.partyId, nowUnixMs);
    if (riftId === 0)
        return;
    state.host.encounter.switchWorld(PartyWorld.Rift, riftId);
    state.host.encounter.restoreRiftStages(riftBoardCleared(nk, riftId, state.partyId));
    state.host.encounter.events.length = 0;
    state.boardRiftId = riftId;
    logger.info("party %s: back in rift %d after a restart", state.partyId, riftId);
}

function partyTrackRift(nk: nkruntime.Nakama, logger: nkruntime.Logger, state: PartyMatchState, nowUnixMs: number): void {
    const host = state.host;
    const current = host.encounter.world === PartyWorld.Rift ? host.encounter.riftId : 0;
    state.boardTicks = (state.boardTicks || 0) + 1;
    const due = state.boardTicks >= Math.max(1, partyTicks(RiftServerRules.boardUpdateMs / 1000));
    const tracked = state.boardRiftId || 0;
    if (tracked !== 0 && (due || current !== tracked)) {
        partyWriteBoard(nk, state, tracked, nowUnixMs);
        state.boardRiftId = current;
        if (current !== tracked)
            riftPartyNote(nk, state.partyId, current);
    } else if (tracked === 0 && current !== 0) {
        state.boardRiftId = current;
        riftPartyNote(nk, state.partyId, current);
        partyWriteBoard(nk, state, current, nowUnixMs);
    }
    if (due) {
        state.boardTicks = 0;
        riftSettleDue(nk, logger, nowUnixMs);
    }
}

function partyWriteBoard(nk: nkruntime.Nakama, state: PartyMatchState, riftId: number, nowUnixMs: number): void {
    if (!state.partyId)
        return;
    const host = state.host;
    const members: string[] = [];
    for (const member of host.encounter.members)
        if (member.joined && !member.guest)
            members.push(member.playerId);
    const group = partyGroupById(nk, state.partyId);
    const title = group ? partyMetaOf(group).title : "";
    const cleared = host.riftCleared;
    host.riftCleared = 0;
    host.chronicle.riftDepth = Math.max(host.chronicle.riftDepth || 0, riftBoardAdd(nk, riftId, state.partyId, title, cleared, members, nowUnixMs));
}

function partyLeaderOf(nk: nkruntime.Nakama, partyId: string): string | null {
    if (!partyId)
        return null;
    const group = partyGroupById(nk, partyId);
    return group ? partyMetaOf(group).leader : null;
}

function partyFlushChronicle(nk: nkruntime.Nakama, logger: nkruntime.Logger, state: PartyMatchState): void {
    const host = state.host;
    const pairs = host.takePairTicks();
    for (const pair of Object.keys(pairs))
        pairs[pair] /= PartyTicksPerSecond;
    host.chronicle.mentorSeconds = (host.chronicle.mentorSeconds || 0) + mentorTogether(nk, logger, pairs, Date.now());
    const delta = host.takeChronicle();
    const members: string[] = [];
    for (const member of host.encounter.members)
        if (member.joined && !member.guest)
            members.push(member.playerId);
    for (const got of chronicleFlush(nk, logger, state.partyId, delta, members))
        logger.info("party %s: feat %s, level %d", state.partyId, got.feat, got.level);
    walletGrantBosses(nk, logger, host.takeFriendBosses(), Date.now());
    walletGrantHelp(nk, logger, host.takeHelpBosses(), Date.now());
}

function partyMatchSignal(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, data: string) {
    state = partyRevive(state);
    const request = data.split(":");
    if (request[0] === "leave" && request[1])
        state.host.leave(request[1]);
    if (request[0] === "invite")
        return { state: state, data: partyInvite(state, request[1] || "", Number(request[2])) };
    return { state: state, data: "ok" };
}

function partySend(dispatcher: nkruntime.MatchDispatcher, state: PartyMatchState, host: PartyHost): void {
    const members = host.encounter.members;
    for (const packet of host.outbox) {
        const targets: nkruntime.Presence[] = [];
        for (const slot of packet.to) {
            const presence = state.presences[members[slot].playerId];
            if (presence)
                targets.push(presence);
        }
        if (targets.length > 0)
            dispatcher.broadcastMessage(new DataView(packet.data).getUint8(0), packet.data, targets, null, true);
    }
    host.outbox.length = 0;
}

function partyUpdateLabel(dispatcher: nkruntime.MatchDispatcher, host: PartyHost): void {
    const open = host.encounter.memberCount() < PartyServerRules.maxMembers;
    dispatcher.matchLabelUpdate(JSON.stringify({ kind: PartyMatchName, open: open }));
}

function partySaveMembership(nk: nkruntime.Nakama, logger: nkruntime.Logger, matchId: string, userId: string, slot: number): void {
    try {
        nk.storageWrite([{ collection: "party", key: "current", userId: userId, value: { matchId: matchId, slot: slot }, permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        logger.warn("party membership was not saved for %s: %s", userId, String(error));
    }
}

function partyReportFlags(nk: nkruntime.Nakama, logger: nkruntime.Logger, matchId: string, host: PartyHost): void {
    for (const slot of host.reports) {
        const member = host.encounter.members[slot];
        logger.warn("party %s: player %s (user %s) has %d capped batches", matchId, host.playerNumbers[slot], member.playerId, host.flags[slot]);
        try {
            partyNoteFlags(nk, member.playerId, host.playerNumbers[slot], host.flags[slot], matchId, Date.now());
        } catch (error) {
            logger.warn("party flags were not saved for %s: %s", member.playerId, String(error));
        }
    }
    host.reports.length = 0;
}
