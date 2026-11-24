# Demo Model 1 package audit

## Reproducible audit

Run the audit tool against the local licensed archive:

\`\`\`sh
python3 tools/audit_demo_model_1_package.py DemoModel1.zip
\`\`\`

The tool checks the release SHA-256, ZIP CRCs, archive paths, route TRK/TDB presence, and basic TRK route identity without extracting the copyrighted package.

## Package identity and integrity

- Package: `DemoModel1.zip`
- Source: published GitHub release `demorouteassets`
- Asset size: 272,422,379 bytes
- SHA-256: `5e4a64e1cd44e23ee7833230348b6b76c43fc555060b152f6c6547e832a5189b`
- ZIP archive: opens successfully
- ZIP CRC validation: passed (all archive entries tested)
- Entries: 4,507 total (4,427 files, 80 directory entries)

## Route package inventory

- Route folder: `Demo Model 1/ROUTES/SCE/`
- Track route file: `SCE.trk`
- Track database: `SCE.tdb` (2,532,218 bytes)
- Activities: 1 `ACTIVITIES/MT_MT_0930 Edinburgh-Glasgow Queen Street.act`
- World scenery tiles: 114 `.w` files
- Rolling-stock consist files: 23 `.con`
- Engine files: 29 `.eng`
- Wagon files: 51 `.wag`
- Shape files: 963 `.s`
- ACE textures: 1,571 `.ace`
- Audio files: 288 `.wav`

## Real route metadata observations

The route metadata is readable as UTF-16LE SIMISA text. The `.trk` file identifies:

- Route ID: `SCE`
- Name: `Scottish Capital Express`
- Description identifies it as an extract from the Scottish Capitals Express, Edinburgh Waverley to Glasgow Queen Street, by Making Tracks.
- Route-start metadata and environmental season/weather references are present.

The TDB text contains 1,101 TrackNode declarations, including 611 TrVectorNode and 366 TrJunctionNode tokens as observed by a simple text scan. These counts are inventory signals only—not validated topology counts. A full upstream-compatible parse and topology checks remain necessary.

The activity file metadata identifies `0930 Edinburgh-Glasgow Queen Street`, describes a Class 47 driving activity, and lists service stops in its briefing. The activity is not yet executed in the browser simulation.

## Validation boundary

This package audit confirms the published ZIP is intact and has the expected real-route file families, route metadata, a TDB, world tiles, rolling stock, and one activity. It does **not** yet prove the browser-port route parser can successfully import the full package, build topology, resolve assets, render the route, or execute the activity.

## Copyright

Demo Model 1 © Making Tracks Ltd. Used and distributed with permission from the Open Rails Management Team.
