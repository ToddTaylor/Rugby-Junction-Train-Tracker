declare module '*.geojson' {
    const value: import('geojson').GeoJSON;
    export default value;
}