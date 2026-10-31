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
  const recent = (slot) => data.getFloat32(state.data.byteLength - 96 + slot * 4, true);
  const hits = (slot) => data.getUint8(state.data.byteLength - 65 + slot);
  assert.ok(Math.abs(recent(1) - 0.2) < 1e-6, "slot 1 hit 0.2 of the bar");
  assert.strictEqual(recent(0), 0);
  assert.strictEqual(hits(1), 1, "slot 1 landed one hit");
  assert.strictEqual(hits(0), 0);
  host.outbox.length = 0;
  let next = null;
  for (let i = 0; i < 10 && !next; i++) {
    host.tick();
    next = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5) || null;
  }
  assert.strictEqual(new DataView(next.data).getFloat32(next.data.byteLength - 92, true), 0, "the sum starts again after each state");
});

test("the state tells every member the marks of each slot", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  for (let i = 0; i < 12; i++)
    host.tick();
  const marks = new Uint8Array(5);
  marks[0] = 17;
  new DataView(marks.buffer).setUint32(1, 0x21, true);
  host.receive("user-b", marks.buffer);
  host.outbox.length = 0;
  let state = null;
  for (let i = 0; i < 10 && !state; i++) {
    host.tick();
    state = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5) || null;
  }
  const data = new DataView(state.data);
  const markOf = (slot) => data.getUint32(state.data.byteLength - 28 + slot * 4, true);
  assert.strictEqual(markOf(1), 0x21, "slot 1 put two marks");
  assert.strictEqual(markOf(0), 0);
  host.leave("user-b");
  host.outbox.length = 0;
  state = null;
  for (let i = 0; i < 10 && !state; i++) {
    host.tick();
    state = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5) || null;
  }
  assert.strictEqual(new DataView(state.data).getUint32(state.data.byteLength - 24, true), 0, "a member who left has no marks");
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



function dropped(focus) {
  const view = new DataView(new ArrayBuffer(7));
  view.setUint8(0, 12);
  view.setUint8(1, 255);
  view.setUint8(2, 255);
  view.setFloat32(3, focus, true);
  return view.buffer;
}

function relays(host) {
  const sent = host.outbox.filter((packet) => new DataView(packet.data).getUint8(0) === 12).map((packet) => Object.assign(party.partyDecodeFocusRelay(packet.data), { to_slots: packet.to }));
  host.outbox.length = 0;
  return sent;
}

test("a dropped focus goes to the member who typed last, once per cooldown", () => {
  const rules = party.PartyServerRules;
  const host = new party.PartyHost(rules, "5");
  for (let i = 0; i < 3; i++)
    host.join("user-" + i, String(100 + i));
  host.tick();
  host.outbox.length = 0;
  host.receive("user-1", batch(1, host.encounter.enemyId, 0.01, 2));
  host.tick();
  host.receive("user-2", batch(1, host.encounter.enemyId, 0.01, 2));
  host.receive("user-0", dropped(1.5));
  const first = relays(host);
  assert.strictEqual(first.length, 1);
  assert.strictEqual(first[0].from, 0);
  assert.strictEqual(first[0].to, 2, "the latest typer gets it");
  assert.strictEqual(first[0].focus, Math.min(rules.relayMaxBonus, 1.5 * rules.relayShare));
  assert.deepStrictEqual(JSON.parse(JSON.stringify(first[0].to_slots)), [0, 1, 2], "everyone sees the relay");
  assert.strictEqual(host.relays, 1);

  host.receive("user-0", dropped(1.5));
  assert.strictEqual(relays(host).length, 0, "the sender waits for the cooldown");
  for (let i = 0; i < rules.relayCooldownSeconds * 10; i++)
    host.tick();
  host.outbox.length = 0;
  host.receive("user-0", batch(2, host.encounter.enemyId, 0.01, 2));
  host.receive("user-0", dropped(9));
  assert.strictEqual(relays(host).length, 0, "nobody typed for the last seconds");
  host.receive("user-1", batch(2, host.encounter.enemyId, 0.02, 2));
  host.receive("user-0", dropped(9));

  const capped = relays(host);
  assert.strictEqual(capped[0].to, 1);
  assert.strictEqual(capped[0].focus, rules.relayMaxBonus, "the bonus stops at maxBonus");
  host.receive("user-1", dropped(-1));
  host.receive("user-1", dropped(NaN));
  assert.strictEqual(relays(host).length, 0);
  assert.strictEqual(host.relays, 2);
});

test("resource for allies goes to the others, capped and once per cooldown", () => {
  const rules = party.PartyServerRules;
  const host = new party.PartyHost(rules, "5");
  for (let i = 0; i < 3; i++)
    host.join("user-" + i, String(100 + i));
  host.tick();
  host.outbox.length = 0;
  const gift = (amount) => {
    const view = new DataView(new ArrayBuffer(6));
    view.setUint8(0, 13);
    view.setFloat32(2, amount, true);
    return view.buffer;
  };
  const sent = () => {
    const out = host.outbox.filter((packet) => new DataView(packet.data).getUint8(0) === 13)
      .map((packet) => ({ slot: new DataView(packet.data).getUint8(1), amount: new DataView(packet.data).getFloat32(2, true), to: JSON.parse(JSON.stringify(packet.to)) }));
    host.outbox.length = 0;
    return out;
  };
  host.receive("user-1", gift(500));
  const first = sent();
  assert.deepStrictEqual(JSON.parse(JSON.stringify(first)), [{ slot: 1, amount: rules.allyResourceMax, to: [0, 2] }]);
  host.receive("user-1", gift(20));
  assert.strictEqual(sent().length, 0, "the sender waits for the cooldown");
  for (let i = 0; i < rules.allyResourceCooldownSeconds * 10; i++)
    host.tick();
  host.outbox.length = 0;
  host.receive("user-1", gift(-3));
  assert.strictEqual(sent().length, 0);
  host.receive("user-1", gift(20));
  assert.strictEqual(sent()[0].amount, 20);
});

test("a weaken cast makes the next enemies of the party come weaker", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.tick();
  const view = new DataView(new ArrayBuffer(56));
  view.setUint8(0, 3);
  view.setInt32(1, 31, true);
  view.setUint8(5, 1);
  view.setFloat64(6, 0.1, true);
  view.setFloat64(14, 4, true);
  view.setUint8(38, 3);
  view.setFloat64(39, 0.25, true);
  view.setFloat64(47, 0.2, true);
  host.receive("user-a", view.buffer);
  assert.strictEqual(host.encounter.weakenedSpawns, 3);
  host.receive("user-a", batch(1, host.encounter.enemyId, 1.5, 1));
  host.outbox.length = 0;
  host.tick();
  assert.strictEqual(host.encounter.weakenedSpawns, 2);
  assert.ok(Math.abs(host.encounter.barLeft - 0.75 * host.encounter.barFull) < 1e-9, "the new enemy comes a quarter weaker");
  assert.ok(Math.abs(host.encounter.damageTakenMultiplier() - 1.1) < 1e-9, "the weakened enemy is vulnerable");
  const state = host.outbox.find((packet) => new Uint8Array(packet.data)[0] === 5);
  assert.ok(state, "the new enemy goes out at once");
  const data = new DataView(state.data);
  assert.strictEqual(data.getUint8(state.data.byteLength - 61), 1, "the state says the enemy is weakened");
  assert.ok(Math.abs(data.getFloat32(state.data.byteLength - 60, true) - 0.2) < 1e-6, "with its gold bonus");
  assert.strictEqual(data.getUint32(state.data.byteLength - 56, true), 1, "the history says the enemy before was killed");
  assert.strictEqual(data.getUint32(state.data.byteLength - 48, true), 1, "and paid to slot 0");
});

test("an aura-bound debuff ends when its owner says the auras are gone", () => {
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.join("user-b", "12");
  host.tick();
  const cast = (end) => {
    const view = new DataView(new ArrayBuffer(56));
    view.setUint8(0, 3);
    view.setInt32(1, 600, true);
    view.setUint8(5, 1);
    view.setFloat64(6, end ? 0 : 0.15, true);
    view.setFloat64(14, end ? 0 : 20, true);
    view.setUint8(55, end ? 1 : 0);
    return view.buffer;
  };
  host.receive("user-a", cast(false));
  assert.ok(Math.abs(host.encounter.damageTakenMultiplier() - 1.15) < 1e-9);
  host.receive("user-b", cast(true));
  assert.ok(Math.abs(host.encounter.damageTakenMultiplier() - 1.15) < 1e-9, "only the owner ends it");
  host.receive("user-a", cast(true));
  assert.strictEqual(host.encounter.damageTakenMultiplier(), 1);
});

test("rift enemies regenerate and come in hordes until the party marks lift them", () => {
  const rules = party.RiftServerRules;
  const start = Date.UTC(2026, 9, 27, 13);
  let riftId = 0;
  for (let day = 0; day < 1400 && riftId === 0; day++) {
    const window = party.riftWindowAt(rules, start + day * 86400000);
    if (!window)
      continue;
    const picked = party.riftModifiersFor(rules, window.id);
    if (picked.indexOf("regeneration") >= 0 && picked.indexOf("horde") >= 0)
      riftId = window.id + 60;
  }
  assert.ok(riftId > 0, "a window with regeneration and a horde");
  const host = new party.PartyHost(party.PartyServerRules, "5");
  host.join("user-a", "11");
  host.tick(riftId * 1000);
  host.encounter.switchWorld(1, riftId);
  host.tick(riftId * 1000);
  const regeneration = rules.enemies[rules.modifiers.indexOf("regeneration")];
  assert.ok(Math.abs(host.encounter.enemyRegen - regeneration.regen) < 1e-12);
  assert.ok(Math.abs(host.encounter.extraStageEnemies - 0.5) < 1e-12);
  const marks = new DataView(new ArrayBuffer(5));
  marks.setUint8(0, 17);
  marks.setUint32(1, regeneration.unless & -regeneration.unless, true);
  host.receive("user-a", marks.buffer);
  host.tick(riftId * 1000);
  assert.strictEqual(host.encounter.enemyRegen, 0, "the right mark stops the regeneration");
  assert.ok(Math.abs(host.encounter.extraStageEnemies - 0.5) < 1e-12);
  host.encounter.switchWorld(0, 0);
  host.tick(riftId * 1000);
  assert.strictEqual(host.encounter.extraStageEnemies, 0);
});
