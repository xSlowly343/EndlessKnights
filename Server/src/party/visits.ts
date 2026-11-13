interface CompanionVisitRules {
    companions: { [id: string]: string };
    biomes: { [biome: string]: { bone: string; fish: string } };
    visitMs: number;
    visitsPerDay: number;
    visitSource: string;
}

interface CompanionVisitOut {
    host: string;
    hostName: string;
    companion: string;
    biome: string;
    endsAt: number;
    day: number;
    count: number;
}

interface CompanionVisitGuest {
    from: string;
    fromName: string;
    companion: string;
    name: string;
    stage: number;
    endsAt: number;
}

const CompanionVisitCollection = "companion_visit";
const CompanionVisitKey = "out";
const CompanionGuestCollection = "companion_guests";
const CompanionVisitNameLimit = 24;

function companionVisitRules(): CompanionVisitRules {
    return CompanionServerRules as unknown as CompanionVisitRules;
}

function companionVisitRead(nk: nkruntime.Nakama, userId: string): { value: CompanionVisitOut | null; version: string } {
    const objects = nk.storageRead([{ collection: CompanionVisitCollection, key: CompanionVisitKey, userId: userId }]);
    if (objects.length === 0)
        return { value: null, version: "*" };
    return { value: objects[0].value as CompanionVisitOut, version: objects[0].version };
}

function companionVisitBiome(nk: nkruntime.Nakama, host: string, companion: string): string {
    const rules = companionVisitRules();
    const snapshot = guestSnapshot(nk, host);
    if (snapshot && rules.biomes[snapshot.theme])
        return snapshot.theme;
    return rules.companions[companion];
}

function companionVisitSend(nk: nkruntime.Nakama, userId: string, request: any, nowUnixMs: number): { [key: string]: any } {
    const rules = companionVisitRules();
    const host = request && request.host;
    const companion = request && request.companion;
    const name = request && request.name;
    const stage = request && request.stage;
    if (typeof host !== "string" || host.length === 0 || typeof companion !== "string" || rules.companions[companion] === undefined)
        throw new Error("bad request");
    if (typeof name !== "string" || name.length > CompanionVisitNameLimit || typeof stage !== "number" || stage !== Math.floor(stage) || stage < 0 || stage > 10)
        throw new Error("bad request");
    if (host === userId)
        throw new Error("not to yourself");
    const users = nk.usersGetId([host, userId]);
    const hostUser = users.filter((user) => user.userId === host)[0];
    if (!hostUser)
        throw new Error("no such player");
    if (!circleClose(nk, userId, host, nowUnixMs))
        throw new Error("visits go only to friends, clan and party");
    const stored = companionVisitRead(nk, userId);
    const day = walletDayOf(nowUnixMs);
    if (stored.value && stored.value.endsAt > 0)
        throw new Error("a companion is already visiting");
    const count = stored.value && stored.value.day === day ? stored.value.count : 0;
    if (count >= rules.visitsPerDay)
        throw new Error("no more visits today");
    const self = users.filter((user) => user.userId === userId)[0];
    const out: CompanionVisitOut = { host: host, hostName: hostUser.displayName || hostUser.username, companion: companion, biome: companionVisitBiome(nk, host, companion), endsAt: nowUnixMs + rules.visitMs, day: day, count: count + 1 };
    const guest: CompanionVisitGuest = { from: userId, fromName: self ? self.displayName || self.username : "", companion: companion, name: name, stage: stage, endsAt: out.endsAt };
    nk.storageWrite([
        { collection: CompanionVisitCollection, key: CompanionVisitKey, userId: userId, value: out as any, version: stored.version, permissionRead: 1, permissionWrite: 0 },
        { collection: CompanionGuestCollection, key: userId, userId: host, value: guest as any, permissionRead: 1, permissionWrite: 0 }
    ]);
    return companionVisitView(nk, userId, nowUnixMs);
}

function companionVisitGuests(nk: nkruntime.Nakama, host: string, nowUnixMs: number): CompanionVisitGuest[] {
    const guests: CompanionVisitGuest[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(host, CompanionGuestCollection, 100, cursor);
        for (const object of page.objects || [])
            if ((object.value as CompanionVisitGuest).endsAt > nowUnixMs)
                guests.push(object.value as CompanionVisitGuest);
        cursor = page.cursor || undefined;
    } while (cursor);
    return guests.sort((a, b) => a.endsAt - b.endsAt);
}

function companionVisitView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const out = companionVisitRead(nk, userId).value;
    const day = walletDayOf(nowUnixMs);
    const away = out && out.endsAt > 0 ? out : null;
    return {
        away: away ? { host: away.host, hostName: away.hostName, companion: away.companion, endsAt: away.endsAt, ready: nowUnixMs >= away.endsAt } : null,
        visitsLeft: Math.max(0, companionVisitRules().visitsPerDay - (out && out.day === day ? out.count : 0)),
        guests: companionVisitGuests(nk, userId, nowUnixMs).map((guest) => ({ fromName: guest.fromName, companion: guest.companion, name: guest.name, stage: guest.stage, endsAt: guest.endsAt })),
        now: nowUnixMs
    };
}

function companionVisitClaim(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const stored = companionVisitRead(nk, userId);
    const out = stored.value;
    if (!out || out.endsAt <= 0)
        throw new Error("no companion is visiting");
    if (nowUnixMs < out.endsAt)
        throw new Error("not back yet");
    const rules = companionVisitRules();
    const loot = rules.biomes[out.biome] || rules.biomes[rules.companions[out.companion]];
    const gifts: { [item: string]: number } = {};
    if (loot) {
        gifts[loot.fish] = (gifts[loot.fish] || 0) + 1;
        gifts[loot.bone] = (gifts[loot.bone] || 0) + 1;
    }
    const done: CompanionVisitOut = { host: "", hostName: "", companion: "", biome: "", endsAt: 0, day: out.day, count: out.count };
    nk.storageWrite([{ collection: CompanionVisitCollection, key: CompanionVisitKey, userId: userId, value: done as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
    nk.storageDelete([{ collection: CompanionGuestCollection, key: userId, userId: out.host }]);
    inflowGrant(nk, userId, gifts, nowUnixMs);
    let hostSigns = 0;
    try {
        hostSigns = walletGrant(nk, out.host, rules.visitSource, 1, nowUnixMs);
    } catch (error) {
        hostSigns = 0;
    }
    const view = companionVisitView(nk, userId, nowUnixMs);
    view.companion = out.companion;
    view.hostName = out.hostName;
    view.gifts = gifts;
    view.hostSigns = hostSigns;
    return view;
}

function companionVisitSendRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionVisitSend(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function companionVisitRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionVisitView(nk, partyCaller(ctx), Date.now()));
}

function companionVisitClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(companionVisitClaim(nk, partyCaller(ctx), Date.now()));
}
