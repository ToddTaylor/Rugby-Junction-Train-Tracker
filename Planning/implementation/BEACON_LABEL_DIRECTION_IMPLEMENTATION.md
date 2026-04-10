# Beacon Label Direction Implementation

## Overview
Beacon labels on the map display the last known train time and direction (compass arrows: ▲ ▼ ► ◄). The direction must refresh in sync with the timestamp; stale arrows cause confusion when the timestamp is freshly updated but the direction stays old.

## Current Frontend Behavior

### BeaconLabelPin Component
The component displays beacon status with a label that includes:
1. **Timestamp**: When the last train was detected (refreshed from live map pin updates)
2. **Direction Arrow**: A compass indicator (▲ N, ▼ S, ► E, ◄ W, etc.)

### The Stale Arrow Bug
**Symptom**: Timestamp updates (showing "30 seconds ago") but the arrow does not change, making the label appear inconsistent.

**Root Cause**: The component's state dependencies were tracking only `actualLastUpdateTime`, not `actualDirection`. Leaflet's DivIcon HTML is not refreshed unless both values change together.

**Example**:
```
Before: ⏱ 2 mins ago ▲ N
New data arrives with 30s timestamp but same direction (N)
After (buggy): ⏱ 30s ago ▲ N  <- Timestamp fresh, but if direction *did* change to ◄, it won't show
```

### Correct Fix
The component's rendering logic must depend on **both** `actualLastUpdateTime` AND `actualDirection`:

```jsx
// In statusText or a similar dependency array
useMemo(() => {
  return `${formatTime(actualLastUpdateTime)} ${getDirectionArrow(actualDirection)}`;
}, [actualLastUpdateTime, actualDirection]);  // BOTH must be here!
```

When either value changes, the entire status text is recalculated, and Leaflet's DivIcon re-renders with the fresh arrow.

## Current Server Behavior

### Direction Calculation in MapPinService
1. **Get the primary beacon**: From the telemetry or map pin.
2. **Get the destination beacon**: The next logical stop based on the subdivision and direction constraints.
3. **Calculate bearing**: Use latitude/longitude to compute compass direction (N, NE, E, SE, S, SW, W, NW).
4. **Constrain to allowed directions**: Apply the beacon's direction constraints (e.g., EastWest only allows E or W).

### Direction Update Timing
- Direction is calculated when `MapPinService.UpsertMapPin()` is called.
- The `MapPin` entity stores the calculated direction.
- SignalR sends the latest map pin (with fresh direction) to all connected clients.
- Clients update their local state and should re-render the beacon label.

### Edge Case: Stationary Trains
If a train has not moved in > 6 hours (configurable), the direction is set to null:
```csharp
if ((utcNow - lastUpdate).TotalHours > StationaryDirectionNullThresholdHours)
{
    direction = null;  // Hide stale direction; show "stationary" instead
}
```

## Data Flow: New Telemetry → Beacon Label Refresh

1. **Telemetry Arrives**: API receives a new telemetry packet with beacon and timestamp.
2. **MapPinService.UpsertMapPin()**: Calls DirectionService to calculate direction.
3. **MapPin Updated**: Entity is saved with new LastUpdate and Direction.
4. **SignalR Broadcast**: NotificationHub.OnMapPinUpdate() sends the updated MapPin DTO to all clients.
5. **Client Receives Update**: Redux or local state updates with the new map pin. 
6. **BeaconLabelPin Re-renders**: Dependencies on (lastUpdateTime, direction) trigger a re-render.
7. **Leaflet Icon HTML Updated**: The new direction arrow appears on the map.

## Key Invariants

### Timestamp-Direction Consistency
- **Rule**: If the timestamp is fresh (recent), the direction must be current too. Never show a stale arrow with a fresh timestamp.
- **Implementation**: Both are recalculated together in MapPinService.UpsertMapPin().
- **Client Sync**: Both are included in the MapPin DTO sent over SignalR.

### Null Direction Handling
- **Stationary trains**: If the train has not moved in > 6 hours, direction is null.
- **Display**: Client shows no arrow or a generic symbol (e.g., ● for stationary).
- **Refresh logic**: If a train is stationary, updates refresh the timestamp but do not show a (stale) direction arrow.

### Multi-BeaconRailroad Ambiguity
- A beacon may have multiple (BeaconID, SubdivisionId) entries, each with a different directional context.
- The direction must match the chosen subdivision; if the service picks subdivision 1 but the beacon's direction constraint is for subdivision 2, the arrow will be wrong.
- Always validate that BeaconRailroad.Direction aligns with the MapPin's chosen subdivision context.

## Testing Coverage

### Server-Side
- **DirectionServiceTests**: [Web/Web.ServerTests/Services/DirectionServiceTests.cs](../../Web/Web.ServerTests/Services/DirectionServiceTests.cs)
  - Tests compass direction calculations from lat/long pairs.
  - Tests direction constraint application (e.g., NorthSouth only).

- **MapPinServiceTests**: [Web/Web.ServerTests/Services/MapPinServiceTests.cs](../../Web/Web.ServerTests/Services/MapPinServiceTests.cs)
  - Tests direction is recalculated when UpsertMapPin is called.
  - Tests direction is set to null for stationary trains.

### Client-Side
- **BeaconLabelPin Component Tests**: Should verify (if present) that direction updates alongside timestamp.
- **SignalR Integration**: Should verify that MapPin DTOs with updated directions are received and rendered.

## Common Failure Modes and Recovery

| Issue | Symptom | Root Cause | Fix |
|-------|---------|-----------|-----|
| **Stale Arrow** | Arrow doesn't match new timestamp | Missing direction dependency in useMemo | Add actualDirection to dependency array |
| **Arrow Disappears** | Arrow was there, now blank | null direction from stationary timeout | Client correctly renders null as no arrow (expected) |
| **Wrong Arrow** | N shown but train is moving E | Wrong subdivision chosen in MapPinService | Validate BeaconRailroad.Direction constraint |
| **Sync Lag** | Timestamp refreshes but arrow lags | SignalR payload doesn't include direction | Check MapPin DTO schema on server and client |

## Data Structure: MapPin DTO
```csharp
public class MapPinDTO
{
    public int ID { get; set; }
    public int BeaconID { get; set; }
    public int SubdivisionId { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? Direction { get; set; }  // Compass direction: N, NE, E, SE, S, SW, W, NW, or null
    public bool Moving { get; set; }
    public List<Address> Addresses { get; set; }
    // ... other fields
}
```

## Future Enhancements
- Add direction confidence metric (high, medium, low) to indicate if calculation was ambiguous.
- Store direction history in MapPinHistory to track when direction last changed.
- Add bearing angle (0–360°) in addition to compass direction for map features like rotation icons.
- Add direction-change alerts when a train reverses unexpectedly.
