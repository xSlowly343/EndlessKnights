const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rules = party.GhostServerRules;
const hour = 3600 * 1000;
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(party[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request) => assert.throws(() => call(user, rpc, request), /bad request/);
  return { nk, call, fails };
}

const upload = (call, user, rate) => call(user, "ghostUploadRpc", { share: true, heroClass: 8, shareRate: rate, marks: ["curse", "curse", "stun"], activeSeconds: 30 });

test("a snapshot is checked before it is kept", () => {
  const { call, fails } = world("ann");
  fails("ann", "ghostUploadRpc", { share: true, heroClass: 14, shareRate: 1, marks: [], activeSeconds: 0 });
  fails("ann", "ghostUploadRpc", { share: true, heroClass: 1.5, shareRate: 1, marks: [], activeSeconds: 0 });
  fails("ann", "ghostUploadRpc", { share: true, heroClass: 1, shareRate: -1, marks: [], activeSeconds: 0 });
  fails("ann", "ghostUploadRpc", { share: true, heroClass: 1, shareRate: 1, marks: ["<script>"], activeSeconds: 0 });
  fails("ann", "ghostUploadRpc", { share: true, heroClass: 1, shareRate: 1, marks: new Array(11).fill("a"), activeSeconds: 0 });
  const reply = upload(call, "ann", 0.4);
  assert.strictEqual(reply.shared, true);
  assert.strictEqual(reply.activity, rules.minActivity, "a few seconds of play give the lowest activity");
});

test("activity is the play of the last day by the hour, capped by real time", () => {
  const record = { heroClass: 0, shareRate: 1, marks: [], updatedAt: 0, hourSeconds: new Array(24).fill(0), hourIndex: 101 };
  record.hourSeconds[100 % 24] = 3600;
  record.hourSeconds[101 % 24] = 3600;
  assert.strictEqual(party.ghostActivity(record, 101 * hour), 2 / rules.fullHours);
  assert.strictEqual(party.ghostActivity(record, 124 * hour + 1), 3600 / 3600 / rules.fullHours, "the hour a day ago is gone");
  assert.strictEqual(party.ghostActivity(record, 200 * hour), rules.minActivity, "nothing played in the last day");
  record.hourSeconds = new Array(24).fill(3600);
  assert.strictEqual(party.ghostActivity(record, 100 * hour), 1, "full activity caps at 1");
});

test("the ghost comes from the party first, then from the pool, never yourself or someone you blocked", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan");
  upload(call, "ann", 0.4);
  upload(call, "cid", 0.9);
  upload(call, "dan", 0.7);
  assert.strictEqual(call("bob", "ghostPickRpc").ghost !== null, true, "someone from the pool");

  const view = call("bob", "partyCreateRpc", {});
  call("ann", "partyJoinRpc", { code: view.code });
  for (let i = 0; i < 5; i++)
    assert.strictEqual(call("bob", "ghostPickRpc").ghost.userId, "ann", "a party member first");
  const ghost = call("bob", "ghostPickRpc").ghost;
  assert.strictEqual(ghost.name, "ann");
  assert.strictEqual(ghost.heroClass, 8);
  assert.strictEqual(ghost.shareRate, 0.4);
  assert.deepStrictEqual(plain(ghost.marks), ["curse", "stun"]);

  assert.strictEqual(call("bob", "ghostPickRpc", { userId: "cid" }).ghost.userId, "cid", "a ghost can be asked for by name");
  assert.strictEqual(call("bob", "ghostPickRpc", { userId: "bob" }).ghost, null, "not yourself");
  call("bob", "partyBlockRpc", { userId: "cid" });
  assert.strictEqual(call("bob", "ghostPickRpc", { userId: "cid" }).ghost, null, "not someone you blocked");

  call("ann", "ghostUploadRpc", { share: false });
  assert.strictEqual(call("bob", "ghostPickRpc", { userId: "ann" }).ghost, null, "a player who stopped sharing is gone");
  assert.strictEqual(plain(nk.storageRead([{ collection: "ghost_pool", key: "recent", userId: party.PartySystemUser }])[0].value.users).indexOf("ann"), -1);
});

test("an old snapshot is not offered", () => {
  const { nk, call } = world("ann", "bob");
  upload(call, "ann", 0.4);
  const stored = nk.storageRead([{ collection: "ghost", key: "snapshot", userId: "ann" }])[0];
  stored.value.updatedAt = Date.now() - (rules.freshHours + 1) * hour;
  nk.storageWrite([{ collection: "ghost", key: "snapshot", userId: "ann", value: stored.value }]);
  assert.strictEqual(call("bob", "ghostPickRpc").ghost, null);
});
