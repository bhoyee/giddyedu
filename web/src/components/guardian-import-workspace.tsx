"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

type PreviewRow = { row: number; admissionNumber: string; guardianName: string; studentName: string | null;
  className: string | null; relationship: string; error: string | null };
type Preview = { operationId: string; totalRows: number; validRows: number; rejectedRows: number; rows: PreviewRow[] };
type Operation = { id: string; status: number; totalRows: number; importedRows: number; rejectedRows: number;
  errorSummary: string | null; errorFileId: string | null; createdAtUtc: string };
const base = "/api/backend/guardians/imports";
const statuses = ["Awaiting upload", "Queued", "Processing", "Completed", "Failed"];

async function responseError(response: Response) {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  return body?.detail ?? body?.title ?? `Request failed (${response.status}).`;
}

export function GuardianImportWorkspace() {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [operations, setOperations] = useState<Operation[]>([]);
  const [pendingDelete, setPendingDelete] = useState<Operation | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const refresh = useCallback(async () => {
    const response = await fetch(base, { cache: "no-store" });
    if (response.ok) setOperations(await response.json() as Operation[]);
    else setError(await responseError(response));
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void refresh(); }, 0); return () => window.clearTimeout(timer); }, [refresh]);
  useEffect(() => {
    if (!operations.some(item => item.status === 1 || item.status === 2)) return;
    const timer = window.setInterval(() => { void refresh(); }, 3000);
    return () => window.clearInterval(timer);
  }, [operations, refresh]);

  async function downloadTemplate(format: "csv" | "xlsx") {
    setError("");
    const response = await fetch(`${base}/template.${format}`);
    if (!response.ok) { setError(await responseError(response)); return; }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a"); link.href = url; link.download = `giddyedu-guardians-template.${format}`; link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  async function review() {
    if (!file) { setError("Choose a file to upload."); return; }
    setBusy(true); setError(""); setNotice(""); setPreview(null);
    try {
      if (file.size > 5 * 1024 * 1024 || !/\.(csv|xlsx)$/i.test(file.name))
        throw new Error("Choose a CSV or Excel (.xlsx) file no larger than 5 MB.");
      const contentType = file.type || (file.name.toLowerCase().endsWith(".csv")
        ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
      const begin = await fetch(`${base}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fileName: file.name, contentType, sizeBytes: file.size }) });
      if (!begin.ok) throw new Error(await responseError(begin));
      const upload = await begin.json() as { operationId: string };
      const stored = await fetch(`${base}/${upload.operationId}/content`, { method: "PUT", headers: { "Content-Type": contentType }, body: file });
      if (!stored.ok) throw new Error(await responseError(stored));
      const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
      const checksum = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
      const response = await fetch(`${base}/${upload.operationId}/preview`, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ checksum }) });
      if (!response.ok) throw new Error(await responseError(response));
      setPreview(await response.json() as Preview);
      await refresh();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The file could not be reviewed.");
      await refresh();
    } finally { setBusy(false); }
  }

  async function confirm() {
    if (!preview?.validRows) return;
    setBusy(true); setError("");
    try {
      const response = await fetch(`${base}/${preview.operationId}/confirm`, { method: "POST" });
      if (!response.ok) throw new Error(await responseError(response));
      setPreview(null); setFile(null);
      setNotice("Import queued. Each newly connected parent or guardian will receive one secure account invitation.");
      await refresh();
    } catch (caught) { setError(caught instanceof Error ? caught.message : "The import could not be queued."); }
    finally { setBusy(false); }
  }

  async function deleteDraft(operation: Operation) {
    setBusy(true); setError("");
    try {
      const response = await fetch(`${base}/${operation.id}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await responseError(response));
      setPendingDelete(null);
      if (preview?.operationId === operation.id) { setPreview(null); setFile(null); }
      setNotice("Upload draft removed. No guardian records were affected.");
      await refresh();
    } catch (caught) { setError(caught instanceof Error ? caught.message : "The draft could not be deleted."); }
    finally { setBusy(false); }
  }

  return <section className="space-y-6">
    <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8">
      <Link href="/portal/guardians" className="text-sm font-semibold tenant-primary-text hover:underline">← Guardian directory</Link>
      <p className="mt-6 text-xs font-bold uppercase tracking-[.18em] tenant-primary-text">People / Guardians</p>
      <h1 className="mt-2 text-3xl font-black tracking-tight text-slate-950">Import guardians</h1>
      <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600">Add one row per guardian–student relationship. Use the student admission number, then review the matched name and class before confirming. The active campus applies to every row.</p>
      <div className="mt-7 grid gap-5 lg:grid-cols-2"><div className="rounded-2xl border border-slate-200 bg-slate-50 p-5"><h2 className="text-sm font-bold text-slate-900">1. Download a template</h2><p className="mt-2 text-xs leading-5 text-slate-600">The Excel file includes relationship keys and instructions. CSV uses the same data columns.</p><div className="mt-4 flex flex-wrap gap-2"><button type="button" onClick={() => void downloadTemplate("xlsx")} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100">Excel template</button><button type="button" onClick={() => void downloadTemplate("csv")} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100">CSV template</button></div></div>
      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5"><label htmlFor="guardian-import-file" className="block text-sm font-bold text-slate-900">2. Upload and review</label><input id="guardian-import-file" type="file" accept=".csv,.xlsx" onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); }} className="mt-3 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white p-3 text-sm text-slate-700 file:mr-4 file:cursor-pointer file:rounded-lg file:border-0 file:bg-slate-900 file:px-4 file:py-2 file:font-bold file:text-white"/><p className="mt-3 text-xs leading-5 text-slate-600">Up to 500 relationships and 5 MB per file. Add photographs and signatures to individual profiles afterward.</p><button type="button" disabled={!file || busy} onClick={() => void review()} className="tenant-primary-bg mt-4 rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Working..." : "Upload and review"}</button></div></div>
      {error && <div role="alert" className="mt-5 flex justify-between rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"><span>{error}</span><button type="button" onClick={() => setError("")} aria-label="Dismiss error" className="ml-3 font-bold">×</button></div>}
      {notice && <p role="status" className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">{notice}</p>}
    </div>
    {preview && <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8"><div className="flex flex-wrap items-center justify-between gap-4"><div><h2 className="text-xl font-black text-slate-950">Review matched students</h2><p className="mt-1 text-sm text-slate-600">{preview.totalRows} rows · {preview.validRows} ready · {preview.rejectedRows} need correction</p></div><button type="button" disabled={busy || preview.validRows === 0} onClick={() => void confirm()} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50">Import {preview.validRows} valid rows</button></div><p className="mt-3 text-xs text-slate-500">Invalid rows are skipped and available in a downloadable error report. Guardians are reused when their email and phone match; invitations are sent once per unconnected guardian.</p><div className="mt-5 max-h-[32rem] overflow-auto rounded-xl border border-slate-200"><table className="w-full min-w-[760px] text-left text-sm"><thead className="sticky top-0 bg-slate-50 text-slate-600"><tr>{["Row", "Guardian", "Admission no.", "Matched student / class", "Relationship", "Result"].map(label => <th key={label} className="px-4 py-3 font-bold">{label}</th>)}</tr></thead><tbody>{preview.rows.map(row => <tr key={row.row} className="border-t border-slate-100"><td className="px-4 py-3">{row.row}</td><td className="px-4 py-3">{row.guardianName}</td><td className="px-4 py-3">{row.admissionNumber}</td><td className="px-4 py-3">{row.studentName ? `${row.studentName} · ${row.className ?? "Class not set"}` : "Not found"}</td><td className="px-4 py-3">{row.relationship}</td><td className={`px-4 py-3 ${row.error ? "text-red-700" : "text-emerald-700"}`}>{row.error ?? "Ready"}</td></tr>)}</tbody></table></div></div>}
    <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8"><h2 className="text-xl font-black text-slate-950">Import history</h2><p className="mt-1 text-sm text-slate-600">Recent imports for this campus. Only unfinished upload drafts can be deleted.</p><div className="mt-5 max-h-[32rem] space-y-3 overflow-y-auto pr-1">{operations.length === 0 ? <p className="rounded-xl bg-slate-50 p-5 text-sm text-slate-500">No guardian imports yet.</p> : operations.map(operation => <div key={operation.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4"><div><p className="font-bold text-slate-900">{statuses[operation.status] ?? "Unknown"}<span className="ml-2 font-normal text-slate-500">{new Date(operation.createdAtUtc).toLocaleString()}</span></p><p className="mt-1 text-sm text-slate-600">{operation.importedRows} linked · {operation.rejectedRows} rejected{operation.errorSummary ? ` · ${operation.errorSummary}` : ""}</p></div><div className="flex flex-wrap gap-2">{operation.errorFileId && <a href={`${base}/${operation.id}/errors`} className="rounded-lg border border-slate-300 bg-slate-100 px-3 py-2 text-sm font-bold text-slate-800 hover:bg-slate-200">Download errors</a>}{operation.status === 0 && <button type="button" disabled={busy} onClick={() => setPendingDelete(operation)} className="rounded-lg bg-red-600 px-3 py-2 text-sm font-bold text-white hover:bg-red-700 disabled:opacity-50">Delete draft</button>}</div></div>)}</div></div>
    {pendingDelete && <div className="fixed inset-0 z-[160] flex items-center justify-center bg-slate-950/50 p-4"><div role="dialog" aria-modal="true" aria-labelledby="guardian-draft-delete-title" className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl"><h2 id="guardian-draft-delete-title" className="text-xl font-black text-slate-950">Delete this upload draft?</h2><p className="mt-2 text-sm text-slate-600">This removes only the awaiting-upload file. No guardian records are affected.</p><div className="mt-6 flex justify-end gap-3"><button type="button" disabled={busy} onClick={() => setPendingDelete(null)} className="rounded-xl bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 hover:bg-red-100">Cancel</button><button type="button" disabled={busy} onClick={() => void deleteDraft(pendingDelete)} className="rounded-xl bg-red-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-red-700">Delete draft</button></div></div></div>}
  </section>;
}
