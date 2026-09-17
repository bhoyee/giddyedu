"use client";

import { createContext, FormEvent, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { notify } from "@/components/app-toast";
import { nigeriaLocations } from "@/lib/nigeria-locations";
import { SearchableCombobox } from "@/components/resource-page";
import { StaffFormSelect } from "@/components/staff-form-select";
import { staffKinRelationships } from "@/lib/staff-kin-relationships";

type Option = { id: string; name: string };
type Structure = { departments?: Option[] };
const steps = ["Employment", "Personal", "Medical", "Qualifications", "Contact", "Documents"] as const;
const input = "mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm outline-none transition focus:border-[var(--tenant-primary,#28654a)] focus:ring-4 focus:ring-emerald-950/5";
const label = "block text-xs font-bold text-slate-700";
const religions = ["Christianity", "Islam", "African Traditional Religion", "Hinduism", "Buddhism", "Judaism", "Sikhism", "Baháʼí Faith", "Jainism", "Taoism", "Shinto", "Zoroastrianism", "No religion", "Prefer not to say", "Other"];
const socialPlatforms = ["LinkedIn", "Facebook", "Instagram", "X", "YouTube", "TikTok", "Threads", "GitHub", "WhatsApp", "Telegram"];
const qualificationGrades = ["A1", "B2", "B3", "C4", "C5", "C6", "D7", "E8", "F9", "Distinction", "Upper Credit", "Lower Credit", "Merit", "Credit", "Pass", "First Class", "Second Class Upper (2:1)", "Second Class Lower (2:2)", "Third Class", "Satisfactory", "Unclassified", "Not graded", "Other"];
type SocialProfileDraft = { key: number; platform: string; handle: string };
const DraftContext = createContext<{ values: Record<string, FormDataEntryValue>; setField: (name: string, value: FormDataEntryValue) => void }>({ values: {}, setField: () => {} });

export function StaffRegistrationWizard({ onCreated }: { onCreated?: () => void }) {
  const [open, setOpen] = useState(false); const [step, setStep] = useState(0); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const [showDocumentNotice, setShowDocumentNotice] = useState(true);
  const nextSocialKey = useRef(1);
  const submitRequested = useRef(false);
  const [socialProfiles, setSocialProfiles] = useState<SocialProfileDraft[]>([{ key: 0, platform: "", handle: "" }]);
  useEffect(() => { if (!error) return; const timeout = window.setTimeout(() => setError(""), 5000); return () => window.clearTimeout(timeout); }, [error]);
  useEffect(() => { if (!open || step !== 5 || !showDocumentNotice) return; const timeout = window.setTimeout(() => setShowDocumentNotice(false), 7000); return () => window.clearTimeout(timeout); }, [open, step, showDocumentNotice]);
  const [draft, setDraft] = useState<Record<string, FormDataEntryValue>>({});
  const [departments, setDepartments] = useState<Option[]>([]);
  const [category, setCategory] = useState(""); const [state, setState] = useState(""); const [religion, setReligion] = useState(""); const [qualification, setQualification] = useState(false); const [qualificationName, setQualificationName] = useState(""); const [grade, setGrade] = useState(""); const [addNextOfKin, setAddNextOfKin] = useState(false); const [kinRelationship, setKinRelationship] = useState("");
  const lgas = useMemo(() => state ? nigeriaLocations[state as keyof typeof nigeriaLocations] ?? [] : [], [state]);
  const setField = useCallback((name: string, value: FormDataEntryValue) => setDraft(current => ({...current, [name]:value})), []);
  const rememberPosition = useCallback((value: string) => setField("positionId", value), [setField]);
  const categoryName = category === "0" ? "Teaching" : category === "1" ? "Administrative" : category === "2" ? "Non-teaching" : "";
  function changeCategory(value: string) { setCategory(value); setDraft(current => ({...current, category:value, positionId:""})); }
  function changeState(value: string) { setState(value); setDraft(current => ({...current, state:value, localGovernment:""})); }
  function updateSocialProfile(key: number, field: "platform" | "handle", value: string) { setSocialProfiles(current => current.map(row => row.key === key ? { ...row, [field]: value } : row)); }
  function addSocialProfile() { setSocialProfiles(current => current.length >= 10 ? current : [...current, { key: nextSocialKey.current++, platform: "", handle: "" }]); }
  function removeSocialProfile(key: number) { setSocialProfiles(current => current.filter(row => row.key !== key)); }
  function validateSocialProfiles() {
    if (socialProfiles.some(row => Boolean(row.platform) !== Boolean(row.handle.trim()))) throw new Error("Choose a platform and enter its profile link or handle for each social profile.");
    const selected = socialProfiles.map(row => row.platform).filter(Boolean);
    if (new Set(selected).size !== selected.length) throw new Error("Each social platform can be added only once.");
  }

  function rememberVisibleFields(form: HTMLFormElement | null) {
    if (!form) return;
    const values = Object.fromEntries([...new FormData(form)].filter(([, value]) => !(value instanceof File && value.size === 0)));
    setDraft(current => ({ ...current, ...values }));
  }

  function changeStep(nextStep: number, form: HTMLFormElement | null) {
    submitRequested.current = false;
    rememberVisibleFields(form);
    setError("");
    setStep(nextStep);
  }

  useEffect(() => { if (!open) return; void fetch("/api/backend/academics/structure").then(r => r.ok ? r.json() : {}).then(structure => setDepartments((structure as Structure).departments ?? [])); }, [open]);

  async function upload(staffId: string, file: File, categoryName: string) {
    const begin = await fetch(`/api/backend/documents/StaffProfile/${staffId}/uploads`, { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({fileName:file.name, contentType:file.type, sizeBytes:file.size, category:categoryName}) });
    if (!begin.ok) throw new Error(`${categoryName === "photo" ? "Photograph" : "Signature"} upload could not be started.`);
    const details = await begin.json() as { fileId:string }; const stored = await fetch(`/api/backend/documents/${details.fileId}/content`, {method:"PUT",headers:{"Content-Type":file.type},body:file});
    if (!stored.ok) throw new Error(`${categoryName} could not be stored. ${await problem(stored,"Please try the upload again.")}`);
    const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer()); const checksum = Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2,"0")).join("");
    const complete = await fetch(`/api/backend/documents/${details.fileId}/complete`, {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({checksum})});
    if (!complete.ok) throw new Error("A document upload could not be verified.");
  }

  function continueRegistration(form: HTMLFormElement | null) {
    if (!form?.reportValidity()) return;
    const visible = new FormData(form);
    if (step === 0 && !category) { setError("Select a staff category."); return; }
    if (step === 0 && !visible.get("status")) { setError("Select the initial account status."); return; }
    if (step === 3 && qualification && !qualificationName) { setError("Select a qualification."); return; }
    if (step === 4) {
      const contact = new FormData(form);
      if (!/^\d{11}$/.test(String(contact.get("phone") ?? ""))) { setError("Phone number must contain exactly 11 digits."); return; }
      if (addNextOfKin && !/^\d{11}$/.test(String(contact.get("kinPhone") ?? ""))) { setError("Next-of-kin phone number must contain exactly 11 digits."); return; }
      if (addNextOfKin && (!kinRelationship || (kinRelationship === "Other" && !String(contact.get("kinOtherRelationship") ?? "").trim()))) { setError("Select the next-of-kin relationship, or specify Other."); return; }
      try { validateSocialProfiles(); normalizeWebsite(String(contact.get("website") ?? "")); }
      catch (reason) { setError(reason instanceof Error ? reason.message : "Check the contact details."); return; }
    }
    changeStep(step + 1, form);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (step !== steps.length - 1 || !submitRequested.current) return;
    submitRequested.current = false;
    setBusy(true); setError(""); const form = event.currentTarget; const data = new FormData(form);
    let createdStaffId: string | null = null;
    let followUp = "staff details";
    try {
      for (const [name, value] of Object.entries(draft)) if (!data.has(name) || (data.get(name) instanceof File && (data.get(name) as File).size === 0)) data.set(name, value);
      if (!data.get("firstName") || !data.get("lastName") || !data.get("hireDate")) { setStep(0); throw new Error("Complete the required employment details before creating the staff record."); }
      if (!data.get("category") || !data.get("status")) { setStep(0); throw new Error("Select a staff category and initial account status."); }
      if (qualification && (!data.get("qualificationName") || !data.get("institution") || !data.get("awardedOn"))) { setStep(3); throw new Error("Complete the required qualification details."); }
      if (religion === "Other" && !String(data.get("otherReligion") ?? "").trim()) { setStep(1); throw new Error("Specify the staff member's religion."); }
      const phone = String(data.get("phone") ?? ""); if (!/^\d{11}$/.test(phone)) { setStep(4); throw new Error("Phone number must contain exactly 11 digits."); }
      const email = String(data.get("email") ?? "").trim(); if (!email) { setStep(4); throw new Error("Email address is required."); }
      if (addNextOfKin && (!String(data.get("kinFullName") ?? "").trim() || !kinRelationship || (kinRelationship === "Other" && !String(data.get("kinOtherRelationship") ?? "").trim()) || !/^\d{11}$/.test(String(data.get("kinPhone") ?? "")))) { setStep(4); throw new Error("Complete the next-of-kin name, relationship and 11-digit phone number."); }
      let website: string | null;
      try { website = normalizeWebsite(String(data.get("website") ?? "")); validateSocialProfiles(); }
      catch (reason) { setStep(4); throw reason; }
      const staffResponse = await fetch("/api/backend/hr/staff", {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({firstName:data.get("firstName"),lastName:data.get("lastName"),category:Number(data.get("category")),campusId:"00000000-0000-0000-0000-000000000000",departmentId:data.get("departmentId")||null,positionId:data.get("positionId")||null,email,phone,hireDate:data.get("hireDate"),status:Number(data.get("status"))})});
      if (!staffResponse.ok) throw new Error(await problem(staffResponse, "The staff profile could not be created."));
      const created = await staffResponse.json() as { id?:string }; const staffId = created.id ?? String(created); if (!staffId) throw new Error("The created staff record could not be identified."); createdStaffId = staffId;
      const social = socialProfiles.filter(row => row.platform && row.handle.trim()).map(row => ({ platform: row.platform, id: row.handle.trim() }));
      const sensitive = await fetch(`/api/backend/hr/staff/${staffId}/sensitive`, {method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({address:data.get("address")||null,notes:data.get("notes")||null,title:data.get("title")||null,middleName:data.get("middleName")||null,gender:data.get("gender")||null,dateOfBirth:data.get("dateOfBirth")||null,maritalStatus:data.get("maritalStatus")||null,religion:data.get("religion")==="Other"?data.get("otherReligion"):data.get("religion")||null,country:"Nigeria",state:data.get("state")||null,localGovernment:data.get("localGovernment")||null,city:data.get("city")||null,genotype:data.get("genotype")||null,bloodGroup:data.get("bloodGroup")||null,weightKg:numberOrNull(data.get("weightKg")),heightCm:numberOrNull(data.get("heightCm")),disability:data.get("disability")||null,skills:data.get("skills")||null,achievements:data.get("achievements")||null,website,officeAddress:null,socialProfilesJson:social.length?JSON.stringify(social):null})});
      if (!sensitive.ok) throw new Error(await problem(sensitive, "The private staff details could not be saved."));
      followUp = "qualification";
      if (qualification && data.get("qualificationName")) { const selected=String(data.get("qualificationName")); const resolvedName=selected==="Other"?String(data.get("otherQualification")??"").trim():selected; if(!resolvedName) throw new Error("Enter the qualification details for Other."); const resolvedGrade=grade==="Other"?String(data.get("otherGrade")??"").trim():grade; if(grade==="Other"&&!resolvedGrade) throw new Error("Enter the grade for Other."); const response = await fetch(`/api/backend/hr/staff/${staffId}/qualifications`, {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({institution:data.get("institution"),name:resolvedName,fieldOfStudy:data.get("fieldOfStudy")||null,awardedOn:data.get("awardedOn"),grade:resolvedGrade||null})}); if(!response.ok) throw new Error(await problem(response,"The qualification could not be saved.")); }
      followUp = "next-of-kin contact";
      if (addNextOfKin) { const relationship = kinRelationship === "Other" ? String(data.get("kinOtherRelationship")).trim() : kinRelationship; const response = await fetch(`/api/backend/hr/staff/${staffId}/next-of-kin`, {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({fullName:String(data.get("kinFullName")).trim(),relationship,phone:data.get("kinPhone"),email:data.get("kinEmail")||null,address:data.get("kinAddress")||null,isPrimary:true})}); if(!response.ok) throw new Error(await problem(response,"The next-of-kin contact could not be saved.")); }
      for (const [fieldName, categoryName] of [["photo","photo"],["signature","signature"],["qualificationDocument","qualification"]] as const) { const file=data.get(fieldName); if(file instanceof File && file.size) { followUp = `${categoryName} upload`; await upload(staffId,file,categoryName); } }
      followUp = "invitation email";
      const invitation = await fetch("/api/backend/account-invitations", {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({targetType:0,targetId:staffId})});
      if (!invitation.ok) throw new Error(await problem(invitation, "The staff invitation could not be queued."));
      notify({title:"Staff record saved successfully",message:`A welcome email with a secure password-setup link has been queued for ${email}.`}); setOpen(false); setStep(0); setDraft({}); setCategory(""); setState(""); setReligion(""); setQualification(false); setQualificationName(""); setGrade(""); setAddNextOfKin(false); setKinRelationship(""); setSocialProfiles([{key:0,platform:"",handle:""}]); nextSocialKey.current=1; setShowDocumentNotice(true); form.reset(); onCreated?.();
    } catch (reason) { const message=reason instanceof Error?reason.message:"The staff record could not be created."; if (createdStaffId) { notify({title:"Staff record saved successfully",message:`The ${followUp} needs attention: ${message} Open the staff record to finish it.`}); setOpen(false); onCreated?.(); } else setError(message); }
    finally { setBusy(false); }
  }

  return <DraftContext.Provider value={{values:draft,setField}}><div>
    <button type="button" onClick={() => setOpen(true)} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-black text-white shadow-sm transition hover:-translate-y-0.5">Add staff member</button>
    {open && <div className="fixed inset-0 z-[120] bg-slate-950/45 p-3 backdrop-blur-sm sm:p-6" role="dialog" aria-modal="true" aria-label="Register staff member"><div className="mx-auto flex h-full max-w-6xl flex-col overflow-hidden rounded-[1.75rem] bg-white shadow-2xl">
      <header className="flex items-start justify-between border-b border-slate-200 px-5 py-4 sm:px-7"><div><p className="text-[10px] font-black uppercase tracking-[.18em] text-emerald-700">Staff registration</p><h2 className="mt-1 text-xl font-black text-slate-950 sm:text-2xl">Create a complete staff record</h2><p className="mt-1 text-xs text-slate-500">The staff number is generated securely after submission.</p></div><button type="button" onClick={() => setOpen(false)} className="grid size-10 place-items-center rounded-full bg-slate-100 text-xl hover:bg-red-50 hover:text-red-700" aria-label="Close">×</button></header>
      <nav className="flex gap-1 overflow-x-auto border-b border-slate-200 px-4 py-3 sm:px-7">{steps.map((name,index)=><button type="button" key={name} onClick={()=>setStep(index)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-black ${step===index?"tenant-primary-bg text-white":"text-slate-500 hover:bg-slate-100 hover:text-slate-900"}`}>{name}</button>)}</nav>
      <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col"><div className="flex-1 overflow-y-auto p-5 sm:p-7">
        {step===0&&<Panel title="Employment details" note="The staff member is assigned automatically to your active campus workspace."><Grid><Field name="firstName" title="First name" required/><Field name="middleName" title="Middle name"/><Field name="lastName" title="Last name" required/><Select name="category" title="Category" value={category} onChange={changeCategory} options={[["0","Teaching"],["1","Administrative"],["2","Non-teaching"]]}/><label className={label}>Primary position<SearchableCombobox key={categoryName} field={{name:"positionId",label:"Primary position",type:"combobox",required:false,optionsEndpoint:"hr/positions",optionLabelKey:"name",optionContextKey:"categoryName",manageEndpoint:"hr/positions"}} categoryContext={categoryName} defaultValue={String(draft.positionId??"")} onValueChange={rememberPosition}/></label><Select name="departmentId" title="Department" optional options={departments.map(x=>[x.id,x.name])}/><Field name="hireDate" title="Hire date" type="date" required/><Select name="status" title="Initial account status" options={[["0","Active"],["3","Away"],["4","Not cleared"],["5","Inactive"],["6","On leave"],["7","Retired"],["8","Resigned"],["9","Sacked"],["10","Left"],["11","Deceased"]]} note="Only staff-relevant statuses are available; student statuses are intentionally excluded."/></Grid></Panel>}
        {step===1&&<Panel title="Personal details" note="Identity and residential details are restricted to authorised staff managers."><Grid><Select name="title" title="Title" optional options={[["Mr","Mr"],["Mrs","Mrs"],["Miss","Miss"],["Dr","Dr"],["Prof","Prof"]]}/><Select name="gender" title="Gender" optional options={[["Male","Male"],["Female","Female"],["Other","Other"],["Prefer not to say","Prefer not to say"]]}/><Field name="dateOfBirth" title="Date of birth" type="date"/><Select name="maritalStatus" title="Marital status" optional options={[["Single","Single"],["Married","Married"],["Divorced","Divorced"],["Widowed","Widowed"]]}/><Select name="religion" title="Religion" optional value={religion} onChange={setReligion} options={religions.map(x=>[x,x])}/>{religion==="Other"&&<Field name="otherReligion" title="Specify religion" required/>}<Field name="country" title="Country" value="Nigeria" readOnly/><Select name="state" title="State" optional value={state} onChange={changeState} options={Object.keys(nigeriaLocations).map(x=>[x,x])}/><Select key={state} name="localGovernment" title="Local government" optional disabled={!state} options={lgas.map(x=>[x,x])}/><Field name="city" title="City / town"/><Area name="address" title="Residential address" wide/></Grid></Panel>}
        {step===2&&<Panel title="Medical information" note="Medical information is sensitive and is available only through the protected staff-sensitive permission."><Grid><Select name="genotype" title="Genotype" optional options={["AA","AS","AC","SS","SC","CC"].map(x=>[x,x])}/><Select name="bloodGroup" title="Blood group" optional options={["A+","A-","B+","B-","AB+","AB-","O+","O-"].map(x=>[x,x])}/><Field name="weightKg" title="Weight (kg)" type="number"/><Field name="heightCm" title="Height (cm)" type="number"/><Area name="disability" title="Disability or accessibility needs" wide/></Grid></Panel>}
        {step===3&&<Panel title="Qualifications and capabilities" note="Add the highest or most relevant qualification now. More qualifications can be added from the staff profile."><label className="mb-5 flex items-center gap-3 text-sm font-bold"><input type="checkbox" checked={qualification} onChange={e=>setQualification(e.target.checked)} className="size-4"/> Add a formal qualification</label>{qualification&&<Grid><Field name="institution" title="Institution" required/><Select name="qualificationName" title="Qualification" value={qualificationName} onChange={setQualificationName} options={["SSCE / WAEC","SSCE / NECO","NABTEB","NCE","OND","HND","Certificate","Diploma","Bachelor's degree","PGDE","Postgraduate diploma","Master's degree","MPhil","Professional qualification","PhD / Doctorate","Other"].map(x=>[x,x])}/>{qualificationName==="Other"&&<Field name="otherQualification" title="Other qualification details" required/>}<Field name="fieldOfStudy" title="Field of study"/><Field name="awardedOn" title="Date awarded" type="date" required/><Select name="grade" title="Grade / class" optional value={grade} onChange={setGrade} options={qualificationGrades.map(x=>[x,x])}/>{grade==="Other"&&<Field name="otherGrade" title="Specify grade" required/>}</Grid>}<div className="mt-6 grid gap-5 md:grid-cols-2"><Area name="skills" title="Skill set"/><Area name="achievements" title="Achievements"/></div></Panel>}
        {step===4&&<Panel title="Contact details" note="Email and phone are required and checked for duplicate staff records in this school."><Grid><Field name="email" title="Email address" type="email" required/><Field name="phone" title="Phone number" type="tel" placeholder="09096735531" maxLength={11} required/><Field name="website" title="Website (optional)" placeholder="salisu.dev"/></Grid>
          <div className="mt-7 border-t border-slate-200 pt-6"><div className="flex flex-wrap items-center justify-between gap-3"><div><h4 className="text-sm font-black text-slate-900">Social profiles</h4><p className="mt-1 text-xs text-slate-500">Add the platforms this staff member uses. Leave the section blank if none apply.</p></div><button type="button" onClick={addSocialProfile} disabled={socialProfiles.length>=10} className="tenant-primary-text rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold transition hover:bg-slate-50 disabled:opacity-40">+ Add another profile</button></div>
            <div className="mt-4 space-y-3">{socialProfiles.map((row,index)=><div key={row.key} className="grid items-end gap-3 rounded-xl border border-slate-200 bg-slate-50/60 p-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)_auto]"><StaffFormSelect name={`socialPlatform-${row.key}`} title={`Platform ${index+1}`} optional options={socialPlatforms.map(platform=>[platform,platform])} value={row.platform} onChange={value=>updateSocialProfile(row.key,"platform",value)}/><label className={label}>Profile link or handle<input type="text" value={row.handle} onChange={event=>updateSocialProfile(row.key,"handle",event.target.value)} maxLength={250} placeholder="@username or profile URL" className={input}/></label><button type="button" onClick={()=>removeSocialProfile(row.key)} aria-label={`Remove social profile ${index+1}`} className="h-10 rounded-lg px-3 text-xs font-bold text-red-700 transition hover:bg-red-50">Remove</button></div>)}</div>
          </div><div className="mt-7 border-t border-slate-200 pt-6"><label className="flex items-center gap-3 text-sm font-bold text-slate-900"><input type="checkbox" checked={addNextOfKin} onChange={event=>setAddNextOfKin(event.target.checked)} className="size-4"/> Add next of kin</label><p className="mt-1 text-xs text-slate-500">An emergency contact for this staff member. You can add more contacts from their staff profile later.</p>{addNextOfKin&&<div className="mt-5"><Grid><Field name="kinFullName" title="Full name" required/><Select name="kinRelationship" title="Relationship" value={kinRelationship} onChange={setKinRelationship} options={staffKinRelationships.map(relationship=>[relationship,relationship])}/>{kinRelationship==="Other"&&<Field name="kinOtherRelationship" title="Specify relationship" required/>}<Field name="kinPhone" title="Phone number" type="tel" placeholder="09096735531" maxLength={11} required/><Field name="kinEmail" title="Email address (optional)" type="email"/><Area name="kinAddress" title="Address"/></Grid></div>}</div><div className="mt-6"><Area name="notes" title="Internal notes" wide/></div></Panel>}
        {step===5&&<Panel title="Photograph, signature and evidence" note="JPG, JPEG, PNG and PDF files use the secured document-storage workflow."><div className="grid gap-5 md:grid-cols-3"><FileField name="photo" title="Staff photograph"/><FileField name="signature" title="Staff signature"/><FileField name="qualificationDocument" title="Qualification document" document/></div>{showDocumentNotice&&<div role="status" className="mt-6 flex items-start justify-between gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-xs leading-5 text-amber-900"><span>Review all tabs before creating the record. If a document upload fails, the staff profile remains available so the file can be added again from the staff detail page.</span><button type="button" onClick={() => setShowDocumentNotice(false)} aria-label="Dismiss document notice" className="grid size-7 shrink-0 place-items-center rounded-lg text-base text-amber-900 hover:bg-amber-100">×</button></div>}</Panel>}
        {error&&<div role="alert" className="mt-5 flex items-start justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-800"><span>{error}</span><button type="button" onClick={() => setError("")} aria-label="Dismiss error" className="grid size-6 shrink-0 place-items-center rounded-md text-red-700 hover:bg-red-100">×</button></div>}
      </div><footer className="flex items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:px-7"><button type="button" disabled={step===0||busy} onClick={event=>changeStep(step-1,event.currentTarget.form)} className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-black disabled:opacity-40">Previous</button><div className="flex gap-2"><button type="button" onClick={()=>setOpen(false)} className="rounded-xl border border-red-200 px-4 py-2.5 text-sm font-black text-red-700 hover:bg-red-50">Cancel</button>{step<steps.length-1?<button type="button" onClick={event=>continueRegistration(event.currentTarget.form)} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white">Continue</button>:<button type="button" disabled={busy} onClick={event=>{submitRequested.current=true;event.currentTarget.form?.requestSubmit()}} className="tenant-primary-bg rounded-xl px-5 py-2.5 text-sm font-black text-white disabled:opacity-50">{busy?"Creating staff record…":"Create staff record"}</button>}</div></footer></form>
    </div></div>}
  </div></DraftContext.Provider>;
}

function Panel({title,note,children}:{title:string;note:string;children:React.ReactNode}){return <section><h3 className="text-lg font-black text-slate-950">{title}</h3><p className="mb-6 mt-1 text-sm text-slate-500">{note}</p>{children}</section>}
function Grid({children}:{children:React.ReactNode}){return <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">{children}</div>}
function Field({name,title,type="text",required=false,placeholder,value,readOnly,maxLength}:{name:string;title:string;type?:string;required?:boolean;placeholder?:string;value?:string;readOnly?:boolean;maxLength?:number}){const {values,setField}=useContext(DraftContext);return <label className={label}>{title}{required&&<span className="text-red-600"> *</span>}<input name={name} type={type} required={required} placeholder={placeholder} value={String(values[name]??value??"")} readOnly={readOnly} maxLength={maxLength} inputMode={type==="tel"?"numeric":undefined} onChange={event=>setField(name,type==="tel"?event.target.value.replace(/\D/g,"").slice(0,11):event.target.value)} className={`${input} ${readOnly?"bg-slate-100 text-slate-500":""}`}/></label>}
function Select({name,title,options,optional=false,value,onChange,disabled=false,note}:{name:string;title:string;options:string[][];optional?:boolean;value?:string;onChange?:(v:string)=>void;disabled?:boolean;note?:string}){const {values,setField}=useContext(DraftContext);return <StaffFormSelect name={name} title={title} options={options} optional={optional} value={value??String(values[name]??"")} onChange={next=>{setField(name,next);onChange?.(next)}} disabled={disabled} note={note}/>}
function Area({name,title,wide=false}:{name:string;title:string;wide?:boolean}){const {values,setField}=useContext(DraftContext);return <label className={`${label} ${wide?"md:col-span-2 xl:col-span-3":""}`}>{title}<textarea name={name} rows={3} value={String(values[name]??"")} onChange={event=>setField(name,event.target.value)} className={input}/></label>}
/* eslint-disable @next/next/no-img-element -- FileReader previews are local files, not network images. */
function FileField({name,title,document=false}:{name:string;title:string;document?:boolean}){
  const {values,setField}=useContext(DraftContext);
  const selected=values[name];
  const file=typeof File!=="undefined"&&selected instanceof File?selected:null;
  const [preview,setPreview]=useState<{file:File;url:string}|null>(null);
  const isImage=Boolean(file&&(file.type.startsWith("image/")||/\.(jpe?g|png)$/i.test(file.name)));
  useEffect(()=>{
    if(!file||!(file.type.startsWith("image/")||/\.(jpe?g|png)$/i.test(file.name)))return;
    const reader=new FileReader();
    reader.onload=()=>{if(typeof reader.result==="string")setPreview({file,url:reader.result});};
    reader.readAsDataURL(file);
    return()=>{reader.abort();};
  },[file]);
  return <label className="group block cursor-pointer rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-5 text-sm font-black text-slate-800 transition hover:border-[var(--tenant-primary,#28654a)] hover:bg-white focus-within:border-[var(--tenant-primary,#28654a)] focus-within:ring-2 focus-within:ring-[var(--tenant-primary,#28654a)]">
    <span className="block cursor-pointer">{title}</span>
    <span className="mt-1 block cursor-pointer text-xs font-normal text-slate-500">{document?"PDF, JPG, JPEG or PNG":"JPG, JPEG or PNG"}, maximum 10 MB</span>
    <input name={name} type="file" accept={document?"application/pdf,.jpg,.jpeg,.png,image/jpeg,image/png":".jpg,.jpeg,.png,image/jpeg,image/png"} onChange={event=>{const nextFile=event.target.files?.[0];if(nextFile)setField(name,nextFile)}} className="sr-only"/>
    <span className="mt-4 inline-flex cursor-pointer items-center rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-bold text-slate-800 group-hover:border-[var(--tenant-primary,#28654a)]">Select file</span>
    {file&&<span className="mt-2 block cursor-pointer truncate text-xs font-normal text-slate-600">Selected: {file.name}</span>}
    {isImage&&preview?.file===file&&<span className="mt-3 block cursor-pointer overflow-hidden rounded-xl border border-slate-200 bg-white p-2"><img src={preview.url} alt={`${title} preview`} className="h-32 w-full object-contain"/></span>}
    {file&&(file.type==="application/pdf"||/\.pdf$/i.test(file.name))&&<span className="mt-3 block cursor-pointer rounded-lg bg-white px-3 py-2 text-xs font-normal text-slate-600">PDF selected: {file.name}</span>}
  </label>;
}
/* eslint-enable @next/next/no-img-element */
function numberOrNull(value:FormDataEntryValue|null){const text=String(value??"");return text?Number(text):null}
function normalizeWebsite(value:string):string|null{const trimmed=value.trim();if(!trimmed)return null;if(/\s/.test(trimmed))throw new Error("Website cannot contain spaces.");if(/^[a-z][a-z\d+.-]*:\/\//i.test(trimmed)&&!/^https?:\/\//i.test(trimmed))throw new Error("Website must use http or https.");let url:URL;try{url=new URL(/^https?:\/\//i.test(trimmed)?trimmed:`https://${trimmed}`)}catch{throw new Error("Enter a valid website, such as salisu.dev.")}if(!["http:","https:"].includes(url.protocol)||!url.hostname.includes(".")||url.username||url.password||url.toString().length>500)throw new Error("Enter a valid website, such as salisu.dev.");return url.toString()}
async function problem(response:Response,fallback:string){try{const body=await response.json() as {detail?:string;message?:string;title?:string};return body.detail??body.message??body.title??fallback}catch{return fallback}}
