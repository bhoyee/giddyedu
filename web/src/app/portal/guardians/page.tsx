"use client";

import { useState } from "react";
import Link from "next/link";
import { GuardianRegistrationForm } from "@/components/guardian-registration-form";
import { GuardianDirectory } from "@/components/guardian-directory";

export default function GuardiansPage() {
  const [revision, setRevision] = useState(0);
  const [binMode, setBinMode] = useState(false);
  const [binCount, setBinCount] = useState(0);
  const [canManage, setCanManage] = useState(false);
  return <section className="space-y-6"><div className="rounded-[1.5rem] border border-[#dfe6e1] bg-white p-4 shadow-[0_12px_36px_rgba(28,53,42,.055)] sm:p-7">
    <div className="mb-6 flex flex-col gap-4 border-b border-slate-100 pb-5 sm:flex-row sm:items-end sm:justify-between"><div><p className="text-xs font-black uppercase tracking-[.16em] tenant-primary-text">People / Guardians</p><h1 className="mt-1 text-2xl font-black tracking-[-.03em] text-slate-950 sm:text-3xl">Guardian directory</h1><p className="mt-1 text-sm text-slate-500">Find and connect parents and guardians to their children.</p></div><div className="flex flex-wrap items-center gap-2">
      {canManage && (binCount > 0 || binMode) && <button type="button" onClick={() => setBinMode(value => !value)} aria-pressed={binMode} className="rounded-xl bg-red-600 px-4 py-2.5 text-sm font-black text-white shadow-sm transition hover:bg-red-700">{binMode ? "← Active guardians" : `Bin · ${binCount}`}</button>}
      {canManage && !binMode && <Link href="/portal/guardians/import" className="tenant-primary-bg rounded-xl px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:brightness-95">Import guardians</Link>}
      {!binMode && <GuardianRegistrationForm onCreated={() => setRevision(value => value + 1)} />}
    </div></div>
    <GuardianDirectory key={binMode ? "bin" : "active"} version={revision} binMode={binMode} onBinCountChange={setBinCount} onCanManageChange={setCanManage} />
  </div></section>;
}
