"use client";

/* eslint-disable @next/next/no-img-element -- Previews use local object URLs. */
import { FormEvent, useEffect, useRef, useState } from "react";
import { notify } from "@/components/app-toast";

type StudentMatch = { id: string; admissionNumber: string; firstName: string; lastName: string; classLevelName: string; classSectionName: string; campusName: string };
const relationships = [
  [0, "Mother"], [1, "Father"], [2, "Parent"], [3, "Legal guardian"], [4, "Relative"],
  [5, "Sponsor"], [6, "Other"], [7, "Stepmother"], [8, "Stepfather"], [9, "Grandmother"],
  [10, "Grandfather"], [11, "Aunt"], [12, "Uncle"], [13, "Sibling"], [14, "Foster parent"],
  [15, "Adoptive parent"], [16, "Caregiver"],
] as const;
const inputClass = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm text-slate-950 outline-none transition focus:border-slate-600 focus:ring-2 focus:ring-slate-200";

function classLabel(student: StudentMatch) {
  return student.classSectionName.toLowerCase().startsWith(student.classLevelName.toLowerCase())
    ? student.classSectionName : `${student.classLevelName} · ${student.classSectionName}`;
}

async function problem(response: Response, fallback: string) {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  return body?.detail ?? body?.title ?? fallback;
}

async function uploadImage(guardianId: string, file: File, category: "photo" | "signature") {
  const begin = await fetch(`/api/backend/documents/Guardian/${guardianId}/uploads`, {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ fileName: file.name, contentType: file.type, sizeBytes: file.size, category }),
  });
  if (!begin.ok) throw new Error(await problem(begin, `${category} upload could not be started.`));
  const { fileId } = await begin.json() as { fileId: string };
  const stored = await fetch(`/api/backend/documents/${fileId}/content`, {
    method: "PUT", headers: { "Content-Type": file.type }, body: file,
  });
  if (!stored.ok) throw new Error(await problem(stored, `${category} could not be stored.`));
  const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
  const checksum = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
  const complete = await fetch(`/api/backend/documents/${fileId}/complete`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checksum }),
  });
  if (!complete.ok) throw new Error(await problem(complete, `${category} could not be verified.`));
}

export function GuardianRegistrationForm({ onCreated }: { onCreated: () => void }) {
  const [canManage, setCanManage] = useState(false);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [matches, setMatches] = useState<StudentMatch[]>([]);
  const [selected, setSelected] = useState<StudentMatch | null>(null);
  const [searchBusy, setSearchBusy] = useState(false);
  const [searched, setSearched] = useState(false);
  const [relationship, setRelationship] = useState("");
  const [relationshipOpen, setRelationshipOpen] = useState(false);
  const firstNameRef = useRef<HTMLInputElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const [photo, setPhoto] = useState<File | null>(null);
  const [signature, setSignature] = useState<File | null>(null);
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);
  const [signatureUrl, setSignatureUrl] = useState<string | null>(null);

  useEffect(() => () => {
    if (photoUrl) URL.revokeObjectURL(photoUrl);
    if (signatureUrl) URL.revokeObjectURL(signatureUrl);
  }, [photoUrl, signatureUrl]);

  useEffect(() => {
    if (!open) return;
    firstNameRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) {
        if (relationshipOpen) setRelationshipOpen(false);
        else setOpen(false);
      }
      if (event.key !== "Tab") return;
      const controls = dialogRef.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled)');
      if (!controls?.length) return;
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener("keydown", escape);
    return () => { document.body.style.overflow = previousOverflow; document.removeEventListener("keydown", escape); };
  }, [open, busy, relationshipOpen]);

  useEffect(() => {
    void fetch("/api/auth/session", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const session = await response.json() as { access?: { permissions?: string[] } };
      setCanManage(session.access?.permissions?.includes("Guardians.Manage") === true);
    });
  }, []);

  useEffect(() => {
    if (!open || selected || search.trim().length < 2) return;
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setSearchBusy(true);
      try {
        const response = await fetch(`/api/backend/guardians/student-search?query=${encodeURIComponent(search.trim())}`, {
          cache: "no-store", signal: controller.signal,
        });
        if (!response.ok) throw new Error(await problem(response, "Students could not be searched."));
        setMatches(await response.json() as StudentMatch[]);
        setSearched(true);
      } catch (reason) {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Students could not be searched.");
      } finally { if (!controller.signal.aborted) setSearchBusy(false); }
    }, 300);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [open, search, selected]);

  function selectImage(file: File | undefined, kind: "photo" | "signature") {
    if (!file) { if (kind === "photo") { setPhoto(null); setPhotoUrl(null); } else { setSignature(null); setSignatureUrl(null); } return; }
    if (!["image/jpeg", "image/png"].includes(file.type) || file.size > 2 * 1024 * 1024) {
      setError("Choose a JPEG or PNG image no larger than 2 MB."); return;
    }
    setError("");
    if (kind === "photo") { setPhoto(file); setPhotoUrl(URL.createObjectURL(file)); }
    else { setSignature(file); setSignatureUrl(URL.createObjectURL(file)); }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) { setError("Select a student from the search results."); return; }
    if (!relationship) { setError("Choose the guardian's relationship to the student."); return; }
    const form = event.currentTarget;
    const data = new FormData(form);
    const phone = String(data.get("phone") ?? "").trim();
    if (!/^\d{11}$/.test(phone)) { setError("Phone number must contain exactly 11 digits."); return; }
    setBusy(true); setError("");
    let createdId: string | null = null;
    try {
      const response = await fetch("/api/backend/guardians", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName: String(data.get("firstName") ?? "").trim(), lastName: String(data.get("lastName") ?? "").trim(),
          phone, email: String(data.get("email") ?? "").trim(), address: String(data.get("address") ?? "").trim(),
          studentId: selected.id, relationship: Number(relationship),
          isPrimary: data.get("isPrimary") === "on", isEmergencyContact: data.get("isEmergencyContact") === "on",
          mayCollect: data.get("mayCollect") === "on",
        }),
      });
      if (!response.ok) throw new Error(await problem(response, "Guardian could not be created."));
      const body = await response.json() as { id?: string };
      createdId = body.id ?? null;
      if (!createdId) throw new Error("The created guardian could not be identified.");
      const imageErrors: string[] = [];
      for (const [file, category] of [[photo, "photo"], [signature, "signature"]] as const) {
        if (!file) continue;
        try { await uploadImage(createdId, file, category); }
        catch (reason) { imageErrors.push(`${category}: ${reason instanceof Error ? reason.message : "upload failed"}`); }
      }
      const invitation = await fetch("/api/backend/account-invitations", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ targetType: 1, targetId: createdId }),
      });
      const invitationError = invitation.ok ? "" : await problem(invitation, "Invitation could not be queued.");
      notify(imageErrors.length || invitationError
        ? { tone: "error", title: "Guardian saved with follow-up needed", message: `${invitationError ? `Invitation: ${invitationError}. ` : ""}${imageErrors.join("; ")}` }
        : { title: "Guardian added", message: "The student link is saved and a secure parent account invitation is queued." });
      setOpen(false); onCreated();
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Guardian could not be created.";
      if (createdId) {
        notify({ tone: "error", title: "Guardian saved with follow-up needed", message: `The record is saved. ${message} Use Send invite in the row menu to retry.` });
        setOpen(false); onCreated();
      } else setError(message);
    } finally { setBusy(false); }
  }

  if (!canManage) return null;
  return <div className="mt-7 flex justify-end"><button type="button" onClick={() => { setOpen(true); setError(""); }} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-bold text-white shadow-sm">Add guardian</button>
    {open && <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-950/55 p-2 sm:p-6" onMouseDown={event => { if (event.target === event.currentTarget && !busy) setOpen(false); }}>
      <div ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby="guardian-dialog-title" className="flex max-h-[95dvh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl sm:max-h-[90dvh] sm:rounded-3xl">
        <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-4 sm:px-7"><div><p className="text-xs font-black uppercase tracking-widest tenant-primary-text">Guardian registration</p><h2 id="guardian-dialog-title" className="mt-1 text-xl font-black text-slate-950">Add a parent or guardian</h2><p className="mt-1 text-sm text-slate-600">Confirm the child and their relationship before saving.</p></div><button type="button" disabled={busy} onClick={() => setOpen(false)} aria-label="Close guardian form" className="rounded-lg px-2 text-2xl text-slate-600 hover:bg-slate-100">×</button></div>
    <form onSubmit={event => void save(event)} className="space-y-7 overflow-y-auto px-5 py-5 sm:px-7">
      <div><h3 className="text-sm font-black text-slate-900">Personal and contact details</h3><div className="mt-4 grid gap-4 md:grid-cols-2">
        <label className="text-sm font-semibold text-slate-700">First name *<input ref={firstNameRef} name="firstName" required maxLength={100} autoComplete="given-name" className={inputClass}/></label>
        <label className="text-sm font-semibold text-slate-700">Last name *<input name="lastName" required maxLength={100} autoComplete="family-name" className={inputClass}/></label>
        <label className="text-sm font-semibold text-slate-700">Phone number *<input name="phone" required type="tel" inputMode="numeric" pattern="[0-9]{11}" maxLength={11} placeholder="09096735531" autoComplete="tel" className={inputClass}/></label>
        <label className="text-sm font-semibold text-slate-700">Email address *<input name="email" required type="email" maxLength={320} autoComplete="email" className={inputClass}/></label>
        <label className="text-sm font-semibold text-slate-700 md:col-span-2">Address *<textarea name="address" required maxLength={1000} rows={3} autoComplete="street-address" className={inputClass}/></label>
      </div></div>
      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5"><h3 className="text-sm font-black text-slate-900">Link to a student</h3><p className="mt-1 text-xs leading-5 text-slate-600">Type at least two letters of the student&apos;s name or admission number. Results show the current class and campus so you can confirm the right child.</p>
        <div className="mt-4 grid gap-4 lg:grid-cols-2"><div className="relative"><label htmlFor="guardian-student-search" className="text-sm font-semibold text-slate-700">Find student *</label><input id="guardian-student-search" value={search} onChange={event => { setSearch(event.target.value); setSelected(null); setMatches([]); setSearched(false); }} autoComplete="off" placeholder="Search name or admission number" className={inputClass}/>
          {!selected && search.trim().length >= 2 && <div role="listbox" aria-label="Student matches" className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-xl border border-slate-200 bg-white p-1 shadow-xl">{searchBusy ? <p className="p-3 text-sm text-slate-500">Searching…</p> : matches.length ? matches.map(student => <button key={student.id} role="option" aria-selected={false} type="button" onClick={() => { setSelected(student); setSearch(`${student.firstName} ${student.lastName}`); setMatches([]); }} className="block w-full rounded-lg px-3 py-2 text-left hover:bg-slate-100"><span className="block text-sm font-bold text-slate-900">{student.firstName} {student.lastName}</span><span className="block text-xs text-slate-600">{student.admissionNumber} · {classLabel(student)} · {student.campusName}</span></button>) : searched ? <p className="p-3 text-sm text-slate-500">No active student matched in this campus.</p> : <p className="p-3 text-sm text-slate-500">Searching…</p>}</div>}
        </div><div className="relative"><span id="guardian-relationship-label" className="text-sm font-semibold text-slate-700">Relationship to this student *</span><button type="button" aria-labelledby="guardian-relationship-label" aria-expanded={relationshipOpen} aria-haspopup="listbox" onClick={() => setRelationshipOpen(value => !value)} className={`${inputClass} flex items-center justify-between text-left`}>{relationships.find(([value]) => String(value) === relationship)?.[1] ?? "Select relationship"}<span aria-hidden="true">⌄</span></button>
          {relationshipOpen && <div role="listbox" aria-labelledby="guardian-relationship-label" className="mt-1 max-h-52 overflow-y-auto rounded-xl border border-slate-200 bg-white p-1 shadow-xl">{relationships.map(([value, label]) => <button key={value} type="button" role="option" aria-selected={relationship === String(value)} onClick={() => { setRelationship(String(value)); setRelationshipOpen(false); }} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-slate-900 hover:bg-slate-100">{label}</button>)}</div>}</div></div>
        {selected && <p className="mt-3 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900"><strong>Selected:</strong> {selected.firstName} {selected.lastName} · {selected.admissionNumber} · {classLabel(selected)} · {selected.campusName}</p>}
        <div className="mt-4 flex flex-wrap gap-x-6 gap-y-3 text-sm text-slate-700"><label className="flex items-center gap-2"><input type="checkbox" name="isPrimary"/>Primary guardian</label><label className="flex items-center gap-2"><input type="checkbox" name="isEmergencyContact"/>Emergency contact</label><label className="flex items-center gap-2"><input type="checkbox" name="mayCollect"/>May collect student</label></div>
      </div>
      <div><h3 className="text-sm font-black text-slate-900">Optional images</h3><p className="mt-1 text-xs text-slate-600">JPEG or PNG, up to 2 MB each. Uploads are stored with the guardian profile.</p><div className="mt-4 grid gap-4 sm:grid-cols-2">
        <label className="rounded-2xl border border-dashed border-slate-300 p-4 text-sm font-semibold text-slate-700">Guardian photograph<input type="file" accept="image/jpeg,image/png" onChange={event => selectImage(event.target.files?.[0], "photo")} className="mt-3 block w-full cursor-pointer text-xs file:mr-3 file:cursor-pointer file:rounded-lg file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:font-bold"/>{photoUrl && <img src={photoUrl} alt="Guardian photograph preview" className="mt-4 h-28 w-28 rounded-xl object-cover"/>}</label>
        <label className="rounded-2xl border border-dashed border-slate-300 p-4 text-sm font-semibold text-slate-700">Guardian signature<input type="file" accept="image/jpeg,image/png" onChange={event => selectImage(event.target.files?.[0], "signature")} className="mt-3 block w-full cursor-pointer text-xs file:mr-3 file:cursor-pointer file:rounded-lg file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:font-bold"/>{signatureUrl && <img src={signatureUrl} alt="Guardian signature preview" className="mt-4 h-28 max-w-full rounded-xl object-contain"/>}</label>
      </div></div>
      {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
      <div className="flex flex-wrap justify-end gap-3 border-t border-slate-200 pt-5"><button type="button" disabled={busy} onClick={() => setOpen(false)} className="rounded-xl bg-red-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-red-700">Cancel</button><button type="submit" disabled={busy} className="tenant-primary-bg rounded-xl px-6 py-2.5 text-sm font-bold text-white disabled:opacity-50">{busy ? "Saving…" : "Save guardian"}</button></div>
    </form></div></div>}
  </div>;
}
