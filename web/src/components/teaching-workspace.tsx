"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type TeachingClass = { assignmentId: string; classSectionId: string; className: string; classCode: string; responsibility: string; subjectId: string | null; subjectName: string | null; studentCount: number | null };

export function TeachingWorkspace() {
  const router = useRouter();
  const [classes, setClasses] = useState<TeachingClass[] | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    void fetch("/api/backend/portal/teaching/classes", { cache: "no-store" }).then(async response => {
      if (response.status === 401) { router.replace("/login"); return; }
      if (response.status === 403) { setError("The teacher workspace is not available for this account."); return; }
      if (!response.ok) { setError("Teaching assignments could not be loaded."); return; }
      setClasses(await response.json() as TeachingClass[]);
    }).catch(() => setError("The service is temporarily unavailable."));
  }, [router]);

  return <main className="min-h-screen bg-[#f6f4ee]">
    <header className="bg-[#12372a] text-white"><div className="mx-auto max-w-6xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header>
    <div className="mx-auto max-w-6xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Teacher</p><h1 className="mt-3 text-4xl font-black">My teaching</h1><p className="mt-2 text-slate-600">Classes and subjects assigned to your linked staff profile.</p>
      {error ? <Panel>{error}</Panel> : classes === null ? <Panel>Loading teaching assignments…</Panel> : classes.length === 0 ? <Panel>No teaching responsibilities are assigned to your account.</Panel> : <div className="mt-8 grid gap-4 md:grid-cols-2">{classes.map(item => <section key={item.assignmentId} className="rounded-3xl bg-white p-6 shadow-sm"><p className="text-xs font-bold uppercase tracking-wide text-slate-500">{formatResponsibility(item.responsibility)}</p><h2 className="mt-2 text-2xl font-black">{item.className}</h2><p className="mt-1 text-sm text-slate-500">{item.classCode}{item.subjectName ? ` · ${item.subjectName}` : ""}</p><p className="mt-5 text-sm font-semibold text-slate-700">{item.studentCount === null ? "Learner totals are unavailable" : `${item.studentCount.toLocaleString()} active learner${item.studentCount === 1 ? "" : "s"}`}</p>{item.studentCount !== null && <Link href="/portal/students" className="mt-5 inline-flex font-bold text-emerald-800">View permitted learners →</Link>}</section>)}</div>}
    </div>
  </main>;
}

function formatResponsibility(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2"); }
function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
