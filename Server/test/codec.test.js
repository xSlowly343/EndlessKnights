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
    else if (op === "weaken") encounter.weakenNext(Number(a), Number(b), Number(c));
    else if (op === "guest") numbers[encounter.joinGuest(a, Number(b))] = a;
  }
  return { encounter, numbers };
}

test("encounter state and roster encode like the core", () => {
  const { encounter, numbers } = scriptedEncounter();
  assert.strictEqual(hex(party.partyEncodeEncounterState(encounter, vectors.encode.encounterState.sequence)), vectors.encode.encounterState.hex);
  assert.strictEqual(hex(party.partyEncodeRoster(encounter, vectors.encode.roster.version, numbers)), vectors.encode.roster.hex);
  const busy = vectors.encode.busyState;
  assert.strictEqual(hex(party.partyEncodeEncounterState(encounter, busy.sequence, busy.recent, busy.hits, busy.marks)), busy.hex, "recent shares, hits and marks, hits above 255 are cut");
});

test("kill and moment encode like the core", () => {
  const killed = vectors.encode.enemyKilled;
  assert.strictEqual(hex(party.partyEncodeEnemyKilled({ kind: 2, tick: killed.tick, enemyId: killed.enemyId, enemyKind: killed.enemyKind, shares: killed.shares, rewarded: killed.rewarded, loop: killed.loop, saved: killed.saved })), killed.hex);
  const moment = vectors.encode.moment;
  assert.strictEqual(hex(party.partyEncodeMoment({ kind: moment.kind, tick: moment.tick, enemyId: moment.enemyId, enemyKind: moment.enemyKind, shares: [0, 0, 0, 0] }, moment.bossTicksLeft)), moment.hex);
});

test("client messages decode like the core", () => {
  const { hello, damageBatch, castEffect, repeatCast, weakenCast, endCast, worldRequest, presence } = vectors.decode;
  const strip = (o) => { const copy = Object.assign({}, o); delete copy.hex; return copy; };
  const plain = (o) => JSON.parse(JSON.stringify(o));
  assert.deepStrictEqual(plain(party.partyDecodeHello(bytes(hello.hex))), strip(hello));
  assert.deepStrictEqual(plain(party.partyDecodeDamageBatch(bytes(damageBatch.hex))), strip(damageBatch));
  assert.deepStrictEqual(plain(party.partyDecodeCastEffect(bytes(castEffect.hex))), strip(castEffect));
  assert.deepStrictEqual(plain(party.partyDecodeCastEffect(bytes(repeatCast.hex))), strip(repeatCast));
  assert.deepStrictEqual(plain(party.partyDecodeCastEffect(bytes(weakenCast.hex))), strip(weakenCast));
  assert.deepStrictEqual(plain(party.partyDecodeCastEffect(bytes(endCast.hex))), strip(endCast));
  assert.strictEqual(party.partyKindOf(bytes(vectors.decode.bossRequest.hex)), 16, "a boss request is the kind byte alone");
  assert.strictEqual(party.partyDecodeMarks(bytes(vectors.decode.marks.hex)), vectors.decode.marks.marks);
  assert.deepStrictEqual(plain(party.partyDecodePresence(bytes(presence.hex))), strip(presence));
  assert.deepStrictEqual(plain(party.partyDecodeWorldRequest(bytes(worldRequest.hex))), strip(worldRequest));
});

test("truncated and foreign packets are refused", () => {
  const hello = vectors.decode.hello.hex;
  assert.strictEqual(party.partyDecodeHello(bytes(hello.slice(0, -2))), null);
  assert.strictEqual(party.partyDecodeHello(bytes(vectors.decode.presence.hex)), null);
  assert.strictEqual(party.partyKindOf(bytes("ff00")), 0);
});

test("reactions are relayed with the sender slot, unknown ones are dropped", () => {
  const reaction = vectors.decode.reactionRelay;
  assert.strictEqual(hex(party.partyRelayReaction(bytes(reaction.hex), reaction.slot, reaction.reaction + 1)), reaction.relayedHex);
  assert.strictEqual(party.partyRelayReaction(bytes(reaction.hex), reaction.slot, reaction.reaction), null);
  assert.strictEqual(party.partyRelayReaction(bytes(reaction.hex + "00"), reaction.slot, 16), null);
});

test("a dropped focus decodes and a relay encodes like the core", () => {
  const { focusDropped, focusRelay } = vectors.decode;
  assert.deepStrictEqual(JSON.parse(JSON.stringify(party.partyDecodeFocusRelay(bytes(focusDropped.hex)))), { from: focusDropped.from, to: focusDropped.to, focus: focusDropped.focus });
  assert.strictEqual(hex(party.partyEncodeFocusRelay(focusRelay.from, focusRelay.to, focusRelay.focus)), focusRelay.hex);
  assert.strictEqual(party.partyKindOf(bytes(focusRelay.hex)), 12);
  assert.strictEqual(party.partyDecodeFocusRelay(bytes(focusRelay.hex.slice(0, -2))), null);
});

test("resource for allies is relayed with the sender slot and capped", () => {
  const resource = vectors.decode.allyResource;
  assert.strictEqual(hex(party.partyRelayResource(bytes(resource.hex), resource.slot, 1000)), resource.relayedHex);
  const capped = party.partyRelayResource(bytes(resource.hex), resource.slot, 5);
  assert.strictEqual(new DataView(capped).getFloat32(2, true), 5);
  assert.strictEqual(party.partyRelayResource(bytes(resource.hex.slice(0, -2)), resource.slot, 1000), null);
});

test("synergies on the enemy decode like the core", () => {
  const synergy = vectors.decode.synergy;
  assert.strictEqual(party.partyDecodeSynergy(bytes(synergy.hex), 12), synergy.reactions);
  assert.strictEqual(party.partyDecodeSynergy(bytes(synergy.hex), 1), 1, "reactions the server does not know are cut");
  assert.strictEqual(party.partyDecodeSynergy(bytes(synergy.hex.slice(0, -2)), 12), null);
});

test("a heavy cast notice is relayed with the sender slot", () => {
  const cast = vectors.decode.allyCast;
  assert.strictEqual(hex(party.partyRelayAllyCast(bytes(cast.hex), cast.slot)), cast.relayedHex);
  assert.strictEqual(party.partyRelayAllyCast(bytes(cast.hex + "00"), cast.slot), null);
});

test("offers are relayed with the sender slot", () => {
  const offer = vectors.decode.offerRelay;
  assert.strictEqual(hex(party.partyRelayOffer(bytes(offer.hex), offer.slot)), offer.relayedHex);
  assert.strictEqual(party.partyOfferAuraTicks(bytes(offer.hex)), offer.auraTicks);
});

test("the skill loadout is relayed with the sender slot, a wrong size is dropped", () => {
  const loadout = vectors.decode.loadoutRelay;
  assert.strictEqual(hex(party.partyRelayLoadout(bytes(loadout.hex), loadout.slot)), loadout.relayedHex);
  assert.strictEqual(party.partyRelayLoadout(bytes(loadout.hex.slice(0, -2)), loadout.slot), null);
});
