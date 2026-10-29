interface PartyEvent {
    kind: PartyEventKind;
    tick: number;
    enemyId: number;
    enemyKind: PartyEnemyKind;
    shares: number[];
    rewarded: number;
    loop: number;
    saved: number;
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
    lastContributionTick = PartyNever;
    restBankTicks = 0;
    heroClass = 0;
    guestTicksLeft = 0;

    get guest(): boolean { return this.guestTicksLeft > 0; }

    reset(): void {
        this.playerId = "";
        this.joined = false;
        this.peaceful = false;
        this.connected = false;
        this.lastInputTick = 0;
        this.disconnectedAtTick = 0;
        this.weight = 0;
        this.enemyShare = 0;
        this.lastContributionTick = PartyNever;
        this.restBankTicks = 0;
        this.heroClass = 0;
        this.guestTicksLeft = 0;
    }
}

interface PartyEnemyEffect {
    id: number;
    damageTaken: number;
    timerSlow: number;
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
    spawnTick = 0;
    loopShare = 0;
    savedBossTicks = 0;
    world = PartyWorld.Home;
    riftId = 0;
    pendingWorld = PartyNoWorld;
    pendingRiftId = 0;
    switchTicksLeft = 0;
    weakenedSpawns = 0;
    weakenShare = 0;
    weakenGold = 0;
    weakenVulnerability = 0;
    weakenVulnerabilitySeconds = 0;
    enemyWeakened = false;
    enemyWeakenGold = 0;
    private weakenVulnerabilityId = 0;
    private weakenVulnerabilityOwner = PartyNoSlot;
    private readonly recent: number[] = [];
    private bossTimerCarry = 0;
    private previousEnemyId = 0;
    private previousKind = PartyEnemyKind.Normal;
    private previousKilledTick = -Number.MAX_SAFE_INTEGER;

    constructor(private rules: PartyEncounterRules, private readonly seed: string) {
        for (let i = 0; i < PartyMaxSlots; i++)
            this.members.push(new PartyMember());
        for (let i = 0; i < PartyMaxEffects; i++)
            this.effects.push({ id: 0, damageTaken: 0, timerSlow: 0, ticksLeft: 0, owner: PartyNoSlot });
        for (let i = 0; i < PartyRecentTicks; i++)
            this.recent.push(0);
    }

    get started(): boolean { return this.enemyId !== 0; }
    get fightingBoss(): boolean { return this.enemyKind === PartyEnemyKind.Boss; }
    get enemySeed(): string { return partySeedFor(this.seed, this.enemyId); }
    get switching(): boolean { return this.pendingWorld !== PartyNoWorld; }

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

    joinGuest(playerId: string, seconds: number): number {
        if (!this.started || this.slotOf(playerId) !== PartyNoSlot || !(seconds > 0))
            return PartyNoSlot;
        const slot = this.join(playerId);
        if (slot !== PartyNoSlot)
            this.members[slot].guestTicksLeft = Math.max(1, partyTicks(seconds));
        return slot;
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
        return this.active(member) ? PartyPresence.Fighting : PartyPresence.Resting;
    }

    relayTarget(from: number): number {
        if (!this.isMember(from) || !this.members[from].connected || this.tick - this.members[from].lastInputTick > partyTicks(this.rules.relaySenderSeconds))
            return PartyNoSlot;
        const recent = partyTicks(this.rules.relayRecipientSeconds);
        let target = PartyNoSlot;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            if (i === from || !member.joined || !member.connected || member.peaceful || this.tick - member.lastInputTick > recent)
                continue;
            if (target === PartyNoSlot || member.lastInputTick > this.members[target].lastInputTick)
                target = i;
        }
        return target;
    }

    typing(slot: number): boolean {
        return this.isMember(slot) && this.tick - this.members[slot].lastInputTick <= partyTicks(this.rules.activeInputSeconds);
    }

    portion(): number {
        let counting = 0;
        for (let i = 0; i < this.members.length; i++)
            if (this.members[i].joined && this.counts(i))
                counting++;
        return this.barFull / Math.max(1, counting);
    }

    damageTakenMultiplier(): number {
        let total = 0;
        for (const effect of this.effects)
            if (effect.ticksLeft > 0)
                total += effect.damageTaken;
        return 1 + Math.min(this.rules.maxEnemyDamageTaken, total);
    }

    bossTimerRate(): number {
        let rate = 1;
        for (const effect of this.effects)
            if (effect.ticksLeft > 0)
                rate *= 1 - effect.timerSlow;
        return Math.max(this.rules.minBossTimerRate, rate);
    }

    applyEnemyEffect(effectId: number, damageTaken: number, seconds: number, owner: number = PartyNoSlot, timerSlow: number = 0): boolean {
        if (!(damageTaken >= 0) || !(timerSlow >= 0) || !(damageTaken > 0 || timerSlow > 0) || !(seconds > 0) || !this.started)
            return false;
        timerSlow = Math.min(1, timerSlow);
        const ticks = partyTicks(seconds);
        let free = PartyNoSlot;
        for (let i = 0; i < this.effects.length; i++) {
            const effect = this.effects[i];
            if (effect.ticksLeft > 0 && effect.id === effectId) {
                if (damageTaken < effect.damageTaken)
                    return false;
                effect.damageTaken = damageTaken;
                effect.timerSlow = Math.max(effect.timerSlow, timerSlow);
                effect.ticksLeft = Math.max(effect.ticksLeft, ticks);
                effect.owner = owner;
                return true;
            }
            if (free === PartyNoSlot && !(effect.ticksLeft > 0))
                free = i;
        }
        if (free === PartyNoSlot)
            return false;
        this.effects[free] = { id: effectId, damageTaken: damageTaken, timerSlow: timerSlow, ticksLeft: ticks, owner: owner };
        return true;
    }

    applyDamage(slot: number, enemyId: number, share: number, limit?: number): number {
        const applicable = this.applicable(slot, enemyId, share);
        const applied = limit === undefined ? applicable : Math.min(applicable, limit);
        if (!(applied > 0))
            return 0;
        this.recent[this.tick % PartyRecentTicks] += applied * this.kindScale();
        return this.take(slot, applied);
    }

    repeatRecent(slot: number, seconds: number): number {
        if (!this.isMember(slot) || !this.started || !(seconds > 0))
            return 0;
        const ticks = Math.min(PartyRecentTicks - 1, partyTicks(seconds));
        let sum = 0;
        for (let i = 0; i < ticks && this.tick - i >= 0; i++)
            sum += this.recent[(this.tick - i) % PartyRecentTicks];
        const applied = Math.min(this.barLeft, sum / this.kindScale());
        if (!(applied > 0))
            return 0;
        this.loopShare += applied;
        return this.take(slot, applied);
    }

    applicable(slot: number, enemyId: number, share: number): number {
        if (!this.isMember(slot) || !this.started || !(share > 0) || share === Infinity)
            return 0;
        if (enemyId !== this.enemyId) {
            if (!this.isLate(enemyId))
                return 0;
            share *= this.kindRatio(this.previousKind, this.enemyKind);
        }
        const applied = Math.min(this.barLeft, share * this.damageTakenMultiplier());
        return this.typing(slot) ? applied : Math.min(applied, this.idleRoom(slot));
    }

    weakenNext(enemies: number, share: number, gold: number = 0, vulnerability: number = 0, vulnerabilitySeconds: number = 0, effectId: number = 0, owner: number = PartyNoSlot): boolean {
        if (!(enemies > 0) || !(share > 0) || !this.started)
            return false;
        this.weakenedSpawns = Math.max(this.weakenedSpawns, Math.min(this.rules.maxWeakenedSpawns, Math.floor(enemies)));
        this.weakenShare = Math.max(this.weakenShare, Math.min(this.rules.maxWeakenShare, share));
        if (gold > 0)
            this.weakenGold = Math.max(this.weakenGold, Math.min(this.rules.maxWeakenGold, gold));
        if (vulnerability > 0 && vulnerabilitySeconds > 0 && vulnerability >= this.weakenVulnerability) {
            this.weakenVulnerability = vulnerability;
            this.weakenVulnerabilitySeconds = vulnerabilitySeconds;
            this.weakenVulnerabilityId = effectId;
            this.weakenVulnerabilityOwner = owner;
        }
        return true;
    }

    endEnemyEffect(effectId: number, owner: number): boolean {
        for (const effect of this.effects) {
            if (!(effect.ticksLeft > 0) || effect.id !== effectId || effect.owner !== owner)
                continue;
            effect.ticksLeft = 0;
            return true;
        }
        return false;
    }

    requestBossFight(): boolean {
        if (!this.started || !this.farming || this.fightingBoss)
            return false;
        this.farming = false;
        this.spawn(PartyEnemyKind.Boss);
        return true;
    }

    proposeWorld(world: PartyWorld, riftId: number): boolean {
        if (!this.started || this.switching || (world === this.world && riftId === this.riftId) || (world === PartyWorld.Home) !== (riftId === 0))
            return false;
        this.pendingWorld = world;
        this.pendingRiftId = riftId;
        this.switchTicksLeft = Math.max(1, partyTicks(this.rules.worldSwitchSeconds));
        this.addEvent(PartyEventKind.WorldPending);
        return true;
    }

    cancelWorld(): boolean {
        if (!this.switching)
            return false;
        this.clearPending();
        this.addEvent(PartyEventKind.WorldCancelled);
        return true;
    }

    switchWorld(world: PartyWorld, riftId: number): void {
        this.releaseGuests(true);
        this.clearPending();
        this.world = world;
        this.riftId = riftId;
        this.killsOnStage = 0;
        this.farming = false;
        this.farmKills = 0;
        this.previousEnemyId = 0;
        this.clearWeaken();
        this.enemyWeakened = false;
        this.enemyWeakenGold = 0;
        for (let i = 0; i < this.effects.length; i++)
            this.effects[i] = { id: 0, damageTaken: 0, timerSlow: 0, ticksLeft: 0, owner: 0 };
        for (let i = 0; i < this.recent.length; i++)
            this.recent[i] = 0;
        this.addEvent(PartyEventKind.WorldChanged);
        if (this.started)
            this.spawn(PartyEnemyKind.Normal);
    }

    advance(): void {
        this.tick++;
        this.recent[this.tick % PartyRecentTicks] = 0;
        this.tickEffects();
        this.releaseGuests(false);
        this.dropLostFighters();
        if (this.switching && --this.switchTicksLeft <= 0) {
            this.switchWorld(this.pendingWorld, this.pendingRiftId);
            return;
        }
        if (!this.fightingBoss || !this.started)
            return;
        const rate = this.bossTimerRate();
        this.savedBossTicks += 1 - rate;
        this.bossTimerCarry += rate;
        while (this.bossTimerCarry >= 1 && this.bossTicksLeft > 0) {
            this.bossTimerCarry -= 1;
            this.bossTicksLeft--;
        }
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

    private clearPending(): void {
        this.pendingWorld = PartyNoWorld;
        this.pendingRiftId = 0;
        this.switchTicksLeft = 0;
    }

    private take(slot: number, applied: number): number {
        this.members[slot].enemyShare += applied;
        this.barLeft -= applied;
        if (this.barLeft <= 0)
            this.kill();
        return applied;
    }

    private kindScale(): number {
        return this.fightingBoss ? this.rules.bossHpMultiplier : 1;
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

    private idleRoom(slot: number): number {
        const portion = this.portion();
        let idleTaken = 0;
        let typing = 0;
        for (let i = 0; i < this.members.length; i++) {
            if (!this.members[i].joined)
                continue;
            if (this.typing(i))
                typing++;
            else
                idleTaken += this.members[i].enemyShare;
        }
        const own = this.rules.idlePortionShare * portion - this.members[slot].enemyShare;
        const shared = this.barFull - this.rules.typingReserveShare * portion * typing - idleTaken;
        return Math.max(0, Math.min(own, shared));
    }

    private kill(): void {
        this.barLeft = 0;
        let total = 0;
        for (const member of this.members)
            total += member.enemyShare;
        const shares: number[] = [];
        for (const member of this.members)
            shares.push(total > 0 ? member.enemyShare / total : 0);
        const rewarded = this.reward();
        this.events.push({
            kind: PartyEventKind.EnemyKilled, tick: this.tick, enemyId: this.enemyId, enemyKind: this.enemyKind, shares: shares, rewarded: rewarded,
            loop: total > 0 ? this.loopShare / total : 0, saved: this.savedBossTicks / PartyTicksPerSecond
        });
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

    private reward(): number {
        const counted = this.rules.minShare * this.portion();
        const lifetime = this.tick - this.spawnTick;
        const bankMax = partyTicks(this.rules.restBankSeconds);
        let rewarded = 0;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            if (!member.joined)
                continue;
            if (member.enemyShare > 0 && member.enemyShare >= counted)
                member.lastContributionTick = this.tick;
            if (!member.connected || member.peaceful || member.guest)
                continue;
            if (this.typing(i)) {
                rewarded |= 1 << i;
                member.restBankTicks = Math.min(bankMax, member.restBankTicks + lifetime);
            } else if (member.restBankTicks > 0) {
                rewarded |= 1 << i;
                member.restBankTicks = Math.max(0, member.restBankTicks - lifetime);
            }
        }
        return rewarded;
    }

    private spawnNext(): void {
        this.spawn(!this.farming && this.killsOnStage >= this.rules.enemiesPerStage ? PartyEnemyKind.Boss : PartyEnemyKind.Normal);
    }

    private spawn(kind: PartyEnemyKind): void {
        this.enemyId++;
        this.enemyKind = kind;
        this.spawnTick = this.tick;
        this.fighters = 0;
        this.loopShare = 0;
        this.savedBossTicks = 0;
        this.bossTimerCarry = 0;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            member.enemyShare = 0;
            member.weight = member.joined && this.counts(i) ? 1 : 0;
            this.fighters += member.weight;
        }
        this.barFull = partyHpMultiplier(this.rules, this.fighters);
        this.barLeft = this.barFull * (1 - this.takeWeaken());
        this.addEvent(PartyEventKind.EnemySpawned);
        if (this.enemyWeakened && this.weakenVulnerability > 0)
            this.applyEnemyEffect(this.weakenVulnerabilityId, this.weakenVulnerability, this.weakenVulnerabilitySeconds, this.weakenVulnerabilityOwner);
        if (this.weakenedSpawns === 0)
            this.clearWeaken();
        if (kind !== PartyEnemyKind.Boss)
            return;
        this.bossTicksLeft = partyTicks(this.rules.bossTimerSeconds);
        this.addEvent(PartyEventKind.BossStarted);
    }

    private takeWeaken(): number {
        this.enemyWeakened = this.weakenedSpawns > 0;
        this.enemyWeakenGold = this.enemyWeakened ? this.weakenGold : 0;
        if (!this.enemyWeakened)
            return 0;
        this.weakenedSpawns--;
        return this.weakenShare;
    }

    private clearWeaken(): void {
        this.weakenedSpawns = 0;
        this.weakenShare = 0;
        this.weakenGold = 0;
        this.weakenVulnerability = 0;
        this.weakenVulnerabilitySeconds = 0;
        this.weakenVulnerabilityId = 0;
        this.weakenVulnerabilityOwner = PartyNoSlot;
    }

    private counts(slot: number): boolean {
        const member = this.members[slot];
        if (!member.connected && this.tick - member.disconnectedAtTick > partyTicks(this.rules.disconnectGraceSeconds))
            return false;
        if (member.peaceful || member.guest)
            return false;
        return this.active(member);
    }

    private active(member: PartyMember): boolean {
        const rest = partyTicks(this.rules.restAfterSeconds);
        return this.tick - member.lastInputTick <= rest || this.tick - member.lastContributionTick <= rest;
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

    private releaseGuests(all: boolean): void {
        let left = 0;
        for (let i = 0; i < this.members.length; i++) {
            const member = this.members[i];
            if (!member.joined || !member.guest)
                continue;
            if (!all && --member.guestTicksLeft > 0)
                continue;
            member.reset();
            left |= 1 << i;
        }
        if (left !== 0)
            this.events.push({ kind: PartyEventKind.GuestLeft, tick: this.tick, enemyId: this.enemyId, enemyKind: this.enemyKind, shares: [0, 0, 0, 0], rewarded: left, loop: 0, saved: 0 });
    }

    private tickEffects(): void {
        for (const effect of this.effects)
            if (effect.ticksLeft > 0)
                effect.ticksLeft--;
    }

    private addEvent(kind: PartyEventKind): void {
        this.events.push({ kind: kind, tick: this.tick, enemyId: this.enemyId, enemyKind: this.enemyKind, shares: [0, 0, 0, 0], rewarded: 0, loop: 0, saved: 0 });
    }
}
