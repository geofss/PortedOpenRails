// Standard-mapped controller: triggers throttle/brake, D-pad direction, face emergency buttons.
export function normalizeTrigger(value, sensitivity = 1) {
    const input = Number(value), curve = Number(sensitivity);
    if (!Number.isFinite(input)) return 0;
    const safeCurve = Number.isFinite(curve) ? Math.max(0.5, Math.min(1.5, curve)) : 1;
    return Math.max(0, Math.min(1, Math.pow(Math.max(0, Math.min(1, input)), 1 / safeCurve)));
}
export function readState(sensitivity = 1) {
    let pads = [];
    try {
        pads = typeof navigator !== "undefined" && navigator.getGamepads ? navigator.getGamepads() : [];
    } catch {
        return null;
    }
    if (!pads) return null;
    // Ignore connected controllers that do not expose the standard browser button layout.
    const pad = Array.from(pads).find(candidate => candidate && candidate.connected && candidate.mapping === "standard");
    if (!pad) return null;
    const value = index => normalizeTrigger(Number(pad.buttons?.[index]?.value ?? (pad.buttons?.[index]?.pressed ? 1 : 0)), sensitivity);
    const pressed = index => Boolean(pad.buttons?.[index]?.pressed);
    return {
        throttle: value(7), brake: value(6),
        forwardPressed: pressed(12), reversePressed: pressed(13), neutralPressed: pressed(14),
        emergencyStopPressed: pressed(0), emergencyReleasePressed: pressed(1),
        leftDoorTogglePressed: pressed(4), rightDoorTogglePressed: pressed(5)
    };
}