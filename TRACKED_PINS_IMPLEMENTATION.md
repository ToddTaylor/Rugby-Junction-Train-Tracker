# Tracked Map Pins Persistence Implementation

## Overview
This implementation adds database persistence for tracked map pins, allowing users' tracking preferences to be stored server-side on a per-user basis. Previously, tracked pins were only stored in the browser's localStorage and were not persistent across sessions or devices.

## Architecture

### Backend Changes

#### 1. Database Entity (`UserTrackedPin.cs`)
- **Location**: `Web.Server/Entities/UserTrackedPin.cs`
- **Fields**:
  - `ID`: Primary key
  - `UserId`: Foreign key to User (cascade delete)
  - `MapPinId`: Foreign key to MapPin (cascade delete)
  - `BeaconID`: Optional beacon ID for reference
  - `BeaconName`: Optional beacon name for reference
  - `Symbol`: User-defined tracking symbol (max 10 chars, uppercase)
  - `Color`: Assigned tracking color
  - `ExpiresUtc`: Expiration timestamp (12-hour default)
  - `CreatedAt`, `LastUpdate`: Audit timestamps

#### 2. Database Context
- **File**: `Web.Server/Data/TelemetryDbContext.cs`
- **Changes**:
  - Added `DbSet<UserTrackedPin> UserTrackedPins`
  - Configured relationships with cascade delete for User and MapPin

#### 3. Repository Pattern
- **Interface**: `IUserTrackedPinRepository` - `Web.Server/Repositories/IUserTrackedPinRepository.cs`
- **Implementation**: `UserTrackedPinRepository` - `Web.Server/Repositories/UserTrackedPinRepository.cs`
- **Methods**:
  - `GetByIdAsync(int id)`: Retrieve a specific tracked pin
  - `GetByUserIdAsync(int userId)`: Get all valid (non-expired) pins for a user
  - `GetByUserAndMapPinAsync(int userId, int mapPinId)`: Get specific user-pin combination
  - `AddAsync()`: Insert new tracked pin
  - `UpdateAsync()`: Update existing tracked pin
  - `DeleteAsync()`: Remove by ID
  - `DeleteByUserAndMapPinAsync()`: Remove user's tracking of specific pin
  - `GetExpiredAsync()`: Find all expired entries
  - `DeleteExpiredAsync()`: Cleanup routine

#### 4. Service Layer
- **Interface**: `IUserTrackedPinService` - `Web.Server/Services/IUserTrackedPinService.cs`
- **Implementation**: `UserTrackedPinService` - `Web.Server/Services/UserTrackedPinService.cs`
- **Features**:
  - Automatic 12-hour expiration calculation
  - Logging of all operations
  - Error handling
  - Expiration cleanup

#### 5. API Controller
- **File**: `Web.Server/Controllers/v1/UserTrackedPinsController.cs`
- **Endpoints**:
  - `GET /api/v1/UserTrackedPins` - Get user's tracked pins
  - `POST /api/v1/UserTrackedPins` - Add new tracked pin
  - `PATCH /api/v1/UserTrackedPins/{mapPinId}/symbol` - Update symbol
  - `DELETE /api/v1/UserTrackedPins/{mapPinId}` - Remove tracked pin
- **Authentication**: Requires valid auth token; user ID extracted from HttpContext.Items

#### 6. DTOs
- **File**: `Web.Server/DTOs/UserTrackedPinDTO.cs`
- **File**: `Web.Server/DTOs/TrackedPinRequestDTOs.cs`
- Contains: `AddTrackedPinRequestDTO`, `UpdateTrackedPinSymbolRequestDTO`

#### 7. Service Registration
- **File**: `Web.Server/Program.cs`
- Added registrations:
  - `AddScoped<IUserTrackedPinRepository, UserTrackedPinRepository>()`
  - `AddScoped<IUserTrackedPinService, UserTrackedPinService>()`

#### 8. AutoMapper Profile
- **File**: `Web.Server/Mappers/AutoMapperProfile.cs`
- Added mappings for `UserTrackedPin` ↔ `UserTrackedPinDTO`

#### 9. Database Migration
- **File**: `Web.Server/Migrations/[timestamp]_AddUserTrackedPins.cs`
- Creates `UserTrackedPins` table with appropriate indexes and constraints

### Frontend Changes

#### 1. Auth Service
- **File**: `web.client/src/services/auth.ts`
- **Function**: `getAuthToken()` - Retrieves Bearer token from cookie or sessionStorage
- Used by tracked pins service for API authentication

#### 2. Updated Tracked Pins Service
- **File**: `web.client/src/services/trackedPins.ts`
- **Changes**:
  - `getTrackedMapPins()`: Now async; fetches from API, falls back to localStorage
  - `addTrackedMapPin()`: Adds locally first, then persists to API
  - `removeTrackedMapPin()`: Removes locally first, then deletes from API
  - `updateTrackedPinSymbol()`: Updates locally first, then persists to API
- **Offline Support**: Falls back to localStorage if API is unavailable
- **Type**: Added `mapPinId` field to `TrackedPin` type

## Data Flow

### Tracking a Pin (User clicks "Not Tracking")
1. **UI**: User clicks tracking link in map marker popup
2. **Component**: `TelemetryMarker.tsx` opens `TrackSymbolModal`
3. **Modal**: User enters optional symbol and clicks Save
4. **Service**: `addTrackedMapPin(id, beaconID, beaconName, symbol)` is called
5. **Local Storage**: Pin added immediately for UI responsiveness
6. **API**: Async POST to `/api/v1/UserTrackedPins` with pin data
7. **Backend**: `UserTrackedPinsController.AddTrackedPin()` creates database record
8. **Database**: `UserTrackedPin` entity inserted with 12-hour expiration

### Updating a Pin's Symbol
1. **UI**: User clicks tracking symbol to edit
2. **Modal**: User modifies symbol
3. **Service**: `updateTrackedPinSymbol(id, symbol)` is called
4. **Local Storage**: Updated immediately
5. **API**: Async PATCH to `/api/v1/UserTrackedPins/{mapPinId}/symbol`
6. **Backend**: Symbol updated in database

### Untracking a Pin
1. **UI**: User clicks tracking indicator → symbol modal → Untrack button
2. **Service**: `removeTrackedMapPin(id)` is called
3. **Local Storage**: Removed immediately
4. **API**: Async DELETE to `/api/v1/UserTrackedPins/{mapPinId}`
5. **Backend**: Record deleted from database

### Loading Tracked Pins on Page Load
1. **UI**: App initializes
2. **Service**: `getTrackedMapPins()` is called (typically in useEffect)
3. **API**: Fetches from `/api/v1/UserTrackedPins`
4. **Backend**: Returns all non-expired pins for current user
5. **Local Storage**: Synced with API response
6. **Fallback**: If API fails, uses cached localStorage data

## Key Features

1. **Per-User Persistence**: Each user's tracked pins are stored separately
2. **12-Hour Expiration**: Tracked pins automatically expire after 12 hours
3. **Color Assignment**: Automatic color selection from available palette
4. **Symbol Support**: Users can add custom symbols (e.g., train numbers)
5. **Offline Support**: Falls back to localStorage if API unavailable
6. **Optimistic Updates**: UI updates immediately while API persists in background
7. **Cross-Device Sync**: Users see same tracked pins across all devices
8. **Audit Trail**: CreatedAt/LastUpdate timestamps for all records
9. **Clean Cleanup**: Expired pins can be cleaned up via `CleanupExpiredAsync()`

## API Response Format

### GET /api/v1/UserTrackedPins
```json
{
  "data": [
    {
      "id": 1,
      "userId": 5,
      "mapPinId": 123,
      "beaconID": 456,
      "beaconName": "Crossing A",
      "symbol": "TRAIN42",
      "color": "#FF3366",
      "expiresUtc": "2026-01-03T14:30:00Z",
      "createdAt": "2026-01-03T02:30:00Z",
      "lastUpdate": "2026-01-03T02:30:00Z"
    }
  ],
  "errors": []
}
```

### POST /api/v1/UserTrackedPins
Request:
```json
{
  "mapPinId": 123,
  "beaconID": 456,
  "beaconName": "Crossing A",
  "symbol": "TRAIN42",
  "color": "#FF3366"
}
```

### PATCH /api/v1/UserTrackedPins/{mapPinId}/symbol
Request:
```json
{
  "symbol": "UPDATED"
}
```

## Error Handling

- **No Authentication**: Returns 400 "User not authenticated"
- **Invalid Parameters**: Returns 400 with validation error
- **Database Errors**: Returns 500 with generic error message
- **API Failures**: Frontend falls back to localStorage cache
- **Expired Tokens**: AuthTokenMiddleware validates and returns 401

## Future Enhancements

1. Add automatic cleanup scheduled task for expired pins
2. Add batch operations (add/remove multiple pins)
3. Add tracking history/audit log
4. Add pin expiration extension endpoint
5. Add pin sharing between users
6. Add configurable expiration times
7. Add analytics on tracking patterns

## Testing Checklist

- [ ] Track a pin and verify it appears in database
- [ ] Untrack a pin and verify it's removed from database
- [ ] Update symbol and verify it syncs
- [ ] Reload page and verify pins are loaded from database
- [ ] Log in on different device and verify pins are there
- [ ] Test offline mode (disable API, should use localStorage)
- [ ] Test expiration (wait 12 hours or modify time)
- [ ] Verify API returns correct user's pins only
- [ ] Test concurrent tracking/untracking
- [ ] Verify color assignment doesn't repeat
