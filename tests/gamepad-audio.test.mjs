import assert from "node:assert/strict";
import { calculateSoundParameters } from "../src/OpenRails.Web/wwwroot/js/train-driving-audio.js";

const idle = calculateSoundParameters(0, 0, 0);
const driving = calculateSoundParameters(100, 100, 0);
const braking = calculateSoundParameters(0, 0, 100);
assert.equal(idle.engineFrequencyHz, 38);
assert.ok(driving.engineFrequencyHz > idle.engineFrequencyHz, "engine pitch rises with throttle/speed");
assert.ok(driving.engineGain > idle.engineGain, "engine output increases with driving input");
assert.equal(idle.brakeGain, 0);
assert.ok(braking.brakeGain > idle.brakeGain, "brake hiss gain follows brake input");
assert.equal(calculateSoundParameters(500, 500, -1).engineFrequencyHz, calculateSoundParameters(100, 180, 0).engineFrequencyHz);
console.log("Audio parameter tests passed.");
