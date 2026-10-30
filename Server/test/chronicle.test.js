const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rules = party.ChronicleServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const viking = 0, metalhead = 5, voodoo = 8, plagueDoctor = 10;

function stats(bosses, hours, orchestra, night) {
  return { bossesTogether: bosses, ticksTogether: Math.round(hours * 36000), fullOrchestra: orchestra, nightBosses: night || 0 };
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

test("hours together and the full orchestra give titles, a feat waits for its own counter", () => {
  const value = party.chronicleEmpty();
  party.chronicleAdd(value, stats(0, 99.9, 1), 1000, rules);
  assert.deepStrictEqual(plain(party.chronicleTitles(value, rules)), ["full_orchestra"]);
  party.chronicleAdd(value, stats(0, 0.1, 0), 2000, rules);
  assert.deepStrictEqual(plain(party.chronicleTitles(value, rules)), ["full_orchestra", "inseparable"]);
  party.chronicleAdd(value, stats(100000, 1000, 100), 3000, rules);
  assert.strictEqual(value.feats.chain_reaction, undefined, "bosses and hours do not make a chain reaction");
  const view = party.chronicleView("p1", value, rules);
  assert.strictEqual(view.feats.find((feat) => feat.id === "chain_reaction").tracked, true);
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

test("night bosses are counted by Moscow time and give the owls", () => {
  const moscow = (day, hour) => Date.UTC(2026, 9, day, hour - 3, 30);
  assert.strictEqual(party.chronicleAtNight(moscow(28, 0), rules), true, "00:30 in Moscow is night");
  assert.strictEqual(party.chronicleAtNight(moscow(28, 5), rules), true);
  assert.strictEqual(party.chronicleAtNight(moscow(28, 6), rules), false, "06:30 in Moscow is morning");
  assert.strictEqual(party.chronicleAtNight(moscow(28, 23), rules), false);
  assert.strictEqual(party.chronicleAtNight(Date.UTC(2026, 9, 28, 22, 30), rules), true, "22:30 UTC is already 01:30 in Moscow");
  const value = party.chronicleEmpty();
  party.chronicleAdd(value, stats(20, 0, 0, 20), 1000, rules);
  assert.deepStrictEqual(plain(party.chronicleTitles(value, rules)), ["owls"]);

  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  host.tick(moscow(28, 12));
  killBoss(host, [0, 1]);
  assert.strictEqual(host.chronicle.nightBosses, 0, "a boss at noon is not the night shift");
  host.tick(moscow(29, 2));
  killBoss(host, [0, 1]);
  assert.strictEqual(host.chronicle.nightBosses, 1);
});

test("Rift depth counts the best Rift of the party, mentor hours add up", () => {
  const value = party.chronicleEmpty();
  const delta = (riftDepth, mentorSeconds) => Object.assign(stats(0, 0, 0), { riftDepth: riftDepth, mentorSeconds: mentorSeconds });
  party.chronicleAdd(value, delta(60, 0), 1000, rules);
  party.chronicleAdd(value, delta(40, 0), 2000, rules);
  assert.strictEqual(value.stats.riftDepth, 60, "a shallower Rift later does not lower the record");
  const earned = party.chronicleAdd(value, delta(101, 9 * 3600), 3000, rules);
  assert.deepStrictEqual(plain(earned.map((got) => got.feat)), ["rift_conquerors"]);
  assert.deepStrictEqual(plain(party.chronicleAdd(value, delta(0, 3600), 4000, rules).map((got) => got.feat)), ["teacher_and_student"]);
  const view = party.chronicleView("p1", value, rules);
  assert.strictEqual(view.stats.riftDepth, 101);
  assert.strictEqual(view.stats.mentorHours, 10);
  assert.strictEqual(view.feats.find((feat) => feat.id === "rift_conquerors").tracked, true);
});

test("the Rift board tells the match how deep the party is", () => {
  const nk = new FakeNakama();
  const riftId = 1793102400;
  assert.strictEqual(party.riftBoardAdd(nk, riftId, "p1", "night_shift", 0, ["ann"], riftId * 1000), 1, "entering the Rift is depth 1");
  assert.strictEqual(party.riftBoardAdd(nk, riftId, "p1", "night_shift", 4, ["ann"], riftId * 1000 + 1000), 5);
});

test("a boss together counts as a guardian when a member who earned it stood on a biome guardian stage", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  const biome = party.PartyServerRules.biomeLength;
  host.stages[0] = biome - 1;
  host.stages[1] = biome + 3;
  killBoss(host, [0, 1]);
  assert.strictEqual(host.chronicle.guardiansTogether, 0);
  host.stages[1] = 2 * biome;
  killBoss(host, [0, 1]);
  assert.strictEqual(host.chronicle.guardiansTogether, 1);
  host.stages[0] = 3 * biome;
  host.stages[1] = 1;
  killBoss(host, [1]);
  assert.strictEqual(host.chronicle.guardiansTogether, 1, "a guardian killed alone is not together");
  const value = party.chronicleEmpty();
  const earned = party.chronicleAdd(value, Object.assign(stats(0, 0, 0), { guardiansTogether: 5 }), 1000, rules);
  assert.deepStrictEqual(plain(earned.map((got) => got.feat)), ["guardian_hunters"]);
});

test("three different synergies on one enemy make a chain reaction", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  host.tick();
  const synergy = (mask) => {
    const view = new DataView(new ArrayBuffer(5));
    view.setUint8(0, 15);
    view.setUint32(1, mask, true);
    return view.buffer;
  };
  const kill = () => {
    host.encounter.reportInput(0);
    host.encounter.applyDamage(0, host.encounter.enemyId, host.encounter.barLeft + 1);
    host.tick();
  };
  host.receive("user-a", synergy(0b0001));
  host.receive("user-b", synergy(0b0010));
  kill();
  assert.strictEqual(host.chronicle.synergiesOnOneEnemy, 2);
  host.receive("user-a", synergy(0));
  host.receive("user-b", synergy(0));
  kill();
  kill();
  assert.strictEqual(host.chronicle.synergiesOnOneEnemy, 2, "synergies still running when an enemy comes count for it, faded ones do not");
  host.receive("user-b", synergy(0b1000));
  host.receive("user-a", synergy(0b0101));
  kill();
  assert.strictEqual(host.chronicle.synergiesOnOneEnemy, 3, "three different synergies while one enemy lived");
  host.receive("user-a", synergy(0xffffffff));
  assert.strictEqual(party.partyBitCount(party.partyDecodeSynergy(synergy(0xffffffff), party.PartyServerRules.synergyReactions)), party.PartyServerRules.synergyReactions, "unknown reactions are dropped");

  const delta = host.takeChronicle();
  assert.strictEqual(delta.synergiesOnOneEnemy, 3);
  assert.strictEqual(host.chronicle.synergiesOnOneEnemy, 0);
  const value = party.chronicleEmpty();
  const earned = party.chronicleAdd(value, delta, 1000, rules);
  assert.deepStrictEqual(plain(earned.map((e) => e.feat)), ["chain_reaction"]);
  party.chronicleAdd(value, Object.assign(party.chronicleNoStats(), { synergiesOnOneEnemy: 1 }), 2000, rules);
  assert.strictEqual(value.stats.synergiesOnOneEnemy, 3, "the best enemy stays the record");
});

test("four different heroes of four roles count wanderer bosses and earn the wanderers", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  ["a", "b", "c", "d"].forEach((name, i) => host.join("user-" + name, String(11 + i)));
  [viking, metalhead, voodoo, plagueDoctor].forEach((heroClass, slot) => host.encounter.setHeroClass(slot, heroClass));
  killBoss(host, [0, 1, 2]);
  assert.strictEqual(host.chronicle.wanderersBosses, 0, "three are not wanderers");
  killBoss(host, [0, 1, 2, 3]);
  assert.strictEqual(host.chronicle.wanderersBosses, 1);
  assert.strictEqual(host.takeChronicle().wanderersBosses, 1);
  assert.strictEqual(host.chronicle.wanderersBosses, 0);

  const feat = rules.feats.find((f) => f.id === "wanderers");
  const value = party.chronicleEmpty();
  const delta = (bosses) => Object.assign(stats(0, 0, 0), { wanderersBosses: bosses });
  assert.deepStrictEqual(plain(party.chronicleAdd(value, delta(feat.levels[0] - 1), 1000, rules)), []);
  assert.deepStrictEqual(plain(party.chronicleAdd(value, delta(1), 2000, rules)), [{ feat: "wanderers", level: 1 }]);
  const view = party.chronicleView("p1", value, rules);
  assert.ok(view.titles.indexOf("wanderers") >= 0);
  assert.strictEqual(view.stats.wanderersBosses, feat.levels[0]);
});

test("each Rift window keeps one record with the deepest milestone of the party", () => {
  const nk = new FakeNakama();
  nk.addUser("ann", "ann", "76561198000000000");
  const view = JSON.parse(party.partyCreateRpc({ userId: "ann", env: {} }, logger, nk, "{}"));
  const every = party.RiftServerRules.milestones.every;
  const rifts = () => party.chronicleRead(nk, view.id).value.records.filter((record) => record.kind === "rift");

  party.chronicleRiftDepth(nk, logger, view.id, 7, 0, every - 1, every, 1000);
  assert.strictEqual(rifts().length, 0, "no milestone yet");
  party.chronicleRiftDepth(nk, logger, view.id, 7, every - 1, every + 3, every, 2000);
  party.chronicleRiftDepth(nk, logger, view.id, 7, every + 3, 2 * every + 1, every, 3000);
  party.chronicleRiftDepth(nk, logger, view.id, 7, 2 * every + 1, 2 * every + 5, every, 4000);
  assert.deepStrictEqual(plain(rifts().map((record) => [record.rift, record.level, record.at])), [[7, 2 * every, 3000]]);
  party.chronicleRiftDepth(nk, logger, view.id, 8, 0, every, every, 5000);
  assert.deepStrictEqual(plain(rifts().map((record) => [record.rift, record.level])), [[7, 2 * every], [8, every]], "the next window starts its own record");
});
