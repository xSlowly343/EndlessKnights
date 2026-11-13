const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const now = Date.UTC(2026, 10, 14, 10);
const minute = 60000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function party(...names) {
  const w = world(...names);
  const view = w.call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    w.call(name, "partyJoinRpc", { code: view.code });
  return { ...w, partyId: view.id };
}

test("the party level follows 250 + 60 × (level − 1) up to level 100", () => {
  assert.strictEqual(rules.maxLevel, 100);
  assert.strictEqual(server.partyGrowthNeed(1), 250);
  assert.strictEqual(server.partyGrowthNeed(4), 430);
  assert.deepStrictEqual(plain(server.partyGrowthLevel(0)), { level: 1, into: 0, need: 250 });
  assert.strictEqual(server.partyGrowthLevel(1359).level, 4);
  assert.deepStrictEqual(plain(server.partyGrowthLevel(1360)), { level: 5, into: 0, need: 490 });
  assert.strictEqual(server.partyGrowthLevel(1e12).level, 100);
  assert.strictEqual(server.partyGrowthLevel(1e12).need, 0);
  assert.ok(rules.unlocks.some((u) => u.level === 5 && u.kind === "mascot"), "the mascot opens at level 5");
});

test("active minutes of every member count, capped by real time and a daily limit", () => {
  const { nk, partyId } = party("ann", "bob");
  let view = plain(server.partyGrowthReport(nk, "ann", 600, now));
  assert.strictEqual(view.experience, 10);
  view = plain(server.partyGrowthReport(nk, "ann", 600, now + 1000));
  assert.strictEqual(view.experience, 11, "a report right after the last one counts only the slack");
  view = plain(server.partyGrowthReport(nk, "bob", 3600, now));
  assert.strictEqual(view.experience, 11 + rules.firstReportSeconds / 60, "the first report is capped");
  assert.strictEqual(view.partyId, partyId);
  let t = now + 2000;
  for (let i = 0; i < 26; i++) {
    t += 30 * minute;
    server.partyGrowthReport(nk, "ann", 1800, t);
  }
  const ann = server.partyGrowthRead(nk, partyId).value.members.ann;
  assert.strictEqual(ann.minutes, rules.dailyMinutesPerMember, "no more than the daily limit");
  server.partyGrowthReport(nk, "ann", 1800, Date.UTC(2026, 10, 15, 10));
  assert.ok(server.partyGrowthRead(nk, partyId).value.members.ann.minutes < rules.dailyMinutesPerMember, "a new day starts again");
});

test("bosses and guardians together and plain adds raise the party, outside a party nothing counts", () => {
  const { nk, call, partyId } = party("ann", "bob");
  server.partyGrowthBattle(nk, partyId, 2, 1);
  server.partyGrowthAdd(nk, partyId, rules.order);
  server.partyGrowthAdd(nk, partyId, 30 * rules.perBuildItem);
  const view = call("ann", "partyGrowthRpc");
  assert.strictEqual(view.experience, 2 * rules.bossTogether + rules.guardianTogether + rules.order + 3);
  assert.strictEqual(view.level, 1);
  const lone = world("cat");
  assert.throws(() => server.partyGrowthReport(lone.nk, "cat", 60, now), /not in a party/);
  assert.throws(() => server.partyGrowthReport(nk, "ann", -5, now), /bad request/);
});
