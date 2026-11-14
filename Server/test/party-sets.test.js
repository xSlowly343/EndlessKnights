const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.partySets;
const now = Date.UTC(2026, 10, 14, 10);
const plain = (o) => JSON.parse(JSON.stringify(o));

function party(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const view = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: view.code });
  return { nk, call, partyId: view.id };
}

function reachLevel(nk, partyId, level) {
  let total = 0;
  for (let l = 1; l < level; l++)
    total += server.partyGrowthNeed(l);
  const have = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyGrowthAdd(nk, partyId, total - have);
}

test("182 party sets: 12 lines of armour and jewelry from rare to primordial, the primal flame from rift", () => {
  assert.strictEqual(rules.sets.length, 11 * 8 * 2 + 3 * 2);
  assert.ok(rules.sets.some((set) => set.id === "party_eternal_hunters_jewelry_mythic"));
  assert.ok(!rules.sets.some((set) => set.id === "party_primal_flame_armor_rare"));
});

test("every member reports collected party sets, the party counts them from level 50, stacking copies", () => {
  const { nk, call, partyId } = party("ann", "bob", "cid");
  assert.throws(() => server.partySetsReport(nk, "ann", ["dragon"]), /bad request/);
  server.partySetsReport(nk, "ann", ["party_common_pot_armor_rare", "party_rank_oath_jewelry_epic"]);
  server.partySetsReport(nk, "bob", ["party_common_pot_armor_rare", "party_common_pot_armor_rare"]);
  let view = call("cid", "partySetsRpc");
  assert.strictEqual(view.opened, false);
  assert.deepStrictEqual(plain(view.counts), {});
  reachLevel(nk, partyId, 50);
  view = server.partySetsMine(nk, "cid");
  assert.strictEqual(view.opened, true);
  assert.deepStrictEqual(plain(view.counts), { party_common_pot_armor_rare: 2, party_rank_oath_jewelry_epic: 1 });
  assert.deepStrictEqual(plain(view.members.map((m) => [m.name, m.sets.length])), [["ann", 2], ["bob", 1], ["cid", 0]]);
  call("bob", "partyLeaveRpc");
  assert.deepStrictEqual(plain(server.partySetsMine(nk, "ann").counts), { party_common_pot_armor_rare: 1, party_rank_oath_jewelry_epic: 1 }, "a leaver takes the sets along");
});

test("eternal hunter jewelry raises trophies, the armour softens hardships, trial seal armour adds tries", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 50);
  server.partySetsReport(nk, "ann", ["party_eternal_hunters_jewelry_rare", "party_trial_seal_armor_rift"]);
  server.partySetsReport(nk, "bob", ["party_eternal_hunters_jewelry_rare", "party_eternal_hunters_armor_rift"]);
  assert.ok(Math.abs(server.partySetsSum(nk, partyId, "colossusTrophies") - 0.1) < 1e-9, "two copies of +5%");
  assert.ok(Math.abs(server.partySetsSum(nk, partyId, "colossusHardship") + 0.15) < 1e-9);
  assert.strictEqual(server.partySetsSum(nk, partyId, "trialTries"), 1);
  const situation = server.partyColossusSituation(nk, partyId, "ann", now);
  assert.ok(Math.abs(situation.soften - 0.15) < 1e-9);
  const trials = server.partyTrialMine(nk, "ann", now);
  assert.ok(trials.trials.every((t) => t.triesLeft === server.PartyGrowthServerRules.trials.tries + 1));
});
