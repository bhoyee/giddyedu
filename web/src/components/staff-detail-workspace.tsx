"use client";

/* eslint-disable @next/next/no-img-element -- Authorised document URLs are short-lived and cannot use the Next image optimizer. */

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";
import { StaffAssignmentsPanel } from "@/components/staff-assignments-panel";
import { StaffRecordSections } from "@/components/staff-record-sections";

type Staff = { id:string; userId:string|null; staffNumber:string; firstName:string; lastName:string; middleInitial:string|null; category:number; status:number; campusId:string; departmentId:string|null; positionId:string|null; positionName:string|null; workEmail:string|null; phone:string|null; hireDate:string; createdAtUtc:string };
type Sensitive = { title?:string|null; middleName?:string|null; gender?:string|null; dateOfBirth?:string|null; maritalStatus?:string|null; religion?:string|null; country?:string|null; state?:string|null; localGovernment?:string|null; city?:string|null; address?:string|null; genotype?:string|null; bloodGroup?:string|null; weightKg?:number|null; heightCm?:number|null; disability?:string|null; skills?:string|null; achievements?:string|null; website?:string|null; notes?:string|null };
type Document = { id:string; fileName:string; category:string; status:string|number; createdAtUtc?:string };
type Qualification = { id:string; name:string; institution:string; fieldOfStudy:string|null; awardedOn:string; grade:string|null };
type Position = { id:string; name:string };
type Tab = "overview"|"position"|"assignments"|"history"|"documents";
const tabs:{id:Tab;label:string}[]=[{id:"overview",label:"Overview"},{id:"position",label:"Role & position"},{id:"assignments",label:"Classes & subjects"},{id:"history",label:"History & contacts"},{id:"documents",label:"Documents"}];
const categories=["Teaching","Administrative","Non-teaching"];
const statuses=["Active","Suspended","Exited","Away","Not cleared","Inactive","On leave","Retired","Resigned","Sacked","Left","Deceased"];
const inputClass="mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none focus:border-[var(--tenant-primary,#28654a)] focus:ring-2 focus:ring-[var(--tenant-primary-soft,#e7f2eb)]";

export function StaffDetailWorkspace({staffId}:{staffId:string}){
  const [staff,setStaff]=useState<Staff|null>(null);
  const [sensitive,setSensitive]=useState<Sensitive|null>(null);
  const [documents,setDocuments]=useState<Document[]>([]);
  const [qualifications,setQualifications]=useState<Qualification[]>([]);
  const [positionName,setPositionName]=useState<string|null>(null);
  const [tab,setTab]=useState<Tab>("overview");
  const [photoUrl,setPhotoUrl]=useState<string|null>(null);
  const [signatureUrl,setSignatureUrl]=useState<string|null>(null);
  const [canManage,setCanManage]=useState(false);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");
  const [uploading,setUploading]=useState(false);
  const [revision,setRevision]=useState(0);

  const load=useCallback(async()=>{
    const [profileResponse,sensitiveResponse,documentsResponse,qualificationsResponse,positionsResponse]=await Promise.all([
      fetch(`/api/backend/hr/staff/${staffId}`,{cache:"no-store"}),
      fetch(`/api/backend/hr/staff/${staffId}/sensitive`,{cache:"no-store"}),
      fetch(`/api/backend/documents/StaffProfile/${staffId}`,{cache:"no-store"}),
      fetch(`/api/backend/hr/staff/${staffId}/qualifications`,{cache:"no-store"}),
      fetch("/api/backend/hr/positions",{cache:"no-store"})
    ]);
    if(!profileResponse.ok)throw new Error(profileResponse.status===403?"You do not have access to this staff record.":"The staff record could not be loaded.");
    const profile=await profileResponse.json() as Staff;
    setStaff(profile);
    const positions=positionsResponse.ok?await positionsResponse.json() as Position[]:[];
    setPositionName(profile.positionName||positions.find(item=>item.id===profile.positionId)?.name||null);
    setQualifications(qualificationsResponse.ok?await qualificationsResponse.json() as Qualification[]:[]);
    setSensitive(sensitiveResponse.ok?await sensitiveResponse.json() as Sensitive:null);
    const files=documentsResponse.ok?await documentsResponse.json() as Document[]:[];
    setDocuments(files);
    const available=(category:string)=>files.find(item=>item.category?.toLowerCase()===category&&(item.status===1||String(item.status).toLowerCase()==="available"));
    const fileUrl=async(category:string)=>{const file=available(category);if(!file)return null;const response=await fetch(`/api/backend/documents/${file.id}/download`);return response.ok?(await response.json() as {url:string}).url:null;};
    const [photo,signature]=await Promise.all([fileUrl("photo"),fileUrl("signature")]);
    setPhotoUrl(photo);setSignatureUrl(signature);
  },[staffId]);

  useEffect(()=>{
    let active=true;
    void fetch("/api/auth/session",{cache:"no-store"}).then(async response=>{
      if(!response.ok||!active)return;
      const session=await response.json() as {access?:{permissions?:string[]}};
      if(active)setCanManage(session.access?.permissions?.includes("Staff.Manage")===true);
    }).catch(()=>{});
    void Promise.resolve().then(load).catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"The staff record could not be loaded.");}).finally(()=>{if(active)setLoading(false);});
    return()=>{active=false;};
  },[load,revision]);

  useEffect(()=>{
    const syncHash=()=>{
      const hash=window.location.hash.slice(1);
      const next:Record<string,Tab>={overview:"overview",profile:"overview","staff-profile":"overview",position:"position","staff-position":"position",assignments:"assignments","staff-assignments":"assignments",history:"history","staff-history":"history",documents:"documents","staff-documents":"documents"};
      if(next[hash])setTab(next[hash]);
    };
    void Promise.resolve().then(syncHash);
    window.addEventListener("hashchange",syncHash);
    return()=>window.removeEventListener("hashchange",syncHash);
  },[]);

  async function upload(event:FormEvent<HTMLFormElement>){
    event.preventDefault();if(!canManage)return;
    const form=event.currentTarget;const values=new FormData(form);const file=values.get("file");
    if(!(file instanceof File)||!file.size||file.size>10*1024*1024||!["application/pdf","image/jpeg","image/png"].includes(file.type)){notify({tone:"error",title:"Choose a valid document",message:"PDF, JPEG or PNG files up to 10 MB are supported."});return;}
    setUploading(true);
    try{
      const begin=await fetch(`/api/backend/documents/StaffProfile/${staffId}/uploads`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({fileName:file.name,contentType:file.type,sizeBytes:file.size,category:values.get("category")})});
      if(!begin.ok)throw new Error(await problem(begin,"The upload could not be started."));
      const pending=await begin.json() as {fileId:string};
      const stored=await fetch(`/api/backend/documents/${pending.fileId}/content`,{method:"PUT",headers:{"Content-Type":file.type},body:file});
      if(!stored.ok)throw new Error(await problem(stored,"The file could not be stored."));
      const digest=await crypto.subtle.digest("SHA-256",await file.arrayBuffer());
      const checksum=Array.from(new Uint8Array(digest),byte=>byte.toString(16).padStart(2,"0")).join("");
      const completed=await fetch(`/api/backend/documents/${pending.fileId}/complete`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({checksum})});
      if(!completed.ok)throw new Error(await problem(completed,"The upload could not be verified."));
      form.reset();setRevision(value=>value+1);notify({title:"Document uploaded",message:`${file.name} is attached to this staff record.`});
    }catch(reason){notify({tone:"error",title:"Document not uploaded",message:reason instanceof Error?reason.message:"Please try again."});}
    finally{setUploading(false);}
  }

  async function download(fileId:string){
    const response=await fetch(`/api/backend/documents/${fileId}/download`);
    if(!response.ok){notify({tone:"error",title:"Download unavailable",message:"Check your document access and try again."});return;}
    const result=await response.json() as {url:string};window.open(result.url,"_blank","noopener,noreferrer");
  }
  async function remove(fileId:string){
    if(!canManage||!window.confirm("Remove this document from the staff record?"))return;
    const response=await fetch(`/api/backend/documents/${fileId}`,{method:"DELETE"});
    if(response.ok){setRevision(value=>value+1);notify({title:"Document removed",message:"The attachment was removed from this staff record."});}
    else notify({tone:"error",title:"Document not removed",message:"Check your permissions and try again."});
  }

  function selectTab(next:Tab){setTab(next);window.history.replaceState(null,"",`#${next}`);}

  if(loading)return <div className="w-full py-6"><div className="h-44 animate-pulse rounded-[1.75rem] bg-slate-200"/><div className="mt-6 h-72 animate-pulse rounded-[1.75rem] bg-slate-100"/></div>;
  if(!staff)return <div className="w-full py-6"><Link href="/portal/staff" className="text-sm font-bold tenant-primary-text">← Staff directory</Link><p role="alert" className="mt-6 rounded-2xl border border-red-200 bg-red-50 p-5 text-red-800">{error||"Staff record not found."}</p></div>;

  const name=[sensitive?.title,staff.firstName,sensitive?.middleName,staff.lastName].filter(Boolean).join(" ");
  const initials=`${staff.firstName[0]??""}${staff.lastName[0]??""}`.toUpperCase();
  const qualification=qualifications[0];
  const availableDocuments=documents.filter(item=>item.status===1||String(item.status).toLowerCase()==="available").length;
  return <main className="w-full space-y-6 pb-8">
    <div className="flex flex-wrap items-center justify-between gap-3"><Link href="/portal/staff" className="inline-flex items-center gap-2 text-sm font-bold text-slate-600 transition hover:text-slate-950">← Staff directory</Link><span className="text-xs font-medium text-slate-500">Staff / {staff.staffNumber}</span></div>
    <section aria-label="Staff summary" className="rounded-[1.5rem] border border-slate-200 bg-white px-5 py-6 shadow-[0_12px_36px_rgba(20,45,34,.06)] sm:px-7 sm:py-7">
      <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 items-center gap-5">
          <div className="relative grid size-20 shrink-0 place-items-center overflow-hidden rounded-full border-4 border-[var(--tenant-primary-soft,#e7f2eb)] bg-slate-100 text-2xl font-black text-[var(--tenant-primary,#28654a)] sm:size-24 sm:text-3xl" aria-hidden="true">{photoUrl?<img src={photoUrl} alt="" className="size-full object-cover"/>:initials}</div>
          <div className="min-w-0"><p className="text-[10px] font-black uppercase tracking-[.2em] text-slate-500">Staff profile · {categories[staff.category]||"Team member"}</p><h1 className="mt-1 break-words text-2xl font-black tracking-[-.03em] text-slate-950 sm:text-3xl">{name}</h1><span className="mt-2 inline-flex rounded-full border border-amber-300 bg-amber-100 px-3 py-1 text-xs font-bold text-amber-900">{positionName||"Position not assigned"}</span><div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-slate-600"><span className="rounded-full bg-[var(--tenant-primary-soft,#e7f2eb)] px-3 py-1.5 font-bold text-[var(--tenant-primary,#28654a)]">ID: {staff.staffNumber}</span><span><span className="text-slate-400">Qualification</span> <strong className="text-slate-800">{qualification?.name||"Not added"}</strong></span><span><span className="text-slate-400">Salary</span> <strong className="text-slate-800">₦ —</strong> <span className="text-slate-400">Payroll coming later</span></span></div></div>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2"><span className="inline-flex items-center gap-2 rounded-full border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700"><span className={`size-2 rounded-full ${staff.status===0?"bg-emerald-500":"bg-amber-500"}`}/>{statuses[staff.status]||"Unknown status"}</span>{canManage&&<Link href={`/portal/staff/${staffId}/edit`} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-xs font-bold text-white transition hover:brightness-110">Edit staff record</Link>}</div>
      </div>
    </section>
    <div className="border-b border-slate-200"><div role="tablist" aria-label="Staff profile sections" className="flex gap-1 overflow-x-auto">{tabs.map((item,index)=><button key={item.id} id={`tab-${item.id}`} type="button" role="tab" tabIndex={tab===item.id?0:-1} aria-selected={tab===item.id} aria-controls={`panel-${item.id}`} onClick={()=>selectTab(item.id)} onKeyDown={event=>{if(event.key!=="ArrowLeft"&&event.key!=="ArrowRight")return;event.preventDefault();const next=tabs[(index+(event.key==="ArrowRight"?1:-1)+tabs.length)%tabs.length];selectTab(next.id);document.getElementById(`tab-${next.id}`)?.focus();}} className={`shrink-0 border-b-2 px-4 py-3 text-sm font-bold transition ${tab===item.id?"border-[var(--tenant-primary,#28654a)] text-[var(--tenant-primary,#28654a)]":"border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-900"}`}>{item.label}</button>)}</div></div>
    {tab==="overview"&&<div id="panel-overview" role="tabpanel" aria-labelledby="tab-overview" className="space-y-6">
    <div className="grid gap-3 sm:grid-cols-3"><SummaryCard label="Employment" value={statuses[staff.status]||"Unknown"} detail={`Joined ${date(staff.hireDate)||"date not set"}`}/><SummaryCard label="Workspace account" value={staff.userId?"Connected":"Not connected"} detail={staff.userId?"User account linked":"Invitation or setup pending"}/><SummaryCard label="Documents" value={String(availableDocuments)} detail={availableDocuments===1?"Available file":"Available files"}/></div>
    <section id="staff-profile" className="scroll-mt-8 rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7">
      <SectionHeading eyebrow="Overview" title="Profile and contact" description="Core employment and contact information for this staff member."/>
      <div className="mt-6 grid gap-x-7 gap-y-1 sm:grid-cols-2 lg:grid-cols-3"><Detail label="Staff number" value={staff.staffNumber}/><Detail label="Category" value={categories[staff.category]}/><Detail label="Status" value={statuses[staff.status]}/><Detail label="Primary position" value={positionName}/><Detail label="Email address" value={staff.workEmail} href={staff.workEmail?`mailto:${staff.workEmail}`:undefined}/><Detail label="Phone number" value={staff.phone} href={staff.phone?`tel:${staff.phone}`:undefined}/><Detail label="Joined school" value={date(staff.hireDate)}/><Detail label="Record created" value={date(staff.createdAtUtc)}/><Detail label="Account" value={staff.userId?"Linked to a user account":"Invitation or account link pending"}/></div>
    </section>
    {sensitive&&<section className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><SectionHeading eyebrow="Restricted" title="Personal information" description="Visible only to users authorised to view sensitive staff data."/><div className="mt-6 grid gap-x-7 gap-y-1 sm:grid-cols-2 lg:grid-cols-3"><Detail label="Date of birth" value={date(sensitive.dateOfBirth)}/><Detail label="Gender" value={sensitive.gender}/><Detail label="Marital status" value={sensitive.maritalStatus}/><Detail label="Religion" value={sensitive.religion}/><Detail label="State" value={sensitive.state}/><Detail label="Local government" value={sensitive.localGovernment}/><Detail label="City / town" value={sensitive.city}/><Detail label="Residential address" value={sensitive.address}/></div><div className="mt-6 grid gap-6 border-t border-slate-100 pt-6 lg:grid-cols-2"><div><h3 className="text-sm font-bold text-slate-900">Health and accessibility</h3><div className="mt-3 grid grid-cols-2 gap-x-4"><Detail label="Blood group" value={sensitive.bloodGroup}/><Detail label="Genotype" value={sensitive.genotype}/><Detail label="Height" value={sensitive.heightCm?`${sensitive.heightCm} cm`:null}/><Detail label="Weight" value={sensitive.weightKg?`${sensitive.weightKg} kg`:null}/></div><Detail label="Accessibility needs" value={sensitive.disability}/></div><div><h3 className="text-sm font-bold text-slate-900">Professional notes</h3><div className="mt-3"><Detail label="Skills" value={sensitive.skills}/><Detail label="Achievements" value={sensitive.achievements}/><Detail label="Internal notes" value={sensitive.notes}/></div></div></div></section>}
    {signatureUrl&&<section className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><SectionHeading eyebrow="Verified asset" title="Staff signature" description="Signature stored with this staff record."/><div className="mt-5 flex h-32 max-w-md items-center rounded-xl border border-slate-100 bg-slate-50 p-3"><img src={signatureUrl} alt={`${name}'s signature`} className="max-h-full max-w-full object-contain"/></div></section>}
    </div>}
    {tab==="position"&&<div id="panel-position" role="tabpanel" aria-labelledby="tab-position"><StaffAssignmentsPanel key={`${revision}-position`} staffId={staffId} view="position" onChanged={()=>setRevision(value=>value+1)}/></div>}
    {tab==="assignments"&&<div id="panel-assignments" role="tabpanel" aria-labelledby="tab-assignments"><StaffAssignmentsPanel key={`${revision}-assignments`} staffId={staffId} view="assignments"/></div>}
    {tab==="history"&&<div id="panel-history" role="tabpanel" aria-labelledby="tab-history"><StaffRecordSections staffId={staffId} onChanged={()=>setRevision(value=>value+1)}/></div>}
    {tab==="documents"&&<div id="panel-documents" role="tabpanel" aria-labelledby="tab-documents">
    <section id="staff-documents" className="scroll-mt-8 rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><SectionHeading eyebrow="Files" title="Documents and evidence" description="Access and manage files attached to this staff profile."/>
      {canManage&&<form onSubmit={upload} className="mt-6 grid gap-3 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] sm:items-end"><label className="text-xs font-bold text-slate-700">Document type<select name="category" className={inputClass}>{["identity","qualification","contract","photo","signature","other"].map(value=><option key={value} value={value}>{value[0].toUpperCase()+value.slice(1)}</option>)}</select></label><label className="text-xs font-bold text-slate-700">Choose a file · PDF, JPG or PNG<input name="file" type="file" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" required className={`${inputClass} cursor-pointer file:cursor-pointer file:border-0 file:bg-transparent file:font-bold`}/></label><button disabled={uploading} className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{uploading?"Uploading…":"Upload file"}</button></form>}
      {documents.length?<ul className="mt-5 divide-y divide-slate-100">{documents.map(item=>{const ready=item.status===1||String(item.status).toLowerCase()==="available";return <li key={item.id} className="flex flex-wrap items-center justify-between gap-3 py-4"><div className="flex min-w-0 items-center gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-slate-100 text-xs font-black text-slate-600">FILE</span><div className="min-w-0"><p className="truncate text-sm font-bold text-slate-900">{item.fileName}</p><p className="mt-0.5 text-xs text-slate-500">{item.category} · {ready?"Available":"Upload incomplete — please upload this file again"}</p></div></div><div className="flex gap-2">{ready&&<button type="button" onClick={()=>void download(item.id)} className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50">Download</button>}{canManage&&<button type="button" onClick={()=>void remove(item.id)} className="rounded-lg border border-red-200 px-3 py-2 text-xs font-bold text-red-700 hover:bg-red-50">Remove</button>}</div></li>;})}</ul>:<p className="mt-5 rounded-xl bg-slate-50 px-4 py-5 text-sm text-slate-500">No documents are available for this staff record yet.</p>}
    </section>
    </div>}
  </main>;
}

function SummaryCard({label,value,detail}:{label:string;value:string;detail:string}){return <div className="rounded-2xl border border-slate-200 bg-white px-5 py-4 shadow-sm"><p className="text-[10px] font-black uppercase tracking-[.15em] text-slate-500">{label}</p><p className="mt-2 text-lg font-black text-slate-950">{value}</p><p className="mt-0.5 text-xs text-slate-500">{detail}</p></div>}
function SectionHeading({eyebrow,title,description}:{eyebrow:string;title:string;description:string}){return <div><p className="text-[10px] font-black uppercase tracking-[.18em] tenant-primary-text">{eyebrow}</p><h2 className="mt-1 text-xl font-black tracking-tight text-slate-950 sm:text-2xl">{title}</h2><p className="mt-1 text-sm text-slate-500">{description}</p></div>}
function Detail({label,value,href}:{label:string;value:string|number|null|undefined;href?:string}){const text=value===null||value===undefined||value===""?"Not provided":String(value);return <div className="border-b border-slate-100 py-3"><p className="text-[11px] font-bold uppercase tracking-wide text-slate-500">{label}</p><p className={`mt-1 break-words text-sm font-semibold ${text==="Not provided"?"font-normal text-slate-400":"text-slate-900"}`}>{href&&text!=="Not provided"?<a href={href} className="tenant-primary-text hover:underline">{text}</a>:text}</p></div>}
function date(value:string|null|undefined){if(!value)return null;const parsed=new Date(value);return Number.isNaN(parsed.getTime())?value:parsed.toLocaleDateString("en-NG",{day:"numeric",month:"short",year:"numeric"});}
async function problem(response:Response,fallback:string){try{const body=await response.json() as {detail?:string;message?:string;title?:string};return body.detail??body.message??body.title??fallback;}catch{return fallback;}}
