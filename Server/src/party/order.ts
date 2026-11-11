interface HideoutOrderRule {
    id: string;
    fromTier: number;
    needs: { counter: string; target: number }[];
}

interface HideoutOrder {
    week: number;
    id: string;
    from: { [counter: string]: number };
    done: boolean;
    redrawn: boolean;
}

function hideoutOrderWeek(nowUnixMs: number): number {
    return Math.floor(walletDayOf(nowUnixMs) / HideoutServerRules.orderDays);
}

function hideoutOrderRule(id: string): HideoutOrderRule | null {
    return HideoutServerRules.orderGoals.filter((goal) => goal.id === id)[0] || null;
}

function hideoutOrderCounter(value: HideoutValue, chronicle: HideoutChronicle, counter: string): number {
    switch (counter) {
        case "bosses": return chronicle.bosses;
        case "guardians": return chronicle.guardiansTogether;
        case "sitMinutes": return Math.floor(value.sitSeconds / 60);
        default: return value.counters[counter] || 0;
    }
}

function hideoutOrderPick(value: HideoutValue, chronicle: HideoutChronicle, week: number, skip: string): HideoutOrder | null {
    const goals = HideoutServerRules.orderGoals.filter((goal) => goal.fromTier <= value.tier && goal.id !== skip);
    if (goals.length === 0)
        return null;
    const goal = goals[(week * 31 + value.orders * 7 + skip.length) % goals.length];
    const from: { [counter: string]: number } = {};
    for (const need of goal.needs)
        from[need.counter] = hideoutOrderCounter(value, chronicle, need.counter);
    return { week: week, id: goal.id, from: from, done: false, redrawn: skip.length > 0 };
}

function hideoutOrderHave(value: HideoutValue, chronicle: HideoutChronicle, order: HideoutOrder, need: { counter: string; target: number }): number {
    return Math.max(0, Math.min(need.target, hideoutOrderCounter(value, chronicle, need.counter) - (order.from[need.counter] || 0)));
}

function hideoutOrderStep(value: HideoutValue, chronicle: HideoutChronicle, nowUnixMs: number): boolean {
    if (value.tier < HideoutServerRules.orderFromTier) {
        value.order = null;
        return false;
    }
    const week = hideoutOrderWeek(nowUnixMs);
    if (!value.order || value.order.week !== week || !hideoutOrderRule(value.order.id))
        value.order = hideoutOrderPick(value, chronicle, week, "");
    const order = value.order;
    const rule = order ? hideoutOrderRule(order.id) : null;
    if (!order || !rule || order.done)
        return false;
    if (!rule.needs.every((need) => hideoutOrderHave(value, chronicle, order, need) >= need.target))
        return false;
    order.done = true;
    value.orders++;
    return true;
}

function hideoutOrderView(value: HideoutValue, chronicle: HideoutChronicle, leader: boolean, nowUnixMs: number): { [key: string]: any } | null {
    const order = value.order;
    const rule = order ? hideoutOrderRule(order.id) : null;
    if (!order || !rule)
        return null;
    const days = HideoutServerRules.orderDays;
    return {
        id: order.id, done: order.done, daysLeft: (order.week + 1) * days - walletDayOf(nowUnixMs),
        canRedraw: leader && !order.done && !order.redrawn,
        needs: rule.needs.map((need) => ({ counter: need.counter, have: hideoutOrderHave(value, chronicle, order, need), target: need.target }))
    };
}

function hideoutOrderRedraw(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    return hideoutUpdate(nk, userId, (hideout, meta, chronicle) => {
        if (meta.leader !== userId)
            throw new Error("only the leader can do that");
        hideoutOrderStep(hideout, chronicle, nowUnixMs);
        const order = hideout.order;
        if (!order || order.done || order.redrawn)
            throw new Error("the order cannot be changed");
        const next = hideoutOrderPick(hideout, chronicle, order.week, order.id);
        if (!next)
            throw new Error("no other order");
        hideout.order = next;
    }, nowUnixMs);
}

function hideoutOrderRedrawRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(hideoutOrderRedraw(nk, partyCaller(ctx), Date.now()));
}
