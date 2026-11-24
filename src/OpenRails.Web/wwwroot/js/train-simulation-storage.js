const prefix = "ported-open-rails:simulation:";
export function saveSnapshot(key, json) {
  try {
    localStorage.setItem(prefix + key, json);
    return true;
  } catch {
    return false;
  }
}
export function loadSnapshot(key) {
  try {
    return localStorage.getItem(prefix + key);
  } catch {
    return null;
  }
}
export function clearSnapshot(key) {
  try {
    localStorage.removeItem(prefix + key);
    return true;
  } catch {
    return false;
  }
}
