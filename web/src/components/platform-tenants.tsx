"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

type Tenant = { id: string; name: string; slug: string; isActive: boolean; createdAtUtc: string; updatedAtUtc: string | null; registeredByName: string | null; registeredByEmail: string | null; campuses: number; memberships: number; subscriptionId: string | null; planName: string | null; subscriptionStatus: number | null; subscriptionEndsAtUtc: string | null; graceEndsAtUtc: string | null };
type Plan = { code: string; name: string };

export function PlatformTenants() {
  const router = useRouter();
  const [tenants, setTenants] = useState<Tenant[] | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [target, setTarget] = useState<Tenant | null>(null);
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    const [response, planResponse] = await Promise.all([fetch("/api/backend/platform/admin/tenants", { cache: "no-store" }), fetch("/api/backend/plans", { cache: "no-store" })]);
    if (response.status === 401) { router.replace("/platform-login"); return; }
    if (!response.ok) throw new Error(response.status === 403 ? "Platform administrator access is required." : "Platform tenants could not be loaded.");
    setTenants(await response.json() as Tenant[]);
    if (planResponse.ok) setPlans(await planResponse.json() as Plan[]);
  }, [router]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load().catch((reason) => setError(reason instanceof Error ? reason.message : "Platform tenants could not be loaded.")), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  async function setStatus(tenant: Tenant) {
    setBusy(true); setError(""); setNotice("");
    const response = await fetch(`/api/backend/platform/admin/tenants/${tenant.id}/status`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ isActive: !tenant.isActive }) });
    setBusy(false);
    if (!response.ok) { setError("The tenant status change was rejected."); return; }
    setNotice("Tenant status updated."); await load();
  }

  async function changeSubscription(tenant: Tenant, path: string, body?: object) {
    setBusy(true); setError(""); setNotice("");
    const response = await fetch(`/api/backend/platform/admin/${path}`, { method: "POST", headers: body ? { "Content-Type": "application/json" } : undefined, body: body ? JSON.stringify(body) : undefined });
    setBusy(false);
    if (!response.ok) { setError("The subscription change was rejected. Verify its current state."); return; }
    setNotice("Tenant subscription updated."); await load();
  }

  async function deleteTenant() {
    if (!target) return;
    setBusy(true); setError(""); setNotice("");
    const response = await fetch(`/api/backend/platform/admin/tenants/${target.id}`, { method: "DELETE", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ confirmation }) });
    setBusy(false);
    if (!response.ok) { setError(`Deletion was rejected. Type DELETE ${target.slug} exactly.`); return; }
    setTarget(null); setConfirmation(""); setExpanded(null);
    setNotice("Tenant deleted. Its records remain retained for audit and controlled recovery.");
    await load();
  }

  return <main className="min-h-screen bg-[#f4f5f1] text-[#1d2c24]">
    <header className="bg-[#12372a] text-white"><div className="mx-auto max-w-7xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100 hover:text-white">← Workspace</Link></div></header>
    <div className="mx-auto max-w-7xl px-6 py-12">
      <p className="text-xs font-black uppercase tracking-[.22em] text-[#297053]">Platform administration</p>
      <h1 className="mt-3 text-4xl font-black tracking-[-.045em] sm:text-5xl">Tenants and subscriptions</h1>
      <p className="mt-4 max-w-3xl leading-7 text-[#657169]">Review each school&apos;s registration contact, account status, campuses, users and commercial access.</p>
      {error && <Message tone="error">{error}</Message>}{notice && <Message>{notice}</Message>}
      {!tenants ? <Message>Loading tenants…</Message> : <div className="mt-9 space-y-4">{tenants.map((tenant) => <article key={tenant.id} className="overflow-hidden rounded-[1.6rem] border border-[#dfe5df] bg-white shadow-[0_14px_45px_rgba(18,55,42,.06)]">
        <div className="flex flex-col gap-5 p-6 lg:flex-row lg:items-center lg:justify-between"><div><div className="flex flex-wrap items-center gap-3"><h2 className="text-xl font-black">{tenant.name}</h2><span className={`rounded-full px-3 py-1 text-xs font-black ${tenant.isActive ? "bg-emerald-50 text-emerald-800" : "bg-red-50 text-red-700"}`}>{tenant.isActive ? "Active" : "Suspended"}</span></div><p className="mt-2 text-sm text-[#718078]">{tenant.slug} · Registered {formatDate(tenant.createdAtUtc)}</p></div><div className="flex flex-wrap items-center gap-3"><div className="rounded-xl bg-[#f3f6f2] px-4 py-2 text-sm"><b>{tenant.campuses}</b> campuses · <b>{tenant.memberships}</b> users</div><button type="button" aria-expanded={expanded === tenant.id} onClick={() => setExpanded(expanded === tenant.id ? null : tenant.id)} className="rounded-full bg-[#12372a] px-5 py-2.5 text-sm font-black text-white">{expanded === tenant.id ? "Close details" : "View & manage"}</button></div></div>
        {expanded === tenant.id && <div className="border-t border-[#e5e9e5] bg-[#fafbf8] p-6"><div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4"><Detail label="Registered by" value={tenant.registeredByName || "Not recorded"} secondary={tenant.registeredByEmail || undefined} /><Detail label="Registered on" value={formatDate(tenant.createdAtUtc)} /><Detail label="Tenant identifier" value={tenant.id} secondary={tenant.slug} /><Detail label="Subscription" value={tenant.planName || "Not provisioned"} secondary={tenant.subscriptionEndsAtUtc ? `Ends ${formatDate(tenant.subscriptionEndsAtUtc)}` : undefined} /></div><div className="mt-5 flex flex-wrap gap-3"><button type="button" disabled={busy} onClick={() => void setStatus(tenant)} className="rounded-xl border border-[#cbd6ce] bg-white px-4 py-3 text-sm font-black">{tenant.isActive ? "Suspend tenant" : "Reactivate tenant"}</button>{tenant.subscriptionId && tenant.subscriptionStatus !== 2 && <button type="button" disabled={busy} onClick={() => void changeSubscription(tenant, `tenants/${tenant.id}/subscriptions/${tenant.subscriptionId}/suspend`)} className="rounded-xl border border-red-200 bg-white px-4 py-3 text-sm font-black text-red-700">Suspend plan</button>}<button type="button" disabled={busy} onClick={() => { setTarget(tenant); setConfirmation(""); setError(""); }} className="rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-sm font-black text-red-800">Delete tenant</button></div><div className="mt-3 flex flex-wrap gap-2">{plans.map((plan) => <button type="button" key={plan.code} disabled={busy} onClick={() => void changeSubscription(tenant, tenant.subscriptionId && tenant.subscriptionStatus === 2 ? `tenants/${tenant.id}/subscriptions/${tenant.subscriptionId}/reactivate` : `tenants/${tenant.id}/subscription`, { planCode: plan.code, endsAtUtc: new Date(Date.now() + 365 * 86400000).toISOString() })} className="rounded-xl bg-[#e7efe9] px-4 py-2.5 text-sm font-black text-[#194b37]">{tenant.subscriptionId ? `${tenant.subscriptionStatus === 2 ? "Reactivate" : "Set"} ${plan.name}` : `Provision ${plan.name}`}</button>)}</div></div>}
      </article>)}</div>}
    </div>
    {target && <DeleteDialog tenant={target} value={confirmation} busy={busy} onChange={setConfirmation} onCancel={() => { setTarget(null); setConfirmation(""); }} onConfirm={() => void deleteTenant()} />}
  </main>;
}

function DeleteDialog({ tenant, value, busy, onChange, onCancel, onConfirm }: { tenant: Tenant; value: string; busy: boolean; onChange: (value: string) => void; onCancel: () => void; onConfirm: () => void }) {
  const phrase = `DELETE ${tenant.slug}`;
  return <div role="dialog" aria-modal="true" aria-labelledby="delete-title" className="fixed inset-0 z-50 grid place-items-center bg-[#071b13]/70 p-4 backdrop-blur-sm"><div className="w-full max-w-lg rounded-[1.75rem] bg-white p-6 shadow-2xl sm:p-8"><div className="grid size-12 place-items-center rounded-full bg-red-100 text-xl font-black text-red-700">!</div><h2 id="delete-title" className="mt-5 text-2xl font-black">Delete {tenant.name}?</h2><p className="mt-3 text-sm leading-6 text-[#657169]">This immediately disables the tenant and removes it from active platform administration. Records are retained for audit and controlled recovery.</p><label className="mt-6 block text-sm font-black">Type <span className="font-mono text-red-700">{phrase}</span> to confirm<input autoFocus value={value} onChange={(event) => onChange(event.target.value)} autoComplete="off" className="mt-2 min-h-13 w-full rounded-xl border border-[#cbd5ce] px-4 font-mono outline-none focus:border-red-500 focus:ring-4 focus:ring-red-100" /></label><div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end"><button type="button" disabled={busy} onClick={onCancel} className="rounded-full border border-[#cbd5ce] px-5 py-3 text-sm font-black">Cancel</button><button type="button" disabled={busy || value !== phrase} onClick={onConfirm} className="rounded-full bg-red-700 px-5 py-3 text-sm font-black text-white disabled:opacity-40">{busy ? "Deleting…" : "Delete tenant"}</button></div></div></div>;
}

function Detail({ label, value, secondary }: { label: string; value: string; secondary?: string }) { return <div className="rounded-2xl border border-[#e1e7e1] bg-white p-5"><p className="text-xs font-black uppercase tracking-[.14em] text-[#718078]">{label}</p><p className="mt-2 break-words text-sm font-black">{value}</p>{secondary && <p className="mt-1 break-words text-sm text-[#718078]">{secondary}</p>}</div>; }
function Message({ children, tone = "success" }: { children: React.ReactNode; tone?: "success" | "error" }) { return <div role={tone === "error" ? "alert" : "status"} className={`mt-6 rounded-2xl border bg-white p-5 ${tone === "error" ? "border-red-200 text-red-700" : "border-[#dfe5df] text-emerald-800"}`}>{children}</div>; }
function formatDate(value: string) { const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? "Not recorded" : parsed.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" }); }
