interface MailReward {
    shards: number;
    uncutGems: number;
    ingots: number;
    friendship?: number;
}

interface MailLetter {
    id: string;
    kind: string;
    at: number;
    place: number;
    of: number;
    depth: number;
    title: string;
    reward: MailReward;
    claimed: boolean;
    claimedAt: number;
}

const MailCollection = "mail";
const MailListLimit = 50;

function mailSend(nk: nkruntime.Nakama, userId: string, letter: MailLetter): boolean {
    try {
        nk.storageWrite([{ collection: MailCollection, key: letter.id, userId: userId, value: letter as any, version: "*", permissionRead: 1, permissionWrite: 0 }]);
        return true;
    } catch (error) {
        return false;
    }
}

function mailLetters(nk: nkruntime.Nakama, userId: string): MailLetter[] {
    const letters: MailLetter[] = [];
    let cursor: string | undefined = undefined;
    do {
        const page: { objects?: nkruntime.StorageObject[]; cursor?: string } = nk.storageList(userId, MailCollection, 100, cursor);
        for (const object of page.objects || [])
            letters.push(object.value as MailLetter);
        cursor = page.cursor || undefined;
    } while (cursor);
    letters.sort((a, b) => (a.claimed === b.claimed ? 0 : a.claimed ? 1 : -1) || b.at - a.at);
    return letters.slice(0, MailListLimit);
}

function mailListRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    riftSettleDue(nk, logger, Date.now());
    const letters = mailLetters(nk, userId);
    let unread = 0;
    for (const letter of letters)
        if (!letter.claimed)
            unread++;
    return JSON.stringify({ letters: letters, unread: unread });
}

function mailClaimRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const request = partyRequest(payload);
    if (typeof request.id !== "string" || request.id.length === 0)
        throw new Error("bad request");
    const objects = nk.storageRead([{ collection: MailCollection, key: request.id, userId: userId }]);
    if (objects.length === 0)
        throw new Error("no such letter");
    const letter = objects[0].value as MailLetter;
    if (letter.claimed)
        throw new Error("already claimed");
    letter.claimed = true;
    letter.claimedAt = Date.now();
    try {
        nk.storageWrite([{ collection: MailCollection, key: request.id, userId: userId, value: letter as any, version: objects[0].version, permissionRead: 1, permissionWrite: 0 }]);
    } catch (error) {
        throw new Error("already claimed");
    }
    return JSON.stringify({ letter: letter });
}
