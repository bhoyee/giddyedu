"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

type PreviewRow = { row: number; firstName: string; lastName: string; category: string; position: string | null; error: string | null };
type Preview = { operationId: string; totalRows: number; validRows: number; rejectedRows: number; rows: PreviewRow[] };
type Operation = { id: string; category: string | null; status: number; totalRows: number; importedRows: number; rejectedRows: number; errorSummary: string | null; errorFileId: string | null; createdAtUtc: string };
const base = "/api/backend/hr/staff/imports";
const statuses = ["Awaiting upload", "Queued", "Processing", "Completed", "Failed"];

async function errorMessage(response: Response): Promise<string> {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string; message?: string } | null;
  return body?.detail ?? body?.message ?? body?.title ?? `Request failed (${response.status}).`;
}

export function StaffImportWorkspace() {
  const [file, setFile] = useState<File | null>(null);
  const [category, setCategory] = useState("");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [operations, setOperations] = useState<Operation[]>([]);
  const [pendingDelete, setPendingDelete] = useState<Operation | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const refresh = useCallback(async () => {
    const response = await fetch(base, { cache: "no-store" });
    if (response.ok) setOperations(await response.json() as Operation[]);
    else setError(await errorMessage(response));
  }, []);
  useEffect(() => {
    const timer = window.setTimeout(() => { void refresh(); }, 0);
    return () => window.clearTimeout(timer);
  }, [refresh]);
  useEffect(() => {
    if (!operations.some(operation => operation.status === 1 || operation.status === 2)) return;
    const timer = window.setInterval(() => { void refresh(); }, 3000);
    return () => window.clearInterval(timer);
  }, [operations, refresh]);

  async function downloadTemplate(format: "csv" | "xlsx") {
    if (!category) { setError("Choose a staff category first."); return; }
    setError("");
    const response = await fetch(`${base}/template.${format}?category=${category}`);
    if (!response.ok) { setError(await errorMessage(response)); return; }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a"); link.href = url; link.download = `giddyedu-${category === "0" ? "teaching" : category === "1" ? "administrative" : "non-teaching"}-staff-template.${format}`; link.click();
    URL.revokeObjectURL(url);
  }

  async function review() {
    if (!file || !category) { setError("Choose a category and file before uploading."); return; }
    setBusy(true); setError(""); setNotice(""); setPreview(null);
    try {
      if (file.size > 5 * 1024 * 1024 || !/\.(csv|xlsx)$/i.test(file.name)) throw new Error("Choose a CSV or Excel (.xlsx) file no larger than 5 MB.");
      const contentType = file.type || (file.name.toLowerCase().endsWith(".csv") ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
      const begin = await fetch(`${base}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fileName: file.name, contentType, sizeBytes: file.size, category: Number(category) }) });
      if (!begin.ok) throw new Error(await errorMessage(begin));
      const upload = await begin.json() as { operationId: string };
      const stored = await fetch(`${base}/${upload.operationId}/content`, { method: "PUT", headers: { "Content-Type": contentType }, body: file });
      if (!stored.ok) throw new Error(await errorMessage(stored));
      const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
      const checksum = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
      const reviewed = await fetch(`${base}/${upload.operationId}/preview`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checksum }) });
      if (!reviewed.ok) throw new Error(await errorMessage(reviewed));
      setPreview(await reviewed.json() as Preview);
      await refresh();
    } catch (caught) { setError(caught instanceof TypeError ? "GiddyEdu could not reach the upload service. Check that the server is running and try again." : caught instanceof Error ? caught.message : "The file could not be reviewed."); await refresh(); }
    finally { setBusy(false); }
  }

  async function confirm() {
    if (!preview || preview.validRows === 0) return;
    setBusy(true); setError("");
    try {
      const response = await fetch(`${base}/${preview.operationId}/confirm`, { method: "POST" });
      if (!response.ok) throw new Error(await errorMessage(response));
      setPreview(null); setFile(null); setNotice("Import queued. Each successfully imported staff member will receive a secure account invitation by email.");
      await refresh();
    } catch (caught) { setError(caught instanceof Error ? caught.message : "The import could not be queued."); }
    finally { setBusy(false); }
  }

  function downloadErrors(id: string) {
    window.open(`${base}/${id}/errors`, "_blank", "noopener,noreferrer");
  }

  async function deleteDraft(operation: Operation) {
    setBusy(true); setError("");
    try {
      const response = await fetch(`${base}/${operation.id}`, { method: "DELETE" });
      if (!response.ok) throw new Error(await errorMessage(response));
      setPendingDelete(null);
      if (preview?.operationId === operation.id) { setPreview(null); setFile(null); }
      setNotice("Awaiting-upload draft deleted. No staff records were affected.");
      await refresh();
    } catch (caught) { setError(caught instanceof Error ? caught.message : "The draft could not be deleted."); }
    finally { setBusy(false); }
  }

  return <section className="space-y-6">
    <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8">
      <Link href="/portal/staff" className="text-sm font-semibold tenant-primary-text hover:underline">← Staff directory</Link>
      <div className="mt-5 flex flex-col gap-5 lg:flex-row lg:justify-between"><div><p className="text-xs font-bold uppercase tracking-[.18em] tenant-primary-text">People / HR</p><h1 className="mt-2 text-3xl font-black tracking-tight text-slate-950">Import staff</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-600">Choose a category, fill its template, then review every row before confirming. Everyone in this upload joins the active campus.</p></div></div>
      <div className="mt-7 grid gap-5 lg:grid-cols-2"><div className="rounded-2xl border border-slate-200 bg-slate-50 p-5"><label htmlFor="staff-import-category" className="block text-sm font-bold text-slate-900">1. Choose staff category</label><select id="staff-import-category" value={category} onChange={event => { setCategory(event.target.value); setFile(null); setPreview(null); setError(""); }} className="mt-3 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm text-slate-900"><option value="">Select category</option><option value="0">Teaching</option><option value="1">Administrative</option><option value="2">Non-teaching</option></select><p className="mt-3 text-xs leading-5 text-slate-500">The selected category applies to the full file and cannot be changed after upload.</p></div><div className="rounded-2xl border border-slate-200 bg-slate-50 p-5"><p className="text-sm font-bold text-slate-900">2. Download a category template</p><p className="mt-2 text-xs leading-5 text-slate-500">The keys area lists your school’s current positions for this category and accepted gender values.</p><div className="mt-4 flex flex-wrap gap-2"><button type="button" disabled={!category} onClick={() => void downloadTemplate("xlsx")} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50">Excel template</button><button type="button" disabled={!category} onClick={() => void downloadTemplate("csv")} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50">CSV template</button></div></div></div>
      <div className="mt-5 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-5"><label htmlFor="staff-import-file" className="block text-sm font-bold text-slate-900">3. Upload the completed template</label><input key={category} id="staff-import-file" type="file" accept=".csv,.xlsx" disabled={!category} onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); }} className="mt-3 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white p-3 text-sm text-slate-700 file:mr-4 file:cursor-pointer file:rounded-lg file:border-0 file:bg-slate-900 file:px-4 file:py-2 file:font-bold file:text-white disabled:cursor-not-allowed disabled:opacity-50"/><p className="mt-3 text-xs leading-5 text-slate-500">Up to 500 records and 5 MB. Position must match a key for the selected category. Photos, signatures, qualifications and documents can be added to each staff profile after import. Successfully imported staff will receive a secure account invitation.</p><button type="button" onClick={() => void review()} disabled={!category || !file || busy} className="tenant-primary-bg mt-5 rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:cursor-not-allowed disabled:opacity-50">{busy ? "Working…" : "Upload and review"}</button></div>
      {error && <div role="alert" className="mt-5 flex items-start justify-between rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"><span>{error}</span><button type="button" onClick={() => setError("")} aria-label="Dismiss error" className="ml-3 font-bold">×</button></div>}{notice && <p role="status" className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">{notice}</p>}
    </div>
    {preview && <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8"><div className="flex flex-wrap items-center justify-between gap-4"><div><h2 className="text-xl font-black text-slate-950">Review before import</h2><p className="mt-1 text-sm text-slate-600">{preview.totalRows} rows · {preview.validRows} ready · {preview.rejectedRows} need correction</p></div><button type="button" disabled={busy || preview.validRows === 0} onClick={() => void confirm()} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50">Import {preview.validRows} valid rows</button></div><p className="mt-3 text-xs text-slate-500">Confirming creates the valid staff records and queues their account invitations. Rows with errors will be skipped; correct them in your file and upload a new import afterward.</p><div className="mt-5 max-h-[32rem] overflow-auto rounded-xl border border-slate-200"><table className="w-full min-w-[700px] text-left text-sm"><thead className="sticky top-0 bg-slate-50 text-slate-600"><tr>{["Row", "Name", "Category", "Position", "Result"].map(label => <th key={label} className="px-4 py-3 font-bold">{label}</th>)}</tr></thead><tbody>{preview.rows.map(row => <tr key={row.row} className="border-t border-slate-100"><td className="px-4 py-3">{row.row}</td><td className="px-4 py-3">{row.firstName} {row.lastName}</td><td className="px-4 py-3">{row.category}</td><td className="px-4 py-3">{row.position || "—"}</td><td className={`px-4 py-3 ${row.error ? "text-red-700" : "text-emerald-700"}`}>{row.error || "Ready"}</td></tr>)}</tbody></table></div></div>}
    <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><h2 className="text-xl font-black text-slate-950">Import history</h2><p className="mt-1 text-sm text-slate-600">Imports for the active campus. Unfinished upload drafts can be deleted.</p></div>
      </div>
      <div className="mt-5 max-h-[32rem] space-y-3 overflow-y-auto pr-1">
        {operations.length === 0 ? <p className="rounded-xl bg-slate-50 p-5 text-sm text-slate-500">No staff imports yet.</p> : operations.map(operation =>
          <div key={operation.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4">
            <div><p className="font-bold text-slate-900">{operation.category ? operation.category + " · " : ""}{statuses[operation.status] ?? "Unknown"} <span className="ml-2 font-normal text-slate-500">{new Date(operation.createdAtUtc).toLocaleString()}</span></p><p className="mt-1 text-sm text-slate-600">{operation.importedRows} imported · {operation.rejectedRows} rejected{operation.errorSummary ? " · " + operation.errorSummary : ""}</p></div>
            <div className="flex flex-wrap gap-2">{operation.errorFileId && <button type="button" onClick={() => void downloadErrors(operation.id)} className="rounded-lg border border-slate-300 bg-slate-100 px-3 py-2 text-sm font-bold text-slate-800 hover:bg-slate-200">Download errors</button>}{operation.status === 0 && <button type="button" disabled={busy} onClick={() => setPendingDelete(operation)} className="rounded-lg bg-red-600 px-3 py-2 text-sm font-bold text-white transition hover:bg-red-700 disabled:opacity-50">Delete draft</button>}</div>
          </div>)}
      </div>
    </div>
    {pendingDelete && <div className="fixed inset-0 z-[160] flex items-center justify-center bg-slate-950/50 p-4" role="presentation"><div role="dialog" aria-modal="true" aria-labelledby="delete-import-title" className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl"><h2 id="delete-import-title" className="text-xl font-black text-slate-950">Delete this upload draft?</h2><p className="mt-2 text-sm leading-6 text-slate-600">This permanently removes the awaiting-upload import and its uploaded file, if any. No staff records have been created from this draft.</p><div className="mt-6 flex justify-end gap-3"><button type="button" disabled={busy} onClick={() => setPendingDelete(null)} className="rounded-xl border border-slate-300 px-4 py-2.5 text-sm font-bold text-slate-800 hover:bg-slate-100">Cancel</button><button type="button" disabled={busy} onClick={() => void deleteDraft(pendingDelete)} className="rounded-xl bg-red-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-red-700 disabled:opacity-50">Delete draft</button></div></div></div>}
  </section>;
}
