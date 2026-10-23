const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");

const party = loadModule();
const vectors = readVectors("party-codec.json");

function hex(buffer) {
  return Array.from(new Uint8Array(buffer), (b) => b.toString(16).padStart(2, "0")).join("");
}

function bytes(text) {
  const out = new Uint8Array(text.length / 2);
  for (let i = 0; i < out.length; i++)
    out[i] = parseInt(text.substr(i * 2, 2), 16);
  return out.buffer;
}

function scriptedEncounter() {
  const encounter = new party.PartyEncounter(party.PartyServerRules, vectors.seed);
  const numbers = ["0", "0", "0", "0"];
  for (const line of vectors.script) {
    const [op, a, b, c, d] = line.split(" ");
    if (op === "join") numbers[encounter.join(a)] = a;
    else if (op === "class") encounter.setHeroClass(Number(a), Number(b));
    else if (op === "damage") encounter.applyDamage(Number(a), Number(b), Number(c));
    else if (op === "effect") encounter.applyEnemyEffect(Number(a), Number(b), Number(c), d === undefined ? -1 : Number(d));
    else if (op === "advance") for (let i = 0; i < Number(a); i++) encounter.advance();
    else if (op === "peaceful") encounter.setPeaceful(Number(a), b === "true");
  }
  return { encounter, numbers };
}

test("encounter state and roster encode like the core", () => {
  const { encounter, numbers } = scriptedEncounter();
  assert.strictEqual(hex(party.partyEncodeEncounterState(encounter, vectors.encode.encounterState.sequence)), vectors.encode.encounterState.hex);
  assert.strictEqual(hex(party.partyEncodeRoster(encounter, vectors.encode.roster.version, numbers)), vectors.encode.roster.hex);
});

test("kill and moment encode like the core", () => {
  const killed = vectors.encode.enemyKilled;
  assert.strictEqual(hex(party.partyEncodeEnemyKilled({ kind: 2, tick: killed.tick, enemyId: killed.enemyId, enemyKind: killed.enemyKind, shares: killed.shares })), killed.hex);
  const moment = vectors.encode.moment;
  assert.strictEqual(hex(party.partyEncodeMoment({ kind: moment.kind, tick: moment.tick, enemyId: moment.enemyId, enemyKind: moment.enemyKind, shares: [0, 0, 0, 0] }, moment.bossTicksLeft)), moment.hex);
});

test("client messages decode like the core", () => {
  const { hello, damageBatch, castEffect, presence } = vectors.decode;
  const strip = (o) => { const copy = Object.assign({}, o); delete copy.hex; return copy; };
  const plain = (o) => JSON.parse(JSON.stringify(o));
  assert.deepStrictEqual(plain(party.partyDecodeHello(bytes(hello.hex))), strip(hello));
  assert.deepStrictEqual(plain(party.partyDecodeDamageBatch(bytes(damageBatch.hex))), strip(damageBatch));
  assert.deepStrictEqual(plain(party.partyDecodeCastEffect(bytes(castEffect.hex))), strip(castEffect));
  assert.deepStrictEqual(plain(party.partyDecodePresence(bytes(presence.hex))), strip(presence));
});

test("truncated and foreign packets are refused", () => {
  const hello = vectors.decode.hello.hex;
  assert.strictEqual(party.partyDecodeHello(bytes(hello.slice(0, -2))), null);
  assert.strictEqual(party.partyDecodeHello(bytes(vectors.decode.presence.hex)), null);
  assert.strictEqual(party.partyKindOf(bytes("ff00")), 0);
});

test("offers are relayed with the sender slot", () => {
  const offer = vectors.decode.offerRelay;
  assert.strictEqual(hex(party.partyRelayOffer(bytes(offer.hex), offer.slot)), offer.relayedHex);
});
