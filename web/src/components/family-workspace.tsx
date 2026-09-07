"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type FamilyStudent = { studentId: string; admissionNumber: string; firstName: string; lastName: string; relationship: string; isPrimaryGuardian: boolean; classSectionId: string | null; className: string | null };

export function FamilyWorkspace() {
  const router = useRouter(); const [students, setStudents] = useState<FamilyStudent[] | null>(null); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/backend/portal/family/students", { cache: "no-store" }).then(async response => {
    if (response.status === 401) { router.replace("/login"); return; }
    if (response.status === 403) { setError("The family workspace is not available for this account."); return; }
    if (!response.ok) { setError("Your linked children could not be loaded."); return; }
    setStudents(await response.json() as FamilyStudent[]);
  }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto max-w-6xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header><div className="mx-auto max-w-6xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Parent</p><h1 className="mt-3 text-4xl font-black">My children</h1><p className="mt-2 text-slate-600">Only learners connected to your guardian profile are shown.</p>{error ? <Panel>{error}</Panel> : students === null ? <Panel>Loading linked children…</Panel> : students.length === 0 ? <Panel>No student is linked to your guardian account. Contact the school administrator to verify the relationship.</Panel> : <div className="mt-8 grid gap-4 md:grid-cols-2">{students.map(student => <section key={student.studentId} className="rounded-3xl bg-white p-6 shadow-sm"><div className="flex items-start justify-between gap-4"><div><h2 className="text-2xl font-black">{student.firstName} {student.lastName}</h2><p className="mt-1 text-sm text-slate-500">{student.admissionNumber}</p></div>{student.isPrimaryGuardian && <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-900">Primary guardian</span>}</div><dl className="mt-5 grid grid-cols-2 gap-3 text-sm"><div><dt className="font-bold text-slate-500">Relationship</dt><dd>{formatValue(student.relationship)}</dd></div><div><dt className="font-bold text-slate-500">Current class</dt><dd>{student.className ?? "Not currently enrolled"}</dd></div></dl><Link href={`/portal/students/${student.studentId}`} className="mt-6 inline-flex font-bold text-emerald-800">Open permitted profile →</Link></section>)}</div>}</div></main>;
}

function formatValue(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2"); }
function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
