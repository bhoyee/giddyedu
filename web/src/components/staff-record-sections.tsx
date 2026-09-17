"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";
import { staffKinRelationships } from "@/lib/staff-kin-relationships";
import { confirmAction } from "@/components/confirm-action";

type RecordValue = string | boolean | null;
type StaffRecord = { id: string; [key: string]: RecordValue };

export function StaffRecordSections({ staffId, onChanged }: { staffId: string; onChanged?: () => void }) {
  const [employment, setEmployment] = useState<StaffRecord[]>([]);
  const [qualifications, setQualifications] = useState<StaffRecord[]>([]);
  const [nextOfKin, setNextOfKin] = useState<StaffRecord[]>([]);
  const [canManage, setCanManage] = useState(false);
  const [error, setError] = useState("");
  const [kinRelationship, setKinRelationship] = useState("");

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
    event.currentTarget.reset(); if (path === "next-of-kin") setKinRelationship(""); await load(); onChanged?.();
  }

  async function remove(path: string, id: string) {
    if (!await confirmAction({ title: "Delete staff record detail?", message: "This employment, qualification, or contact detail will be permanently deleted. This cannot be undone.", confirmLabel: "Delete detail", confirmText: "DELETE" })) return;
    const response = await fetch(`/api/backend/hr/staff/${staffId}/${path}/${id}`, { method: "DELETE" });
    if (!response.ok) { setError("The record could not be deleted."); return; }
    await load(); onChanged?.();
  }

  return <section id="staff-history" className="scroll-mt-8 space-y-5">
    <div><p className="text-[10px] font-black uppercase tracking-[.18em] tenant-primary-text">Background</p><h2 className="mt-1 text-2xl font-black tracking-tight text-slate-950">History and contacts</h2><p className="mt-1 text-sm text-slate-500">Prior employment, qualifications and trusted emergency contacts.</p></div>
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
      <form onSubmit={event => void submit(event, "next-of-kin", form => ({ fullName: form.get("fullName"), relationship: kinRelationship === "Other" ? form.get("otherRelationship") : kinRelationship, phone: form.get("phone"), email: form.get("email") || null, address: form.get("address") || null, isPrimary: form.get("isPrimary") === "on" }))} className="grid gap-3 sm:grid-cols-2">
        <Input name="fullName" label="Full name" /><label className="text-sm font-semibold">Relationship<select value={kinRelationship} onChange={event=>setKinRelationship(event.target.value)} required className="mt-1 w-full rounded-xl border border-slate-300 bg-white px-3 py-2"><option value="">Select relationship</option>{staffKinRelationships.map(relationship=><option key={relationship} value={relationship}>{relationship}</option>)}</select></label>{kinRelationship==="Other"&&<Input name="otherRelationship" label="Specify relationship"/>}<Input name="phone" label="Phone" type="tel" /><Input name="email" label="Email" type="email" required={false} /><Input name="address" label="Address" required={false} /><label className="flex items-center gap-2 text-sm font-semibold"><input name="isPrimary" type="checkbox" /> Primary contact</label><Submit />
      </form>
    </RecordPanel>
  </section>;
}

function RecordPanel({ title, records, path, canManage, onRemove, children }: { title: string; records: StaffRecord[]; path: string; canManage: boolean; onRemove: (path: string, id: string) => Promise<void>; children: ReactNode }) {
  const [adding,setAdding]=useState(false);
  const primary=path==="employment"?"jobTitle":path==="qualifications"?"name":"fullName";
  const secondary=path==="employment"?"employerName":path==="qualifications"?"institution":"relationship";
  const fields=path==="employment"?["startedOn","endedOn","reasonForLeaving"]:path==="qualifications"?["fieldOfStudy","awardedOn","grade"]:["phone","email","address"];
  const labels:Record<string,string>={startedOn:"Started",endedOn:"Ended",reasonForLeaving:"Reason for leaving",fieldOfStudy:"Field of study",awardedOn:"Awarded",grade:"Grade",phone:"Phone",email:"Email",address:"Address"};
  return <section className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h3 className="text-lg font-black text-slate-950">{title}</h3><p className="mt-1 text-xs text-slate-500">{records.length} {records.length===1?"entry":"entries"} on file</p></div>{canManage&&<button type="button" onClick={()=>setAdding(value=>!value)} className="rounded-xl border border-slate-200 px-4 py-2 text-xs font-bold tenant-primary-text transition hover:bg-slate-50">{adding?"Close form":"+ Add entry"}</button>}</div>
    {records.length?<ul className="mt-5 divide-y divide-slate-100">{records.map(record=><li key={record.id} className="flex flex-wrap items-start justify-between gap-3 py-4"><div className="min-w-0"><p className="text-sm font-bold text-slate-950">{String(record[primary]||"Untitled entry")}</p><p className="mt-0.5 text-xs font-medium text-slate-600">{String(record[secondary]||"")}{path==="next-of-kin"&&record.isPrimary?" · Primary contact":""}</p><dl className="mt-3 flex flex-wrap gap-x-6 gap-y-2">{fields.filter(field=>record[field]).map(field=><div key={field}><dt className="text-[10px] font-bold uppercase tracking-wide text-slate-400">{labels[field]}</dt><dd className="mt-0.5 text-xs text-slate-700">{String(record[field])}</dd></div>)}</dl></div>{canManage&&<button type="button" onClick={()=>void onRemove(path,record.id)} className="rounded-lg px-3 py-1.5 text-xs font-bold text-red-700 hover:bg-red-50">Remove</button>}</li>)}</ul>:<p className="mt-5 rounded-xl bg-slate-50 px-4 py-5 text-sm text-slate-500">No {title.toLowerCase()} entries have been added yet.</p>}
    {canManage&&adding&&<div className="mt-5 border-t border-slate-100 pt-5"><p className="mb-4 text-sm font-bold text-slate-900">Add {title.toLowerCase()} entry</p>{children}</div>}
  </section>;
}
function Input({ name, label, type = "text", required = true }: { name: string; label: string; type?: string; required?: boolean }) { return <label className="text-xs font-bold text-slate-700">{label}<input name={name} type={type} required={required} className="mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm font-normal outline-none focus:border-[var(--tenant-primary,#28654a)]" /></label>; }
function Submit() { return <button className="tenant-primary-bg self-end rounded-xl px-4 py-2.5 text-sm font-bold text-white">Save entry</button>; }
