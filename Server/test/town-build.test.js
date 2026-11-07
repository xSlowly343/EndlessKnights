const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.TownBuildServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const day = 86400000;
const now = server.raidWeekStart(3000) + 12 * 3600000;

function world(count) {
  const nk = new FakeNakama();
  const names = [];
  for (let i = 0; i < count; i++) {
    names.push("p" + i);
    nk.addUser("p" + i, "p" + i, "7656119800000" + String(1000 + i));
  }
  return { nk, names };
}

const add = (nk, user, actions, at) => plain(server.townBuildAdd(nk, logger, user, actions, at === undefined ? now : at));

test("rules come from guilds.json and cover every camp building guild", () => {
  assert.ok(rules.order.length > 0);
  assert.ok(rules.dailyCap > 0);
  assert.ok(rules.minTarget > 0);
  for (const guild of rules.startBuilt)
    assert.ok(!rules.order.includes(guild));
});

test("actions go to the current guild hall and each player is capped per day", () => {
  const { nk } = world(1);
  const first = add(nk, "p0", 1200);
  assert.strictEqual(first.project, rules.order[0]);
  assert.strictEqual(first.accepted, 1200);
  assert.strictEqual(first.progress, 1200);
  assert.strictEqual(first.target, rules.minTarget, "a quiet server needs the minimum target");
  assert.deepStrictEqual(first.startBuilt, plain(rules.startBuilt));
  const capped = add(nk, "p0", rules.dailyCap);
  assert.strictEqual(capped.accepted, rules.dailyCap - 1200);
  assert.strictEqual(add(nk, "p0", 10).accepted, 0, "the day is full");
  assert.strictEqual(add(nk, "p0", 10, now + day).accepted, 10, "the next day opens again");
  assert.throws(() => add(nk, "p0", 0), /bad request/);
  assert.throws(() => add(nk, "p0", 2.5), /bad request/);
});

test("a full hall is built, joins the town list and the next project starts", () => {
  const { nk, names } = world(Math.ceil(rules.minTarget / rules.dailyCap) + 1);
  let view = null;
  for (const name of names)
    view = add(nk, name, rules.dailyCap);
  assert.deepStrictEqual(view.built, [rules.order[0]]);
  assert.strictEqual(view.project, rules.order[1]);
  assert.strictEqual(view.progress, names.length * rules.dailyCap - rules.minTarget, "the extra goes to the next hall");
  assert.deepStrictEqual(plain(server.townMine(nk, names[0], now)).guilds, [rules.order[0]]);
});

test("the target grows with the players who built in the last days", () => {
  const days = {};
  for (let i = 1; i <= rules.buildDays; i++)
    days[String(100 - i)] = 200;
  assert.strictEqual(server.townBuildTarget(days, 100), 200 * rules.actionsPerPlayerDay * rules.buildDays);
  assert.strictEqual(server.townBuildTarget({}, 100), rules.minTarget);
});

test("when every hall is built the town stops counting", () => {
  const { nk } = world(1);
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: rules.order.slice(), project: rules.order.length, progress: 0, target: 0, days: {} }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  const view = add(nk, "p0", 100);
  assert.strictEqual(view.project, "");
  assert.strictEqual(view.progress, 0);
  assert.deepStrictEqual(view.built, plain(rules.order));
});
