interface PartyEvent {
    kind: PartyEventKind;
    tick: number;
    enemyId: number;
    enemyKind: PartyEnemyKind;
    shares: number[];
}

class PartyMember {
    playerId = "";
    joined = false;
    peaceful = false;
    connected = false;
    lastInputTick = 0;
    disconnectedAtTick = 0;
    weight = 0;
    enemyShare = 0;
    heroClass = 0;

    reset(): void {
        this.playerId = "";
        this.joined = false;
        this.peaceful = false;
        this.connected = false;
        this.lastInputTick = 0;
        this.disconnectedAtTick = 0;
        this.weight = 0;
        this.enemyShare = 0;
        this.heroClass = 0;
    }
}

interface PartyEnemyEffect {
    id: number;
    damageTaken: number;
    ticksLeft: number;
    owner: number;
}

const PartyMaxEffects = 16;

class PartyEncounter {
    readonly members: PartyMember[] = [];
    readonly effects: PartyEnemyEffect[] = [];
    readonly events: PartyEvent[] = [];
    tick = 0;
    enemyId = 0;
    enemyKind = PartyEnemyKind.Normal;
    killsOnStage = 0;
    farming = false;
    farmKills = 0;
    bossTicksLeft = 0;
    barFull = 0;
    barLeft = 0;
    fighters = 0;
    private previousEnemyId = 0;
    private previousKind = PartyEnemyKind.Normal;
    private previousKilledTick = -Infinity;

    constructor(private rules: PartyEncounterRules, private readonly seed: string) {
        for (let i = 0; i < PartyMaxSlots; i++)
            this.members.push(new PartyMember());
        for (let i = 0; i < PartyMaxEffects; i++)
            this.effects.push({ id: 0, damageTaken: 0, ticksLeft: 0, owner: PartyNoSlot });
    }

    get started(): boolean { return this.enemyId !== 0; }
    get fightingBoss(): boolean { return this.enemyKind === PartyEnemyKind.Boss; }
    get enemySeed(): string { return partySeedFor(this.seed, this.enemyId); }

    useRules(rules: PartyEncounterRules): void {
        this.rules = rules;
    }

    memberCount(): number {
        let count = 0;
        for (const member of this.members)
            if (member.joined)
                count++;
        return count;
    }

    slotOf(playerId: string): number {
        for (let i = 0; i < this.members.length; i++)
            if (this.members[i].joined && this.members[i].playerId === playerId)
                return i;
        return PartyNoSlot;
    }

    join(playerId: string): number {
        const existing = this.slotOf(playerId);
        if (existing !== PartyNoSlot) {
            this.setConnected(existing, true);
            return existing;
        }
        if (this.memberCount() >= this.rules.maxMembers)
            return PartyNoSlot;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            if (member.joined)
                continue;
            member.reset();
            member.playerId = playerId;
            member.joined = true;
            member.connected = true;
            member.lastInputTick = this.tick;
            if (!this.started)
                this.spawnNext();
            return i;
        }
        return PartyNoSlot;
    }

    leave(slot: number): void {
        if (!this.isMember(slot))
            return;
        const member = this.members[slot];
        const hadWeight = member.weight > 0;
        member.reset();
        if (hadWeight)
            this.dropFighterMidFight();
    }

    reportInput(slot: number): void {
        if (this.isMember(slot))
            this.members[slot].lastInputTick = this.tick;
    }

    setPeaceful(slot: number, peaceful: boolean): void {
        if (this.isMember(slot))
            this.members[slot].peaceful = peaceful;
    }

    setHeroClass(slot: number, heroClass: number): void {
        if (this.isMember(slot))
            this.members[slot].heroClass = heroClass;
    }

    setConnected(slot: number, connected: boolean): void {
        if (!this.isMember(slot))
            return;
        const member = this.members[slot];
        if (member.connected === connected)
            return;
        member.connected = connected;
        if (!connected)
            member.disconnectedAtTick = this.tick;
    }

    presence(slot: number): PartyPresence {
        const member = this.members[slot];
        if (!member.connected)
            return PartyPresence.Offline;
        if (member.peaceful)
            return PartyPresence.Peaceful;
        return this.tick - member.lastInputTick > partyTicks(this.rules.restAfterSeconds) ? PartyPresence.Resting : PartyPresence.Fighting;
    }

    damageTakenMultiplier(): number {
        let total = 0;
        for (const effect of this.effects)
            if (effect.ticksLeft > 0)
                total += effect.damageTaken;
        return 1 + Math.min(this.rules.maxEnemyDamageTaken, total);
    }

    applyEnemyEffect(effectId: number, damageTaken: number, seconds: number, owner: number = PartyNoSlot): boolean {
        if (!(damageTaken > 0) || !(seconds > 0) || !this.started)
            return false;
        const ticks = partyTicks(seconds);
        let free = PartyNoSlot;
        for (let i = 0; i < this.effects.length; i++) {
            const effect = this.effects[i];
            if (effect.ticksLeft > 0 && effect.id === effectId) {
                if (damageTaken < effect.damageTaken)
                    return false;
                effect.damageTaken = damageTaken;
                effect.ticksLeft = Math.max(effect.ticksLeft, ticks);
                effect.owner = owner;
                return true;
            }
            if (free === PartyNoSlot && !(effect.ticksLeft > 0))
                free = i;
        }
        if (free === PartyNoSlot)
            return false;
        this.effects[free] = { id: effectId, damageTaken: damageTaken, ticksLeft: ticks, owner: owner };
        return true;
    }

    applyDamage(slot: number, enemyId: number, share: number): number {
        if (!this.isMember(slot) || !this.started || !(share > 0) || share === Infinity)
            return 0;
        if (enemyId !== this.enemyId) {
            if (!this.isLate(enemyId))
                return 0;
            share *= this.kindRatio(this.previousKind, this.enemyKind);
        }
        const applied = Math.min(this.barLeft, share * this.damageTakenMultiplier());
        this.members[slot].enemyShare += applied;
        this.barLeft -= applied;
        if (this.barLeft <= 0)
            this.kill();
        return applied;
    }

    requestBossFight(): boolean {
        if (!this.started || !this.farming || this.fightingBoss)
            return false;
        this.farming = false;
        this.spawn(PartyEnemyKind.Boss);
        return true;
    }

    advance(): void {
        this.tick++;
        this.tickEffects();
        this.dropLostFighters();
        if (!this.fightingBoss || !this.started)
            return;
        this.bossTicksLeft--;
        if (this.bossTicksLeft > 0)
            return;
        this.addEvent(PartyEventKind.BossFailed);
        this.farming = true;
        this.farmKills = 0;
        this.spawn(PartyEnemyKind.Normal);
    }

    weightMask(): number {
        let mask = 0;
        for (let slot = 0; slot < PartyMaxSlots; slot++)
            if (this.members[slot].weight > 0)
                mask |= 1 << slot;
        return mask;
    }

    private isMember(slot: number): boolean {
        return slot >= 0 && slot < this.members.length && this.members[slot].joined;
    }

    private isLate(enemyId: number): boolean {
        return enemyId !== 0 && enemyId === this.previousEnemyId && this.tick - this.previousKilledTick <= partyTicks(this.rules.lateDamageSeconds);
    }

    private kindRatio(from: PartyEnemyKind, to: PartyEnemyKind): number {
        if (from === to)
            return 1;
        const boss = this.rules.bossHpMultiplier;
        return to === PartyEnemyKind.Boss ? 1 / boss : boss;
    }

    private kill(): void {
        this.barLeft = 0;
        let total = 0;
        for (const member of this.members)
            total += member.enemyShare;
        const shares: number[] = [];
        for (const member of this.members)
            shares.push(total > 0 ? member.enemyShare / total : 0);
        this.events.push({ kind: PartyEventKind.EnemyKilled, tick: this.tick, enemyId: this.enemyId, enemyKind: this.enemyKind, shares: shares });
        this.previousEnemyId = this.enemyId;
        this.previousKind = this.enemyKind;
        this.previousKilledTick = this.tick;
        if (this.fightingBoss) {
            this.addEvent(PartyEventKind.StageCleared);
            this.killsOnStage = 0;
            this.farming = false;
        } else if (this.farming) {
            this.farmKills++;
            if (this.farmKills >= this.rules.bossRetryAfterKills)
                this.farming = false;
        } else if (this.killsOnStage < this.rules.enemiesPerStage) {
            this.killsOnStage++;
        }
        this.spawnNext();
    }

    private spawnNext(): void {
        this.spawn(!this.farming && this.killsOnStage >= this.rules.enemiesPerStage ? PartyEnemyKind.Boss : PartyEnemyKind.Normal);
    }

    private spawn(kind: PartyEnemyKind): void {
        this.enemyId++;
        this.enemyKind = kind;
        this.fighters = 0;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            member.enemyShare = 0;
            member.weight = member.joined && this.counts(i) ? 1 : 0;
            this.fighters += member.weight;
        }
        this.barFull = partyHpMultiplier(this.rules, this.fighters);
        this.barLeft = this.barFull;
        this.addEvent(PartyEventKind.EnemySpawned);
        if (kind !== PartyEnemyKind.Boss)
            return;
        this.bossTicksLeft = partyTicks(this.rules.bossTimerSeconds);
        this.addEvent(PartyEventKind.BossStarted);
    }

    private counts(slot: number): boolean {
        const member = this.members[slot];
        if (!member.connected && this.tick - member.disconnectedAtTick > partyTicks(this.rules.disconnectGraceSeconds))
            return false;
        if (member.peaceful)
            return false;
        return this.tick - member.lastInputTick <= partyTicks(this.rules.restAfterSeconds);
    }

    private dropLostFighters(): void {
        const grace = partyTicks(this.rules.disconnectGraceSeconds);
        for (const member of this.members) {
            if (member.weight === 0 || member.connected || this.tick - member.disconnectedAtTick <= grace)
                continue;
            member.weight = 0;
            this.dropFighterMidFight();
        }
    }

    private dropFighterMidFight(): void {
        if (this.fighters === 0)
            return;
        const before = partyHpMultiplier(this.rules, this.fighters);
        this.fighters--;
        if (!this.fightingBoss)
            return;
        const after = partyHpMultiplier(this.rules, this.fighters);
        this.barLeft *= after / before;
        this.barFull = after;
    }

    private tickEffects(): void {
        for (const effect of this.effects)
            if (effect.ticksLeft > 0)
                effect.ticksLeft--;
    }

    private addEvent(kind: PartyEventKind): void {
        this.events.push({ kind: kind, tick: this.tick, enemyId: this.enemyId, enemyKind: this.enemyKind, shares: [0, 0, 0, 0] });
    }
}
