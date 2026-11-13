const StreetKey = "here";

function streetCircle(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): string[] {
    const people: string[] = [];
    const add = (id: string) => {
        if (id !== userId && people.indexOf(id) < 0)
            people.push(id);
    };
    const party = partyGroupOfUser(nk, userId);
    if (party)
        partyMetaOf(party).members.forEach(add);
    const clan = clanGroupOfUser(nk, userId, nowUnixMs);
    if (clan)
        clanMetaOf(clan).members.forEach(add);
    const friends = nk.friendsList(userId, 1000, CircleMutualFriends);
    for (const friend of friends.friends || [])
        if (friend.user)
            add(friend.user.userId);
    return people;
}

function townStreet(nk: nkruntime.Nakama, userId: string, here: unknown, nowUnixMs: number): { [key: string]: any } {
    if (typeof here !== "boolean")
        throw new Error("bad request");
    if (here)
        nk.storageWrite([{ collection: TownCollection, key: StreetKey, userId: userId, value: { at: nowUnixMs } as any, permissionRead: 1, permissionWrite: 0 }]);
    else
        nk.storageDelete([{ collection: TownCollection, key: StreetKey, userId: userId }]);
    const people = streetCircle(nk, userId, nowUnixMs);
    const marks = people.length > 0 ? nk.storageRead(people.map((id) => ({ collection: TownCollection, key: StreetKey, userId: id }))) : [];
    const present = marks.filter((mark) => nowUnixMs - (mark.value as { at: number }).at <= TownServerRules.streetSeconds * 1000)
        .map((mark) => mark.userId!).slice(0, TownServerRules.streetShown);
    const users = present.length > 0 ? nk.usersGetId(present) : [];
    return { walkers: users.map((user) => ({ userId: user.userId, name: user.displayName || user.username })) };
}

function townStreetRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(townStreet(nk, partyCaller(ctx), partyRequest(payload).here, Date.now()));
}
