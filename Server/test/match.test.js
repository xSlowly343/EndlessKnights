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

function presence(userId) {
  return { userId: userId, sessionId: "session-" + userId, username: userId, node: "riftwarden1" };
}

function hex(data) {
  return Buffer.from(data).toString("hex");
}

function throughGo(state) {
  return JSON.parse(JSON.stringify(state));
}

class Dispatcher {
  constructor() { this.sent = []; }
  broadcastMessage(opCode, data, presences) {
    assert.strictEqual(opCode, new DataView(data).getUint8(0));
    this.sent.push(hex(data) + ">" + Array.from(presences, (p) => p.userId).join(","));
  }
  matchLabelUpdate() {}
}

function referenceSent(host) {
  const members = host.encounter.members;
  const sent = Array.from(host.outbox, (packet) => hex(packet.data) + ">" + Array.from(packet.to, (slot) => members[slot].playerId).join(","));
  host.outbox.length = 0;
  return sent;
}

const steps = [
  [],
  [["user-a", batch(1, 1, 1, 3)]],
  [],
  [["user-a", batch(2, 2, 0.5, 3)], ["user-b", batch(1, 2, 1.0, 3)]],
  [["user-b", batch(1, 2, 9.0, 3)]],
  [["user-a", batch(3, 2, 1.1, 3)]],
  []
];

test("the match keeps the fight when Nakama passes the state through Go between calls", () => {
  const nk = { storageWrite() {}, matchCreate() { return "match"; } };
  const ctx = { matchId: "match", env: {} };
  const dispatcher = new Dispatcher();
  const reference = new party.PartyHost(party.PartyServerRules, "5");

  let state = throughGo(party.partyMatchInit(ctx, null, nk, { seed: "5" }).state);
  for (const [userId, number] of [["user-a", "11"], ["user-b", "12"]]) {
    const attempt = party.partyMatchJoinAttempt(ctx, null, nk, dispatcher, 0, state, presence(userId), { playerNumber: number });
    assert.strictEqual(attempt.accept, true);
    state = throughGo(party.partyMatchJoin(ctx, null, nk, dispatcher, 0, throughGo(attempt.state), [presence(userId)]).state);
    reference.join(userId, number);
  }

  for (let tick = 0; tick < 40; tick++) {
    const messages = (steps[tick] || []).map(([userId, data]) => ({ sender: presence(userId), opCode: 2, data: data, reliable: true, receiveTimeMs: 0 }));
    dispatcher.sent.length = 0;
    state = throughGo(party.partyMatchLoop(ctx, null, nk, dispatcher, tick, state, messages).state);
    for (const message of messages)
      reference.receive(message.sender.userId, message.data);
    reference.tick();
    assert.deepStrictEqual(dispatcher.sent, referenceSent(reference), "tick " + tick);
  }

  assert.strictEqual(state.host.kills, 2);
  assert.strictEqual(state.host.encounter.tick, reference.encounter.tick);
});

test("a signal to leave frees the slot after the state went through Go", () => {
  const nk = { storageWrite() {}, matchCreate() { return "match"; } };
  const ctx = { matchId: "match", env: {} };
  const dispatcher = new Dispatcher();
  let state = throughGo(party.partyMatchInit(ctx, null, nk, {}).state);
  state = throughGo(party.partyMatchJoinAttempt(ctx, null, nk, dispatcher, 0, state, presence("user-a"), { playerNumber: "11" }).state);
  state = throughGo(party.partyMatchJoin(ctx, null, nk, dispatcher, 0, state, [presence("user-a")]).state);
  assert.strictEqual(state.host.encounter.members[0].joined, true);
  state = throughGo(party.partyMatchSignal(ctx, null, nk, dispatcher, 1, state, "leave:user-a").state);
  assert.strictEqual(state.host.encounter.members[0].joined, false);
});
