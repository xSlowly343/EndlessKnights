interface RiftWindowStart {
    day: number;
    hourUtc: number;
}

interface RiftRules {
    windows: RiftWindowStart[];
    durationMs: number;
    announceMs: number;
    modifiers: string[];
    modifiersPerWindow: number;
    partyJoinMs: number;
    partyDurationMs: number;
    boardUpdateMs: number;
    boardShown: number;
    boardPlaces: RiftBoardPlace[];
    enemies: RiftEnemyModifier[];
    milestones: RiftMilestoneRules;
}

interface RiftMilestoneStep {
    from: number;
    shards: number;
    uncutGems: number;
    friendship: number;
    rareGem: boolean;
}

interface RiftMilestoneRules {
    every: number;
    steps: RiftMilestoneStep[];
    rareGems: string[];
    rareGemsPerRift: number;
}

interface RiftEnemyModifier {
    unless: number;
    regen: number;
    shield: number;
    extraEnemies: number;
}

interface RiftBoardPlace {
    upTo: number;
    shards: number;
    uncutGems: number;
    ingots: number;
    friendship: number;
}

interface RiftWindow {
    id: number;
    startUnixMs: number;
    endUnixMs: number;
}

declare const RiftServerRules: RiftRules;

const RiftHourMs = 3600 * 1000;
const RiftDayMs = 24 * RiftHourMs;
const RiftWeekMs = 7 * RiftDayMs;

function riftWindowOf(rules: RiftRules, startUnixMs: number): RiftWindow {
    return { id: Math.floor(startUnixMs / 1000), startUnixMs: startUnixMs, endUnixMs: startUnixMs + rules.durationMs };
}

function riftLatestStart(start: RiftWindowStart, nowUnixMs: number): number {
    const days = Math.floor(nowUnixMs / RiftDayMs);
    const today = (((days + 4) % 7) + 7) % 7;
    const daysBack = (today - start.day + 7) % 7;
    const latest = days * RiftDayMs - daysBack * RiftDayMs + start.hourUtc * RiftHourMs;
    return latest > nowUnixMs ? latest - RiftWeekMs : latest;
}

function riftWindowAt(rules: RiftRules, nowUnixMs: number): RiftWindow | null {
    for (let i = 0; i < rules.windows.length; i++) {
        const window = riftWindowOf(rules, riftLatestStart(rules.windows[i], nowUnixMs));
        if (nowUnixMs >= window.startUnixMs && nowUnixMs < window.endUnixMs)
            return window;
    }
    return null;
}

function riftNext(rules: RiftRules, nowUnixMs: number): RiftWindow {
    let best = Infinity;
    for (let i = 0; i < rules.windows.length; i++) {
        let next = riftLatestStart(rules.windows[i], nowUnixMs);
        if (next <= nowUnixMs)
            next += RiftWeekMs;
        best = Math.min(best, next);
    }
    return riftWindowOf(rules, best);
}

function riftAnnounced(rules: RiftRules, nowUnixMs: number): boolean {
    return riftWindowAt(rules, nowUnixMs) === null && riftNext(rules, nowUnixMs).startUnixMs - nowUnixMs <= rules.announceMs;
}

function riftPick<T>(items: T[], pick: number, seedId: number): T[] {
    const order: number[] = [];
    for (let i = 0; i < items.length; i++)
        order.push(i);
    const take = Math.min(pick, order.length);
    const seed = u64FromNumber(seedId);
    for (let i = 0; i < take; i++) {
        const swap = i + u64ModSmall(splitMixAt(seed, i + 1), order.length - i);
        const kept = order[i];
        order[i] = order[swap];
        order[swap] = kept;
    }
    const picked: T[] = [];
    for (let i = 0; i < take; i++)
        picked.push(items[order[i]]);
    return picked;
}

function riftModifiersFor(rules: RiftRules, windowId: number): string[] {
    return riftPick(rules.modifiers, rules.modifiersPerWindow, windowId);
}

function riftMilestoneGems(rules: RiftRules, riftId: number): string[] {
    return riftPick(rules.milestones.rareGems, rules.milestones.rareGemsPerRift, riftId);
}

function riftMilestoneStep(rules: RiftRules, stage: number): RiftMilestoneStep | null {
    const milestones = rules.milestones;
    if (!(stage > 0) || stage % milestones.every !== 0)
        return null;
    let found: RiftMilestoneStep | null = null;
    for (const step of milestones.steps)
        if (step.from <= stage)
            found = step;
    return found;
}

function riftMilestoneGem(rules: RiftRules, riftId: number, stage: number): string {
    const gems = riftMilestoneGems(rules, riftId);
    return gems.length === 0 ? "" : gems[Math.floor(stage / rules.milestones.every) % gems.length];
}

function riftWindowReply(rules: RiftRules, nowUnixMs: number): string {
    const open = riftWindowAt(rules, nowUnixMs);
    const next = riftNext(rules, nowUnixMs);
    return JSON.stringify({
        serverUnixMs: nowUnixMs,
        open: open === null ? null : { id: open.id, startUnixMs: open.startUnixMs, endUnixMs: open.endUnixMs, modifiers: riftModifiersFor(rules, open.id) },
        next: { id: next.id, startUnixMs: next.startUnixMs, endUnixMs: next.endUnixMs, modifiers: riftModifiersFor(rules, next.id) },
        announced: riftAnnounced(rules, nowUnixMs)
    });
}

function riftWindowRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return riftWindowReply(RiftServerRules, Date.now());
}
