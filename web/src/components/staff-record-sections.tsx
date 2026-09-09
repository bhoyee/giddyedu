"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";

type RecordValue = string | boolean | null;
type StaffRecord = { id: string; [key: string]: RecordValue };

export function StaffRecordSections({ staffId }: { staffId: string }) {
  const [employment, setEmployment] = useState<StaffRecord[]>([]);
  const [qualifications, setQualifications] = useState<StaffRecord[]>([]);
  const [nextOfKin, setNextOfKin] = useState<StaffRecord[]>([]);
  const [canManage, setCanManage] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    const requests = ["employment", "qualifications", "next-of-kin"].map(path => fetch(`/api/backend/hr/staff/${staffId}/${path}`, { cache: "no-store" }));
    const [employmentResponse, qualificationResponse, nextOfKinResponse] = await Promise.all(requests);
    if (employmentResponse.ok) setEmployment(await employmentResponse.json() as StaffRecord[]);
    if (qualificationResponse.ok) setQualifications(await qualificationResponse.json() as StaffRecord[]);
    if (nextOfKinResponse.ok) setNextOfKin(await nextOfKinResponse.json() as StaffRecord[]);
  }, [staffId]);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const body = await response.json() as { access?: { permissions?: string[] } };
      setCanManage(body.access?.permissions?.includes("Staff.Manage") === true);
    });
    void Promise.resolve().then(load);
  }, [load]);

  async function submit(event: FormEvent<HTMLFormElement>, path: string, transform?: (form: FormData) => object) {
    event.preventDefault(); setError(""); const form = new FormData(event.currentTarget);
    const payload = transform ? transform(form) : Object.fromEntries(form.entries());
    const response = await fetch(`/api/backend/hr/staff/${staffId}/${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    if (!response.ok) { setError("The record could not be saved. Check the values and your permissions."); return; }
    event.currentTarget.reset(); await load();
  }

  async function remove(path: string, id: string) {
    if (!window.confirm("Delete this record?")) return;
    const response = await fetch(`/api/backend/hr/staff/${staffId}/${path}/${id}`, { method: "DELETE" });
    if (!response.ok) { setError("The record could not be deleted."); return; }
    await load();
  }

  return <section className="space-y-6">
    {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
    <RecordPanel title="Employment history" records={employment} path="employment" canManage={canManage} onRemove={remove}>
      <form onSubmit={event => void submit(event, "employment", form => ({ employerName: form.get("employerName"), jobTitle: form.get("jobTitle"), startedOn: form.get("startedOn"), endedOn: form.get("endedOn") || null, reasonForLeaving: form.get("reasonForLeaving") || null }))} className="grid gap-3 sm:grid-cols-2">
        <Input name="employerName" label="Employer" /><Input name="jobTitle" label="Job title" /><Input name="startedOn" label="Started" type="date" /><Input name="endedOn" label="Ended" type="date" required={false} /><Input name="reasonForLeaving" label="Reason for leaving" required={false} /><Submit />
      </form>
    </RecordPanel>
    <RecordPanel title="Qualifications" records={qualifications} path="qualifications" canManage={canManage} onRemove={remove}>
      <form onSubmit={event => void submit(event, "qualifications", form => ({ institution: form.get("institution"), name: form.get("name"), fieldOfStudy: form.get("fieldOfStudy") || null, awardedOn: form.get("awardedOn"), grade: form.get("grade") || null }))} className="grid gap-3 sm:grid-cols-2">
        <Input name="institution" label="Institution" /><Input name="name" label="Qualification" /><Input name="fieldOfStudy" label="Field of study" required={false} /><Input name="awardedOn" label="Awarded" type="date" /><Input name="grade" label="Grade" required={false} /><Submit />
      </form>
    </RecordPanel>
    <RecordPanel title="Next of kin" records={nextOfKin} path="next-of-kin" canManage={canManage} onRemove={remove}>
      <form onSubmit={event => void submit(event, "next-of-kin", form => ({ fullName: form.get("fullName"), relationship: form.get("relationship"), phone: form.get("phone"), email: form.get("email") || null, address: form.get("address") || null, isPrimary: form.get("isPrimary") === "on" }))} className="grid gap-3 sm:grid-cols-2">
        <Input name="fullName" label="Full name" /><Input name="relationship" label="Relationship" /><Input name="phone" label="Phone" /><Input name="email" label="Email" type="email" required={false} /><Input name="address" label="Address" required={false} /><label className="flex items-center gap-2 text-sm font-semibold"><input name="isPrimary" type="checkbox" /> Primary contact</label><Submit />
      </form>
    </RecordPanel>
  </section>;
}

function RecordPanel({ title, records, path, canManage, onRemove, children }: { title: string; records: StaffRecord[]; path: string; canManage: boolean; onRemove: (path: string, id: string) => Promise<void>; children: ReactNode }) {
  return <section className="rounded-3xl bg-white p-6 shadow-sm"><h2 className="text-xl font-black">{title}</h2>{records.length === 0 ? <p className="my-4 text-slate-600">No records added.</p> : <ul className="my-4 space-y-3">{records.map(record => <li key={record.id} className="rounded-xl border border-slate-200 p-4"><pre className="whitespace-pre-wrap text-sm">{JSON.stringify(record, null, 2)}</pre>{canManage && <button onClick={() => void onRemove(path, record.id)} className="mt-2 text-sm font-bold text-red-700">Delete</button>}</li>)}</ul>}{canManage && children}</section>;
}
function Input({ name, label, type = "text", required = true }: { name: string; label: string; type?: string; required?: boolean }) { return <label className="text-sm font-semibold">{label}<input name={name} type={type} required={required} className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2" /></label>; }
function Submit() { return <button className="self-end rounded-xl bg-emerald-700 px-4 py-3 font-bold text-white">Add record</button>; }
