const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.colossus;
const now = Date.UTC(2026, 10, 14, 10);
const minute = 60000;
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

function played(nk, user, seconds, at) {
  server.partyGrowthReport(nk, user, seconds, at);
}

test("colossus rules come from party.json", () => {
  assert.strictEqual(rules.hpBase, 15000);
  assert.strictEqual(rules.hpGrowth, 1.45);
  assert.strictEqual(server.partyColossusHp(3), 15000 * 1.45 * 1.45);
  assert.strictEqual(server.partyColossusCeiling(9), 0);
  assert.strictEqual(server.partyColossusCeiling(10), 10);
  assert.strictEqual(server.partyColossusCeiling(15), 20);
  assert.strictEqual(server.partyColossusCeiling(95), 70);
});

test("the colossus opens on party level 10 and starts on tier 1 with full hp", () => {
  const { nk, call, partyId } = party("ann", "bob");
  assert.throws(() => server.partyColossusReport(nk, "ann", { points: 10 }, now), /not open yet/);
  reachLevel(nk, partyId, 10);
  const view = call("bob", "partyColossusRpc");
  assert.strictEqual(view.opened, true);
  assert.strictEqual(view.tier, 1);
  assert.strictEqual(view.hp, 15000);
  assert.strictEqual(view.ceiling, 10);
});

test("hits take hp, are capped by the time since the last report and fill the weekly table", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  let view = server.partyColossusReport(nk, "ann", { points: 5000 }, now);
  assert.strictEqual(view.accepted.live, 5000, "the first report may cover one report period");
  view = server.partyColossusReport(nk, "ann", { points: 5000 }, now + 10000);
  assert.strictEqual(view.accepted.live, 10 * rules.maxPointsPerSecond, "ten seconds can hold only so much");
  view = server.partyColossusReport(nk, "bob", { points: 1000 }, now + 10000);
  assert.strictEqual(view.hp, 15000 - 5000 - 400 - 1000);
  assert.deepStrictEqual(plain(view.shares.map((s) => [s.name, s.points])), [["ann", 5400], ["bob", 1000]]);
  assert.strictEqual(view.yours, 1000);
  assert.throws(() => server.partyColossusReport(nk, "ann", { points: -1 }, now), /bad request/);
  const nextWeek = server.partyTrialWeekEndsAt(now) + minute;
  assert.deepStrictEqual(plain(server.partyColossusMine(nk, "ann", nextWeek).shares), [], "the table starts again each week");
});

test("a kill raises the tier, carries the overflow, writes the chronicle and gives party experience", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  const experience = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyColossusReport(nk, "ann", { points: 12000 }, now);
  const view = server.partyColossusReport(nk, "bob", { points: 12000 }, now);
  assert.strictEqual(view.tier, 2);
  assert.strictEqual(view.kills, 1);
  assert.deepStrictEqual(plain(view.accepted.killed), [1]);
  assert.strictEqual(view.hp, server.partyColossusHp(2) - 9000);
  const record = server.chronicleRead(nk, partyId).value.records.filter((r) => r.kind === "colossus")[0];
  assert.strictEqual(record.level, 1);
  const expected = (1 + 9000 / server.partyColossusHp(2)) * 100 * rules.experiencePerPercent;
  assert.ok(Math.abs(server.partyGrowthRead(nk, partyId).value.experience - experience - expected) < 1e-6);
});

test("at the tier ceiling the colossus waits until the party grows", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  server.partyColossusChange(nk, partyId, (value) => {
    value.tier = 10;
    value.hp = 100;
  });
  let view = server.partyColossusReport(nk, "ann", { points: 500 }, now);
  assert.strictEqual(view.waiting, true);
  assert.strictEqual(view.tier, 10);
  assert.strictEqual(view.hp, 0);
  view = server.partyColossusReport(nk, "bob", { points: 500 }, now);
  assert.strictEqual(view.kills, 1, "a sleeping colossus takes no hits");
  reachLevel(nk, partyId, 15);
  view = server.partyColossusMine(nk, "ann", now);
  assert.strictEqual(view.waiting, false);
  assert.strictEqual(view.tier, 11);
  assert.strictEqual(view.hp, server.partyColossusHp(11));
});

test("offline hits count only up to the hours played today: a twink without input gives nothing", () => {
  const { nk, partyId } = party("ann", "twink");
  reachLevel(nk, partyId, 10);
  let view = server.partyColossusReport(nk, "twink", { offlinePoints: 5000, offlineSeconds: 8 * 3600 }, now);
  assert.strictEqual(view.accepted.offline, 0);
  played(nk, "ann", 900, now);
  view = server.partyColossusReport(nk, "ann", { offlinePoints: 4000, offlineSeconds: 3600 }, now + 1000);
  assert.strictEqual(view.accepted.offline, 1000, "15 played minutes let a quarter of the hour count");
  view = server.partyColossusReport(nk, "ann", { offlinePoints: 4000, offlineSeconds: 3600 }, now + 2000);
  assert.strictEqual(view.accepted.offline, 0, "the played time is used up for today");
});

test("offline points are also capped by a rate per second", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  played(nk, "ann", 900, now);
  const view = server.partyColossusReport(nk, "ann", { offlinePoints: 1e9, offlineSeconds: 600 }, now + 1000);
  assert.strictEqual(view.accepted.offline, 600 * rules.maxOfflinePointsPerSecond);
});
