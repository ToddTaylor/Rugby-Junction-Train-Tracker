import { describe, it, expect } from 'vitest';
import { parseSessionRoles, hasRole, isAdmin } from './roles';

describe('parseSessionRoles', () => {
  it('parses comma-separated roles correctly', () => {
    const roles = parseSessionRoles('admin,custodian,viewer');
    expect(roles).toEqual(['admin', 'custodian', 'viewer']);
  });

  it('handles single role', () => {
    const roles = parseSessionRoles('admin');
    expect(roles).toEqual(['admin']);
  });

  it('handles roles with whitespace', () => {
    const roles = parseSessionRoles('admin , custodian , viewer');
    expect(roles).toEqual(['admin', 'custodian', 'viewer']);
  });

  it('converts roles to lowercase', () => {
    const roles = parseSessionRoles('Admin,CUSTODIAN,Viewer');
    expect(roles).toEqual(['admin', 'custodian', 'viewer']);
  });

  it('handles empty/null input', () => {
    expect(parseSessionRoles('')).toEqual([]);
    expect(parseSessionRoles(null)).toEqual([]);
    expect(parseSessionRoles(undefined)).toEqual([]);
  });

  it('filters out empty strings from split', () => {
    const roles = parseSessionRoles('admin,,custodian');
    expect(roles).toEqual(['admin', 'custodian']);
  });

  it('handles whitespace-only roles', () => {
    const roles = parseSessionRoles('admin,   ,custodian');
    expect(roles).toEqual(['admin', 'custodian']);
  });
});

describe('hasRole', () => {
  it('checks if role exists in roles array', () => {
    const roles = ['admin', 'custodian', 'viewer'];
    expect(hasRole(roles, 'admin')).toBe(true);
    expect(hasRole(roles, 'custodian')).toBe(true);
  });

  it('returns false for missing role', () => {
    const roles = ['admin', 'custodian'];
    expect(hasRole(roles, 'superuser')).toBe(false);
  });

  it('is case-insensitive', () => {
    const roles = ['admin', 'custodian'];
    expect(hasRole(roles, 'ADMIN')).toBe(true);
    expect(hasRole(roles, 'Admin')).toBe(true);
  });

  it('handles empty roles array', () => {
    expect(hasRole([], 'admin')).toBe(false);
  });
});

describe('isAdmin', () => {
  it('returns true when admin role is present', () => {
    expect(isAdmin('admin,custodian')).toBe(true);
    expect(isAdmin('admin')).toBe(true);
  });

  it('returns false when admin role is not present', () => {
    expect(isAdmin('custodian,viewer')).toBe(false);
    expect(isAdmin('viewer')).toBe(false);
  });

  it('is case-insensitive for admin role', () => {
    expect(isAdmin('ADMIN')).toBe(true);
    expect(isAdmin('Admin')).toBe(true);
  });

  it('handles null/undefined/empty input', () => {
    expect(isAdmin(null)).toBe(false);
    expect(isAdmin(undefined)).toBe(false);
    expect(isAdmin('')).toBe(false);
  });
});
