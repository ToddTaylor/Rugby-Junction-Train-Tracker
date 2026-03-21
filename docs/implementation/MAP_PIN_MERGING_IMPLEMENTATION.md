# Map Pin Merging Implementation

## Overview
When multiple map pins exist for the same beacon and subdivision, the system must decide which one is primary and merge them intelligently. This behavior ensures users see one clean pin per logical location instead of overlapping duplicates.

## Current Behavior

### When Merging Occurs
- `MapPinService.UpsertMapPin()` checks for existing pins at the beacon via `GetAllByBeaconAsync()`.
- If a new address arrives at a beacon where one or more pins already exist, the service evaluates whether to keep the existing pin, replace it, or merge state.
- Merging behavior is triggered by the order in which addresses are received and the freshness of existing state.

### The Merge Decision Logic
1. **Identify all existing pins** at the target beacon (same BeaconID, same SubdivisionId).
2. **Preserve primary state**: The first (or most recent) address is typically considered primary.
3. **Merge address lists**: If the existing pin has addresses and the new address is different, both addresses are retained in the Address collection.
4. **Direction and movement**: The latest telemetry determines direction and moving state; stale pins are not refreshed if a fresher address exists.
5. **User-tracked pins**: When a merge occurs and the duplicate pin is removed, any user-tracked pins pointing to the old ID must be migrated to the primary pin ID.

### Key Gotchas
- **Duplicate detection**: If two addresses for the same beacon arrive in quick succession, the second one may create a duplicate pin. The next UpsertMapPin call will detect and consolidate.
- **Tracked pin orphaning**: If a tracked pin points to a pin that gets merged away, the user's tracked pin is lost unless explicitly migrated.
- **Multi-railroad beacons**: A single beacon may have multiple (BeaconID, SubdivisionId) entries. The service must match on both, not just BeaconID.

## Architecture

### Repository-Level Merge Check
```csharp
// MapPinRepository.GetAllByBeaconAsync(beaconID, subdivisionId, addressID)
// Returns all existing pins at this beacon/subdivision combo.
public async Task<List<MapPin>> GetAllByBeaconAsync(int beaconID, int subdivisionId, int addressID)
{
    // Query for pins at the beacon that have different addresses
    // or match the address but have different paths
}
```

### Service-Level Consolidation
```csharp
// In MapPinService.UpsertMapPin()
var existingPins = await _mapPinRepository.GetAllByBeaconAsync(beacon.ID, subdivision.ID, address.AddressID);
if (existingPins.Count > 1)
{
    // Decide on primary pin: usually the first or most-recently-updated
    var primaryPin = existingPins.OrderByDescending(p => p.LastUpdate).FirstOrDefault();
    
    // Migrate all user-tracked pins from duplicates to primary
    foreach (var duplicate in existingPins.Where(p => p.ID != primaryPin.ID))
    {
        await _userTrackedPinRepository.UpdateMapPinIdAsync(duplicate.ID, primaryPin.ID);
        await _mapPinRepository.DeleteAsync(duplicate.ID);
    }
}
```

### UserTrackedPinRepository Migration
```csharp
public async Task UpdateMapPinIdAsync(int oldMapPinId, int newMapPinId)
{
    var pins = await _context.UserTrackedPins
        .Where(p => p.MapPinId == oldMapPinId)
        .ToListAsync();
    
    foreach (var pin in pins)
    {
        pin.MapPinId = newMapPinId;
    }
    
    await _context.SaveChangesAsync();
}
```

## Data Flow Example
1. Telemetry arrives at Beacon 42, subdivision 1, address A.
   - **Action**: Create MapPin(ID=100, BeaconID=42, SubdivisionId=1, Addresses=[A])
2. Telemetry arrives at Beacon 42, subdivision 1, address B (same location, different sensor).
   - **Action**: Query finds MapPin(ID=100). Add address B to its Addresses list.
   - **Action**: Update MapPin(ID=100, Addresses=[A, B])
3. User tracks MapPin 100 with symbol "TRAIN1".
   - **Action**: Create UserTrackedPin(MapPinId=100, Symbol="TRAIN1")
4. System anomaly: Telemetry for address A creates a new pin instead of updating.
   - **Action**: Query finds MapPin(ID=100) and MapPin(ID=101) both at Beacon 42, subdivision 1.
   - **Action**: Pick primary (likely ID=100 as the older, fresher one).
   - **Action**: Migrate UserTrackedPin(MapPinId=100) stays, DeleteAsync(MapPin=101).

## Key Invariants
- **One canonical pin per beacon/subdivision**: After merge, exactly one active MapPin exists per unique (BeaconID, SubdivisionId) pair.
- **Tracked pins always valid**: A user's tracked pin ID must always reference an existing MapPin; orphaned pins are migrated or deleted.
- **Address history is preserved**: Addresses in the Addresses collection are never deleted; they capture all sensor observations for this logical train.
- **Fresh data wins**: The most-recent LastUpdate timestamp determines direction and moving state in a merged pin.
- **Composite key safety**: Merging respects (BeaconID, SubdivisionId) as the unique identifier; do not merge pins across different subdivisions even if they sit at the same physical beacon.

## Testing Coverage
- **MapPinServiceTests.cs**: `UpsertMapPin_CreateMapPin_MultiRailroad` verifies multi-railroad beacon handling and the "first railroad wins" hack.
- **MapPinRepositoryTests.cs**: Should cover GetAllByBeaconAsync with various address and count scenarios.
- **Integration Tests**: Should verify tracked pin migration when pins are merged.

## Common Issues and Recovery
- **Duplicate pins persist**: If GetAllByBeaconAsync is not called, duplicates are not detected. Always call it before insert.
- **Tracked pins orphaned**: If migration is skipped, users lose their tracking. The UserTrackedPinRepository.UpdateMapPinIdAsync call is mandatory.
- **Subdivision mismatch**: If the merge logic matches on BeaconID only, pins from different subdivisions may be consolidated incorrectly. Always include SubdivisionId in the uniqueness check.

## Future Enhancements
- Add a database index on (BeaconID, SubdivisionId) to speed up GetAllByBeaconAsync.
- Log merge events so operators can review when pins are consolidated.
- Add a batch cleanup job to find and repair orphaned tracked pins.
- Expose merge metrics via API for diagnostics.
