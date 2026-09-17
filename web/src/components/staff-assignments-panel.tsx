"use client";

import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { notify } from "@/components/app-toast";
import { confirmAction } from "@/components/confirm-action";

type Position = { id: string; name: string; category: number; isActive: boolean };
type StaffPosition = { positionId: string; name: string; category: number; isPrimary: boolean };
type Option = { id: string; name: string };
type ClassSection = Option & { isActive: boolean; classLevelName: string; academicYearName: string };
type ClassSubject = { classSectionId: string; subjectId: string; subjectName: string };
type Structure = { classSections: ClassSection[]; classSubjects: ClassSubject[] };
type Assignment = { id: string; staffId: string; classSectionId: string; subjectId: string | null; role: number };
type Role = { id: string; name: string };
type RoleAccess = { membershipId: string; roleIds: string[] };
const positionCategories = [
  { id: 0, label: "Teaching" },
  { id: 2, label: "Non-teaching" },
  { id: 1, label: "Administrative" }
] as const;

export function StaffAssignmentsPanel({ staffId, view = "all", onChanged }: { staffId: string; view?: "all" | "position" | "assignments"; onChanged?: () => void }) {
  const [positions, setPositions] = useState<Position[]>([]);
  const [staffPositions, setStaffPositions] = useState<StaffPosition[]>([]);
  const [additionalPositionId, setAdditionalPositionId] = useState("");
  const [structure, setStructure] = useState<Structure>({ classSections: [], classSubjects: [] });
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [roleAccess, setRoleAccess] = useState<RoleAccess | null>(null);
  const [canManage, setCanManage] = useState(false);
  const [canManageRoles, setCanManageRoles] = useState(false);
  const [editingPrimary, setEditingPrimary] = useState(false);
  const [primaryCategory, setPrimaryCategory] = useState("");
  const [primaryPositionId, setPrimaryPositionId] = useState("");
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
  const loadStaffPositions = useCallback(async () => {
    const response = await fetch(`/api/backend/hr/staff/${staffId}/positions`, { cache: "no-store" });
    if (response.ok) setStaffPositions(await response.json() as StaffPosition[]);
  }, [staffId]);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      const permissions = session.access?.permissions ?? [];
      setCanManage(permissions.includes("Staff.Manage"));
      setCanManageRoles(permissions.includes("Staff.Manage") && permissions.includes("Roles.Manage"));
    });
    void fetch("/api/backend/hr/positions", { cache: "no-store" }).then(async response => { if (response.ok) setPositions(await response.json() as Position[]); });
    void fetch("/api/backend/academics/structure", { cache: "no-store" }).then(async response => { if (response.ok) setStructure(await response.json() as Structure); });
    void Promise.resolve().then(loadAssignments);
    void fetch("/api/backend/foundation/roles", { cache: "no-store" }).then(async response => { if (response.ok) setRoles(await response.json() as Role[]); });
    void Promise.resolve().then(loadRoleAccess);
    void Promise.resolve().then(loadStaffPositions);
  }, [loadAssignments, loadRoleAccess, loadStaffPositions, staffId]);

  async function updateAdditionalPosition(positionId: string, action: "add" | "remove" | "primary") {
    if (!canManage || busy) return;
    setBusy(true);
    try {
      const response = await fetch(action === "primary" ? `/api/backend/hr/staff/${staffId}/positions/primary` : action === "remove" ? `/api/backend/hr/staff/${staffId}/positions/${positionId}` : `/api/backend/hr/staff/${staffId}/positions`, {
        method: action === "add" ? "POST" : action === "remove" ? "DELETE" : "PUT",
        headers: action === "remove" ? undefined : { "Content-Type": "application/json" },
        body: action === "remove" ? undefined : JSON.stringify({ positionId })
      });
      if (!response.ok) throw new Error(await readError(response, "The position could not be changed."));
      await loadStaffPositions();
      if (action === "primary") {
        setEditingPrimary(false);
      }
      setAdditionalPositionId("");
      notify({ title: "Staff positions updated", message: action === "add" ? "Additional position added." : action === "remove" ? "Additional position removed." : "Primary position updated." });
      onChanged?.();
    } catch (reason) { notify({ tone: "error", title: "Position not changed", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); }
  }

  async function addAssignment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    setBusy(true);
    const response = await fetch("/api/backend/hr/teaching-assignments", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ staffId, classSectionId, subjectId: assignmentRole === "0" ? data.get("subjectId") : null, role: Number(assignmentRole) }) });
    setBusy(false);
    if (response.ok) { notify({ title: "Class responsibility saved", message: "The teaching assignment was added." }); await loadAssignments(); }
    else notify({ tone: "error", title: "Assignment not saved", message: await readError(response, "Staff must be active and have a teaching position; the subject must belong to the class.") });
  }
  async function removeAssignment(id: string) {
    if (!await confirmAction({ title: "Delete teaching assignment?", message: "This class or subject responsibility will be permanently deleted from the staff member. This cannot be undone.", confirmLabel: "Delete assignment", confirmText: "DELETE" })) return;
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
  const availablePositions = positions.filter(item => item.isActive && !staffPositions.some(assigned => assigned.positionId === item.id));
  const primaryPosition = staffPositions.find(item => item.isPrimary);
  return <div className="space-y-6">
    {(view === "all" || view === "position") && <section id="staff-position" className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><h2 className="text-xl font-black text-slate-950">Role and position</h2>
      <div className="mt-5 border-t border-slate-100 pt-5"><h3 className="text-sm font-bold text-slate-900">Employment positions</h3><p className="mt-1 text-xs text-slate-500">Keep one primary position for the staff directory. Add other duties here, including positions from a different category.</p>
        <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50/70 p-4">
          <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-[11px] font-black uppercase tracking-wide text-slate-500">Primary position</p><p className="mt-1 text-sm font-bold text-slate-900">{primaryPosition?.name ?? "Not assigned"}</p></div>{canManage && <button type="button" disabled={busy} onClick={() => { setEditingPrimary(current => !current); setPrimaryCategory(""); setPrimaryPositionId(""); }} className={`rounded-lg px-3 py-2 text-xs font-bold text-white disabled:opacity-50 ${editingPrimary ? "bg-red-700 hover:bg-red-800" : "tenant-primary-bg hover:brightness-105"}`}>{editingPrimary ? "Cancel" : "Change"}</button>}</div>
          {editingPrimary && <form onSubmit={event => { event.preventDefault(); if (primaryPositionId) void updateAdditionalPosition(primaryPositionId, "primary"); }} className="mt-4 grid gap-3 border-t border-slate-200 pt-4 sm:grid-cols-2"><label className="text-xs font-bold text-slate-700">Category<select required value={primaryCategory} onChange={event => { setPrimaryCategory(event.target.value); setPrimaryPositionId(""); }} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">Select category</option>{positionCategories.map(category => <option key={category.id} value={category.id}>{category.label}</option>)}</select></label><label className="text-xs font-bold text-slate-700">Position<select required disabled={!primaryCategory} value={primaryPositionId} onChange={event => setPrimaryPositionId(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm disabled:bg-slate-100 disabled:text-slate-400"><option value="">{primaryCategory ? "Select position" : "Select a category first"}</option>{primaryCategory && positions.filter(item => item.isActive && item.category === Number(primaryCategory)).sort((left, right) => left.name.localeCompare(right.name)).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><button disabled={busy || !primaryPositionId || primaryPositionId === primaryPosition?.positionId} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50 sm:col-span-2 sm:justify-self-end">Save primary position</button></form>}
        </div>
        <div className="mt-3 space-y-2">{staffPositions.filter(item => !item.isPrimary).map(item => <div key={item.positionId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3"><div className="min-w-0"><p className="text-sm font-bold text-slate-900">{item.name}</p><p className="mt-0.5 text-xs text-slate-500">{positionCategories.find(category => category.id === item.category)?.label ?? "Staff"} · Additional position</p></div>{canManage && <div className="flex gap-2"><button type="button" disabled={busy} onClick={() => void updateAdditionalPosition(item.positionId, "primary")} className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-bold tenant-primary-text disabled:opacity-50">Make primary</button><button type="button" disabled={busy} onClick={async () => { if (await confirmAction({ title: `Remove ${item.name}?`, message: "This additional position will be unlinked from the staff member. The position remains available for other staff.", confirmLabel: "Remove position" })) await updateAdditionalPosition(item.positionId, "remove"); }} className="rounded-lg border border-red-200 bg-white px-3 py-2 text-xs font-bold text-red-700 disabled:opacity-50">Remove</button></div>}</div>)}</div>
        {canManage && <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-end"><div className="min-w-0 flex-1"><SearchablePositionSelect positions={availablePositions} value={additionalPositionId} onChange={setAdditionalPositionId} /></div><button type="button" disabled={busy || !additionalPositionId} onClick={() => void updateAdditionalPosition(additionalPositionId, "add")} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">Add position</button></div>}
      </div>
      {canManageRoles && <div className="mt-6 border-t border-slate-100 pt-5"><h3 className="text-sm font-bold text-slate-900">Workspace access roles</h3>{!roleAccess ? <p className="mt-2 text-sm text-slate-500">Link the staff member to a user account before assigning access roles.</p> : <div className="mt-3 flex flex-wrap gap-2">{roles.map(role => { const assigned = roleAccess.roleIds.includes(role.id); return <button key={role.id} type="button" onClick={() => void changeRole(role.id, assigned)} aria-pressed={assigned} className={`rounded-full border px-3 py-1.5 text-xs font-bold ${assigned ? "tenant-primary-bg text-white" : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>{role.name}{assigned ? " ✓" : " +"}</button>; })}</div>}</div>}
    </section>}
    {(view === "all" || view === "assignments") && <section id="staff-assignments" className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><h2 className="text-xl font-black text-slate-950">Class and subject assignments</h2><p className="mt-1 text-sm text-slate-500">Teaching responsibilities assigned to this staff member. Other operational duties are not yet managed here.</p>
      {canManage && staffPositions.some(item => item.category === 0) && <form onSubmit={addAssignment} className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><label className="text-xs font-bold text-slate-700">Class section<select required value={classSectionId} onChange={event => setClassSectionId(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">Select class</option>{structure.classSections.filter(item => item.isActive).map(item => <option key={item.id} value={item.id}>{item.classLevelName} · {item.name} ({item.academicYearName})</option>)}</select></label><label className="text-xs font-bold text-slate-700">Responsibility<select value={assignmentRole} onChange={event => setAssignmentRole(event.target.value)} className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="0">Subject teacher</option><option value="1">Class teacher</option><option value="2">Form teacher</option></select></label>{assignmentRole === "0" && <label className="text-xs font-bold text-slate-700">Subject<select name="subjectId" required className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"><option value="">Select subject</option>{subjects.map(item => <option key={item.subjectId} value={item.subjectId}>{item.subjectName}</option>)}</select></label>}<button disabled={busy} className="tenant-primary-bg self-end rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">Add assignment</button></form>}
      {canManage && staffPositions.length > 0 && !staffPositions.some(item => item.category === 0) && <p className="mt-5 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">Add a teaching position under Role &amp; position before assigning classes or subjects.</p>}
      <div className="mt-5 space-y-2">{assignments.length ? assignments.map(item => { const section = structure.classSections.find(value => value.id === item.classSectionId); const subject = structure.classSubjects.find(value => value.classSectionId === item.classSectionId && value.subjectId === item.subjectId); return <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 px-4 py-3 text-sm"><span><strong>{section?.name ?? "Class section"}</strong> · {item.role === 0 ? `Subject teacher${subject ? ` · ${subject.subjectName}` : ""}` : item.role === 1 ? "Class teacher" : "Form teacher"}</span>{canManage && <button type="button" onClick={() => void removeAssignment(item.id)} className="text-xs font-bold text-red-700 hover:underline">Remove</button>}</div>; }) : <p className="text-sm text-slate-500">No teaching assignments yet.</p>}</div>
    </section>}
  </div>;
}
function SearchablePositionSelect({ positions, value, onChange }: { positions: Position[]; value: string; onChange: (value: string) => void }) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);
  const selected = positions.find(item => item.id === value);

  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [open]);

  const matching = positions.filter(item => item.name.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()));
  return <div ref={containerRef} className="relative text-xs font-bold text-slate-700" onKeyDown={event => { if (event.key === "Escape") setOpen(false); }}>
    <span id="additional-position-label">Add another position</span>
    <button type="button" aria-labelledby="additional-position-label" aria-haspopup="listbox" aria-expanded={open} onClick={() => { setSearch(""); setOpen(current => !current); }} className="mt-1 flex w-full items-center justify-between rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-left text-sm font-normal text-slate-900 hover:border-slate-400"><span className={selected ? "truncate" : "truncate text-slate-500"}>{selected?.name ?? "Select a position"}</span><span aria-hidden="true" className="ml-2 text-slate-500">⌄</span></button>
    {open && <div className="absolute left-0 top-full z-40 mt-1 w-full rounded-xl border border-slate-200 bg-white p-2 shadow-xl"><input autoFocus value={search} onChange={event => setSearch(event.target.value)} placeholder="Search positions" aria-label="Search positions" className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm font-normal outline-none focus:border-[var(--tenant-primary,#28654a)]" /><div role="listbox" aria-labelledby="additional-position-label" className="mt-2 max-h-64 overflow-y-auto">{positionCategories.map(category => {
      const items = matching.filter(item => item.category === category.id).sort((left, right) => left.name.localeCompare(right.name));
      return items.length ? <div key={category.id} role="group" aria-label={category.label}><p className="sticky top-0 bg-slate-50 px-3 py-2 text-xs font-black text-slate-800">{category.label}</p>{items.map(item => <button key={item.id} type="button" role="option" aria-selected={value === item.id} onClick={() => { onChange(item.id); setOpen(false); }} className="block w-full rounded-lg px-4 py-2 text-left text-sm font-normal text-slate-700 hover:bg-slate-100 focus:bg-slate-100">{item.name}</button>)}</div> : null;
    })}{matching.length === 0 && <p className="px-3 py-3 text-sm font-normal text-slate-500">No matching positions.</p>}</div></div>}
  </div>;
}
async function readError(response: Response, fallback: string) { try { const body = await response.json() as { detail?: string; message?: string; title?: string }; return body.detail ?? body.message ?? body.title ?? fallback; } catch { return fallback; } }
