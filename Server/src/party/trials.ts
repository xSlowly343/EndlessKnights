interface PartyTrialRule {
    id: string;
    marathon: boolean;
    ms: number;
    waves: number;
}

interface PartyTrialTreeLine {
    node: number;
    rank: number;
    value: number;
    part: string;
}

interface PartyTrialRules {
    perWeek: number;
    tries: number;
    minFighters: number;
    maxTier: number;
    ceilingBase: number;
    ceilingStep: number;
    firstClearSigns: number;
    experiencePerTier: number;
    minMs: number;
    slackMs: number;
    list: PartyTrialRule[];
    extraTries: PartyTrialTreeLine[];
    carry: PartyTrialTreeLine[];
}

interface PartyTrialRun {
    trial: string;
    tier: number;
    by: string;
    fighters: string[];
    at: number;
}

interface PartyTrialValue {
    best: { [trial: string]: number };
    week: number;
    used: { [trial: string]: number };
    run: PartyTrialRun | null;
}

const PartyTrialCollection = "party_trials";
const PartyTrialAttempts = 4;
const PartyTrialWeekMs = 7 * 86400000;
const PartyTrialFirstMondayMs = 4 * 86400000;

function partyTrialRules(): PartyTrialRules {
    return PartyGrowthServerRules.trials;
}

function partyTrialWeek(nowUnixMs: number): number {
    return Math.floor(Math.max(0, nowUnixMs - PartyTrialFirstMondayMs) / PartyTrialWeekMs);
}

function partyTrialWeekEndsAt(nowUnixMs: number): number {
    return PartyTrialFirstMondayMs + (partyTrialWeek(nowUnixMs) + 1) * PartyTrialWeekMs;
}

function partyTrialOpenIds(week: number): string[] {
    const rules = partyTrialRules();
    const count = rules.list.length;
    const first = (week * rules.perWeek) % count;
    const ids: string[] = [];
    for (let i = 0; i < rules.perWeek; i++)
        ids.push(rules.list[(first + i) % count].id);
    return ids;
}

function partyTrialRuleOf(id: unknown): PartyTrialRule {
    const rule = typeof id === "string" ? partyTrialRules().list.filter((trial) => trial.id === id)[0] : undefined;
    if (!rule)
        throw new Error("bad request");
    return rule;
}

function partyTrialEmpty(): PartyTrialValue {
    return { best: {}, week: -1, used: {}, run: null };
}

function partyTrialRead(nk: nkruntime.Nakama, partyId: string): { value: PartyTrialValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyTrialCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: partyTrialEmpty(), version: "*" };
    const stored = objects[0].value as PartyTrialValue;
    return {
        value: { best: stored.best || {}, week: stored.week === undefined ? -1 : stored.week, used: stored.used || {}, run: stored.run || null },
        version: objects[0].version
    };
}

function partyTrialChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyTrialValue) => T): T {
    for (let attempt = 1; attempt <= PartyTrialAttempts; attempt++) {
        const stored = partyTrialRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyTrialCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyTrialSettle(value: PartyTrialValue, nowUnixMs: number): void {
    const week = partyTrialWeek(nowUnixMs);
    if (value.week !== week) {
        value.week = week;
        value.used = {};
    }
    const run = value.run;
    if (run && nowUnixMs > run.at + partyTrialRuleOf(run.trial).ms + partyTrialRules().slackMs)
        value.run = null;
}

function partyTrialTreeValue(levels: { [node: string]: number }, lines: PartyTrialTreeLine[]): number {
    let total = 0;
    for (const line of lines) {
        const rank = levels[String(line.node)] || 0;
        if (rank < line.rank)
            continue;
        const rule = PartyGrowthServerRules.tree.nodes[line.node - 1];
        total += line.value * (line.part === "a" && rule ? partyTreeAMultiplier(rule, rank) : 1);
    }
    return total;
}

function partyTrialCeiling(level: number): number {
    const rules = partyTrialRules();
    const unlocks = PartyGrowthServerRules.unlocks;
    if (!unlocks.some((unlock) => unlock.kind === "trials" && unlock.level <= level))
        return 0;
    const steps = unlocks.filter((unlock) => unlock.kind === "trialTier" && unlock.level <= level).length;
    return Math.min(rules.maxTier, rules.ceilingBase + rules.ceilingStep * steps);
}

function partyTrialSituation(nk: nkruntime.Nakama, partyId: string): { ceiling: number; tries: number; carry: boolean } {
    const level = partyGrowthLevel(partyGrowthRead(nk, partyId).value.experience).level;
    const levels = partyTreasuryRead(nk, partyId).value.levels;
    return {
        ceiling: partyTrialCeiling(level),
        tries: partyTrialRules().tries + Math.floor(partyTrialTreeValue(levels, partyTrialRules().extraTries) + partySetsSum(nk, partyId, "trialTries") + 1e-9),
        carry: partyTrialTreeValue(levels, partyTrialRules().carry) > 0
    };
}

function partyTrialView(partyId: string, value: PartyTrialValue, situation: { ceiling: number; tries: number; carry: boolean }, nowUnixMs: number, result: { [key: string]: any } | null): { [key: string]: any } {
    const open = partyTrialOpenIds(partyTrialWeek(nowUnixMs));
    const run = value.run;
    return {
        partyId: partyId, opened: situation.ceiling > 0, ceiling: situation.ceiling, carry: situation.carry,
        weekEndsAt: partyTrialWeekEndsAt(nowUnixMs),
        trials: partyTrialRules().list.map((trial) => ({
            id: trial.id, open: open.indexOf(trial.id) >= 0, best: value.best[trial.id] || 0,
            triesLeft: Math.max(0, situation.tries - (value.used[trial.id] || 0))
        })),
        run: run ? { trial: run.trial, tier: run.tier, by: run.by, fighters: run.fighters, at: run.at, until: run.at + partyTrialRuleOf(run.trial).ms + partyTrialRules().slackMs } : null,
        result: result
    };
}

function partyTrialAct(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, act: (value: PartyTrialValue, meta: PartyMeta, partyId: string, situation: { ceiling: number; tries: number; carry: boolean }) => { [key: string]: any } | null): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const meta = partyMetaOf(group);
    const situation = partyTrialSituation(nk, group.id);
    return partyTrialChange(nk, group.id, (value) => {
        partyTrialSettle(value, nowUnixMs);
        const result = act(value, meta, group.id, situation);
        return partyTrialView(group.id, value, situation, nowUnixMs, result);
    });
}

function partyTrialMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const value = partyTrialRead(nk, group.id).value;
    partyTrialSettle(value, nowUnixMs);
    return partyTrialView(group.id, value, partyTrialSituation(nk, group.id), nowUnixMs, null);
}

function partyTrialFighters(fighters: unknown, meta: PartyMeta, userId: string): string[] {
    if (!Array.isArray(fighters) || fighters.some((id) => typeof id !== "string"))
        throw new Error("bad request");
    const unique = (fighters as string[]).filter((id, index, all) => all.indexOf(id) === index);
    if (unique.indexOf(userId) < 0 || unique.some((id) => meta.members.indexOf(id) < 0))
        throw new Error("only party members can fight a trial");
    if (unique.length < partyTrialRules().minFighters)
        throw new Error("a trial needs at least " + partyTrialRules().minFighters + " fighters");
    return unique;
}

function partyTrialStart(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const rule = partyTrialRuleOf(request.trial);
    const tier = request.tier;
    if (!rule.marathon && (typeof tier !== "number" || tier !== Math.floor(tier) || tier < 1))
        throw new Error("bad request");
    return partyTrialAct(nk, userId, nowUnixMs, (value, meta, partyId, situation) => {
        if (situation.ceiling <= 0)
            throw new Error("party trials are not open yet");
        const fighters = partyTrialFighters(request.fighters, meta, userId);
        if (partyTrialOpenIds(value.week).indexOf(rule.id) < 0)
            throw new Error("this trial is not open this week");
        if (value.run)
            throw new Error("a trial is already being fought");
        if ((value.used[rule.id] || 0) >= situation.tries)
            throw new Error("no tries left this week");
        const reachable = Math.min(situation.ceiling, (value.best[rule.id] || 0) + 1);
        if (!rule.marathon && tier > reachable)
            throw new Error("this tier is not open yet");
        value.used[rule.id] = (value.used[rule.id] || 0) + 1;
        value.run = { trial: rule.id, tier: rule.marathon ? 1 : tier, by: userId, fighters: fighters, at: nowUnixMs };
        return null;
    });
}

function partyTrialFirstClears(before: number, after: number): { signs: number; experience: number } {
    const rules = partyTrialRules();
    let signs = 0;
    let experience = 0;
    for (let tier = before + 1; tier <= after; tier++) {
        signs += rules.firstClearSigns * tier;
        experience += rules.experiencePerTier * tier;
    }
    return { signs: signs, experience: experience };
}

function partyTrialCarry(value: PartyTrialValue, trial: string, reached: number, ceiling: number): string[] {
    const carried: string[] = [];
    for (const other of partyTrialOpenIds(value.week)) {
        const tier = Math.min(ceiling, reached - 1);
        if (other === trial || tier <= (value.best[other] || 0))
            continue;
        value.best[other] = tier;
        carried.push(other);
    }
    return carried;
}

function partyTrialTooFast(rule: PartyTrialRule, request: { [key: string]: any }, elapsedMs: number): boolean {
    if (rule.marathon)
        return request.waves > 0 && elapsedMs < Math.max(partyTrialRules().minMs, (request.waves - 1) * rule.ms / rule.waves);
    return request.cleared === true && elapsedMs < partyTrialRules().minMs;
}

function partyTrialReached(rule: PartyTrialRule, run: PartyTrialRun, request: { [key: string]: any }, ceiling: number): number {
    if (rule.marathon) {
        const waves = request.waves;
        if (typeof waves !== "number" || waves !== Math.floor(waves) || waves < 0 || waves > rule.waves)
            throw new Error("bad request");
        return Math.min(ceiling, waves);
    }
    if (typeof request.cleared !== "boolean")
        throw new Error("bad request");
    return request.cleared ? run.tier : 0;
}

function partyTrialFinish(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    let rewards = { signs: 0, experience: 0 };
    let record: { trial: string; tier: number; fighters: string[] } | null = null;
    const view = partyTrialAct(nk, userId, nowUnixMs, (value, meta, partyId, situation) => {
        const run = value.run;
        if (!run || run.by !== userId)
            throw new Error("no trial is being fought");
        const rule = partyTrialRuleOf(run.trial);
        const reached = partyTrialReached(rule, run, request, situation.ceiling);
        if (partyTrialTooFast(rule, request, nowUnixMs - run.at))
            throw new Error("the trial was finished too fast");
        value.run = null;
        const before = value.best[rule.id] || 0;
        rewards = partyTrialFirstClears(before, reached);
        if (reached > before)
            value.best[rule.id] = reached;
        const carried = reached > 0 && situation.carry ? partyTrialCarry(value, rule.id, reached, situation.ceiling) : [];
        if (reached > before)
            record = { trial: rule.id, tier: reached, fighters: run.fighters };
        return { trial: rule.id, tier: reached, best: value.best[rule.id] || 0, firstClear: reached > before, signs: rewards.signs, experience: rewards.experience, carried: carried };
    });
    partyTrialReward(nk, view.partyId, rewards, record, nowUnixMs);
    return view;
}

function partyTrialReward(nk: nkruntime.Nakama, partyId: string, rewards: { signs: number; experience: number }, record: { trial: string; tier: number; fighters: string[] } | null, nowUnixMs: number): void {
    if (rewards.signs > 0)
        partyTreasuryChange(nk, partyId, (value) => {
            value.treasury += rewards.signs;
        });
    partyGrowthAdd(nk, partyId, rewards.experience);
    if (!record)
        return;
    const names = partyTreasuryNames(nk, record.fighters);
    const name = record.fighters.map((id) => names[id] || "").filter((text) => text.length > 0).join(", ");
    try {
        chronicleUpdate(nk, partyId, (value) => chronicleRecord(value, { at: nowUnixMs, kind: "trial", name: name, feat: record!.trial, level: record!.tier }, ChronicleServerRules));
    } catch (error) {
        return;
    }
}

function partyTrialAbandon(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return partyTrialAct(nk, userId, nowUnixMs, (value, meta) => {
        const run = value.run;
        if (!run)
            throw new Error("no trial is being fought");
        if (run.by !== userId && meta.leader !== userId)
            throw new Error("only the one who started the trial or the leader can stop it");
        value.run = null;
        return { trial: run.trial, tier: 0, best: value.best[run.trial] || 0, firstClear: false, signs: 0, experience: 0, carried: [] };
    });
}

function partyTrialsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTrialMine(nk, partyCaller(ctx), Date.now()));
}

function partyTrialStartRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTrialStart(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyTrialFinishRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTrialFinish(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyTrialAbandonRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyTrialAbandon(nk, partyCaller(ctx), Date.now()));
}
