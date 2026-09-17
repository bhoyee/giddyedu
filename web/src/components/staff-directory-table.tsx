"use client";

/* eslint-disable @next/next/no-img-element -- Staff photo links are short-lived authorised URLs and cannot use image optimization. */

import Link from "next/link";
import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { notify } from "@/components/app-toast";

type StaffRow = {
  id: string; userId: string | null; staffNumber: string; firstName: string; lastName: string;
  middleInitial: string | null; category: number; status: number; workEmail: string | null;
  phone: string | null; positionName: string | null; photoUrl: string | null; createdAtUtc: string;
};
type StaffPage = { items: StaffRow[]; total: number };
type RowMenu = { row: StaffRow; top: number; left: number };

const categories = [{ label: "All", value: "" }, { label: "Teaching", value: "0" }, { label: "Non-teaching", value: "2" }, { label: "Administrative", value: "1" }];
const categoryNames = ["Teaching", "Administrative", "Non-teaching"];
const statusNames = ["Active", "Suspended", "Exited", "Away", "Not cleared", "Inactive", "On leave", "Retired", "Resigned", "Sacked", "Left", "Deceased"];
const statusColors = [
  "bg-emerald-700 text-white", "bg-red-100 text-red-800", "bg-slate-200 text-slate-800",
  "bg-sky-100 text-sky-800", "bg-amber-100 text-amber-900", "bg-slate-100 text-slate-700",
  "bg-violet-100 text-violet-800", "bg-slate-200 text-slate-800", "bg-slate-200 text-slate-800",
  "bg-red-100 text-red-800", "bg-slate-200 text-slate-800", "bg-slate-300 text-slate-900",
];
const pageSize = 20;

export function StaffDirectoryTable({ version }: { version: number }) {
  const [rows, setRows] = useState<StaffRow[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [sort, setSort] = useState("name_asc");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [menu, setMenu] = useState<RowMenu | null>(null);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      setCanManage(session.access?.permissions?.includes("Staff.Manage") === true);
    });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setLoading(true);
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), sort });
      if (search.trim()) params.set("search", search.trim());
      if (category) params.set("category", category);
      try {
        const response = await fetch(`/api/backend/hr/staff?${params}`, { cache: "no-store", signal: controller.signal });
        if (!response.ok) throw new Error(response.status === 403 ? "You do not have access to this staff directory." : "The staff directory could not be loaded.");
        const data = await response.json() as StaffPage;
        setRows(data.items ?? []); setTotal(data.total ?? 0); setError("");
      } catch (reason) {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "The staff directory could not be loaded.");
      } finally { if (!controller.signal.aborted) setLoading(false); }
    }, search ? 250 : 0);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [category, page, revision, search, sort, version]);

  useEffect(() => {
    if (!menu) return;
    const onKey = (event: KeyboardEvent) => { if (event.key === "Escape") setMenu(null); };
    const onScroll = () => setMenu(null);
    document.addEventListener("keydown", onKey); window.addEventListener("scroll", onScroll, true);
    return () => { document.removeEventListener("keydown", onKey); window.removeEventListener("scroll", onScroll, true); };
  }, [menu]);

  const selectedRows = rows.filter(row => selected.has(row.id));
  const allSelected = rows.length > 0 && selectedRows.length === rows.length;
  const pages = Math.max(1, Math.ceil(total / pageSize));

  function changeFilter(value: string) { setCategory(value); setPage(1); setSelected(new Set()); setMenu(null); }
  function changeSort(value: string) { setSort(value); setPage(1); setSelected(new Set()); setMenu(null); }
  function toggleRow(id: string) { setSelected(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
  function togglePage() { setSelected(allSelected ? new Set() : new Set(rows.map(row => row.id))); }
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
    const emails = selectedRows.map(row => row.workEmail).filter((email): email is string => Boolean(email));
    if (!emails.length) { notify({ tone: "error", title: "No email addresses", message: "The selected staff do not have email addresses." }); return; }
    window.location.href = `mailto:?bcc=${encodeURIComponent(emails.join(","))}`;
  }
  async function changeStatus(status: 0 | 1) {
    if (!window.confirm(`${status === 1 ? "Suspend" : "Reactivate"} ${selectedRows.length} selected staff record${selectedRows.length === 1 ? "" : "s"}? This changes HR status; account access is managed separately.`)) return;
    setBusy(true);
    let changed = 0;
    for (const row of selectedRows) {
      try {
        const response = await fetch(`/api/backend/hr/staff/${row.id}/status`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ status, exitDate: null }) });
        if (response.ok) changed++;
      } catch { /* Continue to report partial failures for the selected records. */ }
    }
    setBusy(false); setSelected(new Set()); setRevision(value => value + 1);
    notify({ tone: changed === selectedRows.length ? "success" : "error", title: `${changed} of ${selectedRows.length} records updated`, message: changed === selectedRows.length ? "Staff HR statuses were saved." : "Some statuses could not be changed. Review those records individually." });
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
      <div className="flex flex-wrap gap-1.5" aria-label="Filter staff by category">{categories.map(option => <button key={option.label} type="button" onClick={() => changeFilter(option.value)} aria-pressed={category === option.value} className={`rounded-full px-4 py-2 text-xs font-bold transition ${category === option.value ? "tenant-primary-bg text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200"}`}>{option.label}</button>)}</div>
      <div className="flex flex-col gap-2 sm:flex-row"><label className="sr-only" htmlFor="staff-search">Search staff</label><input id="staff-search" type="search" value={search} onChange={event => { setSearch(event.target.value); setPage(1); setSelected(new Set()); }} placeholder="Search name, ID, email or phone" className="min-w-0 rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm outline-none focus:border-[var(--tenant-primary,#28654a)] sm:w-64"/><label className="sr-only" htmlFor="staff-sort">Sort staff</label><select id="staff-sort" value={sort} onChange={event => changeSort(event.target.value)} className="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-700"><option value="name_asc">Name A–Z</option><option value="name_desc">Name Z–A</option><option value="added_desc">Newest first</option><option value="added_asc">Oldest first</option><option value="staff_number">Staff ID</option></select></div>
    </div>
    <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-500">
      <span>{loading ? "Loading staff…" : `${total.toLocaleString()} staff member${total === 1 ? "" : "s"}`}{selected.size ? ` · ${selected.size} selected on this page` : ""}</span>
      {selected.size > 0 && <div className="flex items-center gap-2">
        <button type="button" onClick={() => setSelected(new Set())} className="font-bold text-slate-700 hover:underline">Clear selection</button>
        {canManage && <details className="relative"><summary className="tenant-primary-bg cursor-pointer list-none rounded-lg px-3 py-2 font-bold text-white">Actions ▾</summary>
          <div className="absolute right-0 z-30 mt-1 w-52 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl">
            <button type="button" onClick={() => void changeStatus(0)} disabled={busy} className="block w-full rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40">Status · Activate</button>
            <button type="button" onClick={() => void changeStatus(1)} disabled={busy} className="block w-full rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40">Status · Suspend</button>
            <button type="button" onClick={composeMail} className="block w-full rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50">Mail · Open email app</button>
            <button type="button" onClick={() => void exportSelected()} disabled={busy} className="block w-full rounded-lg px-3 py-2 text-left font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40">Export selected CSV</button>
            <button type="button" disabled title="Administrator password-reset workflow is not available yet" className="block w-full cursor-not-allowed rounded-lg px-3 py-2 text-left font-semibold text-slate-400">Reset password · unavailable</button>
            <button type="button" disabled title="Staff deletion needs an archive or retention policy" className="block w-full cursor-not-allowed rounded-lg px-3 py-2 text-left font-semibold text-red-300">Delete · unavailable</button>
          </div>
        </details>}
      </div>}
    </div>
    {error ? <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p> : <div className="mt-3 overflow-x-auto rounded-2xl border border-slate-200"><table className="w-full min-w-[1060px] text-left text-xs"><thead className="bg-slate-50 text-[10px] font-bold uppercase tracking-[.1em] text-slate-500"><tr><th className="w-10 px-4 py-3"><input type="checkbox" aria-label="Select all staff on this page" checked={allSelected} onChange={togglePage}/></th><th className="px-3 py-3">Full name / staff ID</th><th className="px-3 py-3">Phone</th><th className="px-3 py-3">Email</th><th className="px-3 py-3">Status</th><th className="px-3 py-3">Category</th><th className="px-3 py-3">Position</th><th className="px-3 py-3">Added on</th><th className="w-12 px-3 py-3 text-right">Actions</th></tr></thead><tbody className="divide-y divide-slate-100">{rows.map(row => <tr key={row.id} className="hover:bg-slate-50/70"><td className="px-4 py-3"><input type="checkbox" aria-label={`Select ${row.firstName} ${row.lastName}`} checked={selected.has(row.id)} onChange={() => toggleRow(row.id)}/></td><td className="px-3 py-3"><div className="flex items-center gap-3"><StaffAvatar key={row.photoUrl??row.id} row={row}/><div className="min-w-0"><Link href={`/portal/staff/${row.id}`} className="font-bold text-slate-900 hover:underline">{row.firstName} {row.middleInitial ? `${row.middleInitial}. ` : ""}{row.lastName}</Link><div className="mt-1 flex items-center gap-1.5 font-mono text-[10px] text-slate-500"><span>{row.staffNumber}</span><button type="button" onClick={() => void copyStaffNumber(row.staffNumber)} aria-label={`Copy staff ID ${row.staffNumber}`} title="Copy staff ID" className="rounded p-0.5 hover:bg-slate-100 hover:text-slate-900"><svg aria-hidden="true" viewBox="0 0 20 20" className="size-3.5 fill-none stroke-current" strokeWidth="1.6"><rect x="6" y="6" width="10" height="11" rx="1.5"/><path d="M13 6V4.5A1.5 1.5 0 0 0 11.5 3h-7A1.5 1.5 0 0 0 3 4.5v8A1.5 1.5 0 0 0 4.5 14H6"/></svg></button></div></div></div></td><td className="px-3 py-3 text-slate-600">{row.phone || "—"}</td><td className="max-w-48 truncate px-3 py-3 text-slate-600" title={row.workEmail ?? undefined}>{row.workEmail || "—"}</td><td className="px-3 py-3"><span className={`rounded-full px-2.5 py-1 font-bold ${statusColors[row.status] ?? "bg-slate-100 text-slate-700"}`}>{statusNames[row.status] ?? "Unknown"}</span></td><td className="px-3 py-3 text-slate-600">{categoryNames[row.category] ?? "Unknown"}</td><td className="px-3 py-3 text-slate-600">{row.positionName || "—"}</td><td className="whitespace-nowrap px-3 py-3 text-slate-500">{new Date(row.createdAtUtc).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}</td><td className="px-3 py-3 text-right"><button type="button" onClick={event => openMenu(row, event.currentTarget)} aria-label={`Actions for ${row.firstName} ${row.lastName}`} className="rounded-lg p-2 text-lg leading-none text-slate-600 hover:bg-slate-100 hover:text-slate-950">⋯</button></td></tr>)}</tbody></table>{!loading && rows.length === 0 && <p className="px-5 py-10 text-center text-sm text-slate-500">No staff match your search or category.</p>}</div>}
    <div className="mt-4 flex items-center justify-between text-xs text-slate-500"><span>Page {page} of {pages}</span><div className="flex gap-2"><button type="button" disabled={page === 1 || loading} onClick={() => { setPage(current => current - 1); setSelected(new Set()); }} className="rounded-lg border border-slate-200 px-3 py-2 font-semibold disabled:opacity-40">Previous</button><button type="button" disabled={page >= pages || loading} onClick={() => { setPage(current => current + 1); setSelected(new Set()); }} className="rounded-lg border border-slate-200 px-3 py-2 font-semibold disabled:opacity-40">Next</button></div></div>
    {menu && createPortal(<><button type="button" aria-label="Close staff actions" onClick={() => setMenu(null)} className="fixed inset-0 z-[150] cursor-default bg-transparent"/><div role="menu" aria-label={`Actions for ${menu.row.firstName} ${menu.row.lastName}`} style={{ top: menu.top, left: menu.left }} className="fixed z-[151] w-48 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl"><Link role="menuitem" href={`/portal/staff/${menu.row.id}`} onClick={() => setMenu(null)} className="block rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">Full view</Link>{canManage && <><Link role="menuitem" href={`/portal/staff/${menu.row.id}/edit`} onClick={() => setMenu(null)} className="block rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">Edit</Link><Link role="menuitem" href={`/portal/staff/${menu.row.id}#staff-position`} onClick={() => setMenu(null)} className="block rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">Change role or position</Link><Link role="menuitem" href={`/portal/staff/${menu.row.id}#staff-assignments`} onClick={() => setMenu(null)} className="block rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">Assign classes & subjects</Link><Link role="menuitem" href={`/portal/staff/${menu.row.id}#documents`} onClick={() => setMenu(null)} className="block rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">Upload document</Link>{!menu.row.userId && menu.row.workEmail && <button role="menuitem" type="button" onClick={() => { void inviteStaff(menu.row); setMenu(null); }} className="block w-full rounded-lg px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-50">Send invitation</button>}</>}</div></>, document.body)}
  </section>;
}

function StaffAvatar({row}:{row:StaffRow}){
  const [failed,setFailed]=useState(false);
  const initials=`${row.firstName.trim()[0]??""}${row.lastName.trim()[0]??""}`.toUpperCase();
  return <span aria-hidden="true" className="grid size-10 shrink-0 place-items-center overflow-hidden rounded-full border border-slate-200 bg-[var(--tenant-primary-soft,#e7f2eb)] text-xs font-black text-[var(--tenant-primary,#28654a)]">
    {row.photoUrl&&!failed?<img src={row.photoUrl} alt="" onError={()=>setFailed(true)} className="size-full object-cover"/>:initials}
  </span>;
}
