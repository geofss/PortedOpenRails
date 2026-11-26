#!/usr/bin/env python3
"""Audit the licensed Demo Model 1 archive without extracting route assets."""
from __future__ import annotations
import argparse
import hashlib
import os
import re
import zipfile
from collections import Counter

EXPECTED_SHA256 = "5e4a64e1cd44e23ee7833230348b6b76c43fc555060b152f6c6547e832a5189b"
EXPECTED_ASSET = "DemoModel1.zip"


def audit(path: str, verify_hash: bool = True) -> dict:
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
    digest = hashlib.sha256()
    if verify_hash:
        with open(path, "rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        sha = digest.hexdigest()
        if sha != EXPECTED_SHA256:
            raise ValueError(f"SHA-256 mismatch: {sha}")
    else:
        sha = "not checked"

    with zipfile.ZipFile(path) as archive:
        bad_entry = archive.testzip()
        if bad_entry is not None:
            raise ValueError(f"CRC validation failed: {bad_entry}")
        files = [entry.filename for entry in archive.infolist() if not entry.is_dir()]
        normalized = [name.replace("\\", "/") for name in files]
        # Reject absolute paths, drive-qualified names, and parent traversal.
        for name in normalized:
            if name.startswith("/") or re.match(r"^[A-Za-z]:", name) or ".." in name.split("/"):
                raise ValueError(f"Unsafe archive path: {name}")
        routes = [name for name in normalized if name.lower().endswith(".trk")]
        if len(routes) != 1:
            raise ValueError(f"Expected one route TRK file, found {len(routes)}")
        route_path = routes[0]
        root = route_path.rsplit("/", 1)[0]
        route_id = os.path.splitext(os.path.basename(route_path))[0]
        tdb = f"{root}/{route_id}.tdb"
        if tdb.lower() not in {name.lower() for name in normalized}:
            raise ValueError(f"Missing route TDB: {tdb}")
        route_files = [name for name in normalized if name.lower().startswith(root.lower() + "/")]
        extensions = Counter(os.path.splitext(name)[1].lower() for name in normalized)
        activities = [name for name in route_files if name.lower().endswith(".act")]
        world_tiles = [name for name in route_files if name.lower().endswith(".w")]
        global_tsection = [name for name in normalized if name.lower().endswith("/global/tsection.dat") or name.lower() == "global/tsection.dat"]
        route_tsection = [name for name in route_files if name.lower().endswith("/tsection.dat")]
        trk = archive.read(route_path)
        try:
            text = trk.decode("utf-16le", errors="strict")
        except UnicodeDecodeError as exc:
            raise ValueError("TRK is not valid UTF-16LE text") from exc
        def token(name):
            match = re.search(r"\b" + re.escape(name) + r"\s*\(\s*([^()]+?)\s*\)", text, re.I)
            return match.group(1).strip() if match else None
        route_name = None
        match = re.search(r'\bName\s*\(\s*"([^"]*)"\s*\)', text, re.I | re.S)
        if match:
            route_name = match.group(1)
        if not route_name or not token("RouteID"):
            raise ValueError("TRK metadata missing Name or RouteID")
        return {
            "sha256": sha,
            "zip_entries": len(archive.infolist()),
            "files": len(files),
            "route_file": route_path,
            "route_id": token("RouteID"),
            "route_name": route_name,
            "track_database": tdb,
            "activities": len(activities),
            "world_tiles": len(world_tiles),
            "global_tsection_files": len(global_tsection),
            "route_tsection_files": len(route_tsection),
            "extensions": dict(sorted(extensions.items())),
        }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("zip", help="Path to licensed DemoModel1.zip")
    parser.add_argument("--skip-hash", action="store_true", help="Skip the release SHA-256 check (CRC still runs)")
    args = parser.parse_args()
    result = audit(args.zip, verify_hash=not args.skip_hash)
    for key, value in result.items():
        print(f"{key}: {value}")


if __name__ == "__main__":
    main()
