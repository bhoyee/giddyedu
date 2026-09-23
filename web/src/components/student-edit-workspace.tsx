"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";

type Student = { id: string; admissionNumber: string; firstName: string; middleName?: string | null; lastName: string; dateOfBirth: string; gender?: string | null; studentType: number; email?: string | null; phone?: string | null; status: number; createdAtUtc: string };
type Guardian = { id: string; firstName: string; lastName: string; phone: string; email?: string | null };
type Detail = { student: Student; guardians: Guardian[] };
type Sensitive = { address?: string | null; medicalInformation?: string | null; allergies?: string | null; specialEducationalNeeds?: string | null; genotype?: string | null; bloodGroup?: string | null; weightKg?: number | null; heightCm?: number | null; disability?: string | null; privateNotes?: string | null };
type GuardianMatch = { id: string; firstName: string; lastName: string; phone: string; email?: string | null };
const studentTypes = ["Day student", "Boarding student", "Day and boarding"];
const statuses = [[0, "Active"], [1, "Suspended"], [2, "Withdrawn"], [3, "Graduated"], [4, "Alumni"]] as const;
const genotypes = ["AA", "AS", "AC", "SS", "SC", "CC"];
const bloodGroups = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];
const disabilityOptions = ["None", "Visual impairment", "Hearing impairment", "Physical / mobility impairment", "Speech and language impairment", "Learning disability", "Autism spectrum", "Multiple disabilities", "Other"];
const relationships = ["Mother", "Father", "Parent", "LegalGuardian", "Relative", "Sponsor", "Other", "Stepmother", "Stepfather", "Grandmother", "Grandfather", "Aunt", "Uncle", "Sibling", "FosterParent", "AdoptiveParent", "Caregiver"];
const yesNo = [["true", "Yes"], ["false", "No"]];
const fieldClass = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none focus:border-[var(--tenant-primary,#28654a)] focus:ring-2 focus:ring-[var(--tenant-primary-soft,#e7f2eb)]";

export function StudentEditWorkspace({ studentId }: { studentId: string }) {
  const router = useRouter();
  const [student, setStudent] = useState<Student | null>(null);
  const [guardians, setGuardians] = useState<Guardian[]>([]);
  const [sensitive, setSensitive] = useState<Sensitive | null>(null);
  const [canManage, setCanManage] = useState(false);
  const [canManageGuardians, setCanManageGuardians] = useState(false);
  const [canViewSensitive, setCanViewSensitive] = useState(false);
  const [selectedStatus, setSelectedStatus] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");

  const reloadGuardians = useCallback(async () => {
    const response = await fetch(`/api/backend/students/${studentId}`, { cache: "no-store" });
    if (response.ok) setGuardians((await response.json() as Detail).guardians);
  }, [studentId]);

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [sessionResponse, detailResponse, sensitiveResponse] = await Promise.all([
          fetch("/api/auth/session", { cache: "no-store" }),
          fetch(`/api/backend/students/${studentId}`, { cache: "no-store" }),
          fetch(`/api/backend/students/${studentId}/sensitive`, { cache: "no-store" }),
        ]);
        if (!detailResponse.ok) throw new Error(detailResponse.status === 403 ? "You cannot access this student record." : "The student record could not be loaded.");
        const session = sessionResponse.ok ? await sessionResponse.json() as { access?: { permissions?: string[] } } : null;
        const detail = await detailResponse.json() as Detail;
        const privateRecord = sensitiveResponse.ok ? await sensitiveResponse.json() as Sensitive | null : null;
        if (!active) return;
        const permissions = session?.access?.permissions ?? [];
        setCanManage(permissions.includes("Students.Manage"));
        setCanManageGuardians(permissions.includes("Guardians.Manage"));
        setCanViewSensitive(sensitiveResponse.ok);
        setStudent(detail.student); setGuardians(detail.guardians); setSensitive(privateRecord); setSelectedStatus(detail.student.status);
      } catch (reason) { if (active) setError(reason instanceof Error ? reason.message : "The student record could not be loaded."); }
      finally { if (active) setLoading(false); }
    }
    void load();
    return () => { active = false; };
  }, [studentId]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!student || !canManage) return;
    const data = new FormData(event.currentTarget);
    setSaving(true); setError("");
    try {
      const core = await fetch(`/api/backend/students/${studentId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({
        firstName: data.get("firstName"), middleName: data.get("middleName") || null, lastName: data.get("lastName"),
        dateOfBirth: data.get("dateOfBirth"), gender: data.get("gender") || null, studentType: Number(data.get("studentType")),
        email: data.get("email") || null, phone: data.get("phone") || null,
      }) });
      if (!core.ok) throw new Error(await problem(core, "Student details could not be saved."));
      if (selectedStatus !== student.status) {
        const statusResponse = await fetch("/api/backend/students/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ studentIds: [studentId], status: selectedStatus }) });
        if (!statusResponse.ok) throw new Error(`Student details were saved, but status was not. ${await problem(statusResponse, "Check the selected status.")}`);
      }
      if (canViewSensitive) {
        const weightKg = data.get("weightKg"); const heightCm = data.get("heightCm");
        const privateResponse = await fetch(`/api/backend/students/${studentId}/sensitive`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({
          address: data.get("address") || null, medicalInformation: data.get("medicalInformation") || null,
          allergies: data.get("allergies") || null, specialEducationalNeeds: data.get("specialEducationalNeeds") || null,
          genotype: data.get("genotype") || null, bloodGroup: data.get("bloodGroup") || null,
          weightKg: weightKg ? Number(weightKg) : null, heightCm: heightCm ? Number(heightCm) : null,
          disability: data.get("disability") || null, privateNotes: data.get("privateNotes") || null,
        }) });
        if (!privateResponse.ok) throw new Error(`Student details were saved, but medical and support details were not. ${await problem(privateResponse, "Please retry those details.")}`);
      }
      notify({ title: "Student record updated", message: "Profile and available medical details were saved." });
      router.push(`/portal/students/${studentId}`);
      router.refresh();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The student record could not be saved."); }
    finally { setSaving(false); }
  }

  async function uploadFiles(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) return;
    const form = event.currentTarget;
    const selected = ["photo", "identity", "other"].map(category => ({ category, file: new FormData(form).get(category) })).filter((item): item is { category: string; file: File } => item.file instanceof File && item.file.size > 0);
    if (!selected.length) { setError("Choose at least one file to upload."); return; }
    if (selected.some(item => item.file.size > 10 * 1024 * 1024 || !["image/jpeg", "image/png", "application/pdf"].includes(item.file.type))) { setError("Choose JPG, PNG or PDF files up to 10 MB."); return; }
    setUploading(true); setError("");
    try {
      for (const { category, file } of selected) {
        const begin = await fetch(`/api/backend/documents/Student/${studentId}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size, category }) });
        if (!begin.ok) throw new Error(await problem(begin, `${category} upload could not be started.`));
        const { fileId } = await begin.json() as { fileId: string };
        const stored = await fetch(`/api/backend/documents/${fileId}/content`, { method: "PUT", headers: { "Content-Type": file.type }, body: file });
        if (!stored.ok) throw new Error(await problem(stored, `${category} could not be stored.`));
        const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
        const checksum = Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, "0")).join("");
        const complete = await fetch(`/api/backend/documents/${fileId}/complete`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checksum }) });
        if (!complete.ok) throw new Error(await problem(complete, `${category} upload could not be completed.`));
      }
      form.reset(); notify({ title: "Files uploaded", message: "The selected student files were saved securely." });
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The files could not be uploaded."); }
    finally { setUploading(false); }
  }

  if (loading) return <div className="h-72 w-full animate-pulse rounded-3xl bg-slate-100" />;
  if (error && !student) return <main className="w-full"><Link href="/portal/students" className="text-sm font-bold tenant-primary-text">Back to students</Link><p role="alert" className="mt-5 rounded-2xl bg-red-50 p-5 text-red-800">{error}</p></main>;
  if (!student) return null;
  if (!canManage) return <main className="w-full"><Link href={`/portal/students/${studentId}`} className="text-sm font-bold tenant-primary-text">Back to student record</Link><p role="alert" className="mt-5 rounded-2xl bg-red-50 p-5 text-red-800">You do not have permission to edit student records.</p></main>;

  const privateValue = (key: keyof Sensitive) => sensitive?.[key] ?? "";
  return <main className="w-full space-y-6 pb-8">
    <div className="flex flex-wrap items-center justify-between gap-3"><Link href={`/portal/students/${studentId}`} className="text-sm font-bold text-slate-600 hover:text-slate-950">← Back to student profile</Link><span className="text-xs font-semibold text-slate-500">Student / {student.admissionNumber} / Edit</span></div>
    <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"><p className="text-xs font-black uppercase tracking-[.18em] tenant-primary-text">Student directory</p><h1 className="mt-1 text-2xl font-black text-slate-950 sm:text-3xl">Edit {student.firstName} {student.lastName}</h1><p className="mt-2 text-sm text-slate-600">Update the personal and contact details recorded for this student. The admission number remains system-managed.</p></section>
    <form onSubmit={save} className="space-y-6">
      <Section title="Student identity" note="Core personal details used across the student record."><Grid>
        <Field name="firstName" label="First name" value={student.firstName} required /><Field name="middleName" label="Middle name" value={student.middleName ?? ""} /><Field name="lastName" label="Last name" value={student.lastName} required />
        <Field name="dateOfBirth" label="Date of birth" type="date" value={student.dateOfBirth.slice(0, 10)} required />
        <Select name="gender" label="Gender" value={student.gender ?? ""} optional options={["Male", "Female", "Other", "Prefer not to say"].map(value => [value, value])} />
        <Select name="studentType" label="Student type" value={String(student.studentType)} options={studentTypes.map((name, index) => [String(index), name])} />
        <Select name="status" label="Record status" value={String(student.status)} onChange={value => setSelectedStatus(Number(value))} options={statuses.map(([id, name]) => [String(id), name])} />
      </Grid></Section>
      <Section title="Contact details" note="Email and phone used to reach the student directly, where applicable."><Grid>
        <Field name="email" label="Student email" type="email" value={student.email ?? ""} /><Field name="phone" label="Phone number" type="tel" value={student.phone ?? ""} />
      </Grid></Section>
      {canViewSensitive && <Section title="Personal & residential details" note="Restricted information visible only to authorised student managers."><Grid>
        <Field name="address" label="Home address" value={String(privateValue("address"))} /><Field name="privateNotes" label="Internal notes" value={String(privateValue("privateNotes"))} />
      </Grid></Section>}
      {canViewSensitive && <Section title="Medical information" note="Restricted health and learning-support information."><Grid>
        <Select name="genotype" label="Genotype" value={String(privateValue("genotype"))} optional options={genotypes.map(value => [value, value])} />
        <Select name="bloodGroup" label="Blood group" value={String(privateValue("bloodGroup"))} optional options={bloodGroups.map(value => [value, value])} />
        <Field name="weightKg" label="Weight (kg)" type="number" step="any" value={sensitive?.weightKg != null ? String(sensitive.weightKg) : ""} />
        <Field name="heightCm" label="Height (cm)" type="number" step="any" value={sensitive?.heightCm != null ? String(sensitive.heightCm) : ""} />
        <Select name="disability" label="Disability / accessibility need" value={String(privateValue("disability"))} optional options={disabilityOptions.map(value => [value, value])} />
        <Field name="medicalInformation" label="Medical information" value={String(privateValue("medicalInformation"))} /><Field name="allergies" label="Allergies" value={String(privateValue("allergies"))} />
        <Field name="specialEducationalNeeds" label="Special educational needs" value={String(privateValue("specialEducationalNeeds"))} />
      </Grid></Section>}
      {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{error}</p>}
      <div className="flex flex-wrap justify-end gap-3"><Link href={`/portal/students/${studentId}`} className="rounded-xl bg-red-700 px-5 py-2.5 text-sm font-bold text-white hover:bg-red-800">Cancel</Link><button disabled={saving} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50">{saving ? "Saving…" : "Save student record"}</button></div>
    </form>
    <GuardianSection studentId={studentId} guardians={guardians} canManage={canManageGuardians} onChanged={() => void reloadGuardians()} />
    <section className="rounded-3xl border border-slate-200 bg-white p-6 sm:p-8"><h2 className="text-lg font-black text-slate-950">Photograph and documents</h2><p className="mt-1 text-sm text-slate-600">Upload replacements or additional identity evidence using secure storage.</p><form onSubmit={uploadFiles} className="mt-5 grid gap-4 sm:grid-cols-3">{[["photo", "Student photograph"], ["identity", "Identity document"], ["other", "Other document"]].map(([name, label]) => <label key={name} className="text-xs font-bold text-slate-700">{label}<input name={name} type="file" accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf" className={`${fieldClass} cursor-pointer file:cursor-pointer file:border-0 file:bg-transparent file:font-bold`} /></label>)}<button disabled={uploading} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50 sm:col-span-3 sm:justify-self-start">{uploading ? "Uploading…" : "Upload selected files"}</button></form>{error && <p role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</p>}<Link href={`/portal/students/${studentId}#documents`} className="mt-4 inline-flex text-sm font-bold tenant-primary-text hover:underline">View or remove existing documents →</Link></section>
  </main>;
}

function GuardianSection({ studentId, guardians, canManage, onChanged }: { studentId: string; guardians: Guardian[]; canManage: boolean; onChanged: () => void }) {
  const [linking, setLinking] = useState(false);
  const [query, setQuery] = useState("");
  const [matches, setMatches] = useState<GuardianMatch[]>([]);
  const [searching, setSearching] = useState(false);
  const [selected, setSelected] = useState<GuardianMatch | null>(null);
  const [relationship, setRelationship] = useState("2");
  const [isPrimary, setIsPrimary] = useState(guardians.length === 0);
  const [isEmergency, setIsEmergency] = useState(true);
  const [mayCollect, setMayCollect] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!linking || selected || query.trim().length < 2) {
      const clear = setTimeout(() => setMatches([]), 0);
      return () => clearTimeout(clear);
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setSearching(true);
      void fetch(`/api/backend/guardians?page=1&pageSize=8&sort=lastName&descending=false&search=${encodeURIComponent(query.trim())}`, { cache: "no-store", signal: controller.signal })
        .then(async response => { if (!response.ok) throw new Error(); const body = await response.json() as { items?: GuardianMatch[] }; setMatches((body.items ?? []).filter(match => !guardians.some(existing => existing.id === match.id))); })
        .catch(reason => { if (reason instanceof DOMException && reason.name === "AbortError") return; setMatches([]); })
        .finally(() => setSearching(false));
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [query, linking, selected, guardians]);

  async function link() {
    if (!selected) return;
    setBusy(true); setError("");
    try {
      const response = await fetch(`/api/backend/students/${studentId}/guardians`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ guardianId: selected.id, relationship: Number(relationship), isPrimary, isEmergencyContact: isEmergency, mayCollect }) });
      if (!response.ok) throw new Error(await problem(response, "The guardian could not be linked."));
      notify({ title: "Guardian linked", message: `${selected.firstName} ${selected.lastName} is now linked to this student.` });
      setLinking(false); setSelected(null); setQuery(""); setMatches([]);
      onChanged();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The guardian could not be linked."); }
    finally { setBusy(false); }
  }

  async function unlink(guardianId: string, name: string) {
    setBusy(true); setError("");
    try {
      const response = await fetch(`/api/backend/guardians/${guardianId}/student-links/${studentId}`, { method: "DELETE" });
      if (!response.ok) throw new Error("The guardian could not be unlinked.");
      notify({ title: "Guardian unlinked", message: `${name} is no longer linked to this student.` });
      onChanged();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The guardian could not be unlinked."); }
    finally { setBusy(false); }
  }

  return <Section title="Guardians" note="Manage which parents or guardians are linked to this student.">
    {guardians.length === 0 ? <p className="text-sm text-slate-500">No guardians are linked yet.</p> : <ul className="divide-y divide-slate-100">{guardians.map(guardian => <li key={guardian.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
      <div><Link href={`/portal/guardians/${guardian.id}`} className="text-sm font-bold text-slate-900 hover:underline">{guardian.firstName} {guardian.lastName}</Link><p className="text-xs text-slate-500">{guardian.phone}{guardian.email ? ` · ${guardian.email}` : ""}</p></div>
      {canManage && <button type="button" disabled={busy} onClick={() => void unlink(guardian.id, `${guardian.firstName} ${guardian.lastName}`)} className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-bold text-red-700 hover:bg-red-50 disabled:opacity-50">Unlink</button>}
    </li>)}</ul>}
    {canManage && <div className="mt-5 border-t border-slate-100 pt-5">
      {!linking ? <button type="button" onClick={() => setLinking(true)} className="text-xs font-bold tenant-primary-text">+ Link a guardian</button> : <div className="space-y-4">
        <label className="block text-xs font-bold text-slate-700">Search by name, phone or email<input value={query} onChange={event => { setQuery(event.target.value); setSelected(null); }} className={fieldClass} placeholder="Start typing to search existing guardians" /></label>
        {searching && <p className="text-xs font-semibold text-slate-500">Searching…</p>}
        {!selected && matches.length > 0 && <div className="space-y-2">{matches.map(match => <button type="button" key={match.id} onClick={() => setSelected(match)} className="flex w-full items-center justify-between rounded-lg border border-slate-200 px-3 py-2 text-left hover:border-slate-400"><span><span className="block text-sm font-bold text-slate-900">{match.firstName} {match.lastName}</span><span className="block text-xs text-slate-500">{match.phone}{match.email ? ` · ${match.email}` : ""}</span></span><span className="text-xs font-black tenant-primary-text">Select</span></button>)}</div>}
        {!selected && !searching && query.trim().length >= 2 && matches.length === 0 && <p className="text-xs text-slate-500">No matching guardian found. <Link href="/portal/guardians" className="font-bold tenant-primary-text hover:underline">Add a new guardian</Link> first, then link them here.</p>}
        {selected && <div className="flex items-center justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-3"><div><p className="text-sm font-bold text-slate-900">{selected.firstName} {selected.lastName}</p><p className="text-xs text-slate-600">{selected.phone}{selected.email ? ` · ${selected.email}` : ""}</p></div><button type="button" onClick={() => setSelected(null)} className="rounded-lg bg-red-700 px-3 py-2 text-xs font-bold text-white">Change</button></div>}
        {selected && <Grid>
          <Select name="relationship" label="Relationship" value={relationship} onChange={setRelationship} options={relationships.map((name, index) => [String(index), displayRelationship(name)])} />
          <Select name="isPrimary" label="Primary guardian" value={String(isPrimary)} onChange={value => setIsPrimary(value === "true")} options={yesNo} />
          <Select name="isEmergency" label="Emergency contact" value={String(isEmergency)} onChange={value => setIsEmergency(value === "true")} options={yesNo} />
          <Select name="mayCollect" label="May collect student" value={String(mayCollect)} onChange={value => setMayCollect(value === "true")} options={yesNo} />
        </Grid>}
        {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
        <div className="flex gap-3"><button type="button" onClick={() => { setLinking(false); setSelected(null); setQuery(""); setError(""); }} className="rounded-xl bg-slate-200 px-4 py-2.5 text-sm font-bold text-slate-700">Cancel</button><button type="button" disabled={!selected || busy} onClick={() => void link()} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Linking…" : "Link guardian"}</button></div>
      </div>}
    </div>}
  </Section>;
}

function Section({ title, note, children }: { title: string; note: string; children: React.ReactNode }) { return <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"><h2 className="text-xl font-black text-slate-950">{title}</h2><p className="mb-6 mt-1 text-sm text-slate-500">{note}</p>{children}</section>; }
function Grid({ children }: { children: React.ReactNode }) { return <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">{children}</div>; }
function Field({ name, label, value, type = "text", required, step }: { name: string; label: string; value: string; type?: string; required?: boolean; step?: string }) { return <label className="text-xs font-bold text-slate-700">{label}<input name={name} type={type} step={step} defaultValue={value} required={required} className={fieldClass} /></label>; }
function Select({ name, label, value, options, optional, onChange }: { name: string; label: string; value: string; options: string[][]; optional?: boolean; onChange?: (value: string) => void }) { return <label className="text-xs font-bold text-slate-700">{label}<select name={name} defaultValue={value} onChange={event => onChange?.(event.target.value)} className={fieldClass}>{optional && <option value="">Not selected</option>}{value && !options.some(([id]) => id === value) && <option value={value}>{value}</option>}{options.map(([id, text]) => <option key={id} value={id}>{text}</option>)}</select></label>; }
function displayRelationship(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2"); }
async function problem(response: Response, fallback: string) { try { const body = await response.json() as { detail?: string; message?: string; title?: string }; return body.detail ?? body.message ?? body.title ?? fallback; } catch { return fallback; } }
