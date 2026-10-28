const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rift = party.RiftServerRules;
const window = party.riftNext(rift, Date.now() + 8 * 24 * 3600 * 1000);
const hour = 3600 * 1000;

function worldRequest(world, cancel) {
  const view = new DataView(new ArrayBuffer(11));
  view.setUint8(0, 11);
  view.setUint8(1, world);
  view.setUint8(2, cancel ? 1 : 0);
  return view.buffer;
}

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(party[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function match(nk, call, leader, member) {
  const view = call(leader, "partyCreateRpc", {});
  call(member, "partyJoinRpc", { code: view.code });
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join(leader, "11");
  host.join(member, "12");
  return { host: host, partyId: view.id, presences: {}, pendingNumbers: {}, emptyTicks: 0, chronicleTicks: 0 };
}

function step(nk, state, now) {
  party.partyHandleWorld(nk, state, now);
  state.host.tick(now);
}

function enter(nk, state, from, now) {
  state.host.receive(from, worldRequest(1, false));
  step(nk, state, now);
  for (let i = 0; i < party.PartyServerRules.worldSwitchSeconds * 10 + 1; i++)
    step(nk, state, now);
}

test("only the leader leads the party into the rift and calls it off", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  const inside = window.startUnixMs + hour;
  state.host.receive("bob", worldRequest(1, false));
  step(nk, state, inside);
  assert.strictEqual(state.host.encounter.switching, false, "a member cannot start");
  state.host.receive("ann", worldRequest(1, false));
  step(nk, state, inside);
  assert.strictEqual(state.host.encounter.switching, true);
  state.host.receive("bob", worldRequest(0, true));
  step(nk, state, inside);
  assert.strictEqual(state.host.encounter.switching, true, "a member cannot call it off");
  state.host.receive("ann", worldRequest(0, true));
  step(nk, state, inside);
  assert.strictEqual(state.host.encounter.switching, false);
});

test("the rift opens only inside the window", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  enter(nk, state, "ann", window.startUnixMs - 1000);
  assert.strictEqual(state.host.encounter.world, 0);
  enter(nk, state, "ann", window.endUnixMs);
  assert.strictEqual(state.host.encounter.world, 0);
});

test("parties entering within the hour share one rift and its end, later ones open a new one", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan", "eve", "fay");
  const first = match(nk, call, "ann", "bob");
  const second = match(nk, call, "cid", "dan");
  const third = match(nk, call, "eve", "fay");
  const opened = window.startUnixMs + 2 * hour;
  enter(nk, first, "ann", opened);
  enter(nk, second, "cid", opened + hour - 1000);
  enter(nk, third, "eve", opened + hour + 1000);
  const ids = [first, second, third].map((state) => state.host.encounter.riftId);
  assert.strictEqual(ids[0], Math.floor(opened / 1000));
  assert.strictEqual(ids[1], ids[0], "the second party is still within the hour");
  assert.notStrictEqual(ids[2], ids[0], "the third party opens a new rift");
  assert.strictEqual(party.riftPartyEnd(ids[0]), opened + rift.partyDurationMs);

  step(nk, second, window.endUnixMs + hour);
  assert.strictEqual(second.host.encounter.world, 1, "the closed window does not end an open rift");
  step(nk, second, party.riftPartyEnd(ids[0]) - 100);
  assert.strictEqual(second.host.encounter.world, 1);
  step(nk, second, party.riftPartyEnd(ids[0]));
  assert.strictEqual(second.host.encounter.world, 0, "the rift end brings the party home");
});

test("after a server restart the new match puts the party back into its rift until the rift ends", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  const now = window.startUnixMs + hour;
  enter(nk, state, "ann", now);
  party.partyTrackRift(nk, logger, state, now);
  const riftId = state.host.encounter.riftId;
  const restart = () => party.partyMatchInit({ env: {} }, logger, nk, { partyId: state.partyId }).state;

  const restored = restart();
  assert.strictEqual(restored.host.encounter.world, 1, "the party is back in the rift");
  assert.strictEqual(restored.host.encounter.riftId, riftId);
  assert.strictEqual(restored.boardRiftId, riftId, "the board goes on with the same rift");
  restored.host.join("ann", "11");
  assert.strictEqual(restored.host.encounter.world, 1);
  assert.strictEqual(restored.host.encounter.started, true);

  state.host.receive("ann", worldRequest(0, false));
  step(nk, state, now);
  for (let i = 0; i < party.PartyServerRules.worldSwitchSeconds * 10 + 1; i++)
    step(nk, state, now);
  party.partyTrackRift(nk, logger, state, now);
  assert.strictEqual(state.host.encounter.world, 0);
  assert.strictEqual(restart().host.encounter.world, 0, "a party that went home stays home after a restart");
});

test("a rift that has ended is not restored", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  const now = window.startUnixMs + hour;
  enter(nk, state, "ann", now);
  party.partyTrackRift(nk, logger, state, now);
  const end = party.riftPartyEnd(state.host.encounter.riftId);
  assert.strictEqual(party.riftPartyOf(nk, state.partyId, end - 1), state.host.encounter.riftId);
  assert.strictEqual(party.riftPartyOf(nk, state.partyId, end), 0);
});
