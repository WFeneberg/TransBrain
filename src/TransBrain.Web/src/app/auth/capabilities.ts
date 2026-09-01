/**
 * What a role is allowed to DO. Mirror of the server-side policies in
 * TransBrain.Api/Program.cs:134-137 - if the two ever disagree, this table is the wrong one:
 * the API decides, this only decides what is worth offering.
 */
export type Capability = 'read' | 'masterData.write' | 'dispatch.write' | 'tourStatus.write';

/** The realm roles defined in src/TransBrain.AppHost/realms/transbrain-realm.json. */
export type AppRole = 'admin' | 'disponent' | 'fahrer' | 'viewer';

/** The four functional areas of the application. */
export type Area = 'vehicles' | 'drivers' | 'orders' | 'tours';

const CAPABILITIES_BY_ROLE: Record<AppRole, readonly Capability[]> = {
    admin: ['read', 'masterData.write', 'dispatch.write', 'tourStatus.write'],
    disponent: ['read', 'dispatch.write', 'tourStatus.write'],
    fahrer: ['read', 'tourStatus.write'],
    viewer: ['read'],
};

/**
 * What a role NEEDS to see, which is a different question from what it is allowed to read.
 * Policies.Read covers all four roles, so a fahrer may read the vehicle master data - but they
 * do not work with it, so it is not on their home page or in their navigation. Typing the URL
 * still works; see the guards in auth/capability.guard.ts.
 */
const AREAS_BY_ROLE: Record<AppRole, readonly Area[]> = {
    admin: ['vehicles', 'drivers', 'orders', 'tours'],
    disponent: ['vehicles', 'drivers', 'orders', 'tours'],
    fahrer: ['tours'],
    viewer: ['vehicles', 'drivers', 'orders', 'tours'],
};

const ALL_ROLES = Object.keys(CAPABILITIES_BY_ROLE) as AppRole[];

/** Keeps the realm roles this app knows and drops the rest, rather than failing on a stranger. */
export function knownRoles(roles: readonly string[]): AppRole[] {
    return ALL_ROLES.filter((role) => roles.includes(role));
}

/**
 * Roles are UNIONED, not picked: a user holding two roles gets both sets. A user holding no
 * known role gets nothing, which matches the API's fail-closed SetFallbackPolicy.
 */
export function capabilitiesFor(roles: readonly string[]): ReadonlySet<Capability> {
    const granted = new Set<Capability>();
    for (const role of knownRoles(roles)) {
        for (const capability of CAPABILITIES_BY_ROLE[role]) {
            granted.add(capability);
        }
    }
    return granted;
}

export function areasFor(roles: readonly string[]): ReadonlySet<Area> {
    const relevant = new Set<Area>();
    for (const role of knownRoles(roles)) {
        for (const area of AREAS_BY_ROLE[role]) {
            relevant.add(area);
        }
    }
    return relevant;
}
