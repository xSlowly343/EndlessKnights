const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");

const server = loadModule();
const vectors = readVectors("rift-schedule.json");
const rules = server.RiftServerRules;

test("rift windows open and close like the core", () => {
  for (const sample of vectors.samples) {
    const window = server.riftWindowAt(rules, sample.nowUnixMs);
    if (sample.window === null) {
      assert.strictEqual(window, null, "at " + sample.nowUnixMs);
      continue;
    }
    assert.deepStrictEqual(JSON.parse(JSON.stringify(window)), { id: sample.window.id, startUnixMs: sample.window.startUnixMs, endUnixMs: sample.window.endUnixMs });
    assert.deepStrictEqual(JSON.parse(JSON.stringify(server.riftModifiersFor(rules, window.id))), sample.window.modifiers);
  }
});

test("next window, its modifiers and the announcement match the core", () => {
  for (const sample of vectors.samples) {
    const next = server.riftNext(rules, sample.nowUnixMs);
    assert.strictEqual(next.startUnixMs, sample.nextStartUnixMs, "at " + sample.nowUnixMs);
    assert.deepStrictEqual(JSON.parse(JSON.stringify(server.riftModifiersFor(rules, next.id))), sample.nextModifiers);
    assert.strictEqual(server.riftAnnounced(rules, sample.nowUnixMs), sample.announced, "at " + sample.nowUnixMs);
  }
});

test("rift_window reply carries the open window and the next one", () => {
  const inside = vectors.samples.find((sample) => sample.window !== null);
  const reply = JSON.parse(server.riftWindowReply(rules, inside.nowUnixMs));
  assert.strictEqual(reply.serverUnixMs, inside.nowUnixMs);
  assert.strictEqual(reply.open.endUnixMs, inside.window.endUnixMs);
  assert.deepStrictEqual(reply.open.modifiers, inside.window.modifiers);
  assert.strictEqual(reply.next.startUnixMs, inside.nextStartUnixMs);
  assert.strictEqual(reply.announced, false);
});
