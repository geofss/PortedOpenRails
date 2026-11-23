// Read the first connected standard-mapped gamepad without installing event listeners.
// Analog triggers (buttons 7/6) map to throttle/brake; D-pad 12/13/14 maps direction.
export function readState() {
    const pads = typeof navigator !== "undefined" && navigator.getGamepads ? navigator.getGamepads() : [];
    const pad = Array.from(pads).find(candidate => candidate && candidate.connected);
    if (!pad) return null;
    const value = index => Math.max(0, Math.min(1, Number(pad.buttons?.[index]?.value ?? (pad.buttons?.[index]?.pressed ? 1 : 0)) || 0));
    const pressed = index => Boolean(pad.buttons?.[index]?.pressed);
    return {
        throttle: value(7),
        brake: value(6),
        forwardPressed: pressed(12),
        reversePressed: pressed(13),
        neutralPressed: pressed(14)
    };
}
