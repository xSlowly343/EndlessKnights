const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ChallengeServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const rule = (id) => rules.challenges.find((c) => c.id === id);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

function clanOf(call, names) {
  const clan = call(names[0], "clanCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "clanJoinRpc", { code: clan.code });
  return clan;
}

test("each clan gets its own pair of challenges for the day, the same all day", () => {
  const day = server.challengeDayOf(Date.UTC(2026, 9, 29, 12));
  const picks = server.challengePick("clan-a", day);
  assert.strictEqual(picks.length, rules.perDay);
  assert.strictEqual(new Set(picks).size, picks.length);
  assert.deepStrictEqual(plain(server.challengePick("clan-a", day)), plain(picks));
  const seen = new Set();
  for (let d = day; d < day + 30; d++)
    for (const id of server.challengePick("clan-a", d))
      seen.add(id);
  assert.strictEqual(seen.size, rules.challenges.length, "over a month every challenge comes up");
});

test("targets grow with the clan, counts are capped by the seconds they cover", () => {
  const daily = server.challengeCreate("c", 1, 27);
  for (const id of daily.picks)
    assert.strictEqual(daily.targets[id], rule(id).perMember * 27);
  assert.strictEqual(server.challengeCreate("c", 1, 1).targets[daily.picks[0]], rule(daily.picks[0]).perMember * rules.minMembers);
  const counts = server.challengeCap(server.challengeCounts({ keys: 1e6, clicks: 3, mouseMeters: 2, bosses: 1 }), 15);
  assert.strictEqual(counts.keys, rules.capsPerSecond.keys * 15);
  assert.strictEqual(counts.clicks, 3);
  assert.throws(() => server.challengeCounts({ keys: -1 }), /bad request/);
});

test("a challenge is done once, and seals go to everyone who helped", () => {
  const { nk } = world("ann", "bob", "cid");
  const daily = server.challengeCreate("c", 1, 5);
  daily.picks = ["keys", "bosses"];
  daily.targets = { keys: 100, bosses: 10 };
  daily.progress = { keys: 0, bosses: 0 };
  assert.deepStrictEqual(plain(server.challengeAdd(daily, "ann", { keys: 60 }, 1000)), []);
  assert.deepStrictEqual(plain(server.challengeAdd(daily, "bob", { keys: 50, bosses: 1 }, 2000)), ["keys"]);
  assert.deepStrictEqual(plain(server.challengeAdd(daily, "cid", { keys: 500 }, 3000)), [], "a finished challenge takes no more");
  assert.strictEqual(daily.done.keys, 2000);
  assert.strictEqual(daily.yours.cid, undefined);
  server.challengeReward(nk, logger, daily, "keys", Date.UTC(2026, 9, 29, 12));
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.seals, rule("keys").seals);
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.seals, rule("keys").seals);
  assert.strictEqual(server.walletRead(nk, "cid").value.balances.seals, 0);
  const view = server.challengeView(daily, "bob");
  assert.deepStrictEqual(plain(view.challenges.map((c) => [c.id, c.progress, c.yours])), [["keys", 100, 50], ["bosses", 1, 1]]);
});

test("the clan packet carries the counts, and clan_daily shows the day", () => {
  const { call, fails } = world("ann", "bob", "eve");
  clanOf(call, ["ann", "bob"]);
  fails("eve", "clanDailyRpc", {}, "not in a clan");
  fails("ann", "raidPushRpc", { flesh: 1, seconds: 15, counts: "many" }, "bad request");
  const pushed = call("ann", "raidPushRpc", { flesh: 1, seconds: 15, counts: { keys: 90, clicks: 20, mouseMeters: 12.5, bosses: 2 } });
  assert.strictEqual(pushed.daily.challenges.length, rules.perDay);
  const daily = call("bob", "clanDailyRpc", {});
  assert.deepStrictEqual(plain(daily.challenges.map((c) => c.id)), plain(pushed.daily.challenges.map((c) => c.id)));
  const expected = { keys: 90, clicks: 20, mouseMeters: 12.5, bosses: 2 };
  for (const challenge of daily.challenges) {
    assert.strictEqual(challenge.progress, expected[challenge.counter]);
    assert.strictEqual(challenge.yours, 0, "bob has not helped yet");
  }
  assert.ok(daily.endsAt > Date.now());
});

test("a full challenge pays seals into the wallets of the clan", () => {
  const { nk, call } = world("ann", "bob");
  const clan = clanOf(call, ["ann", "bob"]);
  call("ann", "clanDailyRpc", {});
  const stored = nk.storageRead([{ collection: "clan_daily", key: clan.id, userId: server.PartySystemUser }])[0];
  const first = stored.value.picks[0];
  stored.value.targets[first] = 1;
  nk.storageWrite([{ collection: "clan_daily", key: clan.id, userId: server.PartySystemUser, value: stored.value }]);
  const counts = { keys: 1, clicks: 1, mouseMeters: 1, bosses: 1 };
  const view = call("bob", "raidPushRpc", { seconds: 15, counts: counts });
  assert.ok(view.daily.challenges.find((c) => c.id === first).done > 0);
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.seals, rule(first).seals);
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.seals, 0, "ann did not help");
});
