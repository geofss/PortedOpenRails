const DIRECTORY_NAME = "portedopenrails-packages";
const PROGRESS_INTERVAL_BYTES = 16 * 1024 * 1024;

function safeDisplayName(value) {
    const name = String(value ?? "route.zip").replace(/[\/\0-\x1f]/g, "_").replace(/\\/g, "_").trim();
    return name.length > 180 ? name.slice(-180) : (name || "route.zip");
}

export async function storeArchive(streamReference, originalName, expectedBytes, maxBytes, quotaMarginBytes, progress) {
    if (!("storage" in navigator) || typeof navigator.storage.getDirectory !== "function") {
        throw new Error("This browser does not support the Origin Private File System (OPFS) required for large route archives.");
    }
    if (!Number.isSafeInteger(expectedBytes) || expectedBytes <= 0 || expectedBytes > maxBytes) {
        throw new Error("The selected archive exceeds the 30 GB upload limit or has an invalid size.");
    }

    const estimate = await navigator.storage.estimate();
    const available = Number.isFinite(estimate.quota) && Number.isFinite(estimate.usage)
        ? Math.max(0, estimate.quota - estimate.usage)
        : null;
    if (available !== null && available < expectedBytes + quotaMarginBytes) {
        const freeGb = (available / 1_000_000_000).toFixed(2);
        const requiredGb = ((expectedBytes + quotaMarginBytes) / 1_000_000_000).toFixed(2);
        throw new Error(`Not enough browser storage: approximately ${freeGb} GB available, ${requiredGb} GB required including safety margin.`);
    }

    let persistentStorageGranted = false;
    if (typeof navigator.storage.persist === "function") {
        try { persistentStorageGranted = await navigator.storage.persist(); } catch { /* best effort */ }
    }

    const directory = await navigator.storage.getDirectory();
    const packageDirectory = await directory.getDirectoryHandle(DIRECTORY_NAME, { create: true });
    const id = crypto.randomUUID();
    const archiveName = `${id}.zip`;
    const metadataName = `${id}.json`;
    const archiveHandle = await packageDirectory.getFileHandle(archiveName, { create: true });
    let writer;
    let completed = false;
    let transferred = 0;
    let nextProgress = PROGRESS_INTERVAL_BYTES;

    try {
        writer = await archiveHandle.createWritable({ keepExistingData: false });
        const reader = streamReference.stream().getReader();
        try {
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                if (!(value instanceof Uint8Array)) {
                    throw new Error("The browser stream returned an invalid data chunk.");
                }
                transferred += value.byteLength;
                if (transferred > expectedBytes || transferred > maxBytes) {
                    throw new Error("The selected archive exceeded its declared size or the 30 GB limit.");
                }
                await writer.write(value);
                if (transferred >= nextProgress) {
                    await progress.invokeMethodAsync("Report", transferred, expectedBytes);
                    nextProgress = transferred + PROGRESS_INTERVAL_BYTES;
                }
            }
        } finally {
            reader.releaseLock();
        }

        if (transferred !== expectedBytes) {
            throw new Error(`Upload ended early: received ${transferred} of ${expectedBytes} bytes.`);
        }
        await writer.close();
        writer = null;

        const metadata = {
            id,
            fileName: safeDisplayName(originalName),
            archiveName,
            sizeBytes: transferred,
            createdUtc: new Date().toISOString(),
            persistentStorageGranted
        };
        const metadataHandle = await packageDirectory.getFileHandle(metadataName, { create: true });
        const metadataWriter = await metadataHandle.createWritable();
        await metadataWriter.write(JSON.stringify(metadata));
        await metadataWriter.close();
        completed = true;
        await progress.invokeMethodAsync("Report", transferred, expectedBytes);

        return {
            id,
            fileName: metadata.fileName,
            sizeBytes: transferred,
            persistentStorageGranted
        };
    } finally {
        if (!completed) {
            try { if (writer) await writer.abort(); } catch { /* ignore cleanup errors */ }
            try { await packageDirectory.removeEntry(archiveName); } catch { /* ignore cleanup errors */ }
            try { await packageDirectory.removeEntry(metadataName); } catch { /* ignore cleanup errors */ }
        }
    }
}

function validateArchiveId(id) {
    if (typeof id !== "string" || !/^[0-9a-f-]{36}$/i.test(id)) {
        throw new Error("Invalid staged archive identifier.");
    }
    return id;
}

export async function listArchives() {
    if (!("storage" in navigator) || typeof navigator.storage.getDirectory !== "function") {
        return [];
    }
    const root = await navigator.storage.getDirectory();
    let directory;
    try {
        directory = await root.getDirectoryHandle(DIRECTORY_NAME);
    } catch (error) {
        if (error?.name === "NotFoundError") return [];
        throw error;
    }

    const packages = [];
    for await (const [name, handle] of directory.entries()) {
        if (!name.endsWith(".json") || handle.kind !== "file") continue;
        try {
            const metadata = JSON.parse(await (await handle.getFile()).text());
            const id = validateArchiveId(metadata.id);
            const archiveName = `${id}.zip`;
            await directory.getFileHandle(archiveName);
            packages.push({
                id,
                fileName: safeDisplayName(metadata.fileName),
                sizeBytes: Number(metadata.sizeBytes),
                persistentStorageGranted: metadata.persistentStorageGranted === true
            });
        } catch {
            // Ignore stale or incomplete metadata records.
        }
    }
    return packages.sort((a, b) => a.fileName.localeCompare(b.fileName));
}

export async function deleteArchive(id) {
    const safeId = validateArchiveId(id);
    const root = await navigator.storage.getDirectory();
    const directory = await root.getDirectoryHandle(DIRECTORY_NAME);
    for (const suffix of [".zip", ".json"]) {
        try {
            await directory.removeEntry(`${safeId}${suffix}`);
        } catch (error) {
            if (error?.name !== "NotFoundError") throw error;
        }
    }
}
