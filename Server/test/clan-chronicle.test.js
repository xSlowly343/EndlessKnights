const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

test("the clan chronicle records the life of the clan and gives feats to everyone in it", () => {
  const { nk, call, fails } = world("ann", "bob", "cid");
  const clan = call("ann", "clanCreateRpc", { access: "open" });
  call("bob", "clanJoinRpc", { clanId: clan.id });
  call("cid", "clanJoinRpc", { clanId: clan.id });
  call("cid", "clanLeaveRpc", {});
  fails("cid", "clanChronicleRpc", {}, "not in a clan");

  let view = call("bob", "clanChronicleRpc", {});
  assert.deepStrictEqual(view.records.map((r) => [r.kind, r.name]), [["left", "cid"], ["joined", "cid"], ["joined", "bob"], ["founded", "ann"]]);
  assert.strictEqual(view.stats.membersMax, 3);

  server.clanChronicleNote(nk, logger, clan.id, { raidsKilled: 1 }, { at: Date.now(), kind: "raid", name: "", feat: "", level: 0, detail: "devourer" }, ["ann", "bob"]);
  server.clanChronicleNote(nk, logger, clan.id, { challengesDone: 10, sealsDonated: 1200 }, null, ["ann", "bob"]);
  view = call("ann", "clanChronicleRpc", {});
  const level = (id) => view.feats.find((feat) => feat.id === id).level;
  assert.strictEqual(level("clan_first_fall"), 1);
  assert.strictEqual(level("clan_many_hands"), 1);
  assert.strictEqual(level("clan_treasury"), 1);
  assert.strictEqual(level("clan_old_hunters"), 0);
  assert.deepStrictEqual(view.titles, ["devourer_slayers"]);
  assert.ok(view.records.some((r) => r.kind === "feat" && r.feat === "clan_first_fall"));
  assert.ok(view.records.some((r) => r.kind === "raid" && r.detail === "devourer"));
  const kept = nk.storageRead([{ collection: "clan_feats", key: clan.id, userId: "bob" }])[0].value.feats;
  assert.strictEqual(kept.clan_first_fall.level, 1, "a member keeps a copy of the feats earned with them");

  server.clanChronicleNote(nk, logger, clan.id, { membersMax: 2 }, null, []);
  assert.strictEqual(call("ann", "clanChronicleRpc", {}).stats.membersMax, 3, "the most members ever stays");
  call("bob", "clanLeaveRpc", {});
  call("ann", "clanLeaveRpc", {});
  assert.strictEqual(nk.storageRead([{ collection: "clan_chronicle", key: clan.id, userId: server.PartySystemUser }]).length, 0, "a dissolved clan forgets its chronicle");
  assert.strictEqual(nk.storageRead([{ collection: "clan_feats", key: clan.id, userId: "bob" }]).length, 1);
});
