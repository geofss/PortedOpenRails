import assert from "node:assert/strict";
import { readState } from "../src/OpenRails.Web/wwwroot/js/train-driving-gamepad.js";

const button = (value = 0, pressed = value > 0) => ({ value, pressed });
const disconnected = () => [];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: { getGamepads: disconnected } });
assert.equal(readState(), null, "returns null when no connected pad is found");

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
console.log("Gamepad reader tests passed.");
