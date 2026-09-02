"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";

export function AcceptInvitationForm({ token }: { token: string }) {
  const [busy,setBusy]=useState(false); const [error,setError]=useState(""); const [tenantId,setTenantId]=useState("");
  async function submit(event:FormEvent<HTMLFormElement>){event.preventDefault();setBusy(true);setError("");const data=new FormData(event.currentTarget);const response=await fetch("/api/auth/accept-invitation",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({token,displayName:data.get("displayName"),password:data.get("password")})});setBusy(false);if(!response.ok){setError("This invitation could not be accepted. It may have expired or the password may not meet security requirements.");return;}const body=await response.json() as {tenantId:string};setTenantId(body.tenantId);}
  if(!token)return <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">The invitation link is missing its secure token.</p>;
  if(tenantId)return <div className="rounded-xl bg-emerald-50 p-5 text-emerald-900"><p>Your account is connected to the school workspace.</p><p className="mt-3 break-all text-sm">Workspace ID: <strong>{tenantId}</strong></p><Link href="/login" className="mt-4 inline-block font-bold underline">Continue to sign in</Link></div>;
  return <form onSubmit={submit} className="space-y-4"><label className="block text-sm font-semibold">Display name<input name="displayName" required maxLength={200} className="mt-1 w-full rounded-xl border border-slate-300 px-4 py-3"/></label><label className="block text-sm font-semibold">Create password<input name="password" type="password" required minLength={12} autoComplete="new-password" className="mt-1 w-full rounded-xl border border-slate-300 px-4 py-3"/></label><p className="text-xs text-slate-500">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>{error&&<p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy?"Connecting account…":"Accept invitation"}</button></form>;
}
