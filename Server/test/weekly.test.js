const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.WeeklyServerRules;
const day = 86400000;
const now = Date.UTC(2026, 10, 5, 12, 0);
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  return nk;
}

test("weekly rules come from chronicle.json", () => {
  assert.strictEqual(rules.count, 3);
  assert.ok(rules.trials.length >= rules.count);
  assert.strictEqual(rules.trials[0], "only_mouse");
  assert.strictEqual(rules.classes, 18);
});

test("a week starts on monday and the same week gives the same trials as the game", () => {
  const thursday = Date.UTC(2026, 10, 5, 12, 0);
  const sunday = Date.UTC(2026, 10, 8, 12, 0);
  const monday = Date.UTC(2026, 10, 9, 12, 0);
  assert.strictEqual(server.weeklyWeekOf(thursday), server.weeklyWeekOf(sunday));
  assert.strictEqual(server.weeklyWeekOf(monday), server.weeklyWeekOf(sunday) + 1);
  const week = server.weeklyWeekOf(now);
  const picked = plain(server.weeklyTrials(week));
  const n = rules.trials.length;
  assert.deepStrictEqual(picked, [0, 1, 2].map((i) => rules.trials[(week * 3 + i) % n]));
  assert.notDeepStrictEqual(plain(server.weeklyTrials(week + 1)), picked);
});

test("the best stage of a weekly trial goes to its board and only grows", () => {
  const nk = world("ann", "bob", "cid");
  const week = server.weeklyWeekOf(now);
  const trial = server.weeklyTrials(week)[0];
  server.weeklySubmit(nk, "ann", { week: week, trial: trial, stage: 120, heroClass: 2 }, now);
  server.weeklySubmit(nk, "bob", { week: week, trial: trial, stage: 300, heroClass: 5 }, now);
  const lower = server.weeklySubmit(nk, "ann", { week: week, trial: trial, stage: 90, heroClass: 2 }, now);
  assert.strictEqual(lower.stage, 120, "a worse run does not lower the record");

  const board = plain(server.weeklyBoard(nk, "ann", { trial: trial, friends: ["bob", "cid"] }, now));
  assert.deepStrictEqual(board.top.map((r) => [r.name, r.stage, r.rank]), [["bob", 300, 1], ["ann", 120, 2]]);
  assert.strictEqual(board.mine.stage, 120);
  assert.strictEqual(board.mine.heroClass, 2);
  assert.deepStrictEqual(board.friends.map((r) => r.userId), ["bob", "ann"]);
});

test("a submit outside this week or for another trial is refused", () => {
  const nk = world("ann");
  const week = server.weeklyWeekOf(now);
  const others = rules.trials.filter((t) => server.weeklyTrials(week).indexOf(t) < 0);
  const trial = server.weeklyTrials(week)[0];
  assert.throws(() => server.weeklySubmit(nk, "ann", { week: week - 2, trial: trial, stage: 10, heroClass: 0 }, now), /not this week/);
  assert.throws(() => server.weeklySubmit(nk, "ann", { week: week, trial: others[0], stage: 10, heroClass: 0 }, now), /not a trial of this week/);
  assert.throws(() => server.weeklySubmit(nk, "ann", { week: week, trial: trial, stage: 0, heroClass: 0 }, now), /bad stage/);
  assert.throws(() => server.weeklySubmit(nk, "ann", { week: week, trial: trial, stage: 10, heroClass: 18 }, now), /bad class/);
  const lastWeek = server.weeklyTrials(week - 1)[0];
  assert.strictEqual(server.weeklySubmit(nk, "ann", { week: week - 1, trial: lastWeek, stage: 10, heroClass: 0 }, now + day).stage, 10, "a run near the week edge still counts");
});

test("an empty board call returns the week and its trials", () => {
  const nk = world("ann");
  const board = plain(server.weeklyBoard(nk, "ann", {}, now));
  assert.strictEqual(board.week, server.weeklyWeekOf(now));
  assert.strictEqual(board.trials.length, 3);
});
