"use client";

import { FormEvent, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";

const steps = ["Application", "Personal", "Academic background", "Guardian", "Documents"];
const input = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm outline-none transition focus:border-[var(--tenant-primary,#28654a)] focus:ring-4 focus:ring-emerald-950/5";
const label = "block text-xs font-bold text-slate-700";
const sources = ["Website", "Referral", "Walk-in", "Agent", "Advertisement", "Other"];
const relationships = ["Mother", "Father", "Parent", "LegalGuardian", "Relative", "Sponsor", "Other", "Stepmother", "Stepfather", "Grandmother", "Grandfather", "Aunt", "Uncle", "Sibling", "FosterParent", "AdoptiveParent", "Caregiver"];
type Option = { id: string; name: string };
type Structure = { classLevels?: Option[] };
type GuardianDraft = { name: string; relationship: string; phone: string; email: string; address: string; isPrimary: boolean };
function emptyGuardian(isPrimary: boolean): GuardianDraft { return { name: "", relationship: "2", phone: "", email: "", address: "", isPrimary }; }

export function ApplicantRegistrationWizard({ onCreated }: { onCreated?: () => void }) {
  const [open, setOpen] = useState(false); const [step, setStep] = useState(0); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const [draft, setDraft] = useState<Record<string, string>>({ source: "Website", initialStatus: "draft" });
  const [files, setFiles] = useState<Record<string, File | undefined>>({});
  const [guardians, setGuardians] = useState<GuardianDraft[]>([emptyGuardian(true)]);
  const [structure, setStructure] = useState<Structure>({});
  useEffect(() => { if (!open) return; void fetch("/api/backend/academics/structure", { cache: "no-store" }).then(async response => { if (!response.ok) return; setStructure(await response.json() as Structure); }); }, [open]);
  useEffect(() => { if (!error) return; const timer = window.setTimeout(() => setError(""), 7000); return () => window.clearTimeout(timer); }, [error]);
  function set(name: string, value: string) { setDraft(current => ({ ...current, [name]: value })); }
  function updateGuardian(index: number, patch: Partial<GuardianDraft>) { setGuardians(current => current.map((guardian, i) => i === index ? { ...guardian, ...patch } : guardian)); }
  function addGuardian() { setGuardians(current => [...current, emptyGuardian(false)]); }
  function removeGuardian(index: number) { setGuardians(current => current.filter((_, i) => i !== index)); }
  function validateCurrent() {
    if (step === 1 && (!draft.firstName?.trim() || !draft.lastName?.trim() || !draft.dateOfBirth)) return "Complete the applicant's first name, last name and date of birth.";
    if (step === 1 && draft.phone && !/^\d{11}$/.test(draft.phone)) return "Applicant phone must contain exactly 11 digits.";
    if (step === 3 && guardians.some(guardian => !guardian.name.trim() || !guardian.address.trim() || !/^\d{11}$/.test(guardian.phone) || !guardian.email.trim())) return "Complete each guardian's name, address, email and 11-digit phone number, or remove the incomplete entry.";
    if (step === 3 && guardians.filter(guardian => guardian.isPrimary).length > 1) return "Only one guardian can be marked as the primary guardian.";
    return "";
  }
  function next() { const issue = validateCurrent(); if (issue) { setError(issue); return; } setError(""); setStep(value => Math.min(steps.length - 1, value + 1)); }

  async function upload(applicantId: string, file: File, category: string) {
    const begin = await fetch(`/api/backend/documents/Applicant/${applicantId}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size, category }) });
    if (!begin.ok) throw new Error(`${category} upload could not be started.`);
    const { fileId } = await begin.json() as { fileId: string };
    const stored = await fetch(`/api/backend/documents/${fileId}/content`, { method: "PUT", headers: { "Content-Type": file.type }, body: file });
    if (!stored.ok) throw new Error(`${category} could not be stored.`);
    const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer()); const checksum = Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, "0")).join("");
    const complete = await fetch(`/api/backend/documents/${fileId}/complete`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checksum }) });
    if (!complete.ok) throw new Error(`${category} upload could not be verified.`);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (step < steps.length - 1) { next(); return; }
    const issue = validateCurrent(); if (issue) { setError(issue); return; }
    setBusy(true); setError(""); let applicantCreated = false;
    try {
      const response = await fetch("/api/backend/admissions/applicants", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({
        firstName: draft.firstName, lastName: draft.lastName, dateOfBirth: draft.dateOfBirth, gender: draft.gender || null,
        email: draft.email || null, phone: draft.phone || null, source: draft.source || null,
        proposedClassLevelId: draft.classLevelId || null, submitImmediately: draft.initialStatus === "submitted",
        previousSchool: draft.previousSchool || null, previousSchoolAddress: draft.previousSchoolAddress || null,
        lastClassCompleted: draft.lastClassCompleted || null, leavingDate: draft.leavingDate || null, reasonForLeaving: draft.reasonForLeaving || null,
        guardians: guardians.map(guardian => ({ name: guardian.name, relationship: Number(guardian.relationship), phone: guardian.phone, email: guardian.email, address: guardian.address, isPrimary: guardian.isPrimary })),
      }) });
      if (!response.ok) throw new Error(await problem(response, "The application could not be created."));
      const created = await response.json() as { id: string }; applicantCreated = true;

      if (draft.address) {
        const sensitiveResponse = await fetch(`/api/backend/admissions/applicants/${created.id}/sensitive`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ address: draft.address }) });
        if (!sensitiveResponse.ok) throw new Error(await problem(sensitiveResponse, "The application was saved, but the residential address was not."));
      }
      if (files.photo) await upload(created.id, files.photo, "photo");
      if (files.document) await upload(created.id, files.document, "identity");
      if (files.signature) await upload(created.id, files.signature, "signature");

      notify({ title: "Application saved successfully", message: `${draft.firstName} ${draft.lastName}'s application was created as ${draft.initialStatus === "submitted" ? "submitted" : "a draft"}.` });
      setOpen(false); setStep(0); setDraft({ source: "Website", initialStatus: "draft" }); setGuardians([emptyGuardian(true)]); setFiles({}); if (onCreated) onCreated();
      window.dispatchEvent(new CustomEvent("giddyedu:resource-created", { detail: { endpoint: "admissions/applicants", title: "Applicant" } }));
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "The application could not be created.";
      if (applicantCreated) { notify({ tone: "error", title: "Application saved; follow-up needs attention", message }); setOpen(false); if (onCreated) onCreated(); window.dispatchEvent(new CustomEvent("giddyedu:resource-created", { detail: { endpoint: "admissions/applicants", title: "Applicant" } })); }
      else { setError(message); notify({ tone: "error", title: "Application was not created", message }); }
    } finally { setBusy(false); }
  }

  return <div><button type="button" onClick={() => setOpen(true)} className="tenant-primary-bg inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-black text-white shadow-sm transition hover:-translate-y-0.5">Add applicant</button>
    {open && <div className="fixed inset-0 z-[120] bg-slate-950/45 p-3 backdrop-blur-sm sm:p-6" role="dialog" aria-modal="true" aria-label="Add applicant"><div className="mx-auto flex h-full max-w-5xl flex-col overflow-hidden rounded-[1.75rem] bg-white shadow-2xl">
      <header className="flex items-start justify-between border-b border-slate-200 px-5 py-4 sm:px-7"><div><p className="text-[10px] font-black uppercase tracking-[.18em] tenant-primary-text">Admissions</p><h2 className="mt-1 text-xl font-black text-slate-950 sm:text-2xl">Add a new applicant</h2><p className="mt-1 text-xs text-slate-500">A reference number is generated automatically.</p></div><button type="button" onClick={() => setOpen(false)} className="grid size-10 place-items-center rounded-full bg-slate-100 text-xl hover:bg-red-50 hover:text-red-700" aria-label="Close">×</button></header>
      <nav className="flex gap-1 overflow-x-auto border-b border-slate-200 px-4 py-3 sm:px-7">{steps.map((name, index) => <button type="button" key={name} onClick={() => setStep(index)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-black ${step === index ? "tenant-primary-bg text-white" : "text-slate-500 hover:bg-slate-100 hover:text-slate-900"}`}>{name}</button>)}</nav>
      <form onSubmit={submit} onKeyDown={event => { if (event.key === "Enter" && event.target instanceof HTMLInputElement) event.preventDefault(); }} className="flex min-h-0 flex-1 flex-col"><div className="flex-1 overflow-y-auto p-5 sm:p-7">
        {step === 0 && <Panel title="Application details" note="How this applicant came to the school and the class they would like to start in."><Grid><Select title="How did they hear about us?" value={draft.source} onChange={value => set("source", value)} options={sources.map(value => [value, value])}/><Select title="Proposed class" value={draft.classLevelId} onChange={value => set("classLevelId", value)} optional options={(structure.classLevels ?? []).map(item => [item.id, item.name])}/></Grid><p className="mt-3 text-xs text-slate-500">The proposed class is the class the parent would like their child to start in. The specific class section is assigned by the school when the applicant is enrolled.</p></Panel>}
        {step === 1 && <Panel title="Applicant's personal and contact details" note="Use the applicant's official identity details."><Grid><Field title="First name" value={draft.firstName} onChange={value => set("firstName", value)} required/><Field title="Last name" value={draft.lastName} onChange={value => set("lastName", value)} required/><Field title="Date of birth" type="date" value={draft.dateOfBirth} onChange={value => set("dateOfBirth", value)} required/><Select title="Gender" value={draft.gender} onChange={value => set("gender", value)} optional options={[["Male", "Male"], ["Female", "Female"], ["Other", "Other"]]}/><Field title="Email address (optional)" type="email" value={draft.email} onChange={value => set("email", value)}/><Field title="Phone number (optional)" type="tel" value={draft.phone} onChange={value => set("phone", value.replace(/\D/g, "").slice(0, 11))} placeholder="09096735531"/></Grid><div className="mt-5"><Area title="Residential address (optional)" value={draft.address} onChange={value => set("address", value)}/></div></Panel>}
        {step === 2 && <Panel title="Academic background" note="Details about the applicant's previous school, if any."><Grid><Field title="Previous school name (optional)" value={draft.previousSchool} onChange={value => set("previousSchool", value)}/><Field title="Last class completed (optional)" value={draft.lastClassCompleted} onChange={value => set("lastClassCompleted", value)}/><Field title="Date left previous school (optional)" type="date" value={draft.leavingDate} onChange={value => set("leavingDate", value)}/></Grid><div className="mt-5 grid gap-5 md:grid-cols-2"><Area title="Previous school address (optional)" value={draft.previousSchoolAddress} onChange={value => set("previousSchoolAddress", value)}/><Area title="Reason for leaving (optional)" value={draft.reasonForLeaving} onChange={value => set("reasonForLeaving", value)}/></div></Panel>}
        {step === 3 && <Panel title="Parents or guardians" note="Contact details for at least one parent or guardian are required."><div className="space-y-5">{guardians.map((guardian, index) => <GuardianCard key={index} index={index} guardian={guardian} canRemove={guardians.length > 1} onChange={patch => updateGuardian(index, patch)} onRemove={() => removeGuardian(index)}/>)}<button type="button" onClick={addGuardian} className="w-full rounded-xl border-2 border-dashed border-slate-300 py-3 text-sm font-black text-slate-600 hover:border-[var(--tenant-primary,#28654a)] hover:text-[var(--tenant-primary,#28654a)]">+ Add another guardian</button></div></Panel>}
        {step === 4 && <Panel title="Photograph, documents and application status" note="JPG, JPEG, PNG and PDF files use secure document storage."><div className="grid gap-5 md:grid-cols-3"><File title="Applicant photograph" accept=".jpg,.jpeg,.png,image/jpeg,image/png" file={files.photo} onChange={file => setFiles(current => ({ ...current, photo: file }))}/><File title="Supporting document (report card, birth certificate, etc.)" accept="application/pdf,.jpg,.jpeg,.png,image/jpeg,image/png" file={files.document} onChange={file => setFiles(current => ({ ...current, document: file }))}/><File title="Parent / guardian signature" accept=".jpg,.jpeg,.png,image/jpeg,image/png" file={files.signature} onChange={file => setFiles(current => ({ ...current, signature: file }))}/></div>
          <div className="mt-6 border-t border-slate-100 pt-6"><p className="text-sm font-black text-slate-900">Application status</p><p className="mt-1 text-xs text-slate-500">Choose Submitted if this application is already complete and ready for review; otherwise it is saved as a draft.</p><div className="mt-4 max-w-sm"><Select title="Application status" value={draft.initialStatus} onChange={value => set("initialStatus", value)} options={[["draft", "Draft"], ["submitted", "Submitted"]]}/></div></div>
        </Panel>}
        {error && <div role="alert" className="mt-5 flex items-start justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-800"><span>{error}</span><button type="button" onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
      </div><footer className="flex items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:px-7"><button type="button" disabled={step === 0 || busy} onClick={() => setStep(value => value - 1)} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-black disabled:opacity-40">Previous</button><div className="flex gap-2"><button type="button" onClick={() => setOpen(false)} className="rounded-xl bg-red-700 px-4 py-2.5 text-sm font-black text-white hover:bg-red-800">Cancel</button>{step < steps.length - 1 ? <button type="button" onClick={next} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white">Continue</button> : <button type="submit" disabled={busy} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white disabled:opacity-50">{busy ? "Creating application…" : "Create application"}</button>}</div></footer></form>
    </div></div>}
  </div>;
}

function GuardianCard({ index, guardian, canRemove, onChange, onRemove }: { index: number; guardian: GuardianDraft; canRemove: boolean; onChange: (patch: Partial<GuardianDraft>) => void; onRemove: () => void }) {
  return <div className="rounded-2xl border border-slate-200 p-4 sm:p-5"><div className="mb-4 flex items-center justify-between"><p className="text-xs font-black uppercase tracking-wide text-slate-500">Guardian {index + 1}</p>{canRemove && <button type="button" onClick={onRemove} className="text-xs font-black text-red-700 hover:underline">Remove</button>}</div>
    <Grid><Field title="Full name" value={guardian.name} onChange={value => onChange({ name: value })} required/><Select title="Relationship to applicant" value={guardian.relationship} onChange={value => onChange({ relationship: value })} options={relationships.map((name, i) => [String(i), displayRelationship(name)])}/><Field title="Phone" type="tel" value={guardian.phone} onChange={value => onChange({ phone: value.replace(/\D/g, "").slice(0, 11) })} placeholder="09096735531" required/><Field title="Email" type="email" value={guardian.email} onChange={value => onChange({ email: value })} required/><Area title="Address" value={guardian.address} onChange={value => onChange({ address: value })} required/><Select title="Primary guardian" value={String(guardian.isPrimary)} onChange={value => onChange({ isPrimary: value === "true" })} options={[["true", "Yes"], ["false", "No"]]}/></Grid>
  </div>;
}

function Panel({ title, note, children }: { title: string; note: string; children: React.ReactNode }) { return <section><h3 className="text-lg font-black text-slate-950">{title}</h3><p className="mb-6 mt-1 text-sm text-slate-500">{note}</p>{children}</section>; }
function Grid({ children }: { children: React.ReactNode }) { return <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">{children}</div>; }
function Field({ title, value = "", onChange, type = "text", placeholder, readOnly, required = false }: { title: string; value?: string; onChange: (value: string) => void; type?: string; placeholder?: string; readOnly?: boolean; required?: boolean }) { return <label className={label}>{title}{required && <span className="ml-1 text-red-600">*</span>}<input type={type} value={value} onChange={event => onChange(event.target.value)} placeholder={placeholder} readOnly={readOnly} required={required} inputMode={type === "tel" ? "numeric" : undefined} className={`${input} ${readOnly ? "bg-slate-100 text-slate-500" : ""}`}/></label>; }
function Area({ title, value = "", onChange, required = false }: { title: string; value?: string; onChange: (value: string) => void; required?: boolean }) { return <label className={label}>{title}{required && <span className="ml-1 text-red-600">*</span>}<textarea rows={3} value={value} onChange={event => onChange(event.target.value)} required={required} className={input}/></label>; }
function Select({ title, value = "", onChange, options, optional = false }: { title: string; value?: string; onChange: (value: string) => void; options: string[][]; optional?: boolean }) { return <label className={label}>{title}<select value={value} onChange={event => onChange(event.target.value)} className={input}>{optional && <option value="">Not selected</option>}{options.map(([id, text]) => <option key={id} value={id}>{text}</option>)}</select></label>; }
/* eslint-disable @next/next/no-img-element -- FileReader previews are local files, not network images. */
function File({ title, accept, file, onChange }: { title: string; accept: string; file?: File; onChange: (file?: File) => void }) {
  const [preview, setPreview] = useState<{ file: File; url: string } | null>(null);
  const isImage = Boolean(file && (file.type.startsWith("image/") || /\.(jpe?g|png)$/i.test(file.name)));
  const previewUrl = preview && preview.file === file ? preview.url : undefined;
  useEffect(() => {
    if (!file || !(file.type.startsWith("image/") || /\.(jpe?g|png)$/i.test(file.name))) return;
    const reader = new FileReader();
    reader.onload = () => { if (typeof reader.result === "string") setPreview({ file, url: reader.result }); };
    reader.readAsDataURL(file);
    return () => reader.abort();
  }, [file]);
  return <label className="group block cursor-pointer rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-5 text-sm font-black text-slate-800 transition hover:border-[var(--tenant-primary,#28654a)] hover:bg-white focus-within:border-[var(--tenant-primary,#28654a)] focus-within:ring-2 focus-within:ring-[var(--tenant-primary,#28654a)]"><span className="block">{title}</span><span className="mt-1 block text-xs font-normal text-slate-500">Maximum 10 MB</span><input type="file" accept={accept} onChange={event => onChange(event.target.files?.[0])} className="sr-only"/><span className="mt-4 inline-flex rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-bold group-hover:border-[var(--tenant-primary,#28654a)]">Select file</span>{file && <span className="mt-2 block truncate text-xs font-normal text-slate-600">Selected: {file.name}</span>}{isImage && previewUrl && <span className="mt-3 block overflow-hidden rounded-xl border border-slate-200 bg-white p-2"><img src={previewUrl} alt={`${title} preview`} className="h-32 w-full object-contain"/></span>}{file && !isImage && <span className="mt-3 block rounded-lg bg-white px-3 py-2 text-xs font-normal text-slate-600">Document selected: {file.name}</span>}</label>;
}
/* eslint-enable @next/next/no-img-element */
function displayRelationship(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2"); }
async function problem(response: Response, fallback: string) { try { const body = await response.json() as { detail?: string; title?: string }; return body.detail ?? body.title ?? fallback; } catch { return fallback; } }
