const DIRECTORY_NAME = "portedopenrails-packages";
const MAX_ENTRIES = 100_000;
const MAX_CENTRAL_DIRECTORY_BYTES = 256 * 1024 * 1024;
const EOCD_SIGNATURE = 0x06054b50;
const ZIP64_EOCD_SIGNATURE = 0x06064b50;
const ZIP64_LOCATOR_SIGNATURE = 0x07064b50;
const CENTRAL_SIGNATURE = 0x02014b50;
const LOCAL_SIGNATURE = 0x04034b50;
const UINT16_MAX = 0xffff;
const UINT32_MAX = 0xffffffff;

// A route import opens thousands of entries. Cache the validated directory index,
// lookup map, metadata, and immutable OPFS File so each entry is O(1) to locate.
const archiveContextCache = new Map();

function validateId(id) {
    if (typeof id !== "string" || !/^[0-9a-f-]{36}$/i.test(id)) {
        throw new Error("Invalid staged archive identifier.");
    }
    return id;
}

function readU64(view, offset) {
    const value = view.getBigUint64(offset, true);
    if (value > BigInt(Number.MAX_SAFE_INTEGER)) {
        throw new Error("ZIP offset or size exceeds the browser's safe integer range.");
    }
    return Number(value);
}

function findEocd(bytes) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    for (let offset = bytes.byteLength - 22; offset >= Math.max(0, bytes.byteLength - 65_557); offset--) {
        if (view.getUint32(offset, true) === EOCD_SIGNATURE) {
            const commentLength = view.getUint16(offset + 20, true);
            if (offset + 22 + commentLength === bytes.byteLength) return offset;
        }
    }
    throw new Error("ZIP end-of-central-directory record was not found.");
}

function normalizeEntryPath(raw) {
    const path = raw.replace(/\\/g, "/");
    if (!path || path.startsWith("/") || /^[a-zA-Z]:/.test(path) || path.includes("\0")) {
        throw new Error("Archive contains a rooted or invalid entry path.");
    }
    const parts = path.split("/").filter(Boolean);
    if (!parts.length || parts.some(part => part === "." || part === ".." || part.includes(":"))) {
        throw new Error("Archive contains an unsafe path traversal entry.");
    }
    return parts.join("/");
}

function parseZip64Extra(extra, needs) {
    let cursor = 0;
    while (cursor + 4 <= extra.length) {
        const id = extra.getUint16(cursor, true);
        const length = extra.getUint16(cursor + 2, true);
        cursor += 4;
        if (cursor + length > extra.length) throw new Error("Malformed ZIP extra field.");
        if (id === 0x0001) {
            let position = cursor;
            const end = cursor + length;
            const result = { uncompressedSize: null, compressedSize: null, localOffset: null, diskStart: null };
            for (const field of ["uncompressedSize", "compressedSize", "localOffset", "diskStart"]) {
                if (!needs[field]) continue;
                const width = field === "diskStart" ? 4 : 8;
                if (position + width > end) throw new Error("Incomplete ZIP64 extended information.");
                result[field] = width === 4 ? extra.getUint32(position, true) : readU64(extra, position);
                position += width;
            }
            return result;
        }
        cursor += length;
    }
    if (Object.values(needs).some(Boolean)) throw new Error("ZIP64 entry is missing its ZIP64 extra field.");
    return {};
}

export async function inspectArchive(id) {
    validateId(id);
    if (!navigator.storage?.getDirectory) throw new Error("OPFS is unavailable in this browser.");
    const root = await navigator.storage.getDirectory();
    const directory = await root.getDirectoryHandle(DIRECTORY_NAME);
    const metadataFile = await (await directory.getFileHandle(id + ".json")).getFile();
    const metadata = JSON.parse(await metadataFile.text());
    if (validateId(metadata.id) !== id) throw new Error("Staged archive metadata does not match its identifier.");
    const archiveFile = await (await directory.getFileHandle(id + ".zip")).getFile();
    if (archiveFile.size !== metadata.sizeBytes) throw new Error("Staged archive size does not match its metadata.");
    if (archiveFile.size < 22) throw new Error("The staged file is too small to be a ZIP archive.");

    const tailStart = Math.max(0, archiveFile.size - 65_557);
    const tail = new Uint8Array(await archiveFile.slice(tailStart).arrayBuffer());
    const eocdOffset = findEocd(tail);
    const eocd = new DataView(tail.buffer, tail.byteOffset, tail.byteLength);
    const diskNumber = eocd.getUint16(eocdOffset + 4, true);
    const centralDisk = eocd.getUint16(eocdOffset + 6, true);
    let entriesOnDisk = eocd.getUint16(eocdOffset + 8, true);
    let entryCount = eocd.getUint16(eocdOffset + 10, true);
    let centralSize = eocd.getUint32(eocdOffset + 12, true);
    let centralOffset = eocd.getUint32(eocdOffset + 16, true);

    if (diskNumber !== 0 || centralDisk !== 0 || entriesOnDisk !== entryCount) throw new Error("Multi-disk ZIP archives are not supported.");
    if (entryCount === UINT16_MAX || centralSize === UINT32_MAX || centralOffset === UINT32_MAX) {
        const locatorAbsolute = tailStart + eocdOffset - 20;
        if (locatorAbsolute < 0) throw new Error("ZIP64 locator is missing.");
        const locatorBytes = new DataView(await archiveFile.slice(locatorAbsolute, locatorAbsolute + 20).arrayBuffer());
        if (locatorBytes.getUint32(0, true) !== ZIP64_LOCATOR_SIGNATURE || locatorBytes.getUint32(4, true) !== 0 ||
            locatorBytes.getUint32(16, true) !== 1) {
            throw new Error("Invalid or multi-disk ZIP64 locator.");
        }
        const zip64Offset = readU64(locatorBytes, 8);
        const zip64Header = new DataView(await archiveFile.slice(zip64Offset, zip64Offset + 56).arrayBuffer());
        if (zip64Header.getUint32(0, true) !== ZIP64_EOCD_SIGNATURE) throw new Error("ZIP64 end record is invalid.");
        if (zip64Header.getUint32(16, true) !== 0 || zip64Header.getUint32(20, true) !== 0) {
            throw new Error("Multi-disk ZIP64 archives are not supported.");
        }
        const entriesOnDisk = readU64(zip64Header, 24);
        entryCount = readU64(zip64Header, 32);
        if (entriesOnDisk !== entryCount) throw new Error("Split ZIP64 archives are not supported.");
        centralSize = readU64(zip64Header, 40);
        centralOffset = readU64(zip64Header, 48);
    }

    if (entryCount > MAX_ENTRIES) throw new Error("Archive contains more than " + MAX_ENTRIES + " entries.");
    if (centralSize > MAX_CENTRAL_DIRECTORY_BYTES) throw new Error("ZIP central directory exceeds the 256 MiB indexing limit.");
    const centralEnd = centralOffset + centralSize;
    if (!Number.isSafeInteger(centralEnd) || centralEnd > archiveFile.size) {
        throw new Error("ZIP central directory extends beyond the archive or safe offset range.");
    }
    const centralBytes = new Uint8Array(await archiveFile.slice(centralOffset, centralOffset + centralSize).arrayBuffer());
    const central = new DataView(centralBytes.buffer, centralBytes.byteOffset, centralBytes.byteLength);
    const seen = new Set();
    const entries = [];
    const localRecordRanges = [];
    let cursor = 0;
    let totalUncompressed = 0;

    for (let index = 0; index < entryCount; index++) {
        if (cursor + 46 > central.byteLength || central.getUint32(cursor, true) !== CENTRAL_SIGNATURE) {
            throw new Error("Malformed ZIP central-directory entry at index " + index + ".");
        }
        const flags = central.getUint16(cursor + 8, true);
        const method = central.getUint16(cursor + 10, true);
        const crc32 = central.getUint32(cursor + 16, true);
        let compressedSize = central.getUint32(cursor + 20, true);
        let uncompressedSize = central.getUint32(cursor + 24, true);
        const nameLength = central.getUint16(cursor + 28, true);
        const extraLength = central.getUint16(cursor + 30, true);
        const commentLength = central.getUint16(cursor + 32, true);
        let diskStart = central.getUint16(cursor + 34, true);
        let localOffset = central.getUint32(cursor + 42, true);
        const end = cursor + 46 + nameLength + extraLength + commentLength;
        if (end > central.byteLength) throw new Error("Truncated ZIP central-directory entry.");
        if ((flags & 1) !== 0) throw new Error("Encrypted ZIP entries are not supported.");
        if (method !== 0 && method !== 8) throw new Error("Unsupported ZIP compression method " + method + ".");
        const nameBytes = centralBytes.subarray(cursor + 46, cursor + 46 + nameLength);
        let rawName;
        try {
            rawName = new TextDecoder((flags & 0x800) !== 0 ? "utf-8" : "windows-1252", { fatal: true }).decode(nameBytes);
        } catch {
            throw new Error("Archive contains a filename that cannot be decoded safely.");
        }
        const isDirectory = rawName.endsWith("/") || rawName.endsWith("\\");
        const path = normalizeEntryPath(rawName);
        const extraView = new DataView(centralBytes.buffer, centralBytes.byteOffset + cursor + 46 + nameLength, extraLength);
        const zip64 = parseZip64Extra(extraView, {
            uncompressedSize: uncompressedSize === UINT32_MAX,
            compressedSize: compressedSize === UINT32_MAX,
            localOffset: localOffset === UINT32_MAX,
            diskStart: diskStart === UINT16_MAX
        });
        if (zip64.uncompressedSize !== undefined && zip64.uncompressedSize !== null) uncompressedSize = zip64.uncompressedSize;
        if (zip64.compressedSize !== undefined && zip64.compressedSize !== null) compressedSize = zip64.compressedSize;
        if (zip64.localOffset !== undefined && zip64.localOffset !== null) localOffset = zip64.localOffset;
        if (zip64.diskStart !== undefined && zip64.diskStart !== null) diskStart = zip64.diskStart;
        if (diskStart !== 0) throw new Error("Multi-disk ZIP entries are not supported.");
        if (!isDirectory) {
            const key = path.toLocaleLowerCase("en-US");
            if (seen.has(key)) throw new Error("Archive contains duplicate virtual path: " + path);
            seen.add(key);
            if (uncompressedSize > 30_000_000_000 || compressedSize > 30_000_000_000) {
                throw new Error("An archive entry exceeds the 30 GB supported limit: " + path);
            }
            totalUncompressed += uncompressedSize;
            if (!Number.isSafeInteger(totalUncompressed)) throw new Error("Archive uncompressed size exceeds safe indexing limits.");
            const localHeader = new DataView(await archiveFile.slice(localOffset, localOffset + 30).arrayBuffer());
            if (localHeader.byteLength !== 30 || localHeader.getUint32(0, true) !== LOCAL_SIGNATURE ||
                localHeader.getUint16(6, true) !== flags ||
                localHeader.getUint16(8, true) !== method) {
                throw new Error("Invalid or inconsistent local ZIP header for " + path);
            }
            const localNameLength = localHeader.getUint16(26, true);
            const localExtraLength = localHeader.getUint16(28, true);
            const localNameBytes = new Uint8Array(await archiveFile.slice(
                localOffset + 30, localOffset + 30 + localNameLength).arrayBuffer());
            if (localNameBytes.byteLength !== localNameLength) {
                throw new Error("Truncated local ZIP filename for " + path);
            }
            let localName;
            try {
                localName = new TextDecoder((flags & 0x800) !== 0 ? "utf-8" : "windows-1252", { fatal: true }).decode(localNameBytes);
            } catch {
                throw new Error("Local ZIP filename cannot be decoded safely: " + path);
            }
            if (localName !== rawName) {
                throw new Error("Local and central ZIP filenames do not match: " + path);
            }
            const dataOffset = localOffset + 30 + localNameLength + localExtraLength;
            const dataEnd = dataOffset + compressedSize;
            if (!Number.isSafeInteger(dataOffset) || !Number.isSafeInteger(dataEnd) ||
                dataOffset > centralOffset || dataEnd > centralOffset) {
                throw new Error("ZIP entry data overlaps or extends beyond the central directory: " + path);
            }
            localRecordRanges.push({
                start: localOffset,
                end: dataOffset + compressedSize,
                path
            });
            entries.push({
                path,
                uncompressedSize,
                compressedSize,
                compressionMethod: method,
                crc32,
                dataOffset
            });
        }
        cursor = end;
    }
    if (cursor !== central.byteLength) throw new Error("Unexpected trailing bytes in the ZIP central directory.");
    localRecordRanges.sort((a, b) => a.start - b.start);
    for (let index = 1; index < localRecordRanges.length; index++) {
        if (localRecordRanges[index].start < localRecordRanges[index - 1].end) {
            throw new Error("ZIP local records overlap: " +
                localRecordRanges[index - 1].path + " and " + localRecordRanges[index].path);
        }
    }
    entries.sort((a, b) => a.path.localeCompare(b.path, "en"));
    const indexHandle = await directory.getFileHandle(id + ".index.json", { create: true });
    const indexWriter = await indexHandle.createWritable();
    try {
        await indexWriter.write(JSON.stringify({ version: 1, id, entries }));
        await indexWriter.close();
    } catch (error) {
        await indexWriter.abort().catch(() => {});
        throw error;
    }
    return {
        id,
        fileName: metadata.fileName,
        archiveSizeBytes: archiveFile.size,
        entryCount: entries.length,
        totalUncompressedBytes: totalUncompressed,
        routeFiles: entries.filter(entry => entry.path.toLowerCase().endsWith(".trk")).map(entry => entry.path)
    };
}


// Entry descriptors are persisted separately from the ZIP so later reads do not
// need to rescan a potentially multi-gigabyte archive.
async function readStoredIndex(directory, id) {
    const cached = archiveContextCache.get(id);
    if (cached) return cached;

    const handle = await directory.getFileHandle(id + ".index.json");
    const index = JSON.parse(await (await handle.getFile()).text());
    if (index.version !== 1 || validateId(index.id) !== id ||
        !Array.isArray(index.entries) || index.entries.length > MAX_ENTRIES) {
        throw new Error("Stored ZIP index is invalid or belongs to another archive.");
    }

    const entriesByPath = new Map();
    for (const entry of index.entries) {
        if (!entry || typeof entry.path !== "string" ||
            normalizeEntryPath(entry.path) !== entry.path ||
            !Number.isSafeInteger(entry.uncompressedSize) || entry.uncompressedSize < 0 ||
            entry.uncompressedSize > 30_000_000_000 ||
            !Number.isSafeInteger(entry.compressedSize) || entry.compressedSize < 0 ||
            entry.compressedSize > 30_000_000_000 ||
            !Number.isSafeInteger(entry.dataOffset) || entry.dataOffset < 0 ||
            !Number.isInteger(entry.compressionMethod) ||
            (entry.compressionMethod !== 0 && entry.compressionMethod !== 8) ||
            !Number.isInteger(entry.crc32) || entry.crc32 < 0 || entry.crc32 > UINT32_MAX) {
            throw new Error("Stored ZIP index contains an invalid entry descriptor.");
        }
        const key = entry.path.toLocaleLowerCase("en-US");
        if (entriesByPath.has(key)) throw new Error("Stored ZIP index contains duplicate virtual paths.");
        entriesByPath.set(key, entry);
    }

    const metadata = JSON.parse(await (await directory.getFileHandle(id + ".json")).getFile().then(file => file.text()));
    const archiveFile = await (await directory.getFileHandle(id + ".zip")).getFile();
    if (validateId(metadata.id) !== id ||
        !Number.isSafeInteger(metadata.sizeBytes) || metadata.sizeBytes <= 0 ||
        archiveFile.size !== metadata.sizeBytes) {
        throw new Error("Staged archive or metadata does not match the stored index.");
    }

    const context = { index, entriesByPath, archiveFile };
    archiveContextCache.set(id, context);
    return context;
}

export async function listEntries(id) {
    validateId(id);
    const root = await navigator.storage.getDirectory();
    const directory = await root.getDirectoryHandle(DIRECTORY_NAME);
    const index = await readStoredIndex(directory, id);
    return index.index.entries.map(entry => entry.path);
}

const crcTable = (() => {
    const table = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
        let value = n;
        for (let bit = 0; bit < 8; bit++) value = (value & 1) ? (0xedb88320 ^ (value >>> 1)) : (value >>> 1);
        table[n] = value >>> 0;
    }
    return table;
})();

export async function openEntry(id, requestedPath) {
    validateId(id);
    const normalized = normalizeEntryPath(requestedPath);
    const root = await navigator.storage.getDirectory();
    const directory = await root.getDirectoryHandle(DIRECTORY_NAME);
    const context = await readStoredIndex(directory, id);
    const key = normalized.toLocaleLowerCase("en-US");
    const entry = context.entriesByPath.get(key);
    if (!entry) throw new Error("Archive entry was not found: " + normalized);

    const archive = context.archiveFile;
    if (!Number.isSafeInteger(entry.dataOffset + entry.compressedSize) ||
        entry.dataOffset + entry.compressedSize > archive.size) {
        throw new Error("Staged archive or indexed entry boundaries are invalid.");
    }

    let stream = archive.slice(entry.dataOffset, entry.dataOffset + entry.compressedSize).stream();
    if (entry.compressionMethod === 8) {
        if (typeof DecompressionStream === "undefined") throw new Error("This browser does not support streaming ZIP deflate decompression.");
        try {
            stream = stream.pipeThrough(new DecompressionStream("deflate-raw"));
        } catch (error) {
            throw new Error("This browser cannot decode raw DEFLATE ZIP entries: " + error.message);
        }
    } else if (entry.compressionMethod !== 0) {
        throw new Error("Unsupported ZIP compression method " + entry.compressionMethod + ".");
    }

    let crc = 0xffffffff;
    let total = 0;
    const verifier = new TransformStream({
        transform(chunk, controller) {
            const bytes = chunk instanceof Uint8Array ? chunk : new Uint8Array(chunk);
            total += bytes.byteLength;
            if (!Number.isSafeInteger(total) || total > entry.uncompressedSize) {
                throw new Error("ZIP entry expanded beyond its declared size: " + entry.path);
            }
            for (let i = 0; i < bytes.length; i++) crc = crcTable[(crc ^ bytes[i]) & 0xff] ^ (crc >>> 8);
            controller.enqueue(bytes);
        },
        flush() {
            const actualCrc = (crc ^ 0xffffffff) >>> 0;
            if (total !== entry.uncompressedSize || actualCrc !== entry.crc32) {
                throw new Error("ZIP entry size or CRC-32 validation failed: " + entry.path);
            }
        }
    });
    // Blazor's DotNet.createJSStreamReference accepts a Blob or typed-array
    // payload, not a WHATWG ReadableStream. Materialize this single validated
    // entry as a Blob; the .NET compatibility bridge already materializes each
    // entry into its bounded virtual store, so this keeps the interop contract
    // correct while retaining per-entry limits and CRC verification.
    const validatedBlob = await new Response(stream.pipeThrough(verifier)).blob();
    if (!(validatedBlob instanceof Blob)) {
        throw new Error("ZIP entry could not be materialized as a browser Blob: " + entry.path);
    }
    return DotNet.createJSStreamReference(validatedBlob);
}
