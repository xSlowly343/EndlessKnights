const PartyMaxPacketBytes = 256;

const enum PartyMessageKind {
    None = 0, Hello = 1, DamageBatch = 2, CastEffect = 3, Presence = 4,
    EncounterState = 5, EnemyKilled = 6, EncounterMoment = 7, Roster = 8, PartyOffer = 9, PartyReaction = 10, WorldRequest = 11, FocusRelay = 12, PartyResource = 13, AllyCast = 14, Synergy = 15
}

class PartyWriter {
    private readonly view: DataView;
    length = 0;

    constructor(readonly buffer: ArrayBuffer) {
        this.view = new DataView(buffer);
    }

    byte(value: number): void {
        if (this.length >= this.buffer.byteLength)
            throw new Error("party packet is larger than " + this.buffer.byteLength + " bytes");
        this.view.setUint8(this.length++, value & 0xff);
    }

    bool(value: boolean): void { this.byte(value ? 1 : 0); }

    uint(value: number): void {
        this.ensure(4);
        this.view.setUint32(this.length, value >>> 0, true);
        this.length += 4;
    }

    int(value: number): void {
        this.ensure(4);
        this.view.setInt32(this.length, value | 0, true);
        this.length += 4;
    }

    long(value: number): void {
        const low = value % 4294967296;
        const lowPart = low < 0 ? low + 4294967296 : low;
        this.uint(lowPart);
        this.int(Math.floor(value / 4294967296));
    }

    ulong(decimal: string): void {
        const limbs = u64FromDecimal(decimal);
        for (const limb of limbs) {
            this.byte(limb & 0xff);
            this.byte(limb >>> 8);
        }
    }

    double(value: number): void {
        this.ensure(8);
        this.view.setFloat64(this.length, value, true);
        this.length += 8;
    }

    float(value: number): void {
        this.ensure(4);
        this.view.setFloat32(this.length, value, true);
        this.length += 4;
    }

    bytes(): ArrayBuffer {
        return this.buffer.slice(0, this.length);
    }

    private ensure(count: number): void {
        if (this.length + count > this.buffer.byteLength)
            throw new Error("party packet is larger than " + this.buffer.byteLength + " bytes");
    }
}

class PartyReader {
    private readonly view: DataView;
    private position = 0;
    private failed = false;

    constructor(data: ArrayBuffer) {
        this.view = new DataView(data);
    }

    get complete(): boolean { return !this.failed && this.position === this.view.byteLength; }

    byte(): number { return this.take(1) ? this.view.getUint8(this.position - 1) : 0; }
    bool(): boolean { return this.byte() !== 0; }
    uint(): number { return this.take(4) ? this.view.getUint32(this.position - 4, true) : 0; }
    int(): number { return this.take(4) ? this.view.getInt32(this.position - 4, true) : 0; }
    double(): number { return this.take(8) ? this.view.getFloat64(this.position - 8, true) : 0; }
    float(): number { return this.take(4) ? this.view.getFloat32(this.position - 4, true) : 0; }

    long(): number {
        const low = this.uint();
        const high = this.int();
        return high * 4294967296 + low;
    }

    private take(count: number): boolean {
        if (this.failed || this.position + count > this.view.byteLength) {
            this.failed = true;
            return false;
        }
        this.position += count;
        return true;
    }
}

interface HelloMessage { heroClass: number; stage: number; sharePerSecondCap: number; }
interface DamageBatchMessage { sequence: number; enemyId: number; keys: number; mouse: number; skills: number; dot: number; summons: number; hits: number; crits: number; }
interface CastEffectMessage { effectId: number; onEnemy: boolean; strength: number; seconds: number; timerSlow: number; repeatSeconds: number; weakenEnemies: number; weakenShare: number; }
interface PresenceMessage { presence: PartyPresence; stage: number; resource: number; }
interface WorldRequestMessage { world: PartyWorld; cancel: boolean; riftId: number; }
interface FocusRelayMessage { from: number; to: number; focus: number; }

function partyKindOf(data: ArrayBuffer): PartyMessageKind {
    if (data.byteLength === 0)
        return PartyMessageKind.None;
    const kind = new DataView(data).getUint8(0);
    return kind <= PartyMessageKind.Synergy ? kind : PartyMessageKind.None;
}

function partyOpen(data: ArrayBuffer, kind: PartyMessageKind): PartyReader {
    const reader = new PartyReader(data);
    if (reader.byte() !== kind)
        return new PartyReader(new ArrayBuffer(0));
    return reader;
}

function partyDecodeHello(data: ArrayBuffer): HelloMessage | null {
    const r = partyOpen(data, PartyMessageKind.Hello);
    const message = { heroClass: r.byte(), stage: r.long(), sharePerSecondCap: r.double() };
    return r.complete ? message : null;
}

function partyDecodeDamageBatch(data: ArrayBuffer): DamageBatchMessage | null {
    const r = partyOpen(data, PartyMessageKind.DamageBatch);
    const message = {
        sequence: r.uint(), enemyId: r.uint(), keys: r.double(), mouse: r.double(), skills: r.double(),
        dot: r.double(), summons: r.double(), hits: r.int(), crits: r.int()
    };
    return r.complete ? message : null;
}

function partyBatchTotal(batch: DamageBatchMessage): number {
    return batch.keys + batch.mouse + batch.skills + batch.dot + batch.summons;
}

function partyDecodeCastEffect(data: ArrayBuffer): CastEffectMessage | null {
    const r = partyOpen(data, PartyMessageKind.CastEffect);
    const message = { effectId: r.int(), onEnemy: r.bool(), strength: r.double(), seconds: r.double(), timerSlow: r.double(), repeatSeconds: r.double(), weakenEnemies: r.byte(), weakenShare: r.double() };
    return r.complete ? message : null;
}

function partyDecodePresence(data: ArrayBuffer): PresenceMessage | null {
    const r = partyOpen(data, PartyMessageKind.Presence);
    const message = { presence: r.byte(), stage: r.long(), resource: r.float() };
    return r.complete ? message : null;
}

function partyDecodeWorldRequest(data: ArrayBuffer): WorldRequestMessage | null {
    const r = partyOpen(data, PartyMessageKind.WorldRequest);
    const message = { world: r.byte(), cancel: r.bool(), riftId: r.long() };
    return r.complete ? message : null;
}

function partyDecodeFocusRelay(data: ArrayBuffer): FocusRelayMessage | null {
    const r = partyOpen(data, PartyMessageKind.FocusRelay);
    const message = { from: r.byte(), to: r.byte(), focus: r.float() };
    return r.complete ? message : null;
}

function partyEncodeFocusRelay(from: number, to: number, focus: number): ArrayBuffer {
    const w = partyStart(PartyMessageKind.FocusRelay);
    w.byte(from);
    w.byte(to);
    w.float(focus);
    return w.bytes();
}

function partyRelayResource(data: ArrayBuffer, slot: number, max: number): ArrayBuffer | null {
    const r = partyOpen(data, PartyMessageKind.PartyResource);
    r.byte();
    const amount = r.float();
    if (!r.complete || !(amount > 0) || !isFinite(amount))
        return null;
    const w = partyStart(PartyMessageKind.PartyResource);
    w.byte(slot);
    w.float(Math.min(max, amount));
    return w.bytes();
}

function partyDecodeSynergy(data: ArrayBuffer, reactions: number): number | null {
    const r = partyOpen(data, PartyMessageKind.Synergy);
    const mask = r.uint();
    if (!r.complete)
        return null;
    const known = Math.pow(2, Math.min(32, reactions)) - 1;
    return mask % (known + 1);
}

function partyBitCount(mask: number): number {
    let count = 0;
    for (let rest = mask >>> 0; rest !== 0; rest = (rest & (rest - 1)) >>> 0)
        count++;
    return count;
}

function partyRelayAllyCast(data: ArrayBuffer, slot: number): ArrayBuffer | null {
    if (partyKindOf(data) !== PartyMessageKind.AllyCast || data.byteLength !== 6)
        return null;
    const copy = data.slice(0);
    new DataView(copy).setUint8(1, slot);
    return copy;
}

function partyRelayOffer(data: ArrayBuffer, slot: number): ArrayBuffer | null {
    if (partyKindOf(data) !== PartyMessageKind.PartyOffer || data.byteLength < 2)
        return null;
    const copy = data.slice(0);
    new DataView(copy).setUint8(1, slot);
    return copy;
}

function partyRelayReaction(data: ArrayBuffer, slot: number, known: number): ArrayBuffer | null {
    if (partyKindOf(data) !== PartyMessageKind.PartyReaction || data.byteLength !== 3)
        return null;
    const copy = data.slice(0);
    const view = new DataView(copy);
    if (view.getUint8(2) >= known)
        return null;
    view.setUint8(1, slot);
    return copy;
}

function partyStart(kind: PartyMessageKind): PartyWriter {
    const writer = new PartyWriter(new ArrayBuffer(PartyMaxPacketBytes));
    writer.byte(kind);
    return writer;
}

function partyEncodeEncounterState(encounter: PartyEncounter, sequence: number, recent?: number[], hits?: number[]): ArrayBuffer {
    const w = partyStart(PartyMessageKind.EncounterState);
    w.uint(sequence);
    w.long(encounter.tick);
    w.uint(encounter.enemyId);
    w.byte(encounter.enemyKind);
    w.double(encounter.barLeft);
    w.double(encounter.barFull);
    w.int(encounter.bossTicksLeft);
    w.byte(Math.min(255, encounter.killsOnStage));
    w.bool(encounter.farming);
    w.byte(encounter.weightMask());
    w.double(encounter.damageTakenMultiplier());
    w.ulong(encounter.enemySeed);
    let shown = 0;
    for (const effect of encounter.effects) {
        if (shown === 4 || !(effect.ticksLeft > 0))
            continue;
        w.int(effect.id);
        w.int(effect.ticksLeft);
        w.byte(effect.owner >= 0 && effect.owner < PartyMaxSlots ? effect.owner : 255);
        shown++;
    }
    for (; shown < 4; shown++) {
        w.int(0);
        w.int(0);
        w.byte(0);
    }
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.float(recent ? recent[slot] || 0 : 0);
    w.float(encounter.fightingBoss ? encounter.bossTimerRate() : 1);
    w.byte(encounter.world);
    w.byte(encounter.pendingWorld);
    w.byte(Math.min(255, encounter.switchTicksLeft));
    w.long(encounter.riftId);
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.byte(Math.min(255, Math.max(0, hits ? hits[slot] || 0 : 0)));
    return w.bytes();
}

function partyEncodeEnemyKilled(event: PartyEvent): ArrayBuffer {
    const w = partyStart(PartyMessageKind.EnemyKilled);
    w.long(event.tick);
    w.uint(event.enemyId);
    w.byte(event.enemyKind);
    for (let i = 0; i < PartyMaxSlots; i++)
        w.double(event.shares[i]);
    w.byte(event.rewarded);
    w.float(event.loop || 0);
    w.float(event.saved || 0);
    return w.bytes();
}

function partyEncodeMoment(event: PartyEvent, bossTicksLeft: number): ArrayBuffer {
    const w = partyStart(PartyMessageKind.EncounterMoment);
    w.byte(event.kind);
    w.long(event.tick);
    w.uint(event.enemyId);
    w.int(bossTicksLeft);
    return w.bytes();
}

function partyEncodeRoster(encounter: PartyEncounter, version: number, playerNumbers: string[]): ArrayBuffer {
    const w = partyStart(PartyMessageKind.Roster);
    w.uint(version);
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.ulong(encounter.members[slot].joined ? playerNumbers[slot] : "0");
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.byte(encounter.members[slot].joined ? encounter.presence(slot) : PartyPresence.Offline);
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.byte(encounter.members[slot].joined ? encounter.members[slot].heroClass : 0);
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        w.uint(encounter.members[slot].joined ? Math.floor(encounter.members[slot].restBankTicks / PartyTicksPerSecond) : 0);
    let guests = 0;
    for (let slot = 0; slot < PartyMaxSlots; slot++)
        if (encounter.members[slot].joined && encounter.members[slot].guest)
            guests |= 1 << slot;
    w.byte(guests);
    return w.bytes();
}
