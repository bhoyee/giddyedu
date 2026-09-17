"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";

type Staff = { id: string; staffNumber: string; firstName: string; lastName: string; category: number; campusId: string; departmentId: string | null; positionId: string | null; workEmail: string | null; phone: string | null; hireDate: string; status: number };
type Position = { id: string; name: string; category: number; isActive: boolean };
type Option = { id: string; name: string };
type ClassSection = Option & { isActive: boolean; classLevelName: string; academicYearName: string };
type ClassSubject = { classSectionId: string; subjectId: string; subjectName: string };
type Structure = { classSections: ClassSection[]; classSubjects: ClassSubject[] };
type Assignment = { id: string; staffId: string; classSectionId: string; subjectId: string | null; role: number };
type Role = { id: string; name: string };
type RoleAccess = { membershipId: string; roleIds: string[] };

export function StaffAssignmentsPanel({ staffId, view = "all", onChanged }: { staffId: string; view?: "all" | "position" | "assignments"; onChanged?: () => void }) {
  const [staff, setStaff] = useState<Staff | null>(null);
  const [positions, setPositions] = useState<Position[]>([]);
  const [structure, setStructure] = useState<Structure>({ classSections: [], classSubjects: [] });
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [roleAccess, setRoleAccess] = useState<RoleAccess | null>(null);
  const [canManage, setCanManage] = useState(false);
  const [canManageRoles, setCanManageRoles] = useState(false);
  const [positionId, setPositionId] = useState("");
  const [classSectionId, setClassSectionId] = useState("");
  const [assignmentRole, setAssignmentRole] = useState("0");
  const [busy, setBusy] = useState(false);

  const loadAssignments = useCallback(async () => {
    const response = await fetch("/api/backend/hr/teaching-assignments", { cache: "no-store" });
    if (response.ok) setAssignments((await response.json() as Assignment[]).filter(item => item.staffId === staffId));
  }, [staffId]);
  const loadRoleAccess = useCallback(async () => {
    const response = await fetch(`/api/backend/hr/staff/${staffId}/role-access`, { cache: "no-store" });
    if (response.ok) setRoleAccess(await response.json() as RoleAccess | null);
  }, [staffId]);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      const permissions = session.access?.permissions ?? [];
      setCanManage(permissions.includes("Staff.Manage"));
      setCanManageRoles(permissions.includes("Staff.Manage") && permissions.includes("Roles.Manage"));
    });
    void fetch(`/api/backend/hr/staff/${staffId}`, { cache: "no-store" }).then(async response => {
      if (response.ok) { const record = await response.json() as Staff; setStaff(record); setPositionId(record.positionId ?? ""); }
    });
    void fetch("/api/backend/hr/positions", { cache: "no-store" }).then(async response => { if (response.ok) setPositions(await response.json() as Position[]); });
    void fetch("/api/backend/academics/structure", { cache: "no-store" }).then(async response => { if (response.ok) setStructure(await response.json() as Structure); });
    void Promise.resolve().then(loadAssignments);
    void fetch("/api/backend/foundation/roles", { cache: "no-store" }).then(async response => { if (response.ok) setRoles(await response.json() as Role[]); });
    void Promise.resolve().then(loadRoleAccess);
  }, [loadAssignments, loadRoleAccess, staffId]);

  async function savePosition(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!staff || !canManage) return;
    setBusy(true);
    const response = await fetch(`/api/backend/hr/staff/${staffId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ firstName: staff.firstName, lastName: staff.lastName, category: staff.category, campusId: staff.campusId, departmentId: staff.departmentId, positionId: positionId || null, email: staff.workEmail, phone: staff.phone, hireDate: staff.hireDate, status: staff.status }) });
    setBusy(false);
    if (response.ok) { setStaff({ ...staff, positionId: positionId || null }); notify({ title: "Position updated", message: "The staff member's primary position was saved." }); onChanged?.(); }
    else notify({ tone: "error", title: "Position not changed", message: "Check the selected position and your permissions." });
  }
  async function addAssignment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    setBusy(true);
    const response = await fetch("/api/backend/hr/teaching-assignments", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ staffId, classSectionId, subjectId: assignmentRole === "0" ? data.get("subjectId") : null, role: Number(assignmentRole) }) });
    setBusy(false);
    if (response.ok) { notify({ title: "Class responsibility saved", message: "The teaching assignment was added." }); await loadAssignments(); }
    else notify({ tone: "error", title: "Assignment not saved", message: "The staff member must be active teaching staff, and the subject must belong to the class." });
  }
  async function removeAssignment(id: string) {
    if (!window.confirm("Remove this teaching assignment?")) return;
    const response = await fetch(`/api/backend/hr/teaching-assignments/${id}`, { method: "DELETE" });
    if (response.ok) { notify({ title: "Assignment removed", message: "The class responsibility was removed." }); await loadAssignments(); }
    else notify({ tone: "error", title: "Assignment not removed", message: "Check your permissions and try again." });
  }
  async function changeRole(roleId: string, assigned: boolean) {
    if (!roleAccess) return;
    const response = await fetch(assigned ? `/api/backend/foundation/role-assignments/${roleAccess.membershipId}/${roleId}` : "/api/backend/foundation/role-assignments", { method: assigned ? "DELETE" : "POST", headers: { "Content-Type": "application/json" }, body: assigned ? undefined : JSON.stringify({ membershipId: roleAccess.membershipId, roleId }) });
    if (response.ok) { notify({ title: "Role assignment updated", message: "The staff member's access role was saved." }); await loadRoleAccess(); }
    else notify({ tone: "error", title: "Role not changed", message: "This role change may be restricted or another role manager may be required." });
  }

  const subjects = structure.classSubjects.filter(item => item.classSectionId === classSectionId);
  return <div className="space-y-6">
    {(view === "all" || view === "position") && <section id="staff-position" className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><h2 className="text-xl font-black text-slate-950">Role and position</h2><p className="mt-1 text-sm text-slate-500">A position describes the staff member’s job; an access role controls permissions in GiddyEdu.</p>
      {canManage && staff && <form onSubmit={savePosition} className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-end"><label className="min-w-0 flex-1 text-xs font-bold text-slate-700">Primary position<select value={positionId} onChange={event => setPositionId(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">No position</option>{positions.filter(item => item.category === staff.category && item.isActive).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><button disabled={busy} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">Save position</button></form>}
      {canManageRoles && <div className="mt-6 border-t border-slate-100 pt-5"><h3 className="text-sm font-bold text-slate-900">Workspace access roles</h3>{!roleAccess ? <p className="mt-2 text-sm text-slate-500">Link the staff member to a user account before assigning access roles.</p> : <div className="mt-3 flex flex-wrap gap-2">{roles.map(role => { const assigned = roleAccess.roleIds.includes(role.id); return <button key={role.id} type="button" onClick={() => void changeRole(role.id, assigned)} aria-pressed={assigned} className={`rounded-full border px-3 py-1.5 text-xs font-bold ${assigned ? "tenant-primary-bg text-white" : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>{role.name}{assigned ? " ✓" : " +"}</button>; })}</div>}</div>}
    </section>}
    {(view === "all" || view === "assignments") && <section id="staff-assignments" className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><h2 className="text-xl font-black text-slate-950">Classes and subjects</h2><p className="mt-1 text-sm text-slate-500">Assign active teaching staff to class or subject responsibilities.</p>
      {canManage && <form onSubmit={addAssignment} className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><label className="text-xs font-bold text-slate-700">Class section<select required value={classSectionId} onChange={event => setClassSectionId(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">Select class</option>{structure.classSections.filter(item => item.isActive).map(item => <option key={item.id} value={item.id}>{item.classLevelName} · {item.name} ({item.academicYearName})</option>)}</select></label><label className="text-xs font-bold text-slate-700">Responsibility<select value={assignmentRole} onChange={event => setAssignmentRole(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="0">Subject teacher</option><option value="1">Class teacher</option><option value="2">Form teacher</option></select></label>{assignmentRole === "0" && <label className="text-xs font-bold text-slate-700">Subject<select name="subjectId" required className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">Select subject</option>{subjects.map(item => <option key={item.subjectId} value={item.subjectId}>{item.subjectName}</option>)}</select></label>}<button disabled={busy} className="tenant-primary-bg self-end rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">Add assignment</button></form>}
      <div className="mt-5 space-y-2">{assignments.length ? assignments.map(item => { const section = structure.classSections.find(value => value.id === item.classSectionId); const subject = structure.classSubjects.find(value => value.classSectionId === item.classSectionId && value.subjectId === item.subjectId); return <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 px-4 py-3 text-sm"><span><strong>{section?.name ?? "Class section"}</strong> · {item.role === 0 ? `Subject teacher${subject ? ` · ${subject.subjectName}` : ""}` : item.role === 1 ? "Class teacher" : "Form teacher"}</span>{canManage && <button type="button" onClick={() => void removeAssignment(item.id)} className="text-xs font-bold text-red-700 hover:underline">Remove</button>}</div>; }) : <p className="text-sm text-slate-500">No teaching assignments yet.</p>}</div>
    </section>}
  </div>;
}
