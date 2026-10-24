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

function riftModifiersFor(rules: RiftRules, windowId: number): string[] {
    const order: number[] = [];
    for (let i = 0; i < rules.modifiers.length; i++)
        order.push(i);
    const take = Math.min(rules.modifiersPerWindow, order.length);
    const seed = u64FromNumber(windowId);
    for (let i = 0; i < take; i++) {
        const swap = i + u64ModSmall(splitMixAt(seed, i + 1), order.length - i);
        const kept = order[i];
        order[i] = order[swap];
        order[swap] = kept;
    }
    const picked: string[] = [];
    for (let i = 0; i < take; i++)
        picked.push(rules.modifiers[order[i]]);
    return picked;
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
