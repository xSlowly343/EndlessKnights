const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.SeasonPassServerRules;
const day = 86400000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const seasonStart = server.seasonPassWeekStart(rules.firstWeek);
const now = seasonStart + 2 * day + 12 * 3600000;
const perLevel = rules.pointsPerLevel;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

const give = (nk, user, crystals) => server.walletGrant(nk, user, "crystal_purchase", crystals, now);

function addDays(nk, user, days, points) {
  let view = null;
  for (let i = 0; i < days; i++)
    view = plain(server.seasonPassAdd(nk, user, points, now + i * day));
  return view;
}

test("seasons start on a monday together with clan war seasons", () => {
  assert.strictEqual(rules.firstWeek % rules.seasonWeeks, 0);
  assert.strictEqual(rules.seasonWeeks % server.ClanWarServerRules.seasonWeeks, 0);
  assert.strictEqual(new Date(seasonStart + server.WalletServerRules.dayUtcOffsetMinutes * 60000).getUTCDay(), 1);
  assert.strictEqual(server.weeklyWeekOf(seasonStart), rules.firstWeek);
  assert.strictEqual(server.weeklyWeekOf(seasonStart - 1), rules.firstWeek - 1);
  assert.strictEqual(server.seasonPassSeasonOf(seasonStart - 1), -1);
  assert.strictEqual(server.seasonPassSeasonOf(now), 0);
  assert.strictEqual(server.seasonPassEndsAt(0), server.seasonPassWeekStart(rules.firstWeek + rules.seasonWeeks));
});

test("before the first season nothing is counted", () => {
  const { nk } = world("ann");
  const view = plain(server.seasonPassAdd(nk, "ann", 100, seasonStart - day));
  assert.strictEqual(view.season, -1);
  assert.strictEqual(view.accepted, 0);
  assert.throws(() => server.seasonPassPremium(nk, "ann", seasonStart - day), /no season/);
});

test("points are capped per day and free rewards arrive with levels", () => {
  const { nk } = world("ann");
  const first = plain(server.seasonPassAdd(nk, "ann", rules.dailyCap + 100, now));
  assert.strictEqual(first.accepted, rules.dailyCap);
  assert.strictEqual(plain(server.seasonPassAdd(nk, "ann", 50, now + 1000)).accepted, 0, "the day is full");
  assert.throws(() => server.seasonPassAdd(nk, "ann", -5, now), /bad request/);
  assert.throws(() => server.seasonPassAdd(nk, "ann", 1.5, now), /bad request/);

  const days = Math.ceil(5 * perLevel / rules.dailyCap);
  const view = addDays(nk, "ann", days, rules.dailyCap);
  assert.ok(view.level >= 5);
  assert.ok(plain(server.cosmeticMine(nk, "ann", now)).owned.includes("pass1_frame"));
  assert.ok(!plain(server.cosmeticMine(nk, "ann", now)).owned.includes("pass1_effect"), "premium items wait for the premium track");

  const mine = plain(server.seasonPassAdd(nk, "ann", 0, now + days * day));
  assert.strictEqual(mine.points, view.points);
  assert.strictEqual(mine.id, "embers");
});

test("the premium track costs crystals once and catches up on reached levels", () => {
  const { nk } = world("ann");
  addDays(nk, "ann", Math.ceil(10 * perLevel / rules.dailyCap), rules.dailyCap);
  assert.throws(() => server.seasonPassPremium(nk, "ann", now), /not enough crystals/);
  give(nk, "ann", rules.premiumPrice + 10);
  const bought = plain(server.seasonPassPremium(nk, "ann", now));
  assert.strictEqual(bought.premium, true);
  assert.deepStrictEqual(bought.fresh, ["pass1_effect", "pass1_avatar_gold"]);
  assert.strictEqual(bought.crystals, 10 + 50, "ten levels also paid the free 50 crystals at level 10");
  assert.throws(() => server.seasonPassPremium(nk, "ann", now), /already owned/);
  assert.strictEqual(plain(server.walletRead(nk, "ann").value.balances).crystals, 60);
});

test("a full season gives every reward exactly once and stops at the last level", () => {
  const { nk } = world("ann");
  give(nk, "ann", rules.premiumPrice);
  server.seasonPassPremium(nk, "ann", now);
  const days = Math.ceil(rules.levels * perLevel / rules.dailyCap) + 2;
  const end = addDays(nk, "ann", days, rules.dailyCap);
  assert.strictEqual(end.level, rules.levels);
  assert.strictEqual(end.points, rules.levels * perLevel);
  const season = rules.seasons[0];
  const owned = plain(server.cosmeticMine(nk, "ann", now)).owned;
  for (const reward of season.free.concat(season.premium).filter((r) => r.cosmetic))
    assert.ok(owned.includes(reward.cosmetic), reward.cosmetic);
  const crystals = season.free.concat(season.premium).reduce((sum, r) => sum + r.crystals, 0);
  assert.strictEqual(plain(server.walletRead(nk, "ann").value.balances).crystals, crystals);
});

test("a new season starts from zero", () => {
  const { nk } = world("ann");
  server.seasonPassAdd(nk, "ann", 100, now);
  const next = seasonStart + rules.seasonWeeks * 7 * day + day;
  const view = plain(server.seasonPassAdd(nk, "ann", 0, next));
  assert.strictEqual(view.season, rules.seasons.length > 1 ? 1 : -1);
  assert.strictEqual(view.points, 0);
});

test("changing the path or the class costs crystals", () => {
  const { nk, call } = world("ann");
  const price = server.CrystalChangeServerRules.price;
  assert.throws(() => call("ann", "crystalChangeRpc", { reason: "subclass_change" }), /not enough crystals/);
  give(nk, "ann", price + 1);
  const spent = call("ann", "crystalChangeRpc", { reason: "class_change" });
  assert.deepStrictEqual(spent, { crystals: 1, spent: price });
  assert.throws(() => call("ann", "crystalChangeRpc", { reason: "skin" }), /bad request/);
  assert.strictEqual(server.walletRead(nk, "ann").value.ledger[0].source, "class_change");
});
