import { describe, expect, it } from 'vitest';
import { validateTelemetryStaleHoursOverride } from './AdminBeaconRailroads';

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
