"use client";

/* eslint-disable @next/next/no-img-element -- Staff photo links are short-lived authorised URLs and cannot use image optimization. */

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { notify } from "@/components/app-toast";
import { confirmAction } from "@/components/confirm-action";
import { StaffEmailComposer } from "@/components/staff-email-composer";

type StaffRow = {
  id: string; userId: string | null; staffNumber: string; firstName: string; lastName: string;
  middleInitial: string | null; category: number; status: number; workEmail: string | null;
  phone: string | null; positionName: string | null; photoUrl: string | null; createdAtUtc: string; deletedAtUtc: string | null;
};
type StaffPage = { items: StaffRow[]; total: number };
type StaffBinDetail = { staff: StaffRow; deletedByUserId: string | null; deletedByName: string | null; activity: { action: string; actorUserId: string | null; actorName: string | null; occurredAtUtc: string }[] };
type RowMenu = { row: StaffRow; top: number; left: number };

const categories = [{ label: "All", value: "" }, { label: "Teaching", value: "0" }, { label: "Non-teaching", value: "2" }, { label: "Administrative", value: "1" }];
const statusNames = ["Active", "Suspended", "Exited", "Away", "Not cleared", "Inactive", "On leave", "Retired", "Resigned", "Sacked", "Left", "Deceased"];
const statusColors = [
  "bg-emerald-700 text-white", "bg-red-100 text-red-800", "bg-slate-200 text-slate-800",
  "bg-sky-100 text-sky-800", "bg-amber-100 text-amber-900", "bg-slate-100 text-slate-700",
  "bg-violet-100 text-violet-800", "bg-slate-200 text-slate-800", "bg-slate-200 text-slate-800",
  "bg-red-100 text-red-800", "bg-slate-200 text-slate-800", "bg-slate-300 text-slate-900",
];
const pageSize = 20;

export function StaffDirectoryTable({ version, binMode, onBinCountChange, onCanManageChange }: { version: number; binMode: boolean; onBinCountChange: (value: number) => void; onCanManageChange: (value: boolean) => void }) {
  const [rows, setRows] = useState<StaffRow[]>([]);
  const [total, setTotal] = useState(0);
  const [categoryCounts, setCategoryCounts] = useState<Record<string, number | null>>({});
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [sort, setSort] = useState("name_asc");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [canManageRoles, setCanManageRoles] = useState(false);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [menu, setMenu] = useState<RowMenu | null>(null);
  const [binDetail, setBinDetail] = useState<StaffBinDetail | null>(null);
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [statusValue, setStatusValue] = useState("");
  const [exitDate, setExitDate] = useState("");
  const [statusError, setStatusError] = useState("");
  const [emailRecipients, setEmailRecipients] = useState<{ name: string; email: string }[] | null>(null);
  const [emailSkippedCount, setEmailSkippedCount] = useState(0);
  const bulkActionsRef = useRef<HTMLDetailsElement>(null);
  const rowMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      setCanManage(session.access?.permissions?.includes("Staff.Manage") === true);
      onCanManageChange(session.access?.permissions?.includes("Staff.Manage") === true);
      setCanManageRoles(session.access?.permissions?.includes("Roles.Manage") === true);
    });
  }, [onCanManageChange]);

  useEffect(() => {
    if (!canManage) return;
    void fetch("/api/backend/hr/staff/bin/count", { cache: "no-store" }).then(async response => {
      if (response.ok) onBinCountChange((await response.json() as { count: number }).count);
    }).catch(() => undefined);
  }, [canManage, onBinCountChange, revision, version]);

  useEffect(() => {
    if (binMode) return;
    const controller = new AbortController();
    void Promise.all(categories.map(async option => {
      try {
        const params = new URLSearchParams({ page: "1", pageSize: "1" });
        if (option.value) params.set("category", option.value);
        const response = await fetch(`/api/backend/hr/staff?${params}`, { cache: "no-store", signal: controller.signal });
        return [option.value, response.ok ? (await response.json() as StaffPage).total : null] as const;
      } catch { return [option.value, null] as const; }
    })).then(counts => { if (!controller.signal.aborted) setCategoryCounts(Object.fromEntries(counts)); });
    return () => controller.abort();
  }, [binMode, revision, version]);

  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setLoading(true);
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), sort });
      if (!binMode && search.trim()) params.set("search", search.trim());
      if (!binMode && category) params.set("category", category);
      try {
        const response = await fetch(`/api/backend/hr/staff${binMode ? "/bin" : ""}?${params}`, { cache: "no-store", signal: controller.signal });
        if (!response.ok) throw new Error(response.status === 403 ? "You do not have access to this staff directory." : "The staff directory could not be loaded.");
        const data = await response.json() as StaffPage;
        setRows(data.items ?? []); setTotal(data.total ?? 0); setError("");
      } catch (reason) {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "The staff directory could not be loaded.");
      } finally { if (!controller.signal.aborted) setLoading(false); }
    }, search ? 250 : 0);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [binMode, category, page, revision, search, sort, version]);

  useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (bulkActionsRef.current?.open && !bulkActionsRef.current.contains(target)) bulkActionsRef.current.open = false;
      if (menu && !rowMenuRef.current?.contains(target) && !(target instanceof Element && target.closest('button[aria-label^="Actions for "]'))) setMenu(null);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      if (bulkActionsRef.current) bulkActionsRef.current.open = false;
      setMenu(null);
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => { document.removeEventListener("pointerdown", onPointerDown); document.removeEventListener("keydown", onKeyDown); };
  }, [menu]);

  useEffect(() => {
    if (!menu) return;
    const onScroll = () => setMenu(null);
    window.addEventListener("scroll", onScroll, true);
    return () => window.removeEventListener("scroll", onScroll, true);
  }, [menu]);

  const selectedRows = rows.filter(row => selected.has(row.id));
  const allSelected = rows.length > 0 && selectedRows.length === rows.length;
  const pages = Math.max(1, Math.ceil(total / pageSize));

  function changeFilter(value: string) { setCategory(value); setPage(1); setSelected(new Set()); setMenu(null); }
  function changeSort(value: string) { setSort(value); setPage(1); setSelected(new Set()); setMenu(null); }
  function toggleRow(id: string) { setSelected(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
  function togglePage() { setSelected(allSelected ? new Set() : new Set(rows.map(row => row.id))); }
  async function viewBinDetail(row: StaffRow) {
    setMenu(null);
    try {
      const response = await fetch(`/api/backend/hr/staff/bin/${row.id}`, { cache: "no-store" });
      if (!response.ok) throw new Error();
      setBinDetail(await response.json() as StaffBinDetail);
    } catch { notify({ tone: "error", title: "History unavailable", message: "This staff record could not be loaded. Try again." }); }
  }
  async function changeBinRecords(targets: StaffRow[], action: "delete" | "restore" | "permanent") {
    if (!targets.length || busy) return;
    const permanent = action === "permanent";
    const confirmed = await confirmAction({
      title: permanent ? `Permanently delete ${targets.length} staff record${targets.length === 1 ? "" : "s"}?` : action === "restore" ? `Restore ${targets.length} staff record${targets.length === 1 ? "" : "s"}?` : `Move ${targets.length} staff record${targets.length === 1 ? "" : "s"} to bin?`,
      message: permanent ? "This removes staff details and files and cannot be undone. Parent and student accounts are not deleted." : action === "restore" ? "The staff record and its previous school access will be restored." : "The records will leave the active directory and staff access will be suspended. You can restore them from the bin.",
      confirmLabel: permanent ? "Permanently delete" : action === "restore" ? "Restore staff" : "Move to bin",
      confirmText: permanent ? "DELETE" : undefined
    });
    if (!confirmed) return;
    setBusy(true);
    let changed = 0;
    let failureMessage = "";
    for (const row of targets) {
      try {
        const response = await fetch(`/api/backend/hr/staff/${row.id}${action === "permanent" ? "/permanent" : action === "restore" ? "/restore" : ""}`, { method: action === "restore" ? "POST" : "DELETE" });
        if (response.ok) changed++;
        else if (!failureMessage) {
          const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
          failureMessage = problem?.detail || problem?.title || "The server rejected this change.";
        }
      } catch { /* Report partial failures after attempting each selected record. */ }
    }
    setBusy(false); setSelected(new Set()); setMenu(null); setRevision(value => value + 1);
    notify({ tone: changed === targets.length ? "success" : "error", title: `${changed} of ${targets.length} staff records ${action === "restore" ? "restored" : action === "delete" ? "moved to bin" : "permanently deleted"}`, message: changed === targets.length ? "The staff directory is up to date." : failureMessage || "Some records could not be changed. Review them individually." });
  }
  function openMenu(row: StaffRow, button: HTMLButtonElement) {
    if (menu?.row.id === row.id) { setMenu(null); return; }
    const bounds = button.getBoundingClientRect();
    setMenu({ row, top: Math.max(8, Math.min(bounds.bottom + 6, window.innerHeight - 224)), left: Math.max(8, bounds.right - 190) });
  }
  async function copyStaffNumber(value: string) {
    try { await navigator.clipboard.writeText(value); notify({ title: "Staff ID copied", message: value }); }
    catch { notify({ tone: "error", title: "Could not copy staff ID", message: "Please select and copy the ID manually." }); }
  }
  async function inviteStaff(row: StaffRow) {
    try {
      const response = await fetch("/api/backend/account-invitations", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ targetType: 0, targetId: row.id }) });
      notify(response.ok ? { title: "Invitation queued", message: `A secure account invitation was queued for ${row.workEmail}.` } : { tone: "error", title: "Invitation not sent", message: "Check the staff email address and try again." });
    } catch { notify({ tone: "error", title: "Invitation not sent", message: "The service could not be reached. Please try again." }); }
  }
  function composeMail() {
    const recipients = selectedRows.filter(row => row.workEmail).map(row => ({ name: `${row.firstName} ${row.lastName}`, email: row.workEmail! }));
    if (!recipients.length) { notify({ tone: "error", title: "No email addresses", message: "The selected staff do not have email addresses." }); return; }
    setEmailSkippedCount(selectedRows.length - recipients.length);
    setEmailRecipients(recipients);
    if (bulkActionsRef.current) bulkActionsRef.current.open = false;
  }
  async function changeStatus() {
    const status = Number(statusValue);
    if (statusValue === "" || !statusNames[status] || (status === 2 && !exitDate)) {
      setStatusError(status === 2 ? "Choose an exit date." : "Choose a status before updating.");
      return;
    }
    setBusy(true);
    let changed = 0;
    let failureMessage = "";
    for (const row of selectedRows) {
      try {
        const response = await fetch(`/api/backend/hr/staff/${row.id}/status`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ status, exitDate: status === 2 ? exitDate : null }) });
        if (response.ok) changed++;
        else if (!failureMessage) {
          const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
          failureMessage = problem?.detail || problem?.title || "The server rejected this status change.";
        }
      } catch { failureMessage ||= "The service could not be reached for some records."; }
    }
    setBusy(false); setRevision(value => value + 1);
    if (changed === selectedRows.length) { setSelected(new Set()); setStatusDialogOpen(false); setStatusValue(""); setExitDate(""); setStatusError(""); }
    else setStatusError(failureMessage || "Some records could not be changed. Review them individually.");
    notify({ tone: changed === selectedRows.length ? "success" : "error", title: `${changed} of ${selectedRows.length} records updated`, message: changed === selectedRows.length ? `Staff status changed to ${statusNames[status]}.` : failureMessage || "Some records could not be changed." });
  }
  async function exportSelected() {
    setBusy(true);
    try {
      const response = await fetch("/api/backend/hr/staff/export.csv", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ staffIds: selectedRows.map(row => row.id) }) });
      if (!response.ok) throw new Error("The selected staff could not be exported.");
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement("a"); link.href = url; link.download = "giddyedu-staff.csv"; link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
      notify({ title: "Export ready", message: `${selectedRows.length} staff record${selectedRows.length === 1 ? "" : "s"} exported.` });
    } catch { notify({ tone: "error", title: "Export failed", message: "Check your permissions and try again." }); }
    finally { setBusy(false); }
  }

  return <section aria-label="Staff directory table">
    <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <div className="flex flex-wrap items-center gap-1.5" aria-label="Filter staff by category">{!binMode && categories.map(option => <button key={option.label} type="button" onClick={() => changeFilter(option.value)} aria-pressed={category === option.value} className={`inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-xs font-bold transition ${category === option.value ? "tenant-primary-bg text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200"}`}>{option.label}{categoryCounts[option.value] != null && <span className={`rounded-full px-1.5 py-0.5 text-[10px] leading-none ${category === option.value ? "bg-white/20 text-white" : "bg-white text-slate-500"}`}>{categoryCounts[option.value]}</span>}</button>)}</div>
      {!binMode && <div className="flex flex-col gap-2 sm:flex-row"><label className="sr-only" htmlFor="staff-search">Search staff</label><input id="staff-search" type="search" value={search} onChange={event => { setSearch(event.target.value); setPage(1); setSelected(new Set()); }} placeholder="Search name, ID, email or phone" className="min-w-0 rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm outline-none focus:border-[var(--tenant-primary,#28654a)] sm:w-64"/><label className="sr-only" htmlFor="staff-sort">Sort staff</label><select id="staff-sort" value={sort} onChange={event => changeSort(event.target.value)} className="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-700"><option value="name_asc">Name A–Z</option><option value="name_desc">Name Z–A</option><option value="added_desc">Newest first</option><option value="added_asc">Oldest first</option><option value="staff_number">Staff ID</option></select></div>}
    </div>
    <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-500">
      <span>{loading ? "Loading staff…" : `${total.toLocaleString()} ${binMode ? "deleted" : "active"} staff member${total === 1 ? "" : "s"}`}{selected.size ? ` · ${selected.size} selected on this page` : ""}</span>
      {selected.size > 0 && <div className="flex items-center gap-2">
        <button type="button" onClick={() => setSelected(new Set())} className="font-bold text-slate-700 hover:underline">Clear selection</button>
        {canManage && <details ref={bulkActionsRef} className="relative"><summary className="tenant-primary-bg cursor-pointer list-none rounded-lg px-3 py-2 font-bold text-white">Actions ▾</summary>
          <div className="absolute right-0 z-30 mt-1 w-52 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl">
            {binMode ? <><button type="button" onClick={() => void changeBinRecords(selectedRows, "restore")} disabled={busy || (!canManageRoles && selectedRows.some(row => row.userId))} title={!canManageRoles && selectedRows.some(row => row.userId) ? "Roles.Manage is required for linked staff accounts" : undefined} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"><ActionIcon name="restore"/>Restore selected</button><button type="button" onClick={() => void changeBinRecords(selectedRows, "permanent")} disabled={busy} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-red-700 hover:bg-red-50 disabled:opacity-40"><ActionIcon name="delete"/>Permanently delete</button></> : <><button type="button" onClick={event => { event.currentTarget.closest("details")?.removeAttribute("open"); setStatusError(""); setStatusDialogOpen(true); }} disabled={busy} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"><ActionIcon name="activate"/>Status</button>
            <button type="button" onClick={composeMail} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="mail"/>Email</button>
            <button type="button" onClick={() => void exportSelected()} disabled={busy} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"><ActionIcon name="export"/>Export</button>
            <button type="button" disabled title="Administrator password-reset workflow is not available yet" className="flex w-full cursor-not-allowed items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-slate-400"><ActionIcon name="reset"/>Reset password · unavailable</button>
            <button type="button" onClick={() => void changeBinRecords(selectedRows, "delete")} disabled={busy || (!canManageRoles && selectedRows.some(row => row.userId))} title={!canManageRoles && selectedRows.some(row => row.userId) ? "Roles.Manage is required for linked staff accounts" : undefined} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left font-semibold text-red-700 hover:bg-red-50 disabled:opacity-40"><ActionIcon name="delete"/>Move to bin</button></>}
          </div>
        </details>}
      </div>}
    </div>
    {error ? <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p> : <div className="mt-3 overflow-x-auto rounded-2xl border border-slate-200"><table className="w-full min-w-[960px] text-left text-xs"><thead className="bg-slate-50 text-[10px] font-bold uppercase tracking-[.1em] text-slate-500"><tr><th className="w-10 px-4 py-3"><input type="checkbox" aria-label="Select all staff on this page" checked={allSelected} onChange={togglePage}/></th><th className="px-3 py-3">Full name / staff ID</th><th className="px-3 py-3">Phone</th><th className="px-3 py-3">Email</th><th className="px-3 py-3">Status</th><th className="px-3 py-3">Position</th><th className="px-3 py-3">{binMode ? "Deleted on" : "Added on"}</th><th className="w-12 px-3 py-3 text-right">Actions</th></tr></thead><tbody className="divide-y divide-slate-100">{rows.map(row => <tr key={row.id} className="hover:bg-slate-50/70"><td className="px-4 py-3"><input type="checkbox" aria-label={`Select ${row.firstName} ${row.lastName}`} checked={selected.has(row.id)} onChange={() => toggleRow(row.id)}/></td><td className="px-3 py-3"><div className="flex items-center gap-3"><StaffAvatar key={row.photoUrl??row.id} row={row}/><div className="min-w-0">{binMode ? <span className="font-bold text-slate-900">{row.firstName} {row.lastName}</span> : <Link href={`/portal/staff/${row.id}`} className="font-bold text-slate-900 hover:underline">{row.firstName} {row.middleInitial ? `${row.middleInitial}. ` : ""}{row.lastName}</Link>}<div className="mt-1 flex items-center gap-1.5 font-mono text-[10px] text-slate-500"><span>{row.staffNumber}</span><button type="button" onClick={() => void copyStaffNumber(row.staffNumber)} aria-label={`Copy staff ID ${row.staffNumber}`} title="Copy staff ID" className="rounded p-0.5 hover:bg-slate-100 hover:text-slate-900"><svg aria-hidden="true" viewBox="0 0 20 20" className="size-3.5 fill-none stroke-current" strokeWidth="1.6"><rect x="6" y="6" width="10" height="11" rx="1.5"/><path d="M13 6V4.5A1.5 1.5 0 0 0 11.5 3h-7A1.5 1.5 0 0 0 3 4.5v8A1.5 1.5 0 0 0 4.5 14H6"/></svg></button></div></div></div></td><td className="px-3 py-3 text-slate-600">{row.phone || "—"}</td><td className="max-w-48 truncate px-3 py-3 text-slate-600" title={row.workEmail ?? undefined}>{row.workEmail || "—"}</td><td className="px-3 py-3"><span className={`rounded-full px-2.5 py-1 font-bold ${statusColors[row.status] ?? "bg-slate-100 text-slate-700"}`}>{statusNames[row.status] ?? "Unknown"}</span></td><td className="px-3 py-3 text-slate-600">{row.positionName || "—"}</td><td className="whitespace-nowrap px-3 py-3 text-slate-500">{new Date(binMode && row.deletedAtUtc ? row.deletedAtUtc : row.createdAtUtc).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}</td><td className="px-3 py-3 text-right"><button type="button" onClick={event => openMenu(row, event.currentTarget)} aria-label={`Actions for ${row.firstName} ${row.lastName}`} className="rounded-lg p-2 text-lg leading-none text-slate-600 hover:bg-slate-100 hover:text-slate-950">⋯</button></td></tr>)}</tbody></table>{!loading && rows.length === 0 && <p className="px-5 py-10 text-center text-sm text-slate-500">{binMode ? "No staff records are in the bin." : "No staff match your search or category."}</p>}</div>}
    <div className="mt-4 flex items-center justify-between text-xs text-slate-500"><span>Page {page} of {pages}</span><div className="flex gap-2"><button type="button" disabled={page === 1 || loading} onClick={() => { setPage(current => current - 1); setSelected(new Set()); }} className="tenant-primary-bg rounded-xl px-4 py-2.5 font-bold text-white shadow-sm transition hover:brightness-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-800 disabled:cursor-not-allowed disabled:opacity-40">Previous</button><button type="button" disabled={page >= pages || loading} onClick={() => { setPage(current => current + 1); setSelected(new Set()); }} className="tenant-primary-bg rounded-xl px-4 py-2.5 font-bold text-white shadow-sm transition hover:brightness-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-800 disabled:cursor-not-allowed disabled:opacity-40">Next</button></div></div>
    {menu && createPortal(<>
      <button type="button" aria-label="Close staff actions" onClick={() => setMenu(null)} className="fixed inset-0 z-[150] cursor-default bg-transparent"/>
      <div ref={rowMenuRef} role="menu" aria-label={`Actions for ${menu.row.firstName} ${menu.row.lastName}`} style={{ top: menu.top, left: menu.left }} className="fixed z-[151] w-48 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl">
        {binMode ? <>
          <button role="menuitem" type="button" onClick={() => void viewBinDetail(menu.row)} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="history"/>View record history</button>
          {(!menu.row.userId || canManageRoles) && <button role="menuitem" type="button" onClick={() => void changeBinRecords([menu.row], "restore")} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="restore"/>Restore</button>}
          <button role="menuitem" type="button" onClick={() => void changeBinRecords([menu.row], "permanent")} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-xs font-semibold text-red-700 hover:bg-red-50"><ActionIcon name="delete"/>Permanently delete</button>
        </> : <>
          <Link role="menuitem" href={`/portal/staff/${menu.row.id}`} onClick={() => setMenu(null)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="view"/>Full view</Link>
          {canManage && <>
            <Link role="menuitem" href={`/portal/staff/${menu.row.id}/edit`} onClick={() => setMenu(null)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="edit"/>Edit</Link>
            <Link role="menuitem" href={`/portal/staff/${menu.row.id}?section=position`} onClick={() => setMenu(null)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="position"/>Change role or position</Link>
            {menu.row.category === 0 && <Link role="menuitem" href={`/portal/staff/${menu.row.id}?section=assignments`} onClick={() => setMenu(null)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="assignment"/>Assign classes &amp; subjects</Link>}
            <Link role="menuitem" href={`/portal/staff/${menu.row.id}#documents`} onClick={() => setMenu(null)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="upload"/>Upload document</Link>
            {!menu.row.userId && menu.row.workEmail && <button role="menuitem" type="button" onClick={() => { void inviteStaff(menu.row); setMenu(null); }} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50"><ActionIcon name="mail"/>Send invitation</button>}
            {(!menu.row.userId || canManageRoles) && <button role="menuitem" type="button" onClick={() => void changeBinRecords([menu.row], "delete")} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-xs font-semibold text-red-700 hover:bg-red-50"><ActionIcon name="delete"/>Move to bin</button>}
          </>}
        </>}
      </div>
    </>, document.body)}
    {statusDialogOpen && createPortal(<div className="fixed inset-0 z-[170] grid place-items-center bg-slate-950/60 p-3 backdrop-blur-sm" onMouseDown={event => { if (!busy && event.target === event.currentTarget) setStatusDialogOpen(false); }}><div role="dialog" aria-modal="true" aria-labelledby="staff-status-title" className="w-full max-w-md rounded-2xl bg-white p-5 shadow-2xl sm:p-7"><div className="flex items-start justify-between gap-4"><div><p className="text-xs font-bold uppercase tracking-wide text-[#12372a]">Staff directory</p><h2 id="staff-status-title" className="mt-1 text-xl font-black text-slate-950">Update status</h2><p className="mt-2 text-sm text-slate-500">Apply one status to {selectedRows.length} selected staff member{selectedRows.length === 1 ? "" : "s"}.</p></div><button type="button" onClick={() => setStatusDialogOpen(false)} disabled={busy} aria-label="Close status dialog" className="grid size-9 shrink-0 place-items-center rounded-lg bg-slate-100 text-xl text-slate-600 hover:bg-slate-200 disabled:opacity-40">×</button></div><label htmlFor="bulk-staff-status" className="mt-6 block text-sm font-bold text-slate-800">New status</label><select id="bulk-staff-status" value={statusValue} onChange={event => { setStatusValue(event.target.value); setStatusError(""); }} className="mt-2 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-3 text-sm text-slate-900 outline-none focus:border-[#12372a]"><option value="">Select a status</option>{statusNames.map((name, index) => <option key={name} value={index}>{name}</option>)}</select>{statusValue === "2" && <><label htmlFor="bulk-staff-exit-date" className="mt-4 block text-sm font-bold text-slate-800">Exit date</label><input id="bulk-staff-exit-date" type="date" required value={exitDate} onChange={event => { setExitDate(event.target.value); setStatusError(""); }} className="mt-2 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-3 text-sm text-slate-900 outline-none focus:border-[#12372a]"/></>}<p className="mt-4 text-xs leading-5 text-slate-500">This updates the HR status. Account access is managed separately.</p>{statusError && <p role="alert" className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">{statusError}</p>}<div className="mt-6 flex justify-end gap-2 border-t border-slate-100 pt-5"><button type="button" onClick={() => setStatusDialogOpen(false)} disabled={busy} className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 transition hover:bg-red-100 disabled:opacity-50">Close</button><button type="button" onClick={() => void changeStatus()} disabled={busy} className="rounded-xl bg-[#12372a] px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-[#1d513d] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#12372a] disabled:opacity-70">{busy ? "Updating…" : "Update status"}</button></div></div></div>, document.body)}
    {emailRecipients && <StaffEmailComposer recipients={emailRecipients} skippedCount={emailSkippedCount} onClose={() => setEmailRecipients(null)} />}
    {binDetail && createPortal(<div className="fixed inset-0 z-[170] grid place-items-center bg-slate-950/60 p-3 backdrop-blur-sm" onMouseDown={event => { if (event.target === event.currentTarget) setBinDetail(null); }}><div role="dialog" aria-modal="true" aria-labelledby="staff-bin-history-title" className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-5 shadow-2xl sm:p-7"><div className="flex items-start justify-between gap-4"><div><p className="text-xs font-bold uppercase tracking-wide text-amber-700">Staff bin · read-only history</p><h2 id="staff-bin-history-title" className="mt-1 text-xl font-black text-slate-950">{binDetail.staff.firstName} {binDetail.staff.lastName}</h2><p className="mt-1 text-xs text-slate-500">{binDetail.staff.staffNumber} · {binDetail.staff.positionName || "Position not recorded"}</p></div><button type="button" aria-label="Close history" onClick={() => setBinDetail(null)} className="rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-bold text-slate-700 hover:bg-slate-50">Close</button></div><div className="mt-5 grid gap-3 rounded-xl bg-slate-50 p-4 text-xs text-slate-700 sm:grid-cols-2"><div><span className="font-bold">Email</span><p className="mt-1 break-all">{binDetail.staff.workEmail || "—"}</p></div><div><span className="font-bold">Phone</span><p className="mt-1">{binDetail.staff.phone || "—"}</p></div><div><span className="font-bold">Created</span><p className="mt-1">{new Date(binDetail.staff.createdAtUtc).toLocaleString()}</p></div><div><span className="font-bold">Moved to bin</span><p className="mt-1">{binDetail.staff.deletedAtUtc ? new Date(binDetail.staff.deletedAtUtc).toLocaleString() : "—"}</p></div><div className="sm:col-span-2"><span className="font-bold">Deleted by</span><p className="mt-1">{binDetail.deletedByName || (binDetail.deletedByUserId ? "Former user" : "—")}</p></div></div><h3 className="mt-6 text-sm font-black text-slate-900">Recorded activity</h3>{binDetail.activity.length ? <ol className="mt-3 divide-y divide-slate-100 border-y border-slate-100">{binDetail.activity.map((item, index) => <li key={`${item.occurredAtUtc}-${index}`} className="flex flex-wrap justify-between gap-2 py-3 text-xs"><span className="font-bold text-slate-800">{item.action}</span><span className="text-slate-500">{new Date(item.occurredAtUtc).toLocaleString()} · {item.actorName || (item.actorUserId ? "Former user" : "System")}</span></li>)}</ol> : <p className="mt-2 text-xs text-slate-500">No activity events are recorded for this staff profile.</p>}</div></div>, document.body)}
  </section>;
}

function StaffAvatar({row}:{row:StaffRow}){
  const [failed,setFailed]=useState(false);
  const initials=`${row.firstName.trim()[0]??""}${row.lastName.trim()[0]??""}`.toUpperCase();
  return <span aria-hidden="true" className="grid size-10 shrink-0 place-items-center overflow-hidden rounded-full border border-slate-200 bg-[var(--tenant-primary-soft,#e7f2eb)] text-xs font-black text-[var(--tenant-primary,#28654a)]">
    {row.photoUrl&&!failed?<img src={row.photoUrl} alt="" onError={()=>setFailed(true)} className="size-full object-cover"/>:initials}
  </span>;
}

const actionIconPaths = {
  activate: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
  suspend: <><circle cx="12" cy="12" r="9"/><path d="M9 9h6v6H9z"/></>,
  mail: <><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m4 7 8 6 8-6"/></>,
  export: <><path d="M12 3v12m0 0 4-4m-4 4-4-4"/><path d="M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3"/></>,
  reset: <><path d="M4 11a8 8 0 1 1 2 6M4 5v6h6"/><path d="M12 8v4l3 2"/></>,
  restore: <><path d="M4 11a8 8 0 1 1 2 6M4 5v6h6"/><path d="M9 12h6m-3-3-3 3 3 3"/></>,
  delete: <><path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7"/><path d="M10 11v5m4-5v5"/></>,
  history: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
  view: <><path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z"/><circle cx="12" cy="12" r="2.5"/></>,
  edit: <><path d="M4 20h4l11-11-4-4L4 16v4Z"/><path d="m13 7 4 4"/></>,
  position: <><circle cx="12" cy="7" r="3"/><path d="M5 20v-2a7 7 0 0 1 14 0v2M3 12h3m12 0h3"/></>,
  assignment: <><rect x="4" y="4" width="16" height="17" rx="2"/><path d="M8 9h8m-8 4h8m-8 4h5"/></>,
  upload: <><path d="M12 16V4m0 0-4 4m4-4 4 4"/><path d="M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3"/></>,
};

function ActionIcon({ name }: { name: keyof typeof actionIconPaths }) {
  return <svg aria-hidden="true" viewBox="0 0 24 24" className="size-4 shrink-0 fill-none stroke-current" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{actionIconPaths[name]}</svg>;
}
