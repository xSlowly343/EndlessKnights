interface PartyMascotRules {
    maxLevel: number;
    levelBase: number;
    levelStep: number;
    perMinute: number;
    stages: { from: number; id: string }[];
    branchFrom: number;
    foods: { id: string; experience: number }[];
    favouriteMultiplier: number;
    feedPerDay: number;
    changeMs: number;
    changeKeep: number;
    tempers: string[];
    bond: { hearts: number; perHeart: number; feed: number; pet: number; petsPerDay: number; perMinute: number };
    coats: { id: string; partyLevel: number; trialTier: number; colossusKills: number; event: boolean }[];
    gear: { id: string; slot: string; level: number; colossusTier: number }[];
    beasts: { id: string; favourite: string; branches: string[] }[];
    treeExperience: PartyTrialTreeLine[];
    treeFeedExperience: PartyTrialTreeLine[];
    treeFeeds: PartyTrialTreeLine[];
    treeFeedCost: PartyTrialTreeLine[];
    treeStageEarly: PartyTrialTreeLine[];
    relicExperience: { id: string; value: number }[];
}

interface PartyMascotMember {
    day: number;
    fed: number;
    pets: number;
}

interface PartyMascotValue {
    beast: string;
    chosenAt: number;
    experience: number;
    temper: string;
    branch: string;
    votes: { [userId: string]: string };
    coat: string;
    gear: { [slot: string]: string };
    bonds: { [userId: string]: number };
    members: { [userId: string]: PartyMascotMember };
    extraCoats: string[];
}

interface PartyMascotSituation {
    partyId: string;
    level: number;
    members: string[];
    leader: string;
    levels: { [node: string]: number };
    trialBest: number;
    colossusKills: number;
    colossusBest: number;
    relicExperience: number;
}

const PartyMascotCollection = "party_mascot";
const PartyMascotAttempts = 4;

function partyMascotRules(): PartyMascotRules {
    return PartyGrowthServerRules.mascot;
}

function partyMascotEmpty(): PartyMascotValue {
    return { beast: "", chosenAt: 0, experience: 0, temper: "", branch: "", votes: {}, coat: "common", gear: {}, bonds: {}, members: {}, extraCoats: [] };
}

function partyMascotRead(nk: nkruntime.Nakama, partyId: string): { value: PartyMascotValue; version: string } {
    const objects = nk.storageRead([{ collection: PartyMascotCollection, key: partyId, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: partyMascotEmpty(), version: "*" };
    const stored = objects[0].value as PartyMascotValue;
    const value = partyMascotEmpty();
    for (const key of Object.keys(value))
        if ((stored as any)[key] !== undefined)
            (value as any)[key] = (stored as any)[key];
    return { value: value, version: objects[0].version };
}

function partyMascotChange<T>(nk: nkruntime.Nakama, partyId: string, change: (value: PartyMascotValue) => T): T {
    for (let attempt = 1; attempt <= PartyMascotAttempts; attempt++) {
        const stored = partyMascotRead(nk, partyId);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: PartyMascotCollection, key: partyId, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the party is busy, try again");
}

function partyMascotNeed(level: number): number {
    const rules = partyMascotRules();
    return level >= rules.maxLevel ? 0 : rules.levelBase + rules.levelStep * (level - 1);
}

function partyMascotLevel(experience: number): { level: number; into: number; need: number } {
    const rules = partyMascotRules();
    let level = 1;
    let left = Math.max(0, experience);
    while (level < rules.maxLevel && left >= partyMascotNeed(level)) {
        left -= partyMascotNeed(level);
        level++;
    }
    return { level: level, into: level >= rules.maxLevel ? 0 : left, need: partyMascotNeed(level) };
}

function partyMascotStage(level: number, early: number): string {
    const stages = partyMascotRules().stages;
    let stage = stages[0].id;
    stages.forEach((candidate, index) => {
        const from = index === stages.length - 1 ? candidate.from - early : candidate.from;
        if (level >= from)
            stage = candidate.id;
    });
    return stage;
}

function partyMascotBeast(id: unknown): { id: string; favourite: string; branches: string[] } | undefined {
    return typeof id === "string" ? partyMascotRules().beasts.filter((beast) => beast.id === id)[0] : undefined;
}

function partyMascotSituation(nk: nkruntime.Nakama, userId: string): PartyMascotSituation {
    const group = partyGroupOfUser(nk, userId);
    if (!group)
        throw new Error("not in a party");
    const meta = partyMetaOf(group);
    const trials = partyTrialRead(nk, group.id).value;
    const colossus = partyColossusRead(nk, group.id).value;
    let relicExperience = 0;
    for (const relic of partyMascotRules().relicExperience)
        relicExperience += relic.value * (colossus.relics[relic.id] || 0);
    return {
        partyId: group.id,
        level: partyGrowthLevel(partyGrowthRead(nk, group.id).value.experience).level,
        members: meta.members,
        leader: meta.leader,
        levels: partyTreasuryRead(nk, group.id).value.levels,
        trialBest: Object.keys(trials.best).reduce((best, id) => Math.max(best, trials.best[id]), 0),
        colossusKills: colossus.kills,
        colossusBest: colossus.best,
        relicExperience: relicExperience
    };
}

function partyMascotOpened(situation: PartyMascotSituation): boolean {
    return PartyGrowthServerRules.unlocks.some((unlock) => unlock.kind === "mascot" && unlock.level <= situation.level);
}

function partyMascotExperienceScale(situation: PartyMascotSituation): number {
    return 1 + partyTrialTreeValue(situation.levels, partyMascotRules().treeExperience) + situation.relicExperience;
}

function partyMascotCoats(value: PartyMascotValue, situation: PartyMascotSituation): string[] {
    return partyMascotRules().coats.filter((coat) => {
        if (coat.event)
            return value.extraCoats.indexOf(coat.id) >= 0;
        return situation.level >= coat.partyLevel && situation.trialBest >= coat.trialTier && situation.colossusKills >= coat.colossusKills;
    }).map((coat) => coat.id);
}

function partyMascotGear(value: PartyMascotValue, situation: PartyMascotSituation): string[] {
    const level = partyMascotLevel(value.experience).level;
    return partyMascotRules().gear.filter((item) => level >= item.level && situation.colossusBest >= item.colossusTier).map((item) => item.id);
}

function partyMascotToday(member: PartyMascotMember, nowUnixMs: number): PartyMascotMember {
    const day = Math.floor(nowUnixMs / PartyGrowthDayMs);
    if (member.day !== day) {
        member.day = day;
        member.fed = 0;
        member.pets = 0;
    }
    return member;
}

function partyMascotMember(value: PartyMascotValue, userId: string, nowUnixMs: number): PartyMascotMember {
    const member = partyMascotToday(value.members[userId] || { day: 0, fed: 0, pets: 0 }, nowUnixMs);
    value.members[userId] = member;
    return member;
}

function partyMascotFeedLimit(situation: PartyMascotSituation): number {
    return partyMascotRules().feedPerDay + Math.floor(partyTrialTreeValue(situation.levels, partyMascotRules().treeFeeds) + 1e-9);
}

function partyMascotHearts(bond: number): number {
    const rules = partyMascotRules().bond;
    return Math.min(rules.hearts, Math.floor(Math.max(0, bond) / rules.perHeart + 1e-9));
}

function partyMascotView(nk: nkruntime.Nakama, value: PartyMascotValue, situation: PartyMascotSituation, userId: string, nowUnixMs: number, accepted: { [key: string]: any } | null): { [key: string]: any } {
    const level = partyMascotLevel(value.experience);
    const member = partyMascotToday(value.members[userId] ? Object.assign({}, value.members[userId]) : { day: 0, fed: 0, pets: 0 }, nowUnixMs);
    const ids = situation.members;
    const names = partyTreasuryNames(nk, ids);
    return {
        partyId: situation.partyId, opened: partyMascotOpened(situation), leader: situation.leader === userId,
        beast: value.beast, experience: Math.floor(value.experience), level: level.level, into: Math.floor(level.into), need: level.need,
        stage: partyMascotStage(level.level, Math.round(partyTrialTreeValue(situation.levels, partyMascotRules().treeStageEarly))),
        temper: value.temper, branch: value.branch, branchOpen: level.level >= partyMascotRules().branchFrom && !value.branch,
        votes: value.votes, coat: value.coat, coats: partyMascotCoats(value, situation), gear: value.gear, gearOwned: partyMascotGear(value, situation),
        changeAt: value.chosenAt > 0 ? value.chosenAt + partyMascotRules().changeMs : 0,
        feedLeft: Math.max(0, partyMascotFeedLimit(situation) - member.fed), petsLeft: Math.max(0, partyMascotRules().bond.petsPerDay - member.pets),
        bonds: ids.map((id) => ({ userId: id, name: names[id] || "", bond: Math.floor(value.bonds[id] || 0), hearts: partyMascotHearts(value.bonds[id] || 0) })),
        yourBond: Math.floor(value.bonds[userId] || 0), yourHearts: partyMascotHearts(value.bonds[userId] || 0),
        accepted: accepted
    };
}

function partyMascotAct(nk: nkruntime.Nakama, userId: string, nowUnixMs: number, act: (value: PartyMascotValue, situation: PartyMascotSituation) => { [key: string]: any } | null): { [key: string]: any } {
    const situation = partyMascotSituation(nk, userId);
    if (!partyMascotOpened(situation))
        throw new Error("the mascot is not open yet");
    return partyMascotChange(nk, situation.partyId, (value) => {
        const accepted = act(value, situation);
        return partyMascotView(nk, value, situation, userId, nowUnixMs, accepted);
    });
}

function partyMascotMine(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const situation = partyMascotSituation(nk, userId);
    return partyMascotView(nk, partyMascotRead(nk, situation.partyId).value, situation, userId, nowUnixMs, null);
}

function partyMascotTemper(partyId: string, beast: string, nowUnixMs: number): string {
    const tempers = partyMascotRules().tempers;
    return tempers[Math.floor(partyColossusSeed(partyId + ":" + beast + ":" + nowUnixMs)() * tempers.length)];
}

function partyMascotChoose(nk: nkruntime.Nakama, userId: string, id: unknown, nowUnixMs: number): { [key: string]: any } {
    const beast = partyMascotBeast(id);
    if (!beast)
        throw new Error("bad request");
    return partyMascotAct(nk, userId, nowUnixMs, (value, situation) => {
        if (situation.leader !== userId)
            throw new Error("only the leader can do that");
        if (value.beast === beast.id)
            throw new Error("this beast is already the mascot");
        if (value.beast && nowUnixMs < value.chosenAt + partyMascotRules().changeMs)
            throw new Error("the mascot can change once in " + Math.round(partyMascotRules().changeMs / 86400000) + " days");
        if (value.beast)
            value.experience *= partyMascotRules().changeKeep;
        value.beast = beast.id;
        value.chosenAt = nowUnixMs;
        value.temper = partyMascotTemper(situation.partyId, beast.id, nowUnixMs);
        value.branch = "";
        value.votes = {};
        return null;
    });
}

function partyMascotFeed(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const food = typeof request.food === "string" ? partyMascotRules().foods.filter((item) => item.id === request.food)[0] : undefined;
    const amount = request.amount;
    if (!food || typeof amount !== "number" || amount !== Math.floor(amount) || amount < 1 || amount > 1000)
        throw new Error("bad request");
    return partyMascotAct(nk, userId, nowUnixMs, (value, situation) => {
        const beast = partyMascotBeast(value.beast);
        if (!beast)
            throw new Error("choose a mascot first");
        const member = partyMascotMember(value, userId, nowUnixMs);
        const portions = Math.max(0, Math.min(amount, partyMascotFeedLimit(situation) - member.fed));
        if (portions === 0)
            throw new Error("the mascot has eaten enough from you today");
        const rules = partyMascotRules();
        const favourite = beast.favourite === food.id ? rules.favouriteMultiplier : 1;
        const feedBonus = 1 + partyTrialTreeValue(situation.levels, rules.treeFeedExperience);
        const cost = 1 + partyTrialTreeValue(situation.levels, rules.treeFeedCost);
        const experience = portions * food.experience * favourite * feedBonus / Math.max(0.1, cost) * partyMascotExperienceScale(situation);
        member.fed += portions;
        value.experience += experience;
        value.bonds[userId] = (value.bonds[userId] || 0) + portions * rules.bond.feed;
        return { portions: portions, experience: experience, favourite: favourite > 1 };
    });
}

function partyMascotPet(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return partyMascotAct(nk, userId, nowUnixMs, (value) => {
        if (!value.beast)
            throw new Error("choose a mascot first");
        const member = partyMascotMember(value, userId, nowUnixMs);
        if (member.pets >= partyMascotRules().bond.petsPerDay)
            throw new Error("the mascot wants to rest");
        member.pets++;
        value.bonds[userId] = (value.bonds[userId] || 0) + partyMascotRules().bond.pet;
        return { bond: partyMascotRules().bond.pet };
    });
}

function partyMascotBranch(nk: nkruntime.Nakama, userId: string, branch: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof branch !== "string")
        throw new Error("bad request");
    return partyMascotAct(nk, userId, nowUnixMs, (value, situation) => {
        const beast = partyMascotBeast(value.beast);
        if (!beast || beast.branches.indexOf(branch) < 0)
            throw new Error("bad request");
        if (value.branch)
            throw new Error("the branch is already chosen");
        if (partyMascotLevel(value.experience).level < partyMascotRules().branchFrom)
            throw new Error("the branch opens on level " + partyMascotRules().branchFrom);
        value.votes[userId] = branch;
        for (const id of Object.keys(value.votes))
            if (situation.members.indexOf(id) < 0)
                delete value.votes[id];
        const count = Object.keys(value.votes).filter((id) => value.votes[id] === branch).length;
        if (count * 2 > situation.members.length) {
            value.branch = branch;
            value.votes = {};
        }
        return { chosen: value.branch };
    });
}

function partyMascotLook(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    return partyMascotAct(nk, userId, nowUnixMs, (value, situation) => {
        if (situation.leader !== userId)
            throw new Error("only the leader can do that");
        if (request.coat !== undefined) {
            if (typeof request.coat !== "string" || partyMascotCoats(value, situation).indexOf(request.coat) < 0)
                throw new Error("this coat is not open yet");
            value.coat = request.coat;
        }
        if (request.gear !== undefined) {
            if (typeof request.gear !== "object" || request.gear === null)
                throw new Error("bad request");
            const owned = partyMascotGear(value, situation);
            for (const slot of Object.keys(request.gear)) {
                const id = request.gear[slot];
                const item = partyMascotRules().gear.filter((candidate) => candidate.id === id)[0];
                if (id === "") {
                    delete value.gear[slot];
                    continue;
                }
                if (!item || item.slot !== slot || owned.indexOf(id) < 0)
                    throw new Error("this gear is not open yet");
                value.gear[slot] = id;
            }
        }
        return null;
    });
}

function partyMascotBattle(nk: nkruntime.Nakama, partyId: string, userId: string, minutes: number): void {
    if (!partyId || !(minutes > 0))
        return;
    try {
        const situation = partyMascotSituation(nk, userId);
        partyMascotChange(nk, partyId, (value) => {
            if (!value.beast)
                return;
            value.experience += minutes * partyMascotRules().perMinute * partyMascotExperienceScale(situation);
            value.bonds[userId] = (value.bonds[userId] || 0) + minutes * partyMascotRules().bond.perMinute;
        });
    } catch (error) {
        return;
    }
}

function partyMascotRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotMine(nk, partyCaller(ctx), Date.now()));
}

function partyMascotChooseRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotChoose(nk, partyCaller(ctx), partyRequest(payload).beast, Date.now()));
}

function partyMascotFeedRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotFeed(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function partyMascotPetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotPet(nk, partyCaller(ctx), Date.now()));
}

function partyMascotBranchRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotBranch(nk, partyCaller(ctx), partyRequest(payload).branch, Date.now()));
}

function partyMascotLookRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(partyMascotLook(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}
