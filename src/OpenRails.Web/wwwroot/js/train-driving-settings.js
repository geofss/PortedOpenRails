const KEY = "ported-openrails-driving-settings-v1";
const DEFAULTS = Object.freeze({ audioVolume: 35, gamepadSensitivity: 100 });
function sanitize(raw) {
 return { audioVolume: Number.isFinite(Number(raw?.audioVolume)) ? Math.max(0,Math.min(100,Math.round(Number(raw.audioVolume)))) : 35,
 gamepadSensitivity: Number.isFinite(Number(raw?.gamepadSensitivity)) ? Math.max(50,Math.min(150,Math.round(Number(raw.gamepadSensitivity)))) : 100 };
}
export function loadSettings() { try { if(typeof localStorage==="undefined")return {...DEFAULTS}; const v=localStorage.getItem(KEY); return v===null?{...DEFAULTS}:sanitize(JSON.parse(v)); } catch { return {...DEFAULTS}; } }
export function saveSettings(audioVolume,gamepadSensitivity) { const s=sanitize({audioVolume,gamepadSensitivity}); try { if(typeof localStorage==="undefined")return false; localStorage.setItem(KEY,JSON.stringify(s)); return true; } catch { return false; } }
export function resetSettings() { try { if(typeof localStorage==="undefined")return false; localStorage.setItem(KEY,JSON.stringify(DEFAULTS)); return true; } catch { return false; } }