"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { notify } from "@/components/app-toast";
import { confirmAction } from "@/components/confirm-action";
import { reviewGuardianStudentLinks } from "@/components/guardian-unlink-dialog";
import { GuardianRelationshipDialog, GuardianStatusDialog, LinkGuardianStudentDialog, type GuardianStudent } from "@/components/guardian-management-dialogs";
import { StaffEmailComposer } from "@/components/staff-email-composer";

type Guardian = { id: string; firstName: string; lastName: string; phone: string; email: string | null; hasAccount?: boolean; status?: number; linkedStudents?: GuardianStudent[]; deletedAtUtc?: string; deletedByName?: string | null };
type Page = { items: Guardian[]; total: number };
type Props = { version: number; binMode: boolean; onBinCountChange: (count: number) => void; onCanManageChange: (allowed: boolean) => void };
type SortKey = "lastName" | "firstName" | "phone" | "email";
type ClassLevel = { id: string; name: string; displayOrder?: number };
type ClassSection = { id: string; classLevelId: string; name: string; isActive?: boolean };
const pageSize = 20;

async function responseError(response: Response) {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  return body?.detail ?? body?.title ?? `Request failed (${response.status}).`;
}

export function GuardianDirectory({ version, binMode, onBinCountChange, onCanManageChange }: Props) {
  const [rows, setRows] = useState<Guardian[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<SortKey>("lastName");
  const [descending, setDescending] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [canInvite, setCanInvite] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const [actionsOpen, setActionsOpen] = useState(false);
  const [menu, setMenu] = useState<{ row: Guardian; top: number; right: number } | null>(null);
  const [linking, setLinking] = useState<Guardian | null>(null);
  const [relationships, setRelationships] = useState<Guardian | null>(null);
  const [statusIds, setStatusIds] = useState<string[] | null>(null);
  const [mailRecipients, setMailRecipients] = useState<{ name: string; email: string }[] | null>(null);
  const [classLevels, setClassLevels] = useState<ClassLevel[]>([]);
  const [classSections, setClassSections] = useState<ClassSection[]>([]);
  const [classLevelId, setClassLevelId] = useState("");
  const [classSectionId, setClassSectionId] = useState("");
  const [revision, setRevision] = useState(0);
  const actionsRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const refresh = useCallback(() => { setRevision(value => value + 1); setSelected([]); setMenu(null); setActionsOpen(false); }, []);

  useEffect(() => { const timer = window.setTimeout(() => { setQuery(search.trim()); setPage(1); }, 250); return () => window.clearTimeout(timer); }, [search]);
  useEffect(() => {
    const controller = new AbortController();
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (!binMode) { params.set("sort", sort); params.set("descending", String(descending)); }
    if (query) params.set("search", query);
    if (!binMode && classLevelId) params.set("classLevelId", classLevelId);
    if (!binMode && classSectionId) params.set("classSectionId", classSectionId);
    const path = binMode ? "guardians/bin" : "guardians";
    void fetch(`/api/backend/${path}?${params}`, { cache: "no-store", signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error(response.status === 403 ? "You do not have access to guardian records." : await responseError(response));
      const data = await response.json() as Page; setRows(data.items); setTotal(data.total); setError("");
    }).catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Guardian records could not be loaded."); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, query, sort, descending, revision, version, binMode, classLevelId, classSectionId]);
  useEffect(() => {
    if (binMode) return;
    void fetch("/api/backend/academics/structure", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const structure = await response.json() as { classLevels?: ClassLevel[]; classSections?: ClassSection[] };
      setClassLevels((structure.classLevels ?? []).sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0)));
      setClassSections((structure.classSections ?? []).filter(section => section.isActive !== false));
    });
  }, [binMode]);
  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      const permissions = session.access?.permissions ?? [];
      setCanInvite(permissions.includes("Users.Manage")); setCanManage(permissions.includes("Guardians.Manage"));
      onCanManageChange(permissions.includes("Guardians.Manage"));
    });
  }, [onCanManageChange]);
  useEffect(() => {
    if (!canManage) return;
    void fetch("/api/backend/guardians/bin/count", { cache: "no-store" }).then(async response => {
      if (response.ok) onBinCountChange((await response.json() as { count: number }).count);
    });
  }, [canManage, revision, version, binMode, onBinCountChange]);
  useEffect(() => {
    if (!menu && !actionsOpen) return;
    const dismiss = (event: PointerEvent) => {
      const target = event.target as Element;
      if (menu && !menuRef.current?.contains(target) && !target.closest("[data-guardian-menu-trigger]")) setMenu(null);
      if (actionsOpen && !actionsRef.current?.contains(target)) setActionsOpen(false);
    };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") { setMenu(null); setActionsOpen(false); } };
    document.addEventListener("pointerdown", dismiss); document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("pointerdown", dismiss); document.removeEventListener("keydown", escape); };
  }, [menu, actionsOpen]);

  function changeSort(next: SortKey) { setLoading(true); setPage(1); if (sort === next) setDescending(value => !value); else { setSort(next); setDescending(false); } }
  function toggleMenu(row: Guardian, button: HTMLButtonElement) {
    if (menu?.row.id === row.id) { setMenu(null); return; }
    const bounds = button.getBoundingClientRect();
    setMenu({ row, top: Math.max(8, Math.min(bounds.bottom + 6, window.innerHeight - 180)), right: Math.max(8, window.innerWidth - bounds.right) });
  }
  async function invite(row: Guardian) {
    setMenu(null);
    try {
      const response = await fetch("/api/backend/account-invitations", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ targetType: 1, targetId: row.id }) });
      if (!response.ok) throw new Error(await responseError(response));
      notify({ title: "Invitation queued", message: `A secure account invitation was queued for ${row.email}.` }); refresh();
    } catch (reason) { notify({ tone: "error", title: "Invitation not sent", message: reason instanceof Error ? reason.message : "Please try again." }); }
  }
  async function exportSelected() {
    setActionsOpen(false); setBusy(true);
    try {
      const response = await fetch("/api/backend/guardians/export.csv", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ guardianIds: selected }) });
      if (!response.ok) throw new Error(await responseError(response));
      const url = URL.createObjectURL(await response.blob()); const link = document.createElement("a");
      link.href = url; link.download = "giddyedu-guardians.csv"; link.click(); window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      notify({ title: "Export ready", message: `${selected.length} selected guardian records exported.` });
    } catch (reason) { notify({ tone: "error", title: "Export failed", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); }
  }
  async function updateStatus(ids: string[], status: number) {
    setBusy(true);
    try {
      for (const id of ids) {
        const response = await fetch(`/api/backend/guardians/${id}/status`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ status }) });
        if (!response.ok) throw new Error(await responseError(response));
      }
      notify({ title: "Status updated", message: `${ids.length} guardian record${ids.length === 1 ? " was" : "s were"} updated.` }); refresh();
    } catch (reason) { notify({ tone: "error", title: "Status not updated", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); }
  }
  function composeMail(guardians: Guardian[]) {
    const recipients = guardians.filter(row => row.email).map(row => ({ name: `${row.firstName} ${row.lastName}`, email: row.email! }));
    if (recipients.length === 0) { notify({ tone: "error", title: "No email recipients", message: "The selected guardian records do not have email addresses." }); return; }
    setMailRecipients(recipients); setMenu(null); setActionsOpen(false);
  }
  async function unlinkStudent(row: Guardian, student: GuardianStudent) {
    const studentName = [student.firstName, student.middleName, student.lastName].filter(Boolean).join(" ");
    const confirmed = await confirmAction({ title: "Unlink student from guardian?", message: `${studentName} will no longer be connected to ${row.firstName} ${row.lastName}. Neither record will be deleted.`, confirmLabel: "Unlink student" });
    if (!confirmed) return;
    setBusy(true);
    try {
      const response = await fetch(`/api/backend/guardians/${row.id}/student-links/${student.studentId}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await responseError(response));
      notify({ title: "Student unlinked", message: `${studentName} was unlinked from this guardian.` }); refresh();
    } catch (reason) { notify({ tone: "error", title: "Student not unlinked", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); }
  }
  async function changeBin(ids: string[], operation: "move" | "restore" | "permanent") {
    setActionsOpen(false); setMenu(null);
    const confirmed = await confirmAction({
      title: operation === "permanent" ? "Permanently delete guardian?" : operation === "restore" ? "Restore guardian?" : "Move guardian to bin?",
      message: operation === "permanent" ? "This removes the guardian record permanently. This cannot be undone." : operation === "restore" ? "The guardian will return to the active directory." : "The guardian will be hidden from the active directory. If students are linked, you will be asked to confirm unlinking them first.",
      confirmLabel: operation === "permanent" ? "Permanently delete" : operation === "restore" ? "Restore" : "Move to bin",
      ...(operation === "permanent" ? { confirmText: "DELETE" } : {}),
    });
    if (!confirmed) return;
    setBusy(true); let completed = 0;
    try {
      for (const id of ids) {
        const path = operation === "move" ? `/api/backend/guardians/${id}` : `/api/backend/guardians/${id}/${operation}`;
        let response = await fetch(path, { method: operation === "restore" ? "POST" : "DELETE" });
        if (!response.ok && (operation === "move" || operation === "permanent")) {
          const detail = await responseError(response);
          if (detail.includes("Unlink the student records")) {
            if (operation === "move") {
              const readyToMove = await reviewGuardianStudentLinks(id);
              if (!readyToMove) throw new Error(`${completed} of ${ids.length} completed. The remaining operation was cancelled.`);
              response = await fetch(path, { method: "DELETE" });
            } else {
              const unlink = await confirmAction({ title: "Guardian has linked students", message: `${detail} Continuing will remove those family links. The students themselves will not be deleted.`, confirmLabel: "Unlink and permanently delete", confirmText: "DELETE" });
              if (!unlink) throw new Error(`${completed} of ${ids.length} completed. The remaining operation was cancelled.`);
              response = await fetch(`${path}?unlinkStudents=true`, { method: "DELETE" });
            }
          } else throw new Error(`${completed} of ${ids.length} completed. ${detail}`);
        }
        if (!response.ok) throw new Error(`${completed} of ${ids.length} completed. ${await responseError(response)}`);
        completed++;
      }
      notify({ title: operation === "move" ? "Moved to bin" : operation === "restore" ? "Guardian restored" : "Guardian deleted", message: `${completed} guardian records updated.` });
    } catch (reason) { notify({ tone: "error", title: "Action could not finish", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); refresh(); }
  }

  const visible = rows;
  const visibleIds = visible.map(row => row.id);
  const allSelected = visibleIds.length > 0 && visibleIds.every(id => selected.includes(id));
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
    <div className="flex flex-wrap items-end justify-between gap-4 border-b border-slate-200 p-4 sm:p-5">
      <div><p className="text-xs font-black uppercase tracking-widest tenant-primary-text">{binMode ? "Recycle bin" : "Directory"}</p><h2 className="mt-1 text-xl font-black text-slate-950">{binMode ? "Deleted guardians" : "Parents and guardians"}</h2><p className="mt-1 text-sm text-slate-500">{total} record{total === 1 ? "" : "s"}</p></div>
      <div className="flex w-full flex-wrap items-end gap-2 sm:w-auto">
        {canManage && selected.length > 0 && <><button type="button" onClick={() => setSelected([])} className="h-11 text-xs font-bold text-slate-700 hover:underline">Clear selection</button><div ref={actionsRef} className="relative"><button type="button" disabled={busy} onClick={() => setActionsOpen(value => !value)} aria-expanded={actionsOpen} className="tenant-primary-bg h-11 rounded-xl px-4 text-xs font-bold text-white shadow-sm hover:brightness-95 disabled:opacity-50">Actions ▾</button>{actionsOpen && <div className="absolute left-0 top-full z-30 mt-2 min-w-48 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl">{binMode ? <><button type="button" onClick={() => void changeBin(selected, "restore")} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50">↺ Restore selected</button><button type="button" onClick={() => void changeBin(selected, "permanent")} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-red-700 hover:bg-red-50">⌫ Permanently delete</button></> : <><button type="button" onClick={() => { setActionsOpen(false); setStatusIds(selected); }} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50">● Status</button><button type="button" onClick={() => composeMail(rows.filter(row => selected.includes(row.id)))} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50">✉ Email</button><button type="button" onClick={() => void exportSelected()} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50">⇩ Export</button><button type="button" onClick={() => void changeBin(selected, "move")} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-red-700 hover:bg-red-50">⌫ Move to bin</button></>}</div>}</div></>}
        {!binMode && <><label className="w-full text-xs font-bold text-slate-600 sm:w-44">Class level<select value={classLevelId} onChange={event => { setClassLevelId(event.target.value); setClassSectionId(""); setPage(1); setLoading(true); }} className="mt-1 block h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-normal text-slate-950 outline-none focus:border-slate-700"><option value="">All class levels</option>{classLevels.map(level => <option key={level.id} value={level.id}>{level.name}</option>)}</select></label><label className="w-full text-xs font-bold text-slate-600 sm:w-48">Class section<select value={classSectionId} disabled={!classLevelId} onChange={event => { setClassSectionId(event.target.value); setPage(1); setLoading(true); }} className="mt-1 block h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-normal text-slate-950 outline-none focus:border-slate-700 disabled:bg-slate-100 disabled:text-slate-400"><option value="">{classLevelId ? "All sections" : "Select class level first"}</option>{classSections.filter(section => section.classLevelId === classLevelId).map(section => <option key={section.id} value={section.id}>{section.name}</option>)}</select></label></>}
        <label className="min-w-0 flex-1 text-xs font-bold text-slate-600 sm:w-64">Search guardians<input value={search} onChange={event => { setSearch(event.target.value); setLoading(true); }} placeholder="Name, email or phone" className="mt-1 block h-11 w-full rounded-xl border border-slate-300 px-4 text-sm font-normal text-slate-950 outline-none focus:border-slate-700" /></label>
      </div>
    </div>
    {error ? <p role="alert" className="p-6 text-sm text-red-700">{error}</p> : loading ? <p className="p-6 text-sm text-slate-500">Loading guardians...</p> : visible.length === 0 ? <p className="p-6 text-sm text-slate-500">{binMode ? "No guardians in the bin." : "No guardians match this search."}</p> : <div className="overflow-x-auto"><table className="w-full min-w-[860px] text-left text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500"><tr>
      {canManage && <th scope="col" className="w-12 px-4 py-4"><input type="checkbox" aria-label="Select all visible guardians" checked={allSelected} onChange={() => setSelected(ids => allSelected ? ids.filter(id => !visibleIds.includes(id)) : [...new Set([...ids, ...visibleIds])])} className="size-4 accent-emerald-700" /></th>}
      {([["lastName", "Guardian"], ["phone", "Phone"]] as const).map(([key, label]) => <th key={key} scope="col" className="px-4 py-4">{binMode ? label : <button type="button" onClick={() => changeSort(key)} className="inline-flex items-center gap-2 font-bold hover:text-slate-950" aria-label={`Sort by ${label}`}>{label}<span aria-hidden="true">{sort === key ? descending ? "↓" : "↑" : "↕"}</span></button>}</th>)}
      {!binMode && <><th scope="col" className="px-4 py-4">Status</th><th scope="col" className="px-4 py-4">Linked students</th></>}<th scope="col" className="px-4 py-4">{binMode ? "Moved to bin" : "Account"}</th><th scope="col" className="px-4 py-4 text-right">Actions</th>
    </tr></thead><tbody>{visible.map(row => <tr key={row.id} className="border-t border-slate-100 hover:bg-slate-50/80">
      {canManage && <td className="px-4 py-4"><input type="checkbox" aria-label={`Select ${row.firstName} ${row.lastName}`} checked={selected.includes(row.id)} onChange={() => setSelected(ids => ids.includes(row.id) ? ids.filter(id => id !== row.id) : [...ids, row.id])} className="size-4 accent-emerald-700" /></td>}
      <td className="px-4 py-4"><div className="flex items-center gap-2.5"><span className="inline-flex size-9 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-black text-amber-900">{row.firstName.charAt(0)}{row.lastName.charAt(0)}</span><div className="min-w-0"><Link href={`/portal/guardians/${row.id}`} className="font-black text-slate-900 hover:underline">{row.firstName} {row.lastName}</Link><p className="max-w-52 truncate text-[11px] font-medium text-slate-500">{row.email ?? "No email address"}</p></div></div></td><td className="px-4 py-4 text-slate-700">{row.phone}</td>
      {!binMode && <><td className="px-4 py-4"><span title="Record status controls whether the guardian is available for normal school operations. It does not indicate portal login activity." className={`rounded-full px-2.5 py-1 text-xs font-bold ${row.status === 2 ? "bg-red-100 text-red-800" : row.status === 1 ? "bg-slate-200 text-slate-700" : "bg-emerald-100 text-emerald-800"}`}>{row.status === 2 ? "Suspended" : row.status === 1 ? "Inactive" : "Active record"}</span></td><td className="px-4 py-4"><div className="max-w-64 space-y-1">{(row.linkedStudents?.length ?? 0) === 0 ? <span className="text-xs font-semibold text-slate-400">No student linked</span> : row.linkedStudents?.map(student => <div key={student.studentId} className="flex min-w-0 items-center gap-1.5"><button type="button" disabled={busy} onClick={() => void unlinkStudent(row, student)} title={`Unlink ${student.firstName} ${student.lastName}`} aria-label={`Unlink ${student.firstName} ${student.lastName} from ${row.firstName} ${row.lastName}`} className="shrink-0 text-sm font-black text-red-600 hover:text-red-800 disabled:opacity-40">×</button><p className="truncate text-xs font-semibold text-slate-700" title={`${student.firstName} ${student.lastName} · ${student.classSectionName ?? "No class"}`}>{student.firstName} {student.lastName}<span className="font-normal text-slate-400"> · {student.classSectionName ?? "No class"}</span></p></div>)}</div></td></>}
      <td className="px-4 py-4">{binMode ? <span className="text-xs text-slate-600">{row.deletedAtUtc ? new Date(row.deletedAtUtc).toLocaleString() : "—"}<br />{row.deletedByName ? `By ${row.deletedByName}` : ""}</span> : <span title={row.hasAccount ? "A portal user account is linked to this guardian record." : "The guardian record exists, but no parent-portal user account has been linked yet."} className={`rounded-full px-2.5 py-1 text-xs font-bold ${row.hasAccount ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-900"}`}>{row.hasAccount ? "Portal connected" : "No portal account"}</span>}</td>
      <td className="px-4 py-4 text-right"><button type="button" data-guardian-menu-trigger onClick={event => toggleMenu(row, event.currentTarget)} aria-label={`Actions for ${row.firstName} ${row.lastName}`} aria-expanded={menu?.row.id === row.id} className="rounded-lg border border-slate-200 px-3 py-1 text-xl font-bold leading-6 text-slate-700 hover:bg-slate-100">···</button></td>
    </tr>)}</tbody></table></div>}
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4 text-xs text-slate-500"><span>Showing {total === 0 ? 0 : (page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}</span><div className="flex items-center gap-2"><button type="button" disabled={page <= 1 || loading} onClick={() => { setLoading(true); setPage(value => value - 1); setSelected([]); }} className="tenant-primary-bg rounded-xl px-4 py-2.5 font-bold text-white shadow-sm transition hover:brightness-95 disabled:cursor-not-allowed disabled:opacity-40">Previous</button><span className="px-2">{page} / {totalPages}</span><button type="button" disabled={page >= totalPages || loading} onClick={() => { setLoading(true); setPage(value => value + 1); setSelected([]); }} className="tenant-primary-bg rounded-xl px-4 py-2.5 font-bold text-white shadow-sm transition hover:brightness-95 disabled:cursor-not-allowed disabled:opacity-40">Next</button></div></div>
    {menu && createPortal(<div ref={menuRef} style={{ top: menu.top, right: menu.right }} className="fixed z-[110] w-56 rounded-xl border border-slate-200 bg-white p-1 text-left shadow-xl">{binMode ? <><button type="button" onClick={() => void changeBin([menu.row.id], "restore")} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">↺ Restore</button><button type="button" onClick={() => void changeBin([menu.row.id], "permanent")} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-red-700 hover:bg-red-50">✕ Permanently delete</button></> : <><Link href={`/portal/guardians/${menu.row.id}`} className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100">◉ View profile</Link>{canManage && <Link href={`/portal/guardians/${menu.row.id}#profile`} className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100">✎ Edit details</Link>}{canManage && <button type="button" onClick={() => { setLinking(menu.row); setMenu(null); }} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">＋ Link student</button>}{canManage && <button type="button" onClick={() => { setRelationships(menu.row); setMenu(null); }} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">⇄ Change guardian type</button>}{canManage && <button type="button" onClick={() => { setStatusIds([menu.row.id]); setMenu(null); }} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">● Change status</button>}<button type="button" onClick={() => composeMail([menu.row])} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">✉ Email</button>{canInvite && !menu.row.hasAccount && <button type="button" onClick={() => void invite(menu.row)} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">↗ Send portal invite</button>}{canManage && <button type="button" onClick={() => void changeBin([menu.row.id], "move")} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-red-700 hover:bg-red-50">⌫ Move to bin</button>}</>}</div>, document.body)}
    {linking && <LinkGuardianStudentDialog guardianId={linking.id} existingIds={(linking.linkedStudents ?? []).map(student => student.studentId)} onClose={() => setLinking(null)} onSaved={refresh} />}
    {relationships && <GuardianRelationshipDialog guardianId={relationships.id} students={relationships.linkedStudents ?? []} onClose={() => setRelationships(null)} onSaved={refresh} />}
    {statusIds && <GuardianStatusDialog count={statusIds.length} initialStatus={statusIds.length === 1 ? rows.find(row => row.id === statusIds[0])?.status : undefined} onClose={() => setStatusIds(null)} onSave={status => updateStatus(statusIds, status)} />}
    {mailRecipients && <StaffEmailComposer recipients={mailRecipients} skippedCount={0} audience="guardians" contextLabel="Guardian directory" onClose={() => setMailRecipients(null)} />}
  </section>;
}
