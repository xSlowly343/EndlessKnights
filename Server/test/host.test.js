const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");

const party = loadModule();

function batch(sequence, enemyId, keys, hits) {
  const view = new DataView(new ArrayBuffer(57));
  view.setUint8(0, 2);
  view.setUint32(1, sequence, true);
  view.setUint32(5, enemyId, true);
  view.setFloat64(9, keys, true);
  view.setInt32(49, hits, true);
  return view.buffer;
}

function kinds(host) {
  const sent = host.outbox.map((packet) => new DataView(packet.data).getUint8(0));
  host.outbox.length = 0;
  return sent;
}

test("two members kill the shared enemy through batches", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  assert.strictEqual(host.join("user-a", "11"), 0);
  assert.strictEqual(host.join("user-b", "12"), 1);
  host.tick();
  assert.ok(kinds(host).includes(8));
  assert.strictEqual(host.encounter.barFull, 1);
  host.receive("user-a", batch(1, 1, 1, 3));
  host.tick();
  assert.strictEqual(host.kills, 1);
  assert.ok(kinds(host).includes(6));
  assert.strictEqual(host.encounter.barFull, 2);
  host.receive("user-a", batch(2, 2, 0.5, 3));
  host.receive("user-b", batch(1, 2, 1.0, 3));
  host.receive("user-b", batch(1, 2, 9.0, 3));
  host.tick();
  assert.strictEqual(host.kills, 1);
  host.receive("user-a", batch(3, 2, 1.1, 3));
  host.tick();
  assert.strictEqual(host.kills, 2);
  assert.ok(Math.abs(host.acceptedShares - 3) < 1e-12);
});

test("a flooding member is cut at the message cap", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  const enemy = host.encounter.enemyId;
  for (let i = 1; i <= party.PartyServerRules.maxMessagesPerSecond + 5; i++)
    host.receive("user-a", batch(i, enemy, i * 0.001, 1));
  assert.ok(Math.abs(host.acceptedShares - party.PartyServerRules.maxMessagesPerSecond * 0.001) < 1e-9);
});

test("a full party refuses the fifth member and offers skip the sender", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  for (let i = 0; i < 4; i++)
    host.join("user-" + i, String(100 + i));
  assert.strictEqual(host.join("user-4", "104"), -1);
  host.tick();
  kinds(host);
  const offer = new Uint8Array(74);
  offer[0] = 9;
  host.receive("user-2", offer.buffer);
  assert.strictEqual(host.outbox.length, 1);
  assert.deepStrictEqual(Array.from(host.outbox[0].to), [0, 1, 3]);
  assert.strictEqual(new Uint8Array(host.outbox[0].data)[1], 2);
});

test("the state tells every member how much each slot hit since the last state", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  for (let i = 0; i < 12; i++)
    host.tick();
  const batch = new Uint8Array(57);
  const view = new DataView(batch.buffer);
  batch[0] = 2;
  view.setUint32(1, 1, true);
  view.setUint32(5, host.encounter.enemyId, true);
  view.setFloat64(9, 0.2, true);
  view.setInt32(49, 1, true);
  host.receive("user-b", batch.buffer);
  host.outbox.length = 0;
  let state = null;
  for (let i = 0; i < 10 && !state; i++) {
    host.tick();
    state = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5) || null;
  }
  assert.ok(state, "a state goes out");
  const data = new DataView(state.data);
  const recent = (slot) => data.getFloat32(state.data.byteLength - 16 + slot * 4, true);
  assert.ok(Math.abs(recent(1) - 0.2) < 1e-6, "slot 1 hit 0.2 of the bar");
  assert.strictEqual(recent(0), 0);
  host.outbox.length = 0;
  let next = null;
  for (let i = 0; i < 10 && !next; i++) {
    host.tick();
    next = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5) || null;
  }
  assert.strictEqual(new DataView(next.data).getFloat32(next.data.byteLength - 12, true), 0, "the sum starts again after each state");
});

test("reactions skip the sender and wait for the cooldown", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  for (let i = 0; i < 3; i++)
    host.join("user-" + i, String(100 + i));
  host.tick();
  host.outbox.length = 0;
  const reaction = new Uint8Array([10, 0, 1]);
  host.receive("user-1", reaction.buffer);
  assert.strictEqual(host.outbox.length, 1);
  assert.deepStrictEqual(Array.from(host.outbox[0].to), [0, 2]);
  assert.deepStrictEqual(Array.from(new Uint8Array(host.outbox[0].data)), [10, 1, 1]);
  host.outbox.length = 0;
  host.receive("user-1", reaction.buffer);
  assert.strictEqual(host.outbox.length, 0);
  host.receive("user-2", new Uint8Array([10, 0, party.PartyServerRules.reactionCount]).buffer);
  assert.strictEqual(host.outbox.length, 0);
  for (let i = 0; i < party.PartyServerRules.reactionCooldownSeconds * 10; i++)
    host.tick();
  host.outbox.length = 0;
  host.receive("user-1", reaction.buffer);
  assert.strictEqual(host.outbox.length, 1);
});

function joinedHost() {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.tick();
  host.outbox.length = 0;
  return host;
}

test("forged damage is cut to the bar rate and flagged", () => {
  const host = joinedHost();
  const rules = party.PartyServerRules;
  for (let i = 0; i < 20; i++) {
    host.receive("user-a", batch(100 + i, host.encounter.enemyId, 1e6, 1));
    host.tick();
  }
  assert.ok(host.kills > 0);
  assert.ok(host.kills <= Math.ceil(rules.burstBars + rules.barsPerSecond * 2), "kills " + host.kills);
  assert.ok(host.flags[0] > 0);
  assert.ok(host.cutShares > 0);
});

test("one strong hit with overkill is not flagged", () => {
  const host = joinedHost();
  host.receive("user-a", batch(1, host.encounter.enemyId, 50, 1));
  host.tick();
  assert.strictEqual(host.kills, 1);
  assert.strictEqual(host.flags[0], 0);
  assert.strictEqual(host.cutShares, 0);
});

test("more hits than ticks are flagged and reports come every flagsBeforeReport flags", () => {
  const host = joinedHost();
  const every = party.PartyServerRules.flagsBeforeReport;
  for (let i = 0; i < every; i++)
    host.receive("user-a", batch(1 + i, host.encounter.enemyId, 0.001, 1000));
  assert.strictEqual(host.flags[0], every);
  assert.deepStrictEqual(Array.from(host.reports), [0]);
});

test("the reward mask goes out with every kill", () => {
  const host = joinedHost();
  host.receive("user-a", batch(1, host.encounter.enemyId, 5, 1));
  host.tick();
  const killed = host.outbox.map((packet) => new Uint8Array(packet.data)).find((bytes) => bytes[0] === 6);
  assert.ok(killed);
  assert.strictEqual(killed[1 + 8 + 4 + 1 + 4 * 8], 1);
});

