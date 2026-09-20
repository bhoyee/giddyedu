"use client";

import { useState } from "react";
import Link from "next/link";
import { StaffRegistrationWizard } from "@/components/staff-registration-wizard";
import { StaffDirectoryTable } from "@/components/staff-directory-table";

export function StaffManagementWorkspace() {
  const [directoryVersion, setDirectoryVersion] = useState(0);
  const [binMode, setBinMode] = useState(false);
  const [binCount, setBinCount] = useState(0);
  const [canManageStaff, setCanManageStaff] = useState(false);

  return <section className="space-y-6">
    <div className="rounded-[1.5rem] border border-[#dfe6e1] bg-white p-4 shadow-[0_12px_36px_rgba(28,53,42,.055)] sm:p-7">
      <div className="mb-6 flex flex-col gap-4 border-b border-slate-100 pb-5 sm:flex-row sm:items-end sm:justify-between"><div><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">People / HR</p><h1 className="mt-1 text-2xl font-black tracking-[-.03em] text-slate-950 sm:text-3xl">Staff directory</h1><p className="mt-1 text-sm text-slate-500">Find and manage your school team.</p></div><div className="flex flex-wrap items-center gap-2">{canManageStaff && (binCount > 0 || binMode) && <button type="button" onClick={() => setBinMode(value => !value)} aria-pressed={binMode} className="rounded-xl bg-red-600 px-4 py-2.5 text-sm font-black text-white shadow-sm transition hover:bg-red-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600">{binMode ? "← Active staff" : `Bin · ${binCount}`}</button>}{canManageStaff && <Link href="/portal/staff/import" className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:brightness-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-800">Import staff</Link>}<StaffRegistrationWizard onCreated={()=>setDirectoryVersion(value=>value+1)}/></div></div>
      <div className="space-y-5">
        <StaffDirectoryTable key={binMode ? "bin" : "active"} version={directoryVersion} binMode={binMode} onBinCountChange={setBinCount} onCanManageChange={setCanManageStaff}/>
      </div>
    </div>
  </section>;
}
