const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.CoopHelpServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const week = 7 * 86400000;
const noon = Date.UTC(2026, 9, 29, 9, 0, 0);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, ""));
  return { nk, call };
}

function killBoss(host, slots) {
  const encounter = host.encounter;
  while (!encounter.fightingBoss) {
    for (const slot of slots)
      encounter.reportInput(slot);
    encounter.applyDamage(slots[0], encounter.enemyId, encounter.barLeft + 1);
    host.tick();
  }
  for (const slot of slots)
    encounter.reportInput(slot);
  const share = encounter.barLeft / slots.length / encounter.damageTakenMultiplier() + 1e-9;
  for (const slot of slots)
    encounter.applyDamage(slot, encounter.enemyId, share);
  host.tick();
}

const source = (view, id) => view.sources.find((s) => s.source === id);

test("rules come from coop_help.json and a single source may fill the whole pool", () => {
  assert.strictEqual(rules.maxPoints, 5);
  for (const max of [rules.mentorMax, rules.bossesMax, rules.wanderersMax])
    assert.strictEqual(max, rules.maxPoints);
  assert.strictEqual(Math.min(rules.riftMax, rules.riftStages.length), rules.riftStages.length, "the rift gives a point for each listed stage");
});

test("bosses alone fill the whole pool", () => {
  const { nk, call } = world("ann");
  server.coopHelpBosses(nk, logger, { ann: rules.bossesPerPoint * rules.maxPoints });
  const view = call("ann", "coopHelpRpc");
  assert.strictEqual(view.points, rules.maxPoints);
  assert.strictEqual(source(view, "bosses").next, 0);
});

test("bosses as a fighter give a point for every few hundred, up to the cap of the source", () => {
  const { nk, call } = world("ann");
  server.coopHelpBosses(nk, logger, { ann: rules.bossesPerPoint - 1 });
  assert.strictEqual(call("ann", "coopHelpRpc").points, 0);
  assert.strictEqual(source(call("ann", "coopHelpRpc"), "bosses").progress, rules.bossesPerPoint - 1);
  server.coopHelpBosses(nk, logger, { ann: 1 });
  assert.strictEqual(call("ann", "coopHelpRpc").points, 1);
  server.coopHelpBosses(nk, logger, { ann: rules.bossesPerPoint * 10 });
  const view = call("ann", "coopHelpRpc");
  assert.strictEqual(source(view, "bosses").points, rules.bossesMax);
  assert.strictEqual(source(view, "bosses").next, 0);
});

test("rift stages count once each for everyone in the party", () => {
  const { nk, call } = world("ann", "bob");
  server.coopHelpRift(nk, logger, ["ann", "bob"], rules.riftStages[0] - 1);
  assert.strictEqual(call("ann", "coopHelpRpc").points, 0);
  server.coopHelpRift(nk, logger, ["ann"], rules.riftStages[0]);
  server.coopHelpRift(nk, logger, ["ann"], rules.riftStages[0] + 5);
  assert.strictEqual(call("ann", "coopHelpRpc").points, 1, "the same stage twice is one point");
  assert.strictEqual(call("bob", "coopHelpRpc").points, 0);
  server.coopHelpRift(nk, logger, ["ann", "bob"], rules.riftStages[rules.riftStages.length - 1]);
  assert.strictEqual(call("ann", "coopHelpRpc").points, rules.riftStages.length);
  assert.strictEqual(call("bob", "coopHelpRpc").points, rules.riftStages.length, "a deep rift reached at once gives every stage");
});

test("wanderers give one point a week up to the cap", () => {
  const { nk, call } = world("ann");
  server.coopHelpWanderers(nk, logger, ["ann"], noon);
  server.coopHelpWanderers(nk, logger, ["ann"], noon + 1000);
  assert.strictEqual(call("ann", "coopHelpRpc").points, 1, "once a week");
  for (let i = 1; i < 6; i++)
    server.coopHelpWanderers(nk, logger, ["ann"], noon + i * week);
  assert.strictEqual(source(call("ann", "coopHelpRpc"), "wanderers").points, rules.wanderersMax);
});

test("all sources together stop at five and the mentor points count too", () => {
  const { nk, call } = world("ann");
  server.mentorChange(nk, "ann", (profile) => { profile.coopPoints = 5; });
  server.coopHelpBosses(nk, logger, { ann: rules.bossesPerPoint * rules.bossesMax });
  server.coopHelpRift(nk, logger, ["ann"], 10000);
  const view = call("ann", "coopHelpRpc");
  assert.strictEqual(source(view, "mentor").points, rules.mentorMax);
  assert.strictEqual(view.points, rules.maxPoints);
  assert.strictEqual(view.max, rules.maxPoints);
});

test("the host notes wanderers only for four different heroes of four different roles", () => {
  const host = new server.PartyHost(server.PartyServerRules, "5");
  ["a", "b", "c", "d"].forEach((name, i) => host.join("user-" + name, String(11 + i)));
  [0, 5, 8, 12].forEach((heroClass, slot) => host.encounter.setHeroClass(slot, heroClass));
  killBoss(host, [0, 1, 2]);
  assert.deepStrictEqual(plain(host.takeWanderers()), [], "three are not enough");
  killBoss(host, [0, 1, 2, 3]);
  assert.deepStrictEqual(plain(host.takeWanderers().sort()), ["user-a", "user-b", "user-c", "user-d"]);
  host.encounter.setHeroClass(3, 1);
  killBoss(host, [0, 1, 2, 3]);
  assert.deepStrictEqual(plain(host.takeWanderers()), [], "two damage dealers repeat a role");
});
