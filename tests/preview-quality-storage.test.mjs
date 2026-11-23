import assert from "node:assert/strict";
import { getSavedQualityPreset, saveQualityPreset } from "../src/OpenRails.Web/wwwroot/js/route-terrain-renderer.js";

const store = new Map();
globalThis.localStorage = {
  getItem(key) { return store.has(key) ? store.get(key) : null; },
  setItem(key, value) { store.set(key, String(value)); }
};

assert.equal(getSavedQualityPreset(), "high", "default preset is high");
assert.equal(saveQualityPreset("low"), true);
assert.equal(getSavedQualityPreset(), "low", "saved low preset is restored");
assert.equal(saveQualityPreset("medium"), true);
assert.equal(getSavedQualityPreset(), "medium", "medium preset round-trips");
assert.equal(saveQualityPreset("ultra"), false, "unsupported preset is rejected");
assert.equal(getSavedQualityPreset(), "medium", "invalid preset does not overwrite stored value");

globalThis.localStorage = { getItem() { throw new Error("storage unavailable"); }, setItem() { throw new Error("storage unavailable"); } };
assert.equal(getSavedQualityPreset(), "high", "storage errors safely default to high");
assert.equal(saveQualityPreset("low"), false, "storage errors are reported without throwing");

delete globalThis.localStorage;
assert.equal(getSavedQualityPreset(), "high", "missing storage safely defaults to high");
assert.equal(saveQualityPreset("medium"), false, "missing storage is reported as unavailable");
console.log("Preview quality persistence tests passed.");
