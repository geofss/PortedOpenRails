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
    let pad;
    try {
        pad = Array.from(pads).find(candidate => candidate && candidate.connected === true && candidate.mapping === "standard");
    } catch {
        return null;
    }
    if (!pad) return null;
    try {
        const buttons = pad.buttons;
        const button = index => {
            const item = buttons?.[index];
            return item && typeof item === "object" ? item : null;
        };
        const value = index => {
            const item = button(index);
            if (!item) return 0;
            const raw = item.value;
            const numeric = raw === undefined || raw === null ? (item.pressed === true ? 1 : 0) : Number(raw);
            return normalizeTrigger(numeric, sensitivity);
        };
        const pressed = index => button(index)?.pressed === true;
        return {
            throttle: value(7), brake: value(6),
            forwardPressed: pressed(12), reversePressed: pressed(13), neutralPressed: pressed(14),
            emergencyStopPressed: pressed(0), emergencyReleasePressed: pressed(1),
            leftDoorTogglePressed: pressed(4), rightDoorTogglePressed: pressed(5)
        };
    } catch {
        // Browser controller objects can become unavailable between polling frames.
        return null;
    }
}
