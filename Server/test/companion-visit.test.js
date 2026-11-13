const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama } = require("./fake-nakama");

const server = loadModule();
const plain = (o) => JSON.parse(JSON.stringify(o));
const now = Date.UTC(2026, 10, 13, 12);
const rules = server.CompanionServerRules;
const companion = Object.keys(rules.companions)[0];

function world() {
  const nk = new FakeNakama();
  for (const [i, name] of ["ann", "bob", "eve"].entries())
    nk.addUser(name, name, "7656119800000000" + i);
  nk.addFriends("ann", "bob");
  return nk;
}

const send = (nk, from, host, at) => plain(server.companionVisitSend(nk, from, { host: host, companion: companion, name: "Rex", stage: 1 }, at === undefined ? now : at));

test("a companion visits a friend's hideout and comes back with a gift from its biome", () => {
  const nk = world();
  const biome = Object.keys(rules.biomes).find((key) => key !== rules.companions[companion]);
  server.hideoutPublish(nk, "bob", { theme: biome }, now);
  assert.throws(() => send(nk, "ann", "eve"), /only to friends, clan and party/);
  assert.throws(() => send(nk, "ann", "ann"), /not to yourself/);
  assert.throws(() => server.companionVisitSend(nk, "ann", { host: "bob", companion: "dragon", name: "", stage: 1 }, now), /bad request/);
  const sent = send(nk, "ann", "bob");
  assert.strictEqual(sent.away.hostName, "bob");
  assert.strictEqual(sent.away.ready, false);
  assert.strictEqual(sent.visitsLeft, rules.visitsPerDay - 1);
  assert.throws(() => send(nk, "ann", "bob"), /already visiting/);
  const host = plain(server.companionVisitView(nk, "bob", now + 1000));
  assert.deepStrictEqual(host.guests.map((guest) => [guest.fromName, guest.name]), [["ann", "Rex"]]);
  assert.throws(() => server.companionVisitClaim(nk, "ann", now + 1000), /not back yet/);
  const back = plain(server.companionVisitClaim(nk, "ann", now + rules.visitMs));
  assert.deepStrictEqual(back.gifts, { [rules.biomes[biome].fish]: 1, [rules.biomes[biome].bone]: 1 }, "the gift comes from the host's biome");
  assert.strictEqual(back.companion, companion);
  assert.strictEqual(back.away, null);
  assert.ok(server.walletRead(nk, "bob").value.balances.friendship > 0, "the host gets a sign of friendship");
  assert.deepStrictEqual(plain(server.companionVisitView(nk, "bob", now + rules.visitMs)).guests, [], "the guest has gone home");
  assert.strictEqual(server.inflowRead(nk, "ann", server.walletDayOf(now)).value.held[rules.biomes[biome].fish], 1, "the gift counts as handed out by the server");
  assert.throws(() => server.companionVisitClaim(nk, "ann", now + rules.visitMs), /no companion is visiting/);
});

test("a few visits a day", () => {
  const nk = world();
  let at = now;
  for (let i = 0; i < rules.visitsPerDay; i++) {
    send(nk, "ann", "bob", at);
    at += rules.visitMs;
    server.companionVisitClaim(nk, "ann", at);
  }
  assert.throws(() => send(nk, "ann", "bob", at), /no more visits today/);
});
