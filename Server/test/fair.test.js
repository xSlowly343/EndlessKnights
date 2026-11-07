const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.FairServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const day = 86400000;
const week = 3000;
const now = server.seasonPassWeekStart(week) + day;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const clan = call(names[0], "clanCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "clanJoinRpc", { code: clan.code });
  return { nk, call };
}

const fish = (nk, user, id, cm, at) => plain(server.fairEnterFish(nk, logger, user, id, cm, at === undefined ? now : at));
const garden = (nk, user, harvests, mutations, at) => plain(server.fairEnterGarden(nk, logger, user, harvests, mutations, at === undefined ? now : at));
const contest = (view, id) => view.contests.find((entry) => entry.id === id);
const friendship = (nk, user) => plain(server.walletRead(nk, user)).value.balances.friendship;

test("rules come from fair.json and fishing.json", () => {
  assert.deepStrictEqual(plain(rules.contests), ["big_fish", "best_garden"]);
  assert.strictEqual(rules.fish["fish_hills_pike"].maxCm, 90);
  assert.strictEqual(rules.fish["legend_hills_boar_catfish"].minCm, 210);
});

test("the biggest fish is scored against its kind and only a better one replaces it", () => {
  const { nk } = world("ann", "bob");
  const first = fish(nk, "ann", "fish_hills_pike", 45);
  assert.strictEqual(contest(first, "big_fish").mine.score, 50);
  fish(nk, "ann", "fish_hills_roach", 12.5);
  assert.strictEqual(fish(nk, "bob", "fish_hills_roach", 25).fish.score, 100);
  const view = fish(nk, "ann", "fish_hills_pike", 81);
  assert.deepStrictEqual(contest(view, "big_fish").top.map((row) => [row.name, row.score]), [["bob", 100], ["ann", 90]]);
  assert.throws(() => fish(nk, "ann", "fish_hills_pike", 91), /bad request/);
  assert.throws(() => fish(nk, "ann", "fish_moon", 10), /bad request/);
});

test("the garden scores harvests and mutations with a daily cap", () => {
  const { nk } = world("ann", "bob");
  const view = garden(nk, "ann", 10, 2);
  assert.strictEqual(contest(view, "best_garden").mine.score, 10 * rules.harvestPoints + 2 * rules.mutationPoints);
  const capped = garden(nk, "ann", rules.maxHarvestsPerDay * 3, 0);
  assert.strictEqual(contest(capped, "best_garden").mine.score, rules.maxHarvestsPerDay * rules.harvestPoints + 2 * rules.mutationPoints);
  const nextDay = garden(nk, "ann", 5, 0, now + day);
  assert.strictEqual(contest(nextDay, "best_garden").mine.score, (rules.maxHarvestsPerDay + 5) * rules.harvestPoints + 2 * rules.mutationPoints);
  assert.throws(() => garden(nk, "ann", -1, 0), /bad request/);
});

test("after the week the fair pays places in friendship by letter once", () => {
  const { nk, call } = world("ann", "bob", "cat");
  fish(nk, "ann", "fish_hills_pike", 90);
  fish(nk, "bob", "fish_hills_pike", 60);
  fish(nk, "cat", "fish_hills_pike", 40);
  garden(nk, "bob", 3, 0);
  const next = server.seasonPassWeekStart(week + 1) + 1000;
  assert.strictEqual(server.fairSettle(nk, logger, server.clanGroupOfUser(nk, "ann", next).id, week, next), 4);
  assert.strictEqual(server.fairSettle(nk, logger, server.clanGroupOfUser(nk, "ann", next).id, week, next), 0, "paid once");
  assert.strictEqual(friendship(nk, "ann"), rules.rewards[0].friendship);
  assert.strictEqual(friendship(nk, "bob"), rules.rewards[1].friendship + rules.takingPart, "a garden of one is a taking part only");
  assert.strictEqual(friendship(nk, "cat"), rules.rewards[2].friendship);
  const letters = plain(server.mailLetters(nk, "ann"));
  assert.strictEqual(letters[0].kind, "fair");
  assert.strictEqual(letters[0].place, 1);
  const fresh = call("ann", "fairViewRpc");
  assert.ok(Array.isArray(fresh.contests));
});

test("a lone entrant only gets the taking-part prize and the current week is not settled", () => {
  const { nk } = world("ann", "bob");
  fish(nk, "ann", "fish_hills_pike", 90);
  const clanId = server.clanGroupOfUser(nk, "ann", now).id;
  assert.strictEqual(server.fairSettle(nk, logger, clanId, week, now), 0, "the week is still on");
  const next = server.seasonPassWeekStart(week + 1) + 1000;
  server.fairSettle(nk, logger, clanId, week, next);
  assert.strictEqual(friendship(nk, "ann"), rules.takingPart);
});

test("the fair rpcs need a clan", () => {
  const nk = new FakeNakama();
  nk.addUser("solo", "solo", "76561198000000009");
  assert.throws(() => server.fairViewRpc({ userId: "solo", env: {} }, logger, nk, ""), /not in a clan/);
});
