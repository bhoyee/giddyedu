"use client";

import Link from "next/link";
import { useCallback, useState } from "react";
import { StudentDirectoryTable } from "@/components/student-directory-table";
import { StudentRegistrationWizard } from "@/components/student-registration-wizard";

export function StudentPageWorkspace(){
  const [binMode,setBinMode]=useState(false); const [binCount,setBinCount]=useState(0);
  const updateBinCount=useCallback((count:number)=>{setBinCount(count);if(count===0)setBinMode(false)},[]);
  return <section className="space-y-6">
    <div className="flex flex-col gap-4 rounded-[1.5rem] border border-[#dfe6e1] bg-white p-5 shadow-sm sm:flex-row sm:items-end sm:justify-between sm:p-7">
      <div><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">People / Students</p><h1 className="mt-1 text-2xl font-black tracking-[-.03em] text-slate-950 sm:text-3xl">Student directory</h1><p className="mt-1 text-sm text-slate-500">Manage learner records, class placement and family connections.</p></div>
      <div className="flex flex-wrap items-center gap-2"><Link href="/portal/students/import" className="tenant-primary-bg inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-bold text-white shadow-sm hover:brightness-95">Import students</Link>{binCount>0&&<button type="button" onClick={()=>setBinMode(value=>!value)} className={binMode?"tenant-primary-bg inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-bold text-white":"inline-flex h-11 items-center justify-center rounded-xl bg-red-700 px-5 text-sm font-bold text-white hover:bg-red-800"}>{binMode?"Back to students":`Bin · ${binCount}`}</button>}<StudentRegistrationWizard /></div>
    </div>
    <StudentDirectoryTable binMode={binMode} onBinCountChange={updateBinCount}/>
  </section>
}
