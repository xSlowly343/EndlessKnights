const fs = require("fs");
const path = require("path");
const vm = require("vm");

function loadModule() {
  const context = vm.createContext({ ArrayBuffer, DataView, Uint8Array, JSON, Math, Date, Error, String, Object });
  vm.runInContext(fs.readFileSync(path.join(__dirname, "..", "build", "index.js"), "utf8"), context);
  return context;
}

function readVectors(name) {
  return JSON.parse(fs.readFileSync(path.join(__dirname, "..", "test-vectors", name), "utf8"));
}

module.exports = { loadModule, readVectors };
