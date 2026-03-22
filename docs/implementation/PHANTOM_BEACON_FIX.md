# Phantom Beacon Detection Requirements

## Purpose

This document explains the phantom beacon fix in plain language, focusing on business requirements and expected behavior rather than implementation details.

The goal is to prevent false train position updates caused by beacon misreads while still allowing valid movement updates during real-world outages.

## Problem Statement

In a known failure scenario, NEENAH can appear to detect a train that is actually much farther south. This causes the train pin to jump to the wrong location.

Key mileposts in this scenario:

- FOND DU LAC: 157.26
- NORTH FOND DU LAC: 160.53
- OSHKOSH: 172.8
- NEENAH: 184.8

If a train appears to jump directly from FOND DU LAC to NEENAH, the gap is 27.54 miles and skips valid intermediate beacons.

## Requirements

### R1: Prevent impossible beacon jumps

The system must reject updates that skip intermediate beacons on the same railroad when the jump is larger than the configured sequence threshold.

Current threshold requirement:

- Sequence gap threshold is 15 miles.

Expected behavior:

- A jump larger than 15 miles with active intermediate beacons must be rejected.
- A jump larger than 15 miles with no active intermediate beacons may continue to later checks.

### R2: Do not let offline beacons block valid movement

The system must ignore intermediate beacons that have been offline too long, so dead hardware does not block legitimate train movement.

Current offline requirement:

- Intermediate beacons older than 24 hours are considered offline for sequence validation.

Expected behavior:

- Active intermediates still block impossible jumps.
- Offline intermediates do not count as blockers.

### R3: Enforce per-beacon distance limits

The system must support an optional maximum detection distance per beacon mapping to prevent nearby false steals that do not trigger sequence checks.

Expected behavior:

- If a maximum distance is configured, updates beyond that distance must be rejected.
- If no maximum distance is configured, this check is not applied.
- This check applies only within the same railroad context.

### R4: Apply checks in stable order

The system must evaluate map pin checks in a consistent order so results are predictable and auditable.

Required order:

1. Sequence skip validation
2. Maximum detection distance validation
3. Trackage rights validation

## Configuration Requirements

### C1: Admin configurability

Operations users must be able to set and edit Max Detection Distance from the Admin Beacon Railroads screen.

Expected behavior:

- Blank value means no max distance limit.
- Positive numeric value enables max distance filtering.
- Value changes made in admin must persist to the database on update.

### C2: Backward compatibility

Existing beacon railroad records must remain valid without requiring immediate configuration updates.

Expected behavior:

- Null max distance remains supported.
- Existing flows continue to operate when no max distance is set.

## Scenario Outcomes

| Scenario | Description | Sequence Check | Offline Beacon Rule | Max Distance Rule | Final Outcome |
|----------|-------------|----------------|---------------------|-------------------|---------------|
| Scenario A | Direct FOND DU LAC to NEENAH jump with NORTH FOND DU LAC and OSHKOSH active | ❌ Fails | ✅ Not relevant | ✅ Not reached | ❌ Reject update |
| Scenario B | Large jump with NORTH FOND DU LAC offline but OSHKOSH still active | ❌ Fails | ✅ Works as intended | ✅ Not reached | ❌ Reject update |
| Scenario C | Large jump with both NORTH FOND DU LAC and OSHKOSH offline | ✅ Passes | ✅ Works as intended | Depends on beacon setting | Depends on later checks |
| Scenario D | OSHKOSH to NEENAH movement near 12 miles with max distance set to 10 | ✅ Passes | ✅ Not relevant | ❌ Fails | ❌ Reject update |
| Scenario E | OSHKOSH to NEENAH movement near 12 miles with max distance set to 12 or more | ✅ Passes | ✅ Not relevant | ✅ Passes | ✅ Allow update |
| Scenario F | Large jump where no max distance is configured and no active intermediates exist | ✅ Passes | ✅ Works as intended | ✅ Passes | Depends on trackage and other downstream checks |

Interpretation:

- ✅ means the scenario passes that requirement or the requirement behaves correctly for that case.
- ❌ means the scenario is blocked by that requirement.
- "Not reached" means an earlier requirement already rejected the update.
- "Depends on later checks" means the scenario is not rejected at that step and continues through the remaining validation flow.

## User-Facing Expectations

From an operations perspective:

- Train pins should not jump unrealistically to distant beacons.
- Real movements should still flow when intermediate hardware is down.
- Admin users can tune strictness by beacon using Max Detection Distance.
- Clearing Max Detection Distance disables that limit for the selected beacon mapping.

## Operational Guidance

When tuning values:

- Use larger values only when a beacon is known to legitimately detect farther distances.
- Keep values conservative to reduce false steals.
- Revisit values when beacon hardware, antenna setup, or terrain conditions change.

## Validation Status

The feature set is covered by unit tests for:

- Sequence jump rejection
- Offline intermediate handling
- Max detection distance filtering
- Persistence of Max Detection Distance updates

## Maintenance Notes

This document intentionally avoids source code snippets.

To keep this document current over time:

- Update thresholds here whenever business requirements change.
- Update scenario mileposts here if route reference values change.
- Keep implementation-specific details in code comments and tests, not in this requirements document.

