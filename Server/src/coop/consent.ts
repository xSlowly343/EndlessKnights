interface ConsentValue {
    shown: string[];
}

const ConsentCollection = "consent";
const ConsentKey = "names";
const ConsentTownTop = "town_top";
const ConsentMuseum = "museum";
const ConsentPlaces = [ConsentTownTop, ConsentMuseum];

function consentRead(nk: nkruntime.Nakama, userId: string): ConsentValue {
    const objects = nk.storageRead([{ collection: ConsentCollection, key: ConsentKey, userId: userId }]);
    const shown = objects.length > 0 ? (objects[0].value as { shown?: unknown }).shown : [];
    return { shown: Array.isArray(shown) ? shown.filter((place) => typeof place === "string" && ConsentPlaces.indexOf(place) >= 0) as string[] : [] };
}

function consentShows(nk: nkruntime.Nakama, userId: string, place: string): boolean {
    return consentRead(nk, userId).shown.indexOf(place) >= 0;
}

function consentShownName(nk: nkruntime.Nakama, userId: string, place: string): string {
    if (!consentShows(nk, userId, place))
        return "";
    const users = nk.usersGetId([userId]);
    return users.length > 0 ? users[0].displayName || users[0].username : "";
}

function consentSet(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, place: unknown, shown: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof place !== "string" || ConsentPlaces.indexOf(place) < 0 || typeof shown !== "boolean")
        throw new Error("bad request");
    const value = consentRead(nk, userId);
    value.shown = value.shown.filter((entry) => entry !== place);
    if (shown)
        value.shown.push(place);
    nk.storageWrite([{ collection: ConsentCollection, key: ConsentKey, userId: userId, value: value as any, permissionRead: 1, permissionWrite: 0 }]);
    if (place === ConsentTownTop)
        townBuildRename(nk, logger, userId, nowUnixMs);
    return { shown: value.shown };
}

function consentRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    if (!payload)
        return JSON.stringify({ shown: consentRead(nk, partyCaller(ctx)).shown });
    const request = partyRequest(payload);
    return JSON.stringify(consentSet(nk, logger, partyCaller(ctx), request.place, request.shown, Date.now()));
}
