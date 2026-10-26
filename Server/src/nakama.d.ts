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
    interface StorageWriteRequest { collection: string; key: string; userId: string; value: { [key: string]: any }; permissionRead?: number; permissionWrite?: number; }
    interface Nakama {
        matchCreate(module: string, params?: { [key: string]: any }): string;
        storageWrite(objects: StorageWriteRequest[]): any;
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
    }
}

declare const PartyServerRules: PartyServerConfig;
