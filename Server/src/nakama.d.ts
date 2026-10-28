declare namespace nkruntime {
    interface Context { matchId?: string; userId?: string; env: { [key: string]: string }; }
    interface Logger {
        info(format: string, ...params: any[]): void;
        warn(format: string, ...params: any[]): void;
        error(format: string, ...params: any[]): void;
    }
    interface Presence { userId: string; sessionId: string; username: string; node: string; }
    interface MatchMessage { sender: Presence; opCode: number; data: ArrayBuffer; reliable: boolean; receiveTimeMs: number; }
    interface MatchDispatcher {
        broadcastMessage(opCode: number, data: ArrayBuffer | string | null, presences?: Presence[] | null, sender?: Presence | null, reliable?: boolean): void;
        matchLabelUpdate(label: string): void;
    }
    interface StorageWriteRequest { collection: string; key: string; userId: string; value: { [key: string]: any }; version?: string; permissionRead?: number; permissionWrite?: number; }
    interface StorageReadRequest { collection: string; key: string; userId: string; }
    interface StorageDeleteRequest { collection: string; key: string; userId: string; version?: string; }
    interface StorageObject { collection: string; key: string; userId: string; version: string; value: { [key: string]: any }; }
    interface Group { id: string; name: string; langTag: string; open: boolean; edgeCount: number; maxCount: number; metadata: { [key: string]: any }; }
    interface User { userId: string; username: string; displayName: string; steamId: string; online: boolean; }
    interface GroupUser { user: User; state?: number; }
    interface Match { matchId: string; size: number; label: string; }
    interface Nakama {
        matchCreate(module: string, params?: { [key: string]: any }): string;
        matchGet(id: string): Match | null;
        matchSignal(id: string, data: string): string;
        storageRead(keys: StorageReadRequest[]): StorageObject[];
        storageWrite(objects: StorageWriteRequest[]): any;
        storageDelete(keys: StorageDeleteRequest[]): void;
        storageList(userId: string | undefined, collection: string, limit?: number, cursor?: string): { objects?: StorageObject[]; cursor?: string };
        uuidv4(): string;
        usersGetId(userIds: string[]): User[];
        groupCreate(userId: string, name: string, creatorId?: string | null, lang?: string | null, description?: string | null, avatarURL?: string | null,
            open?: boolean | null, metadata?: { [key: string]: any } | null, limit?: number | null): Group;
        groupUpdate(groupId: string, userId: string | null, name?: string | null, creatorId?: string | null, lang?: string | null, description?: string | null,
            avatarURL?: string | null, open?: boolean | null, metadata?: { [key: string]: any } | null, limit?: number | null): void;
        groupDelete(groupId: string): void;
        groupsGetId(groupIds: string[]): Group[];
        groupsList(name?: string, langTag?: string, open?: boolean, members?: number, limit?: number, cursor?: string): { groups?: Group[]; cursor?: string };
        groupUsersAdd(groupId: string, userIds: string[], callerId?: string): void;
        groupUsersKick(groupId: string, userIds: string[], callerId?: string): void;
        groupUsersPromote(groupId: string, userIds: string[], callerId?: string): void;
        groupUsersList(groupId: string, limit?: number, state?: number, cursor?: string): { groupUsers?: GroupUser[]; cursor?: string };
        userGroupsList(userId: string, limit?: number, state?: number, cursor?: string): { userGroups?: { group?: Group; state?: number }[]; cursor?: string };
    }
    type MatchState = { [key: string]: any };
    interface MatchHandler<T> {
        matchInit(ctx: Context, logger: Logger, nk: Nakama, params: { [key: string]: string }): { state: T; tickRate: number; label: string };
        matchJoinAttempt(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, presence: Presence, metadata: { [key: string]: any }): { state: T; accept: boolean; rejectMessage?: string } | null;
        matchJoin(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, presences: Presence[]): { state: T } | null;
        matchLeave(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, presences: Presence[]): { state: T } | null;
        matchLoop(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, messages: MatchMessage[]): { state: T } | null;
        matchTerminate(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, graceSeconds: number): { state: T } | null;
        matchSignal(ctx: Context, logger: Logger, nk: Nakama, dispatcher: MatchDispatcher, tick: number, state: T, data: string): { state: T; data?: string } | null;
    }
    interface Initializer {
        registerMatch<T>(name: string, handler: MatchHandler<T>): void;
        registerRpc(id: string, fn: (ctx: Context, logger: Logger, nk: Nakama, payload: string) => string): void;
        registerBeforeCreateGroup(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { name?: string }) => any): void;
        registerBeforeUpdateGroup(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeDeleteGroup(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeJoinGroup(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeLeaveGroup(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeAddGroupUsers(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeBanGroupUsers(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeKickGroupUsers(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforePromoteGroupUsers(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeDemoteGroupUsers(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { groupId?: string }) => any): void;
        registerBeforeListGroups(fn: (ctx: Context, logger: Logger, nk: Nakama, data: { [key: string]: any }) => any): void;
    }
}

declare const PartyServerRules: PartyServerConfig;
