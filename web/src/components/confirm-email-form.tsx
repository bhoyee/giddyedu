"use client";

import Link from "next/link";
import { OtpInput } from "./otp-input";
import { FormEvent, useCallback, useEffect, useRef, useState } from "react";

export function ConfirmEmailForm({ email, token, codeAlreadySent = false }: { email: string; token: string; codeAlreadySent?: boolean }) {
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const automaticallyRequested = useRef(false);

  const sendCode = useCallback(async () => {
    if (!email) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const response = await fetch("/api/auth/confirm-email/resend", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email }) });
      if (!response.ok) { setError(response.status === 429 ? "Too many code requests. Please wait before trying again." : "A new code could not be sent. Please try again."); return; }
      setNotice("A new six-digit code has been sent. It expires in 10 minutes.");
    } catch { setError("Email confirmation is temporarily unavailable. Please try again."); }
    finally { setBusy(false); }
  }, [email]);

  useEffect(() => { if (!token && email && !codeAlreadySent && !automaticallyRequested.current) { automaticallyRequested.current = true; void sendCode(); } }, [codeAlreadySent, email, sendCode, token]);

  async function confirmWithCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    const code = String(new FormData(event.currentTarget).get("code") ?? "");
    try {
      const response = await fetch("/api/auth/confirm-email/code", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, code }) });
      if (!response.ok) { setError("This code is invalid or expired. Request a new code and try again."); return; }
      setConfirmed(true);
    } catch { setError("Email confirmation is temporarily unavailable. Please try again."); }
    finally { setBusy(false); }
  }

  async function confirmWithLink() {
    setBusy(true); setError("");
    try { const response = await fetch("/api/auth/confirm-email", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, token }) }); if (!response.ok) { setError("This confirmation link is invalid or expired. Request a new code below."); return; } setConfirmed(true); }
    catch { setError("Email confirmation is temporarily unavailable. Please try again."); }
    finally { setBusy(false); }
  }

  if (!email) return <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">An email address is required. Return to sign in and try again.</p>;
  if (confirmed) return <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-6 text-emerald-950"><div className="grid size-11 place-items-center rounded-full bg-emerald-700 text-xl font-black text-white">✓</div><h2 className="mt-4 text-xl font-black">Email confirmed</h2><p className="mt-2 text-sm leading-6 text-emerald-900/75">Your account is ready. We have also sent your GiddyEdu welcome email.</p><Link href="/login" className="mt-5 inline-flex min-h-12 items-center rounded-full bg-[#12372a] px-6 text-sm font-black text-white">Continue to sign in</Link></div>;

  return <div>
    <div className="rounded-2xl border border-[#d9e3dc] bg-[#f7f9f6] p-4 text-sm leading-6 text-[#56655d]">Confirming <strong className="break-all text-[#25372d]">{email}</strong></div>
    {token ? <div className="mt-5"><button type="button" disabled={busy} onClick={() => void confirmWithLink()} className="min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white transition hover:bg-[#1d513d] disabled:opacity-50">{busy ? "Confirming…" : "Confirm email securely"}</button><div className="my-6 flex items-center gap-3 text-xs font-bold uppercase tracking-[.12em] text-[#87938c]"><span className="h-px flex-1 bg-[#dfe5e0]"/>or enter a code<span className="h-px flex-1 bg-[#dfe5e0]"/></div></div> : <p className="mt-5 text-sm leading-6 text-[#66736c]">Enter the six-digit verification code sent to your inbox.</p>}
    <form onSubmit={confirmWithCode} className={token ? "" : "mt-5"}><OtpInput name="code" label="Verification code" autoFocus={!token} />{notice&&<p role="status" className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">{notice}</p>}{error&&<p role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="mt-5 min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white transition hover:bg-[#1d513d] disabled:opacity-50">{busy ? "Checking code…" : "Verify email"}</button></form>
    <button type="button" disabled={busy} onClick={() => void sendCode()} className="mt-4 w-full py-2 text-sm font-black text-[#277052] transition hover:text-[#12372a] disabled:opacity-50">Send a new code</button>
    <p className="mt-4 text-center text-xs leading-5 text-[#7a867f]">Codes expire after 10 minutes and are invalidated after five incorrect attempts.</p>
  </div>;
}
