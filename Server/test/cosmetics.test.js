const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.CosmeticServerRules;
const day = 86400000;
const now = Date.UTC(2026, 10, 5, 12, 0);
const plain = (o) => JSON.parse(JSON.stringify(o));
const item = (id) => rules.items.find((i) => i.id === id);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

const give = (nk, user, crystals) => server.walletGrant(nk, user, "crystal_purchase", crystals, now);

test("rules come from cosmetics.json and every reward exists", () => {
  assert.strictEqual(rules.items.filter((i) => i.source === "shop").length, 72);
  assert.strictEqual(item(rules.championTop10).source, "reward");
  assert.strictEqual(item(rules.warVictors).clan, true);
  assert.deepStrictEqual(Object.keys(plain(rules.leagueBanners)), ["bronze", "silver", "gold"]);
});

test("a shop item is bought once with crystals; rewards and clan items are not sold to a player", () => {
  const { nk } = world("ann");
  assert.throws(() => server.cosmeticBuy(nk, "ann", "skin_runedancer", now), /not enough crystals/);
  give(nk, "ann", 1500);
  const mine = plain(server.cosmeticBuy(nk, "ann", "skin_runedancer", now));
  assert.deepStrictEqual(mine.owned, ["skin_runedancer"]);
  assert.strictEqual(mine.crystals, 1500 - item("skin_runedancer").price);
  assert.throws(() => server.cosmeticBuy(nk, "ann", "skin_runedancer", now), /already owned/);
  assert.throws(() => server.cosmeticBuy(nk, "ann", "champion_top10", now), /not for sale/);
  assert.throws(() => server.cosmeticBuy(nk, "ann", "banner_wolf", now), /clan items/);
  assert.strictEqual(server.walletRead(nk, "ann").value.ledger[0].source, "cosmetic");
});

test("last week's weekly trial places give the champion frame and crystals once", () => {
  const { nk } = world("ann", "bob");
  const lastWeek = server.weeklyWeekOf(now) - 1;
  const trial = server.weeklyTrials(lastWeek)[0];
  server.weeklySubmit(nk, "ann", { week: lastWeek, trial: trial, stage: 500, heroClass: 0 }, now - 7 * day);
  for (let i = 0; i < 12; i++) {
    nk.addUser("u" + i, "u" + i, "");
    server.weeklySubmit(nk, "u" + i, { week: lastWeek, trial: trial, stage: 1000 + i, heroClass: 0 }, now - 7 * day);
  }
  const first = plain(server.cosmeticMine(nk, "ann", now));
  assert.deepStrictEqual(first.fresh, [rules.championTop100], "13th place is in the top hundred");
  assert.strictEqual(first.crystals, 20);
  const again = plain(server.cosmeticMine(nk, "ann", now + 1000));
  assert.deepStrictEqual(again.fresh, []);
  assert.strictEqual(again.crystals, 20);
  const top = plain(server.cosmeticMine(nk, "u11", now));
  assert.deepStrictEqual(top.fresh, [rules.championTop10]);
  assert.strictEqual(top.crystals, 100);
  assert.deepStrictEqual(plain(server.cosmeticMine(nk, "bob", now)).fresh, []);
});

test("the clan leader buys and sets the banner; a war win raises the victor banner for a week", () => {
  const w = world("ann", "bob");
  const clan = w.call("ann", "clanCreateRpc", {});
  w.call("bob", "clanJoinRpc", { code: clan.code });
  give(w.nk, "ann", 2000);
  give(w.nk, "bob", 2000);
  assert.throws(() => server.clanCosmeticBuy(w.nk, "bob", "banner_wolf", now), /only the leader/);
  assert.throws(() => server.clanCosmeticSet(w.nk, "ann", { banner: "banner_wolf" }, now), /not owned/);
  server.clanCosmeticBuy(w.nk, "ann", "banner_wolf", now);
  const set = plain(server.clanCosmeticSet(w.nk, "ann", { banner: "banner_wolf" }, now));
  assert.strictEqual(set.banner, "banner_wolf");
  assert.throws(() => server.clanCosmeticSet(w.nk, "ann", { emblem: "banner_wolf" }, now), /not owned/);

  const clanId = server.clanOf(w.nk, "ann", now).id;
  server.clanCosmeticNoteWar(w.nk, clanId, "win", "silver", true, now);
  const view = plain(server.clanCosmeticView(server.clanCosmeticRead(w.nk, clanId).value, now + day));
  assert.strictEqual(view.victor, true);
  assert.ok(view.owned.includes("league_silver"), "the season's league banner stays");
  assert.strictEqual(plain(server.clanCosmeticView(server.clanCosmeticRead(w.nk, clanId).value, now + 8 * day)).victor, false);
});
