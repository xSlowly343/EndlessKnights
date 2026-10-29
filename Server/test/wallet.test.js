const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.WalletServerRules;
const day = 86400000;
const noon = Date.UTC(2026, 9, 29, 9, 0);
const plain = (o) => JSON.parse(JSON.stringify(o));
const source = (id) => rules.sources.find((s) => s.id === id);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

function killBoss(host, slots) {
  const encounter = host.encounter;
  while (!encounter.fightingBoss) {
    for (const slot of slots)
      encounter.reportInput(slot);
    encounter.applyDamage(slots[0], encounter.enemyId, encounter.barLeft + 1);
    host.tick();
  }
  for (const slot of slots)
    encounter.reportInput(slot);
  const share = encounter.barLeft / slots.length / encounter.damageTakenMultiplier() + 1e-9;
  for (const slot of slots)
    encounter.applyDamage(slot, encounter.enemyId, share);
  host.tick();
}

test("rules come from coop_currency.json", () => {
  assert.deepStrictEqual(plain(rules.currencies), ["friendship", "seals"]);
  assert.strictEqual(source("boss_together").currency, "friendship");
  assert.ok(source("ghost_helped").perFriendDailyCap > 0);
});

test("a grant stops at the daily cap of its source and the cap resets the next day", () => {
  const { nk, call } = world("ann");
  const cap = source("boss_together").dailyCap;
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", cap - 1, noon), cap - 1);
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", 5, noon + 1000), 1);
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", 5, noon + 2000), 0);
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", 5, noon + day), 5);
  const wallet = server.walletRead(nk, "ann").value;
  assert.strictEqual(wallet.balances.friendship, cap + 5);
  assert.strictEqual(wallet.balances.seals, 0);
  assert.strictEqual(call("ann", "coopWalletRpc").balances.friendship, cap + 5);
});

test("the day turns at midnight Moscow time", () => {
  const { nk } = world("ann");
  const cap = source("boss_together").dailyCap;
  const lateEvening = Date.UTC(2026, 9, 29, 20, 59);
  server.walletGrant(nk, "ann", "boss_together", cap, lateEvening);
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", 1, Date.UTC(2026, 9, 29, 20, 59, 30)), 0, "23:59 in Moscow is still the same day");
  assert.strictEqual(server.walletGrant(nk, "ann", "boss_together", 1, Date.UTC(2026, 9, 29, 21, 0, 30)), 1, "00:00 in Moscow is a new day");
});

test("the ledger merges grants of one source in a day, keeps the newest first and only so many", () => {
  const { nk } = world("ann");
  server.walletGrant(nk, "ann", "boss_together", 2, noon);
  server.walletGrant(nk, "ann", "boss_together", 3, noon + 1000);
  let wallet = server.walletRead(nk, "ann").value;
  assert.deepStrictEqual(plain(wallet.ledger.map((e) => [e.source, e.amount])), [["boss_together", 5]]);
  for (let i = 1; i <= rules.ledgerSize + 3; i++)
    server.walletGrant(nk, "ann", i % 2 === 0 ? "boss_together" : "ghost_helped", 1, noon + i * day);
  wallet = server.walletRead(nk, "ann").value;
  assert.strictEqual(wallet.ledger.length, rules.ledgerSize);
  assert.strictEqual(wallet.ledger[0].at, noon + (rules.ledgerSize + 3) * day);
});

test("spending takes only what there is", () => {
  const { nk } = world("ann");
  server.walletGrant(nk, "ann", "boss_together", 4, noon);
  assert.strictEqual(server.walletSpend(nk, "ann", "friendship", 3, "gift", noon + 1), 1);
  assert.throws(() => server.walletSpend(nk, "ann", "friendship", 2, "gift", noon + 2), /not enough friendship/);
  assert.throws(() => server.walletSpend(nk, "ann", "gold", 1, "gift", noon + 2), /bad request/);
  assert.throws(() => server.walletSpend(nk, "ann", "friendship", 0.5, "gift", noon + 2), /bad request/);
  const wallet = server.walletRead(nk, "ann").value;
  assert.strictEqual(wallet.balances.friendship, 1);
  assert.deepStrictEqual(plain(wallet.ledger[0]), { at: noon + 1, source: "gift", currency: "friendship", amount: -3 });
});

test("the host counts bosses for every paid member when two or more are paid", () => {
  const host = new server.PartyHost(server.PartyServerRules, "5");
  ["a", "b", "c"].forEach((name, i) => host.join("user-" + name, String(11 + i)));
  killBoss(host, [0]);
  assert.deepStrictEqual(plain(host.takeFriendBosses()), {}, "a boss killed alone gives no friendship");
  killBoss(host, [0, 1]);
  killBoss(host, [0, 1]);
  assert.deepStrictEqual(plain(host.takeFriendBosses()), { "user-a": 2, "user-b": 2 });
  assert.deepStrictEqual(plain(host.takeFriendBosses()), {});
});

test("the match pays bosses together into the wallets when it flushes", () => {
  const { nk } = world("user-a", "user-b");
  server.walletGrantBosses(nk, logger, { "user-a": 2, "user-b": 30 }, noon);
  assert.strictEqual(server.walletRead(nk, "user-a").value.balances.friendship, 2);
  assert.strictEqual(server.walletRead(nk, "user-b").value.balances.friendship, source("boss_together").dailyCap);
});

test("a ghost's owner is thanked only by the player who picked that ghost, a few bosses a day per friend", () => {
  const { nk, call, fails } = world("ann", "bob", "cid");
  call("ann", "ghostUploadRpc", { share: true, heroClass: 8, shareRate: 0.5, marks: [], activeSeconds: 30 });
  fails("bob", "ghostThanksRpc", { userId: "ann", bosses: 1 }, "not your ghost");
  assert.strictEqual(call("bob", "ghostPickRpc", { userId: "ann" }).ghost.userId, "ann");
  fails("bob", "ghostThanksRpc", { userId: "ann", bosses: 0 }, "bad request");
  fails("bob", "ghostThanksRpc", { userId: "cid", bosses: 1 }, "not your ghost");
  const perFriend = source("ghost_helped").perFriendDailyCap;
  assert.strictEqual(call("bob", "ghostThanksRpc", { userId: "ann", bosses: 50 }).granted, perFriend * source("ghost_helped").amount);
  assert.strictEqual(call("bob", "ghostThanksRpc", { userId: "ann", bosses: 5 }).granted, 0);
  call("bob", "ghostPickRpc", { userId: "ann" });
  assert.strictEqual(call("bob", "ghostThanksRpc", { userId: "ann", bosses: 5 }).granted, 0, "picking the ghost again does not reset the day");
  call("cid", "ghostPickRpc", { userId: "ann" });
  assert.strictEqual(call("cid", "ghostThanksRpc", { userId: "ann", bosses: 1 }).granted, 1);
  const wallet = call("ann", "coopWalletRpc");
  assert.strictEqual(wallet.balances.friendship, perFriend + 1);
  assert.strictEqual(wallet.today.find((s) => s.source === "ghost_helped").earned, perFriend + 1);
  assert.strictEqual(call("bob", "coopWalletRpc").balances.friendship, 0, "the helper earns nothing");
});
