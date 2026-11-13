const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.treasury;
const now = Date.UTC(2026, 10, 14, 10);
const day = 86400000;
const plain = (o) => JSON.parse(JSON.stringify(o));

function party(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const view = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: view.code });
  return { nk, call, partyId: view.id };
}

function openTree(nk, partyId) {
  server.partyGrowthAdd(nk, partyId, 1e6);
}

function seedTree(nk, partyId, spent, levels) {
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.spent = spent;
    value.levels = levels;
  });
}

test("treasury rules come from party.json", () => {
  assert.strictEqual(rules.currency, "friendship");
  assert.strictEqual(rules.nodes, 50);
  assert.strictEqual(rules.maxVotes, 3);
  assert.strictEqual(rules.freeResetMs, 7 * day);
  assert.strictEqual(rules.resetFeeShare, 0.1);
});

test("any member puts friendship tokens into the treasury, each share is shown and nothing comes back", () => {
  const { nk, call, partyId } = party("ann", "bob");
  server.walletGrant(nk, "ann", "mentor_hours", 50, now);
  server.walletGrant(nk, "bob", "mentor_hours", 30, now);
  server.partyTreasuryDonate(nk, "ann", 40, now);
  const view = server.partyTreasuryDonate(nk, "bob", 30, now);
  assert.strictEqual(view.treasury, 70);
  assert.strictEqual(view.yours, 30);
  assert.deepStrictEqual(plain(view.donors.map((d) => [d.name, d.amount])), [["ann", 40], ["bob", 30]]);
  assert.throws(() => server.partyTreasuryDonate(nk, "bob", 1, now), /not enough/);
  assert.throws(() => server.partyTreasuryDonate(nk, "ann", 0, now), /bad request/);
  assert.throws(() => server.partyTreasuryDonate(nk, "ann", 1.5, now), /bad request/);
  assert.strictEqual(call("ann", "partyTreasuryRpc").treasury, 70);
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 70);
});

test("votes need the party tree, each member holds up to 3 and sees who voted", () => {
  const { nk, partyId } = party("ann", "bob");
  assert.throws(() => server.partyTreasuryVote(nk, "bob", { node: 4 }, now), /not open/);
  openTree(nk, partyId);
  server.partyTreasuryVote(nk, "ann", { node: 4 }, now);
  server.partyTreasuryVote(nk, "bob", { node: 4 }, now);
  server.partyTreasuryVote(nk, "bob", { node: 4 }, now);
  server.partyTreasuryVote(nk, "bob", { node: 9 }, now);
  let view = server.partyTreasuryVote(nk, "bob", { node: 11 }, now);
  assert.deepStrictEqual(plain(view.votes["4"].sort()), ["ann", "bob"]);
  assert.deepStrictEqual(plain(view.mine), [4, 9, 11]);
  assert.strictEqual(view.votesLeft, 0);
  assert.throws(() => server.partyTreasuryVote(nk, "bob", { node: 12 }, now), /no votes left/);
  assert.throws(() => server.partyTreasuryVote(nk, "bob", { node: 51 }, now), /bad request/);
  view = server.partyTreasuryVote(nk, "bob", { node: 9, add: false }, now);
  assert.deepStrictEqual(plain(view.mine), [4, 11]);
  assert.strictEqual(view.votesLeft, 1);
});

test("a vote lasts until its node goes up a level, and a leaver's votes go away", () => {
  const { nk, call, partyId } = party("ann", "bob", "cid");
  openTree(nk, partyId);
  server.partyTreasuryVote(nk, "ann", { node: 4 }, now);
  server.partyTreasuryVote(nk, "bob", { node: 4 }, now);
  server.partyTreasuryVote(nk, "cid", { node: 7 }, now);
  seedTree(nk, partyId, 100, { "4": 1 });
  let view = server.partyTreasuryMine(nk, "ann", now);
  assert.strictEqual(view.votes["4"], undefined);
  assert.deepStrictEqual(plain(view.votes["7"]), ["cid"]);
  assert.strictEqual(view.votesLeft, 3);
  call("cid", "partyLeaveRpc");
  view = server.partyTreasuryMine(nk, "ann", now);
  assert.strictEqual(view.votes["7"], undefined);
});

test("the leader proposes a reset, it passes with more than half and is free once a week", () => {
  const { nk, partyId } = party("ann", "bob", "cid", "dan");
  openTree(nk, partyId);
  seedTree(nk, partyId, 2000, { "1": 5, "4": 3 });
  assert.throws(() => server.partyTreasuryPropose(nk, "bob", now), /only the leader/);
  let view = server.partyTreasuryPropose(nk, "ann", now);
  assert.deepStrictEqual(plain(view.reset.yes), ["ann"]);
  assert.strictEqual(view.resetFee, 0);
  assert.throws(() => server.partyTreasuryPropose(nk, "ann", now), /already open/);
  view = server.partyTreasuryResetVote(nk, "bob", true, now);
  assert.strictEqual(view.outcome, "", "two of four is not more than half");
  view = server.partyTreasuryResetVote(nk, "cid", true, now);
  assert.strictEqual(view.outcome, "passed");
  assert.strictEqual(view.reset, null);
  assert.deepStrictEqual(plain(view.levels), {});
  assert.strictEqual(view.spent, 0);
  assert.strictEqual(view.treasury, 2000, "the spent tokens come back to the treasury");
  assert.strictEqual(view.freeResetAt, now + 7 * day);

  seedTree(nk, partyId, 1000, { "2": 1 });
  view = server.partyTreasuryPropose(nk, "ann", now + day);
  assert.strictEqual(view.resetFee, 100, "a second reset in a week costs 10% of the tree");
  server.partyTreasuryResetVote(nk, "bob", true, now + day);
  view = server.partyTreasuryResetVote(nk, "dan", true, now + day);
  assert.strictEqual(view.outcome, "passed");
  assert.strictEqual(view.treasury, 2000 + 900);
  assert.strictEqual(view.freeResetAt, now + 7 * day, "a paid reset does not move the free week");
});

test("a reset vote fails when half say no, ends after a day and the leader can cancel it", () => {
  const { nk, partyId } = party("ann", "bob", "cid", "dan");
  openTree(nk, partyId);
  seedTree(nk, partyId, 500, { "3": 2 });
  server.partyTreasuryPropose(nk, "ann", now);
  server.partyTreasuryResetVote(nk, "bob", false, now);
  let view = server.partyTreasuryResetVote(nk, "cid", false, now);
  assert.strictEqual(view.outcome, "rejected");
  assert.deepStrictEqual(plain(view.levels), { "3": 2 });

  server.partyTreasuryPropose(nk, "ann", now);
  view = server.partyTreasuryMine(nk, "bob", now + day);
  assert.strictEqual(view.reset, null, "the vote closes after a day");
  assert.throws(() => server.partyTreasuryResetVote(nk, "bob", true, now + day), /no reset vote/);

  server.partyTreasuryPropose(nk, "ann", now + day);
  assert.throws(() => server.partyTreasuryCancel(nk, "bob", now + day), /only the leader/);
  view = server.partyTreasuryCancel(nk, "ann", now + day);
  assert.strictEqual(view.outcome, "cancelled");
  assert.strictEqual(view.reset, null);
});

test("a solo party resets at once, an empty tree has nothing to reset", () => {
  const { nk, partyId } = party("ann");
  openTree(nk, partyId);
  assert.throws(() => server.partyTreasuryPropose(nk, "ann", now), /nothing to reset/);
  seedTree(nk, partyId, 300, { "1": 2 });
  const view = server.partyTreasuryPropose(nk, "ann", now);
  assert.strictEqual(view.outcome, "passed");
  assert.strictEqual(view.treasury, 300);
});
