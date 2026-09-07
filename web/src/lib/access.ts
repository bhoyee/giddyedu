export type Entitlement = { enabled: boolean; limit: number | null };
export type PortalAudience = "SuperAdmin" | "SchoolAdmin" | "Teacher" | "Staff" | "Parent" | "Student" | "Accountant";
export type AccessContext = { userId: string; tenantId: string; campusId: string | null; roles: string[]; audiences: PortalAudience[]; defaultAudience: PortalAudience; permissions: string[]; entitlements: Record<string, Entitlement> };
export type PortalDashboard = { audience: PortalAudience; metrics: { key: string; label: string; value: number; href: string | null }[]; guidance: string };
export type PortalItem = { href: string; label: string; description: string; permission: string; feature?: string; audiences: PortalAudience[] };

export const portalItems: PortalItem[] = [
  { href: "/portal/subscription", label: "Subscription", description: "Plans and enabled features", permission: "TenantSettings.Manage", audiences: ["SuperAdmin", "SchoolAdmin", "Accountant"] },
  { href: "/portal/school", label: "School", description: "Profile and campuses", permission: "Schools.View", feature: "school-administration", audiences: ["SuperAdmin", "SchoolAdmin", "Teacher", "Staff"] },
  { href: "/portal/academics", label: "Academics", description: "Years, terms, classes and subjects", permission: "Academics.View", feature: "academic-structure", audiences: ["SchoolAdmin", "Teacher", "Staff", "Student"] },
  { href: "/portal/staff", label: "Staff", description: "People and positions", permission: "Staff.View", feature: "staff-management", audiences: ["SuperAdmin", "SchoolAdmin", "Teacher", "Staff"] },
  { href: "/portal/admissions", label: "Admissions", description: "Applicants and offers", permission: "Admissions.View", feature: "admissions", audiences: ["SchoolAdmin", "Staff"] },
  { href: "/portal/students", label: "Students", description: "Learners and enrolments", permission: "Students.View", feature: "student-information", audiences: ["SchoolAdmin", "Teacher", "Staff", "Parent", "Student"] },
  { href: "/portal/guardians", label: "Guardians", description: "Families and relationships", permission: "Guardians.View", feature: "guardian-management", audiences: ["SchoolAdmin", "Parent"] },
];

export const audienceCopy: Record<PortalAudience, { label: string; title: string; description: string }> = {
  SuperAdmin: { label: "Super Admin", title: "Platform administration", description: "Manage the GiddyEdu platform using explicitly granted platform capabilities." },
  SchoolAdmin: { label: "School Admin", title: "School operations", description: "Configure and operate this school within your granted permissions and subscription." },
  Teacher: { label: "Teacher", title: "Teaching workspace", description: "Access assigned classes and permitted learner information." },
  Staff: { label: "Staff", title: "Staff workspace", description: "Access school resources available to your staff role and campus." },
  Parent: { label: "Parent", title: "Family workspace", description: "Access only children and family records linked to your account." },
  Student: { label: "Student", title: "Student workspace", description: "Access learning and school information made available to your account." },
  Accountant: { label: "Accountant / Bursar", title: "Finance workspace", description: "Access subscribed financial capabilities explicitly granted to your role." },
};

export function visiblePortalItems(access: AccessContext, audience: PortalAudience) { const permissions = new Set(access.permissions); return portalItems.filter(item => item.audiences.includes(audience) && permissions.has(item.permission) && (!item.feature || access.entitlements[item.feature]?.enabled === true)); }
