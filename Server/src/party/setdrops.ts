interface PartySetDropStep {
    upTo?: number;
    rarities: { [rarity: string]: number };
}

interface PartySetRules {
    drops: { [source: string]: { chance: number; steps: PartySetDropStep[] } };
    sourceOf: { [lineSource: string]: string };
    far: { perMinute: { [source: string]: number }; firstMs: number; dungeonTiers: number };
    craft: { currency: string; cost: { [rarity: string]: number } };
    sets: { id: string; line: string; kind: string; rarity: string; source: string; pieces: string[]; server: { stat: string; kind: string; value: number }[] }[];
}

interface PartySetDrop {
    set: string;
    piece: string;
    source: string;
}

interface PartySetDropsValue {
    pending: { [userId: string]: PartySetDrop[] };
    far?: { [userId: string]: number };
}

const PartySetDropsCollection = "party_set_drops";
const PartySetDropsAttempts = 4;
const PartySetDropsMax = 50;

function partySetRules(): PartySetRules {
    return PartyGrowthServerRules.partySets;
}

function partySetDropsRead(nk: nkruntime.Nakama, partyId: string): { value: PartySetDropsValue; version: string } {
    const objects = nk.storageRead([{ collection: PartySetDropsCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { pending: {} }, version: "*" };
    const stored = objects[0].value as PartySetDropsValue;
    return { value: { pending: stored.pending || {}, far: stored.far || {} }, version: objects[0].version };
}

function partySetDropsChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartySetDropsValue) => T): T {
    for (let attempt = 1; attempt <= PartySetDropsAttempts; attempt++) {
        const stored = partySetDropsRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartySetDropsCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partySetLinesOf(source: string): string[] {
    const rules = partySetRules();
    const lines: string[] = [];
    for (const set of rules.sets) {
        const dropSource = rules.sourceOf[set.source] || set.source;
        if (dropSource === source && lines.indexOf(set.line) < 0)
            lines.push(set.line);
    }
    return lines;
}

function partySetRarity(source: string, depth: number, random: () => number): string {
    const steps = partySetRules().drops[source].steps;
    const step = steps.filter((candidate) => candidate.upTo === undefined || depth <= candidate.upTo)[0] || steps[steps.length - 1];
    const names = Object.keys(step.rarities);
    const total = names.reduce((sum, name) => sum + step.rarities[name], 0);
    let roll = random() * total;
    for (const name of names) {
        roll -= step.rarities[name];
        if (roll < 0)
            return name;
    }
    return names[names.length - 1];
}

function partySetPick(source: string, depth: number, random: () => number): PartySetDrop | null {
    const lines = partySetLinesOf(source);
    if (lines.length === 0)
        return null;
    const rarity = partySetRarity(source, depth, random);
    const line = lines[Math.floor(random() * lines.length)];
    const kind = random() < 0.5 ? "armor" : "jewelry";
    const set = partySetRules().sets.filter((candidate) => candidate.line === line && candidate.kind === kind && candidate.rarity === rarity)[0]
        || partySetRules().sets.filter((candidate) => candidate.line === line && candidate.kind === kind).slice(-1)[0];
    if (!set)
        return null;
    return { set: set.id, piece: set.pieces[Math.floor(random() * set.pieces.length)], source: source };
}

function partySetDropRoll(nk: nkruntime.Nakama, partyId: string, userIds: string[], source: string, depth: number, times: number, seed: string): number {
    const rules = partySetRules().drops[source];
    if (!rules || !partyId || userIds.length === 0 || !(times > 0) || !partySetsOpened(nk, partyId))
        return 0;
    const random = partyColossusSeed(seed);
    const found: { userId: string; drop: PartySetDrop }[] = [];
    for (const userId of userIds)
        for (let i = 0; i < times; i++)
            if (random() < rules.chance) {
                const drop = partySetPick(source, depth, random);
                if (drop)
                    found.push({ userId: userId, drop: drop });
            }
    if (found.length === 0)
        return 0;
    try {
        partySetDropsChange(nk, partyId, (value) => {
            for (const item of found) {
                const pending = value.pending[item.userId] || [];
                if (pending.length < PartySetDropsMax)
                    pending.push(item.drop);
                value.pending[item.userId] = pending;
            }
        });
    } catch (error) {
        return 0;
    }
    return found.length;
}

function partySetDropsClaim(nk: nkruntime.Nakama, userId: string, room: unknown): { [key: string]: any } {
    if (typeof room !== "number" || room !== Math.floor(room) || room < 0 || room > 1000)
        throw new Error("bad request");
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    return partySetDropsChange(nk, group.id, (value) => {
        const pending = value.pending[userId] || [];
        const taken = pending.slice(0, room);
        const left = pending.slice(room);
        if (left.length > 0)
            value.pending[userId] = left;
        else
            delete value.pending[userId];
        return { drops: taken, left: left.length };
    });
}

function partySetFar(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const source = request.source;
    const bosses = request.bosses;
    const depth = request.depth;
    const whole = (value: unknown, max: number) => typeof value === "number" && value === Math.floor(value) && value >= 1 && value <= max;
    if ((source !== "dungeons" && source !== "rift") || !whole(bosses, 100000) || !whole(depth, 1e12))
        throw new Error("bad request");
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    if (!partySetsOpened(nk, group.id))
        throw new Error("party sets are not open yet");
    const rules = partySetRules().far;
    const trusted = cloudTrustedRead(nk, userId);
    const deepest = source === "dungeons" ? rules.dungeonTiers : Math.max(1, trusted ? trusted.facts.record : 1);
    const counted = partySetDropsChange(nk, group.id, (value) => {
        const far = value.far || {};
        const key = userId + ":" + source;
        const perMinute = rules.perMinute[source];
        const last = far[key] || nowUnixMs - rules.firstMs;
        const allowed = Math.floor(Math.max(0, nowUnixMs - last) / 60000 * perMinute);
        const taken = Math.min(bosses, allowed);
        if (taken > 0)
            far[key] = taken < allowed ? last + taken * 60000 / perMinute : nowUnixMs;
        value.far = far;
        return taken;
    });
    const dropped = counted > 0 ? partySetDropRoll(nk, group.id, [userId], source, Math.min(depth, deepest), counted, group.id + ":" + userId + ":" + source + ":" + nowUnixMs) : 0;
    return { counted: counted, dropped: dropped };
}

function partySetFarRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partySetFar(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partySetCraft(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const set = typeof request.set === "string" ? partySetRules().sets.filter((candidate) => candidate.id === request.set)[0] : undefined;
    if (!set || typeof request.piece !== "string" || set.pieces.indexOf(request.piece) < 0)
        throw new Error("bad request");
    const cost = partySetRules().craft.cost[set.rarity];
    if (!cost)
        throw new Error("this rarity is not crafted, it only drops");
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    if (!partySetsOpened(nk, group.id))
        throw new Error("party sets are not open yet");
    const left = walletSpend(nk, userId, partySetRules().craft.currency, cost, "party_set_craft", nowUnixMs);
    return { set: set.id, piece: request.piece, cost: cost, oath: left };
}

function partySetDropsClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partySetDropsClaim(nk, partyCaller(ctx), partyRequest(payload).room));
}

function partySetCraftRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partySetCraft(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partySetGrant(nk: nkruntime.Nakama, userIds: string[], source: string, times: number, nowUnixMs: number): void {
    for (const userId of userIds) {
        try {
            walletGrant(nk, userId, source, times, nowUnixMs);
        } catch (error) {
            continue;
        }
    }
}

function partySetReward(nk: nkruntime.Nakama, partyId: string, userIds: string[], source: string, depth: number, oathSource: string, times: number, nowUnixMs: number): void {
    if (!partySetsOpened(nk, partyId))
        return;
    partySetGrant(nk, userIds, oathSource, times, nowUnixMs);
    partySetDropRoll(nk, partyId, userIds, source, depth, times, partyId + ":" + source + ":" + nowUnixMs);
}

function partySetBattle(nk: nkruntime.Nakama, partyId: string, bosses: number): void {
    const group = partyId ? nk.groupsGetId([partyId])[0] : undefined;
    if (!group || !(bosses > 0))
        return;
    partySetReward(nk, partyId, partyMetaOf(group).members, "battle", 0, "oath_battle", bosses, Date.now());
}

function partySetColossusKill(nk: nkruntime.Nakama, partyId: string, tier: number, nowUnixMs: number): void {
    const group = nk.groupsGetId([partyId])[0];
    if (!group)
        return;
    const members = partyMetaOf(group).members;
    partySetReward(nk, partyId, members, "colossus", tier, "oath_colossus", tier, nowUnixMs);
    const kill = partyColossusRead(nk, partyId).value.log.filter((entry) => entry.tier === tier).slice(-1)[0];
    if (kill && tier >= 46 && kill.hardships.length >= 3)
        partySetDropRoll(nk, partyId, members, "extreme", tier, 1, partyId + ":extreme:" + nowUnixMs);
    if (PartyGrowthServerRules.banner.colossus.indexOf(tier) >= 0)
        partySetDropRoll(nk, partyId, members, "banner", tier, 1, partyId + ":banner:" + nowUnixMs);
}

function partySetTrialClear(nk: nkruntime.Nakama, partyId: string, fighters: string[], tier: number, firstClear: boolean, nowUnixMs: number): void {
    if (!partySetsOpened(nk, partyId))
        return;
    if (firstClear)
        partySetGrant(nk, fighters, "oath_trial", tier, nowUnixMs);
    partySetDropRoll(nk, partyId, fighters, "trials", tier, 1, partyId + ":trials:" + nowUnixMs);
    if (tier >= 30)
        partySetDropRoll(nk, partyId, fighters, "extreme", tier, 1, partyId + ":extreme:" + nowUnixMs);
    if (firstClear && PartyGrowthServerRules.banner.trial.indexOf(tier) >= 0)
        partySetDropRoll(nk, partyId, fighters, "banner", tier, 1, partyId + ":banner:" + nowUnixMs);
}
