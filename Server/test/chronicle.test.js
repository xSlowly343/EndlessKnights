const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rules = party.ChronicleServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const viking = 0, metalhead = 5, voodoo = 8, plagueDoctor = 10;

function stats(bosses, hours, orchestra) {
  return { bossesTogether: bosses, ticksTogether: Math.round(hours * 36000), fullOrchestra: orchestra };
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

test("ten bosses together give the first banner, a hundred raise it", () => {
  const value = party.chronicleEmpty();
  assert.deepStrictEqual(party.chronicleAdd(value, stats(9, 0, 0), 1000, rules).length, 0);
  const earned = party.chronicleAdd(value, stats(1, 0, 0), 2000, rules);
  assert.deepStrictEqual(plain(earned.map((got) => got.feat)), ["first_steps"]);
  assert.strictEqual(party.chronicleBanner(value, rules), 1);
  party.chronicleAdd(value, stats(990, 0, 0), 3000, rules);
  assert.strictEqual(value.feats.old_guard.level, 2, "a thousand bosses is the second tier");
  assert.strictEqual(party.chronicleBanner(value, rules), 3);
  assert.deepStrictEqual(plain(value.records.map((record) => record.feat + record.level)), ["first_steps1", "old_guard1", "old_guard2"]);
});

test("hours together and the full orchestra give titles, untracked feats wait", () => {
  const value = party.chronicleEmpty();
  party.chronicleAdd(value, stats(0, 99.9, 1), 1000, rules);
  assert.deepStrictEqual(plain(party.chronicleTitles(value, rules)), ["full_orchestra"]);
  party.chronicleAdd(value, stats(0, 0.1, 0), 2000, rules);
  assert.deepStrictEqual(plain(party.chronicleTitles(value, rules)), ["full_orchestra", "inseparable"]);
  party.chronicleAdd(value, stats(100000, 1000, 100), 3000, rules);
  assert.strictEqual(value.feats.night_shift, undefined, "night bosses are not counted yet");
  const view = party.chronicleView("p1", value, rules);
  assert.strictEqual(view.feats.find((feat) => feat.id === "night_shift").tracked, false);
  assert.strictEqual(view.records[0].at, 3000, "the newest record comes first");
});

test("the record list keeps only the newest entries", () => {
  const value = party.chronicleEmpty();
  for (let i = 0; i < rules.maxRecords + 5; i++)
    party.chronicleRecord(value, { at: i, kind: "joined", name: "n" + i, feat: "", level: 0 }, rules);
  assert.strictEqual(value.records.length, rules.maxRecords);
  assert.strictEqual(value.records[0].at, 5);
});

test("the host counts bosses killed together and a full orchestra", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  ["a", "b", "c", "d"].forEach((name, i) => host.join("user-" + name, String(11 + i)));
  host.encounter.setHeroClass(0, viking);
  host.encounter.setHeroClass(1, metalhead);
  host.encounter.setHeroClass(2, voodoo);
  host.encounter.setHeroClass(3, plagueDoctor);
  killBoss(host, [0]);
  assert.strictEqual(host.chronicle.bossesTogether, 0, "a boss one player killed alone is not together");
  killBoss(host, [0, 1]);
  assert.strictEqual(host.chronicle.bossesTogether, 1);
  assert.strictEqual(host.chronicle.fullOrchestra, 0);
  killBoss(host, [0, 1, 2, 3]);
  assert.strictEqual(host.chronicle.bossesTogether, 2);
  assert.strictEqual(host.chronicle.fullOrchestra, 1);
  assert.ok(host.chronicle.ticksTogether > 0, "four typing members fight together");
  const taken = host.takeChronicle();
  assert.strictEqual(taken.bossesTogether, 2);
  assert.strictEqual(host.chronicle.bossesTogether, 0);
});

test("the party chronicle keeps feats, records and copies for every member", () => {
  const nk = new FakeNakama();
  ["ann", "bob", "cid"].forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(party[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const view = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: view.code });
  call("cid", "partyJoinRpc", { code: view.code });
  call("cid", "partyLeaveRpc");

  const earned = party.chronicleFlush(nk, logger, view.id, stats(10, 1, 0), ["ann", "bob"]);

  assert.deepStrictEqual(plain(earned.map((got) => got.feat)), ["first_steps"]);
  const chronicle = call("bob", "partyChronicleRpc");
  assert.strictEqual(chronicle.banner, 1);
  assert.strictEqual(chronicle.stats.bossesTogether, 10);
  assert.strictEqual(chronicle.stats.hoursTogether, 1);
  assert.deepStrictEqual(plain(chronicle.records.map((record) => record.kind + ":" + (record.name || record.feat))),
    ["feat:first_steps", "left:cid", "joined:cid", "joined:bob", "created:"]);
  assert.strictEqual(chronicle.feats.find((feat) => feat.id === "first_steps").level, 1);
  assert.strictEqual(chronicle.feats.find((feat) => feat.id === "old_guard").progress, 10);
  for (const member of ["ann", "bob"])
    assert.strictEqual(nk.storageRead([{ collection: "party_feats", key: view.id, userId: member }])[0].value.feats.first_steps.level, 1);
  assert.throws(() => call("cid", "partyChronicleRpc"), /not in a party/);
  assert.deepStrictEqual(plain(party.chronicleFlush(nk, logger, view.id, stats(0, 0, 0), ["ann"])), [], "an empty flush writes nothing");

  call("bob", "partyLeaveRpc");
  call("ann", "partyLeaveRpc");
  assert.strictEqual(nk.storageRead([{ collection: "party_chronicle", key: view.id, userId: party.PartySystemUser }]).length, 0, "a dissolved party forgets its chronicle");
  assert.strictEqual(nk.storageRead([{ collection: "party_feats", key: view.id, userId: "bob" }]).length, 1, "a former member keeps the copy");
});
