interface PartyServerConfig extends PartyEncounterRules {
    silenceSeconds: number;
    presenceSeconds: number;
    biomeLength: number;
    stateBroadcastSeconds: number;
    maxMessagesPerSecond: number;
    barsPerSecond: number;
    burstBars: number;
    hitBurstSeconds: number;
    flagsBeforeReport: number;
    reactionCount: number;
    reactionCooldownSeconds: number;
    repeatMaxSeconds: number;
    repeatCooldownSeconds: number;
}

const PartyCutTolerance = 1e-9;

class PartyHost {
    readonly encounter: PartyEncounter;
    readonly outbox: { data: ArrayBuffer; to: number[] }[] = [];
    readonly playerNumbers: string[] = ["0", "0", "0", "0"];
    kills = 0;
    acceptedShares = 0;
    cutShares = 0;
    repeatedShares = 0;
    clockUnixMs = 0;
    readonly chronicle: ChronicleStats = { bossesTogether: 0, ticksTogether: 0, fullOrchestra: 0, nightBosses: 0, riftDepth: 0, mentorSeconds: 0, guardiansTogether: 0 };
    readonly stages = [0, 0, 0, 0];
    friendBosses: { [userId: string]: number } = {};
    pairTicks: { [pair: string]: number } = {};
    readonly flags = [0, 0, 0, 0];
    readonly reports: number[] = [];
    readonly worldRequests: { slot: number; world: PartyWorld; cancel: boolean }[] = [];
    riftCleared = 0;
    private readonly allowance = [0, 0, 0, 0];
    private readonly hitAllowance = [0, 0, 0, 0];
    private readonly lastHeardTick = [0, 0, 0, 0];
    private readonly lastSequence = [0, 0, 0, 0];
    private readonly lastEnemy = [0, 0, 0, 0];
    private readonly lastTotal = [0, 0, 0, 0];
    private readonly messagesThisSecond = [0, 0, 0, 0];
    private readonly sentPresence = [PartyPresence.Fighting, PartyPresence.Fighting, PartyPresence.Fighting, PartyPresence.Fighting];
    private readonly lastReactionTick = [PartyNever, PartyNever, PartyNever, PartyNever];
    private readonly lastRepeatTick = [PartyNever, PartyNever, PartyNever, PartyNever];
    private readonly recentShares = [0, 0, 0, 0];
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
            this.stages[slot] = 0;
            this.lastSequence[slot] = 0;
            this.lastEnemy[slot] = 0;
            this.lastTotal[slot] = 0;
            this.allowance[slot] = this.config.burstBars;
            this.hitAllowance[slot] = this.hitBurst();
            this.flags[slot] = 0;
            this.lastReactionTick[slot] = PartyNever;
            this.lastRepeatTick[slot] = PartyNever;
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
                if (hello)
                    this.stages[slot] = hello.stage;
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
                if (presence) {
                    this.encounter.setPeaceful(slot, presence.presence === PartyPresence.Peaceful);
                    this.stages[slot] = presence.stage;
                }
                break;
            }
            case PartyMessageKind.CastEffect: {
                const effect = partyDecodeCastEffect(data);
                if (effect && effect.onEnemy)
                    this.cast(slot, effect);
                break;
            }
            case PartyMessageKind.PartyOffer: {
                const relayed = partyRelayOffer(data, slot);
                if (relayed)
                    this.broadcast(relayed, slot);
                break;
            }
            case PartyMessageKind.WorldRequest: {
                const request = partyDecodeWorldRequest(data);
                if (request)
                    this.worldRequests.push({ slot: slot, world: request.world, cancel: request.cancel });
                break;
            }
            case PartyMessageKind.PartyReaction: {
                const relayed = partyRelayReaction(data, slot, this.config.reactionCount);
                if (relayed && this.encounter.tick - this.lastReactionTick[slot] >= Math.max(1, partyTicks(this.config.reactionCooldownSeconds))) {
                    this.lastReactionTick[slot] = this.encounter.tick;
                    this.broadcast(relayed, slot);
                }
                break;
            }
        }
    }

    tick(nowUnixMs?: number): void {
        if (nowUnixMs !== undefined)
            this.clockUnixMs = nowUnixMs;
        this.encounter.advance();
        this.returnWhenTheRiftCloses();
        this.refill();
        if (this.encounter.tick % PartyTicksPerSecond === 0)
            for (let i = 0; i < PartyMaxSlots; i++)
                this.messagesThisSecond[i] = 0;
        this.watchSilence();
        this.watchPresence();
        this.noteTogether();
        this.flush();
    }

    takeChronicle(): ChronicleStats {
        const taken = {
            bossesTogether: this.chronicle.bossesTogether, ticksTogether: this.chronicle.ticksTogether, fullOrchestra: this.chronicle.fullOrchestra,
            nightBosses: this.chronicle.nightBosses, riftDepth: this.chronicle.riftDepth || 0, mentorSeconds: this.chronicle.mentorSeconds || 0,
            guardiansTogether: this.chronicle.guardiansTogether || 0
        };
        this.chronicle.guardiansTogether = 0;
        this.chronicle.riftDepth = 0;
        this.chronicle.mentorSeconds = 0;
        this.chronicle.nightBosses = 0;
        this.chronicle.bossesTogether = 0;
        this.chronicle.ticksTogether = 0;
        this.chronicle.fullOrchestra = 0;
        return taken;
    }

    takePairTicks(): { [pair: string]: number } {
        const taken = this.pairTicks;
        this.pairTicks = {};
        return taken;
    }

    takeFriendBosses(): { [userId: string]: number } {
        const taken = this.friendBosses;
        this.friendBosses = {};
        return taken;
    }

    private noteTogether(): void {
        const fighting: string[] = [];
        for (let slot = 0; slot < PartyMaxSlots; slot++)
            if (this.encounter.members[slot].joined && this.encounter.presence(slot) === PartyPresence.Fighting)
                fighting.push(this.encounter.members[slot].playerId);
        if (fighting.length < 2)
            return;
        this.chronicle.ticksTogether++;
        fighting.sort();
        for (let a = 0; a < fighting.length; a++)
            for (let b = a + 1; b < fighting.length; b++) {
                const pair = fighting[a] + "|" + fighting[b];
                this.pairTicks[pair] = (this.pairTicks[pair] || 0) + 1;
            }
    }

    private noteBossKill(event: PartyEvent): void {
        this.noteFriendBoss(event);
        const roles: string[] = [];
        let guardian = false;
        for (let slot = 0; slot < PartyMaxSlots; slot++)
            if (event.shares[slot] > 0 && this.encounter.members[slot].joined) {
                roles.push(chronicleRoleOf(this.encounter.members[slot].heroClass));
                guardian = guardian || this.onGuardianStage(slot);
            }
        if (roles.length < 2)
            return;
        this.chronicle.bossesTogether++;
        if (guardian)
            this.chronicle.guardiansTogether = (this.chronicle.guardiansTogether || 0) + 1;
        if (chronicleFullOrchestra(roles))
            this.chronicle.fullOrchestra++;
        if (chronicleAtNight(this.clockUnixMs, ChronicleServerRules))
            this.chronicle.nightBosses++;
    }

    private onGuardianStage(slot: number): boolean {
        const stage = this.stages[slot];
        return stage > 0 && this.encounter.world === PartyWorld.Home && stage % this.config.biomeLength === 0;
    }

    private noteFriendBoss(event: PartyEvent): void {
        const paid: string[] = [];
        for (let slot = 0; slot < PartyMaxSlots; slot++)
            if ((event.rewarded & (1 << slot)) !== 0 && event.shares[slot] > 0 && this.encounter.members[slot].joined)
                paid.push(this.encounter.members[slot].playerId);
        if (paid.length < 2)
            return;
        for (const userId of paid)
            this.friendBosses[userId] = (this.friendBosses[userId] || 0) + 1;
    }

    private returnWhenTheRiftCloses(): void {
        if (this.encounter.world === PartyWorld.Rift && this.clockUnixMs >= riftPartyEnd(this.encounter.riftId))
            this.encounter.switchWorld(PartyWorld.Home, 0);
    }

    private cast(slot: number, effect: CastEffectMessage): void {
        if (!(effect.repeatSeconds > 0)) {
            this.encounter.applyEnemyEffect(effect.effectId, effect.strength, effect.seconds, slot, effect.timerSlow);
            return;
        }
        if (this.encounter.tick - this.lastRepeatTick[slot] < Math.max(1, partyTicks(this.config.repeatCooldownSeconds)))
            return;
        this.lastRepeatTick[slot] = this.encounter.tick;
        const applied = this.encounter.repeatRecent(slot, Math.min(this.config.repeatMaxSeconds, effect.repeatSeconds));
        this.acceptedShares += applied;
        this.repeatedShares += applied;
        this.recentShares[slot] += applied;
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
        if (!(delta > 0))
            return;
        const accepted = this.cappedDamage(slot, batch.enemyId, delta);
        this.acceptedShares += accepted;
        this.recentShares[slot] += accepted;
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
                if (event.enemyKind === PartyEnemyKind.Boss)
                    this.noteBossKill(event);
                this.broadcast(partyEncodeEnemyKilled(event), PartyNoSlot);
            } else if (event.kind === PartyEventKind.StageCleared && this.encounter.world === PartyWorld.Rift) {
                this.riftCleared++;
                this.broadcast(partyEncodeMoment(event, this.encounter.bossTicksLeft), PartyNoSlot);
            } else if (event.kind === PartyEventKind.EnemySpawned || event.kind === PartyEventKind.WorldPending
                || event.kind === PartyEventKind.WorldCancelled || event.kind === PartyEventKind.WorldChanged) {
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
            this.broadcast(partyEncodeEncounterState(this.encounter, ++this.stateSequence, this.recentShares), PartyNoSlot);
            for (let slot = 0; slot < PartyMaxSlots; slot++)
                this.recentShares[slot] = 0;
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
