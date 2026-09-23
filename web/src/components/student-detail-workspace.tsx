"use client";

/* eslint-disable @next/next/no-img-element -- Student photos use short-lived authorised storage URLs. */

import Link from "next/link";
import type { ReactNode } from "react";
import { useCallback, useEffect, useState } from "react";
import { notify } from "@/components/app-toast";

type Student = { id:string; admissionNumber:string; firstName:string; middleName?:string|null; lastName:string; dateOfBirth:string; gender?:string|null; studentType:number; email?:string|null; phone?:string|null; status:number; createdAtUtc:string };
type Guardian = { id:string; firstName:string; lastName:string; phone:string; email?:string|null };
type Enrollment = { id:string; academicYearName:string; classSectionName:string; enrolledOn:string; status:number };
type Sensitive = { address?:string|null; medicalInformation?:string|null; allergies?:string|null; specialEducationalNeeds?:string|null; genotype?:string|null; bloodGroup?:string|null; weightKg?:number|null; heightCm?:number|null; disability?:string|null; privateNotes?:string|null; updatedAtUtc:string };
type DocumentRow = { id:string; fileName:string; category:string; status:string|number; createdAtUtc?:string };
type Detail = { student:Student; guardians:Guardian[]; enrollments:Enrollment[] };
type Tab = "overview"|"personal"|"enrolment"|"medical"|"documents"|"attendance"|"timetable"|"results"|"fees"|"activity";

const tabs:{id:Tab;label:string}[]=[
  {id:"overview",label:"Overview"},{id:"personal",label:"Personal"},
  {id:"enrolment",label:"Enrolment"},{id:"medical",label:"Medical & support"},{id:"documents",label:"Documents"},
  {id:"attendance",label:"Attendance"},{id:"timetable",label:"Timetable"},{id:"results",label:"Results"},
  {id:"fees",label:"Fees"},{id:"activity",label:"Activity"}
];
const studentStatuses=["Active","Suspended","Withdrawn","Graduated","Alumni"];
const enrollmentStatuses=["Active","Completed","Withdrawn","Transferred"];
const studentTypes=["Day student","Boarding student","Day and boarding"];

export function StudentDetailWorkspace({studentId}:{studentId:string}){
  const [detail,setDetail]=useState<Detail|null>(null);
  const [sensitive,setSensitive]=useState<Sensitive|null>(null);
  const [documents,setDocuments]=useState<DocumentRow[]>([]);
  const [photoUrl,setPhotoUrl]=useState(`/api/backend/students/${studentId}/photo?v=2`);
  const [tab,setTab]=useState<Tab>("overview");
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");

  const load=useCallback(async()=>{
    const profileResponse=await fetch(`/api/backend/students/${studentId}`,{cache:"no-store"});
    if(!profileResponse.ok)throw new Error(profileResponse.status===403?"You do not have permission to view this student.":"The student record could not be loaded.");
    setDetail(await profileResponse.json() as Detail);
    setLoading(false);

    const [sensitiveResult,documentResult]=await Promise.allSettled([
      fetch(`/api/backend/students/${studentId}/sensitive`,{cache:"no-store"}),
      fetch(`/api/backend/documents/Student/${studentId}`,{cache:"no-store"})
    ]);
    if(sensitiveResult.status==="fulfilled"&&sensitiveResult.value.ok)setSensitive(await sensitiveResult.value.json() as Sensitive);
    if(documentResult.status==="fulfilled"&&documentResult.value.ok)setDocuments(await documentResult.value.json() as DocumentRow[]);
  },[studentId]);

  useEffect(()=>{let active=true;const timer=window.setTimeout(()=>{void load().catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"The student record could not be loaded.");}).finally(()=>{if(active)setLoading(false);});},0);return()=>{active=false;window.clearTimeout(timer);};},[load]);
  useEffect(()=>{const timer=window.setTimeout(()=>{const hash=window.location.hash.slice(1) as Tab;if(tabs.some(item=>item.id===hash))setTab(hash);},0);return()=>window.clearTimeout(timer);},[]);

  function selectTab(next:Tab){setTab(next);window.history.replaceState(null,"",`#${next}`);}
  async function copy(value:string,label:string){await navigator.clipboard.writeText(value);notify({title:`${label} copied`,message:value});}
  async function download(fileId:string){const response=await fetch(`/api/backend/documents/${fileId}/download`);if(!response.ok){notify({tone:"error",title:"Download unavailable",message:"This document could not be opened."});return;}window.open((await response.json() as {url:string}).url,"_blank","noopener,noreferrer");}

  if(loading)return <div className="space-y-5 py-4"><div className="h-48 animate-pulse rounded-[1.75rem] bg-slate-200"/><div className="h-72 animate-pulse rounded-[1.75rem] bg-slate-100"/></div>;
  if(!detail)return <div className="rounded-3xl border border-red-200 bg-red-50 p-6 text-red-800"><h1 className="text-xl font-black">Student profile unavailable</h1><p className="mt-2 text-sm">{error}</p><Link href="/portal/students" className="mt-4 inline-flex font-bold underline">Return to student directory</Link></div>;

  const {student,guardians,enrollments}=detail;
  const fullName=[student.firstName,student.middleName,student.lastName].filter(Boolean).join(" ");
  const initials=`${student.firstName[0]??""}${student.lastName[0]??""}`.toUpperCase();
  const current=enrollments.find(item=>item.status===0)??enrollments[0];
  const availableDocuments=documents.filter(item=>item.status===1||String(item.status)==="1"||String(item.status).toLowerCase()==="available");

  return <main className="min-w-0 w-full space-y-5 overflow-x-hidden pb-8">
    <div className="flex flex-wrap items-center justify-between gap-3"><Link href="/portal/students" className="inline-flex items-center gap-2 text-sm font-bold text-slate-600 hover:text-slate-950">← Student directory</Link><span className="text-xs font-bold uppercase tracking-[.14em] text-slate-400">Student profile</span></div>
    <section aria-label="Student summary" className="min-w-0 rounded-[1.75rem] border border-slate-200 bg-white px-5 py-7 shadow-[0_18px_45px_rgba(15,23,42,.07)] sm:px-8 sm:py-9 lg:px-10">
      <div className="grid min-w-0 gap-6 lg:grid-cols-[auto_minmax(0,1fr)_auto] lg:items-center">
        <div className="relative grid size-24 shrink-0 place-items-center overflow-hidden rounded-full border-[6px] border-slate-100 bg-[var(--tenant-primary-soft,#e7f2eb)] text-2xl font-black text-[var(--tenant-primary,#28654a)] shadow-inner sm:size-32 sm:text-4xl" aria-label={`${fullName} profile photograph or initials`}>
          <span aria-hidden="true">{initials}</span>
          {photoUrl&&<img src={photoUrl} alt={`${fullName} profile`} onLoad={event=>event.currentTarget.classList.remove("opacity-0")} onError={()=>setPhotoUrl("")} className="absolute inset-0 size-full object-cover opacity-0 transition-opacity duration-200"/>}
        </div>
        <div className="min-w-0">
          <p className="text-[10px] font-black uppercase tracking-[.22em] text-slate-500 sm:text-xs">Student profile · {studentTypes[student.studentType]??"Learner"}</p>
          <h1 className="mt-2 break-words text-2xl font-black leading-none tracking-[-.04em] text-slate-950 sm:text-4xl">{fullName}</h1>
          <div className="mt-4"><span className="inline-flex max-w-full rounded-full bg-[#FFDE00] px-4 py-2 text-xs font-black text-black sm:text-sm">{current?.classSectionName??"Class not assigned"}</span></div>
          <div className="mt-5 flex min-w-0 flex-col items-start gap-3 text-xs text-slate-600 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-6">
            <span className="inline-flex max-w-full items-center gap-2 break-all rounded-full bg-slate-100 px-3.5 py-2 font-mono text-[11px] font-bold text-slate-800"><StudentHeaderIcon kind="id"/>ID: {student.admissionNumber}</span>
            <span className="inline-flex items-center gap-2"><StudentHeaderIcon kind="calendar"/><span className="text-slate-500">Academic session</span><strong className="text-slate-900">{current?.academicYearName??"Not assigned"}</strong></span>
            <span className="inline-flex min-w-0 items-center gap-2"><StudentHeaderIcon kind="person"/><span className="text-slate-500">Guardian</span><strong className="truncate text-slate-900">{guardians[0]?`${guardians[0].firstName} ${guardians[0].lastName}`:"Not linked"}</strong></span>
          </div>
        </div>
        <div className="flex w-full flex-wrap items-center gap-3 border-t border-slate-100 pt-5 lg:w-auto lg:flex-nowrap lg:border-0 lg:pt-0">
          <span className="inline-flex min-h-11 items-center gap-2 rounded-full border border-slate-200 bg-white px-4 text-xs font-black text-slate-800"><span className={`size-2.5 rounded-full ${student.status===0?"bg-emerald-500":"bg-amber-500"}`}/>{studentStatuses[student.status]??"Unknown"}</span>
          <Link href={`/portal/students/${studentId}/edit`} className="tenant-primary-bg inline-flex min-h-12 flex-1 items-center justify-center rounded-xl px-5 text-sm font-black text-white shadow-sm transition hover:brightness-110 lg:flex-none">Edit student record</Link>
        </div>
      </div>
    </section>

    <nav aria-label="Student profile sections" className="rounded-xl border border-slate-200 bg-white p-3 sm:hidden"><label htmlFor="student-profile-tab" className="text-xs font-bold text-slate-600">Profile section</label><select id="student-profile-tab" value={tab} onChange={event=>selectTab(event.target.value as Tab)} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-3 text-sm font-bold">{tabs.map(item=><option key={item.id} value={item.id}>{item.label}</option>)}</select></nav>
    <div className="hidden border-b border-slate-200 sm:block"><div role="tablist" aria-label="Student profile sections" className="flex gap-1 overflow-x-auto">{tabs.map(item=><button key={item.id} type="button" role="tab" aria-selected={tab===item.id} onClick={()=>selectTab(item.id)} className={`shrink-0 border-b-2 px-4 py-3 text-sm font-bold transition ${tab===item.id?"border-[var(--tenant-primary,#28654a)] text-[var(--tenant-primary,#28654a)]":"border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-900"}`}>{item.label}</button>)}</div></div>

    {tab==="overview"&&<div className="space-y-5"><div className="grid gap-3 lg:grid-cols-3"><Summary label="Current class" value={current?.classSectionName??"Not assigned"} detail={current?.academicYearName??"No active session"}/><GuardianSummary guardians={guardians} onCopy={copy}/><Summary label="Documents" value={String(availableDocuments.length)} detail="Available student files"/></div><div className="grid gap-5 xl:grid-cols-[1.25fr_.75fr]"><Panel title="Student information" eyebrow="Profile overview"><InfoGrid items={[["Student ID",student.admissionNumber],["Date of birth",date(student.dateOfBirth)],["Gender",student.gender??"Not provided"],["Student type",studentTypes[student.studentType]??"Not provided"],["Email",student.email??"Not provided"],["Phone",student.phone??"Not provided"]]}/></Panel><Panel title="Current placement" eyebrow="Academic"><InfoGrid items={[["Class",current?.classSectionName??"Not assigned"],["Academic year",current?.academicYearName??"Not assigned"],["Enrolled",date(current?.enrolledOn)],["Status",current?enrollmentStatuses[current.status]??"Unknown":"Not enrolled"]]}/></Panel></div></div>}
    {tab==="personal"&&<div className="space-y-5"><Panel title="Personal information" eyebrow="Student identity"><InfoGrid items={[["Full name",fullName],["Student ID",student.admissionNumber],["Date of birth",date(student.dateOfBirth)],["Gender",student.gender??"Not provided"],["Email",student.email??"Not provided"],["Phone",student.phone??"Not provided"],["Address",sensitive?.address??"Not provided"],["Record created",dateTime(student.createdAtUtc)]]}/></Panel><Panel title="Guardian contacts" eyebrow="Family details"><GuardianCards guardians={guardians} onCopy={copy}/></Panel></div>}
    {tab==="enrolment"&&<Panel title="Enrolment history" eyebrow="Academic placement">{enrollments.length===0?<Empty title="No enrolment history" text="This student has not been assigned to a class."/>:<div className="overflow-hidden rounded-2xl border border-slate-200"><div className="overflow-x-auto"><table className="w-full min-w-[640px] text-left text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-4 py-3">Academic year</th><th className="px-4 py-3">Class</th><th className="px-4 py-3">Enrolled</th><th className="px-4 py-3">Status</th></tr></thead><tbody className="divide-y divide-slate-100">{enrollments.map(item=><tr key={item.id}><td className="px-4 py-4 font-bold text-slate-900">{item.academicYearName}</td><td className="px-4 py-4 text-slate-700">{item.classSectionName}</td><td className="px-4 py-4 text-slate-600">{date(item.enrolledOn)}</td><td className="px-4 py-4"><span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold">{enrollmentStatuses[item.status]??"Unknown"}</span></td></tr>)}</tbody></table></div></div>}</Panel>}
    {tab==="medical"&&<Panel title="Medical and learning support" eyebrow="Restricted information">{sensitive?<InfoGrid items={[["Genotype",sensitive.genotype??"Not recorded"],["Blood group",sensitive.bloodGroup??"Not recorded"],["Weight",sensitive.weightKg!=null?`${sensitive.weightKg} kg`:"Not recorded"],["Height",sensitive.heightCm!=null?`${sensitive.heightCm} cm`:"Not recorded"],["Disability",sensitive.disability??"Not recorded"],["Medical information",sensitive.medicalInformation??"None recorded"],["Allergies",sensitive.allergies??"None recorded"],["Special educational needs",sensitive.specialEducationalNeeds??"None recorded"],["Private notes",sensitive.privateNotes??"None recorded"],["Last updated",dateTime(sensitive.updatedAtUtc)]]}/>:<Empty title="Restricted or unavailable" text="No medical and support information is available to your account."/>}</Panel>}
    {tab==="documents"&&<Panel title="Student documents" eyebrow="Secure files">{availableDocuments.length===0?<Empty title="No documents available" text="Photographs, identity records and other student files will appear here."/>:<div className="grid gap-3 md:grid-cols-2">{availableDocuments.map(file=><button key={file.id} type="button" onClick={()=>void download(file.id)} className="flex items-center justify-between gap-3 rounded-2xl border border-slate-200 p-4 text-left hover:bg-slate-50"><span className="min-w-0"><strong className="block truncate text-sm text-slate-900">{file.fileName}</strong><small className="text-slate-500">{file.category} · {dateTime(file.createdAtUtc)}</small></span><span className="text-xs font-black tenant-primary-text">Open</span></button>)}</div>}</Panel>}
    {(["attendance","timetable","results","fees","activity"] as Tab[]).includes(tab)&&<PlannedPanel tab={tab}/>}
  </main>;
}

function Panel({title,eyebrow,children}:{title:string;eyebrow:string;children:ReactNode}){return <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">{eyebrow}</p><h2 className="mt-1 text-xl font-black text-slate-950">{title}</h2><div className="mt-5">{children}</div></section>}
function GuardianSummary({guardians,onCopy}:{guardians:Guardian[];onCopy:(value:string,label:string)=>Promise<void>}){const guardian=guardians[0];if(!guardian)return <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"><p className="text-[10px] font-black uppercase tracking-[.14em] text-slate-400">Primary guardian</p><p className="mt-2 text-sm font-black text-slate-950">No guardian linked</p><p className="mt-1 text-xs text-slate-500">Add a family contact to this student.</p></div>;return <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"><p className="text-[10px] font-black uppercase tracking-[.14em] text-slate-400">Primary guardian</p><Link href={`/portal/guardians/${guardian.id}`} className="mt-2 block truncate text-sm font-black text-slate-950 hover:underline">{guardian.firstName} {guardian.lastName}</Link><ContactLine label="Copy guardian phone" value={guardian.phone} onCopy={()=>onCopy(guardian.phone,"Phone")}/>{guardian.email&&<ContactLine label="Copy guardian email" value={guardian.email} onCopy={()=>onCopy(guardian.email!,"Email")}/>} {guardians.length>1&&<p className="mt-2 text-[10px] font-bold text-slate-400">Plus {guardians.length-1} additional guardian{guardians.length===2?"":"s"}</p>}</div>}
function GuardianCards({guardians,onCopy}:{guardians:Guardian[];onCopy:(value:string,label:string)=>Promise<void>}){if(guardians.length===0)return <Empty title="No guardian linked" text="Link a guardian from the student or guardian workflow."/>;return <div className="grid gap-3 md:grid-cols-2">{guardians.map(guardian=><article key={guardian.id} className="rounded-2xl border border-slate-200 p-4"><div className="flex items-center gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-full bg-amber-100 text-[11px] font-black text-amber-900">{guardian.firstName[0]}{guardian.lastName[0]}</span><Link href={`/portal/guardians/${guardian.id}`} className="truncate text-sm font-black text-slate-950 hover:underline">{guardian.firstName} {guardian.lastName}</Link></div><div className="mt-3 border-t border-slate-100 pt-2"><ContactLine label="Copy guardian phone" value={guardian.phone} onCopy={()=>onCopy(guardian.phone,"Phone")}/>{guardian.email?<ContactLine label="Copy guardian email" value={guardian.email} onCopy={()=>onCopy(guardian.email!,"Email")}/>:<p className="mt-1 text-[11px] text-slate-400">No email address</p>}</div></article>)}</div>}
function ContactLine({label,value,onCopy}:{label:string;value:string;onCopy:()=>Promise<void>}){return <button type="button" onClick={()=>void onCopy()} title={label} aria-label={`${label}: ${value}`} className="mt-1 flex max-w-full items-center gap-1.5 text-left text-[11px] font-semibold text-slate-500 hover:text-slate-950"><CopyIcon/><span className="truncate">{value}</span></button>}
function CopyIcon(){return <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className="size-3.5 shrink-0"><rect x="6" y="6" width="10" height="11" rx="1.5"/><path d="M13 6V4.5A1.5 1.5 0 0 0 11.5 3h-7A1.5 1.5 0 0 0 3 4.5v8A1.5 1.5 0 0 0 4.5 14H6"/></svg>}
function StudentHeaderIcon({kind}:{kind:"id"|"calendar"|"person"}){const paths={id:"M3 5h18v14H3zM7 9h4M7 13h5M16 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4Z",calendar:"M7 3v4M17 3v4M4 9h16M5 5h14a1 1 0 0 1 1 1v13H4V6a1 1 0 0 1 1-1Z",person:"M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM4 21a8 8 0 0 1 16 0"};return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" className="size-4 shrink-0"><path d={paths[kind]}/></svg>}
function Summary({label,value,detail}:{label:string;value:string;detail:string}){return <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"><p className="text-[10px] font-black uppercase tracking-[.14em] text-slate-400">{label}</p><p className="mt-2 text-lg font-black text-slate-950">{value}</p><p className="mt-1 text-xs text-slate-500">{detail}</p></div>}
function InfoGrid({items}:{items:[string,string][]}){return <dl className="grid gap-3 sm:grid-cols-2">{items.map(([label,value])=><div key={label} className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4"><dt className="text-[10px] font-black uppercase tracking-[.12em] text-slate-400">{label}</dt><dd className="mt-1 break-words text-sm font-bold text-slate-800">{value}</dd></div>)}</dl>}
function Empty({title,text}:{title:string;text:string}){return <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-8 text-center"><p className="font-black text-slate-800">{title}</p><p className="mx-auto mt-1 max-w-lg text-sm text-slate-500">{text}</p></div>}
function PlannedPanel({tab}:{tab:Tab}){const copy:Record<string,[string,string]>={attendance:["Attendance","Daily and term attendance will appear here when attendance recording is connected."],timetable:["Timetable","The student’s class timetable will appear here when timetable management is available."],results:["Results","Assessments, report cards and published results will appear here when results are connected."],fees:["Fees","Invoices, payments and outstanding balances will appear here when student billing is connected."],activity:["Activity","Authorised changes and important student-record events will appear here when this view is connected to audit history."]};const [title,text]=copy[tab];return <Panel title={title} eyebrow="Student workspace"><Empty title={`${title} is not connected yet`} text={text}/></Panel>}
function date(value?:string|null){if(!value)return "Not provided";return new Date(`${value.length===10?`${value}T00:00:00`:value}`).toLocaleDateString(undefined,{dateStyle:"medium"})}
function dateTime(value?:string|null){if(!value)return "Not available";return new Date(value).toLocaleString(undefined,{dateStyle:"medium",timeStyle:"short"})}
