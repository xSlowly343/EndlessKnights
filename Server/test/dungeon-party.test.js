const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const keyId = 1847201043054892;
const dungeon = 2;

function worldRequest(world, cancel, id) {
  const view = new DataView(new ArrayBuffer(11));
  view.setUint8(0, 11);
  view.setUint8(1, world);
  view.setUint8(2, cancel ? 1 : 0);
  view.setUint32(3, id % 4294967296, true);
  view.setInt32(7, Math.floor(id / 4294967296), true);
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

function launch(nk, state, from, id) {
  state.host.receive(from, worldRequest(dungeon, false, id));
  for (let i = 0; i < party.PartyServerRules.worldSwitchSeconds * 10 + 2; i++)
    step(nk, state, 1000);
}

function killEnemy(encounter) {
  encounter.reportInput(0);
  encounter.applyDamage(0, encounter.enemyId, encounter.barLeft * 2 + 1);
}

test("the dungeon id unpacks the same way as in the core", () => {
  assert.strictEqual(party.partyDungeonStage(keyId), 300);
  assert.strictEqual(party.partyDungeonStages(keyId), 2);
  assert.ok(keyId <= Number.MAX_SAFE_INTEGER);
});

test("only the leader takes the party into a dungeon, and only from home", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  step(nk, state, 1000);
  launch(nk, state, "bob", keyId);
  assert.strictEqual(state.host.encounter.world, 0, "a member cannot launch");
  launch(nk, state, "ann", 0);
  assert.strictEqual(state.host.encounter.world, 0, "a key is required");
  launch(nk, state, "ann", keyId);
  assert.strictEqual(state.host.encounter.world, dungeon);
  assert.strictEqual(state.host.encounter.riftId, keyId);
  assert.strictEqual(state.host.encounter.waveStage(), 301);
  launch(nk, state, "ann", keyId + 1);
  assert.strictEqual(state.host.encounter.riftId, keyId, "no second key over a running dungeon");
});

test("the party returns home after the dungeon's last boss", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  step(nk, state, 1000);
  launch(nk, state, "ann", keyId);
  const encounter = state.host.encounter;
  let bosses = 0;
  for (let i = 0; i < 2000 && encounter.world === dungeon; i++) {
    if (encounter.fightingBoss)
      bosses++;
    killEnemy(encounter);
    step(nk, state, 1000);
  }
  assert.strictEqual(encounter.world, 0);
  assert.strictEqual(encounter.riftId, 0);
  assert.strictEqual(bosses, 2);
  assert.strictEqual(encounter.dungeonStagesCleared, 0);
});

test("a lost boss ends the dungeon instead of sending the party to farm", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  step(nk, state, 1000);
  launch(nk, state, "ann", keyId);
  const encounter = state.host.encounter;
  for (let i = 0; i < 2000 && !encounter.fightingBoss; i++) {
    killEnemy(encounter);
    step(nk, state, 1000);
  }
  assert.strictEqual(encounter.fightingBoss, true);
  for (let i = 0; i < 100000 && encounter.world === dungeon; i++)
    step(nk, state, 1000);
  assert.strictEqual(encounter.world, 0);
  assert.strictEqual(encounter.farming, false);
});

test("a member's keys are relayed to the others under the sender's slot", () => {
  const { nk, call } = world("ann", "bob");
  const state = match(nk, call, "ann", "bob");
  step(nk, state, 1000);
  state.host.outbox.length = 0;
  const view = new DataView(new ArrayBuffer(67));
  view.setUint8(0, 19);
  view.setUint8(1, 3);
  view.setUint8(2, 1);
  view.setUint32(3, keyId % 4294967296, true);
  view.setInt32(7, Math.floor(keyId / 4294967296), true);
  state.host.receive("bob", view.buffer);
  const relayed = state.host.outbox.filter(packet => new DataView(packet.data).getUint8(0) === 19);
  assert.strictEqual(relayed.length, 1);
  assert.strictEqual(new DataView(relayed[0].data).getUint8(1), 1);
  assert.strictEqual(relayed[0].to.join(","), "0");

  state.host.outbox.length = 0;
  state.host.receive("bob", new ArrayBuffer(12));
  view.setUint8(2, 9);
  state.host.receive("bob", view.buffer);
  assert.strictEqual(state.host.outbox.filter(packet => new DataView(packet.data).getUint8(0) === 19).length, 0);
});
