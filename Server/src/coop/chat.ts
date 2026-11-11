interface ChatMessage {
    seq: number;
    from: string;
    name: string;
    role: string;
    text: string;
    at: number;
}

interface ChatValue {
    seq: number;
    messages: ChatMessage[];
    removed: number[];
}

interface ChatRoom {
    channel: SocialChannelRule;
    key: string;
    moderators: string[];
}

interface ChatMute {
    until: number;
    scope: string;
    reason: string;
}

const ChatCollection = "chat";
const ChatRateCollection = "chat_rate";
const ChatMuteCollection = "chat_mutes";
const ChatReportCollection = "chat_reports";
const ChatReportedCollection = "chat_reported";
const ChatRoleCollection = "chat_roles";
const ChatOperator = "operator";
const ChatWorld = "world";
const ChatParty = "party";
const ChatClan = "clan";
const ChatScopeAll = "all";
const ChatWriteAttempts = 5;
const ChatHourMs = 3600000;
const ChatModerationLimit = 100;

function chatChannel(id: any): SocialChannelRule {
    for (const channel of SocialServerRules.channels)
        if (channel.id === id)
            return channel;
    throw new Error("bad request");
}

function chatRoom(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): ChatRoom {
    const channel = chatChannel(request.channel);
    if (channel.id === ChatWorld) {
        const languages = PartyDirectoryRules.languages;
        return { channel: channel, key: ChatWorld + "-" + partyPick(request.language, languages, languages[0]), moderators: [] };
    }
    if (channel.id === ChatParty) {
        const party = partyGroupOfUser(nk, userId);
        if (!party)
            throw new Error("not in a party");
        return { channel: channel, key: party.id, moderators: [partyMetaOf(party).leader] };
    }
    const clan = clanOf(nk, userId, nowUnixMs);
    const meta = clanMetaOf(clan);
    return { channel: channel, key: clan.id, moderators: [meta.leader].concat(meta.officers) };
}

function chatRoleOf(nk: nkruntime.Nakama, userId: string): string {
    const objects = nk.storageRead([{ collection: ChatRoleCollection, key: userId, userId: PartySystemUser }]);
    const role = objects.length > 0 ? objects[0].value.role : "";
    return SocialServerRules.roles.some((rule) => rule.id === role) ? role : "";
}

function chatRoleRule(role: string): { id: string; muteHours: number; grantsRoles: boolean } {
    if (role === ChatOperator)
        return { id: ChatOperator, muteHours: SocialServerRules.maxMuteHours, grantsRoles: true };
    return SocialServerRules.roles.filter((rule) => rule.id === role)[0];
}

function chatRead(nk: nkruntime.Nakama, key: string): { value: ChatValue; version: string } {
    const objects = nk.storageRead([{ collection: ChatCollection, key: key, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { value: { seq: 0, messages: [], removed: [] }, version: "*" };
    const value = objects[0].value as ChatValue;
    return { value: { seq: value.seq || 0, messages: value.messages || [], removed: value.removed || [] }, version: objects[0].version || "*" };
}

function chatChange<T>(nk: nkruntime.Nakama, key: string, change: (value: ChatValue) => T): T {
    for (let attempt = 1; attempt <= ChatWriteAttempts; attempt++) {
        const stored = chatRead(nk, key);
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: ChatCollection, key: key, userId: PartySystemUser, value: stored.value as any, version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("the chat is busy, try again");
}

function chatForget(nk: nkruntime.Nakama, key: string): void {
    nk.storageDelete([{ collection: ChatCollection, key: key, userId: PartySystemUser }]);
}

function chatFresh(messages: ChatMessage[], nowUnixMs: number): ChatMessage[] {
    const since = nowUnixMs - SocialServerRules.chatDays * PartyDayMs;
    return messages.filter((message) => message.at >= since);
}

function chatMuteOf(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): ChatMute | null {
    const objects = nk.storageRead([{ collection: ChatMuteCollection, key: userId, userId: PartySystemUser }]);
    const mute = objects.length > 0 ? objects[0].value as ChatMute : null;
    return mute && mute.until > nowUnixMs ? mute : null;
}

function chatMuted(mute: ChatMute | null, channel: string): boolean {
    return !!mute && (mute.scope === ChatScopeAll || mute.scope === channel);
}

function chatSetMute(nk: nkruntime.Nakama, userId: string, hours: number, scope: string, reason: string, nowUnixMs: number): ChatMute {
    const mute: ChatMute = { until: nowUnixMs + Math.round(hours * ChatHourMs), scope: scope, reason: reason };
    nk.storageWrite([{ collection: ChatMuteCollection, key: userId, userId: PartySystemUser, value: mute as any, permissionRead: 0, permissionWrite: 0 }]);
    return mute;
}

function chatSpend(nk: nkruntime.Nakama, userId: string, channel: SocialChannelRule, nowUnixMs: number): void {
    const objects = nk.storageRead([{ collection: ChatRateCollection, key: channel.id, userId: userId }]);
    const sent: number[] = objects.length > 0 && Array.isArray(objects[0].value.at) ? objects[0].value.at : [];
    const recent = sent.filter((at) => at > nowUnixMs - 60000 && at <= nowUnixMs);
    if (recent.length >= channel.perMinute || (recent.length > 0 && nowUnixMs - recent[recent.length - 1] < channel.cooldownMs))
        throw new Error("too fast");
    recent.push(nowUnixMs);
    nk.storageWrite([{ collection: ChatRateCollection, key: channel.id, userId: userId, value: { at: recent }, permissionRead: 0, permissionWrite: 0 }]);
}

function chatText(value: any): string {
    if (typeof value !== "string")
        throw new Error("bad request");
    const text = socialTidy(value);
    if (text === "" || text.length > SocialServerRules.chatMax)
        throw new Error("bad request");
    return socialMask(text);
}

function chatView(nk: nkruntime.Nakama, userId: string, room: ChatRoom, value: ChatValue, after: any, nowUnixMs: number): { [key: string]: any } {
    const from = typeof after === "number" && after > 0 ? after : 0;
    const hidden = partyBlocksOf(nk, [userId])[userId] || [];
    const mute = chatMuteOf(nk, userId, nowUnixMs);
    const role = chatRoleOf(nk, userId);
    return {
        channel: room.channel.id,
        room: room.key,
        seq: value.seq,
        messages: chatFresh(value.messages, nowUnixMs).filter((message) => message.seq > from && hidden.indexOf(message.from) < 0),
        removed: value.removed,
        role: role,
        canModerate: role !== "" || room.moderators.indexOf(userId) >= 0,
        mutedUntil: chatMuted(mute, room.channel.id) ? mute!.until : 0
    };
}

function chatSend(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const room = chatRoom(nk, userId, request, nowUnixMs);
    const text = chatText(request.text);
    if (chatMuted(chatMuteOf(nk, userId, nowUnixMs), room.channel.id))
        throw new Error("muted");
    chatSpend(nk, userId, room.channel, nowUnixMs);
    const user = nk.usersGetId([userId])[0];
    const name = user ? user.displayName || user.username : "";
    const role = chatRoleOf(nk, userId);
    const value = chatChange(nk, room.key, (stored) => {
        stored.seq++;
        stored.messages = chatFresh(stored.messages, nowUnixMs);
        stored.messages.push({ seq: stored.seq, from: userId, name: name, role: role, text: text, at: nowUnixMs });
        if (stored.messages.length > room.channel.history)
            stored.messages.splice(0, stored.messages.length - room.channel.history);
        return stored;
    });
    return chatView(nk, userId, room, value, request.after, nowUnixMs);
}

function chatDrop(value: ChatValue, seq: number): ChatMessage | null {
    for (let i = 0; i < value.messages.length; i++) {
        if (value.messages[i].seq !== seq)
            continue;
        const dropped = value.messages.splice(i, 1)[0];
        value.removed.push(seq);
        if (value.removed.length > SocialServerRules.removedKept)
            value.removed.splice(0, value.removed.length - SocialServerRules.removedKept);
        return dropped;
    }
    return null;
}

function chatDelete(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const room = chatRoom(nk, userId, request, nowUnixMs);
    const staff = chatRoleOf(nk, userId) !== "";
    const value = chatChange(nk, room.key, (stored) => {
        const message = stored.messages.filter((candidate) => candidate.seq === request.seq)[0];
        if (!message)
            throw new Error("no such message");
        if (message.from !== userId && !staff && room.moderators.indexOf(userId) < 0)
            throw new Error("not yours");
        chatDrop(stored, message.seq);
        return stored;
    });
    return chatView(nk, userId, room, value, request.after, nowUnixMs);
}

function chatNoteReport(nk: nkruntime.Nakama, target: string, reporter: string, nowUnixMs: number): number {
    const objects = nk.storageRead([{ collection: ChatReportedCollection, key: target, userId: PartySystemUser }]);
    const stored: { [reporter: string]: number } = objects.length > 0 ? objects[0].value.reporters || {} : {};
    const since = nowUnixMs - SocialServerRules.autoMuteDays * PartyDayMs;
    const reporters: { [reporter: string]: number } = {};
    for (const id of Object.keys(stored))
        if (stored[id] >= since)
            reporters[id] = stored[id];
    reporters[reporter] = nowUnixMs;
    nk.storageWrite([{ collection: ChatReportedCollection, key: target, userId: PartySystemUser, value: { reporters: reporters }, permissionRead: 0, permissionWrite: 0 }]);
    return Object.keys(reporters).length;
}

function chatReport(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    const room = chatRoom(nk, userId, request, nowUnixMs);
    const reason = partyPick(request.reason, SocialServerRules.reportReasons, "");
    const message = chatRead(nk, room.key).value.messages.filter((candidate) => candidate.seq === request.seq)[0];
    if (reason === "" || !message)
        throw new Error("no such message");
    if (message.from === userId)
        throw new Error("bad request");
    nk.storageWrite([{
        collection: ChatReportCollection, key: message.from + ":" + userId, userId: PartySystemUser, permissionRead: 0, permissionWrite: 0,
        value: { target: message.from, name: message.name, reporter: userId, reason: reason, channel: room.channel.id, room: room.key, seq: message.seq, text: message.text, at: nowUnixMs }
    }]);
    let muted = false;
    if (room.channel.id === ChatWorld && chatNoteReport(nk, message.from, userId, nowUnixMs) >= SocialServerRules.autoMuteReporters && !chatMuteOf(nk, message.from, nowUnixMs)) {
        chatSetMute(nk, message.from, SocialServerRules.autoMuteHours, ChatWorld, "reports", nowUnixMs);
        muted = true;
    }
    logger.warn("chat report: %s reported %s in %s for %s", userId, message.from, room.channel.id, reason);
    return { ok: true, muted: muted };
}

function chatSendRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(chatSend(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function chatReadRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    const now = Date.now();
    const room = chatRoom(nk, userId, request, now);
    return JSON.stringify(chatView(nk, userId, room, chatRead(nk, room.key).value, request.after, now));
}

function chatDeleteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(chatDelete(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function chatReportRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(chatReport(nk, logger, partyCaller(ctx), partyRequest(payload), Date.now()));
}

function chatStaff(ctx: nkruntime.Context, nk: nkruntime.Nakama, payload: string): { role: string; request: { [key: string]: any } } {
    const role = ctx.userId ? chatRoleOf(nk, ctx.userId) : ChatOperator;
    if (role === "")
        throw { message: "not available to players", code: 7 };
    try {
        return { role: role, request: partyRequest(payload) };
    } catch (error) {
        throw { message: "bad request", code: 3 };
    }
}

function chatModerationRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = chatStaff(ctx, nk, payload).request;
    const now = Date.now();
    const since = now - (typeof request.days === "number" && request.days > 0 ? Math.min(365, request.days) : PartyModerationDays) * PartyDayMs;
    const reports = partyListAll(nk, ChatReportCollection).map((object) => object.value as { [key: string]: any })
        .filter((report) => report.at >= since)
        .sort((a, b) => b.at - a.at)
        .slice(0, ChatModerationLimit);
    const mutes = partyListAll(nk, ChatMuteCollection).map((object) => ({ userId: object.key, until: object.value.until, scope: object.value.scope, reason: object.value.reason }))
        .filter((mute) => mute.until > now);
    const staff = partyListAll(nk, ChatRoleCollection).map((object) => ({ userId: object.key, role: object.value.role }));
    return JSON.stringify({ reports: reports, mutes: mutes, staff: staff });
}

function chatMuteRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const staff = chatStaff(ctx, nk, payload);
    const request = staff.request;
    if (typeof request.userId !== "string" || request.userId === "" || request.userId === ctx.userId)
        throw { message: "bad request", code: 3 };
    if (staff.role !== ChatOperator && chatRoleOf(nk, request.userId) !== "")
        throw { message: "not available to players", code: 7 };
    const limit = chatRoleRule(staff.role).muteHours;
    const hours = typeof request.hours === "number" ? Math.max(0, Math.min(limit, request.hours)) : Math.min(limit, SocialServerRules.autoMuteHours);
    const scope = request.scope === undefined ? ChatScopeAll : partyPick(request.scope, [ChatScopeAll, ChatWorld, ChatParty, ChatClan], ChatScopeAll);
    const now = Date.now();
    if (hours === 0) {
        nk.storageDelete([{ collection: ChatMuteCollection, key: request.userId, userId: PartySystemUser }]);
        return JSON.stringify({ userId: request.userId, until: 0 });
    }
    const mute = chatSetMute(nk, request.userId, hours, scope, typeof request.reason === "string" ? request.reason : staff.role, now);
    logger.warn("chat mute: %s until %d in %s by %s", request.userId, mute.until, scope, ctx.userId || ChatOperator);
    return JSON.stringify({ userId: request.userId, until: mute.until, scope: mute.scope });
}

function chatRemoveRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = chatStaff(ctx, nk, payload).request;
    if (typeof request.room !== "string" || typeof request.seq !== "number")
        throw { message: "bad request", code: 3 };
    const removed = chatChange(nk, request.room, (stored) => chatDrop(stored, request.seq));
    return JSON.stringify({ removed: !!removed });
}

function chatRoleRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const staff = chatStaff(ctx, nk, payload);
    const request = staff.request;
    if (!chatRoleRule(staff.role).grantsRoles)
        throw { message: "not available to players", code: 7 };
    if (typeof request.userId !== "string" || request.userId === "" || nk.usersGetId([request.userId]).length === 0)
        throw { message: "bad request", code: 3 };
    const role = request.role === "" || request.role === undefined ? "" : partyPick(request.role, SocialServerRules.roles.map((rule) => rule.id), "");
    if (role === "")
        nk.storageDelete([{ collection: ChatRoleCollection, key: request.userId, userId: PartySystemUser }]);
    else
        nk.storageWrite([{ collection: ChatRoleCollection, key: request.userId, userId: PartySystemUser, value: { role: role }, permissionRead: 0, permissionWrite: 0 }]);
    logger.warn("chat role: %s is now %s by %s", request.userId, role || "a player", ctx.userId || ChatOperator);
    return JSON.stringify({ userId: request.userId, role: role });
}
