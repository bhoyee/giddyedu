"use client";

/* eslint-disable @next/next/no-img-element -- Guardian photos use short-lived authorized object-storage URLs. */

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";
import { confirmAction } from "@/components/confirm-action";
import { LinkGuardianStudentDialog } from "@/components/guardian-management-dialogs";

type Guardian = { id: string; firstName: string; lastName: string; phone: string; email: string | null; address?: string | null; hasAccount: boolean; status: number };
type StudentLink = { studentId: string; admissionNumber: string; firstName: string; middleName?: string | null; lastName: string; classSectionName?: string | null; academicYearName?: string | null; relationship: string };
type DocumentRow = { id: string; fileName: string; category: string; status: string | number };

async function responseMessage(response: Response, fallback: string) {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  return body?.detail ?? body?.title ?? fallback;
}

export function GuardianProfileWorkspace({ guardianId }: { guardianId: string }) {
  const [guardian, setGuardian] = useState<Guardian | null>(null);
  const [students, setStudents] = useState<StudentLink[]>([]);
  const [documents, setDocuments] = useState<DocumentRow[]>([]);
  const [photoUrl, setPhotoUrl] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [familyPanelOpen, setFamilyPanelOpen] = useState(false);
  const [linkStudentOpen, setLinkStudentOpen] = useState(false);
  const [studentSearch, setStudentSearch] = useState("");

  const load = useCallback(async () => {
    try {
      const [profileResponse, studentsResponse, documentsResponse] = await Promise.all([
        fetch(`/api/backend/guardians/${guardianId}`, { cache: "no-store" }),
        fetch(`/api/backend/guardians/${guardianId}/student-links`, { cache: "no-store" }),
        fetch(`/api/backend/documents/Guardian/${guardianId}`, { cache: "no-store" }),
      ]);
      if (!profileResponse.ok) throw new Error(await responseMessage(profileResponse, "Guardian profile could not be loaded."));
      if (!studentsResponse.ok && studentsResponse.status !== 403) throw new Error(await responseMessage(studentsResponse, "Linked students could not be loaded."));
      const profile = await profileResponse.json() as Guardian;
      const linked = studentsResponse.ok ? await studentsResponse.json() as StudentLink[] : [];
      const files = documentsResponse.ok ? await documentsResponse.json() as DocumentRow[] : [];
      setGuardian(profile); setStudents(linked); setDocuments(files);
      const photo = files.find(file => file.category.toLowerCase() === "photo");
      if (photo) {
        const download = await fetch(`/api/backend/documents/${photo.id}/download`);
        if (download.ok) setPhotoUrl((await download.json() as { url: string }).url);
      } else setPhotoUrl("");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Guardian profile could not be loaded."); }
    finally { setLoading(false); }
  }, [guardianId]);

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer); }, [load, revision]);
  useEffect(() => { void fetch("/api/auth/session", { cache: "no-store" }).then(async response => { if (!response.ok) return; const session = await response.json() as { access?: { permissions?: string[] } }; setCanManage(session.access?.permissions?.includes("Guardians.Manage") === true); }); }, []);

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    const values = new FormData(event.currentTarget);
    try {
      const response = await fetch(`/api/backend/guardians/${guardianId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ firstName: values.get("firstName"), lastName: values.get("lastName"), phone: values.get("phone"), email: values.get("email"), address: values.get("address") }) });
      if (!response.ok) throw new Error(await responseMessage(response, "Profile changes could not be saved."));
      notify({ title: "Guardian updated", message: "The guardian profile was saved successfully." }); setRevision(value => value + 1);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Profile changes could not be saved."); }
    finally { setBusy(false); }
  }

  async function unlink(student: StudentLink) {
    const studentName = [student.firstName, student.middleName, student.lastName].filter(Boolean).join(" ");
    const confirmed = await confirmAction({ title: "Unlink student from guardian?", message: `${studentName} will no longer be connected to ${guardian?.firstName ?? "this"} ${guardian?.lastName ?? "guardian"}. Neither record will be deleted.`, confirmLabel: "Unlink student" });
    if (!confirmed) return;
    setBusy(true);
    try {
      const response = await fetch(`/api/backend/guardians/${guardianId}/student-links/${student.studentId}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await responseMessage(response, "Student could not be unlinked."));
      notify({ title: "Student unlinked", message: `${studentName} is no longer linked to this guardian.` }); setRevision(value => value + 1);
    } catch (reason) { notify({ tone: "error", title: "Student not unlinked", message: reason instanceof Error ? reason.message : "Please try again." }); }
    finally { setBusy(false); }
  }

  async function download(fileId: string) {
    const response = await fetch(`/api/backend/documents/${fileId}/download`);
    if (!response.ok) { notify({ tone: "error", title: "Download unavailable", message: "This document could not be downloaded." }); return; }
    window.open((await response.json() as { url: string }).url, "_blank", "noopener,noreferrer");
  }

  if (loading && !guardian) return <div className="grid min-h-96 place-items-center rounded-3xl border border-slate-200 bg-white text-sm font-semibold text-slate-500">Loading guardian profile…</div>;
  if (!guardian) return <div className="rounded-3xl border border-red-200 bg-red-50 p-6 text-red-800"><h1 className="text-xl font-black">Guardian profile unavailable</h1><p className="mt-2 text-sm">{error}</p><Link href="/portal/guardians" className="mt-4 inline-block font-bold underline">Return to guardian directory</Link></div>;

  const fullName = `${guardian.firstName} ${guardian.lastName}`;
  const initials = `${guardian.firstName.charAt(0)}${guardian.lastName.charAt(0)}`.toUpperCase();
  const statusLabel = guardian.status === 2 ? "Suspended" : guardian.status === 1 ? "Inactive" : "Active record";
  const inputClass = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm text-slate-950 outline-none focus:border-slate-600 focus:ring-2 focus:ring-slate-200";
  const visibleStudents = students.slice(0, 4);
  const matchingStudents = students.filter(student => `${student.firstName} ${student.middleName ?? ""} ${student.lastName} ${student.admissionNumber} ${student.classSectionName ?? ""}`.toLowerCase().includes(studentSearch.trim().toLowerCase()));

  return <section className="w-full space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><Link href="/portal/guardians" className="inline-flex items-center gap-2 text-sm font-bold text-slate-600 hover:text-slate-950">← Guardian directory</Link><span className="text-xs font-bold uppercase tracking-[.14em] text-slate-400">Guardian profile</span></div>
    <section aria-label="Guardian summary" className="relative overflow-hidden rounded-[1.75rem] border border-slate-200 bg-white p-5 shadow-[0_18px_55px_rgba(15,23,42,.08)] sm:p-7">
      <div className="absolute inset-x-0 top-0 h-1.5 tenant-primary-bg" />
      <div className="flex flex-col gap-5 sm:flex-row sm:items-center">
        {photoUrl ? <img src={photoUrl} alt={`${fullName} profile`} className="size-24 rounded-2xl border-4 border-white object-cover shadow-lg sm:size-28" /> : <div className="grid size-24 shrink-0 place-items-center rounded-2xl bg-amber-100 text-2xl font-black text-amber-900 shadow-inner sm:size-28">{initials}</div>}
        <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-black tracking-[-.03em] text-slate-950 sm:text-3xl">{fullName}</h1><span className={`rounded-full px-3 py-1 text-xs font-black ${guardian.status === 2 ? "bg-red-100 text-red-800" : guardian.status === 1 ? "bg-slate-200 text-slate-700" : "bg-emerald-100 text-emerald-800"}`}>{statusLabel}</span></div><p className="mt-2 text-sm text-slate-600">{guardian.email ?? "No email address"} · {guardian.phone}</p><div className="mt-4 flex flex-wrap gap-2"><span className={`rounded-lg px-3 py-1.5 text-xs font-bold ${guardian.hasAccount ? "bg-emerald-50 text-emerald-800" : "bg-amber-50 text-amber-800"}`}>{guardian.hasAccount ? "Parent portal connected" : "No parent portal account"}</span><span className="rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-bold text-slate-700">{students.length} linked student{students.length === 1 ? "" : "s"}</span></div></div>
      </div>
    </section>
    {error && <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 px-5 py-4 text-sm font-semibold text-red-800">{error}</div>}
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(20rem,.65fr)]">
      <div className="space-y-6">
        <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">Family links</p><h2 className="mt-1 text-xl font-black text-slate-950">Linked students</h2><p className="mt-1 text-sm text-slate-500">A compact view of the children connected to this guardian.</p></div>{canManage && <button type="button" onClick={() => setLinkStudentOpen(true)} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-black text-white shadow-sm hover:brightness-95">＋ Link another student</button>}</div>
          {students.length === 0 ? <div className="mt-5 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-7 text-center"><p className="font-black text-slate-800">No linked students</p><p className="mt-1 text-sm text-slate-500">Link a student to create this guardian’s family group.</p></div> : <div className="mt-5 overflow-hidden rounded-2xl border border-slate-200"><div className="divide-y divide-slate-100">{visibleStudents.map(student => <FamilyRow key={student.studentId} student={student} canManage={canManage} busy={busy} onUnlink={unlink} />)}</div>{students.length > 4 && <button type="button" onClick={() => { setStudentSearch(""); setFamilyPanelOpen(true); }} className="tenant-primary-text flex w-full items-center justify-center gap-2 border-t border-slate-200 bg-slate-50 px-4 py-3 text-sm font-black hover:bg-slate-100">View all {students.length} students <span aria-hidden="true">→</span></button>}</div>}
        </section>
        <section id="profile" className="scroll-mt-24 rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">Contact record</p><h2 className="mt-1 text-xl font-black text-slate-950">Guardian information</h2>{canManage ? <form onSubmit={saveProfile} className="mt-5 grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold text-slate-700">First name<input name="firstName" required defaultValue={guardian.firstName} className={inputClass} /></label><label className="text-sm font-bold text-slate-700">Last name<input name="lastName" required defaultValue={guardian.lastName} className={inputClass} /></label><label className="text-sm font-bold text-slate-700">Phone<input name="phone" inputMode="numeric" pattern="[0-9]{11}" maxLength={11} required defaultValue={guardian.phone} className={inputClass} /></label><label className="text-sm font-bold text-slate-700">Email<input name="email" type="email" required defaultValue={guardian.email ?? ""} className={inputClass} /></label><label className="text-sm font-bold text-slate-700 sm:col-span-2">Address<textarea name="address" required rows={3} defaultValue={guardian.address ?? ""} className={inputClass} /></label><div className="flex justify-end sm:col-span-2"><button type="submit" disabled={busy} className="tenant-primary-bg rounded-xl px-5 py-3 text-sm font-black text-white shadow-sm hover:brightness-95 disabled:opacity-50">{busy ? "Saving…" : "Save changes"}</button></div></form> : <dl className="mt-5 grid gap-3 sm:grid-cols-2"><Info label="Phone" value={guardian.phone} /><Info label="Email" value={guardian.email ?? "Not provided"} /><Info label="Address" value={guardian.address ?? "Not provided"} wide /></dl>}</section>
      </div>
      <aside className="space-y-6"><section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">Record summary</p><dl className="mt-4 space-y-3"><Info label="Lifecycle status" value={statusLabel} /><Info label="Portal access" value={guardian.hasAccount ? "Connected" : "Not connected"} /><Info label="Students" value={String(students.length)} /></dl></section><section id="documents" className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">Files</p><h2 className="mt-1 text-xl font-black text-slate-950">Photo and signature</h2>{documents.length === 0 ? <p className="mt-4 rounded-xl bg-slate-50 p-4 text-sm text-slate-500">No guardian files uploaded.</p> : <div className="mt-4 space-y-2">{documents.map(document => <button key={document.id} type="button" onClick={() => void download(document.id)} className="flex w-full items-center justify-between gap-3 rounded-xl border border-slate-200 p-3 text-left hover:bg-slate-50"><span className="min-w-0"><strong className="block truncate text-sm text-slate-900">{document.fileName}</strong><small className="text-slate-500">{document.category}</small></span><span className="text-xs font-bold tenant-primary-text">Open</span></button>)}</div>}</section></aside>
    </div>
    {familyPanelOpen && <div className="fixed inset-0 z-[220] flex justify-end bg-slate-950/55 backdrop-blur-sm" onMouseDown={event => { if (event.target === event.currentTarget) setFamilyPanelOpen(false); }}><section role="dialog" aria-modal="true" aria-labelledby="family-panel-title" className="flex h-full w-full max-w-2xl flex-col bg-white shadow-2xl"><header className="flex items-start justify-between gap-4 border-b border-slate-200 p-5 sm:p-6"><div><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">Family links · {students.length}</p><h2 id="family-panel-title" className="mt-1 text-2xl font-black text-slate-950">All linked students</h2><p className="mt-1 text-sm text-slate-500">Search, review or unlink students without leaving this profile.</p></div><button type="button" onClick={() => setFamilyPanelOpen(false)} aria-label="Close family links" className="grid size-10 shrink-0 place-items-center rounded-xl bg-slate-100 text-xl text-slate-600 hover:bg-red-50 hover:text-red-700">×</button></header><div className="border-b border-slate-200 p-4 sm:px-6"><label className="text-xs font-black uppercase tracking-[.12em] text-slate-500">Search linked students<input autoFocus value={studentSearch} onChange={event => setStudentSearch(event.target.value)} placeholder="Name, admission number or class" className="mt-2 h-11 w-full rounded-xl border border-slate-300 px-4 text-sm font-normal normal-case tracking-normal outline-none focus:border-slate-600" /></label></div><div className="flex-1 overflow-y-auto p-4 sm:p-6">{matchingStudents.length === 0 ? <p className="rounded-xl bg-slate-50 p-6 text-center text-sm text-slate-500">No linked students match this search.</p> : <div className="overflow-hidden rounded-2xl border border-slate-200 divide-y divide-slate-100">{matchingStudents.map(student => <FamilyRow key={student.studentId} student={student} canManage={canManage} busy={busy} onUnlink={unlink} />)}</div>}</div><footer className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 p-4 sm:px-6"><span className="text-xs font-semibold text-slate-500">Showing {matchingStudents.length} of {students.length}</span><div className="flex gap-2">{canManage && <button type="button" onClick={() => setLinkStudentOpen(true)} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-black text-white">＋ Link student</button>}<button type="button" onClick={() => setFamilyPanelOpen(false)} className="rounded-xl bg-red-50 px-4 py-2.5 text-sm font-black text-red-700 hover:bg-red-100">Close</button></div></footer></section></div>}
    {linkStudentOpen && <LinkGuardianStudentDialog guardianId={guardianId} existingIds={students.map(student => student.studentId)} onClose={() => setLinkStudentOpen(false)} onSaved={() => setRevision(value => value + 1)} />}
  </section>;
}

function Info({ label, value, wide = false }: { label: string; value: string; wide?: boolean }) { return <div className={`rounded-xl border border-slate-200 bg-white p-3 ${wide ? "col-span-2" : ""}`}><dt className="text-[10px] font-black uppercase tracking-[.12em] text-slate-400">{label}</dt><dd className="mt-1 break-words text-sm font-bold text-slate-800">{value}</dd></div>; }

function FamilyRow({ student, canManage, busy, onUnlink }: { student: StudentLink; canManage: boolean; busy: boolean; onUnlink: (student: StudentLink) => Promise<void> }) {
  const name = [student.firstName, student.middleName, student.lastName].filter(Boolean).join(" ");
  const initials = `${student.firstName.charAt(0)}${student.lastName.charAt(0)}`.toUpperCase();
  return <article className="grid gap-3 bg-white p-4 sm:grid-cols-[minmax(0,1.2fr)_minmax(8rem,.7fr)_minmax(8rem,.7fr)_auto] sm:items-center"><div className="flex min-w-0 items-center gap-3"><span className="grid size-9 shrink-0 place-items-center rounded-xl bg-sky-100 text-[11px] font-black text-sky-800">{initials}</span><div className="min-w-0"><Link href={`/portal/students/${student.studentId}`} className="block truncate text-sm font-black text-slate-950 hover:underline">{name}</Link><p className="truncate text-[11px] font-semibold text-slate-500">{student.admissionNumber} · {student.relationship}</p></div></div><div><p className="text-[10px] font-black uppercase tracking-wider text-slate-400">Class</p><p className="mt-0.5 truncate text-xs font-bold text-slate-700">{student.classSectionName ?? "Not assigned"}</p></div><div><p className="text-[10px] font-black uppercase tracking-wider text-slate-400">Session</p><p className="mt-0.5 truncate text-xs font-bold text-slate-700">{student.academicYearName ?? "Not assigned"}</p></div>{canManage && <button type="button" disabled={busy} onClick={() => void onUnlink(student)} title={`Unlink ${name}`} aria-label={`Unlink ${name}`} className="grid size-8 place-items-center rounded-lg border border-red-200 bg-red-50 text-lg font-black text-red-600 hover:bg-red-100 disabled:opacity-40">×</button>}</article>;
}
