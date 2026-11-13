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
  assert.throws(() => server.partyColossusReport(nk, "ann", { direct: [10, 0, 0, 0] }, now), /not open yet/);
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
  let view = server.partyColossusReport(nk, "ann", { direct: [5000, 0, 0, 0] }, now);
  assert.strictEqual(view.accepted.live, 5000, "the first report may cover one report period");
  view = server.partyColossusReport(nk, "ann", { direct: [5000, 0, 0, 0] }, now + 10000);
  assert.strictEqual(view.accepted.live, 10 * rules.maxPointsPerSecond, "ten seconds can hold only so much");
  view = server.partyColossusReport(nk, "bob", { direct: [1000, 0, 0, 0] }, now + 10000);
  assert.strictEqual(view.hp, 15000 - 5000 - 400 - 1000);
  assert.deepStrictEqual(plain(view.shares.map((s) => [s.name, s.points])), [["ann", 5400], ["bob", 1000]]);
  assert.strictEqual(view.yours, 1000);
  assert.throws(() => server.partyColossusReport(nk, "ann", { direct: [-1, 0, 0, 0] }, now), /bad request/);
  assert.throws(() => server.partyColossusReport(nk, "ann", { direct: [1, 0] }, now), /bad request/);
  const nextWeek = server.partyTrialWeekEndsAt(now) + minute;
  assert.deepStrictEqual(plain(server.partyColossusMine(nk, "ann", nextWeek).shares), [], "the table starts again each week");
});

test("a kill raises the tier, carries the overflow, writes the chronicle and gives party experience", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  const experience = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyColossusReport(nk, "ann", { direct: [12000, 0, 0, 0] }, now);
  const view = server.partyColossusReport(nk, "bob", { direct: [12000, 0, 0, 0] }, now);
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
  let view = server.partyColossusReport(nk, "ann", { direct: [500, 0, 0, 0] }, now);
  assert.strictEqual(view.waiting, true);
  assert.strictEqual(view.tier, 10);
  assert.strictEqual(view.hp, 0);
  view = server.partyColossusReport(nk, "bob", { direct: [500, 0, 0, 0] }, now);
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

function seed(nk, partyId, change) {
  server.partyColossusChange(nk, partyId, change);
}

function atTier(nk, partyId, tier) {
  seed(nk, partyId, (value) => {
    value.tier = tier;
    value.hp = server.partyColossusBaseHp(tier);
  });
}

test("every 5th tier has one mandatory hardship from the list of twelve, the rest take optional ones", () => {
  assert.strictEqual(rules.hardships.length, 12);
  assert.strictEqual(server.partyColossusMandatory(4), "");
  assert.strictEqual(server.partyColossusMandatory(5), "carapace");
  assert.strictEqual(server.partyColossusMandatory(10), "shadow");
  assert.strictEqual(server.partyColossusMandatory(60), "greedy");
  assert.strictEqual(server.partyColossusMandatory(65), "carapace");
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  atTier(nk, partyId, 5);
  const view = server.partyColossusMine(nk, "ann", now);
  assert.deepStrictEqual(plain(view.active), ["carapace"]);
  assert.strictEqual(view.optionalAllowed, false);
});

test("carapace and shadow cut keys and mouse, rift stops the background hit", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 15);
  atTier(nk, partyId, 5);
  let view = server.partyColossusReport(nk, "ann", { direct: [1000, 1000, 0, 0] }, now);
  assert.strictEqual(view.accepted.counted, 600 + 1000);
  atTier(nk, partyId, 10);
  view = server.partyColossusReport(nk, "bob", { direct: [1000, 1000, 0, 0] }, now);
  assert.strictEqual(view.accepted.counted, 1000 + 600);
  seed(nk, partyId, (value) => {
    value.tier = 4;
    value.hp = server.partyColossusBaseHp(4);
    value.optional = ["rift"];
    value.touched = false;
  });
  view = server.partyColossusReport(nk, "ann", { direct: [100, 0, 0, 0], background: [500, 0, 0, 0] }, now + 300000);
  assert.strictEqual(view.accepted.counted, 100, "only direct hits pass the rift");
});

test("the skill shield takes only skills and blocks everything else while it stands", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 15);
  seed(nk, partyId, (value) => {
    value.tier = 4;
    value.optional = ["skillShield"];
  });
  seed(nk, partyId, (value) => server.partyColossusNewTier(value, server.partyColossusSituation(nk, partyId, "ann", now), now));
  let view = server.partyColossusMine(nk, "ann", now);
  const shield = view.shieldFull;
  assert.strictEqual(shield, server.partyColossusBaseHp(4) * 0.05);
  view = server.partyColossusReport(nk, "ann", { direct: [5000, 0, 200, 0] }, now);
  assert.strictEqual(view.shield, shield - 200);
  assert.strictEqual(view.hp, view.hpFull, "keys do not pass the shield");
  view = server.partyColossusReport(nk, "bob", { direct: [0, 0, shield, 0] }, now);
  assert.strictEqual(view.shield, 0);
  assert.strictEqual(view.hp, view.hpFull - 200, "skills past the shield hit the colossus");
  view = server.partyColossusMine(nk, "ann", now + 6 * 3600000);
  assert.strictEqual(view.shield, shield, "the shield comes back every six hours");
});

test("fury heals after six quiet hours, regeneration every quiet hour, the last keystone stops both", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 30);
  const full = server.partyColossusBaseHp(20);
  seed(nk, partyId, (value) => {
    value.tier = 20;
    value.hp = full / 2;
  });
  server.partyColossusReport(nk, "ann", { direct: [0, 0, 0, 5000] }, now);
  let view = server.partyColossusMine(nk, "ann", now + 5 * 3600000);
  assert.strictEqual(view.hp, full / 2 - 5000);
  view = server.partyColossusMine(nk, "ann", now + 13 * 3600000);
  assert.ok(Math.abs(view.hp - (full / 2 - 5000 + 2 * 0.03 * full)) < 1e-6, "two quiet periods heal twice");

  seed(nk, partyId, (value) => {
    value.tier = 9;
    value.hp = server.partyColossusBaseHp(9) - 30000;
    value.optional = ["regeneration"];
    value.lastHit = now;
    value.regenAt = 0;
  });
  view = server.partyColossusMine(nk, "ann", now + 3.5 * 3600000);
  assert.ok(Math.abs(view.hp - (server.partyColossusBaseHp(9) - 30000 + 2 * 0.01 * server.partyColossusBaseHp(9))) < 1e-6);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "49": 5 };
  });
  view = server.partyColossusMine(nk, "ann", now + 10 * 3600000);
  assert.strictEqual(view.hp, server.partyColossusBaseHp(9) - 30000, "Endless Hunt: no healing");
});

test("solitude wants two hitters a day, night favours the night, mirrored favours one mark", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 30);
  atTier(nk, partyId, 25);
  let view = server.partyColossusReport(nk, "ann", { direct: [1000, 0, 0, 0] }, now);
  assert.strictEqual(view.accepted.counted, 600);
  view = server.partyColossusReport(nk, "bob", { direct: [1000, 0, 0, 0] }, now);
  assert.strictEqual(view.accepted.counted, 1250);
  atTier(nk, partyId, 30);
  view = server.partyColossusReport(nk, "ann", { direct: [1000, 0, 0, 0] }, now + 600000);
  assert.strictEqual(view.accepted.counted, server.partyColossusAtNight(now + 600000) ? 1300 : 700);
  seed(nk, partyId, (value) => {
    value.tier = 7;
    value.hp = server.partyColossusBaseHp(7);
    value.optional = ["mirrored"];
  });
  const weak = server.partyColossusMine(nk, "ann", now).weakClass;
  view = server.partyColossusReport(nk, "bob", { direct: [1000, 0, 0, 0], heroClass: weak }, now + 1200000);
  assert.strictEqual(view.accepted.counted, 1500);
  view = server.partyColossusReport(nk, "ann", { direct: [1000, 0, 0, 0], heroClass: (weak + 1) % 18 }, now + 1800000);
  assert.strictEqual(view.accepted.counted, 800);
});

test("limit armor makes the tier fifty times tougher", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 30);
  atTier(nk, partyId, 3);
  const view = server.partyColossusChoose(nk, "ann", ["limitArmor"], now);
  assert.strictEqual(view.hpFull, server.partyColossusBaseHp(3) * 50);
  assert.strictEqual(view.hp, view.hpFull);
});

test("the leader picks up to two optional hardships before the tier is hit, each gives a quarter more trophies", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  assert.throws(() => server.partyColossusChoose(nk, "bob", ["night"], now), /only the leader/);
  assert.throws(() => server.partyColossusChoose(nk, "ann", ["night", "shadow", "rift"], now), /at most 2/);
  assert.throws(() => server.partyColossusChoose(nk, "ann", ["nope"], now), /bad request/);
  let view = server.partyColossusChoose(nk, "ann", ["shadow", "greedy"], now);
  assert.deepStrictEqual(plain(view.active), ["shadow", "greedy"]);
  server.partyColossusReport(nk, "bob", { direct: [10, 0, 0, 0] }, now);
  assert.throws(() => server.partyColossusChoose(nk, "ann", [], now), /already been hit/);
  view = server.partyColossusReport(nk, "bob", { direct: [20000, 0, 0, 0] }, now + 600000);
  assert.strictEqual(view.tier, 2);
  const kill = server.partyColossusRead(nk, partyId).value.log[0];
  assert.strictEqual(kill.trophyBonus, 0.5);
  assert.deepStrictEqual(plain(kill.hardships), ["shadow", "greedy"]);
  assert.deepStrictEqual(plain(view.active), ["shadow", "greedy"], "the choice carries to the next tier");
});

test("Hunting Horn softens the mandatory hardship by 10%, Colossus Trap switches one off", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  atTier(nk, partyId, 5);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "38": 3 };
  });
  let view = server.partyColossusReport(nk, "ann", { direct: [1000, 0, 0, 0] }, now);
  assert.ok(Math.abs(view.accepted.counted - 640) < 1e-9);
  assert.throws(() => server.partyColossusSwitchOff(nk, "ann", "carapace", now), /does not allow/);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "44": 5 };
  });
  assert.throws(() => server.partyColossusSwitchOff(nk, "bob", "carapace", now), /only the leader/);
  view = server.partyColossusSwitchOff(nk, "ann", "carapace", now);
  assert.deepStrictEqual(plain(view.active), []);
  view = server.partyColossusReport(nk, "bob", { direct: [1000, 0, 0, 0] }, now);
  assert.strictEqual(view.accepted.counted, 1000);
});
