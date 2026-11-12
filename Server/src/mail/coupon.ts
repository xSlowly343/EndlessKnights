interface Coupon {
    reward: MailReward;
    from?: number;
    until?: number;
    limit?: number;
    used?: number;
}

interface CouponTries {
    hour: number;
    count: number;
}

const CouponCollection = "coupons";
const CouponUsedCollection = "coupon_used";
const CouponTriesCollection = "coupon_tries";
const CouponTriesKey = "tries";
const CouponTriesPerHour = 10;
const CouponMinLength = 4;
const CouponMaxLength = 24;
const CouponKind = "coupon";
const CouponHourMs = 3600000;

function couponCode(raw: unknown): string {
    if (typeof raw !== "string")
        return "";
    const code = raw.replace(/[\s-]/g, "").toUpperCase();
    return code.length >= CouponMinLength && code.length <= CouponMaxLength && /^[A-Z0-9]+$/.test(code) ? code : "";
}

function couponAmount(raw: unknown): number {
    return typeof raw === "number" && isFinite(raw) && raw > 0 ? Math.floor(raw) : 0;
}

function couponReward(raw: any): MailReward {
    const reward = raw || {};
    return { shards: couponAmount(reward.shards), ingots: couponAmount(reward.ingots), friendship: couponAmount(reward.friendship), essence: couponAmount(reward.essence) };
}

function couponPut(nk: nkruntime.Nakama, rawCode: string, coupon: Coupon): string {
    const code = couponCode(rawCode);
    if (code.length === 0)
        throw new Error("bad code");
    const value: Coupon = { reward: couponReward(coupon.reward), from: couponAmount(coupon.from), until: couponAmount(coupon.until), limit: couponAmount(coupon.limit), used: 0 };
    nk.storageWrite([{ collection: CouponCollection, key: code, userId: PartySystemUser, value: value as any, version: "*", permissionRead: 0, permissionWrite: 0 }]);
    return code;
}

function couponTry(nk: nkruntime.Nakama, userId: string, now: number): boolean {
    const hour = Math.floor(now / CouponHourMs);
    const objects = nk.storageRead([{ collection: CouponTriesCollection, key: CouponTriesKey, userId: userId }]);
    const tries: CouponTries = objects.length > 0 && (objects[0].value as CouponTries).hour === hour ? objects[0].value as CouponTries : { hour: hour, count: 0 };
    if (tries.count >= CouponTriesPerHour)
        return false;
    tries.count++;
    nk.storageWrite([{ collection: CouponTriesCollection, key: CouponTriesKey, userId: userId, value: tries as any, permissionRead: 0, permissionWrite: 0 }]);
    return true;
}

function couponRedeem(nk: nkruntime.Nakama, userId: string, rawCode: unknown, now: number): { status: string; letter?: MailLetter } {
    const code = couponCode(rawCode);
    if (code.length === 0)
        return { status: "unknown" };
    if (!couponTry(nk, userId, now))
        return { status: "tooMany" };
    const objects = nk.storageRead([{ collection: CouponCollection, key: code, userId: PartySystemUser }]);
    if (objects.length === 0)
        return { status: "unknown" };
    const coupon = objects[0].value as Coupon;
    if (coupon.from && now < coupon.from)
        return { status: "early" };
    if (coupon.until && now >= coupon.until)
        return { status: "expired" };
    if (nk.storageRead([{ collection: CouponUsedCollection, key: code, userId: userId }]).length > 0)
        return { status: "used" };
    if (coupon.limit && (coupon.used || 0) >= coupon.limit)
        return { status: "spent" };
    coupon.used = (coupon.used || 0) + 1;
    try {
        nk.storageWrite([
            { collection: CouponUsedCollection, key: code, userId: userId, value: { at: now } as any, version: "*", permissionRead: 0, permissionWrite: 0 },
            { collection: CouponCollection, key: code, userId: PartySystemUser, value: coupon as any, version: objects[0].version, permissionRead: 0, permissionWrite: 0 }
        ]);
    } catch (error) {
        return { status: "busy" };
    }
    const letter: MailLetter = { id: CouponKind + ":" + code, kind: CouponKind, at: now, place: 0, of: 0, depth: 0, title: code, reward: couponReward(coupon.reward), claimed: false, claimedAt: 0 };
    if (!mailSend(nk, userId, letter))
        throw new Error("the gift was not sent");
    return { status: "done", letter: letter };
}

function couponRedeemRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(couponRedeem(nk, partyCaller(ctx), partyRequest(payload).code, Date.now()));
}

function couponPutRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    if (ctx.userId)
        throw new Error("only the server key may add coupons");
    const request = partyRequest(payload);
    return JSON.stringify({ code: couponPut(nk, request.code, { reward: request.reward, from: request.from, until: request.until, limit: request.limit }) });
}
