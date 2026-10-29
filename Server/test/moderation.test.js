const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const server = { env: {} };

function world() {
  const nk = new FakeNakama();
  for (const name of ["ann", "bob", "cid", "dan"])
    nk.addUser(name, name.toUpperCase(), "7656119800000000" + name.length);
  return nk;
}

test("the moderation report lists flagged players and reported players for the console only", () => {
  const nk = world();
  const now = Date.now();
  party.partyNoteFlags(nk, "bob", "76561198000000002", 20, "m1", now - 1000);
  party.partyNoteFlags(nk, "bob", "76561198000000002", 40, "m1", now);
  party.partyNoteFlags(nk, "cid", "76561198000000003", 20, "m2", now);
  for (const [from, reason] of [["ann", "cheating"], ["cid", "cheating"], ["dan", "idle_farming"]])
    party.partyReportRpc({ userId: from, env: {} }, logger, nk, JSON.stringify({ userId: "bob", reason: reason }));
  party.partyReportRpc({ userId: "ann", env: {} }, logger, nk, JSON.stringify({ userId: "dan", reason: "offensive_name" }));
  nk.storageWrite([{ collection: "party_reports", key: "cid:dan", userId: party.PartySystemUser, value: { target: "cid", reporter: "dan", reason: "cheating", at: now - 40 * 86400000 } }]);

  const report = JSON.parse(party.partyModerationRpc(server, logger, nk, JSON.stringify({ days: 30 })));

  assert.deepStrictEqual(report.flagged.map((row) => [row.userId, row.name, row.total, row.lastMatch]), [["bob", "BOB", 2 * party.PartyServerRules.flagsBeforeReport, 40], ["cid", "CID", party.PartyServerRules.flagsBeforeReport, 20]]);
  assert.deepStrictEqual(report.reported.map((row) => [row.userId, row.name, row.reports, row.reporters]), [["bob", "BOB", 3, 3], ["dan", "DAN", 1, 1]]);
  assert.deepStrictEqual(report.reported[0].reasons, { cheating: 2, idle_farming: 1 });
  assert.strictEqual(report.reportedTotal, 2, "a report older than the window is left out");

  const longer = JSON.parse(party.partyModerationRpc(server, logger, nk, JSON.stringify({ days: 60, limit: 1 })));
  assert.strictEqual(longer.reportedTotal, 3);
  assert.strictEqual(longer.reported.length, 1);
  assert.throws(() => party.partyModerationRpc({ userId: "ann", env: {} }, logger, nk, "{}"), (error) => error.code === 7);
});
