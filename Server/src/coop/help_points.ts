interface CoopHelpRules {
    maxPoints: number;
    mentorMax: number;
    bossesMax: number;
    bossesPerPoint: number;
    riftMax: number;
    riftStages: number[];
    wanderersMax: number;
    ghostMax: number;
    ghostBosses: number[];
    aurasMax: number;
    auraHours: number[];
}

interface CoopHelpProfile {
    bosses: number;
    rift: number[];
    wanderers: number[];
    ghost?: number;
    auraSeconds?: number;
}

declare const CoopHelpServerRules: CoopHelpRules;

const CoopHelpCollection = "coop_help";
const CoopHelpKey = "profile";
const CoopHelpWriteAttempts = 3;

function coopHelpEmpty(): CoopHelpProfile {
    return { bosses: 0, rift: [], wanderers: [] };
}

function coopHelpChange(nk: nkruntime.Nakama, userId: string, change: (profile: CoopHelpProfile) => boolean): void {
    for (let attempt = 0; attempt < CoopHelpWriteAttempts; attempt++) {
        const objects = nk.storageRead([{ collection: CoopHelpCollection, key: CoopHelpKey, userId: userId }]);
        const profile = objects.length > 0 ? objects[0].value as CoopHelpProfile : coopHelpEmpty();
        if (!change(profile))
            return;
        try {
            nk.storageWrite([{ collection: CoopHelpCollection, key: CoopHelpKey, userId: userId, value: profile, version: objects.length > 0 ? objects[0].version : "*", permissionRead: 1, permissionWrite: 0 }]);
            return;
        } catch (error) {
            if (attempt === CoopHelpWriteAttempts - 1)
                throw error;
        }
    }
}

function coopHelpRead(nk: nkruntime.Nakama, userId: string): CoopHelpProfile {
    const objects = nk.storageRead([{ collection: CoopHelpCollection, key: CoopHelpKey, userId: userId }]);
    return objects.length > 0 ? objects[0].value as CoopHelpProfile : coopHelpEmpty();
}

function coopHelpBosses(nk: nkruntime.Nakama, logger: nkruntime.Logger, bosses: { [userId: string]: number }): void {
    for (const userId of Object.keys(bosses)) {
        if (!(bosses[userId] > 0))
            continue;
        try {
            coopHelpChange(nk, userId, (profile) => {
                if (Math.floor(profile.bosses / CoopHelpServerRules.bossesPerPoint) >= CoopHelpServerRules.bossesMax)
                    return false;
                profile.bosses += bosses[userId];
                return true;
            });
        } catch (error) {
            logger.warn("coop help bosses were not counted for %s: %s", userId, String(error));
        }
    }
}

function coopHelpRift(nk: nkruntime.Nakama, logger: nkruntime.Logger, members: string[], cleared: number): void {
    const reached = CoopHelpServerRules.riftStages.filter((stage) => cleared >= stage);
    if (reached.length === 0)
        return;
    for (const userId of members) {
        try {
            coopHelpChange(nk, userId, (profile) => {
                const fresh = reached.filter((stage) => profile.rift.indexOf(stage) < 0);
                if (fresh.length === 0)
                    return false;
                for (const stage of fresh)
                    profile.rift.push(stage);
                return true;
            });
        } catch (error) {
            logger.warn("coop help rift stages were not counted for %s: %s", userId, String(error));
        }
    }
}

function coopHelpWanderers(nk: nkruntime.Nakama, logger: nkruntime.Logger, userIds: string[], nowUnixMs: number): void {
    const week = raidWeekOf(nowUnixMs);
    for (const userId of userIds) {
        try {
            coopHelpChange(nk, userId, (profile) => {
                if (profile.wanderers.length >= CoopHelpServerRules.wanderersMax || profile.wanderers.indexOf(week) >= 0)
                    return false;
                profile.wanderers.push(week);
                return true;
            });
        } catch (error) {
            logger.warn("coop help wanderers were not counted for %s: %s", userId, String(error));
        }
    }
}

function coopHelpGhost(nk: nkruntime.Nakama, logger: nkruntime.Logger, ownerId: string, bosses: number): void {
    if (!(bosses > 0))
        return;
    const last = CoopHelpServerRules.ghostBosses[CoopHelpServerRules.ghostBosses.length - 1];
    try {
        coopHelpChange(nk, ownerId, (profile) => {
            if ((profile.ghost || 0) >= last)
                return false;
            profile.ghost = Math.min(last, (profile.ghost || 0) + bosses);
            return true;
        });
    } catch (error) {
        logger.warn("coop help ghost bosses were not counted for %s: %s", ownerId, String(error));
    }
}

function coopHelpAuras(nk: nkruntime.Nakama, logger: nkruntime.Logger, seconds: { [userId: string]: number }): void {
    const last = CoopHelpServerRules.auraHours[CoopHelpServerRules.auraHours.length - 1] * 3600;
    for (const userId of Object.keys(seconds)) {
        if (!(seconds[userId] > 0))
            continue;
        try {
            coopHelpChange(nk, userId, (profile) => {
                if ((profile.auraSeconds || 0) >= last)
                    return false;
                profile.auraSeconds = Math.min(last, (profile.auraSeconds || 0) + seconds[userId]);
                return true;
            });
        } catch (error) {
            logger.warn("coop help aura hours were not counted for %s: %s", userId, String(error));
        }
    }
}

function coopHelpSteps(steps: number[], value: number): number {
    let reached = 0;
    while (reached < steps.length && value >= steps[reached])
        reached++;
    return reached;
}

function coopHelpNext(steps: number[], value: number): number {
    const reached = coopHelpSteps(steps, value);
    return reached < steps.length ? steps[reached] : 0;
}

function coopHelpWanderersParty(roles: string[], classes: number[]): boolean {
    if (classes.length < PartyMaxSlots)
        return false;
    for (let i = 0; i < classes.length; i++)
        for (let j = i + 1; j < classes.length; j++)
            if (classes[i] === classes[j] || roles[i] === roles[j])
                return false;
    return true;
}

function coopHelpView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const rules = CoopHelpServerRules;
    const profile = coopHelpRead(nk, userId);
    const mentor = Math.min(rules.mentorMax, mentorRead(nk, [userId])[userId].value.coopPoints);
    const bosses = Math.min(rules.bossesMax, Math.floor(profile.bosses / rules.bossesPerPoint));
    const rift = Math.min(rules.riftMax, profile.rift.length);
    const wanderers = Math.min(rules.wanderersMax, profile.wanderers.length);
    const ghostBosses = profile.ghost || 0;
    const ghost = Math.min(rules.ghostMax, coopHelpSteps(rules.ghostBosses, ghostBosses));
    const auraHours = Math.floor((profile.auraSeconds || 0) / 3600);
    const auras = Math.min(rules.aurasMax, coopHelpSteps(rules.auraHours, auraHours));
    return {
        points: Math.min(rules.maxPoints, mentor + bosses + rift + wanderers + ghost + auras),
        max: rules.maxPoints,
        sources: [
            { source: "mentor", points: mentor, max: rules.mentorMax },
            { source: "bosses", points: bosses, max: rules.bossesMax, progress: profile.bosses % rules.bossesPerPoint, next: bosses >= rules.bossesMax ? 0 : rules.bossesPerPoint },
            { source: "rift", points: rift, max: Math.min(rules.riftMax, rules.riftStages.length), stages: profile.rift },
            { source: "wanderers", points: wanderers, max: rules.wanderersMax, thisWeek: profile.wanderers.indexOf(raidWeekOf(nowUnixMs)) >= 0 },
            { source: "ghost", points: ghost, max: Math.min(rules.ghostMax, rules.ghostBosses.length), progress: ghostBosses, next: coopHelpNext(rules.ghostBosses, ghostBosses) },
            { source: "auras", points: auras, max: Math.min(rules.aurasMax, rules.auraHours.length), progress: auraHours, next: coopHelpNext(rules.auraHours, auraHours) }
        ]
    };
}

function coopHelpRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(coopHelpView(nk, partyCaller(ctx), Date.now()));
}
