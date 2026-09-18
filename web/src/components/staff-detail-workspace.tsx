"use client";

/* eslint-disable @next/next/no-img-element -- Authorised document URLs are short-lived and cannot use the Next image optimizer. */

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";
import { confirmAction } from "@/components/confirm-action";
import { StaffAssignmentsPanel } from "@/components/staff-assignments-panel";
import { StaffRecordSections } from "@/components/staff-record-sections";

type Staff = { id:string; userId:string|null; staffNumber:string; firstName:string; lastName:string; middleInitial:string|null; category:number; status:number; campusId:string; departmentId:string|null; positionId:string|null; positionName:string|null; workEmail:string|null; phone:string|null; hireDate:string; createdAtUtc:string };
type Sensitive = { title?:string|null; middleName?:string|null; gender?:string|null; dateOfBirth?:string|null; maritalStatus?:string|null; religion?:string|null; country?:string|null; state?:string|null; localGovernment?:string|null; city?:string|null; address?:string|null; genotype?:string|null; bloodGroup?:string|null; weightKg?:number|null; heightCm?:number|null; disability?:string|null; skills?:string|null; achievements?:string|null; website?:string|null; notes?:string|null };
type Document = { id:string; fileName:string; category:string; status:string|number; createdAtUtc?:string };
type Qualification = { id:string; name:string; institution:string; fieldOfStudy:string|null; awardedOn:string; grade:string|null };
type Position = { id:string; name:string };
type StaffPosition = { positionId:string; name:string; isPrimary:boolean };
type Tab = "overview"|"position"|"assignments"|"attendance"|"timetable"|"salary"|"leaves"|"activity"|"history"|"documents";
const tabs:{id:Tab;label:string}[]=[{id:"overview",label:"Overview"},{id:"position",label:"Role & position"},{id:"assignments",label:"Assignments"},{id:"attendance",label:"Attendance"},{id:"timetable",label:"Timetable"},{id:"salary",label:"Salary"},{id:"leaves",label:"Leaves"},{id:"activity",label:"Activity"},{id:"history",label:"History & contacts"},{id:"documents",label:"Documents"}];
const plannedTabs:{id:Tab;title:string;description:string}[]=[
  {id:"attendance",title:"Attendance",description:"Staff attendance records will appear here when the attendance workflow is available."},
  {id:"timetable",title:"Timetable",description:"This view will show the staff member's scheduled classes and duties when timetable management is available."},
  {id:"salary",title:"Salary",description:"Pay information will appear here when payroll is available. No salary figure is recorded in this profile yet."},
  {id:"leaves",title:"Leaves",description:"Leave balances and requests will appear here when leave management is available."},
  {id:"activity",title:"Activity",description:"Staff activity history will appear here when this view is connected to the audit records."},
];
const categories=["Teaching","Administrative","Non-teaching"];
const statuses=["Active","Suspended","Exited","Away","Not cleared","Inactive","On leave","Retired","Resigned","Sacked","Left","Deceased"];
const inputClass="mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none focus:border-[var(--tenant-primary,#28654a)] focus:ring-2 focus:ring-[var(--tenant-primary-soft,#e7f2eb)]";
type ProfileIconKind="id"|"cap"|"calendar"|"mail"|"phone"|"briefcase"|"shield"|"person"|"file"|"place"|"heart"|"measure"|"skills"|"note"|"wallet"|"clock";
const detailIcons:Record<string,ProfileIconKind>={
  "Staff number":"id",Category:"briefcase",Status:"shield","Primary position":"briefcase","Email address":"mail","Phone number":"phone","Joined school":"calendar","Record created":"clock",Account:"person",
  "Date of birth":"calendar",Gender:"person","Marital status":"heart",Religion:"person",State:"place","Local government":"place","City / town":"place","Residential address":"place",
  "Blood group":"heart",Genotype:"heart",Height:"measure",Weight:"measure","Accessibility needs":"heart",Skills:"skills",Achievements:"cap","Internal notes":"note"
};
const iconPaths:Record<ProfileIconKind,string>={
  id:"M3 5h18v14H3zM7 9h4M7 13h5M16 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4Z",
  cap:"m2 9 10-5 10 5-10 5L2 9Zm4 3v5c3 2 9 2 12 0v-5M22 9v6",
  calendar:"M7 3v4M17 3v4M4 9h16M5 5h14a1 1 0 0 1 1 1v13H4V6a1 1 0 0 1 1-1ZM8 13h3v3H8z",
  mail:"M3 6h18v12H3zM3 7l9 7 9-7",
  phone:"M7 3h10v18H7zM11 17h2",
  briefcase:"M3 8h18v12H3zM8 8V5h8v3M3 13h18M11 13v2h2v-2",
  shield:"m12 3 8 4v5c0 5-3 8-8 9-5-1-8-4-8-9V7l8-4Zm-3 9 2 2 4-4",
  person:"M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM4 21a8 8 0 0 1 16 0",
  file:"M6 3h8l4 4v14H6zM14 3v5h4M9 12h6M9 16h6",
  place:"M12 21s7-6 7-11a7 7 0 0 0-14 0c0 5 7 11 7 11Zm0-8a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z",
  heart:"M20.8 9c0 4.2-8.8 10-8.8 10S3.2 13.2 3.2 9a4.2 4.2 0 0 1 8.8-1 4.2 4.2 0 0 1 8.8 1Z",
  measure:"M4 5h16v14H4zM8 5v4M12 5v3M16 5v4",
  skills:"m12 3 2.5 5.5L20 11l-5.5 2.5L12 19l-2.5-5.5L4 11l5.5-2.5L12 3Z",
  note:"M5 3h14v18H5zM8 8h8M8 12h8M8 16h5",
  wallet:"M3 6h18v14H3zM3 10h18M15 14h4",
  clock:"M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm0 4v5l3 2"
};

export function StaffDetailWorkspace({staffId}:{staffId:string}){
  const [staff,setStaff]=useState<Staff|null>(null);
  const [sensitive,setSensitive]=useState<Sensitive|null>(null);
  const [documents,setDocuments]=useState<Document[]>([]);
  const [qualifications,setQualifications]=useState<Qualification[]>([]);
  const [positionName,setPositionName]=useState<string|null>(null);
  const [additionalPositions,setAdditionalPositions]=useState<StaffPosition[]>([]);
  const [tab,setTab]=useState<Tab>("overview");
  const [photoUrl,setPhotoUrl]=useState<string|null>(null);
  const [signatureUrl,setSignatureUrl]=useState<string|null>(null);
  const [canManage,setCanManage]=useState(false);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");
  const [uploading,setUploading]=useState(false);
  const [revision,setRevision]=useState(0);

  const load=useCallback(async()=>{
    const [profileResponse,sensitiveResponse,documentsResponse,qualificationsResponse,positionsResponse,staffPositionsResponse]=await Promise.all([
      fetch(`/api/backend/hr/staff/${staffId}`,{cache:"no-store"}),
      fetch(`/api/backend/hr/staff/${staffId}/sensitive`,{cache:"no-store"}),
      fetch(`/api/backend/documents/StaffProfile/${staffId}`,{cache:"no-store"}),
      fetch(`/api/backend/hr/staff/${staffId}/qualifications`,{cache:"no-store"}),
      fetch("/api/backend/hr/positions",{cache:"no-store"}),
      fetch(`/api/backend/hr/staff/${staffId}/positions`,{cache:"no-store"})
    ]);
    if(!profileResponse.ok)throw new Error(profileResponse.status===403?"You do not have access to this staff record.":"The staff record could not be loaded.");
    const profile=await profileResponse.json() as Staff;
    setStaff(profile);
    const positions=positionsResponse.ok?await positionsResponse.json() as Position[]:[];
    setPositionName(profile.positionName||positions.find(item=>item.id===profile.positionId)?.name||null);
    setAdditionalPositions(staffPositionsResponse.ok?(await staffPositionsResponse.json() as StaffPosition[]).filter(item=>!item.isPrimary):[]);
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
      const section=new URLSearchParams(window.location.search).get("section") ?? "";
      const next:Record<string,Tab>={overview:"overview",profile:"overview","staff-profile":"overview",position:"position","staff-position":"position",assignments:"assignments","staff-assignments":"assignments",attendance:"attendance",timetable:"timetable",salary:"salary",leaves:"leaves",activity:"activity",history:"history","staff-history":"history",documents:"documents","staff-documents":"documents"};
      if(next[hash] || next[section])setTab(next[hash] ?? next[section]);
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
    if(!canManage||!await confirmAction({title:"Delete staff document?",message:"This document will be permanently removed from the staff record. This cannot be undone.",confirmLabel:"Delete document",confirmText:"DELETE"}))return;
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
  return <main className="min-w-0 w-full space-y-4 overflow-x-hidden pb-8 sm:space-y-6">
    <div className="flex min-w-0 flex-wrap items-center justify-between gap-2"><Link href="/portal/staff" className="inline-flex items-center gap-2 text-sm font-bold text-slate-600 transition hover:text-slate-950">← Staff directory</Link><span className="hidden max-w-full break-all text-[11px] font-medium text-slate-500 sm:inline">Staff / {staff.staffNumber}</span></div>
    <section aria-label="Staff summary" className="min-w-0 rounded-2xl border border-slate-200 bg-white px-4 py-5 shadow-[0_12px_36px_rgba(20,45,34,.06)] sm:rounded-[1.5rem] sm:px-7 sm:py-7">
      <div className="flex min-w-0 flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 items-start gap-3 sm:items-center sm:gap-5">
          <div className="flex w-20 shrink-0 flex-col items-center gap-1 sm:w-24"><div className="relative grid size-16 place-items-center overflow-hidden rounded-full border-4 border-[var(--tenant-primary-soft,#e7f2eb)] bg-slate-100 text-xl font-black text-[var(--tenant-primary,#28654a)] sm:size-24 sm:text-3xl" aria-hidden="true">{photoUrl?<img src={photoUrl} alt="" className="size-full object-cover"/>:initials}</div><span className="max-w-full break-all text-center text-[10px] font-bold leading-tight text-slate-500 sm:hidden">ID: {staff.staffNumber}</span></div>
          <div className="min-w-0"><p className="text-[10px] font-black uppercase tracking-[.12em] text-slate-500 sm:tracking-[.2em]">Staff profile · {categories[staff.category]||"Team member"}</p><h1 className="mt-1 break-words text-xl font-black leading-tight tracking-[-.03em] text-slate-950 sm:text-3xl">{name}</h1><div className="mt-2 flex min-w-0 flex-wrap gap-1.5"><span className="inline-flex max-w-full break-words rounded-full border border-[#FFDE00] bg-[#FFDE00] px-2.5 py-1 text-[11px] font-bold text-black sm:px-3 sm:text-xs">{positionName||"Position not assigned"}</span>{additionalPositions.map(item=><span key={item.positionId} className="inline-flex max-w-full break-words rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-[10px] font-bold text-sky-800">{item.name}</span>)}</div><div className="mt-3 flex min-w-0 flex-col items-start gap-2 text-xs text-slate-600 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-4"><span className="hidden max-w-full items-center gap-1.5 break-all rounded-full bg-[var(--tenant-primary-soft,#e7f2eb)] px-3 py-1.5 font-bold text-[var(--tenant-primary,#28654a)] sm:inline-flex"><ProfileIcon kind="id"/>ID: {staff.staffNumber}</span><span className="inline-flex min-w-0 flex-wrap items-center gap-1.5"><ProfileIcon kind="cap"/><span className="text-slate-500">Qualification</span> <strong className="text-slate-800">{qualification?.name||"Not added"}</strong></span><span className="inline-flex items-center gap-1.5"><ProfileIcon kind="wallet"/><span className="text-slate-500">Salary</span> <strong className="text-slate-800">₦ —</strong> <span className="text-slate-400">Payroll coming later</span></span></div></div>
        </div>
        <div className="flex w-full flex-wrap items-center gap-2 border-t border-slate-100 pt-4 sm:w-auto sm:border-0 sm:pt-0"><span className="inline-flex items-center gap-2 rounded-full border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700"><span className={`size-2 rounded-full ${staff.status===0?"bg-emerald-500":"bg-amber-500"}`}/>{statuses[staff.status]||"Unknown status"}</span>{canManage&&<Link href={`/portal/staff/${staffId}/edit`} className="tenant-primary-bg inline-flex min-h-10 flex-1 items-center justify-center rounded-xl px-4 py-2.5 text-xs font-bold text-white transition hover:brightness-110 sm:flex-none">Edit staff record</Link>}</div>
      </div>
    </section>
    <nav aria-label="Staff profile sections" className="rounded-xl border border-slate-200 bg-white p-3 sm:hidden"><label htmlFor="staff-mobile-section" className="block text-xs font-bold text-slate-600">Profile section</label><select id="staff-mobile-section" value={tab} onChange={event=>selectTab(event.target.value as Tab)} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-3 text-sm font-bold text-slate-900">{tabs.map(item=><option key={item.id} value={item.id}>{item.label}</option>)}</select></nav>
    <div className="hidden min-w-0 border-b border-slate-200 sm:block"><div role="tablist" aria-label="Staff profile sections" className="flex gap-1 overflow-x-auto">{tabs.map((item,index)=><button key={item.id} id={`tab-${item.id}`} type="button" role="tab" tabIndex={tab===item.id?0:-1} aria-selected={tab===item.id} aria-controls={`panel-${item.id}`} onClick={()=>selectTab(item.id)} onKeyDown={event=>{if(event.key!=="ArrowLeft"&&event.key!=="ArrowRight")return;event.preventDefault();const next=tabs[(index+(event.key==="ArrowRight"?1:-1)+tabs.length)%tabs.length];selectTab(next.id);document.getElementById(`tab-${next.id}`)?.focus();}} className={`shrink-0 border-b-2 px-4 py-3 text-sm font-bold transition ${tab===item.id?"border-[var(--tenant-primary,#28654a)] text-[var(--tenant-primary,#28654a)]":"border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-900"}`}>{item.label}</button>)}</div></div>
    {tab==="overview"&&<div id="panel-overview" role="tabpanel" aria-labelledby="tab-overview" className="space-y-6">
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3"><SummaryCard label="Employment" value={statuses[staff.status]||"Unknown"} detail={`Joined ${date(staff.hireDate)||"date not set"}`}/><SummaryCard label="Workspace account" value={staff.userId?"Connected":"Not connected"} detail={staff.userId?"User account linked":"Invitation or setup pending"}/><div className="col-span-2 sm:col-span-1"><SummaryCard label="Documents" value={String(availableDocuments)} detail={availableDocuments===1?"Available file":"Available files"}/></div></div>
    <section id="staff-profile" className="min-w-0 scroll-mt-8 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:rounded-[1.5rem] sm:p-7">
      <SectionHeading eyebrow="Overview" title="Profile and contact" description="Core employment and contact information for this staff member."/>
      <div className="mt-4 grid min-w-0 gap-x-7 gap-y-1 sm:mt-6 sm:grid-cols-2 lg:grid-cols-3"><Detail label="Staff number" value={staff.staffNumber}/><Detail label="Category" value={categories[staff.category]}/><Detail label="Status" value={statuses[staff.status]}/><Detail label="Primary position" value={positionName}/><Detail label="Email address" value={staff.workEmail} href={staff.workEmail?`mailto:${staff.workEmail}`:undefined}/><Detail label="Phone number" value={staff.phone} href={staff.phone?`tel:${staff.phone}`:undefined}/><Detail label="Joined school" value={date(staff.hireDate)}/><Detail label="Record created" value={date(staff.createdAtUtc)}/><Detail label="Account" value={staff.userId?"Linked to a user account":"Invitation or account link pending"}/></div>
    </section>
    {sensitive&&<section className="min-w-0 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:rounded-[1.5rem] sm:p-7"><SectionHeading eyebrow="Restricted" title="Personal information" description="Visible only to users authorised to view sensitive staff data."/><div className="mt-6 grid gap-x-7 gap-y-1 sm:grid-cols-2 lg:grid-cols-3"><Detail label="Date of birth" value={date(sensitive.dateOfBirth)}/><Detail label="Gender" value={sensitive.gender}/><Detail label="Marital status" value={sensitive.maritalStatus}/><Detail label="Religion" value={sensitive.religion}/><Detail label="State" value={sensitive.state}/><Detail label="Local government" value={sensitive.localGovernment}/><Detail label="City / town" value={sensitive.city}/><Detail label="Residential address" value={sensitive.address}/></div><div className="mt-5 grid min-w-0 gap-5 border-t border-slate-100 pt-5 lg:grid-cols-2"><div><h3 className="text-sm font-bold text-slate-900">Health and accessibility</h3><div className="mt-3 grid grid-cols-1 gap-x-4 min-[380px]:grid-cols-2"><Detail label="Blood group" value={sensitive.bloodGroup}/><Detail label="Genotype" value={sensitive.genotype}/><Detail label="Height" value={sensitive.heightCm?`${sensitive.heightCm} cm`:null}/><Detail label="Weight" value={sensitive.weightKg?`${sensitive.weightKg} kg`:null}/></div><Detail label="Accessibility needs" value={sensitive.disability}/></div><div><h3 className="text-sm font-bold text-slate-900">Professional notes</h3><div className="mt-3"><Detail label="Skills" value={sensitive.skills}/><Detail label="Achievements" value={sensitive.achievements}/><Detail label="Internal notes" value={sensitive.notes}/></div></div></div></section>}
    {signatureUrl&&<section className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><SectionHeading eyebrow="Verified asset" title="Staff signature" description="Signature stored with this staff record."/><div className="mt-5 flex h-32 max-w-full items-center rounded-xl border border-slate-100 bg-slate-50 p-3 sm:max-w-md"><img src={signatureUrl} alt={`${name}'s signature`} className="max-h-full max-w-full object-contain"/></div></section>}
    </div>}
    {tab==="position"&&<div id="panel-position" role="tabpanel" aria-labelledby="tab-position"><StaffAssignmentsPanel key={`${revision}-position`} staffId={staffId} view="position" onChanged={()=>setRevision(value=>value+1)}/></div>}
    {tab==="assignments"&&<div id="panel-assignments" role="tabpanel" aria-labelledby="tab-assignments"><StaffAssignmentsPanel key={`${revision}-assignments`} staffId={staffId} view="assignments"/></div>}
    {plannedTabs.filter(item=>item.id===tab).map(item=><div key={item.id} id={`panel-${item.id}`} role="tabpanel" aria-labelledby={`tab-${item.id}`}><PlannedStaffPanel title={item.title} description={item.description}/></div>)}
    {tab==="history"&&<div id="panel-history" role="tabpanel" aria-labelledby="tab-history"><StaffRecordSections staffId={staffId} onChanged={()=>setRevision(value=>value+1)}/></div>}
    {tab==="documents"&&<div id="panel-documents" role="tabpanel" aria-labelledby="tab-documents">
    <section id="staff-documents" className="scroll-mt-8 rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><SectionHeading eyebrow="Files" title="Documents and evidence" description="Access and manage files attached to this staff profile."/>
      {canManage&&<form onSubmit={upload} className="mt-5 grid min-w-0 gap-3 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-3 sm:p-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] lg:items-end"><label className="text-xs font-bold text-slate-700">Document type<select name="category" className={inputClass}>{["identity","qualification","contract","photo","signature","other"].map(value=><option key={value} value={value}>{value[0].toUpperCase()+value.slice(1)}</option>)}</select></label><label className="text-xs font-bold text-slate-700">Choose a file · PDF, JPG or PNG<input name="file" type="file" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" required className={`${inputClass} min-w-0 cursor-pointer file:cursor-pointer file:border-0 file:bg-transparent file:font-bold`}/></label><button disabled={uploading} className="tenant-primary-bg min-h-11 rounded-xl px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50">{uploading?"Uploading…":"Upload file"}</button></form>}
      {documents.length?<ul className="mt-5 divide-y divide-slate-100">{documents.map(item=>{const ready=item.status===1||String(item.status).toLowerCase()==="available";return <li key={item.id} className="flex min-w-0 flex-col items-stretch gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3 sm:items-center"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-slate-100 text-xs font-black text-slate-600">FILE</span><div className="min-w-0"><p className="truncate text-sm font-bold text-slate-900">{item.fileName}</p><p className="mt-0.5 break-words text-[11px] text-slate-500 sm:text-xs">{item.category} · {ready?"Available":"Upload incomplete — please upload this file again"}</p></div></div><div className="flex w-full gap-2 sm:w-auto">{ready&&<button type="button" onClick={()=>void download(item.id)} className="min-h-10 flex-1 rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50 sm:flex-none">Download</button>}{canManage&&<button type="button" onClick={()=>void remove(item.id)} className="min-h-10 flex-1 rounded-lg border border-red-200 px-3 py-2 text-xs font-bold text-red-700 hover:bg-red-50 sm:flex-none">Remove</button>}</div></li>;})}</ul>:<p className="mt-5 rounded-xl bg-slate-50 px-4 py-5 text-sm text-slate-500">No documents are available for this staff record yet.</p>}
    </section>
    </div>}
  </main>;
}

function SummaryCard({label,value,detail}:{label:string;value:string;detail:string}){const kind:ProfileIconKind=label==="Employment"?"briefcase":label==="Documents"?"file":"person";return <div className="h-full min-w-0 rounded-2xl border border-slate-200 bg-white px-3 py-3 shadow-sm sm:px-5 sm:py-4"><div className="flex items-center gap-2.5"><span className="grid size-9 place-items-center rounded-xl bg-[var(--tenant-primary-soft,#e7f2eb)] text-[var(--tenant-primary,#28654a)]"><ProfileIcon kind={kind}/></span><p className="text-[10px] font-black uppercase tracking-[.15em] text-slate-500">{label}</p></div><p className="mt-2 break-words text-base font-black text-slate-950 sm:mt-3 sm:text-lg">{value}</p><p className="mt-0.5 text-xs text-slate-500">{detail}</p></div>}
function PlannedStaffPanel({title,description}:{title:string;description:string}){return <section className="rounded-[1.5rem] border border-slate-200 bg-white p-6 shadow-sm sm:p-8"><div className="flex flex-wrap items-center gap-3"><h2 className="text-xl font-black text-slate-950">{title}</h2><span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-[11px] font-bold text-slate-600">Coming later</span></div><p className="mt-3 max-w-2xl text-sm leading-6 text-slate-600">{description}</p></section>}
function SectionHeading({eyebrow,title,description}:{eyebrow:string;title:string;description:string}){return <div><p className="text-[10px] font-black uppercase tracking-[.18em] tenant-primary-text">{eyebrow}</p><h2 className="mt-1 text-xl font-black tracking-tight text-slate-950 sm:text-2xl">{title}</h2><p className="mt-1 text-sm text-slate-500">{description}</p></div>}
function Detail({label,value,href}:{label:string;value:string|number|null|undefined;href?:string}){const text=value===null||value===undefined||value===""?"Not provided":String(value);return <div className="flex min-w-0 gap-3 border-b border-slate-100 py-3.5"><span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl bg-slate-50 text-[var(--tenant-primary,#28654a)]"><ProfileIcon kind={detailIcons[label]??"note"}/></span><div className="min-w-0"><p className="text-[11px] font-bold uppercase tracking-wide text-slate-500">{label}</p><p className={`mt-1 break-words text-sm font-semibold ${text==="Not provided"?"font-normal text-slate-400":"text-slate-900"}`}>{href&&text!=="Not provided"?<a href={href} className="tenant-primary-text hover:underline">{text}</a>:text}</p></div></div>}
function ProfileIcon({kind}:{kind:ProfileIconKind}){return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" className="size-4" aria-hidden="true"><path d={iconPaths[kind]}/></svg>}
function date(value:string|null|undefined){if(!value)return null;const parsed=new Date(value);return Number.isNaN(parsed.getTime())?value:parsed.toLocaleDateString("en-NG",{day:"numeric",month:"short",year:"numeric"});}
async function problem(response:Response,fallback:string){try{const body=await response.json() as {detail?:string;message?:string;title?:string};return body.detail??body.message??body.title??fallback;}catch{return fallback;}}
