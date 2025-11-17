import { openDB, IDBPDatabase } from 'idb';

const DB_NAME = 'railways-db';
const DB_VERSION = 2;

// Stores used across domain APIs
const STORES = ['geojson', 'beacons'];

export async function openRailwaysDB(): Promise<IDBPDatabase<any>> {
  return openDB(DB_NAME, DB_VERSION, {
    upgrade(db) {
      STORES.forEach(store => {
        if (!db.objectStoreNames.contains(store)) {
          db.createObjectStore(store);
        }
      });
    }
  });
}
