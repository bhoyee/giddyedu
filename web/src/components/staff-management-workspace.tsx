"use client";

import { useEffect, useState } from "react";
import { ResourcePage } from "@/components/resource-page";
import { StaffRegistrationWizard } from "@/components/staff-registration-wizard";
import { StaffDirectoryTable } from "@/components/staff-directory-table";

type StaffPageResult = { total: number };

export function StaffManagementWorkspace() {
  const [total, setTotal] = useState<number | null>(null);
  const [teachingTotal, setTeachingTotal] = useState<number | null>(null);
  const [nonTeachingTotal, setNonTeachingTotal] = useState<number | null>(null);
  const [directoryVersion, setDirectoryVersion] = useState(0);

  useEffect(() => {
    void Promise.all(["", "&category=0", "&category=2"].map(async filter => {
      const response = await fetch(`/api/backend/hr/staff?page=1&pageSize=1${filter}`, { cache: "no-store" });
      return response.ok ? (await response.json() as StaffPageResult).total : null;
    })).then(([all, teaching, nonTeaching]) => { setTotal(all); setTeachingTotal(teaching); setNonTeachingTotal(nonTeaching); });
  }, [directoryVersion]);

  return <section className="space-y-6">
    <header className="tenant-primary-bg relative overflow-hidden rounded-[1.75rem] px-6 py-7 text-white shadow-[0_24px_60px_rgba(18,55,42,.16)] sm:px-8 sm:py-9">
      <div className="absolute -right-20 -top-28 size-80 rounded-full border-[52px] border-white/[.04]" />
      <div className="absolute -bottom-16 right-[28%] size-44 rounded-full bg-emerald-300/10 blur-2xl" />
      <div className="relative flex flex-col gap-7 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-2xl"><p className="text-xs font-black uppercase tracking-[.2em] text-emerald-200">People · HR & staff</p><h1 className="mt-3 text-3xl font-black tracking-[-.04em] sm:text-4xl">Your school team, in one place</h1><p className="mt-3 text-sm leading-6 text-white/70 sm:text-base">Maintain trusted employment records, organise responsibilities and give every member of staff the right access to their workspace.</p></div>
        <div className="grid grid-cols-3 gap-2"><Metric label="All staff" value={total} /><Metric label="Teaching" value={teachingTotal} /><Metric label="Non-teaching" value={nonTeachingTotal} /></div>
      </div>
    </header>

    <div className="rounded-[1.5rem] border border-[#dfe6e1] bg-white p-4 shadow-[0_12px_36px_rgba(28,53,42,.055)] sm:p-7">
      <div className="mb-6 flex flex-col gap-4 border-b border-slate-100 pb-5 sm:flex-row sm:items-end sm:justify-between"><div><p className="text-xs font-black uppercase tracking-[.16em] text-[#5f806f]">Staff directory</p><h2 className="mt-2 text-2xl font-black tracking-[-.03em] text-slate-950">Manage staff records</h2><p className="mt-2 max-w-lg text-sm leading-6 text-slate-500">Open a staff member for their complete record, send an account invitation, or attach verified documents.</p></div><StaffRegistrationWizard onCreated={()=>setDirectoryVersion(value=>value+1)}/></div>
      <div className="space-y-5">
        <StaffDirectoryTable version={directoryVersion}/>
        <ResourcePage embedded splitManagement hideRecords title="Staff" description="Manage authoritative staff profiles, positions, and teaching responsibilities." endpoint="hr/staff?page=1&pageSize=50" emptyMessage="No staff profiles have been created. Add the first member of your school team to begin." fields={[]} creates={[
          { endpoint:"hr/teaching-assignments", title:"Assign teaching responsibility", fields:[{name:"staffId",label:"Teaching staff",type:"select",optionsEndpoint:"hr/staff?page=1&pageSize=100",optionLabelKey:"staffNumber"},{name:"classSectionId",label:"Class section",type:"select",optionsEndpoint:"academics/structure",optionsCollectionKey:"classSections"},{name:"subjectId",label:"Subject (subject teachers only)",type:"select",required:false,optionsEndpoint:"academics/structure",optionsCollectionKey:"subjects"},{name:"role",label:"Responsibility",type:"select",numeric:true,options:[{value:"0",label:"Subject teacher"},{value:"1",label:"Class teacher"},{value:"2",label:"Form teacher"}]}] }
        ]} actions={[{ endpoint:"", title:"Open", href:"/portal/staff/{id}" },{ endpoint:"account-invitations", title:"Invite staff member", payload:{targetType:0}, itemIdField:"targetId" },{ endpoint:"documents/StaffProfile/{id}/uploads", title:"Upload document", documentUpload:true, fields:[{name:"category",label:"Category",type:"select",options:[{value:"identity",label:"Identity"},{value:"qualification",label:"Qualification"},{value:"contract",label:"Contract"},{value:"photo",label:"Photograph"},{value:"other",label:"Other"}]},{name:"file",label:"PDF, JPEG, or PNG (maximum 10 MB)",type:"file"}] }]} />
      </div>
    </div>
  </section>;
}

function Metric({ label, value }: { label: string; value: number | null }) {
  return <div className="min-w-24 rounded-xl border border-white/10 bg-white/[.07] px-3 py-3 backdrop-blur-sm"><p className="text-[9px] font-black uppercase tracking-[.13em] text-white/55">{label}</p><p className="mt-1 text-xl font-black">{value === null ? "—" : value.toLocaleString()}</p></div>;
}
