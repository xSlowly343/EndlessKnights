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

function stockBag(server, nk, users) {
  const items = {};
  const put = (item) => { items[item] = 1000; };
  server.TownServerRules.giftItems.forEach(put);
  Object.keys(server.HideoutServerRules.materials).forEach(put);
  ["herb_knockbloom", "herb_windwort", "ember_coal", "ore_hills", "ore_ruins", "fish_hills_roach"].forEach(put);
  server.HideoutServerRules.dishes.forEach((dish) => put("dish:" + dish));
  server.HideoutServerRules.recipes.forEach((recipe) => { put("potion:" + recipe); put("potion:" + recipe + ":fine"); });
  for (const user of users)
    server.inflowBaseline(nk, user, items);
}

module.exports = { loadModule, readVectors, stockBag };
