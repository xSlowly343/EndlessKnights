const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rift = party.RiftServerRules;
const window = party.riftNext(rift, Date.now() + 8 * 24 * 3600 * 1000);
const opened = window.startUnixMs + 3600 * 1000;
const riftId = Math.floor(opened / 1000);
const end = party.riftPartyEnd(riftId);
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(party[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function partyOf(call, leader, ...members) {
  const view = call(leader, "partyCreateRpc", { title: "night_shift" });
  for (const member of members)
    call(member, "partyJoinRpc", { code: view.code });
  return view.id;
}

test("the board ranks parties by depth and then by who got there first", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan", "eve");
  const first = partyOf(call, "ann", "bob");
  const second = partyOf(call, "cid", "dan");
  const third = partyOf(call, "eve");
  party.riftBoardAdd(nk, riftId, first, "night_shift", 0, ["ann", "bob"], opened);
  party.riftBoardAdd(nk, riftId, second, "night_shift", 12, ["cid", "dan"], opened + 1000);
  party.riftBoardAdd(nk, riftId, first, "night_shift", 12, ["ann", "bob"], opened + 2000);
  party.riftBoardAdd(nk, riftId, third, "night_shift", 3, ["eve"], opened + 3000);

  const board = call("ann", "riftBoardRpc", { riftId: riftId });

  assert.strictEqual(board.parties, 3);
  assert.deepStrictEqual(plain(board.top.map((row) => [row.place, row.depth, row.yours])), [[1, 13, false], [2, 13, true], [3, 4, false]]);
  assert.strictEqual(board.own.place, 2);
  assert.strictEqual(board.own.reward.shards, rift.boardPlaces[1].shards);
  assert.strictEqual(board.endUnixMs, end);
  assert.strictEqual(board.settled, false);
});

test("after the rift every member gets the reward of the party place once", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan");
  const first = partyOf(call, "ann", "bob");
  const second = partyOf(call, "cid");
  party.riftBoardAdd(nk, riftId, first, "night_shift", 30, ["ann", "bob"], opened);
  party.riftBoardAdd(nk, riftId, second, "night_shift", 10, ["cid", "dan"], opened);
  call("dan", "partyCreateRpc", {});

  party.riftSettleDue(nk, logger, end);
  assert.deepStrictEqual(plain(call("ann", "riftRewardsRpc").rewards), [], "nothing before the last update after the end");

  party.riftSettleDue(nk, logger, end + rift.boardUpdateMs);
  assert.strictEqual(party.riftBoardAdd(nk, riftId, second, "night_shift", 100, ["cid"], end + rift.boardUpdateMs), false, "a late update does not change a settled board");
  const ann = call("ann", "riftRewardsRpc").rewards;
  assert.strictEqual(ann.length, 1);
  assert.strictEqual(ann[0].place, 1);
  assert.strictEqual(ann[0].of, 2);
  assert.strictEqual(ann[0].depth, 31);
  assert.strictEqual(ann[0].shards, rift.boardPlaces[0].shards);
  assert.strictEqual(call("bob", "riftRewardsRpc").rewards[0].place, 1);
  const dan = call("dan", "riftRewardsRpc").rewards;
  assert.strictEqual(dan[0].place, 2, "a member who fought in the rift and left still gets the reward");
  assert.strictEqual(dan[0].ingots, rift.boardPlaces[1].ingots);
  assert.deepStrictEqual(plain(call("ann", "riftRewardsRpc").rewards), [], "a reward is claimed once");
  party.riftSettleDue(nk, logger, end + 2 * rift.boardUpdateMs);
  assert.deepStrictEqual(plain(call("ann", "riftRewardsRpc").rewards), [], "settling again pays nothing twice");
});

test("everyone past the listed places gets the rest tier", () => {
  assert.strictEqual(party.riftRewardFor(1).shards, rift.boardPlaces[0].shards);
  assert.strictEqual(party.riftRewardFor(100000).upTo, 0);
});

test("the match writes its depth to the board on entry, every update and on the way home", () => {
  const { nk, call } = world("ann", "bob");
  const partyId = partyOf(call, "ann", "bob");
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("ann", "11");
  host.join("bob", "12");
  const state = { host: host, partyId: partyId, presences: {}, pendingNumbers: {}, emptyTicks: 0, chronicleTicks: 0, boardRiftId: 0, boardTicks: 0 };
  host.encounter.switchWorld(1, riftId);
  party.partyTrackRift(nk, logger, state, opened);
  assert.strictEqual(call("ann", "riftBoardRpc", { riftId: riftId }).own.depth, 1, "the party is on the board as soon as it enters");

  for (let boss = 0; boss < 2; boss++) {
    while (!host.encounter.fightingBoss) {
      host.encounter.reportInput(0);
      host.encounter.applyDamage(0, host.encounter.enemyId, host.encounter.barLeft + 1);
      host.tick(opened);
    }
    host.encounter.reportInput(0);
    host.encounter.applyDamage(0, host.encounter.enemyId, host.encounter.barLeft / host.encounter.damageTakenMultiplier() + 1e-9);
    host.tick(opened);
  }
  assert.strictEqual(host.riftCleared, 2);
  const updateTicks = rift.boardUpdateMs / 100;
  for (let i = 0; i < updateTicks; i++)
    party.partyTrackRift(nk, logger, state, opened);
  assert.strictEqual(call("ann", "riftBoardRpc", { riftId: riftId }).own.depth, 3, "two bosses later the party is on the third stage");
  assert.strictEqual(host.riftCleared, 0);
});
