const TownPresenceCollection = "town_presence";
const TownStreetSeconds = 180;
const TownStreetFriends = 50;
const TownStreetShown = 12;
const TownStreetPlaces = ["town", "refuge", "hideout", "clanCamp"];

function townGroupMembers(nk: nkruntime.Nakama, group: nkruntime.Group | null): string[] {
    if (group === null)
        return [];
    return (nk.groupUsersList(group.id, 100).groupUsers || []).map((entry) => entry.user.userId);
}

function townStreet(nk: nkruntime.Nakama, userId: string, request: { [key: string]: any }, nowUnixMs: number): { [key: string]: any } {
    if (typeof request.here !== "boolean")
        throw new Error("bad request");
    const place = typeof request.place === "string" && TownStreetPlaces.indexOf(request.place) >= 0 ? request.place : TownStreetPlaces[0];
    if (request.here)
        nk.storageWrite([{ collection: TownPresenceCollection, key: userId, userId: PartySystemUser, value: { at: nowUnixMs, place: place }, permissionRead: 0, permissionWrite: 0 }]);
    else
        nk.storageDelete([{ collection: TownPresenceCollection, key: userId, userId: PartySystemUser }]);
    const friends = Array.isArray(request.friends) ? request.friends.filter((id: unknown) => typeof id === "string").slice(0, TownStreetFriends) : [];
    const party = townGroupMembers(nk, partyGroupOfUser(nk, userId));
    const clan = townGroupMembers(nk, clanGroupOfUser(nk, userId, nowUnixMs));
    const ties: { [id: string]: string[] } = {};
    const tie = (ids: string[], name: string) => {
        for (const id of ids) {
            if (id === userId)
                continue;
            (ties[id] = ties[id] || []).push(name);
        }
    };
    tie(party, "party");
    tie(clan, "clan");
    tie(friends, "friend");
    const ids = Object.keys(ties);
    if (ids.length === 0)
        return { street: [] };
    const seen = nk.storageRead(ids.map((id) => ({ collection: TownPresenceCollection, key: id, userId: PartySystemUser })));
    const names = partyNames(nk, seen.map((object) => object.key));
    const street = seen
        .filter((object) => nowUnixMs - object.value.at <= TownStreetSeconds * 1000)
        .map((object) => ({ userId: object.key, name: names[object.key] || "", place: object.value.place as string, ties: ties[object.key], at: object.value.at as number }))
        .sort((a, b) => b.ties.length - a.ties.length || b.at - a.at)
        .slice(0, TownStreetShown);
    return { street: street.map((entry) => ({ userId: entry.userId, name: entry.name, place: entry.place, ties: entry.ties })) };
}

function townStreetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townStreet(nk, partyCaller(ctx), partyRequest(payload), Date.now()));
}
