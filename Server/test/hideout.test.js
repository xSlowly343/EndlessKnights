const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.HideoutServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const minute = 60000;
const now = 1800000000000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function party(call, names) {
  const created = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: created.code });
  return created;
}

const at = (nk, user, here, time) => plain(server.hideoutUpdate(nk, user, here, time));

function bosses(nk, user, count) {
  const group = server.partyGroupOfUser(nk, user);
  server.chronicleUpdate(nk, group.id, (value) => { value.stats.bossesTogether = count; });
}

test("rules come from party_hideout.json with three base tiers", () => {
  assert.strictEqual(rules.tiers.length, 3);
  assert.strictEqual(rules.tiers[1].sitSeconds, 2 * 3600);
});

test("a party of one has no hideout, two open the rest stop", () => {
  const { nk, call } = world("ann", "bob");
  assert.throws(() => at(nk, "ann", true, now), /not in a party/);
  party(call, ["ann"]);
  assert.strictEqual(at(nk, "ann", true, now).tier, 0);
  call("bob", "partyJoinRpc", { code: call("ann", "partyMineRpc").party.code });
  assert.strictEqual(at(nk, "ann", true, now + 1).tier, 1);
});

test("two at the fire see each other and sit together, one alone does not", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  at(nk, "ann", true, now);
  assert.strictEqual(at(nk, "ann", true, now + minute).sitSeconds, 0, "alone at the fire");
  const both = at(nk, "bob", true, now + 2 * minute);
  assert.deepStrictEqual(both.here.map((entry) => entry.name).sort(), ["ann", "bob"]);
  const later = at(nk, "ann", true, now + 3 * minute);
  assert.strictEqual(later.sitSeconds, 60);
  const gap = at(nk, "bob", true, now + 30 * minute);
  assert.ok(gap.sitSeconds <= 60 + rules.maxSitStepMs / 1000, "a long gap counts no more than one step");
  at(nk, "ann", true, now + 30.5 * minute);
  const left = at(nk, "bob", false, now + 31 * minute);
  assert.deepStrictEqual(left.here.map((entry) => entry.name), ["ann"]);
});

test("a player who stops pinging leaves the fire on their own", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  at(nk, "ann", true, now);
  const view = at(nk, "bob", true, now + rules.hereMs + 1);
  assert.deepStrictEqual(view.here.map((entry) => entry.name), ["bob"]);
});

test("the yard needs bosses together and two hours by the fire", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  bosses(nk, "ann", rules.tiers[1].bosses);
  assert.strictEqual(at(nk, "ann", true, now).tier, 1, "bosses alone are not enough");
  let time = now;
  at(nk, "bob", true, time);
  while (time < now + 2 * 3600 * 1000 + 2 * minute) {
    time += minute;
    at(nk, time % (2 * minute) === 0 ? "ann" : "bob", true, time);
  }
  const view = at(nk, "ann", true, time + minute);
  assert.ok(view.sitSeconds >= rules.tiers[1].sitSeconds);
  assert.strictEqual(view.tier, 2);
  bosses(nk, "ann", rules.tiers[2].bosses);
  assert.strictEqual(at(nk, "ann", true, time + 2 * minute).tier, 2, "the homestead waits for party orders and a second-level fire");
});

test("the hideout is read and pinged through the rpc", () => {
  const { call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  assert.strictEqual(call("ann", "hideoutPresenceRpc", { here: true }).here.length, 1);
  assert.strictEqual(call("bob", "hideoutViewRpc").here.length, 1);
  assert.throws(() => call("ann", "hideoutPresenceRpc", { here: "yes" }), /bad request/);
});
