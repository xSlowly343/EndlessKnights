const assert = require("assert");
const crypto = require("crypto");

const RosterKind = 8;
const EncounterStateKind = 5;
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
      return envelope.error ? new Error("match join: " + JSON.stringify(envelope.error)) : envelope.match;
    });
    this.socket.send(JSON.stringify({ cid: cid, match_join: { match_id: matchId, metadata: { playerNumber: number } } }));
    return joined;
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

  const rift = await rpc(base, session.token, "rift_window", {});
  assert.ok(rift.serverUnixMs > 0 && rift.next, "rift_window reply " + JSON.stringify(rift));
  console.log("ok    rpc rift_window, server time " + new Date(rift.serverUnixMs).toISOString());

  const created = await rpc(base, session.token, "party_create", { seed: "123" });
  assert.ok(created.matchId, "party_create reply " + JSON.stringify(created));
  console.log("ok    rpc party_create, match " + created.matchId);

  const first = new MatchSocket(base, session.token);
  await first.open();
  const firstRoster = first.packet(RosterKind, (data) => rosterNumber(data, 0) === firstNumber);
  const firstState = first.packet(EncounterStateKind, () => true);
  await first.join(created.matchId, firstNumber);
  await firstRoster;
  const startTick = stateTick(await firstState);
  console.log("ok    joined the party match, Roster slot 0 = " + firstNumber + ", fight tick " + startTick);

  const friend = await authenticate(base, serverKey);
  const second = new MatchSocket(base, friend.token);
  await second.open();
  const bothRoster = first.packet(RosterKind, (data) => rosterNumber(data, 0) === firstNumber && rosterNumber(data, 1) === secondNumber);
  await second.join(created.matchId, secondNumber);
  await bothRoster;
  console.log("ok    second player joined, Roster slot 1 = " + secondNumber);

  const laterState = await first.packet(EncounterStateKind, (data) => stateTick(data) >= startTick + 10);
  console.log("ok    the fight keeps its state between ticks, tick " + startTick + " -> " + stateTick(laterState));
  first.close();
  second.close();

  const membership = await readMembership(base, session.token, session.userId);
  assert.strictEqual(membership.matchId, created.matchId);
  assert.strictEqual(membership.slot, 0);
  console.log("ok    storage party/current = slot " + membership.slot);
  console.log("smoke: all good");
}

main().catch((error) => {
  console.error("FAIL  " + error.message);
  process.exit(1);
});
