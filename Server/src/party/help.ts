interface HelpRules {
    seconds: number;
    answerMs: number;
    cooldownMs: number;
    defaultAccept: string;
}

declare const HelpServerRules: HelpRules;

const HelpCollection = "help";
const HelpSettingsKey = "settings";
const HelpCallsKey = "calls";
const HelpInviteCollection = "help_invite";
const HelpNotificationCode = 120;
const HelpAcceptAll = "all";
const HelpAcceptFriends = "friends";
const HelpAcceptNone = "none";
const HelpAccepts = [HelpAcceptAll, HelpAcceptFriends, HelpAcceptNone];
const HelpRefused = "this player does not take calls from you";

interface HelpInvite {
    from: string;
    name: string;
    partyId: string;
    matchId: string;
    until: number;
}

function helpAcceptOf(nk: nkruntime.Nakama, userId: string): string {
    const objects = nk.storageRead([{ collection: HelpCollection, key: HelpSettingsKey, userId: userId }]);
    const accept = objects.length > 0 ? objects[0].value.accept : null;
    return HelpAccepts.indexOf(accept) >= 0 ? accept : HelpServerRules.defaultAccept;
}

function helpFriends(nk: nkruntime.Nakama, a: string, b: string, nowUnixMs: number): boolean {
    const clan = clanGroupOfUser(nk, a, nowUnixMs);
    const other = clan ? clanGroupOfUser(nk, b, nowUnixMs) : null;
    return !!clan && !!other && clan.id === other.id;
}

function helpTakesCalls(nk: nkruntime.Nakama, caller: string, target: string, nowUnixMs: number): boolean {
    const accept = helpAcceptOf(nk, target);
    if (accept === HelpAcceptNone)
        return false;
    if ((partyBlocksOf(nk, [target])[target] || []).indexOf(caller) >= 0)
        return false;
    return accept === HelpAcceptAll || helpFriends(nk, caller, target, nowUnixMs);
}

function helpSettingsRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    if (request.accept !== undefined) {
        if (HelpAccepts.indexOf(request.accept) < 0)
            throw new Error("bad request");
        nk.storageWrite([{ collection: HelpCollection, key: HelpSettingsKey, userId: userId, value: { accept: request.accept }, permissionRead: 1, permissionWrite: 0 }]);
    }
    return JSON.stringify({ accept: helpAcceptOf(nk, userId) });
}

function helpCallRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const caller = partyCaller(ctx);
    const request = partyRequest(payload);
    const target = request.userId;
    const now = Date.now();
    if (typeof target !== "string" || target === caller || nk.usersGetId([target]).length === 0)
        throw new Error("bad request");
    const group = partyGroupOfUser(nk, caller);
    if (!group)
        throw new Error("not in a party");
    if (partyMetaOf(group).members.indexOf(target) >= 0)
        throw new Error("already in your party");
    if (!helpTakesCalls(nk, caller, target, now))
        throw new Error(HelpRefused);
    const calls = nk.storageRead([{ collection: HelpCollection, key: HelpCallsKey, userId: caller }]);
    if (calls.length > 0 && now - (calls[0].value.lastAt || 0) < HelpServerRules.cooldownMs)
        throw new Error("wait before calling again");
    const matchId = partyLiveMatchId(nk, group.id);
    if (!matchId)
        throw new Error("no boss to help with");
    const until = now + HelpServerRules.answerMs;
    const reply = nk.matchSignal(matchId, "invite:" + target + ":" + until);
    if (reply !== "ok")
        throw new Error(reply || "no boss to help with");
    const invite: HelpInvite = { from: caller, name: chronicleNameOf(nk, caller), partyId: group.id, matchId: matchId, until: until };
    nk.storageWrite([
        { collection: HelpInviteCollection, key: caller, userId: target, value: invite as any, permissionRead: 1, permissionWrite: 0 },
        { collection: HelpCollection, key: HelpCallsKey, userId: caller, value: { lastAt: now }, permissionRead: 1, permissionWrite: 0 }
    ]);
    try {
        nk.notificationSend(target, "help_call", { from: caller, name: invite.name, until: until }, HelpNotificationCode, caller, false);
    } catch (error) {
        logger.warn("call for help was not announced to %s: %s", target, String(error));
    }
    return JSON.stringify({ until: until });
}

function helpInvitesOf(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): HelpInvite[] {
    const invites: HelpInvite[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, HelpInviteCollection, 100, cursor);
        for (const object of page.objects || [])
            if ((object.value as HelpInvite).until > nowUnixMs)
                invites.push(object.value as HelpInvite);
        cursor = page.cursor || undefined;
    } while (cursor);
    invites.sort((a, b) => b.until - a.until);
    return invites;
}

function helpInvitesRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const invites = helpInvitesOf(nk, partyCaller(ctx), Date.now());
    return JSON.stringify({ invites: invites.map((invite) => ({ from: invite.from, name: invite.name, until: invite.until })) });
}

function helpAnswerRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    if (typeof request.from !== "string")
        throw new Error("bad request");
    const objects = nk.storageRead([{ collection: HelpInviteCollection, key: request.from, userId: userId }]);
    const invite = objects.length > 0 ? objects[0].value as HelpInvite : null;
    if (!invite || invite.until <= Date.now() || !nk.matchGet(invite.matchId))
        throw new Error("the call is over");
    nk.storageDelete([{ collection: HelpInviteCollection, key: request.from, userId: userId }]);
    return JSON.stringify({ matchId: invite.matchId, seconds: HelpServerRules.seconds });
}

function walletGrantHelp(nk: nkruntime.Nakama, logger: nkruntime.Logger, bosses: { [userId: string]: number }, nowUnixMs: number): void {
    for (const userId of Object.keys(bosses)) {
        try {
            walletGrant(nk, userId, "help_call", bosses[userId], nowUnixMs);
        } catch (error) {
            logger.warn("friendship for help was not granted to %s: %s", userId, String(error));
        }
    }
}
