"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AccessContext, visiblePortalItems } from "@/lib/access";

export function PortalShell() {
  const router = useRouter(); const [access, setAccess] = useState<AccessContext | null>(null); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/auth/session", { cache: "no-store" }).then(async response => { if (response.status === 401) { router.replace("/login"); return; } if (!response.ok) { setError("Your workspace could not be loaded."); return; } const body = await response.json(); setAccess(body.access); }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  async function logout() { await fetch("/api/auth/logout", { method: "POST" }); router.replace("/login"); router.refresh(); }
  if (error) return <State title="Workspace unavailable" detail={error} />;
  if (!access) return <State title="Loading workspace" detail="Checking your tenant, permissions and subscription…" />;
  const items = visiblePortalItems(access);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-5"><div><p className="text-xl font-black">GiddyEdu</p><p className="text-xs text-emerald-100">Tenant {access.tenantId}</p></div><button onClick={logout} className="rounded-full border border-white/30 px-4 py-2 text-sm">Sign out</button></div></header><div className="mx-auto max-w-7xl px-6 py-12"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Your workspace</p><h1 className="mt-3 text-4xl font-black tracking-tight">School operations</h1><p className="mt-3 max-w-2xl text-slate-600">Only subscribed features permitted for your account are shown. The API authorizes every operation again.</p>{items.length === 0 ? <div className="mt-8 rounded-3xl bg-white p-8"><h2 className="font-bold">No workspace modules available</h2><p className="mt-2 text-slate-600">Ask a school administrator to assign permissions and confirm the subscription.</p></div> : <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{items.map(item => <Link key={item.href} href={item.href} className="rounded-3xl border border-slate-900/10 bg-white p-6 transition hover:-translate-y-1 hover:shadow-lg"><h2 className="text-xl font-black">{item.label}</h2><p className="mt-2 text-slate-600">{item.description}</p><p className="mt-6 text-sm font-bold text-emerald-800">Open module →</p></Link>)}</div>}</div></main>;
}
function State({ title, detail }: { title: string; detail: string }) { return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6"><div className="rounded-3xl bg-white p-8 text-center shadow-lg"><h1 className="text-xl font-black">{title}</h1><p className="mt-2 text-slate-600">{detail}</p></div></main>; }
