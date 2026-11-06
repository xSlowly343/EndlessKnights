const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.SeasonalEventServerRules;
const day = 86400000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const pumpkin = Date.UTC(2026, 9, 31, 12);
const pumpkinWindow = "pumpkin_night_2026";

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

test("four windows a year, two weeks each, the shop three days longer", () => {
  assert.strictEqual(rules.events.length, 4);
  const today = server.walletDayOf(pumpkin);
  assert.strictEqual(server.eventActiveWindow(today).id, pumpkinWindow);
  const window = server.eventActiveWindow(today);
  assert.strictEqual(server.eventActiveWindow(window.endDay), null);
  assert.strictEqual(server.eventShopWindow(window.endDay + rules.shopGraceDays - 1).id, pumpkinWindow);
  assert.strictEqual(server.eventShopWindow(window.endDay + rules.shopGraceDays), null);
  assert.strictEqual(server.eventActiveWindow(server.walletDayOf(Date.UTC(2027, 0, 3, 12))).id, "long_night_2026");
});

test("tokens have a daily cap and only count for the open window", () => {
  const { nk } = world("ann");
  const first = plain(server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: rules.dailyTokenCap + 50 }, pumpkin));
  assert.strictEqual(first.accepted, rules.dailyTokenCap);
  assert.strictEqual(first.tokens, rules.dailyTokenCap);
  assert.strictEqual(plain(server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: 10 }, pumpkin + 1000)).accepted, 0);
  assert.strictEqual(plain(server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: 10 }, pumpkin + day)).accepted, 10);
  assert.throws(() => server.eventProgress(nk, "ann", { window: "solstice_2026", tokens: 10 }, pumpkin), /event is over/);
  assert.throws(() => server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: -1 }, pumpkin), /bad request/);
});

test("goals pay tokens and pass points once, all goals pay crystals once", () => {
  const { nk } = world("ann");
  const passStart = Date.UTC(2026, 10, 18, 12);
  const window = "pumpkin_night_2026";
  const some = plain(server.eventProgress(nk, "ann", { window: window, goals: ["guests", "guests"] }, pumpkin));
  assert.deepStrictEqual(some.freshGoals, ["guests"]);
  assert.strictEqual(some.tokens, rules.goalTokens);
  const again = plain(server.eventProgress(nk, "ann", { window: window, goals: ["guests"] }, pumpkin));
  assert.deepStrictEqual(again.freshGoals, []);
  assert.throws(() => server.eventProgress(nk, "ann", { window: window, goals: ["nothing"] }, pumpkin), /bad goal/);
  const all = plain(server.eventProgress(nk, "ann", { window: window, goals: rules.goals }, pumpkin));
  assert.strictEqual(all.freshCrystals, rules.allGoalsCrystals);
  assert.strictEqual(plain(server.eventProgress(nk, "ann", { window: window, goals: rules.goals }, pumpkin)).freshCrystals, 0);
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.crystals, rules.allGoalsCrystals);
  assert.ok(passStart > pumpkin);
});

test("the shop sells event cosmetics for tokens until three days after the event", () => {
  const { nk } = world("ann");
  const item = rules.events[0].shop.find((entry) => entry.price <= rules.dailyTokenCap);
  assert.throws(() => server.eventBuy(nk, "ann", item.cosmetic, pumpkin), /not enough tokens/);
  server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: item.price }, pumpkin);
  const bought = plain(server.eventBuy(nk, "ann", item.cosmetic, pumpkin));
  assert.deepStrictEqual(bought.owned, [item.cosmetic]);
  assert.strictEqual(bought.tokens, 0);
  assert.throws(() => server.eventBuy(nk, "ann", item.cosmetic, pumpkin), /already owned/);
  assert.throws(() => server.eventBuy(nk, "ann", "skin_duelist", pumpkin), /not for sale/);
  const window = server.eventActiveWindow(server.walletDayOf(pumpkin));
  const late = window.endDay * day + (rules.shopGraceDays + 1) * day;
  assert.throws(() => server.eventBuy(nk, "ann", rules.events[0].shop[1].cosmetic, late), /not for sale/);
});

test("tokens burn when the next event comes", () => {
  const { nk } = world("ann");
  server.eventProgress(nk, "ann", { window: pumpkinWindow, tokens: 100 }, pumpkin);
  const winter = Date.UTC(2026, 11, 31, 12);
  const view = plain(server.eventProgress(nk, "ann", { window: "long_night_2026", tokens: 5 }, winter));
  assert.strictEqual(view.tokens, 5);
});

test("the clan gets the event emblem when members bring enough guests", () => {
  const w = world("ann", "bob");
  const clan = w.call("ann", "clanCreateRpc", {});
  w.call("bob", "clanJoinRpc", { code: clan.code });
  const half = Math.ceil(rules.clanTarget / 2);
  const per = Math.min(half, rules.clanGuestsPerMember);
  for (const user of ["ann", "bob"])
    server.eventProgress(w.nk, user, { window: pumpkinWindow, guests: per }, pumpkin);
  const clanId = server.clanOf(w.nk, "ann", pumpkin).id;
  const owned = plain(server.clanCosmeticRead(w.nk, clanId).value.owned);
  assert.strictEqual(owned.includes(rules.events[0].clanEmblem), per * 2 >= rules.clanTarget);
});

test("the event boss board keeps the best score and the top get the champion frame after the event", () => {
  const { nk } = world("ann", "bob");
  server.eventProgress(nk, "ann", { window: pumpkinWindow, score: 30000 }, pumpkin);
  server.eventProgress(nk, "bob", { window: pumpkinWindow, score: 10000 }, pumpkin);
  server.eventProgress(nk, "ann", { window: pumpkinWindow, score: 5000 }, pumpkin);
  const board = plain(server.eventBoard(nk, "bob", pumpkin));
  assert.strictEqual(board.top[0].userId, "ann");
  assert.strictEqual(board.top[0].score, 30000);
  assert.strictEqual(board.mine.rank, 2);
  const window = server.eventActiveWindow(server.walletDayOf(pumpkin));
  const after = window.endDay * day + day;
  assert.deepStrictEqual(plain(server.eventChampion(nk, "ann", server.walletDayOf(after))), [rules.championFrame]);
  assert.deepStrictEqual(plain(server.eventChampion(nk, "ann", server.walletDayOf(after))), []);
});
