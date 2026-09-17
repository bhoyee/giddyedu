"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";
import { nigeriaLocations } from "@/lib/nigeria-locations";
import { StaffRecordSections } from "@/components/staff-record-sections";

type Staff = { id: string; staffNumber: string; firstName: string; lastName: string; category: number; status: number; campusId: string; departmentId: string | null; positionId: string | null; workEmail: string | null; phone: string | null; hireDate: string; exitDate: string | null };
type Sensitive = Record<string, string | number | null>;
type Position = { id: string; name: string; category: number; isActive: boolean };
type Department = { id: string; name: string };
type Social = { platform: string; id: string };
const categories = ["Teaching", "Administrative", "Non-teaching"];
const statuses = [[0,"Active"],[1,"Suspended"],[2,"Exited"],[3,"Away"],[4,"Not cleared"],[5,"Inactive"],[6,"On leave"],[7,"Retired"],[8,"Resigned"],[9,"Sacked"],[10,"Left"],[11,"Deceased"]] as const;
const fieldClass = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none focus:border-[var(--tenant-primary,#28654a)] focus:ring-2 focus:ring-[var(--tenant-primary-soft,#e7f2eb)]";
const socialPlatforms = ["LinkedIn","Facebook","Instagram","X","YouTube","TikTok","Threads","GitHub","WhatsApp","Telegram"];

export function StaffEditWorkspace({ staffId }: { staffId: string }) {
  const router = useRouter();
  const [staff, setStaff] = useState<Staff | null>(null);
  const [sensitive, setSensitive] = useState<Sensitive | null>(null);
  const [canManage, setCanManage] = useState(false);
  const [canViewSensitive, setCanViewSensitive] = useState(false);
  const [positions, setPositions] = useState<Position[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [category, setCategory] = useState(0);
  const [selectedStatus, setSelectedStatus] = useState(0);
  const [state, setState] = useState("");
  const [social, setSocial] = useState<Social[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [sessionResponse, staffResponse, sensitiveResponse, positionsResponse, structureResponse] = await Promise.all([
          fetch("/api/auth/session", { cache: "no-store" }),
          fetch(`/api/backend/hr/staff/${staffId}`, { cache: "no-store" }),
          fetch(`/api/backend/hr/staff/${staffId}/sensitive`, { cache: "no-store" }),
          fetch("/api/backend/hr/positions", { cache: "no-store" }),
          fetch("/api/backend/academics/structure", { cache: "no-store" }),
        ]);
        if (!staffResponse.ok) throw new Error(staffResponse.status === 403 ? "You cannot access this staff record." : "The staff record could not be loaded.");
        const session = sessionResponse.ok ? await sessionResponse.json() as { access?: { permissions?: string[] } } : null;
        const record = await staffResponse.json() as Staff;
        const privateRecord = sensitiveResponse.ok ? await sensitiveResponse.json() as Sensitive | null : null;
        const positionItems = positionsResponse.ok ? await positionsResponse.json() as Position[] : [];
        const structure = structureResponse.ok ? await structureResponse.json() as { departments?: Department[] } : {};
        if (!active) return;
        setCanManage(session?.access?.permissions?.includes("Staff.Manage") === true);
        setCanViewSensitive(sensitiveResponse.ok);
        setStaff(record); setSensitive(privateRecord); setPositions(positionItems); setDepartments(structure.departments ?? []);
        setCategory(record.category); setSelectedStatus(record.status); setState(String(privateRecord?.state ?? ""));
        try { const parsed = JSON.parse(String(privateRecord?.socialProfilesJson ?? "[]")) as Social[]; setSocial(Array.isArray(parsed) ? parsed : []); } catch { setSocial([]); }
      } catch (reason) { if (active) setError(reason instanceof Error ? reason.message : "The staff record could not be loaded."); }
      finally { if (active) setLoading(false); }
    }
    void load();
    return () => { active = false; };
  }, [staffId]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!staff || !canManage) return;
    const data = new FormData(event.currentTarget);
    const phone = String(data.get("phone") ?? "");
    if (!/^\d{11}$/.test(phone)) { setError("Phone number must contain exactly 11 digits."); return; }
    if (social.some(item => Boolean(item.platform) !== Boolean(item.id.trim())) || new Set(social.filter(item => item.platform).map(item => item.platform)).size !== social.filter(item => item.platform).length) { setError("Complete each social profile and use each platform only once."); return; }
    setSaving(true); setError("");
    try {
      const core = await fetch(`/api/backend/hr/staff/${staffId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ firstName: data.get("firstName"), lastName: data.get("lastName"), category, campusId: staff.campusId, departmentId: data.get("departmentId") || null, positionId: data.get("positionId") || null, email: data.get("email"), phone, hireDate: data.get("hireDate"), status: Number(data.get("status")) }) });
      if (!core.ok) throw new Error(await problem(core, "Employment details could not be saved."));
      if (selectedStatus !== staff.status || (selectedStatus === 2 && String(data.get("exitDate") ?? "") !== (staff.exitDate ?? ""))) {
        const statusResponse = await fetch(`/api/backend/hr/staff/${staffId}/status`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ status: selectedStatus, exitDate: selectedStatus === 2 ? data.get("exitDate") : null }) });
        if (!statusResponse.ok) throw new Error(`Employment details were saved, but status was not. ${await problem(statusResponse, "Check the status and exit date.")}`);
      }
      if (canViewSensitive) {
        const keys = ["title","middleName","gender","dateOfBirth","maritalStatus","religion","state","localGovernment","city","address","genotype","bloodGroup","weightKg","heightCm","disability","skills","achievements","website","notes"];
        const privateValues: Record<string, string | number | null> = { country: "Nigeria", nextOfKinName: sensitive?.nextOfKinName ?? null, nextOfKinPhone: sensitive?.nextOfKinPhone ?? null, officeAddress: sensitive?.officeAddress ?? null, socialProfilesJson: JSON.stringify(social.filter(item => item.platform && item.id.trim())) };
        for (const key of keys) privateValues[key] = String(data.get(key) ?? "").trim() || null;
        for (const key of ["weightKg","heightCm"]) privateValues[key] = privateValues[key] === null ? null : Number(privateValues[key]);
        const privateResponse = await fetch(`/api/backend/hr/staff/${staffId}/sensitive`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(privateValues) });
        if (!privateResponse.ok) throw new Error(`Employment details were saved, but private details were not. ${await problem(privateResponse, "Please retry the private details.")}`);
      }
      notify({ title: "Staff record updated", message: "Employment and available private details were saved." });
      router.push(`/portal/staff/${staffId}`);
      router.refresh();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The staff record could not be saved."); }
    finally { setSaving(false); }
  }

  async function uploadFiles(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) return;
    const form = event.currentTarget;
    const selected = ["photo", "signature", "qualification"].map(category => ({ category, file: new FormData(form).get(category) })).filter((item): item is { category: string; file: File } => item.file instanceof File && item.file.size > 0);
    if (!selected.length) { setError("Choose at least one file to upload."); return; }
    if (selected.some(item => item.file.size > 10 * 1024 * 1024 || !["image/jpeg", "image/png", "application/pdf"].includes(item.file.type) || (item.category !== "qualification" && item.file.type === "application/pdf"))) { setError("Choose JPG or PNG images up to 10 MB. Qualification evidence may also be PDF."); return; }
    setUploading(true); setError("");
    try {
      for (const { category, file } of selected) {
        const begin = await fetch(`/api/backend/documents/StaffProfile/${staffId}/uploads`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size, category }) });
        if (!begin.ok) throw new Error(await problem(begin, `${category} upload could not be started.`));
        const { fileId } = await begin.json() as { fileId: string };
        const stored = await fetch(`/api/backend/documents/${fileId}/content`, { method: "PUT", headers: { "Content-Type": file.type }, body: file });
        if (!stored.ok) throw new Error(await problem(stored, `${category} could not be stored.`));
        const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
        const checksum = Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, "0")).join("");
        const complete = await fetch(`/api/backend/documents/${fileId}/complete`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checksum }) });
        if (!complete.ok) throw new Error(await problem(complete, `${category} upload could not be completed.`));
      }
      form.reset(); notify({ title: "Files uploaded", message: "The selected staff files were saved securely." });
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The files could not be uploaded."); }
    finally { setUploading(false); }
  }

  if (loading) return <div className="h-72 w-full animate-pulse rounded-3xl bg-slate-100" />;
  if (error && !staff) return <main className="w-full"><Link href="/portal/staff" className="text-sm font-bold tenant-primary-text">Back to staff</Link><p role="alert" className="mt-5 rounded-2xl bg-red-50 p-5 text-red-800">{error}</p></main>;
  if (!staff) return null;
  if (!canManage) return <main className="w-full"><Link href={`/portal/staff/${staffId}`} className="text-sm font-bold tenant-primary-text">Back to staff record</Link><p role="alert" className="mt-5 rounded-2xl bg-red-50 p-5 text-red-800">You do not have permission to edit staff records.</p></main>;

  const privateValue = (key: string) => sensitive?.[key] == null ? "" : String(sensitive[key]);
  return <main className="w-full space-y-6 pb-8">
    <div className="flex flex-wrap items-center justify-between gap-3"><Link href={`/portal/staff/${staffId}`} className="text-sm font-bold text-slate-600 hover:text-slate-950">← Back to staff profile</Link><span className="text-xs font-semibold text-slate-500">Staff / {staff.staffNumber} / Edit</span></div>
    <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"><p className="text-xs font-black uppercase tracking-[.18em] tenant-primary-text">Staff directory</p><h1 className="mt-1 text-2xl font-black text-slate-950 sm:text-3xl">Edit {staff.firstName} {staff.lastName}</h1><p className="mt-2 text-sm text-slate-600">Update the employment and personal details recorded at registration. The staff number and active campus remain system-managed.</p></section>
    <form onSubmit={save} className="space-y-6">
      <Section title="Employment" note="Role, position and school employment details."><Grid>
        <Field name="firstName" label="First name" value={staff.firstName} required /><Field name="lastName" label="Last name" value={staff.lastName} required />
        <Select name="category" label="Category" value={String(category)} onChange={value => setCategory(Number(value))} options={categories.map((name, index) => [String(index), name])} />
        <Select name="positionId" label="Primary position" key={category} value={staff.category === category ? staff.positionId ?? "" : ""} optional options={positions.filter(item => item.category === category && item.isActive).map(item => [item.id, item.name])} />
        <Select name="departmentId" label="Department" value={staff.departmentId ?? ""} optional options={departments.map(item => [item.id, item.name])} />
        <Select name="status" label="Account status" value={String(staff.status)} onChange={value => setSelectedStatus(Number(value))} options={statuses.map(([id, name]) => [String(id), name])} />
        {selectedStatus === 2 && <Field name="exitDate" label="Exit date" type="date" value={staff.exitDate ?? ""} required />}
        <Field name="hireDate" label="Hire date" type="date" value={staff.hireDate} required />
      </Grid></Section>
      {canViewSensitive && <><Section title="Personal details" note="Restricted information visible only to authorised staff managers."><Grid>
        <Select name="title" label="Title" value={privateValue("title")} optional options={["Mr","Mrs","Miss","Dr","Prof"].map(value => [value,value])} />
        <Field name="middleName" label="Middle name" value={privateValue("middleName")} />
        <Select name="gender" label="Gender" value={privateValue("gender")} optional options={["Male","Female","Other","Prefer not to say"].map(value => [value,value])} />
        <Field name="dateOfBirth" label="Date of birth" type="date" value={privateValue("dateOfBirth")} />
        <Select name="maritalStatus" label="Marital status" value={privateValue("maritalStatus")} optional options={["Single","Married","Divorced","Widowed"].map(value => [value,value])} />
        <Select name="religion" label="Religion" value={privateValue("religion")} optional options={["Christianity","Islam","African Traditional Religion","Hinduism","Buddhism","Judaism","Sikhism","No religion","Prefer not to say","Other"].map(value => [value,value])} />
        <Field name="country" label="Country" value="Nigeria" readOnly />
        <Select name="state" label="State" value={state} onChange={setState} optional options={Object.keys(nigeriaLocations).map(value => [value,value])} />
        <Select name="localGovernment" label="Local government" key={state} value={state === privateValue("state") ? privateValue("localGovernment") : ""} optional options={(nigeriaLocations[state as keyof typeof nigeriaLocations] ?? []).map(value => [value,value])} />
        <Field name="city" label="City / town" value={privateValue("city")} />
        <Field name="address" label="Residential address" value={privateValue("address")} />
      </Grid></Section>
      <Section title="Medical information" note="Medical details remain protected by the staff-sensitive permission."><Grid>
        <Select name="genotype" label="Genotype" value={privateValue("genotype")} optional options={["AA","AS","AC","SS","SC","CC"].map(value => [value,value])} />
        <Select name="bloodGroup" label="Blood group" value={privateValue("bloodGroup")} optional options={["A+","A-","B+","B-","AB+","AB-","O+","O-"].map(value => [value,value])} />
        <Field name="weightKg" label="Weight (kg)" type="number" value={privateValue("weightKg")} />
        <Field name="heightCm" label="Height (cm)" type="number" value={privateValue("heightCm")} />
        <Field name="disability" label="Accessibility needs" value={privateValue("disability")} />
      </Grid></Section>
      <Section title="Qualifications and capabilities" note="Edit skills here. Add or remove formal qualifications below."><Grid><Field name="skills" label="Skill set" value={privateValue("skills")} /><Field name="achievements" label="Achievements" value={privateValue("achievements")} /></Grid></Section></>}
      <Section title="Contact details" note="Email and phone must remain unique to this staff member within the school."><Grid>
        <Field name="email" label="Email address" type="email" value={staff.workEmail ?? ""} required />
        <Field name="phone" label="Phone number" type="tel" value={staff.phone ?? ""} required pattern="[0-9]{11}" maxLength={11} />
        {canViewSensitive && <><Field name="website" label="Website" value={privateValue("website")} /><Field name="notes" label="Internal notes" value={privateValue("notes")} /></>}
      </Grid>{canViewSensitive && <div className="mt-6 border-t border-slate-100 pt-5"><div className="flex items-center justify-between gap-3"><h3 className="text-sm font-bold text-slate-900">Social profiles</h3><button type="button" disabled={social.length >= 10} onClick={() => setSocial(current => [...current, { platform: "", id: "" }])} className="text-xs font-bold tenant-primary-text disabled:opacity-50">+ Add profile</button></div>{social.map((item, index) => <div key={index} className="mt-3 grid items-end gap-3 sm:grid-cols-[1fr_2fr_auto]"><Select name={`social-${index}`} label="Platform" value={item.platform} optional options={socialPlatforms.map(value => [value,value])} onChange={value => setSocial(current => current.map((row, i) => i === index ? { ...row, platform: value } : row))} /><label className="text-xs font-bold text-slate-700">Profile link or handle<input value={item.id} onChange={event => setSocial(current => current.map((row, i) => i === index ? { ...row, id: event.target.value } : row))} className={fieldClass} /></label><button type="button" onClick={() => setSocial(current => current.filter((_, i) => i !== index))} className="rounded-xl border border-red-200 px-3 py-2.5 text-xs font-bold text-red-700">Remove</button></div>)}</div>}</Section>
      {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{error}</p>}
      <div className="flex flex-wrap justify-end gap-3"><Link href={`/portal/staff/${staffId}`} className="rounded-xl bg-red-700 px-5 py-2.5 text-sm font-bold text-white hover:bg-red-800">Cancel</Link><button disabled={saving} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50">{saving ? "Saving…" : "Save staff record"}</button></div>
    </form>
    <StaffRecordSections staffId={staffId} />
    <section className="rounded-3xl border border-slate-200 bg-white p-6 sm:p-8"><h2 className="text-lg font-black text-slate-950">Photograph, signature and documents</h2><p className="mt-1 text-sm text-slate-600">Upload replacements or additional qualification evidence using secure storage.</p><form onSubmit={uploadFiles} className="mt-5 grid gap-4 sm:grid-cols-3">{[["photo","Staff photograph"],["signature","Staff signature"],["qualification","Qualification document"]].map(([name, label]) => <label key={name} className="text-xs font-bold text-slate-700">{label}<input name={name} type="file" accept={name === "qualification" ? ".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf" : ".jpg,.jpeg,.png,image/jpeg,image/png"} className={`${fieldClass} cursor-pointer file:cursor-pointer file:border-0 file:bg-transparent file:font-bold`} /></label>)}<button disabled={uploading} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white disabled:opacity-50 sm:col-span-3 sm:justify-self-start">{uploading ? "Uploading…" : "Upload selected files"}</button></form>{error && <p role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</p>}<Link href={`/portal/staff/${staffId}#documents`} className="mt-4 inline-flex text-sm font-bold tenant-primary-text hover:underline">View or remove existing documents →</Link></section>
  </main>;
}

function Section({ title, note, children }: { title: string; note: string; children: React.ReactNode }) { return <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"><h2 className="text-xl font-black text-slate-950">{title}</h2><p className="mb-6 mt-1 text-sm text-slate-500">{note}</p>{children}</section>; }
function Grid({ children }: { children: React.ReactNode }) { return <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">{children}</div>; }
function Field({ name, label, value, type = "text", required, readOnly, pattern, maxLength }: { name: string; label: string; value: string; type?: string; required?: boolean; readOnly?: boolean; pattern?: string; maxLength?: number }) { return <label className="text-xs font-bold text-slate-700">{label}<input name={name} type={type} defaultValue={value} required={required} readOnly={readOnly} pattern={pattern} maxLength={maxLength} className={`${fieldClass} ${readOnly ? "bg-slate-100" : ""}`} /></label>; }
function Select({ name, label, value, options, optional, onChange }: { name: string; label: string; value: string; options: string[][]; optional?: boolean; onChange?: (value: string) => void }) { return <label className="text-xs font-bold text-slate-700">{label}<select name={name} defaultValue={value} onChange={event => onChange?.(event.target.value)} className={fieldClass}>{optional && <option value="">Not selected</option>}{value && !options.some(([id]) => id === value) && <option value={value}>{value}</option>}{options.map(([id, text]) => <option key={id} value={id}>{text}</option>)}</select></label>; }
async function problem(response: Response, fallback: string) { try { const body = await response.json() as { detail?: string; message?: string; title?: string }; return body.detail ?? body.message ?? body.title ?? fallback; } catch { return fallback; } }
