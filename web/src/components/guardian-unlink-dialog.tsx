"use client";

import { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";

type StudentLink = {
  studentId: string;
  admissionNumber: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  classSectionName?: string | null;
  relationship: string;
};

async function errorMessage(response: Response) {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  return body?.detail ?? body?.title ?? "The student link could not be updated.";
}

export function reviewGuardianStudentLinks(guardianId: string): Promise<boolean> {
  return new Promise(resolve => {
    const host = document.createElement("div");
    document.body.appendChild(host);
    const root = createRoot(host);
    let settled = false;
    const finish = (moveToBin: boolean) => {
      if (settled) return;
      settled = true;
      queueMicrotask(() => { root.unmount(); host.remove(); });
      resolve(moveToBin);
    };
    root.render(<GuardianUnlinkDialog guardianId={guardianId} onFinish={finish} />);
  });
}

function GuardianUnlinkDialog({ guardianId, onFinish }: { guardianId: string; onFinish: (moveToBin: boolean) => void }) {
  const [links, setLinks] = useState<StudentLink[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const closeRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    closeRef.current?.focus();
    const controller = new AbortController();
    void fetch(`/api/backend/guardians/${guardianId}/student-links`, { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(await errorMessage(response));
        setLinks(await response.json() as StudentLink[]);
      })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Linked students could not be loaded."); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") onFinish(false); };
    document.addEventListener("keydown", escape);
    return () => { controller.abort(); document.removeEventListener("keydown", escape); };
  }, [guardianId, onFinish]);

  async function unlink(studentId: string) {
    setBusyId(studentId); setError("");
    try {
      const response = await fetch(`/api/backend/guardians/${guardianId}/student-links/${studentId}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await errorMessage(response));
      setLinks(current => current.filter(link => link.studentId !== studentId));
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The student could not be unlinked."); }
    finally { setBusyId(null); }
  }

  async function unlinkAll() {
    setBusyId("all"); setError("");
    try {
      const response = await fetch(`/api/backend/guardians/${guardianId}/student-links`, { method: "DELETE" });
      if (!response.ok) throw new Error(await errorMessage(response));
      setLinks([]);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The students could not be unlinked."); }
    finally { setBusyId(null); }
  }

  return <div className="fixed inset-0 z-[250] grid place-items-center bg-slate-950/60 p-3 backdrop-blur-sm sm:p-4">
    <div role="dialog" aria-modal="true" aria-labelledby="guardian-links-title" className="flex max-h-[90vh] w-full max-w-xl flex-col overflow-hidden rounded-[1.5rem] border border-slate-200 bg-white shadow-2xl">
      <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-5 sm:px-6">
        <div><p className="text-xs font-black uppercase tracking-[0.18em] text-amber-700">Student relationships</p><h2 id="guardian-links-title" className="mt-1 text-xl font-black text-slate-950">Review linked students</h2><p className="mt-1 text-sm leading-6 text-slate-600">Unlink every student before moving this guardian to the bin. Student records will remain unchanged.</p></div>
        <button ref={closeRef} type="button" disabled={Boolean(busyId)} onClick={() => onFinish(false)} aria-label="Close" className="grid size-10 shrink-0 place-items-center rounded-xl border border-slate-200 text-xl text-slate-600 hover:bg-slate-100 disabled:opacity-50">×</button>
      </div>
      <div className="min-h-36 flex-1 overflow-y-auto px-5 py-4 sm:px-6">
        {loading && <div className="grid min-h-32 place-items-center text-sm font-semibold text-slate-500">Loading linked students…</div>}
        {!loading && error && <div role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-800">{error}</div>}
        {!loading && links.length === 0 && <div className="rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-6 text-center"><p className="font-black text-emerald-900">All student links are cleared</p><p className="mt-1 text-sm text-emerald-700">You can now move the guardian to the bin.</p></div>}
        <div className="space-y-2">
          {links.map(link => {
            const fullName = [link.firstName, link.middleName, link.lastName].filter(Boolean).join(" ");
            return <div key={link.studentId} className="flex flex-col gap-3 rounded-2xl border border-slate-200 p-4 sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0"><p className="truncate font-black text-slate-950">{fullName}</p><p className="mt-1 text-xs font-semibold text-slate-500">{link.admissionNumber} · {link.classSectionName ?? "No active class"} · {link.relationship}</p></div>
              <button type="button" disabled={Boolean(busyId)} onClick={() => void unlink(link.studentId)} className="shrink-0 rounded-xl border border-red-200 bg-red-50 px-3.5 py-2 text-xs font-black text-red-700 hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-50">{busyId === link.studentId ? "Unlinking…" : "Unlink"}</button>
            </div>;
          })}
        </div>
      </div>
      <div className="flex flex-col-reverse gap-2 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <button type="button" disabled={Boolean(busyId)} onClick={() => onFinish(false)} className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 hover:bg-red-100 disabled:opacity-50">Cancel</button>
        <div className="flex flex-col gap-2 sm:flex-row">
          {links.length > 1 && <button type="button" disabled={Boolean(busyId)} onClick={() => void unlinkAll()} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100 disabled:opacity-50">{busyId === "all" ? "Unlinking all…" : `Unlink all (${links.length})`}</button>}
          <button type="button" disabled={loading || Boolean(busyId) || links.length > 0} onClick={() => onFinish(true)} className="rounded-xl bg-red-700 px-4 py-2.5 text-sm font-bold text-white hover:bg-red-800 disabled:cursor-not-allowed disabled:opacity-40">Move to bin</button>
        </div>
      </div>
    </div>
  </div>;
}
