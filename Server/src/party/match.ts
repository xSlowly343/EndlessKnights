interface PartyMatchState {
    host: PartyHost;
    presences: { [userId: string]: nkruntime.Presence };
    pendingNumbers: { [userId: string]: string };
    emptyTicks: number;
}

const PartyMatchName = "party";
const PartyEmptyTicksBeforeClose = 600;

function partyDecimalOrNull(value: any): string | null {
    return typeof value === "string" && /^[0-9]{1,20}$/.test(value) ? value : null;
}

const partyMatchHandler: nkruntime.MatchHandler<PartyMatchState> = {
    matchInit(ctx, logger, nk, params) {
        const seed = partyDecimalOrNull(params.seed) || String(Date.now());
        return {
            state: { host: new PartyHost(PartyServerRules, seed), presences: {}, pendingNumbers: {}, emptyTicks: 0 },
            tickRate: PartyTicksPerSecond,
            label: JSON.stringify({ kind: PartyMatchName, open: true })
        };
    },

    matchJoinAttempt(ctx, logger, nk, dispatcher, tick, state, presence, metadata) {
        const known = state.host.encounter.slotOf(presence.userId) !== PartyNoSlot;
        if (!known && state.host.encounter.memberCount() >= PartyServerRules.maxMembers)
            return { state: state, accept: false, rejectMessage: "party is full" };
        if (!known && partyDecimalOrNull(metadata.playerNumber) === null)
            return { state: state, accept: false, rejectMessage: "playerNumber is required" };
        if (!known)
            state.pendingNumbers[presence.userId] = metadata.playerNumber;
        return { state: state, accept: true };
    },

    matchJoin(ctx, logger, nk, dispatcher, tick, state, presences) {
        for (const presence of presences) {
            state.presences[presence.userId] = presence;
            const slot = state.host.join(presence.userId, state.pendingNumbers[presence.userId] || "0");
            delete state.pendingNumbers[presence.userId];
            partySaveMembership(nk, logger, ctx.matchId || "", presence.userId, slot);
        }
        partyUpdateLabel(dispatcher, state);
        return { state: state };
    },

    matchLeave(ctx, logger, nk, dispatcher, tick, state, presences) {
        for (const presence of presences) {
            delete state.presences[presence.userId];
            state.host.disconnect(presence.userId);
        }
        return { state: state };
    },

    matchLoop(ctx, logger, nk, dispatcher, tick, state, messages) {
        for (const message of messages)
            state.host.receive(message.sender.userId, message.data);
        state.host.tick();
        partySend(dispatcher, state);
        state.emptyTicks = Object.keys(state.presences).length === 0 ? state.emptyTicks + 1 : 0;
        return state.emptyTicks > PartyEmptyTicksBeforeClose ? null : { state: state };
    },

    matchTerminate(ctx, logger, nk, dispatcher, tick, state) {
        return { state: state };
    },

    matchSignal(ctx, logger, nk, dispatcher, tick, state, data) {
        const request = data.split(":");
        if (request[0] === "leave" && request[1])
            state.host.leave(request[1]);
        return { state: state, data: "ok" };
    }
};

function partySend(dispatcher: nkruntime.MatchDispatcher, state: PartyMatchState): void {
    const members = state.host.encounter.members;
    for (const packet of state.host.outbox) {
        const targets: nkruntime.Presence[] = [];
        for (const slot of packet.to) {
            const presence = state.presences[members[slot].playerId];
            if (presence)
                targets.push(presence);
        }
        if (targets.length > 0)
            dispatcher.broadcastMessage(new DataView(packet.data).getUint8(0), packet.data, targets, null, true);
    }
    state.host.outbox.length = 0;
}

function partyUpdateLabel(dispatcher: nkruntime.MatchDispatcher, state: PartyMatchState): void {
    const open = state.host.encounter.memberCount() < PartyServerRules.maxMembers;
    dispatcher.matchLabelUpdate(JSON.stringify({ kind: PartyMatchName, open: open }));
}

function partySaveMembership(nk: nkruntime.Nakama, logger: nkruntime.Logger, matchId: string, userId: string, slot: number): void {
    try {
        nk.storageWrite([{ collection: "party", key: "current", userId: userId, value: { matchId: matchId, slot: slot }, permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        logger.warn("party membership was not saved for %s: %s", userId, String(error));
    }
}
