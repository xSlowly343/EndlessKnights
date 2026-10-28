interface PartyServerConfig extends PartyEncounterRules {
    silenceSeconds: number;
    presenceSeconds: number;
    stateBroadcastSeconds: number;
    maxMessagesPerSecond: number;
    barsPerSecond: number;
    burstBars: number;
    hitBurstSeconds: number;
    flagsBeforeReport: number;
}

const PartyCutTolerance = 1e-9;

class PartyHost {
    readonly encounter: PartyEncounter;
    readonly outbox: { data: ArrayBuffer; to: number[] }[] = [];
    readonly playerNumbers: string[] = ["0", "0", "0", "0"];
    kills = 0;
    acceptedShares = 0;
    cutShares = 0;
    readonly flags = [0, 0, 0, 0];
    readonly reports: number[] = [];
    private readonly allowance = [0, 0, 0, 0];
    private readonly hitAllowance = [0, 0, 0, 0];
    private readonly lastHeardTick = [0, 0, 0, 0];
    private readonly lastSequence = [0, 0, 0, 0];
    private readonly lastEnemy = [0, 0, 0, 0];
    private readonly lastTotal = [0, 0, 0, 0];
    private readonly messagesThisSecond = [0, 0, 0, 0];
    private readonly sentPresence = [PartyPresence.Fighting, PartyPresence.Fighting, PartyPresence.Fighting, PartyPresence.Fighting];
    private stateSequence = 0;
    private rosterVersion = 0;
    private rosterChanged = false;
    private ticksToState = 0;
    private ticksToRoster = 0;

    constructor(private readonly config: PartyServerConfig, seed: string) {
        this.encounter = new PartyEncounter(config, seed);
    }

    join(playerId: string, playerNumber: string): number {
        const known = this.encounter.slotOf(playerId);
        const slot = this.encounter.join(playerId);
        if (slot === PartyNoSlot)
            return slot;
        this.lastHeardTick[slot] = this.encounter.tick;
        if (known === PartyNoSlot) {
            this.playerNumbers[slot] = playerNumber;
            this.lastSequence[slot] = 0;
            this.lastEnemy[slot] = 0;
            this.lastTotal[slot] = 0;
            this.allowance[slot] = this.config.burstBars;
            this.hitAllowance[slot] = this.hitBurst();
            this.flags[slot] = 0;
            this.ticksToState = 0;
        }
        this.rosterChanged = true;
        return slot;
    }

    leave(playerId: string): void {
        const slot = this.encounter.slotOf(playerId);
        if (slot === PartyNoSlot)
            return;
        this.encounter.leave(slot);
        this.playerNumbers[slot] = "0";
        this.rosterChanged = true;
    }

    disconnect(playerId: string): void {
        const slot = this.encounter.slotOf(playerId);
        if (slot === PartyNoSlot)
            return;
        this.encounter.setConnected(slot, false);
        this.rosterChanged = true;
    }

    receive(playerId: string, data: ArrayBuffer): void {
        const slot = this.encounter.slotOf(playerId);
        if (slot === PartyNoSlot || ++this.messagesThisSecond[slot] > this.config.maxMessagesPerSecond)
            return;
        this.heard(slot);
        switch (partyKindOf(data)) {
            case PartyMessageKind.Hello: {
                const hello = partyDecodeHello(data);
                if (hello && this.encounter.members[slot].heroClass !== hello.heroClass) {
                    this.encounter.setHeroClass(slot, hello.heroClass);
                    this.rosterChanged = true;
                }
                break;
            }
            case PartyMessageKind.DamageBatch: {
                const batch = partyDecodeDamageBatch(data);
                if (batch)
                    this.damage(slot, batch);
                break;
            }
            case PartyMessageKind.Presence: {
                const presence = partyDecodePresence(data);
                if (presence)
                    this.encounter.setPeaceful(slot, presence.presence === PartyPresence.Peaceful);
                break;
            }
            case PartyMessageKind.CastEffect: {
                const effect = partyDecodeCastEffect(data);
                if (effect && effect.onEnemy)
                    this.encounter.applyEnemyEffect(effect.effectId, effect.strength, effect.seconds, slot);
                break;
            }
            case PartyMessageKind.PartyOffer: {
                const relayed = partyRelayOffer(data, slot);
                if (relayed)
                    this.broadcast(relayed, slot);
                break;
            }
        }
    }

    tick(): void {
        this.encounter.advance();
        this.refill();
        if (this.encounter.tick % PartyTicksPerSecond === 0)
            for (let i = 0; i < PartyMaxSlots; i++)
                this.messagesThisSecond[i] = 0;
        this.watchSilence();
        this.watchPresence();
        this.flush();
    }

    private heard(slot: number): void {
        this.lastHeardTick[slot] = this.encounter.tick;
        if (this.encounter.members[slot].connected)
            return;
        this.encounter.setConnected(slot, true);
        this.rosterChanged = true;
    }

    private damage(slot: number, batch: DamageBatchMessage): void {
        if (batch.sequence <= this.lastSequence[slot])
            return;
        this.lastSequence[slot] = batch.sequence;
        const total = partyBatchTotal(batch);
        const delta = batch.enemyId === this.lastEnemy[slot] ? total - this.lastTotal[slot] : total;
        if (batch.enemyId !== this.lastEnemy[slot] || total > this.lastTotal[slot]) {
            this.lastEnemy[slot] = batch.enemyId;
            this.lastTotal[slot] = total;
        }
        if (batch.hits > this.hitAllowance[slot])
            this.flag(slot);
        this.hitAllowance[slot] = Math.max(0, this.hitAllowance[slot] - Math.max(0, batch.hits));
        if (batch.hits > 0)
            this.encounter.reportInput(slot);
        if (delta > 0)
            this.acceptedShares += this.cappedDamage(slot, batch.enemyId, delta);
    }

    private cappedDamage(slot: number, enemyId: number, delta: number): number {
        const barFull = this.encounter.barFull;
        const wanted = this.encounter.applicable(slot, enemyId, delta);
        const allowed = Math.min(wanted, this.allowance[slot] * barFull);
        if (wanted - allowed > PartyCutTolerance * barFull) {
            this.cutShares += wanted - allowed;
            this.flag(slot);
        }
        const applied = this.encounter.applyDamage(slot, enemyId, delta, allowed);
        this.allowance[slot] = Math.max(0, this.allowance[slot] - applied / barFull);
        return applied;
    }

    private flag(slot: number): void {
        this.flags[slot]++;
        if (this.flags[slot] % this.config.flagsBeforeReport === 0)
            this.reports.push(slot);
    }

    private refill(): void {
        const step = this.config.barsPerSecond / PartyTicksPerSecond;
        const hitBurst = this.hitBurst();
        for (let slot = 0; slot < PartyMaxSlots; slot++) {
            this.allowance[slot] = Math.min(this.config.burstBars, this.allowance[slot] + step);
            this.hitAllowance[slot] = Math.min(hitBurst, this.hitAllowance[slot] + 1);
        }
    }

    private hitBurst(): number {
        return this.config.hitBurstSeconds * PartyTicksPerSecond;
    }

    private watchSilence(): void {
        const silence = partyTicks(this.config.silenceSeconds);
        for (let slot = 0; slot < PartyMaxSlots; slot++) {
            const member = this.encounter.members[slot];
            if (!member.joined || !member.connected || this.encounter.tick - this.lastHeardTick[slot] <= silence)
                continue;
            this.encounter.setConnected(slot, false);
            this.rosterChanged = true;
        }
    }

    private watchPresence(): void {
        for (let slot = 0; slot < PartyMaxSlots; slot++) {
            const presence = this.encounter.members[slot].joined ? this.encounter.presence(slot) : PartyPresence.Offline;
            if (presence === this.sentPresence[slot])
                continue;
            this.sentPresence[slot] = presence;
            this.rosterChanged = true;
        }
    }

    private flush(): void {
        const events = this.encounter.events;
        for (const event of events) {
            if (event.kind === PartyEventKind.EnemyKilled) {
                this.kills++;
                this.broadcast(partyEncodeEnemyKilled(event), PartyNoSlot);
            } else if (event.kind === PartyEventKind.EnemySpawned) {
                this.ticksToState = 0;
            } else {
                this.broadcast(partyEncodeMoment(event, this.encounter.bossTicksLeft), PartyNoSlot);
            }
        }
        events.length = 0;
        if (--this.ticksToRoster <= 0 || this.rosterChanged) {
            this.ticksToRoster = Math.max(1, partyTicks(this.config.presenceSeconds));
            this.rosterChanged = false;
            this.broadcast(partyEncodeRoster(this.encounter, ++this.rosterVersion, this.playerNumbers), PartyNoSlot);
        }
        if (--this.ticksToState <= 0 && this.encounter.started) {
            this.ticksToState = Math.max(1, partyTicks(this.config.stateBroadcastSeconds));
            this.broadcast(partyEncodeEncounterState(this.encounter, ++this.stateSequence), PartyNoSlot);
        }
    }

    private broadcast(data: ArrayBuffer, skipped: number): void {
        const to: number[] = [];
        for (let slot = 0; slot < PartyMaxSlots; slot++)
            if (this.encounter.members[slot].joined && slot !== skipped)
                to.push(slot);
        if (to.length > 0)
            this.outbox.push({ data: data, to: to });
    }
}
