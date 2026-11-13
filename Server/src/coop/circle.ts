const CircleMutualFriends = 0;
const CircleFriendPages = 10;

function circleFriends(nk: nkruntime.Nakama, userId: string, other: string): boolean {
    let cursor: string | undefined = undefined;
    for (let page = 0; page < CircleFriendPages; page++) {
        const list: { friends?: { user?: { userId: string }; state?: number }[]; cursor?: string } = nk.friendsList(userId, 1000, CircleMutualFriends, cursor);
        if ((list.friends || []).some((friend) => friend.user !== undefined && friend.user.userId === other))
            return true;
        cursor = list.cursor || undefined;
        if (!cursor)
            return false;
    }
    return false;
}

function circleSameGroup(first: nkruntime.Group | null, second: nkruntime.Group | null): boolean {
    return first !== null && second !== null && first.id === second.id;
}

function circleClose(nk: nkruntime.Nakama, userId: string, other: string, nowUnixMs: number): boolean {
    if (circleSameGroup(partyGroupOfUser(nk, userId), partyGroupOfUser(nk, other)))
        return true;
    if (circleSameGroup(clanGroupOfUser(nk, userId, nowUnixMs), clanGroupOfUser(nk, other, nowUnixMs)))
        return true;
    return circleFriends(nk, userId, other);
}
