"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type Overview = { planCode: string | null; planName: string | null; startsAtUtc: string | null; endsAtUtc: string | null; activeCampusCount: number; features: { key: string; enabled: boolean; limit: number | null }[] };

export function CommercialOverview() {
  const router = useRouter(); const [overview, setOverview] = useState<Overview | null>(null); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/backend/portal/commercial", { cache: "no-store" }).then(async response => {
    if (response.status === 401) { router.replace("/login"); return; }
    if (response.status === 403) { setError("The accountant workspace is not available for this account."); return; }
    if (!response.ok) { setError("The commercial overview could not be loaded."); return; }
    setOverview(await response.json() as Overview);
  }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto max-w-6xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header><div className="mx-auto max-w-6xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Accountant / Bursar</p><h1 className="mt-3 text-4xl font-black">Commercial overview</h1><p className="mt-2 text-slate-600">Read-only Phase 1 subscription visibility. Billing, accounting and payments arrive with the Phase 4 finance modules.</p>{error ? <Panel>{error}</Panel> : !overview ? <Panel>Loading commercial overview…</Panel> : <><div className="mt-8 grid gap-4 md:grid-cols-3"><Summary label="Current plan" value={overview.planName ?? "No active plan"} detail={overview.planCode ?? "—"} /><Summary label="Active campuses" value={overview.activeCampusCount.toLocaleString()} detail="Within this tenant" /><Summary label="Plan start" value={overview.startsAtUtc ? new Date(overview.startsAtUtc).toLocaleDateString() : "—"} detail={overview.endsAtUtc ? `Ends ${new Date(overview.endsAtUtc).toLocaleDateString()}` : "No scheduled end"} /></div><section className="mt-6 rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-xl font-black">Phase 1 capabilities</h2><div className="mt-5 grid gap-3 sm:grid-cols-2">{overview.features.map(feature => <div key={feature.key} className="flex items-center justify-between rounded-2xl bg-slate-50 p-4"><span className="font-semibold">{formatKey(feature.key)}</span><span className={`rounded-full px-3 py-1 text-xs font-bold ${feature.enabled ? "bg-emerald-100 text-emerald-900" : "bg-slate-200 text-slate-600"}`}>{feature.enabled ? feature.limit === null ? "Enabled" : `Limit ${feature.limit}` : "Disabled"}</span></div>)}</div></section></>}</div></main>;
}

function formatKey(value: string) { return value.split("-").map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(" "); }
function Summary({ label, value, detail }: { label: string; value: string; detail: string }) { return <section className="rounded-3xl bg-white p-6 shadow-sm"><p className="text-sm font-bold text-slate-500">{label}</p><p className="mt-2 text-2xl font-black">{value}</p><p className="mt-2 text-sm text-slate-500">{detail}</p></section>; }
function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
