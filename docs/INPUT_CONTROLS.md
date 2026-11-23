# Browser driving input reference

These mappings apply to the experimental route-driving controller; they are not a full Open Rails command map.

## Pointer / touch
- Throttle and brake: range sliders.
- Reverser: Forward / Neutral / Reverse selector (changing direction requires the consist stopped and throttle at zero).
- Left/right passenger doors: buttons; doors can open only when the consist is stopped and throttle is zero. Open doors block throttle.
- Emergency stop: applies full brake and latches the stop; driver commands are ignored until explicitly released.
- Start, Pause, Reset: simulation session controls.

## Keyboard (focus the keyboard-control group)
| Key | Action |
|---|---|
| Arrow Up / Down | Increase / decrease throttle in 5-point steps |
| Space | Toggle full brake |
| F / N / R | Forward / Neutral / Reverse |
| L / K | Toggle left / right doors (door safety interlock applies) |
| X | Apply latched emergency stop |
| Z | Explicitly release emergency-stop latch |
| Escape | Release throttle and brake |

## Standard-mapped gamepad
| Input | Action |
|---|---|
| Right trigger (button 7) | Throttle |
| Left trigger (button 6) | Brake |
| D-pad Up / Down / Left (buttons 12 / 13 / 14) | Forward / Reverse / Neutral |
| Left / Right shoulder (buttons 4 / 5) | Toggle left / right doors |
| South face button (button 0) | Apply emergency stop |
| East face button (button 1) | Explicitly release emergency stop |

Analog trigger values are clamped to 0–100% and non-finite values are treated as zero. The controller polls the first connected standard-mapped pad. If a gamepad disconnects while driving, throttle is cut and full brake is applied.

## Local driving preferences
- Synthesized audio volume is adjustable from 0–100% and saved locally.
- Gamepad trigger sensitivity is adjustable from 50–150%; the response curve is clamped and persisted.
- Reset driving settings restores 35% audio and 100% trigger sensitivity. Storage unavailable/errors fall back to defaults without blocking driving.

## Limitations
- Controls operate a prototype simulation, not the complete Open Rails command set.
- Door controls are interlocks only; passenger door animations and vehicle-specific door systems are not ported.
- Keyboard commands require focus on the keyboard-control area to avoid intercepting ordinary browser typing.
- Cab views, authentic cab controls, signalling commands, train protection, and activities remain unimplemented.
