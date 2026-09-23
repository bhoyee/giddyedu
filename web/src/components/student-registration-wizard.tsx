"use client";

import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { notify } from "@/components/app-toast";
import { StaffFormSelect } from "@/components/staff-form-select";
import { nigeriaLocations } from "@/lib/nigeria-locations";

type Option = { id: string; name: string; status?: number; statusLabel?: string };
type Section = Option & { academicYearId: string; campusId: string; isActive: boolean };
type Structure = { academicYears?: Option[]; classSections?: Section[] };
type GuardianMatch = { id: string; firstName: string; lastName: string; phone: string; email?: string | null; hasAccount?: boolean };
type GuardianDraft = { firstName: string; lastName: string; phone: string; email: string; gender: string; address: string; relationship: string; isPrimary: boolean; isEmergency: boolean; mayCollect: boolean; existingGuardianId: string | null; photo?: File };
const steps = ["Academic", "Personal", "Contact", "Medical", "Guardian", "Documents"];
const input = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm outline-none transition focus:border-[var(--tenant-primary,#28654a)] focus:ring-4 focus:ring-emerald-950/5";
const label = "block text-xs font-bold text-slate-700";
const relationships = ["Mother", "Father", "Parent", "LegalGuardian", "Relative", "Sponsor", "Other", "Stepmother", "Stepfather", "Grandmother", "Grandfather", "Aunt", "Uncle", "Sibling", "FosterParent", "AdoptiveParent", "Caregiver"];
function emptyGuardian(isPrimary: boolean): GuardianDraft { return { firstName: "", lastName: "", phone: "", email: "", gender: "", address: "", relationship: "0", isPrimary, isEmergency: true, mayCollect: true, existingGuardianId: null }; }

export function StudentRegistrationWizard({ onCreated }: { onCreated?: () => void }) {
  const finalSubmissionRequested = useRef(false);
  const [open, setOpen] = useState(false); const [step, setStep] = useState(0); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const [draft, setDraft] = useState<Record<string, string>>({ studentType: "0", country: "Nigeria", enrolledOn: new Date().toISOString().slice(0, 10) });
  const [useCurrentYear, setUseCurrentYear] = useState(true); const [autoStudentId, setAutoStudentId] = useState(true);
  const [structure, setStructure] = useState<Structure>({}); const [guardians, setGuardians] = useState<GuardianDraft[]>([emptyGuardian(true)]); const [files, setFiles] = useState<Record<string, File | undefined>>({});
  const years = structure.academicYears ?? []; const yearId = draft.academicYearId ?? "";
  const sections = (structure.classSections ?? []).filter(section => section.isActive !== false && (!yearId || section.academicYearId === yearId));
  const states = Object.keys(nigeriaLocations); const lgas = useMemo(() => draft.state ? nigeriaLocations[draft.state as keyof typeof nigeriaLocations] ?? [] : [], [draft.state]);
  useEffect(() => { if (!open) return; void fetch("/api/backend/academics/structure", { cache: "no-store" }).then(async response => { if (!response.ok) throw new Error("Academic structure could not be loaded."); const loaded = await response.json() as Structure; setStructure(loaded); const activeYear = loaded.academicYears?.find(year => year.status === 1 || year.statusLabel === "Active"); if (activeYear) setDraft(current => ({ ...current, academicYearId: activeYear.id, classSectionId: "" })); else { setUseCurrentYear(false); setError("No active academic year is configured for this workspace. Select an available year or configure the active year first."); } }).catch(reason => setError(reason instanceof Error ? reason.message : "Academic structure could not be loaded.")); }, [open]);
  useEffect(() => { if (!error) return; const timer = window.setTimeout(() => setError(""), 7000); return () => window.clearTimeout(timer); }, [error]);
  function set(name: string, value: string) { setDraft(current => ({ ...current, [name]: value, ...(name === "academicYearId" ? { classSectionId: "" } : {}), ...(name === "state" ? { localGovernment: "" } : {}) })); }
  function updateGuardian(index: number, patch: Partial<GuardianDraft>) { setGuardians(current => current.map((guardian, i) => i === index ? { ...guardian, ...patch } : guardian)); }
  function addGuardian() { setGuardians(current => [...current, emptyGuardian(current.length === 0)]); }
  function removeGuardian(index: number) { setGuardians(current => current.filter((_, i) => i !== index)); }
  function validateCurrent() {
    if (step === 0 && (!draft.academicYearId || !draft.classSectionId || !draft.enrolledOn || !draft.studentType)) return "Select the academic year, class, enrolment date and student type.";
    if (step === 0 && !autoStudentId && !draft.customStudentId?.trim()) return "Enter the existing student ID or select automatic generation.";
    if (step === 1 && (!draft.firstName?.trim() || !draft.lastName?.trim() || !draft.dateOfBirth || !draft.gender)) return "Complete the student's first name, last name, date of birth and gender.";
    if (step === 2 && draft.phone && !/^\d{11}$/.test(draft.phone)) return "Student phone must contain exactly 11 digits.";
    if (step === 4 && guardians.some(guardian => !guardian.firstName.trim() || !guardian.lastName.trim() || !/^\d{11}$/.test(guardian.phone) || !guardian.email)) return "Complete each guardian's name, email and 11-digit phone number, or remove the incomplete entry.";
    if (step === 4 && guardians.filter(guardian => guardian.isPrimary).length > 1) return "Only one guardian can be marked as the primary guardian.";
    return "";
  }
  function next() { const issue = validateCurrent(); if (issue) { setError(issue); return; } setError(""); setStep(value => Math.min(steps.length - 1, value + 1)); }

  async function upload(entityType: "Student" | "Guardian", entityId: string, file: File, category: string) {
    const begin = await fetch(`/api/backend/documents/${entityType}/${entityId}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size, category }) });
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
    if (!finalSubmissionRequested.current) return;
    finalSubmissionRequested.current = false;
    const issue = validateCurrent(); if (issue) { setError(issue); return; }
    setBusy(true); setError(""); let studentCreated = false;
    try {
      const response = await fetch("/api/backend/students", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({
        firstName: draft.firstName, middleName: draft.middleName || null, lastName: draft.lastName, dateOfBirth: draft.dateOfBirth,
        gender: draft.gender, studentType: Number(draft.studentType), email: draft.email || null, phone: draft.phone || null,
        autoGenerateStudentId: autoStudentId, customStudentId: autoStudentId ? null : draft.customStudentId,
        academicYearId: draft.academicYearId, classSectionId: draft.classSectionId, enrolledOn: draft.enrolledOn,
        address: [draft.address, draft.city, draft.localGovernment, draft.state, "Nigeria"].filter(Boolean).join(", ") || null,
        medicalInformation: [["Genotype", draft.genotype], ["Blood group", draft.bloodGroup], ["Weight", draft.weight ? `${draft.weight} kg` : ""], ["Height", draft.height ? `${draft.height} cm` : ""], ["Other medical information / medication", draft.medicalInformation]].filter(([, value]) => value).map(([name, value]) => `${name}: ${value}`).join("\n") || null, allergies: draft.allergies || null,
        specialEducationalNeeds: draft.specialEducationalNeeds || null, privateNotes: draft.privateNotes || null,
        guardians: guardians.map(guardian => ({ firstName: guardian.firstName, lastName: guardian.lastName, phone: guardian.phone,
          email: guardian.email, gender: guardian.gender || null, address: guardian.address || null,
          relationship: Number(guardian.relationship), isPrimary: guardian.isPrimary,
          isEmergencyContact: guardian.isEmergency, mayCollect: guardian.mayCollect, existingGuardianId: guardian.existingGuardianId })),
      }) });
      if (!response.ok) throw new Error(await problem(response, "The student record could not be created."));
      const created = await response.json() as { studentId: string; admissionNumber: string; guardians: { guardianId: string; needsInvitation: boolean }[] }; studentCreated = true;
      if (files.studentPhoto) await upload("Student", created.studentId, files.studentPhoto, "photo");
      if (files.birthCertificate) await upload("Student", created.studentId, files.birthCertificate, "identity");
      for (const [index, guardian] of guardians.entries()) {
        const createdGuardian = created.guardians[index];
        if (!createdGuardian) continue;
        if (guardian.photo) await upload("Guardian", createdGuardian.guardianId, guardian.photo, "photo");
        if (createdGuardian.needsInvitation) {
          const invitation = await fetch("/api/backend/account-invitations", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ targetType: 1, targetId: createdGuardian.guardianId }) });
          if (!invitation.ok) throw new Error(await problem(invitation, "A guardian invitation could not be queued."));
        }
      }
      const invitedCount = created.guardians.filter(guardian => guardian.needsInvitation).length;
      const linkedCount = created.guardians.length - invitedCount;
      const guardianSummary = [invitedCount ? `${invitedCount} new guardian invitation${invitedCount === 1 ? "" : "s"} queued` : "", linkedCount ? `${linkedCount} linked to an existing guardian account` : ""].filter(Boolean).join(" and ");
      notify({ title: "Student record saved successfully", message: `${created.admissionNumber} was enrolled${guardianSummary ? ` and ${guardianSummary}` : ""}.` });
      setOpen(false); setStep(0); setUseCurrentYear(true); setAutoStudentId(true); setGuardians([emptyGuardian(true)]); setDraft({ studentType: "0", country: "Nigeria", enrolledOn: new Date().toISOString().slice(0, 10) }); setFiles({}); if (onCreated) onCreated(); window.dispatchEvent(new CustomEvent("giddyedu:resource-created", { detail: { endpoint: "students", title: "Student" } }));
    } catch (reason) { const message = reason instanceof Error ? reason.message : "The student could not be created."; if (studentCreated) { notify({ tone: "error", title: "Student saved; follow-up needs attention", message }); setOpen(false); if (onCreated) onCreated(); window.dispatchEvent(new CustomEvent("giddyedu:resource-created", { detail: { endpoint: "students", title: "Student" } })); } else { setError(message); notify({ tone: "error", title: "Student was not created", message }); } }
    finally { setBusy(false); }
  }
  return <div><button type="button" onClick={() => setOpen(true)} className="tenant-primary-bg inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-black text-white shadow-sm transition hover:-translate-y-0.5">Add student</button>
    {open && <div className="fixed inset-0 z-[120] bg-slate-950/45 p-3 backdrop-blur-sm sm:p-6" role="dialog" aria-modal="true" aria-label="Add student"><div className="mx-auto flex h-full max-w-6xl flex-col overflow-hidden rounded-[1.75rem] bg-white shadow-2xl">
      <header className="flex items-start justify-between border-b border-slate-200 px-5 py-4 sm:px-7"><div><p className="text-[10px] font-black uppercase tracking-[.18em] tenant-primary-text">Student registration</p><h2 className="mt-1 text-xl font-black text-slate-950 sm:text-2xl">Create a complete student record</h2><p className="mt-1 text-xs text-slate-500">Generate a student ID automatically or retain the school&apos;s existing identifier.</p></div><button type="button" onClick={() => setOpen(false)} className="grid size-10 place-items-center rounded-full bg-slate-100 text-xl hover:bg-red-50 hover:text-red-700" aria-label="Close">×</button></header>
      <nav className="flex gap-1 overflow-x-auto border-b border-slate-200 px-4 py-3 sm:px-7">{steps.map((name, index) => <button type="button" key={name} onClick={() => setStep(index)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-black ${step === index ? "tenant-primary-bg text-white" : "text-slate-500 hover:bg-slate-100 hover:text-slate-900"}`}>{name}</button>)}</nav>
      <form onSubmit={submit} onKeyDown={event => { if (event.key === "Enter" && event.target instanceof HTMLInputElement) event.preventDefault(); }} className="flex min-h-0 flex-1 flex-col"><div className="flex-1 overflow-y-auto p-5 sm:p-7">
        {step === 0 && <Panel title="Academic placement" note="Placement is restricted to the active campus workspace."><div className="mb-5 grid gap-3 lg:grid-cols-2"><Choice checked={useCurrentYear} onChange={checked => { setUseCurrentYear(checked); const active = years.find(year => year.status === 1 || year.statusLabel === "Active"); set("academicYearId", checked ? active?.id ?? "" : ""); }} title="Use current academic year" note={years.find(year => year.status === 1 || year.statusLabel === "Active")?.name ?? "No active academic year configured"}/><Choice checked={autoStudentId} onChange={checked => { setAutoStudentId(checked); if (checked) set("customStudentId", ""); }} title="Generate student ID automatically" note="Recommended for consistent, unique student records"/></div><Grid>{useCurrentYear ? <Field title="Academic year" value={years.find(year => year.id === draft.academicYearId)?.name ?? "No active academic year"} onChange={() => {}} readOnly required/> : <Select title="Academic year" value={draft.academicYearId} onChange={value => set("academicYearId", value)} options={years.map(item => [item.id, `${item.name}${item.statusLabel ? ` · ${item.statusLabel}` : ""}`])}/>} {!autoStudentId && <Field title="Student ID" value={draft.customStudentId} onChange={value => set("customStudentId", value.toUpperCase().replace(/[^A-Z0-9/_-]/g, "").slice(0, 50))} placeholder="e.g. BHS/2026/0042" required/>}<Select title="Class / arm / stream" value={draft.classSectionId} onChange={value => set("classSectionId", value)} options={sections.map(item => [item.id, item.name])}/><Field title="Enrolment date" type="date" value={draft.enrolledOn} onChange={value => set("enrolledOn", value)} required/><Select title="Student type" value={draft.studentType} onChange={value => set("studentType", value)} options={[["0", "Day student"], ["1", "Boarding student"], ["2", "Day and boarding"]]}/></Grid></Panel>}
        {step === 1 && <Panel title="Personal details" note="Use the student's official identity details."><Grid><Field title="First name" value={draft.firstName} onChange={value => set("firstName", value)} required/><Field title="Middle name (optional)" value={draft.middleName} onChange={value => set("middleName", value)}/><Field title="Last name" value={draft.lastName} onChange={value => set("lastName", value)} required/><Field title="Date of birth" type="date" value={draft.dateOfBirth} onChange={value => set("dateOfBirth", value)} required/><Select title="Gender" value={draft.gender} onChange={value => set("gender", value)} options={[["Male", "Male"], ["Female", "Female"], ["Other", "Other"]]}/></Grid></Panel>}
        {step === 2 && <Panel title="Contact and residential details" note="Student email and phone are optional; guardian contact is collected separately."><Grid><Field title="Email address (optional)" type="email" value={draft.email} onChange={value => set("email", value)}/><Field title="Phone number (optional)" type="tel" value={draft.phone} onChange={value => set("phone", value.replace(/\D/g, "").slice(0, 11))} placeholder="09096735531"/><Field title="Country" value="Nigeria" onChange={() => {}} readOnly/><Select title="State" value={draft.state} onChange={value => set("state", value)} optional options={states.map(value => [value, value])}/><Select title="Local government" value={draft.localGovernment} onChange={value => set("localGovernment", value)} optional options={lgas.map(value => [value, value])}/><Field title="City / town" value={draft.city} onChange={value => set("city", value)}/><Area title="Residential address" value={draft.address} onChange={value => set("address", value)}/></Grid></Panel>}
        {step === 3 && <Panel title="Medical and learning support" note="This sensitive information is visible only to authorised users. Record medication, allergies, accessibility support and special education needs accurately."><Grid><Select title="Genotype" value={draft.genotype} onChange={value => set("genotype", value)} optional options={["AA", "AS", "AC", "SS", "SC", "CC"].map(value => [value, value])}/><Select title="Blood group" value={draft.bloodGroup} onChange={value => set("bloodGroup", value)} optional options={["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"].map(value => [value, value])}/><Field title="Weight (kg)" type="number" value={draft.weight} onChange={value => set("weight", value)} placeholder="e.g. 32.5"/><Field title="Height (cm)" type="number" value={draft.height} onChange={value => set("height", value)} placeholder="e.g. 138"/><Select title="Disability or accessibility need" value={draft.hasAccessibilityNeed ?? "false"} onChange={value => set("hasAccessibilityNeed", value)} options={yesNo}/>{draft.hasAccessibilityNeed === "true" && <Area title="Accessibility support required" value={draft.specialEducationalNeeds} onChange={value => set("specialEducationalNeeds", value)}/>}<Area title="Other medical information or medication" value={draft.medicalInformation} onChange={value => set("medicalInformation", value)}/><Area title="Allergies" value={draft.allergies} onChange={value => set("allergies", value)}/><Area title="Private notes" value={draft.privateNotes} onChange={value => set("privateNotes", value)} helper="Use this for relevant confidential context not captured above, including additional medication information or special education needs."/></Grid></Panel>}
        {step === 4 && <Panel title="Parents or guardians" note="Enter the phone or email first. GiddyEdu will show matching guardians so an existing parent can be selected instead of duplicated."><div className="space-y-5">{guardians.map((guardian, index) => <GuardianCard key={index} index={index} guardian={guardian} canRemove={guardians.length > 1} onChange={patch => updateGuardian(index, patch)} onRemove={() => removeGuardian(index)}/>)}<button type="button" onClick={addGuardian} className="w-full rounded-xl border-2 border-dashed border-slate-300 py-3 text-sm font-black text-slate-600 hover:border-[var(--tenant-primary,#28654a)] hover:text-[var(--tenant-primary,#28654a)]">+ Add another guardian</button>{guardians.length === 0 && <p className="text-sm text-slate-500">No guardian will be linked now. You can link one later from the student&apos;s profile.</p>}</div></Panel>}
        {step === 5 && <Panel title="Photographs and documents" note="JPG, JPEG, PNG and PDF files use secure document storage."><div className="grid gap-5 md:grid-cols-3"><File title="Student photograph" accept=".jpg,.jpeg,.png,image/jpeg,image/png" file={files.studentPhoto} onChange={file => setFiles(current => ({ ...current, studentPhoto: file }))}/><File title="Birth certificate / identity document" accept="application/pdf,.jpg,.jpeg,.png,image/jpeg,image/png" file={files.birthCertificate} onChange={file => setFiles(current => ({ ...current, birthCertificate: file }))}/>{guardians.map((guardian, index) => <File key={index} title={`${guardian.firstName || `Guardian ${index + 1}`} photograph`} accept=".jpg,.jpeg,.png,image/jpeg,image/png" file={guardian.photo} onChange={file => updateGuardian(index, { photo: file })}/>)}</div></Panel>}
        {error && <div role="alert" className="mt-5 flex items-start justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-800"><span>{error}</span><button type="button" onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
      </div><footer className="flex items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:px-7"><button type="button" disabled={step === 0 || busy} onClick={() => setStep(value => value - 1)} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-black disabled:opacity-40">Previous</button><div className="flex gap-2"><button type="button" onClick={() => setOpen(false)} className="rounded-xl bg-red-700 px-4 py-2.5 text-sm font-black text-white hover:bg-red-800">Cancel</button>{step < steps.length - 1 ? <button type="button" onClick={next} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white">Continue</button> : <button type="submit" disabled={busy} onClick={() => { finalSubmissionRequested.current = true; }} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white disabled:opacity-50">{busy ? "Creating student…" : "Create student record"}</button>}</div></footer></form>
    </div></div>}
  </div>;
}

const yesNo = [["true", "Yes"], ["false", "No"]];
function GuardianCard({ index, guardian, canRemove, onChange, onRemove }: { index: number; guardian: GuardianDraft; canRemove: boolean; onChange: (patch: Partial<GuardianDraft>) => void; onRemove: () => void }) {
  const [matches, setMatches] = useState<GuardianMatch[]>([]);
  const [searching, setSearching] = useState(false);
  useEffect(() => {
    if (guardian.existingGuardianId) {
      const clear = window.setTimeout(() => setMatches([]), 0);
      return () => window.clearTimeout(clear);
    }
    const query = (guardian.email.trim() || guardian.phone.trim()).trim();
    if (query.length < 3) {
      const clear = window.setTimeout(() => setMatches([]), 0);
      return () => window.clearTimeout(clear);
    }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setSearching(true);
      void fetch(`/api/backend/guardians?page=1&pageSize=8&sort=lastName&descending=false&search=${encodeURIComponent(query)}`, { cache: "no-store", signal: controller.signal })
        .then(async response => { if (!response.ok) throw new Error(); const body = await response.json() as { items?: GuardianMatch[] }; setMatches(body.items ?? []); })
        .catch(reason => { if (reason instanceof DOMException && reason.name === "AbortError") return; setMatches([]); })
        .finally(() => setSearching(false));
    }, 350);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [guardian.email, guardian.phone, guardian.existingGuardianId]);
  const selected = guardian.existingGuardianId ? { id: guardian.existingGuardianId, firstName: guardian.firstName, lastName: guardian.lastName, phone: guardian.phone, email: guardian.email } : null;
  return <div className="rounded-2xl border border-slate-200 p-4 sm:p-5"><div className="mb-4 flex items-center justify-between"><p className="text-xs font-black uppercase tracking-wide text-slate-500">Guardian {index + 1}</p>{canRemove && <button type="button" onClick={onRemove} className="text-xs font-black text-red-700 hover:underline">Remove</button>}</div>
    <Grid><Field title="Phone" type="tel" value={guardian.phone} onChange={value => onChange({ phone: value.replace(/\D/g, "").slice(0, 11), existingGuardianId: null })} placeholder="09096735531" required/><Field title="Email" type="email" value={guardian.email} onChange={value => onChange({ email: value, existingGuardianId: null })} required/></Grid>
    {searching && <p className="mt-3 text-xs font-semibold text-slate-500">Checking existing guardians…</p>}
    {!selected && matches.length > 0 && <div className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3"><p className="text-xs font-black uppercase tracking-wide text-amber-900">Existing guardian found</p><div className="mt-2 space-y-2">{matches.map(match => <button type="button" key={match.id} onClick={() => { onChange({ firstName: match.firstName, lastName: match.lastName, phone: match.phone, email: match.email ?? "", existingGuardianId: match.id }); setMatches([]); }} className="flex w-full items-center justify-between rounded-lg border border-amber-200 bg-white px-3 py-2 text-left hover:border-amber-500"><span><span className="block text-sm font-black text-slate-900">{match.firstName} {match.lastName}</span><span className="block text-xs text-slate-500">{match.phone} · {match.email ?? "No email recorded"}</span></span><span className="text-xs font-black text-amber-800">Select</span></button>)}</div></div>}
    {selected && <div className="mt-3 flex items-center justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-3"><div><p className="text-xs font-black uppercase tracking-wide text-emerald-800">Existing guardian selected</p><p className="mt-1 text-sm font-bold text-slate-900">{selected.firstName} {selected.lastName}</p><p className="text-xs text-slate-600">{selected.phone} · {selected.email}</p></div><button type="button" onClick={() => onChange({ existingGuardianId: null })} className="rounded-lg bg-red-700 px-3 py-2 text-xs font-black text-white">Change</button></div>}
    <div className="mt-5"><Grid><Field title="First name" value={guardian.firstName} onChange={value => onChange({ firstName: value })} readOnly={Boolean(guardian.existingGuardianId)} required/><Field title="Last name" value={guardian.lastName} onChange={value => onChange({ lastName: value })} readOnly={Boolean(guardian.existingGuardianId)} required/><Select title="Relationship to this student" value={guardian.relationship} onChange={value => onChange({ relationship: value })} options={relationships.map((name, i) => [String(i), displayRelationship(name)])}/><Select title="Gender" value={guardian.gender} onChange={value => onChange({ gender: value })} optional options={[["Male", "Male"], ["Female", "Female"], ["Other", "Other"]]}/><Area title="Guardian address" value={guardian.address} onChange={value => onChange({ address: value })}/><Select title="Primary guardian" value={String(guardian.isPrimary)} onChange={value => onChange({ isPrimary: value === "true" })} options={yesNo}/><Select title="Emergency contact" value={String(guardian.isEmergency)} onChange={value => onChange({ isEmergency: value === "true" })} options={yesNo}/><Select title="May collect student" value={String(guardian.mayCollect)} onChange={value => onChange({ mayCollect: value === "true" })} options={yesNo}/></Grid></div>
  </div>;
}
function Panel({ title, note, children }: { title: string; note: string; children: React.ReactNode }) { return <section><h3 className="text-lg font-black text-slate-950">{title}</h3><p className="mb-6 mt-1 text-sm text-slate-500">{note}</p>{children}</section>; }
function Grid({ children }: { children: React.ReactNode }) { return <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">{children}</div>; }
function Field({ title, value = "", onChange, type = "text", placeholder, readOnly, required = false }: { title: string; value?: string; onChange: (value: string) => void; type?: string; placeholder?: string; readOnly?: boolean; required?: boolean }) { return <label className={label}>{title}{required && <span className="ml-1 text-red-600">*</span>}<input type={type} value={value} onChange={event => onChange(event.target.value)} placeholder={placeholder} readOnly={readOnly} required={required} inputMode={type === "tel" ? "numeric" : type === "number" ? "decimal" : undefined} min={type === "number" ? "0" : undefined} step={type === "number" ? "0.1" : undefined} className={`${input} ${readOnly ? "bg-slate-100 text-slate-500" : ""}`}/></label>; }
function Area({ title, value = "", onChange, helper }: { title: string; value?: string; onChange: (value: string) => void; helper?: string }) { return <label className={label}>{title}<textarea rows={3} value={value} onChange={event => onChange(event.target.value)} className={input}/>{helper && <span className="mt-1.5 block text-xs font-normal leading-5 text-slate-500">{helper}</span>}</label>; }
function Select({ title, value = "", onChange, options, optional = false }: { title: string; value?: string; onChange: (value: string) => void; options: string[][]; optional?: boolean }) { return <StaffFormSelect name={title.replaceAll(" ", "-")} title={title} value={value} onChange={onChange} options={options} optional={optional}/>; }
function Choice({ checked, onChange, title, note }: { checked: boolean; onChange: (checked: boolean) => void; title: string; note: string }) { return <label className={`flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition ${checked ? "border-[var(--tenant-primary,#28654a)] bg-emerald-50/40" : "border-slate-200 bg-white"}`}><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} className="mt-0.5 size-4"/><span><span className="block text-sm font-black text-slate-900">{title}</span><span className="mt-0.5 block text-xs text-slate-500">{note}</span></span></label>; }
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
