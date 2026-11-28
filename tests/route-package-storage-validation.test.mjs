import assert from "node:assert/strict";
import { storeArchive } from "../src/OpenRails.Web/wwwroot/js/route-package-storage.js";

const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
Object.defineProperty(globalThis, "navigator", {
  configurable: true,
  value: { storage: { async getDirectory() { throw new Error("Storage should not be accessed for invalid arguments."); } } }
});

const progress = { invokeMethodAsync() {} };
await assert.rejects(storeArchive({}, "route.zip", 0, 100, 0, progress), /size or storage limit is invalid/);
await assert.rejects(storeArchive({}, "route.zip", 10, 0, 0, progress), /size or storage limit is invalid/);
await assert.rejects(storeArchive({}, "route.zip", 10, 100, -1, progress), /size or storage limit is invalid/);
await assert.rejects(storeArchive({}, "route.zip", Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER, 1, progress), /size or storage limit is invalid/);
await assert.rejects(storeArchive({}, "route.zip", 10, 100, 0, null), /progress reporting is unavailable/);

if (originalNavigator) Object.defineProperty(globalThis, "navigator", originalNavigator);
else delete globalThis.navigator;

console.log("Route package storage validation tests passed.");
