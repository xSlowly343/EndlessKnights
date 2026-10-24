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
