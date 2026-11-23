import assert from "node:assert/strict";
import { getCameraPreset, setCameraPreset } from "../src/OpenRails.Web/wwwroot/js/route-terrain-renderer.js";

assert.equal(getCameraPreset(), "overview");
assert.equal(setCameraPreset("trackside"), true);
assert.equal(getCameraPreset(), "trackside");
assert.equal(setCameraPreset("topdown"), true);
assert.equal(getCameraPreset(), "topdown");
assert.equal(setCameraPreset("overview"), true);
assert.equal(getCameraPreset(), "overview");
assert.equal(setCameraPreset("cab"), false, "unsupported camera modes are rejected");
assert.equal(getCameraPreset(), "overview", "invalid selection preserves the active preset");

console.log("Route camera preset tests passed.");
