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

test("each source drops lines of its own kind and rarities by depth", () => {
  const lines = (source) => plain(server.partySetLinesOf(source)).sort();
  assert.deepStrictEqual(lines("colossus"), ["eternal_hunters"]);
  assert.deepStrictEqual(lines("orders"), ["party_forge", "peace_guild", "rank_oath"]);
  assert.deepStrictEqual(lines("battle"), ["common_pot", "night_watch"]);
  assert.deepStrictEqual(lines("extreme"), ["primal_flame"]);
  const counts = {};
  const random = server.partyColossusSeed("x");
  for (let i = 0; i < 2000; i++) {
    const rarity = server.partySetRarity("colossus", 50, random);
    counts[rarity] = (counts[rarity] || 0) + 1;
  }
  assert.ok(counts.rift > counts.astral && counts.astral > (counts.primordial || 0), "harder drops are rarer");
  assert.ok(!counts.rare && !counts.epic, "a colossus beyond tier 45 drops only the high rarities");
  assert.ok(Object.keys(counts).every((r) => ["rift", "astral", "primordial"].includes(r)));
});

test("drops wait on the server for each member and are claimed up to the free bag room", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 50);
  const found = server.partySetDropRoll(nk, partyId, ["ann", "bob"], "trials", 20, 40, "seed");
  assert.ok(found > 0);
  const pending = server.partySetDropsRead(nk, partyId).value.pending;
  const ann = pending.ann ? pending.ann.length : 0;
  assert.ok(ann + (pending.bob ? pending.bob.length : 0) === found);
  const first = server.partySetDropsClaim(nk, "ann", 1);
  assert.strictEqual(first.drops.length, Math.min(1, ann));
  assert.strictEqual(first.left, Math.max(0, ann - 1));
  if (ann > 0) {
    const drop = first.drops[0];
    assert.ok(drop.set.startsWith("party_trial_seal_"));
    assert.ok(["helmet", "chest", "gloves", "boots", "amulet", "ring", "ring_right", "weapon"].includes(drop.piece));
  }
  const rest = call("ann", "partySetDropsClaimRpc", { room: 100 });
  assert.strictEqual(rest.left, 0);
  assert.throws(() => server.partySetDropsClaim(nk, "ann", -1), /bad request/);
});

test("nothing drops before party level 50", () => {
  const { nk, partyId } = party("ann", "bob");
  assert.strictEqual(server.partySetDropRoll(nk, partyId, ["ann"], "colossus", 10, 100, "seed"), 0);
});

test("crafting a piece costs oath shards by rarity, astral and primordial only drop", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 50);
  assert.throws(() => server.partySetCraft(nk, "ann", { set: "party_common_pot_armor_rare", piece: "helmet" }, now), /not enough oath/);
  server.walletGrant(nk, "ann", "oath_order", 1, now);
  server.walletGrant(nk, "ann", "oath_trial", 10, now);
  const view = server.partySetCraft(nk, "ann", { set: "party_common_pot_armor_rare", piece: "helmet" }, now);
  assert.strictEqual(view.cost, 20);
  assert.strictEqual(view.oath, 10 + 50 - 20);
  assert.throws(() => server.partySetCraft(nk, "ann", { set: "party_common_pot_armor_astral", piece: "helmet" }, now), /only drops/);
  assert.throws(() => server.partySetCraft(nk, "ann", { set: "party_common_pot_armor_rare", piece: "amulet" }, now), /bad request/);
});

test("boss kills together, colossus kills, first trial clears and mascot calls give oath shards", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 50);
  server.partyGrowthBattle(nk, partyId, 3, 0);
  assert.strictEqual(server.walletRead(nk, "bob").value.today.oath_battle, 3);
  server.partySetColossusKill(nk, partyId, 4, now);
  assert.strictEqual(server.walletRead(nk, "ann").value.today.oath_colossus, 20);
});
