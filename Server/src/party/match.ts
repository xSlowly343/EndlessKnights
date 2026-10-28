interface PartyMatchState {
    host: PartyHost;
    partyId: string;
    presences: { [userId: string]: nkruntime.Presence };
    pendingNumbers: { [userId: string]: string };
    emptyTicks: number;
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
    const state: PartyMatchState = { host: new PartyHost(PartyServerRules, seed), partyId: partyId, presences: {}, pendingNumbers: {}, emptyTicks: 0 };
    return { state: state, tickRate: PartyTicksPerSecond, label: JSON.stringify({ kind: PartyMatchName, open: true }) };
}

function partyMatchJoinAttempt(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, presence: nkruntime.Presence, metadata: { [key: string]: any }) {
    state = partyRevive(state);
    const host = state.host;
    const known = host.encounter.slotOf(presence.userId) !== PartyNoSlot;
    if (state.partyId && !partyMatchMember(nk, state.partyId, presence.userId))
        return { state: state, accept: false, rejectMessage: "not a member of this party" };
    if (!known && host.encounter.memberCount() >= PartyServerRules.maxMembers)
        return { state: state, accept: false, rejectMessage: "party is full" };
    if (!known && partyDecimalOrNull(metadata.playerNumber) === null)
        return { state: state, accept: false, rejectMessage: "playerNumber is required" };
    if (!known)
        state.pendingNumbers[presence.userId] = metadata.playerNumber;
    return { state: state, accept: true };
}

function partyMatchJoin(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, presences: nkruntime.Presence[]) {
    state = partyRevive(state);
    const host = state.host;
    for (const presence of presences) {
        state.presences[presence.userId] = presence;
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
    host.tick();
    partySend(dispatcher, state, host);
    partyReportFlags(nk, logger, ctx.matchId || "", host);
    state.emptyTicks = Object.keys(state.presences).length === 0 ? state.emptyTicks + 1 : 0;
    return state.emptyTicks > PartyEmptyTicksBeforeClose ? null : { state: state };
}

function partyMatchTerminate(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, graceSeconds: number) {
    return { state: state };
}

function partyMatchSignal(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher,
    tick: number, state: PartyMatchState, data: string) {
    state = partyRevive(state);
    const request = data.split(":");
    if (request[0] === "leave" && request[1])
        state.host.leave(request[1]);
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
            nk.storageWrite([{
                collection: "party_flags", key: member.playerId, userId: member.playerId,
                value: { flags: host.flags[slot], matchId: matchId, playerNumber: host.playerNumbers[slot], at: Date.now() },
                permissionRead: 0, permissionWrite: 0
            }]);
        } catch (error) {
            logger.warn("party flags were not saved for %s: %s", member.playerId, String(error));
        }
    }
    host.reports.length = 0;
}
