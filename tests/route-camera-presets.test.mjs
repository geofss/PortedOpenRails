import assert from "node:assert/strict";
import { getCameraPreset, getSavedCameraPreset, saveCameraPreset, setCameraPreset } from "../src/OpenRails.Web/wwwroot/js/route-terrain-renderer.js";

assert.equal(getCameraPreset(), "overview");
assert.equal(getSavedCameraPreset(), "overview", "missing storage defaults to overview");
assert.equal(saveCameraPreset("cab"), false, "unsupported camera modes cannot be persisted");

const savedValues = new Map();
Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    value: {
        getItem: key => savedValues.get(key) ?? null,
        setItem: (key, value) => savedValues.set(key, value)
    }
});
assert.equal(saveCameraPreset("trackside"), true);
assert.equal(getSavedCameraPreset(), "trackside");
assert.equal(setCameraPreset("trackside"), true);
assert.equal(getCameraPreset(), "trackside");
assert.equal(setCameraPreset("topdown"), true);
assert.equal(getCameraPreset(), "topdown");
assert.equal(setCameraPreset("overview"), true);
assert.equal(getCameraPreset(), "overview");
assert.equal(setCameraPreset("cab"), false, "unsupported camera modes are rejected");
assert.equal(getCameraPreset(), "overview", "invalid selection preserves the active preset");

delete globalThis.localStorage;
console.log("Route camera preset tests passed.");
