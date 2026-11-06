const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");

const server = loadModule();

test("enemies on a stage keep growing in steps after the first cap", () => {
  const rules = server.PartyServerRules;
  const at = (stage) => server.partyEnemiesOnStage(rules, stage);
  assert.strictEqual(at(1), 10);
  assert.strictEqual(at(71), 32);
  assert.strictEqual(at(299), 32);
  assert.strictEqual(at(300), 48);
  assert.strictEqual(at(1000), 64);
  assert.strictEqual(at(3000), 96);
  assert.strictEqual(at(8000), 128);
  assert.strictEqual(at(20000), 160);
  assert.strictEqual(at(50000), 200);
  assert.strictEqual(at(100000), 256);
});
