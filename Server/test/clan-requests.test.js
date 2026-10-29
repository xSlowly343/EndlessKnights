const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ClanServerRules;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

test("a player asks to join a closed clan and an officer lets them in", () => {
  const { nk, call, fails } = world("ann", "bob", "cid", "dan", "eve");
  const closed = call("ann", "clanCreateRpc", { access: "invite" });
  call("cid", "clanJoinRpc", { code: closed.code });
  call("dan", "clanJoinRpc", { code: closed.code });
  call("ann", "clanPromoteRpc", { userId: "cid" });
  const open = call("eve", "clanCreateRpc", { access: "open" });

  const listed = call("bob", "clanListRpc", { closed: true });
  assert.deepStrictEqual(listed.clans.map((card) => [card.id, card.byRequest]), [[closed.id, true]]);
  assert.strictEqual(call("bob", "clanListRpc", {}).clans[0].byRequest, false);
  fails("bob", "clanApplyRpc", { clanId: open.id }, "clan is open");
  fails("ann", "clanApplyRpc", { clanId: closed.id }, "already in a clan");

  const sent = call("bob", "clanApplyRpc", { clanId: closed.id });
  assert.strictEqual(sent.clanId, closed.id);
  call("bob", "clanApplyRpc", { clanId: closed.id });
  assert.deepStrictEqual(nk.notifications.filter((n) => n.subject === "clan_request").map((n) => n.userId).sort(), ["ann", "cid"], "the leader and officers hear about it once");
  assert.strictEqual(call("bob", "clanApplicationsRpc", {}).clans.length, 1);

  fails("dan", "clanRequestsRpc", {}, "only officers can do that");
  const requests = call("cid", "clanRequestsRpc", {});
  assert.deepStrictEqual(requests.requests.map((r) => [r.userId, r.name]), [["bob", "bob"]]);
  assert.strictEqual(requests.days, rules.requestDays);

  call("cid", "clanAnswerRpc", { userId: "bob", accept: true });
  assert.ok(call("bob", "clanMineRpc", {}).clan.members.some((m) => m.userId === "bob"));
  assert.strictEqual(call("bob", "clanApplicationsRpc", {}).clans.length, 0);
  assert.strictEqual(call("ann", "clanRequestsRpc", {}).requests.length, 0);
  fails("ann", "clanAnswerRpc", { userId: "bob", accept: true }, "no such request");
  assert.strictEqual(nk.notifications.filter((n) => n.subject === "clan_answer" && n.userId === "bob")[0].content.accepted, true);
});

test("requests are limited, can be declined or withdrawn and expire after their days", () => {
  const names = ["ann", "bob", "cid", "dan", "eve", "fay"];
  const { nk, call, fails } = world(...names, "zed");
  const clans = names.slice(0, 5).map((name) => call(name, "clanCreateRpc", { access: "invite" }));
  for (let i = 0; i < rules.requestsPerPlayer; i++)
    call("zed", "clanApplyRpc", { clanId: clans[i].id });
  fails("zed", "clanApplyRpc", { clanId: clans[rules.requestsPerPlayer].id }, "too many requests");

  call("ann", "clanAnswerRpc", { userId: "zed", accept: false });
  assert.strictEqual(call("zed", "clanApplicationsRpc", {}).clans.length, rules.requestsPerPlayer - 1);
  assert.strictEqual(nk.notifications.filter((n) => n.subject === "clan_answer")[0].content.accepted, false);
  assert.strictEqual(call("zed", "clanMineRpc", {}).clan, null);

  call("zed", "clanWithdrawRpc", { clanId: clans[1].id });
  assert.strictEqual(call("bob", "clanRequestsRpc", {}).requests.length, 0);

  const stored = nk.storageRead([{ collection: "clan_requests", key: clans[2].id, userId: server.PartySystemUser }])[0];
  stored.value.requests[0].at -= rules.requestDays * 86400000 + 1000;
  nk.storageWrite([{ collection: "clan_requests", key: clans[2].id, userId: server.PartySystemUser, value: stored.value }]);
  assert.strictEqual(call("cid", "clanRequestsRpc", {}).requests.length, 0, "an old request is gone");

  call("fay", "clanBlockRpc" in server ? "clanBlockRpc" : "partyBlockRpc", { userId: "eve" });
  fails("fay", "clanApplyRpc", { clanId: clans[4].id }, "clan not found");
});
