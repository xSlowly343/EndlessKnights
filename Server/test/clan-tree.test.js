const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ClanTreeServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const start = rules.nodes.find((n) => n.requires.length === 0);
const child = rules.nodes.find((n) => n.requires.length === 1 && n.requires[0] === start.id);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  const seals = (user, amount) => server.walletRefund(nk, user, "seals", amount, "test", Date.now());
  return { nk, call, fails, seals };
}

function clanOf(call, names) {
  const clan = call(names[0], "clanCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "clanJoinRpc", { code: clan.code });
  return clan;
}

test("rules come from clan_tree.json", () => {
  assert.ok(rules.minDonation > 0);
  assert.ok(start && child, "the tree has a start node and a node after it");
});

test("members give seals to the clan treasury", () => {
  const { nk, call, fails, seals } = world("ann", "bob", "eve");
  clanOf(call, ["ann", "bob"]);
  seals("bob", 500);
  fails("eve", "clanDonateRpc", { amount: 100 }, "not in a clan");
  fails("bob", "clanDonateRpc", { amount: rules.minDonation - 1 }, "bad request");
  fails("bob", "clanDonateRpc", { amount: 501 }, "not enough seals");
  const view = call("bob", "clanDonateRpc", { amount: 300 });
  assert.strictEqual(view.treasury, 300);
  assert.strictEqual(view.yours, 300);
  assert.deepStrictEqual(plain(view.donors.map((d) => [d.name, d.amount])), [["bob", 300]]);
  assert.strictEqual(view.log[0].kind, "donated");
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.seals, 200);
  assert.strictEqual(call("ann", "clanTreeRpc", {}).yours, 0);
});

test("the leader and officers open nodes from the treasury, in order, once", () => {
  const { call, fails, seals } = world("ann", "bob", "cid");
  clanOf(call, ["ann", "bob", "cid"]);
  seals("cid", start.cost + child.cost);
  call("cid", "clanDonateRpc", { amount: start.cost + child.cost });
  fails("cid", "clanTreeBuyRpc", { node: start.id }, "only the leader can do that");
  fails("ann", "clanTreeBuyRpc", { node: "nowhere" }, "bad request");
  fails("ann", "clanTreeBuyRpc", { node: child.id }, "not connected");
  call("ann", "clanPromoteRpc", { userId: "bob" });
  assert.deepStrictEqual(plain(call("bob", "clanTreeBuyRpc", { node: start.id }).taken), [start.id]);
  fails("ann", "clanTreeBuyRpc", { node: start.id }, "already taken");
  const view = call("ann", "clanTreeBuyRpc", { node: child.id });
  assert.deepStrictEqual(plain(view.taken), [start.id, child.id]);
  assert.strictEqual(view.treasury, 0);
  const expensive = rules.nodes.find((n) => n.requires.indexOf(child.id) >= 0);
  if (expensive)
    fails("ann", "clanTreeBuyRpc", { node: expensive.id }, "not enough seals");
  assert.deepStrictEqual(plain(call("cid", "clanTreeRpc", {}).taken), [start.id, child.id], "every member sees the opened nodes");
});

test("a donation that cannot be written goes back to the wallet", () => {
  const { nk, call, seals } = world("ann");
  const clan = clanOf(call, ["ann"]);
  seals("ann", 100);
  const write = nk.storageWrite.bind(nk);
  nk.storageWrite = (objects) => {
    if (objects.some((o) => o.collection === "clan_tree" && o.key === clan.id))
      throw new Error("storage write rejected - version check failed");
    return write(objects);
  };
  assert.throws(() => call("ann", "clanDonateRpc", { amount: 50 }), /clan is busy/);
  nk.storageWrite = write;
  const wallet = server.walletRead(nk, "ann").value;
  assert.strictEqual(wallet.balances.seals, 100);
  assert.strictEqual(wallet.ledger[0].source, "clan_donation_back");
});
