const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.trials;
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

function experienceFor(level) {
  let total = 0;
  for (let l = 1; l < level; l++)
    total += server.partyGrowthNeed(l);
  return total;
}

function reachLevel(nk, partyId, level) {
  const have = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyGrowthAdd(nk, partyId, experienceFor(level) - have);
}

const open = () => server.partyTrialOpenIds(server.partyTrialWeek(now));
const trialOf = (view, id) => view.trials.filter((t) => t.id === id)[0];

function clear(nk, user, trial, tier, fighters, at) {
  server.partyTrialStart(nk, user, { trial: trial, tier: tier, fighters: fighters }, at);
  return server.partyTrialFinish(nk, user, { cleared: true }, at + 4 * minute);
}

test("trial rules come from party.json", () => {
  assert.strictEqual(rules.list.length, 8);
  assert.deepStrictEqual(plain(rules.list.map((t) => t.id)), ["choir", "relay", "mirror", "silence", "pulse", "shields", "chain", "marathon"]);
  assert.strictEqual(rules.perWeek, 2);
  assert.strictEqual(rules.tries, 5);
  assert.strictEqual(rules.minFighters, 2);
  assert.strictEqual(rules.list[7].waves, 20);
  assert.deepStrictEqual(plain(rules.extraTries), [{ node: 31, rank: 3, value: 1 }]);
  assert.deepStrictEqual(plain(rules.carry), [{ node: 31, rank: 5, value: 1 }]);
});

test("two of eight trials are open each week and every trial comes round in four weeks", () => {
  const week = server.partyTrialWeek(now);
  const seen = [];
  for (let i = 0; i < 4; i++)
    seen.push(...server.partyTrialOpenIds(week + i));
  assert.strictEqual(open().length, 2);
  assert.deepStrictEqual(plain(seen).sort(), plain(rules.list.map((t) => t.id)).sort());
  assert.strictEqual(new Date(server.partyTrialWeekEndsAt(now)).getUTCDay(), 1, "the week turns on Monday");
});

test("trials open on party level 20 with a tier ceiling of 10 that grows with the level", () => {
  const { nk, partyId } = party("ann", "bob");
  let view = server.partyTrialMine(nk, "ann", now);
  assert.strictEqual(view.opened, false);
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: open()[0], tier: 1, fighters: ["ann", "bob"] }, now), /not open yet/);
  reachLevel(nk, partyId, 20);
  view = server.partyTrialMine(nk, "ann", now);
  assert.strictEqual(view.opened, true);
  assert.strictEqual(view.ceiling, 10);
  reachLevel(nk, partyId, 25);
  assert.strictEqual(server.partyTrialMine(nk, "ann", now).ceiling, 15);
  reachLevel(nk, partyId, 90);
  assert.strictEqual(server.partyTrialMine(nk, "ann", now).ceiling, 35);
});

test("a trial needs two members, an open trial, a free try and the next tier", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  const closed = rules.list.map((t) => t.id).filter((id) => open().indexOf(id) < 0)[0];
  const trial = open().filter((id) => id !== "marathon")[0];
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann"] }, now), /at least 2/);
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "zed"] }, now), /only party members/);
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: closed, tier: 1, fighters: ["ann", "bob"] }, now), /not open this week/);
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: trial, tier: 2, fighters: ["ann", "bob"] }, now), /tier is not open/);
  const view = server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now);
  assert.strictEqual(view.run.trial, trial);
  assert.strictEqual(trialOf(view, trial).triesLeft, 4);
  assert.throws(() => server.partyTrialStart(nk, "bob", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now), /already being fought/);
});

test("a cleared tier becomes the best tier, gives seals, signs to the treasury, party experience and a chronicle line", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  const trial = open().filter((id) => id !== "marathon")[0];
  const experience = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now);
  assert.throws(() => server.partyTrialFinish(nk, "bob", { cleared: true }, now + 4 * minute), /no trial/);
  assert.throws(() => server.partyTrialFinish(nk, "ann", { cleared: true }, now + 1000), /too fast/);
  let view = server.partyTrialFinish(nk, "ann", { cleared: true }, now + 4 * minute);
  assert.strictEqual(view.result.firstClear, true);
  assert.strictEqual(view.result.signs, 20);
  assert.strictEqual(trialOf(view, trial).best, 1);
  assert.strictEqual(view.run, null);
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 20);
  assert.strictEqual(server.partyGrowthRead(nk, partyId).value.experience, experience + 5);
  const record = server.chronicleRead(nk, partyId).value.records.filter((r) => r.kind === "trial")[0];
  assert.strictEqual(record.feat, trial);
  assert.strictEqual(record.level, 1);
  assert.strictEqual(record.name, "ann, bob");

  view = clear(nk, "bob", trial, 1, ["ann", "bob"], now + 10 * minute);
  assert.strictEqual(view.result.firstClear, false, "a tier pays only the first time");
  assert.strictEqual(view.result.signs, 0);
  view = clear(nk, "bob", trial, 2, ["ann", "bob"], now + 20 * minute);
  assert.strictEqual(view.result.signs, 40);
  assert.strictEqual(trialOf(view, trial).best, 2);
});

test("a failed or abandoned run still spends the try, five tries a week and the week resets them", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  const trial = open().filter((id) => id !== "marathon")[0];
  server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now);
  let view = server.partyTrialFinish(nk, "ann", { cleared: false }, now + 1000);
  assert.strictEqual(view.result.tier, 0);
  assert.strictEqual(trialOf(view, trial).best, 0);
  server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now);
  assert.throws(() => server.partyTrialAbandon(nk, "cid", now), /not in a party/);
  server.partyTrialAbandon(nk, "ann", now);
  server.partyTrialStart(nk, "bob", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now);
  view = server.partyTrialMine(nk, "ann", now + 20 * minute);
  assert.strictEqual(view.run, null, "a forgotten run ends after its time and the slack");
  server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now + 20 * minute);
  server.partyTrialAbandon(nk, "ann", now + 20 * minute);
  server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now + 20 * minute);
  server.partyTrialAbandon(nk, "ann", now + 20 * minute);
  assert.strictEqual(trialOf(server.partyTrialMine(nk, "ann", now + 20 * minute), trial).triesLeft, 0);
  assert.throws(() => server.partyTrialStart(nk, "ann", { trial: trial, tier: 1, fighters: ["ann", "bob"] }, now + 20 * minute), /no tries left/);
  const nextWeek = server.partyTrialWeekEndsAt(now) + minute;
  view = server.partyTrialMine(nk, "ann", nextWeek);
  assert.ok(view.trials.every((t) => t.triesLeft === rules.tries));
});

test("the marathon reports waves held, capped by the ceiling and the clock", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  let at = now;
  while (server.partyTrialOpenIds(server.partyTrialWeek(at)).indexOf("marathon") < 0)
    at += 7 * 86400000;
  server.partyTrialStart(nk, "ann", { trial: "marathon", fighters: ["ann", "bob"] }, at);
  assert.throws(() => server.partyTrialFinish(nk, "ann", { waves: 21 }, at + 10 * minute), /bad request/);
  assert.throws(() => server.partyTrialFinish(nk, "ann", { waves: 14 }, at + 6 * minute), /too fast/, "wave 14 starts after six and a half minutes");
  let view = server.partyTrialFinish(nk, "ann", { waves: 14 }, at + 7 * minute);
  assert.strictEqual(view.result.tier, 10, "the ceiling at level 20 is 10");
  assert.strictEqual(trialOf(view, "marathon").best, 10);
  assert.strictEqual(view.result.signs, 20 * 55);
});

test("Trial Takers give an extra try at level 3 and carry the best tier to the other trial at level 5", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "31": 3 };
  });
  const [first, second] = open();
  const trial = first === "marathon" ? second : first;
  const other = trial === first ? second : first;
  let view = server.partyTrialMine(nk, "ann", now);
  assert.strictEqual(trialOf(view, trial).triesLeft, 6);
  assert.strictEqual(view.carry, false);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "31": 5 };
  });
  view = clear(nk, "ann", trial, 1, ["ann", "bob"], now);
  view = clear(nk, "ann", trial, 2, ["ann", "bob"], now + 10 * minute);
  assert.deepStrictEqual(plain(view.result.carried), [other]);
  assert.strictEqual(trialOf(view, other).best, 1);
  assert.strictEqual(view.result.signs, 40, "a carried tier gives no signs");
});

test("the trials view comes through the RPC", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 20);
  const view = call("bob", "partyTrialsRpc");
  assert.strictEqual(view.partyId, partyId);
  assert.strictEqual(view.trials.length, 8);
  assert.strictEqual(view.trials.filter((t) => t.open).length, 2);
});
