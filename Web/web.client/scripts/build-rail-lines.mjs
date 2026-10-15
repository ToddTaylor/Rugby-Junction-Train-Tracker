// Builds the compact rail-lines GeoJSON the map loads from the NTAD North American Rail Network Lines file.
//
// Usage:
//   node scripts/build-rail-lines.mjs [input] [output] [--state WI] [--precision 5]
//
// Defaults:
//   input      source-data/NTAD_North_American_Rail_Network_Lines.geojson
//   output     public/data/rail-lines-wi.geojson
//   --state    WI (matched against STATEAB)
//   --precision 5 decimal places (about 1.1 m), far finer than the map can draw
//
// Only the railroad owner fields (RROWNER1/2/3) are kept, and only when they have a value.

import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const OWNER_FIELDS = ['RROWNER1', 'RROWNER2', 'RROWNER3'];

function parseArgs(argv) {
    const options = { state: 'WI', precision: 5, positional: [] };
    for (let i = 0; i < argv.length; i++) {
        const arg = argv[i];
        if (arg === '--state') options.state = argv[++i];
        else if (arg === '--precision') options.precision = Number(argv[++i]);
        else options.positional.push(arg);
    }
    return options;
}

const options = parseArgs(process.argv.slice(2));
const input = options.positional[0] ?? 'source-data/NTAD_North_American_Rail_Network_Lines.geojson';
const output = options.positional[1] ?? `public/data/rail-lines-${options.state.toLowerCase()}.geojson`;
const factor = 10 ** options.precision;

const round = (n) => Math.round(n * factor) / factor;

function roundCoordinates(coords) {
    return typeof coords[0] === 'number'
        ? [round(coords[0]), round(coords[1])]
        : coords.map(roundCoordinates);
}

// Rounding can collapse neighbouring points onto each other; drop the repeats.
function dedupeLine(line) {
    return line.filter((pt, i) => i === 0 || pt[0] !== line[i - 1][0] || pt[1] !== line[i - 1][1]);
}

function compactGeometry(geometry) {
    const coordinates = roundCoordinates(geometry.coordinates);
    if (geometry.type === 'LineString') {
        return { type: 'LineString', coordinates: dedupeLine(coordinates) };
    }
    if (geometry.type === 'MultiLineString') {
        return { type: 'MultiLineString', coordinates: coordinates.map(dedupeLine) };
    }
    return { type: geometry.type, coordinates };
}

const started = Date.now();
const source = JSON.parse(fs.readFileSync(input, 'utf8'));

const features = [];
for (const feature of source.features) {
    if (feature.properties?.STATEAB !== options.state || !feature.geometry) continue;

    const properties = {};
    for (const field of OWNER_FIELDS) {
        const value = feature.properties[field];
        if (value) properties[field] = value;
    }
    features.push({ type: 'Feature', properties, geometry: compactGeometry(feature.geometry) });
}

const json = JSON.stringify({ type: 'FeatureCollection', features });
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, json);
const gz = zlib.gzipSync(json, { level: 9 });

const kb = (bytes) => `${(bytes / 1024).toFixed(0)} KB`;
console.log(`${features.length} features for ${options.state} written to ${output}`);
console.log(`Size: ${kb(json.length)} (${kb(gz.length)} gzipped) in ${((Date.now() - started) / 1000).toFixed(1)}s`);
