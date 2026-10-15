# Rail Line Source Data

This folder holds the source file for the track lines drawn on the map. The source file is too large for Git (about 360 MB, over the GitHub 100 MB file limit), so every file in this folder except this README is ignored.

## Source

The track lines come from the North American Rail Network (NARN) Lines dataset in the National Transportation Atlas Database (NTAD), published by the U.S. Bureau of Transportation Statistics:

https://geodata.bts.gov/datasets/usdot::north-american-rail-network-lines/about

Download the dataset in GeoJSON format and save it in this folder as `NTAD_North_American_Rail_Network_Lines.geojson`.

## Generated File

`npm run build:rail-lines` reads the source file and writes `public/data/rail-lines-wi.geojson`, which the map loads. The script (`scripts/build-rail-lines.mjs`):

- Keeps only the features whose `STATEAB` is `WI`.
- Keeps only the railroad owner fields `RROWNER1`, `RROWNER2`, and `RROWNER3`, and only when they have a value.
- Rounds coordinates to 5 decimal places (about 1.1 m).

The generated file is committed to Git so that deployments include it. Files in `public/data` are stored in plain Git rather than Git Large File Storage (LFS), because the deploy workflows check out the repository without LFS.

## Regenerating the Track File

From `Web/web.client`:

1. Download the latest dataset into this folder, as described above.
2. Run `npm run build:rail-lines`. The script prints the feature count and the file size.
3. Change `RAILWAYS_VERSION` in `src/hooks/useRailways.ts`. Browsers cache the track data in IndexedDB and reload it only when this value changes.
4. Commit `public/data/rail-lines-wi.geojson` and `src/hooks/useRailways.ts`.

To build another state, pass its abbreviation: `npm run build:rail-lines -- --state MN` writes `public/data/rail-lines-mn.geojson`. The map loads only the Wisconsin file, so a new state also requires a change to `src/workers/railwaysWorker.ts`.

## Railroad Names

The Railroads panel on the map lists each owner code found in the track data. Full names for the codes are defined in `src/utils/railroadOwners.ts`; a code without an entry is shown as the code alone. `DEFAULT_SELECTED_OWNERS` in the same file lists the railroads highlighted by default.
