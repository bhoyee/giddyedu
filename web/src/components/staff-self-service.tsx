"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type StaffRecord = { staffId: string; staffNumber: string; firstName: string; lastName: string; category: string; status: string; campusName: string; departmentName: string | null; positionName: string | null; workEmail: string | null; phone: string | null; hireDate: string; teachingAssignmentCount: number };

export function StaffSelfService() {
  const router = useRouter(); const [record, setRecord] = useState<StaffRecord | null | undefined>(undefined); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/backend/portal/staff", { cache: "no-store" }).then(async response => {
    if (response.status === 401) { router.replace("/login"); return; }
    if (response.status === 403) { setError("The staff workspace is not available for this account."); return; }
    if (!response.ok) { setError("Your staff profile could not be loaded."); return; }
    setRecord(await response.json() as StaffRecord | null);
  }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto max-w-5xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header><div className="mx-auto max-w-5xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Staff self-service</p><h1 className="mt-3 text-4xl font-black">My staff profile</h1><p className="mt-2 text-slate-600">Your linked employment record. Changes are managed by an authorised school administrator.</p>{error ? <Panel>{error}</Panel> : record === undefined ? <Panel>Loading your staff profile…</Panel> : record === null ? <Panel>No staff profile is linked to this account. Contact your school administrator.</Panel> : <div className="mt-8 grid gap-6 lg:grid-cols-[1.3fr_.7fr]"><section className="rounded-3xl bg-white p-6 shadow-sm"><div className="flex flex-wrap items-start justify-between gap-4"><div><h2 className="text-2xl font-black">{record.firstName} {record.lastName}</h2><p className="mt-1 text-sm text-slate-500">{record.staffNumber}</p></div><span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-900">{formatValue(record.status)}</span></div><dl className="mt-6 grid gap-4 sm:grid-cols-2"><Field label="Category" value={formatValue(record.category)} /><Field label="Campus" value={record.campusName} /><Field label="Department" value={record.departmentName ?? "Not assigned"} /><Field label="Position" value={record.positionName ?? "Not assigned"} /><Field label="Work email" value={record.workEmail ?? "Not recorded"} /><Field label="Phone" value={record.phone ?? "Not recorded"} /><Field label="Hire date" value={record.hireDate} /></dl></section><section className="rounded-3xl bg-[#12372a] p-6 text-white shadow-sm"><p className="text-sm font-bold text-emerald-200">Teaching responsibilities</p><p className="mt-3 text-5xl font-black">{record.teachingAssignmentCount.toLocaleString()}</p><p className="mt-3 text-sm text-emerald-100">Assignments currently linked to your staff profile.</p>{record.teachingAssignmentCount > 0 && <Link href="/portal/teaching" className="mt-6 inline-flex font-bold text-white">Open teaching workspace →</Link>}</section></div>}</div></main>;
}

function formatValue(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2"); }
function Field({ label, value }: { label: string; value: string }) { return <div className="rounded-2xl bg-slate-50 p-4"><dt className="text-xs font-bold uppercase text-slate-500">{label}</dt><dd className="mt-1 font-semibold">{value}</dd></div>; }
function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
