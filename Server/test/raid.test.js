const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.RaidServerRules;
const day = 86400000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const layer = (raid, id) => raid.layers.find((l) => l.id === id);
const perMember = (id) => rules.layers.find((l) => l.id === id).perMember;

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

test("rules come from raid.json: four layers, each taken down by its own kind of input", () => {
  assert.strictEqual(rules.boss, "devourer");
  assert.deepStrictEqual(plain(rules.layers.map((l) => [l.id, l.input])), [["shield", "skill"], ["armor", "mouse"], ["flesh", "keys"], ["curse", "idle"]]);
  assert.strictEqual(rules.weekStartDay, 1, "the raid week starts on Monday");
});

test("the raid week turns at Monday midnight Moscow time", () => {
  const monday = Date.UTC(2026, 9, 25, 21, 0);
  assert.strictEqual(server.raidWeekOf(monday), server.raidWeekOf(monday + 7 * day - 1));
  assert.notStrictEqual(server.raidWeekOf(monday), server.raidWeekOf(monday - 1));
  assert.strictEqual(server.raidWeekStart(server.raidWeekOf(monday + 3 * day)), monday);
});

test("the boss grows with the clan but never below the minimum size", () => {
  assert.strictEqual(server.raidCreate("c", 1, 2).members, rules.minMembers);
  const big = server.raidCreate("c", 1, 27);
  assert.strictEqual(big.members, 27);
  assert.strictEqual(layer(big, "flesh").hp, perMember("flesh") * 27);
});

test("each layer takes its own input; extra damage into a fallen layer spills weakly into the rest", () => {
  const raid = server.raidCreate("c", 1, rules.minMembers);
  const shieldHp = layer(raid, "shield").hp;
  layer(raid, "shield").left = 10;
  const dealt = server.raidHit(raid, "shield", 410);
  assert.strictEqual(layer(raid, "shield").left, 0);
  const spill = 400 * rules.spillover / 3;
  assert.ok(Math.abs(dealt - (10 + 400 * rules.spillover)) < 1e-9);
  assert.ok(Math.abs(layer(raid, "flesh").left - (layer(raid, "flesh").hp - spill)) < 1e-9);
  assert.ok(shieldHp > 10);
});

test("a push is capped by real time and by the most a player can deal per second", () => {
  const raid = server.raidCreate("c", 1, rules.minMembers);
  const now = 1_800_000_000_000;
  const fair = server.raidApply(raid, "ann", { flesh: 30, armor: 5 }, 15, now);
  assert.strictEqual(fair, 35);
  const greedy = server.raidApply(raid, "ann", { flesh: 1e6 }, 15, now + 15000);
  assert.strictEqual(greedy, rules.maxSharesPerSecond * 15);
  assert.strictEqual(raid.tally.ann.capped, 1);
  const tooSoon = server.raidApply(raid, "ann", { flesh: 1e6 }, 600, now + 16000);
  assert.ok(tooSoon <= rules.maxSharesPerSecond * 6 + 1e-9, "a minute cannot be claimed one second after the last push");
});

test("the boss falls when every layer is down and rewards follow the share of the damage", () => {
  const raid = server.raidCreate("c", 1, rules.minMembers);
  for (const l of raid.layers)
    l.left = l.id === "flesh" ? 30 : 0;
  server.raidApply(raid, "ann", { flesh: 20 }, 15, 1000);
  assert.strictEqual(raid.killedAt, 0);
  server.raidApply(raid, "bob", { flesh: 10 }, 15, 2000);
  assert.strictEqual(raid.killedAt, 2000);
  raid.tally.ann.dealt = 300;
  raid.tally.bob.dealt = 100;
  raid.tally.cid = { dealt: 0, layers: {}, lastPush: 3000, capped: 0 };
  const result = server.raidResult(raid);
  assert.strictEqual(result.killed, true);
  assert.deepStrictEqual(plain(result.places), ["ann", "bob"], "no damage, no place");
  const pool = rules.sealsPerMember * rules.minMembers * (1 + rules.killBonus);
  assert.strictEqual(result.seals.ann, Math.round(pool * 0.75));
  assert.strictEqual(result.seals.bob, Math.max(rules.minSeals, Math.round(pool * 0.25)));
});

test("clan members push into one shared boss and see their place", () => {
  const { call, fails } = world("ann", "bob", "eve");
  clanOf(call, ["ann", "bob"]);
  fails("eve", "raidViewRpc", {}, "not in a clan");
  fails("ann", "raidPushRpc", { flesh: -1, seconds: 15 }, "bad request");
  call("ann", "raidPushRpc", { flesh: 30, armor: 6, seconds: 15 });
  const view = call("bob", "raidPushRpc", { flesh: 10, shield: 2, seconds: 15 });
  assert.strictEqual(view.boss, "devourer");
  assert.strictEqual(view.members, rules.minMembers);
  assert.strictEqual(layer(view, "flesh").left, layer(view, "flesh").hp - 40);
  assert.strictEqual(view.own.place, 2);
  assert.strictEqual(view.own.players, 2);
  assert.ok(Math.abs(view.own.share - 12 / 48) < 1e-9);
  assert.deepStrictEqual(plain(view.top.map((row) => row.name)), ["ann", "bob"]);
  assert.ok(view.endsAt > Date.now());
  assert.strictEqual(view.last, null);
});

test("a new week settles the old raid once: seals go to the wallets and the result is shown", () => {
  const { nk, call } = world("ann", "bob");
  const clan = clanOf(call, ["ann", "bob"]);
  call("ann", "raidPushRpc", { flesh: 30, seconds: 15 });
  call("bob", "raidPushRpc", { flesh: 10, seconds: 15 });
  const stored = nk.storageRead([{ collection: "clan_raid", key: clan.id, userId: server.PartySystemUser }])[0];
  stored.value.week -= 1;
  nk.storageWrite([{ collection: "clan_raid", key: clan.id, userId: server.PartySystemUser, value: stored.value }]);
  const view = call("bob", "raidViewRpc", {});
  assert.strictEqual(view.own.players, 0, "the new week starts clean");
  assert.strictEqual(view.last.killed, false);
  assert.strictEqual(view.last.place, 2);
  assert.strictEqual(view.last.seals, rules.minSeals);
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.seals, call("ann", "raidViewRpc", {}).last.seals);
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.seals, rules.minSeals);
  const old = nk.storageRead([{ collection: "clan_raid_last", key: clan.id, userId: server.PartySystemUser }])[0].value;
  server.raidSettle(nk, logger, Object.assign(plain(stored.value), { week: old.week }), Date.now());
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.seals, rules.minSeals, "a second settlement pays nothing");
});
