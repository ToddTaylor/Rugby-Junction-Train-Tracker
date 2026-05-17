/**
 * Returns normalized role flags derived from a raw roles array.
 * Comparison is case-insensitive to match the server's stored role names
 * (Admin, Custodian, Viewer).
 */
export function parseSessionRoles(roles: string[] | undefined | null) {
    const normalized = (roles ?? []).map(r => r.toLowerCase());
    return {
        isAdmin: normalized.includes('admin'),
        isCustodian: normalized.includes('custodian'),
        /** True for roles that may see support-only data (HOT/EOT/DPU addresses). */
        canViewSupportAddresses: normalized.includes('admin') || normalized.includes('custodian'),
    };
}
