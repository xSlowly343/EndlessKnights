interface RiftParty {
    id: number;
    windowId: number;
    startUnixMs: number;
    endUnixMs: number;
    joinUntilUnixMs: number;
}

const RiftPartyCollection = "rift_party";
const RiftPartyOpenKey = "open";
const RiftPartyWriteAttempts = 3;
const RiftPartyOfCollection = "rift_party_of";

function riftPartyEnd(riftId: number): number {
    return riftId * 1000 + RiftServerRules.partyDurationMs;
}

function riftPartyJoin(nk: nkruntime.Nakama, nowUnixMs: number): RiftParty | null {
    const window = riftWindowAt(RiftServerRules, nowUnixMs);
    if (window === null)
        return null;
    for (let attempt = 0; attempt < RiftPartyWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: RiftPartyCollection, key: RiftPartyOpenKey, userId: PartySystemUser }]);
        const open = objects.length > 0 ? objects[0].value as RiftParty : null;
        if (open !== null && open.windowId === window.id && nowUnixMs < open.joinUntilUnixMs)
            return open;
        const id = Math.floor(nowUnixMs / 1000);
        const created: RiftParty = { id: id, windowId: window.id, startUnixMs: nowUnixMs, endUnixMs: riftPartyEnd(id), joinUntilUnixMs: nowUnixMs + RiftServerRules.partyJoinMs };
        try {
            nk.storageWrite([{
                collection: RiftPartyCollection, key: RiftPartyOpenKey, userId: PartySystemUser, value: created,
                version: objects.length > 0 ? objects[0].version : "*", permissionRead: 0, permissionWrite: 0
            }]);
            return created;
        } catch (error) {
            continue;
        }
    }
    return null;
}

function riftPartyOf(nk: nkruntime.Nakama, partyId: string, nowUnixMs: number): number {
    if (!partyId)
        return 0;
    const objects = nk.storageRead([{ collection: RiftPartyOfCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return 0;
    const riftId = (objects[0].value as { riftId: number }).riftId || 0;
    return riftId !== 0 && nowUnixMs < riftPartyEnd(riftId) ? riftId : 0;
}

function riftPartyNote(nk: nkruntime.Nakama, partyId: string, riftId: number): void {
    if (!partyId)
        return;
    if (riftId === 0) {
        nk.storageDelete([{ collection: RiftPartyOfCollection, key: partyId, userId: PartySystemUser }]);
        return;
    }
    nk.storageWrite([{ collection: RiftPartyOfCollection, key: partyId, userId: PartySystemUser, value: { riftId: riftId }, permissionRead: 0, permissionWrite: 0 }]);
}
