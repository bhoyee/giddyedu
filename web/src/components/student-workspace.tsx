"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type StudentRecord = { studentId: string; admissionNumber: string; firstName: string; lastName: string; dateOfBirth: string; email: string | null; status: string; currentEnrollment: { enrollmentId: string; classSectionId: string; className: string; classCode: string; academicYearId: string; academicYearName: string; enrolledOn: string } | null };

export function StudentWorkspace() {
  const router = useRouter(); const [record, setRecord] = useState<StudentRecord | null | undefined>(undefined); const [error, setError] = useState("");
  useEffect(() => { void fetch("/api/backend/portal/student", { cache: "no-store" }).then(async response => {
    if (response.status === 401) { router.replace("/login"); return; }
    if (response.status === 403) { setError("The student workspace is not available for this account."); return; }
    if (!response.ok) { setError("Your school record could not be loaded."); return; }
    setRecord(await response.json() as StudentRecord | null);
  }).catch(() => setError("The service is temporarily unavailable.")); }, [router]);
  return <main className="min-h-screen bg-[#f6f4ee]"><header className="bg-[#12372a] text-white"><div className="mx-auto max-w-5xl px-6 py-5"><Link href="/portal" className="font-bold text-emerald-100">← Workspace</Link></div></header><div className="mx-auto max-w-5xl px-6 py-10"><p className="text-sm font-bold uppercase tracking-[.22em] text-emerald-800">Student</p><h1 className="mt-3 text-4xl font-black">My school record</h1><p className="mt-2 text-slate-600">Your canonical learner profile and current class placement.</p>{error ? <Panel>{error}</Panel> : record === undefined ? <Panel>Loading your school record…</Panel> : record === null ? <Panel>No student profile is linked to this account. Contact your school administrator.</Panel> : <div className="mt-8 grid gap-6 lg:grid-cols-[1.2fr_.8fr]"><section className="rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-2xl font-black">{record.firstName} {record.lastName}</h2><dl className="mt-6 grid gap-4 sm:grid-cols-2"><Field label="Admission number" value={record.admissionNumber} /><Field label="Date of birth" value={record.dateOfBirth} /><Field label="Email" value={record.email ?? "Not recorded"} /><Field label="Status" value={record.status} /></dl></section><section className="rounded-3xl bg-[#12372a] p-6 text-white shadow-sm"><p className="text-xs font-bold uppercase tracking-wide text-emerald-200">Current enrolment</p>{record.currentEnrollment ? <><h2 className="mt-3 text-2xl font-black">{record.currentEnrollment.className}</h2><p className="mt-1 text-emerald-100">{record.currentEnrollment.classCode}</p><p className="mt-6 text-sm">Academic year</p><p className="font-bold">{record.currentEnrollment.academicYearName}</p><p className="mt-4 text-sm text-emerald-100">Enrolled {record.currentEnrollment.enrolledOn}</p></> : <p className="mt-4 text-emerald-100">No active enrolment is recorded.</p>}</section></div>}</div></main>;
}

function Field({ label, value }: { label: string; value: string }) { return <div className="rounded-2xl bg-slate-50 p-4"><dt className="text-xs font-bold uppercase text-slate-500">{label}</dt><dd className="mt-1 font-semibold">{value}</dd></div>; }
function Panel({ children }: { children: React.ReactNode }) { return <div className="mt-8 rounded-3xl bg-white p-8 text-slate-600 shadow-sm">{children}</div>; }
