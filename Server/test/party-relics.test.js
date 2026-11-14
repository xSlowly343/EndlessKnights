const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.colossus;
const now = Date.UTC(2026, 10, 14, 10);
const minute = 60000;
const plain = (o) => JSON.parse(JSON.stringify(o));
rules.mandatory = [{ upTo: 0, min: 0, max: 0 }];

function party(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const view = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: view.code });
  let total = 0;
  for (let l = 1; l < 30; l++)
    total += server.partyGrowthNeed(l);
  server.partyGrowthAdd(nk, view.id, total);
  return { nk, call, partyId: view.id };
}

function nearlyDead(nk, partyId, tier, mandatory = [], optional = []) {
  server.partyColossusChange(nk, partyId, (value) => {
    value.tier = tier;
    value.drawnTier = tier;
    value.mandatory = mandatory;
    value.optional = optional;
    value.touched = false;
    value.shield = 0;
    value.hp = 10;
  });
}

function kill(nk, user, at) {
  return server.partyColossusReport(nk, user, { direct: [0, 0, 0, 1000] }, at);
}

const treasury = (nk, partyId) => server.partyTreasuryRead(nk, partyId).value.treasury;

test("relic rules come from party.json: 24 relics, a new one every 2 tiers, a level costs its number in trophies", () => {
  assert.strictEqual(rules.relics.length, 24);
  assert.strictEqual(rules.relics.filter((r) => r.stat === "attackPower" || r.stat === "colossusDamage").length >= 6, true);
  assert.strictEqual(rules.relicEvery, 2);
  assert.strictEqual(rules.relicMaxLevel, 30);
  assert.strictEqual(rules.relicCost, 1);
});

test("a kill gives 1 + tier/5 trophies and 10 signs per tier to the treasury", () => {
  const { nk, partyId } = party("ann", "bob");
  nearlyDead(nk, partyId, 1);
  let view = kill(nk, "ann", now);
  assert.strictEqual(view.trophies, 1);
  assert.strictEqual(view.accepted.signs, 10);
  assert.strictEqual(treasury(nk, partyId), 10);
  nearlyDead(nk, partyId, 10);
  view = kill(nk, "ann", now + minute);
  assert.strictEqual(view.trophies, 1 + 3);
  assert.strictEqual(treasury(nk, partyId), 10 + 100);
  assert.strictEqual(view.best, 10);
  const log = server.partyColossusRead(nk, partyId).value.log;
  assert.deepStrictEqual(plain(log.map((k) => [k.tier, k.trophies, k.signs])), [[1, 1, 10], [10, 3, 100]]);
});

test("optional hardships add a quarter each, greedy trades trophies for signs, limit armor doubles the reward", () => {
  const { nk, partyId } = party("ann", "bob");
  nearlyDead(nk, partyId, 10, [], ["night", "shadow"]);
  let view = kill(nk, "ann", now);
  assert.strictEqual(view.trophies, 4, "3 trophies with +50% are 4.5: 4 now and half carried");
  nearlyDead(nk, partyId, 10, ["limitArmor"]);
  view = kill(nk, "ann", now + minute);
  assert.strictEqual(view.trophies, 4 + 6, "doubled 3 is 6, the half stays carried");
  nearlyDead(nk, partyId, 10, ["greedy"]);
  const before = treasury(nk, partyId);
  view = kill(nk, "ann", now + 2 * minute);
  assert.strictEqual(view.accepted.signs, 200, "greedy doubles the signs");
  assert.strictEqual(treasury(nk, partyId) - before, 200);
  assert.strictEqual(view.trophies, 10 + 2, "greedy takes 20%: 2.4 and the carried half make 2.9");
});

test("the party tree and the Trophy Chain relic raise trophies, Treasurer raises signs", () => {
  const { nk, partyId } = party("ann", "bob");
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "6": 3, "26": 3, "34": 3 };
  });
  server.partyColossusChange(nk, partyId, (value) => {
    value.relics = { trophy_chain: 30 };
  });
  nearlyDead(nk, partyId, 20);
  const view = kill(nk, "ann", now);
  assert.strictEqual(view.trophies, Math.floor(5 * (1 + 0.03 + 0.05 + 0.3)));
  assert.strictEqual(view.accepted.signs, 220);
});

test("the leader raises open relics with trophies, one level at a time up to 30", () => {
  const { nk, call, partyId } = party("ann", "bob");
  server.partyColossusChange(nk, partyId, (value) => {
    value.trophies = 1000;
    value.best = 3;
  });
  assert.throws(() => server.partyColossusRelicUp(nk, "bob", "colossus_fang", now), /only the leader/);
  assert.throws(() => server.partyColossusRelicUp(nk, "ann", "nope", now), /bad request/);
  assert.throws(() => server.partyColossusRelicUp(nk, "ann", "hunt_horn", now), /not open yet/, "tier 3 opens only the first relic");
  let view = server.partyColossusRelicUp(nk, "ann", "colossus_fang", now);
  view = server.partyColossusRelicUp(nk, "ann", "colossus_fang", now);
  assert.strictEqual(view.relics.colossus_fang, 2);
  assert.strictEqual(view.trophies, 1000 - 1 - 2);
  assert.strictEqual(view.relicsOpen, 1);
  server.partyColossusChange(nk, partyId, (value) => {
    value.relics = { colossus_fang: 30 };
    value.best = 48;
    value.trophies = 1;
  });
  assert.throws(() => server.partyColossusRelicUp(nk, "ann", "colossus_fang", now), /last level/);
  view = server.partyColossusRelicUp(nk, "ann", "last_sand", now);
  assert.strictEqual(view.relics.last_sand, 1, "tier 48 opens the 24th relic");
  assert.throws(() => server.partyColossusRelicUp(nk, "ann", "last_sand", now), /not enough trophies/);
  server.partyColossusChange(nk, partyId, (value) => {
    value.trophies = 1;
  });
  view = call("ann", "partyRelicUpRpc", { id: "eternal_heart" });
  assert.strictEqual(view.relics.eternal_heart, 1);
  assert.strictEqual(view.relicsOpen, 24);
});

test("every member with more than 10% of last week's colossus damage gets 5 friendship once, the top one is shown", () => {
  const { nk, partyId } = party("ann", "bob", "cid");
  server.partyColossusReport(nk, "ann", { direct: [9000, 0, 0, 0] }, now);
  server.partyColossusReport(nk, "bob", { direct: [2000, 0, 0, 0] }, now);
  server.partyColossusReport(nk, "cid", { direct: [500, 0, 0, 0] }, now);
  const nextWeek = server.partyTrialWeekEndsAt(now) + minute;
  let view = server.partyColossusReport(nk, "ann", { direct: [1, 0, 0, 0] }, nextWeek);
  assert.strictEqual(view.accepted.weekly, true);
  assert.strictEqual(view.topLastWeek, "ann");
  view = server.partyColossusReport(nk, "ann", { direct: [1, 0, 0, 0] }, nextWeek + minute);
  assert.strictEqual(view.accepted.weekly, false, "paid once");
  assert.strictEqual(server.partyColossusReport(nk, "bob", { direct: [1, 0, 0, 0] }, nextWeek).accepted.weekly, true);
  assert.strictEqual(server.partyColossusReport(nk, "cid", { direct: [1, 0, 0, 0] }, nextWeek).accepted.weekly, false, "500 of 11500 is under 10%");
  const wallet = server.walletRead(nk, "ann").value;
  assert.strictEqual(wallet.today.colossus_share, 5);
});

test("Colossus Trap gives one more trophy for every kill at its third level", () => {
  const { nk, partyId } = party("ann", "bob");
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "44": 3 };
  });
  nearlyDead(nk, partyId, 1);
  assert.strictEqual(kill(nk, "ann", now).trophies, 2);
});
