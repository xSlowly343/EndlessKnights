const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.GiftServerRules;
const day = 24 * 3600 * 1000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  const tokens = (user, amount) => server.walletGrant(nk, user, "mentor_hours", amount, Date.now());
  const friendship = (user) => call(user, "coopWalletRpc").balances.friendship;
  const gifts = (user) => call(user, "mailListRpc").letters.filter((letter) => letter.kind === "gift");
  return { nk, call, fails, tokens, friendship, gifts };
}

test("a gem goes to a friend by mail for friendship tokens and arrives bound", () => {
  const { call, fails, tokens, friendship, gifts } = world("ann", "bob");
  tokens("ann", 20);

  const sent = call("ann", "giftSendRpc", { userId: "bob", gemId: "runedancer_hit_2" });

  assert.strictEqual(sent.gemId, "runedancer_hit_2");
  assert.strictEqual(sent.mentor, false);
  assert.strictEqual(sent.left, rules.dailyLimit - 1);
  assert.strictEqual(friendship("ann"), 20 - rules.cost);
  assert.strictEqual(call("ann", "coopWalletRpc").ledger[0].source, "gift_sent");
  const letter = gifts("bob")[0];
  assert.strictEqual(letter.gem, "runedancer_hit_2");
  assert.strictEqual(letter.title, "ann");
  assert.strictEqual(call("bob", "mailClaimRpc", { id: letter.id }).letter.gem, "runedancer_hit_2");
  assert.deepStrictEqual(JSON.parse(JSON.stringify(call("ann", "giftStatusRpc"))), { left: rules.dailyLimit - 1, dailyLimit: rules.dailyLimit, perFriendDaily: rules.perFriendDaily, cost: rules.cost });
  fails("ann", "giftSendRpc", { userId: "bob", gemId: "minstrel_aura_2" }, "already gifted this friend today");
});

test("path gems, strangers to the rules and empty wallets are refused", () => {
  const { nk, call, fails, tokens, friendship } = world("ann", "bob", "cid");
  fails("ann", "giftSendRpc", { userId: "bob", gemId: "runedancer_hit_2" }, "not enough friendship");
  tokens("ann", 20);
  assert.ok(rules.giftable.indexOf("runedancer_path_1") < 0, "gems of a path are not giftable");
  fails("ann", "giftSendRpc", { userId: "bob", gemId: "runedancer_path_1" }, "this gem cannot be gifted");
  fails("ann", "giftSendRpc", { userId: "bob", gemId: "no_such_gem" }, "this gem cannot be gifted");
  fails("ann", "giftSendRpc", { userId: "ann", gemId: "runedancer_hit_2" }, "bad request");
  fails("ann", "giftSendRpc", { userId: "nobody", gemId: "runedancer_hit_2" }, "bad request");
  call("cid", "partyBlockRpc", { userId: "ann" });
  fails("ann", "giftSendRpc", { userId: "cid", gemId: "runedancer_hit_2" }, "this player does not take gifts from you");
  assert.strictEqual(friendship("ann"), 20, "a refused gift costs nothing");
  assert.strictEqual(nk.storageList("bob", "mail").objects.length, 0);
});

test("a sender has a daily limit and a friend can take only so many gifts a day", () => {
  const people = ["ann", "bob", "cid", "dan", "eve", "fay", "gus", "hal"];
  const { call, fails, tokens } = world(...people);
  for (const name of people)
    tokens(name, 100);
  for (let i = 0; i < rules.dailyLimit; i++)
    call("ann", "giftSendRpc", { userId: people[1 + i], gemId: "runedancer_hit_2" });
  fails("ann", "giftSendRpc", { userId: "hal", gemId: "runedancer_hit_2" }, "no gifts left today");
  assert.strictEqual(call("ann", "giftStatusRpc").left, 0);

  const givers = people.slice(1).filter((name) => name !== "hal");
  let given = 0;
  for (const giver of givers) {
    if (given === rules.receiveDailyLimit)
      break;
    call(giver, "giftSendRpc", { userId: "hal", gemId: "minstrel_aura_2" });
    given++;
  }
  assert.strictEqual(given, rules.receiveDailyLimit);
  fails(givers[given], "giftSendRpc", { userId: "hal", gemId: "minstrel_aura_2" }, "this friend got enough gifts today");
});

test("a mentor gifts the student without the daily limit", () => {
  const { nk, call, tokens, gifts } = world("ann", "bob");
  tokens("ann", 100);
  nk.storageWrite([{ collection: "mentor", key: "profile", userId: "ann", value: { ready: true, studentId: "bob", studentUntil: Date.now() + day, mentorId: "", seconds: 0, level: 0, marks: [], perks: [], coopPoints: 0, graduates: 0 } }]);

  for (let i = 0; i < rules.dailyLimit + 2; i++)
    assert.strictEqual(call("ann", "giftSendRpc", { userId: "bob", gemId: "runedancer_hit_2" }).mentor, true);

  assert.strictEqual(gifts("bob").length, rules.dailyLimit + 2);
  assert.strictEqual(call("ann", "giftStatusRpc").left, rules.dailyLimit, "mentor gifts do not use up the limit");
});
