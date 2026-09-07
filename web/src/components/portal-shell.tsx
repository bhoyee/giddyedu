"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AccessContext, audienceCopy, PortalAudience, PortalDashboard, visiblePortalItems } from "@/lib/access";

export function PortalShell() {
  const router = useRouter();
  const [access, setAccess] = useState<AccessContext | null>(null);
  const [audience, setAudience] = useState<PortalAudience | null>(null);
  const [error, setError] = useState("");
  const [dashboard, setDashboard] = useState<PortalDashboard | null>(null);
  const [dashboardError, setDashboardError] = useState<{ audience: PortalAudience; message: string } | null>(null);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (response.status === 401) { router.replace("/login"); return; }
      if (!response.ok) { setError("Your workspace could not be loaded."); return; }
      const body = await response.json() as { access: AccessContext };
      setAccess(body.access); setAudience(body.access.defaultAudience);
    }).catch(() => setError("The service is temporarily unavailable."));
  }, [router]);

  useEffect(() => {
    if (!audience) return;
    void fetch(`/api/backend/portal/dashboard?audience=${encodeURIComponent(audience)}`, { cache: "no-store" }).then(async response => {
      if (!response.ok) { setDashboardError({ audience, message: "Dashboard summary could not be loaded." }); return; }
      setDashboard(await response.json() as PortalDashboard);
      setDashboardError(null);
    }).catch(() => setDashboardError({ audience, message: "Dashboard summary could not be loaded." }));
  }, [audience]);

  async function logout() { await fetch("/api/auth/logout", { method: "POST" }); router.replace("/login"); router.refresh(); }
  if (error) return <State title="Workspace unavailable" detail={error} />;
  if (!access || !audience) return <State title="Loading workspace" detail="Checking your tenant, roles, permissions and subscription…" />;

  const experience = audienceCopy[audience];
  const items = visiblePortalItems(access, audience);
  return <main className="min-h-screen bg-[#f6f4ee]">
    <header className="bg-[#12372a] text-white"><div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-5"><div><p className="text-xl font-black">GiddyEdu</p><p className="text-xs text-emerald-100">Tenant {access.tenantId}{access.campusId ? ` · Campus ${access.campusId}` : ""}</p></div><button onClick={logout} className="rounded-full border border-white/30 px-4 py-2 text-sm">Sign out</button></div></header>
    <div className="mx-auto max-w-7xl px-6 py-12">
      <div className="flex flex-wrap items-end justify-between gap-4"><div><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">{experience.label}</p><h1 className="mt-3 text-4xl font-black tracking-tight">{experience.title}</h1><p className="mt-3 max-w-2xl text-slate-600">{experience.description} The API authorizes every operation independently.</p></div>{access.audiences.length > 1 && <label className="text-sm font-bold text-slate-700">Workspace<select aria-label="Active workspace" value={audience} onChange={event => setAudience(event.target.value as PortalAudience)} className="ml-2 rounded-xl border border-slate-300 bg-white px-4 py-3">{access.audiences.map(value => <option key={value} value={value}>{audienceCopy[value].label}</option>)}</select></label>}</div>
      <p className="mt-4 text-sm text-slate-500">Assigned roles: {access.roles.length ? access.roles.join(", ") : "No named role"}</p>
      {dashboardError?.audience === audience ? <p role="alert" className="mt-6 rounded-2xl bg-red-50 p-4 text-sm text-red-800">{dashboardError.message}</p> : dashboard?.audience === audience ? <section aria-label={`${experience.label} summary`} className="mt-8"><div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">{dashboard.metrics.map(metric => { const content = <><p className="text-sm font-bold text-slate-500">{metric.label}</p><p className="mt-2 text-3xl font-black">{metric.value.toLocaleString()}</p></>; return metric.href ? <Link key={metric.key} href={metric.href} className="rounded-3xl bg-white p-6 shadow-sm transition hover:-translate-y-1 hover:shadow-lg">{content}</Link> : <div key={metric.key} className="rounded-3xl bg-white p-6 shadow-sm">{content}</div>; })}</div><p className="mt-4 text-sm text-slate-600">{dashboard.guidance}</p></section> : <p className="mt-6 text-sm text-slate-500">Loading dashboard summary…</p>}
      {items.length === 0 ? <div className="mt-8 rounded-3xl bg-white p-8"><h2 className="font-bold">No modules available in this workspace</h2><p className="mt-2 text-slate-600">This experience has no subscribed modules permitted for your account. Ask an administrator to review your role and subscription.</p></div> : <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{items.map(item => <Link key={item.href} href={item.href} className="rounded-3xl border border-slate-900/10 bg-white p-6 transition hover:-translate-y-1 hover:shadow-lg"><h2 className="text-xl font-black">{item.label}</h2><p className="mt-2 text-slate-600">{item.description}</p><p className="mt-6 text-sm font-bold text-emerald-800">Open module →</p></Link>)}</div>}
    </div>
  </main>;
}

function State({ title, detail }: { title: string; detail: string }) { return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6"><div className="rounded-3xl bg-white p-8 text-center shadow-lg"><h1 className="text-xl font-black">{title}</h1><p className="mt-2 text-slate-600">{detail}</p></div></main>; }
