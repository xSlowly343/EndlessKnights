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
