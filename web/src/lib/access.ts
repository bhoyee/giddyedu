export type Entitlement = { enabled: boolean; limit: number | null };
export type AccessContext = { userId: string; tenantId: string; campusId: string | null; permissions: string[]; entitlements: Record<string, Entitlement> };
export type PortalItem = { href: string; label: string; description: string; permission: string; feature?: string };

export const portalItems: PortalItem[] = [
  { href: "/portal/subscription", label: "Subscription", description: "Plans and enabled features", permission: "TenantSettings.Manage" },
  { href: "/portal/school", label: "School", description: "Profile and campuses", permission: "Schools.View", feature: "school-administration" },
  { href: "/portal/academics", label: "Academics", description: "Years, terms, classes and subjects", permission: "Academics.View", feature: "academic-structure" },
  { href: "/portal/staff", label: "Staff", description: "People and positions", permission: "Staff.View", feature: "staff-management" },
  { href: "/portal/admissions", label: "Admissions", description: "Applicants and offers", permission: "Admissions.View", feature: "admissions" },
  { href: "/portal/students", label: "Students", description: "Learners and enrolments", permission: "Students.View", feature: "student-information" },
  { href: "/portal/guardians", label: "Guardians", description: "Families and relationships", permission: "Guardians.View", feature: "guardian-management" },
];

export function visiblePortalItems(access: AccessContext) { const permissions = new Set(access.permissions); return portalItems.filter(item => permissions.has(item.permission) && (!item.feature || access.entitlements[item.feature]?.enabled === true)); }
