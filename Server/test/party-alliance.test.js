const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const now = Date.UTC(2026, 10, 14, 10);
const day = 86400000;
server.PartyGrowthServerRules.colossus.mandatory = [{ upTo: 0, min: 0, max: 0 }];

function world() {
  const nk = new FakeNakama();
  ["ann", "bob", "cid", "dan", "eve"].forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const first = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: first.code });
  const second = call("cid", "partyCreateRpc", {});
  call("dan", "partyJoinRpc", { code: second.code });
  const third = call("eve", "partyCreateRpc", {});
  return { nk, call, first, second, third };
}

function reachLevel(nk, partyId, level) {
  let total = 0;
  for (let l = 1; l < level; l++)
    total += server.partyGrowthNeed(l);
  const have = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyGrowthAdd(nk, partyId, total - have);
}

test("two parties of level 60 ally by code: the leader proposes, the other leader accepts", () => {
  const { nk, call, first, second } = world();
  assert.throws(() => server.partyAlliancePropose(nk, "ann", second.code, now), /level 60/);
  reachLevel(nk, first.id, 60);
  assert.throws(() => server.partyAlliancePropose(nk, "ann", second.code, now), /has not reached level 60/);
  reachLevel(nk, second.id, 60);
  assert.throws(() => server.partyAlliancePropose(nk, "bob", second.code, now), /only the leader/);
  assert.throws(() => server.partyAlliancePropose(nk, "ann", "NOPE", now), /unknown code/);
  assert.throws(() => server.partyAlliancePropose(nk, "ann", first.code, now), /itself/);
  let view = server.partyAlliancePropose(nk, "ann", second.code, now);
  assert.strictEqual(view.outgoing.partyId, second.id);
  view = call("cid", "partyAllianceRpc");
  assert.strictEqual(view.incoming.length, 1);
  assert.throws(() => server.partyAllianceAnswer(nk, "dan", { partyId: first.id, accept: true }, now), /only the leader/);
  view = server.partyAllianceAnswer(nk, "cid", { partyId: first.id, accept: true }, now);
  assert.strictEqual(view.ally.partyId, first.id);
  assert.strictEqual(server.partyAllianceMine(nk, "bob", now).ally.partyId, second.id);
});

test("a proposal expires, a refusal clears it, an allied party takes no second ally", () => {
  const { nk, first, second, third } = world();
  reachLevel(nk, first.id, 60);
  reachLevel(nk, second.id, 60);
  reachLevel(nk, third.id, 60);
  server.partyAlliancePropose(nk, "ann", second.code, now);
  assert.strictEqual(server.partyAllianceMine(nk, "cid", now + 4 * day).incoming.length, 0, "three days to answer");
  server.partyAlliancePropose(nk, "ann", second.code, now);
  server.partyAllianceAnswer(nk, "cid", { partyId: first.id, accept: false }, now);
  assert.strictEqual(server.partyAllianceMine(nk, "ann", now).outgoing, null);
  server.partyAlliancePropose(nk, "ann", second.code, now);
  server.partyAllianceAnswer(nk, "cid", { partyId: first.id, accept: true }, now);
  assert.throws(() => server.partyAlliancePropose(nk, "eve", second.code, now), /already has an ally/);
  const view = server.partyAllianceBreak(nk, "ann", now);
  assert.strictEqual(view.ally, null);
  assert.strictEqual(server.partyAllianceMine(nk, "cid", now).ally, null);
});

test("once a week a party sends a tenth of its background damage to the ally's colossus, both treasuries get signs", () => {
  const { nk, call, first, second } = world();
  reachLevel(nk, first.id, 60);
  reachLevel(nk, second.id, 60);
  server.partyAlliancePropose(nk, "ann", second.code, now);
  server.partyAllianceAnswer(nk, "cid", { partyId: first.id, accept: true }, now);
  assert.throws(() => server.partyAllianceHelp(nk, "bob", now), /fight first/);
  server.partyColossusReport(nk, "bob", { background: [0, 0, 0, 5000] }, now);
  let view = server.partyAllianceMine(nk, "ann", now);
  assert.strictEqual(view.helpReady, true);
  assert.strictEqual(view.helpPoints, 500);
  const hp = server.partyColossusMine(nk, "cid", now).hp;
  view = call("bob", "partyAllianceHelpRpc");
  assert.strictEqual(view.result.points, 500);
  assert.strictEqual(server.partyColossusMine(nk, "cid", now).hp, hp - 500);
  assert.strictEqual(server.partyTreasuryRead(nk, first.id).value.treasury, 50);
  assert.strictEqual(server.partyTreasuryRead(nk, second.id).value.treasury, 50);
  assert.throws(() => server.partyAllianceHelp(nk, "ann", now), /already helped/);
  assert.strictEqual(server.partyAllianceMine(nk, "ann", now + 7 * day).helpReady, true, "a new week, a new help");
});
