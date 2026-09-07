"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type Readiness = { completedSteps: number; totalSteps: number; percentage: number; steps: { key: string; label: string; complete: boolean; detail: string; href: string }[] };

export function OperationalReadiness() {
  const router = useRouter(); const [readiness, setReadiness] = useState<Readiness | null>(null); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/backend/portal/operations/readiness", { cache: "no-store" }).then(async response => {
    if (response.status === 401) { router.replace("/login"); return; }
    if (response.status === 403) { setError("The school management workspace is not available for this account."); return; }
    if (!response.ok) { setError("Operational readiness could not be loaded."); return; }
    setReadiness(await response.json() as Readiness);
  }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto max-w-6xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header><div className="mx-auto max-w-6xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">School management</p><h1 className="mt-3 text-4xl font-black">Operational readiness</h1><p className="mt-2 text-slate-600">Complete the subscribed and permitted foundations needed to operate the school.</p>{error ? <Panel>{error}</Panel> : !readiness ? <Panel>Checking operational readiness…</Panel> : <><section className="mt-8 rounded-3xl bg-[#12372a] p-7 text-white"><div className="flex flex-wrap items-end justify-between gap-4"><div><p className="text-sm font-bold text-emerald-200">Phase 1 progress</p><p className="mt-2 text-5xl font-black">{readiness.percentage}%</p></div><p className="text-emerald-100">{readiness.completedSteps} of {readiness.totalSteps} available steps complete</p></div><div className="mt-6 h-3 overflow-hidden rounded-full bg-white/20"><div className="h-full rounded-full bg-emerald-300" style={{ width: `${readiness.percentage}%` }} /></div></section><div className="mt-6 grid gap-4 md:grid-cols-2">{readiness.steps.map(step => <Link key={step.key} href={step.href} className="rounded-3xl bg-white p-6 shadow-sm transition hover:-translate-y-1 hover:shadow-lg"><div className="flex items-start gap-4"><span aria-hidden="true" className={`grid size-9 shrink-0 place-items-center rounded-full font-black ${step.complete ? "bg-emerald-100 text-emerald-900" : "bg-amber-100 text-amber-900"}`}>{step.complete ? "✓" : "!"}</span><div><h2 className="text-lg font-black">{step.label}</h2><p className="mt-2 text-sm text-slate-600">{step.detail}</p><p className="mt-4 text-sm font-bold text-emerald-800">{step.complete ? "Review" : "Complete step"} →</p></div></div></Link>)}</div></>}</div></main>;
}

function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
