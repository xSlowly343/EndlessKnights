const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");

const server = loadModule();

test("enemies on a stage keep growing in steps after the first cap", () => {
  const rules = server.PartyServerRules;
  const at = (stage) => server.partyEnemiesOnStage(rules, stage);
  assert.strictEqual(at(1), 10);
  assert.strictEqual(at(71), 32);
  assert.strictEqual(at(999), 32);
  assert.strictEqual(at(1000), 48);
  assert.strictEqual(at(3000), 64);
  assert.strictEqual(at(10000), 96);
  assert.strictEqual(at(25000), 128);
  assert.strictEqual(at(50000), 160);
  assert.strictEqual(at(100000), 200);
});
