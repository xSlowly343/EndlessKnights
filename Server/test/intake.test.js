const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const intake = loadModule();
const samples = readVectors("intake-samples.json");
const server = { env: {} };

function push(nk, rpc, value) {
  return JSON.parse(intake[rpc](server, logger, nk, typeof value === "string" ? value : JSON.stringify(value)));
}

function rejects(nk, rpc, value, message) {
  assert.throws(() => push(nk, rpc, value), (error) => error.message.indexOf(message) === 0 && error.code === (message === "daily limit reached" ? 8 : 3), rpc + " should fail with " + message);
}

const telemetry = samples.telemetry[0];

test("packets written by the client are accepted as they are", () => {
  const nk = new FakeNakama();
  for (const packet of samples.telemetry)
    assert.deepStrictEqual(push(nk, "telemetryPushRpc", packet), { ok: true });
  for (const report of samples.errors)
    assert.deepStrictEqual(push(nk, "errorReportPushRpc", report), { ok: true });
  assert.strictEqual(nk.storageList(intake.IntakeSystemUser, "telemetry").objects.length, 2);
  assert.strictEqual(nk.storageList(intake.IntakeSystemUser, "error_reports").objects.length, 1);
});

test("players cannot push or read through their own session, so a packet is never tied to an account", () => {
  const nk = new FakeNakama();
  const player = { userId: "u1", env: {} };
  for (const rpc of ["telemetryPushRpc", "errorReportPushRpc", "telemetrySummaryRpc", "errorReportListRpc"])
    assert.throws(() => intake[rpc](player, logger, nk, "{}"), (error) => error.message === "not available to players" && error.code === 7);
});

test("unknown, missing and out of range fields are refused", () => {
  const nk = new FakeNakama();
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { steamId: "7656" }), "unknown field steamId");
  const missing = Object.assign({}, telemetry);
  delete missing.recordStage;
  rejects(nk, "telemetryPushRpc", missing, "missing field recordStage");
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { installId: "ABC" }), "bad installId");
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { keysPerMinute: -1 }), "bad keysPerMinute");
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { character: "dragon" }), "bad character");
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { returnDays: [400] }), "bad returnDays");
  rejects(nk, "telemetryPushRpc", "not json", "bad packet");
  rejects(nk, "errorReportPushRpc", Object.assign({}, samples.errors[0], { kind: "panic" }), "bad kind");
});

test("a crash draft never overwrites the closed session it belongs to", () => {
  const nk = new FakeNakama();
  push(nk, "telemetryPushRpc", telemetry);
  push(nk, "telemetryPushRpc", Object.assign({}, telemetry, { closed: false, sessionSeconds: 10 }));
  const stored = nk.storageList(intake.IntakeSystemUser, "telemetry").objects[0].value;
  assert.strictEqual(stored.closed, true);
  assert.strictEqual(stored.sessionSeconds, telemetry.sessionSeconds);
});

test("error reports are cut, scrubbed again and grouped by fingerprint", () => {
  const nk = new FakeNakama();
  const long = Object.assign({}, samples.errors[0], {
    message: "Failed to load C:\\Users\\Ivan\\AppData\\save.json " + "x".repeat(5000),
    lines: Array.from({ length: 150 }, (_, i) => "line " + i + " /home/ivan/game")
  });
  push(nk, "errorReportPushRpc", long);
  push(nk, "errorReportPushRpc", long);
  push(nk, "errorReportPushRpc", samples.errors[0]);
  const stored = nk.storageList(intake.IntakeSystemUser, "error_reports").objects.map((o) => o.value);
  const scrubbed = stored.find((report) => report.message.indexOf("Failed") === 0);
  assert.ok(scrubbed.message.indexOf("C:\\Users\\%USER%\\AppData") >= 0, scrubbed.message.slice(0, 80));
  assert.strictEqual(scrubbed.message.length, intake.IntakeRules.errors.maxMessageChars);
  assert.strictEqual(scrubbed.lines.length, intake.IntakeRules.errors.maxLines);
  assert.strictEqual(scrubbed.lines[0], "line 50 /home/%USER%/game");
  const list = JSON.parse(intake.errorReportListRpc(server, logger, nk, "{}"));
  assert.strictEqual(list.total, 2);
  assert.strictEqual(list.groups[0].count, 2);
  assert.strictEqual(list.groups[0].kind, "crash");
});

test("the daily limit refuses extra packets and the first packet of a day sweeps old ones", () => {
  const nk = new FakeNakama();
  const old = Date.now() - (intake.IntakeRules.telemetry.retentionDays + 1) * 86400000;
  nk.storageWrite([{ collection: "telemetry", key: "old:1", userId: intake.IntakeSystemUser, value: Object.assign({}, telemetry, { receivedAt: old }) }]);
  push(nk, "telemetryPushRpc", telemetry);
  assert.deepStrictEqual(nk.storageList(intake.IntakeSystemUser, "telemetry").objects.map((o) => o.key), [telemetry.installId + ":4"]);
  const day = new Date().toISOString().substring(0, 10);
  nk.storageWrite([{ collection: "intake_days", key: "telemetry:" + day, userId: intake.IntakeSystemUser, value: { count: intake.IntakeRules.telemetry.maxPacketsPerDay } }]);
  rejects(nk, "telemetryPushRpc", Object.assign({}, telemetry, { session: 5 }), "daily limit reached");
});

test("the summary answers time to first prestige, keys per minute and retention", () => {
  const nk = new FakeNakama();
  const installs = [];
  for (let i = 0; i < 10; i++) {
    const id = ("0000000000000000000000000000000" + i.toString(16)).slice(-32);
    installs.push(id);
    push(nk, "telemetryPushRpc", Object.assign({}, telemetry, {
      installId: id, session: 1, keysPerMinute: 100 + i * 10, firstPrestigeSeconds: i < 4 ? 3600 * (i + 1) : null,
      daysSinceFirst: i < 8 ? 7 : 0, returnDays: i < 3 ? [0, 1, 7] : [0], character: i % 2 ? "viking" : "voodoo"
    }));
  }
  push(nk, "telemetryPushRpc", Object.assign({}, telemetry, { installId: installs[0], session: 2, keysPerMinute: 300, firstPrestigeSeconds: 3600, daysSinceFirst: 7, returnDays: [0, 1, 7] }));
  const summary = JSON.parse(intake.telemetrySummaryRpc(server, logger, nk, "{}"));
  assert.strictEqual(summary.installs, 10);
  assert.strictEqual(summary.sessions, 11);
  assert.deepStrictEqual(summary.retention.d7, { eligible: 8, returned: 3, share: 3 / 8 });
  assert.deepStrictEqual(summary.retention.d1, { eligible: 8, returned: 3, share: 3 / 8 });
  assert.strictEqual(summary.firstPrestigeSeconds.count, 4);
  assert.strictEqual(summary.firstPrestigeSeconds.median, 7200);
  assert.strictEqual(summary.withoutPrestige, 6);
  assert.strictEqual(summary.keysPerMinute.count, 11);
  assert.strictEqual(summary.keysPerMinute.p90, 190);
  assert.deepStrictEqual(summary.characters, { voodoo: 5, viking: 5 });
});
