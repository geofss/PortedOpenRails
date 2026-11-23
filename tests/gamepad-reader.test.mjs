import assert from "node:assert/strict";
import { readState } from "../src/OpenRails.Web/wwwroot/js/train-driving-gamepad.js";

const button = (value = 0, pressed = value > 0) => ({ value, pressed });
const disconnected = () => [];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: { getGamepads: disconnected } });
assert.equal(readState(), null, "returns null when no connected pad is found");
Object.defineProperty(globalThis, "navigator", { configurable: true, value: { getGamepads: () => null } });
assert.equal(readState(), null, "handles a null gamepad list");
Object.defineProperty(globalThis, "navigator", { configurable: true, value: { getGamepads: () => { throw new Error("blocked"); } } });
assert.equal(readState(), null, "handles gamepad API errors without propagating");
Object.defineProperty(globalThis, "navigator", { configurable: true, value: { getGamepads: disconnected } });

const buttons = Array.from({ length: 16 }, () => button());
buttons[6] = button(0.35, true);
buttons[7] = button(0.8, true);
buttons[12] = button(1, true);
buttons[0] = button(1, true);
buttons[4] = button(1, true);
Object.defineProperty(globalThis, "navigator", {
    configurable: true,
    value: { getGamepads: () => [{ connected: true, buttons }] }
});
assert.deepEqual(readState(), {
    throttle: 0.8,
    brake: 0.35,
    forwardPressed: true,
    reversePressed: false,
    neutralPressed: false,
    emergencyStopPressed: true,
    emergencyReleasePressed: false,
    leftDoorTogglePressed: true,
    rightDoorTogglePressed: false
});

buttons[12] = button();
buttons[13] = button(1, true);
buttons[14] = button(1, true);
buttons[0] = button();
buttons[1] = button(1, true);
buttons[4] = button();
buttons[5] = button(1, true);
assert.deepEqual(readState(), {
    throttle: 0.8,
    brake: 0.35,
    forwardPressed: false,
    reversePressed: true,
    neutralPressed: true,
    emergencyStopPressed: false,
    emergencyReleasePressed: true,
    leftDoorTogglePressed: false,
    rightDoorTogglePressed: true
});

const disconnectedFirst = { connected: false, buttons: Array.from({ length: 16 }, () => button(1, true)) };
const connectedSecondButtons = Array.from({ length: 16 }, () => button());
connectedSecondButtons[6] = { pressed: true }; // value omitted: pressed fallback
connectedSecondButtons[7] = { value: 1.4, pressed: true }; // clamp to 1
connectedSecondButtons[8] = { value: Number.POSITIVE_INFINITY, pressed: true }; // malformed value becomes 0
Object.defineProperty(globalThis, "navigator", {
    configurable: true,
    value: { getGamepads: () => [disconnectedFirst, { connected: true, buttons: connectedSecondButtons }] }
});
assert.deepEqual(readState(), {
    throttle: 1,
    brake: 1,
    forwardPressed: false,
    reversePressed: false,
    neutralPressed: false,
    emergencyStopPressed: false,
    emergencyReleasePressed: false,
    leftDoorTogglePressed: false,
    rightDoorTogglePressed: false
}, "ignores disconnected pads, handles missing analog values, and clamps trigger values");

connectedSecondButtons[6] = { value: Number.NaN, pressed: true };
assert.equal(readState().brake, 0, "non-finite analog trigger values safely become zero");
connectedSecondButtons[6] = { value: Number.POSITIVE_INFINITY, pressed: true };
assert.equal(readState().brake, 0, "infinite analog trigger values safely become zero");

// Sensitivity changes are applied to the live analog trigger values.
connectedSecondButtons[7] = { value: 0.25, pressed: true };
connectedSecondButtons[6] = { value: 0.25, pressed: true };
assert.ok(readState(1.5).throttle > readState(1).throttle, "higher sensitivity makes low trigger values respond earlier");
assert.ok(readState(0.5).throttle < readState(1).throttle, "lower sensitivity makes trigger response more gradual");
assert.equal(readState(Number.NaN).throttle, readState(1).throttle, "invalid sensitivity safely falls back to neutral curve");

console.log("Gamepad reader tests passed.");
