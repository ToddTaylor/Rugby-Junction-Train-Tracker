import { describe, expect, it } from 'vitest';
import { validateTelemetryStaleHoursOverride, canEditOfflineNote } from './AdminBeaconRailroads';

describe('validateTelemetryStaleHoursOverride', () => {
  it('returns null for null (no override)', () => {
    expect(validateTelemetryStaleHoursOverride(null)).toBeNull();
  });

  it('returns null for undefined (no override)', () => {
    expect(validateTelemetryStaleHoursOverride(undefined)).toBeNull();
  });

  it('returns null for a valid positive integer', () => {
    expect(validateTelemetryStaleHoursOverride(6)).toBeNull();
    expect(validateTelemetryStaleHoursOverride(1)).toBeNull();
    expect(validateTelemetryStaleHoursOverride(24)).toBeNull();
  });

  it('returns an error for zero', () => {
    const error = validateTelemetryStaleHoursOverride(0);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a negative value', () => {
    const error = validateTelemetryStaleHoursOverride(-1);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a non-integer (float)', () => {
    const error = validateTelemetryStaleHoursOverride(1.5);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a negative float', () => {
    const error = validateTelemetryStaleHoursOverride(-0.5);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });
});

describe('canEditOfflineNote', () => {
  it('allows admins regardless of subdivision custodian', () => {
    expect(canEditOfflineNote(true, false, 999, 1)).toBe(true);
    expect(canEditOfflineNote(true, false, null, 1)).toBe(true);
  });

  it('allows a custodian assigned to the subdivision', () => {
    expect(canEditOfflineNote(false, true, 50, 50)).toBe(true);
  });

  it('denies a custodian not assigned to the subdivision', () => {
    expect(canEditOfflineNote(false, true, 999, 50)).toBe(false);
  });

  it('denies a custodian when the subdivision has no assigned custodian', () => {
    expect(canEditOfflineNote(false, true, null, 50)).toBe(false);
    expect(canEditOfflineNote(false, true, undefined, 50)).toBe(false);
  });

  it('denies a user who is neither admin nor custodian', () => {
    expect(canEditOfflineNote(false, false, 50, 50)).toBe(false);
  });
});
