"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";

type Value = string | number | boolean | null;
interface Data { [key: string]: Value | Data | Data[] }
type Field = { name: string; label: string; type?: string; required?: boolean; numeric?: boolean };
type Props = { id: string; kind: "Student" | "Guardian" | "StaffProfile"; endpoint: string; backPath: string; title: string; fields: Field[]; sensitiveEndpoint?: string };

const managePermission = { Student: "Students.Manage", Guardian: "Guardians.Manage", StaffProfile: "Staff.Manage" } as const;

export function ProfileDetail({ id, kind, endpoint, backPath, title, fields, sensitiveEndpoint }: Props) {
  const [data, setData] = useState<Data | null>(null);
  const [documents, setDocuments] = useState<Data[]>([]);
  const [sensitive, setSensitive] = useState<Data | null>(null);
  const [canManage, setCanManage] = useState(false);
  const [accessLoaded, setAccessLoaded] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [revision, setRevision] = useState(0);
  const profile = (kind === "Student" && data?.student && typeof data.student === "object" && !Array.isArray(data.student) ? data.student : data) as Data | null;

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const body = await response.json() as { access?: { permissions?: string[] } };
      setCanManage(body.access?.permissions?.includes(managePermission[kind]) === true);
    }).finally(() => setAccessLoaded(true));
  }, [kind]);

  useEffect(() => {
    void fetch(`/api/backend/${endpoint}`, { cache: "no-store" }).then(async response => {
      if (!response.ok) throw new Error("Profile could not be loaded.");
      setData(await response.json() as Data);
    }).catch(value => setError(value instanceof Error ? value.message : "Profile could not be loaded."));
    void fetch(`/api/backend/documents/${kind}/${id}`, { cache: "no-store" }).then(async response => { if (response.ok) setDocuments(await response.json() as Data[]); });
    if (sensitiveEndpoint) void fetch(`/api/backend/${sensitiveEndpoint}`, { cache: "no-store" }).then(async response => { if (response.ok) setSensitive(await response.json() as Data); });
  }, [endpoint, id, kind, revision, sensitiveEndpoint]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) { setError("You do not have permission to change this profile."); return; }
    setError("");
    const form = new FormData(event.currentTarget);
    const payload = Object.fromEntries(fields.map(field => {
      const value = form.get(field.name);
      return [field.name, value === "" && field.required === false ? null : field.numeric ? Number(value) : value];
    }));
    const response = await fetch(`/api/backend/${endpoint}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    if (!response.ok) { setError("Changes could not be saved. Check your permission and values."); return; }
    setMessage("Profile updated."); setRevision(value => value + 1);
  }

  async function download(fileId: string) {
    const response = await fetch(`/api/backend/documents/${fileId}/download`);
    if (!response.ok) { setError("Document download is not permitted."); return; }
    const body = await response.json() as { url: string }; window.open(body.url, "_blank", "noopener,noreferrer");
  }

  async function remove(fileId: string) {
    if (!canManage || !window.confirm("Remove this document?")) return;
    const response = await fetch(`/api/backend/documents/${fileId}`, { method: "DELETE" });
    if (!response.ok) { setError("Document could not be removed."); return; }
    setDocuments(current => current.filter(item => item.id !== fileId));
  }

  return <main className="min-h-screen bg-[#f6f4ee]">
    <header className="bg-[#12372a] text-white"><div className="mx-auto max-w-6xl px-6 py-5"><Link href={backPath} className="font-bold text-emerald-100">← Back</Link></div></header>
    <div className="mx-auto max-w-6xl space-y-8 px-6 py-10">
      <div><h1 className="text-4xl font-black">{title}</h1><p className="mt-2 text-slate-600">Record ID: {id}</p></div>
      {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
      {!profile || !accessLoaded ? <section className="rounded-3xl bg-white p-8">Loading profile…</section> : <section className="rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-xl font-black">Profile</h2>{canManage ? <form onSubmit={save} className="mt-5 grid gap-4 sm:grid-cols-2">{fields.map(field => <label key={field.name} className="text-sm font-semibold">{field.label}<input name={field.name} type={field.type ?? "text"} required={field.required !== false} defaultValue={String(profile[field.name] ?? "")} className="mt-1 w-full rounded-xl border border-slate-300 px-4 py-3" /></label>)}<button className="sm:col-span-2 rounded-xl bg-emerald-700 px-4 py-3 font-bold text-white">Save changes</button>{message && <p role="status" className="sm:col-span-2 text-sm text-emerald-800">{message}</p>}</form> : <dl className="mt-5 grid gap-4 sm:grid-cols-2">{fields.map(field => <div key={field.name} className="rounded-xl bg-slate-50 p-4"><dt className="text-xs font-bold uppercase text-slate-500">{field.label}</dt><dd className="mt-1 text-slate-900">{String(profile[field.name] ?? "—")}</dd></div>)}</dl>}</section>}
      {kind === "Student" && data && <section className="grid gap-6 lg:grid-cols-2"><JsonPanel title="Enrolment history" value={data.enrollments} /><JsonPanel title="Guardian relationships" value={data.guardians} /></section>}
      {sensitive && <JsonPanel title="Restricted information" value={sensitive} />}
      <section className="rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-xl font-black">Documents</h2>{documents.length === 0 ? <p className="mt-3 text-slate-600">No accessible documents.</p> : <ul className="mt-4 space-y-3">{documents.map(document => <li key={String(document.id)} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4"><span><strong>{String(document.fileName)}</strong><br /><small>{String(document.category)} · {String(document.status)}</small></span><span className="flex gap-2"><button onClick={() => void download(String(document.id))} className="rounded-lg border px-3 py-2 text-sm">Download</button>{canManage && <button onClick={() => void remove(String(document.id))} className="rounded-lg border border-red-300 px-3 py-2 text-sm text-red-700">Delete</button>}</span></li>)}</ul>}</section>
    </div>
  </main>;
}

function JsonPanel({ title, value }: { title: string; value: Value | Data | Data[] | undefined }) { return <section className="rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-xl font-black">{title}</h2><pre className="mt-4 overflow-auto whitespace-pre-wrap text-sm text-slate-700">{JSON.stringify(value ?? [], null, 2)}</pre></section>; }
