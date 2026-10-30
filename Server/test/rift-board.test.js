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
const system = "00000000-0000-0000-0000-000000000000";

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

test("after the rift every member gets a letter with the reward of the party place, claimed once", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan");
  const first = partyOf(call, "ann", "bob");
  const second = partyOf(call, "cid");
  party.riftBoardAdd(nk, riftId, first, "night_shift", 30, ["ann", "bob"], opened);
  party.riftBoardAdd(nk, riftId, second, "night_shift", 10, ["cid", "dan"], opened);
  call("dan", "partyCreateRpc", {});
  const letters = (user) => call(user, "mailListRpc");

  party.riftSettleDue(nk, logger, end);
  assert.strictEqual(letters("ann").unread, 0, "no letter before the last update after the end");

  party.riftSettleDue(nk, logger, end + rift.boardUpdateMs);
  assert.strictEqual(party.riftBoardAdd(nk, riftId, second, "night_shift", 100, ["cid"], end + rift.boardUpdateMs), 0, "a late update does not change a settled board");
  const mail = letters("ann");
  assert.strictEqual(mail.unread, 1);
  const letter = mail.letters[0];
  assert.strictEqual(letter.id, "rift-" + riftId);
  assert.strictEqual(letter.kind, "rift");
  assert.strictEqual(letter.place, 1);
  assert.strictEqual(letter.of, 2);
  assert.strictEqual(letter.depth, 31);
  assert.strictEqual(letter.reward.shards, rift.boardPlaces[0].shards);
  assert.strictEqual(letter.reward.friendship, rift.boardPlaces[0].friendship);
  assert.strictEqual(letter.claimed, false);
  assert.strictEqual(letters("bob").letters[0].place, 1);
  const dan = letters("dan").letters[0];
  assert.strictEqual(dan.place, 2, "a member who fought in the rift and left still gets the letter");
  assert.strictEqual(dan.reward.ingots, rift.boardPlaces[1].ingots);

  const claimed = call("ann", "mailClaimRpc", { id: letter.id }).letter;
  assert.strictEqual(claimed.claimed, true);
  assert.strictEqual(claimed.reward.shards, rift.boardPlaces[0].shards);
  assert.throws(() => call("ann", "mailClaimRpc", { id: letter.id }), /already claimed/);
  assert.throws(() => call("ann", "mailClaimRpc", { id: "rift-1" }), /no such letter/);
  assert.strictEqual(letters("bob").unread, 1, "each member has a letter of their own");
  const after = letters("ann");
  assert.strictEqual(after.unread, 0);
  assert.strictEqual(after.letters[0].claimed, true, "a claimed letter stays in the list");
  party.riftSettleDue(nk, logger, end + 2 * rift.boardUpdateMs);
  assert.strictEqual(letters("ann").unread, 0, "settling again sends nothing twice");
});

test("friendship tokens of the place go to the coop wallet at the settlement, once", () => {
  const { nk, call } = world("ann", "bob", "cid");
  const first = partyOf(call, "ann", "bob");
  const second = partyOf(call, "cid");
  party.riftBoardAdd(nk, riftId, first, "night_shift", 30, ["ann", "bob"], opened);
  party.riftBoardAdd(nk, riftId, second, "night_shift", 10, ["cid"], opened);
  const friendship = (user) => call(user, "coopWalletRpc").balances.friendship;

  party.riftSettleDue(nk, logger, end);
  assert.strictEqual(friendship("ann"), 0, "nothing before the settlement");

  party.riftSettleDue(nk, logger, end + rift.boardUpdateMs);
  assert.ok(rift.boardPlaces[0].friendship > rift.boardPlaces[1].friendship);
  assert.strictEqual(friendship("ann"), rift.boardPlaces[0].friendship);
  assert.strictEqual(friendship("bob"), rift.boardPlaces[0].friendship);
  assert.strictEqual(friendship("cid"), rift.boardPlaces[1].friendship);
  const ledger = call("ann", "coopWalletRpc").ledger;
  assert.strictEqual(ledger[0].source, "rift_place");

  const board = nk.storageRead([{ collection: "rift_board", key: String(riftId), userId: system }])[0];
  nk.storageWrite([{ collection: "rift_board", key: String(riftId), userId: system, value: Object.assign({}, board.value, { settled: false }), version: board.version }]);
  party.riftUpdate(nk, "pending", () => ({ rifts: [] }), (index) => { index.rifts.push(riftId); return true; });
  party.riftSettleDue(nk, logger, end + 2 * rift.boardUpdateMs);
  assert.strictEqual(friendship("ann"), rift.boardPlaces[0].friendship, "a settlement that runs again after a failed write pays no second time");
  call("ann", "mailClaimRpc", { id: "rift-" + riftId });
  assert.strictEqual(friendship("ann"), rift.boardPlaces[0].friendship, "claiming the letter does not pay the tokens again");
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

test("every 25 stages of the party sends a milestone letter once, with friendship and on deep stages a rare gem of the rift", () => {
  const nk = new FakeNakama();
  ["ann", "bob"].forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const rules = party.RiftServerRules;
  const every = rules.milestones.every;
  const riftId = 1792497600;
  assert.strictEqual(party.riftMilestones(nk, logger, riftId, "night_shift", ["ann", "bob"], 0, every - 1, Date.now()), 0);
  assert.strictEqual(party.riftMilestones(nk, logger, riftId, "night_shift", ["ann", "bob"], every - 1, every * 4 + 3, Date.now()), 8);
  assert.strictEqual(party.riftMilestones(nk, logger, riftId, "night_shift", ["ann"], 0, every * 4, Date.now()), 0, "a letter for a stage goes out once");
  const letters = nk.storageList("ann", "mail").objects.map((object) => object.value).filter((letter) => letter.kind === "rift_depth");
  assert.deepStrictEqual(letters.map((letter) => letter.depth).sort((a, b) => a - b), [every, every * 2, every * 3, every * 4]);
  const deep = letters.find((letter) => letter.depth === every * 4);
  assert.ok(party.riftMilestoneGems(rules, riftId).indexOf(deep.gem) >= 0, "the rare gem comes from the pool of the rift");
  assert.strictEqual(letters.find((letter) => letter.depth === every).gem, undefined);
  const wallet = party.walletRead(nk, "ann").value;
  const expected = [1, 2, 3, 4].reduce((sum, n) => sum + party.riftMilestoneStep(rules, n * every).friendship, 0);
  assert.strictEqual(wallet.balances.friendship, expected);
});
