"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { notify } from "@/components/app-toast";

type Guardian = { id: string; firstName: string; lastName: string; phone: string; email: string | null; hasAccount: boolean };
type Page = { items: Guardian[]; total: number };
type SortKey = "lastName" | "firstName" | "phone" | "email";
const pageSize = 20;

export function GuardianDirectory() {
  const [rows, setRows] = useState<Guardian[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<SortKey>("lastName");
  const [descending, setDescending] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [canInvite, setCanInvite] = useState(false);
  const [canEdit, setCanEdit] = useState(false);
  const [openMenu, setOpenMenu] = useState<{ row: Guardian; top: number; right: number } | null>(null);
  const [revision, setRevision] = useState(0);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => { setQuery(search.trim()); setPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    const controller = new AbortController();
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), sort, descending: String(descending) });
    if (query) params.set("search", query);
    void fetch(`/api/backend/guardians?${params}`, { cache: "no-store", signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error(response.status === 403 ? "You do not have access to guardian records." : "Guardian records could not be loaded.");
      const data = await response.json() as Page;
      setRows(data.items); setTotal(data.total); setError("");
    }).catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Guardian records could not be loaded."); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, query, sort, descending, revision]);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      setCanInvite(session.access?.permissions?.includes("Users.Manage") === true);
      setCanEdit(session.access?.permissions?.includes("Guardians.Manage") === true);
    });
  }, []);

  useEffect(() => {
    if (!openMenu) return;
    const dismiss = (event: PointerEvent) => {
      const target = event.target as Element;
      if (!menuRef.current?.contains(target) && !target.closest("[data-guardian-menu-trigger]")) setOpenMenu(null);
    };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") setOpenMenu(null); };
    document.addEventListener("pointerdown", dismiss);
    document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("pointerdown", dismiss); document.removeEventListener("keydown", escape); };
  }, [openMenu]);

  function changeSort(next: SortKey) {
    setOpenMenu(null);
    setLoading(true);
    setPage(1);
    if (sort === next) setDescending(value => !value);
    else { setSort(next); setDescending(false); }
  }

  function toggleMenu(row: Guardian, button: HTMLButtonElement) {
    if (openMenu?.row.id === row.id) { setOpenMenu(null); return; }
    const bounds = button.getBoundingClientRect();
    setOpenMenu({ row, top: Math.max(8, Math.min(bounds.bottom + 6, window.innerHeight - 160)), right: Math.max(8, window.innerWidth - bounds.right) });
  }

  async function invite(row: Guardian) {
    setOpenMenu(null);
    try {
      const response = await fetch("/api/backend/account-invitations", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ targetType: 1, targetId: row.id }),
      });
      if (!response.ok) {
        const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
        throw new Error(body?.detail ?? body?.title ?? "The invitation could not be queued.");
      }
      notify({ title: "Invitation queued", message: `A secure account invitation was queued for ${row.email}.` });
      setLoading(true);
      setRevision(value => value + 1);
    } catch (reason) {
      notify({ tone: "error", title: "Invitation not sent", message: reason instanceof Error ? reason.message : "Please try again." });
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return <section className="mt-7 rounded-3xl border border-slate-200 bg-white shadow-sm">
    <div className="flex flex-wrap items-end justify-between gap-4 border-b border-slate-200 p-5 sm:p-6">
      <div><p className="text-xs font-black uppercase tracking-widest tenant-primary-text">Directory</p><h2 className="mt-1 text-xl font-black text-slate-950">Parents and guardians</h2><p className="mt-1 text-sm text-slate-500">{total} record{total === 1 ? "" : "s"}</p></div>
      <label className="w-full text-xs font-bold text-slate-600 sm:w-72">Search guardians
        <input value={search} onChange={event => { setSearch(event.target.value); setLoading(true); }} placeholder="Name, email or phone" className="mt-1 block w-full rounded-xl border border-slate-300 px-4 py-2.5 text-sm font-normal text-slate-950 outline-none focus:border-slate-700" />
      </label>
    </div>
    {error ? <p role="alert" className="p-6 text-sm text-red-700">{error}</p> : loading ? <p className="p-6 text-sm text-slate-500">Loading guardians...</p> : rows.length === 0 ? <p className="p-6 text-sm text-slate-500">No guardians match this search.</p> : <div className="overflow-x-auto"><table className="w-full min-w-[680px] text-left text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500"><tr>
      {([["lastName", "Name"], ["phone", "Phone"], ["email", "Email"]] as const).map(([key, label]) => <th key={key} scope="col" className="px-5 py-4"><button type="button" onClick={() => changeSort(key)} className="inline-flex items-center gap-2 font-bold hover:text-slate-950" aria-label={`Sort by ${label}`}>{label}<span aria-hidden="true">{sort === key ? descending ? "↓" : "↑" : "↕"}</span></button></th>)}
      <th scope="col" className="px-5 py-4">Account</th><th scope="col" className="px-5 py-4 text-right">Actions</th>
    </tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t border-slate-100 hover:bg-slate-50/80">
      <td className="px-5 py-4 font-bold text-slate-900">{row.firstName} {row.lastName}</td><td className="px-5 py-4 text-slate-700">{row.phone}</td><td className="px-5 py-4 text-slate-700">{row.email ?? "—"}</td>
      <td className="px-5 py-4"><span className={`rounded-full px-2.5 py-1 text-xs font-bold ${row.hasAccount ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-900"}`}>{row.hasAccount ? "Connected" : "Not connected"}</span></td>
      <td className="px-5 py-4 text-right"><button type="button" data-guardian-menu-trigger onClick={event => toggleMenu(row, event.currentTarget)} aria-label={`Actions for ${row.firstName} ${row.lastName}`} aria-expanded={openMenu?.row.id === row.id} className="rounded-lg border border-slate-200 px-3 py-1 text-xl font-bold leading-6 text-slate-700 hover:bg-slate-100">···</button></td>
    </tr>)}</tbody></table></div>}
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-5 text-sm text-slate-600"><span>Showing {total === 0 ? 0 : (page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}</span><div className="flex items-center gap-2"><button type="button" disabled={page <= 1 || loading} onClick={() => { setLoading(true); setPage(value => value - 1); }} className="rounded-lg border border-slate-300 px-4 py-2 font-bold text-slate-800 hover:bg-slate-100 disabled:opacity-40">Previous</button><span className="px-2">{page} / {totalPages}</span><button type="button" disabled={page >= totalPages || loading} onClick={() => { setLoading(true); setPage(value => value + 1); }} className="tenant-primary-bg rounded-lg px-4 py-2 font-bold text-white disabled:opacity-40">Next</button></div></div>
    {openMenu && createPortal(<div ref={menuRef} style={{ top: openMenu.top, right: openMenu.right }} className="fixed z-[110] w-44 rounded-xl border border-slate-200 bg-white p-1 text-left shadow-xl"><Link href={`/portal/guardians/${openMenu.row.id}`} className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100">View profile</Link>{canEdit && <Link href={`/portal/guardians/${openMenu.row.id}#profile`} className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100">Edit details</Link>}{canInvite && !openMenu.row.hasAccount && <button type="button" onClick={() => void invite(openMenu.row)} className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-100">Send invite</button>}</div>, document.body)}
  </section>;
}
