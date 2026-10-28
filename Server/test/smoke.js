const assert = require("assert");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const RosterKind = 8;
const EncounterStateKind = 5;
const EnemyKilledKind = 6;
const DamageBatchKind = 2;
const firstNumber = "76561197960287930";
const secondNumber = "76561197960287931";

function usage() {
  console.error("usage: node test/smoke.js <https://host> <server key>");
  process.exit(2);
}

async function call(base, path, init) {
  const response = await fetch(base + path, init);
  const text = await response.text();
  assert.ok(response.ok, path + " -> " + response.status + " " + text);
  return text ? JSON.parse(text) : {};
}

async function refused(base, path, init) {
  const response = await fetch(base + path, init);
  const text = await response.text();
  assert.ok(!response.ok, path + " should be refused, got " + response.status + " " + text);
  return text;
}

async function rpcRefused(base, token, id, payload, message) {
  const text = await refused(base, "/v2/rpc/" + id, bearer(token, "POST", JSON.stringify(payload)));
  assert.ok(text.indexOf(message) >= 0, id + " should fail with \"" + message + "\", got " + text);
}

function bearer(token, method, body) {
  return { method: method, headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" }, body: body === undefined ? undefined : JSON.stringify(body) };
}

async function authenticate(base, serverKey) {
  const basic = Buffer.from(serverKey + ":").toString("base64");
  const deviceId = "smoke-" + crypto.randomUUID();
  const session = await call(base, "/v2/account/authenticate/device?create=true", {
    method: "POST",
    headers: { Authorization: "Basic " + basic, "Content-Type": "application/json" },
    body: JSON.stringify({ id: deviceId })
  });
  assert.ok(session.token, "no session token");
  const userId = JSON.parse(Buffer.from(session.token.split(".")[1], "base64url").toString()).uid;
  return { token: session.token, userId: userId };
}

async function rpc(base, token, id, payload) {
  const reply = await call(base, "/v2/rpc/" + id, {
    method: "POST",
    headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
    body: JSON.stringify(JSON.stringify(payload))
  });
  return JSON.parse(reply.payload);
}

class MatchSocket {
  constructor(base, token) {
    this.url = base.replace(/^http/, "ws") + "/ws?lang=en&status=true&token=" + encodeURIComponent(token);
    this.waiters = [];
    this.nextCid = 1;
    this.counts = {};
  }

  open() {
    return new Promise((resolve, reject) => {
      this.socket = new WebSocket(this.url);
      this.socket.onopen = () => resolve();
      this.socket.onerror = (event) => reject(new Error("socket error " + (event.message || "")));
      this.socket.onmessage = (event) => this.receive(JSON.parse(event.data));
    });
  }

  receive(envelope) {
    if (envelope.match_data) {
      const kind = Number(envelope.match_data.op_code);
      this.counts[kind] = (this.counts[kind] || 0) + 1;
      if (kind === EncounterStateKind)
        this.enemy = Buffer.from(envelope.match_data.data, "base64").readUInt32LE(13);
    }
    for (const waiter of this.waiters.slice())
      waiter(envelope);
  }

  waitFor(what, test) {
    return new Promise((resolve, reject) => {
      const waiter = (envelope) => {
        const result = test(envelope);
        if (result === undefined)
          return;
        clearTimeout(timer);
        this.waiters.splice(this.waiters.indexOf(waiter), 1);
        if (result instanceof Error)
          reject(result);
        else
          resolve(result);
      };
      const timer = setTimeout(() => {
        this.waiters.splice(this.waiters.indexOf(waiter), 1);
        reject(new Error("no " + what + " in 10 s"));
      }, 10000);
      this.waiters.push(waiter);
    });
  }

  packet(kind, test) {
    return this.waitFor("packet " + kind, (envelope) => {
      if (!envelope.match_data || Number(envelope.match_data.op_code) !== kind)
        return undefined;
      const data = Buffer.from(envelope.match_data.data, "base64");
      return test(data) ? data : undefined;
    });
  }

  async join(matchId, number) {
    const cid = String(this.nextCid++);
    const joined = this.waitFor("match join reply", (envelope) => {
      if (envelope.cid !== cid)
        return undefined;
      return envelope.error ? new Error("match join: " + (envelope.error.message || JSON.stringify(envelope.error))) : envelope.match;
    });
    this.socket.send(JSON.stringify({ cid: cid, match_join: { match_id: matchId, metadata: { playerNumber: number } } }));
    return joined;
  }

  send(matchId, data) {
    this.socket.send(JSON.stringify({ match_data_send: { match_id: matchId, op_code: data[0], data: data.toString("base64") } }));
  }

  close() {
    this.socket.close();
  }
}

function rosterNumber(data, slot) {
  return data.readBigUInt64LE(5 + slot * 8).toString();
}

function stateTick(data) {
  return Number(data.readBigInt64LE(5));
}

function damageBatch(sequence, enemyId, keys, hits) {
  const data = Buffer.alloc(57);
  data[0] = DamageBatchKind;
  data.writeUInt32LE(sequence, 1);
  data.writeUInt32LE(enemyId, 5);
  data.writeDoubleLE(keys, 9);
  data.writeInt32LE(hits, 49);
  return data;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function readMembership(base, token, userId) {
  const reply = await call(base, "/v2/storage", {
    method: "POST",
    headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
    body: JSON.stringify({ object_ids: [{ collection: "party", key: "current", user_id: userId }] })
  });
  assert.ok(reply.objects && reply.objects.length === 1, "no party/current in storage");
  return JSON.parse(reply.objects[0].value);
}

async function main() {
  const [base, serverKey] = process.argv.slice(2);
  if (!base || !serverKey)
    usage();

  const health = await fetch(base + "/healthcheck");
  assert.strictEqual(health.status, 200, "healthcheck");
  console.log("ok    healthcheck");

  const session = await authenticate(base, serverKey);
  console.log("ok    device login, user " + session.userId);

  const samples = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "test-vectors", "intake-samples.json"), "utf8"));
  const intake = async (kind, body) => (await fetch(base + "/intake/" + kind, { method: "POST", headers: { "Content-Type": "application/json" }, body: body })).status;
  assert.strictEqual(await intake("telemetry", JSON.stringify(samples.telemetry[0])), 200, "telemetry sample");
  assert.strictEqual(await intake("errors", JSON.stringify(samples.errors[0])), 200, "error report sample");
  assert.strictEqual(await intake("telemetry", JSON.stringify(Object.assign({ steamId: "1" }, samples.telemetry[0]))), 400, "a packet with an extra field");
  console.log("ok    intake takes client packets without login and refuses extra fields");

  const rift = await rpc(base, session.token, "rift_window", {});
  assert.ok(rift.serverUnixMs > 0 && rift.next, "rift_window reply " + JSON.stringify(rift));
  console.log("ok    rpc rift_window, server time " + new Date(rift.serverUnixMs).toISOString());

  const created = await rpc(base, session.token, "party_create", { language: "ru", title: "night_shift" });
  assert.match(created.code, /^[2-9A-HJ-NP-Z]{6}$/, "party code " + created.code);
  assert.strictEqual(created.leader, session.userId);
  console.log("ok    rpc party_create, code " + created.code.slice(0, 3) + "-" + created.code.slice(3));

  const friend = await authenticate(base, serverKey);
  const typedCode = created.code.slice(0, 3).toLowerCase() + "-" + created.code.slice(3);
  const joinedView = await rpc(base, friend.token, "party_join", { code: typedCode });
  assert.strictEqual(joinedView.members.length, 2);
  const connected = await rpc(base, session.token, "party_connect", {});
  assert.strictEqual((await rpc(base, friend.token, "party_connect", {})).matchId, connected.matchId, "both members get the same match");
  created.matchId = connected.matchId;
  console.log("ok    friend joined by code, both connect to match " + connected.matchId);

  const stranger = await authenticate(base, serverKey);
  await rpcRefused(base, stranger.token, "party_connect", {}, "not in a party");
  const intruder = new MatchSocket(base, stranger.token);
  await intruder.open();
  await assert.rejects(intruder.join(created.matchId, "76561197960287999"), /not a member|rejected/);
  intruder.close();
  await refused(base, "/v2/group", bearer(stranger.token, "POST", { name: "party-x", open: true }));
  await refused(base, "/v2/group?limit=10", bearer(stranger.token, "GET"));
  await refused(base, "/v2/group/" + created.id + "/join", bearer(stranger.token, "POST"));
  console.log("ok    a stranger cannot enter the match, create, list or join party groups");

  const first = new MatchSocket(base, session.token);
  await first.open();
  const firstRoster = first.packet(RosterKind, (data) => rosterNumber(data, 0) === firstNumber);
  const firstState = first.packet(EncounterStateKind, () => true);
  await first.join(created.matchId, firstNumber);
  await firstRoster;
  const startTick = stateTick(await firstState);
  console.log("ok    joined the party match, Roster slot 0 = " + firstNumber + ", fight tick " + startTick);

  const second = new MatchSocket(base, friend.token);
  await second.open();
  const bothRoster = first.packet(RosterKind, (data) => rosterNumber(data, 0) === firstNumber && rosterNumber(data, 1) === secondNumber);
  await second.join(created.matchId, secondNumber);
  await bothRoster;
  console.log("ok    second player joined, Roster slot 1 = " + secondNumber);

  const laterState = await first.packet(EncounterStateKind, (data) => stateTick(data) >= startTick + 10);
  console.log("ok    the fight keeps its state between ticks, tick " + startTick + " -> " + stateTick(laterState));
  second.close();
  await sleep(300);

  const killsBefore = first.counts[EnemyKilledKind] || 0;
  const paidKill = first.packet(EnemyKilledKind, () => true);
  for (let i = 0; i < 20; i++) {
    first.send(created.matchId, damageBatch(1000 + i, first.enemy, 1e6 * (i + 1), 1));
    await sleep(100);
  }
  await sleep(500);
  const forgedKills = (first.counts[EnemyKilledKind] || 0) - killsBefore;
  assert.ok(forgedKills >= 1 && forgedKills <= 8, "forged damage killed " + forgedKills + " enemies in 2 s");
  const killed = await paidKill;
  assert.strictEqual(killed[1 + 8 + 4 + 1 + 4 * 8] & 1, 1, "the typing player is not in the reward mask");
  console.log("ok    forged damage is cut: " + forgedKills + " kills in 2 s instead of 20, reward mask " + killed[1 + 8 + 4 + 1 + 4 * 8]);
  first.close();

  const membership = await readMembership(base, session.token, session.userId);
  assert.strictEqual(membership.matchId, created.matchId);
  assert.strictEqual(membership.partyId, created.id);
  assert.ok(membership.seen > 0);
  console.log("ok    storage party/current = party " + membership.partyId + ", slot " + membership.slot);

  const opened = await rpc(base, session.token, "party_update", { access: "open", wanted: "buffer" });
  assert.strictEqual(opened.access, "open");
  const listed = (await rpc(base, stranger.token, "party_list", { language: "ru", role: "buffer" })).parties;
  const card = listed.find((entry) => entry.id === created.id);
  assert.ok(card, "the open party is listed");
  assert.strictEqual(card.wantsYou, true);
  assert.strictEqual(card.code, undefined);
  const quick = await rpc(base, stranger.token, "party_quick", { language: "ru", role: "buffer" });
  assert.strictEqual(quick.id, created.id);
  assert.strictEqual(quick.members.length, 3);
  console.log("ok    the open party is found in the search and by quick match (" + listed.length + " listed)");

  await rpc(base, friend.token, "party_leave", {});
  await rpc(base, stranger.token, "party_leave", {});
  const after = await rpc(base, session.token, "party_leave", {});
  assert.strictEqual(after.party, null);
  assert.strictEqual((await rpc(base, session.token, "party_mine", {})).party, null);
  await rpcRefused(base, friend.token, "party_join", { code: created.code }, "unknown code");
  console.log("ok    everyone left, the party and its code are gone");
  console.log("smoke: all good");
}

main().catch((error) => {
  console.error("FAIL  " + error.message);
  process.exit(1);
});
