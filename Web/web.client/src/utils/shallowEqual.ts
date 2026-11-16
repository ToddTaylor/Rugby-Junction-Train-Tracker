/** True when both objects have the same keys and each value is identical (Object.is). */
export function shallowEqual(a: object, b: object): boolean {
    const aKeys = Object.keys(a);
    if (aKeys.length !== Object.keys(b).length) return false;
    return aKeys.every(key =>
        Object.prototype.hasOwnProperty.call(b, key)
        && Object.is((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key]));
}

/**
 * React.memo comparison for map markers. The map rebuilds its pin objects whenever any pin changes, so `pin`
 * is compared field by field; its nested arrays keep their identity until that pin itself changes. Every other
 * prop is compared by identity.
 */
export function propsEqualWithShallowPin<P extends { pin: object }>(prev: Readonly<P>, next: Readonly<P>): boolean {
    return shallowEqual(prev.pin, next.pin) && shallowEqual({ ...prev, pin: null }, { ...next, pin: null });
}
