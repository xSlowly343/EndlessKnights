const enum PartyTrialOutcome { Idle = 0, Running = 1, Cleared = 2, Failed = 3 }

const PartyTrialKinds = 4;
const PartyTrialNoKind = -1;

interface PartyTrialHit {
    keys: number;
    mouse: number;
    skills: number;
    minions: number;
    typing: boolean;
    armorPen: number;
    shred: number;
}

interface PartyTrialFightDef {
    id: string;
    rule: string;
    seconds: number;
    passSeconds: number;
    bankShare: number;
    windowSeconds: number;
    resist: number;
    everySeconds: number;
    inside: number;
    outside: number;
    shieldShare: number;
    chainBase: number;
    chainStep: number;
    chainMax: number;
    keepSeconds: number;
    stepSeconds: number;
    waveSeconds: number;
    waves: number;
}

interface PartyTrialComplicationRule {
    kind: string;
    keys: number;
    mouse: number;
    share: number;
    day: number;
    night: number;
    perSecond: number;
    afterSeconds: number;
}

interface PartyTrialFightRules {
    hpGrowth: number;
    armorPerTier: number;
    armorMax: number;
    complicationFromTier: number;
    complications: PartyTrialComplicationRule[];
    defense: { k: number; armorPenCap: number; shredCap: number };
    list: PartyTrialFightDef[];
}

function partyTrialHitOf(hit: PartyTrialHit, kind: number): number {
    return kind === 0 ? hit.keys : kind === 1 ? hit.mouse : kind === 2 ? hit.skills : hit.minions;
}

function partyTrialEmptyHit(): PartyTrialHit {
    return { keys: 0, mouse: 0, skills: 0, minions: 0, typing: false, armorPen: 0, shred: 0 };
}

function partyTrialComplicationOf(rules: PartyTrialFightRules, index: number, tier: number): PartyTrialComplicationRule | null {
    const trial = rules.list[index];
    if (rules.complications.length === 0 || tier < rules.complicationFromTier || trial.rule === "marathon")
        return null;
    return rules.complications[(tier - rules.complicationFromTier + index) % rules.complications.length];
}

function partyTrialArmorShare(rules: PartyTrialFightRules, tier: number): number {
    return Math.min(rules.armorMax, rules.armorPerTier * (Math.max(1, tier) - 1));
}

function partyTrialWaveHp(rules: PartyTrialFightRules, wave: number): number {
    return Math.pow(rules.hpGrowth, Math.max(1, wave) - 1);
}

class PartyTrialFight {
    shields = [0, 0, 0, 0];
    window = [0, 0, 0, 0];
    kindFactor = [1, 1, 1, 1];
    bank = [0, 0, 0, 0];
    index = -1;
    complication: PartyTrialComplicationRule | null = null;
    power = 0;
    globalFactor = 0;
    quietTicks = 0;
    chainQuietTicks = 0;
    chainStepTicks = 0;
    lastLeader = PartyTrialNoKind;
    outcome = PartyTrialOutcome.Idle;
    tier = 0;
    fighters = 0;
    tick = 0;
    totalTicks = 0;
    hpLeft = 0;
    hpFull = 0;
    skillShieldLeft = 0;
    holder = 0;
    resisted = PartyTrialNoKind;
    chain = 0;
    wave = 0;
    armor = 0;
    wavesCleared = 0;

    running(): boolean { return this.outcome === PartyTrialOutcome.Running; }

    trial(rules: PartyTrialFightRules): PartyTrialFightDef | null {
        return this.index >= 0 ? rules.list[this.index] : null;
    }

    resultTier(rules: PartyTrialFightRules): number {
        const trial = this.trial(rules);
        if (this.outcome !== PartyTrialOutcome.Cleared || !trial)
            return 0;
        return trial.rule === "marathon" ? this.wavesCleared : this.tier;
    }

    pulseOpen(rules: PartyTrialFightRules): boolean {
        const trial = this.trial(rules);
        return !!trial && trial.rule === "pulse" && this.tick % partyTrialTicks(trial.everySeconds) < partyTrialTicks(trial.windowSeconds);
    }

    chainFactor(rules: PartyTrialFightRules): number {
        const trial = this.trial(rules);
        if (!trial)
            return 1;
        return this.chain === 0 ? trial.chainBase : Math.min(trial.chainMax, 1 + trial.chainStep * this.chain);
    }

    start(rules: PartyTrialFightRules, index: number, tier: number, fighters: number, power: number, night: boolean): void {
        const trial = rules.list[index];
        const marathon = trial.rule === "marathon";
        this.index = index;
        this.tier = marathon ? 1 : Math.max(1, tier);
        this.fighters = Math.max(1, fighters);
        this.power = Math.max(0, power);
        this.complication = partyTrialComplicationOf(rules, index, this.tier);
        this.tick = 0;
        this.totalTicks = partyTrialTicks(trial.seconds);
        this.outcome = PartyTrialOutcome.Running;
        this.holder = 0;
        this.bank = [0, 0, 0, 0];
        this.resisted = PartyTrialNoKind;
        this.chain = 0;
        this.quietTicks = 0;
        this.chainQuietTicks = 0;
        this.chainStepTicks = 0;
        this.lastLeader = PartyTrialNoKind;
        this.window = [0, 0, 0, 0];
        const shield = trial.rule === "shields" ? trial.shieldShare : 0;
        this.shields = [shield, shield, shield, shield];
        this.wave = 1;
        this.wavesCleared = 0;
        this.hpFull = marathon ? partyTrialWaveHp(rules, 1) : 1;
        this.hpLeft = this.hpFull;
        this.armor = partyTrialArmorShare(rules, this.tier) * rules.defense.k;
        this.skillShieldLeft = this.is("skillShield") ? this.complication!.share : 0;
        this.setFactors(night);
    }

    stop(): void {
        if (this.running())
            this.outcome = PartyTrialOutcome.Failed;
    }

    advance(rules: PartyTrialFightRules, hits: PartyTrialHit[], count: number): number {
        const trial = this.trial(rules);
        if (!this.running() || !trial)
            return 0;
        const dealt = this.strike(rules, trial, hits, Math.min(count, hits.length));
        this.noteQuiet(dealt);
        this.tick++;
        this.finish(rules, trial);
        return dealt;
    }

    armorFactor(rules: PartyTrialFightRules, hit: PartyTrialHit): number {
        if (!(this.armor > 0))
            return 1;
        const defense = rules.defense;
        const shred = Math.min(defense.shredCap, Math.max(0, hit.shred));
        const pen = Math.min(defense.armorPenCap, Math.max(0, hit.armorPen));
        return defense.k / (this.armor * (1 - shred) * (1 - pen) + defense.k);
    }

    private strike(rules: PartyTrialFightRules, trial: PartyTrialFightDef, hits: PartyTrialHit[], count: number): number {
        if (trial.rule === "choir" && !partyTrialAllTyping(hits, count))
            return 0;
        if (trial.rule === "chain")
            this.countChain(trial, hits, count);
        if (trial.rule === "relay")
            return this.relay(rules, trial, hits, count);
        let dealt = 0;
        for (let kind = 0; kind < PartyTrialKinds; kind++) {
            let amount = 0;
            for (let i = 0; i < count; i++)
                amount += this.landed(rules, hits[i], kind);
            dealt += this.hit(trial, kind, amount * this.factor(rules, trial, kind));
        }
        if (trial.rule === "mirror")
            this.mirror(trial, hits, count);
        return dealt;
    }

    private relay(rules: PartyTrialFightRules, trial: PartyTrialFightDef, hits: PartyTrialHit[], count: number): number {
        let dealt = 0;
        const passTicks = partyTrialTicks(trial.passSeconds);
        if (this.tick > 0 && this.tick % passTicks === 0) {
            this.holder = Math.floor(this.tick / passTicks) % this.fighters;
            for (let kind = 0; kind < PartyTrialKinds; kind++) {
                dealt += this.hit(trial, kind, this.bank[kind] * trial.bankShare);
                this.bank[kind] = 0;
            }
        }
        for (let i = 0; i < count; i++) {
            for (let kind = 0; kind < PartyTrialKinds; kind++) {
                const amount = this.landed(rules, hits[i], kind) * this.factor(rules, trial, kind);
                if (i === this.holder)
                    dealt += this.hit(trial, kind, amount);
                else
                    this.bank[kind] += amount;
            }
        }
        return dealt;
    }

    private landed(rules: PartyTrialFightRules, hit: PartyTrialHit, kind: number): number {
        return Math.max(0, partyTrialHitOf(hit, kind)) * this.armorFactor(rules, hit);
    }

    private hit(trial: PartyTrialFightDef, kind: number, amount: number): number {
        if (amount <= 0)
            return 0;
        if (trial.rule === "shields" && this.anyShield()) {
            const taken = Math.min(this.shields[kind], amount);
            this.shields[kind] -= taken;
            return taken;
        }
        if (this.skillShieldLeft > 0) {
            if (kind !== 2)
                return 0;
            const taken = Math.min(this.skillShieldLeft, amount);
            this.skillShieldLeft -= taken;
            return taken;
        }
        const dealt = Math.min(this.hpLeft, amount);
        this.hpLeft -= dealt;
        return dealt;
    }

    private factor(rules: PartyTrialFightRules, trial: PartyTrialFightDef, kind: number): number {
        return this.kindFactor[kind] * this.globalFactor * this.ruleFactor(rules, trial, kind);
    }

    private ruleFactor(rules: PartyTrialFightRules, trial: PartyTrialFightDef, kind: number): number {
        switch (trial.rule) {
            case "silence":
                return kind === 0 ? 0 : 1;
            case "mirror":
                return kind === this.resisted ? 1 - trial.resist : 1;
            case "pulse":
                return this.pulseOpen(rules) ? trial.inside : trial.outside;
            case "chain":
                return this.chainFactor(rules);
            default:
                return 1;
        }
    }

    private mirror(trial: PartyTrialFightDef, hits: PartyTrialHit[], count: number): void {
        for (let i = 0; i < count; i++)
            for (let kind = 0; kind < PartyTrialKinds; kind++)
                this.window[kind] += Math.max(0, partyTrialHitOf(hits[i], kind));
        if ((this.tick + 1) % partyTrialTicks(trial.windowSeconds) !== 0)
            return;
        let best = PartyTrialNoKind;
        for (let kind = 0; kind < PartyTrialKinds; kind++)
            if (this.window[kind] > 0 && (best === PartyTrialNoKind || this.window[kind] > this.window[best]))
                best = kind;
        this.resisted = best;
        this.window = [0, 0, 0, 0];
    }

    private countChain(trial: PartyTrialFightDef, hits: PartyTrialHit[], count: number): void {
        this.chainStepTicks++;
        const leader = partyTrialLeader(hits, count);
        if (leader !== PartyTrialNoKind && this.lastLeader !== PartyTrialNoKind && leader !== this.lastLeader && this.chainStepTicks >= partyTrialTicks(trial.stepSeconds)) {
            this.chain++;
            this.chainStepTicks = 0;
            this.chainQuietTicks = 0;
        } else if (++this.chainQuietTicks >= partyTrialTicks(trial.keepSeconds)) {
            this.chain = 0;
            this.chainQuietTicks = 0;
        }
        if (leader !== PartyTrialNoKind)
            this.lastLeader = leader;
    }

    private noteQuiet(dealt: number): void {
        this.quietTicks = dealt > 0 ? 0 : this.quietTicks + 1;
        const complication = this.complication;
        if (!complication || complication.kind !== "regeneration" || this.quietTicks < partyTrialTicks(complication.afterSeconds))
            return;
        this.hpLeft = Math.min(this.hpFull, this.hpLeft + this.hpFull * complication.perSecond / PartyTicksPerSecond);
    }

    private finish(rules: PartyTrialFightRules, trial: PartyTrialFightDef): void {
        if (trial.rule === "marathon") {
            this.finishWave(rules, trial);
            return;
        }
        if (this.hpLeft <= 1e-12)
            this.outcome = PartyTrialOutcome.Cleared;
        else if (this.tick >= this.totalTicks)
            this.outcome = PartyTrialOutcome.Failed;
    }

    private finishWave(rules: PartyTrialFightRules, trial: PartyTrialFightDef): void {
        const waveTicks = partyTrialTicks(trial.waveSeconds);
        if (this.hpLeft <= 1e-12 && this.wavesCleared < this.wave)
            this.wavesCleared = this.wave;
        if (this.tick % waveTicks !== 0)
            return;
        if (this.wavesCleared < this.wave) {
            this.outcome = this.wavesCleared > 0 ? PartyTrialOutcome.Cleared : PartyTrialOutcome.Failed;
            return;
        }
        if (this.wave >= trial.waves) {
            this.outcome = PartyTrialOutcome.Cleared;
            return;
        }
        this.wave++;
        this.hpFull = partyTrialWaveHp(rules, this.wave);
        this.hpLeft = this.hpFull;
        this.armor = partyTrialArmorShare(rules, this.wave) * rules.defense.k;
    }

    private setFactors(night: boolean): void {
        this.kindFactor = [1, 1, 1, 1];
        this.globalFactor = this.power;
        const complication = this.complication;
        if (!complication)
            return;
        if (complication.kind === "carapace")
            this.kindFactor[0] = complication.keys;
        else if (complication.kind === "shadow")
            this.kindFactor[1] = complication.mouse;
        else if (complication.kind === "night")
            this.globalFactor *= night ? complication.night : complication.day;
    }

    private is(kind: string): boolean {
        return !!this.complication && this.complication.kind === kind;
    }

    private anyShield(): boolean {
        for (let i = 0; i < PartyTrialKinds; i++)
            if (this.shields[i] > 1e-12)
                return true;
        return false;
    }
}

function partyTrialAllTyping(hits: PartyTrialHit[], count: number): boolean {
    if (count === 0)
        return false;
    for (let i = 0; i < count; i++)
        if (!hits[i].typing)
            return false;
    return true;
}

function partyTrialLeader(hits: PartyTrialHit[], count: number): number {
    let leader = PartyTrialNoKind;
    let best = 0;
    for (let i = 0; i < count; i++) {
        let total = 0;
        for (let kind = 0; kind < PartyTrialKinds; kind++)
            total += Math.max(0, partyTrialHitOf(hits[i], kind));
        if (total > best) {
            best = total;
            leader = i;
        }
    }
    return leader;
}

function partyTrialTicks(seconds: number): number {
    return Math.max(1, partyTicks(seconds));
}

function partyTrialIdPack(index: number, tier: number, night: boolean): number {
    return (Math.min(254, Math.max(0, index)) + 1) + Math.min(65535, Math.max(1, tier)) * 256 + (night ? 16777216 : 0);
}

function partyTrialIdIndex(id: number): number {
    return id % 256 - 1;
}

function partyTrialIdTier(id: number): number {
    return Math.max(1, Math.floor(id / 256) % 65536);
}

function partyTrialIdNight(id: number): boolean {
    return Math.floor(id / 16777216) % 2 === 1;
}
