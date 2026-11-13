const StreetKey = "here";
const StreetPlaces = ["town", "hideout", "clanCamp"];

function streetTies(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [id: string]: string[] } {
    const ties: { [id: string]: string[] } = {};
    const tie = (ids: string[], name: string) => {
        for (const id of ids)
            if (id !== userId)
                (ties[id] = ties[id] || []).push(name);
    };
    const party = partyGroupOfUser(nk, userId);
    tie(party ? partyMetaOf(party).members : [], "party");
    const clan = clanGroupOfUser(nk, userId, nowUnixMs);
    tie(clan ? clanMetaOf(clan).members : [], "clan");
    const friends = nk.friendsList(userId, 1000, CircleMutualFriends);
    tie((friends.friends || []).filter((friend) => friend.user).map((friend) => friend.user!.userId), "friend");
    return ties;
}

function townStreet(nk: nkruntime.Nakama, userId: string, here: unknown, place: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof here !== "boolean")
        throw new Error("bad request");
    const where = typeof place === "string" && StreetPlaces.indexOf(place) >= 0 ? place : StreetPlaces[0];
    if (here)
        nk.storageWrite([{ collection: TownCollection, key: StreetKey, userId: userId, value: { at: nowUnixMs, place: where } as any, permissionRead: 1, permissionWrite: 0 }]);
    else
        nk.storageDelete([{ collection: TownCollection, key: StreetKey, userId: userId }]);
    const ties = streetTies(nk, userId, nowUnixMs);
    const people = Object.keys(ties);
    const marks = people.length > 0 ? nk.storageRead(people.map((id) => ({ collection: TownCollection, key: StreetKey, userId: id }))) : [];
    const present = marks.filter((mark) => nowUnixMs - (mark.value as { at: number }).at <= TownServerRules.streetSeconds * 1000)
        .sort((a, b) => ties[b.userId!].length - ties[a.userId!].length || (b.value as { at: number }).at - (a.value as { at: number }).at)
        .slice(0, TownServerRules.streetShown);
    const users = present.length > 0 ? nk.usersGetId(present.map((mark) => mark.userId!)) : [];
    return {
        walkers: present.map((mark) => {
            const user = users.filter((entry) => entry.userId === mark.userId)[0];
            return { userId: mark.userId, name: user ? user.displayName || user.username : "", place: (mark.value as { place?: string }).place || StreetPlaces[0], ties: ties[mark.userId!] };
        })
    };
}

function townStreetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const request = partyRequest(payload);
    return JSON.stringify(townStreet(nk, partyCaller(ctx), request.here, request.place, Date.now()));
}
