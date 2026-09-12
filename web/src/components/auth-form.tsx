"use client";
import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { OtpInput } from "./otp-input";
import { TurnstileWidget } from "@/components/turnstile-widget";

export function LoginForm() {
  type Workspace = { tenantId: string; tenantName: string; campuses: { campusId: string; campusName: string }[] };
  type WorkspaceChoice = { tenantId: string; tenantName: string; campusId: string | null; campusName: string | null };
  const router = useRouter();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [credentials, setCredentials] = useState<{ email: string; password: string; rememberMe: boolean } | null>(null);
  const [choices, setChoices] = useState<WorkspaceChoice[]>([]);
  const [email, setEmail] = useState("");
  const [rememberMe, setRememberMe] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const rememberedEmail = window.localStorage.getItem("giddyedu_remembered_email");
      if (rememberedEmail) { setEmail(rememberedEmail); setRememberMe(true); }
    }, 0);
    return () => window.clearTimeout(timer);
  }, []);

  async function signInto(choice: WorkspaceChoice, enteredCredentials: { email: string; password: string; rememberMe: boolean }) {
    const response = await fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...enteredCredentials, tenantId: choice.tenantId, campusId: choice.campusId }) });
    if (!response.ok) { setError("Sign in could not be completed for that workspace."); setBusy(false); return; }
    if (enteredCredentials.rememberMe) window.localStorage.setItem("giddyedu_remembered_email", enteredCredentials.email);
    else window.localStorage.removeItem("giddyedu_remembered_email");
    router.replace("/portal"); router.refresh();
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    const data = new FormData(event.currentTarget);
    const enteredCredentials = { email: String(data.get("email") ?? "").trim(), password: String(data.get("password") ?? ""), rememberMe };
    const response = await fetch("/api/auth/workspaces", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: enteredCredentials.email, password: enteredCredentials.password }) });
    if (!response.ok) { const body = await response.json().catch(() => ({})) as { code?: string }; setBusy(false); if (response.status === 403 && body.code === "email_unconfirmed") { router.push(`/confirm-email?email=${encodeURIComponent(enteredCredentials.email)}`); return; } setError(response.status === 401 ? "Email or password is incorrect." : "Sign in could not be completed."); return; }
    const workspaces = await response.json() as Workspace[];
    const availableChoices: WorkspaceChoice[] = [];
    for (const workspace of workspaces) {
      if (workspace.campuses.length === 0) availableChoices.push({ tenantId: workspace.tenantId, tenantName: workspace.tenantName, campusId: null, campusName: null });
      else for (const campus of workspace.campuses) availableChoices.push({ tenantId: workspace.tenantId, tenantName: workspace.tenantName, campusId: campus.campusId, campusName: campus.campusName });
    }
    if (availableChoices.length === 0) { setBusy(false); setError("No active school workspace is available for this account."); return; }
    if (availableChoices.length === 1) { await signInto(availableChoices[0], enteredCredentials); return; }
    setCredentials(enteredCredentials); setChoices(availableChoices); setBusy(false);
  }

  if (credentials && choices.length > 1) return <div><button type="button" onClick={() => { setCredentials(null); setChoices([]); setError(""); }} className="text-sm font-bold text-[#277052]">← Use another account</button><h2 className="mt-6 text-2xl font-black tracking-[-.04em]">Choose your workspace</h2><p className="mt-2 text-sm leading-6 text-[#66736c]">This account belongs to more than one school or campus. Choose where you want to work now.</p><div className="mt-6 grid gap-3">{choices.map(choice => <button key={`${choice.tenantId}:${choice.campusId ?? "all"}`} type="button" disabled={busy} onClick={() => { setBusy(true); setError(""); void signInto(choice, credentials); }} className="group flex items-center justify-between rounded-2xl border border-[#d6dfd8] bg-[#fbfaf6] p-4 text-left transition hover:border-[#2f6d52] hover:bg-white"><span><span className="block font-black text-[#1f3027]">{choice.tenantName}</span><span className="mt-1 block text-sm text-[#6a776f]">{choice.campusName ?? "School-wide workspace"}</span></span><span className="grid size-9 place-items-center rounded-full bg-[#e7efe9] text-[#255c43] transition group-hover:bg-[#12372a] group-hover:text-white">→</span></button>)}</div>{error && <p role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}</div>;
  return <><form onSubmit={submit} autoComplete="on" className="space-y-5"><label className="block text-sm font-black text-[#29372f]">Email address<input name="email" type="email" required autoComplete="username" placeholder="you@school.edu" value={email} onChange={(event) => setEmail(event.target.value)} className={inputClassName} /></label><div><div className="mb-2 flex items-center justify-between"><label htmlFor="login-password" className="text-sm font-black text-[#29372f]">Password</label><a href="/forgot-password" className="text-sm font-bold text-[#277052] transition hover:text-[#12372a]">Forgot password?</a></div><input id="login-password" name="password" type="password" required autoComplete="current-password" className={inputClassName} /></div><label className="flex cursor-pointer items-start gap-3 text-sm leading-6 text-[#526159]"><input name="rememberMe" type="checkbox" checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} className="mt-1 size-4 rounded border-[#aebbb2] accent-[#1d6046]" /><span><strong className="font-bold text-[#29372f]">Remember me</strong> on this device</span></label>{error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white shadow-lg shadow-[#12372a]/15 transition hover:-translate-y-0.5 hover:bg-[#1d513d] disabled:translate-y-0 disabled:opacity-50">{busy ? "Finding your workspace…" : "Sign in"}</button></form><div className="mt-8 border-t border-[#e0e5e1] pt-6 text-center text-sm text-[#66736c]">New school? <a className="font-bold text-[#277052]" href="/register">Register your school</a></div></>;
}

export function RegisterForm() {
  const router = useRouter();
  const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); setMessage(""); const form = event.currentTarget; const formData = new FormData(form); const data = Object.fromEntries(formData); const email = String(formData.get("email") ?? "").trim(); const response = await fetch("/api/auth/register", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...data, acceptTerms: formData.get("acceptTerms") === "on", turnstileToken: formData.get("cf-turnstile-response") }) }); await response.json().catch(() => ({})); setBusy(false); if (!response.ok) { setError("Registration could not be completed. Check the entered values and security check, then try again."); return; } router.push(`/confirm-email?email=${encodeURIComponent(email)}&sent=1`); }
  return <form onSubmit={submit} className="space-y-5"><div className="grid gap-4 sm:grid-cols-2"><div className="sm:col-span-2"><Field name="schoolName" label="School name" autoComplete="organization" /></div><Field name="schoolSlug" label="School URL name" placeholder="greenfield-academy" /><Field name="campusName" label="Main campus" /><Field name="displayName" label="Administrator name" autoComplete="name" /><Field name="email" label="Email address" type="email" autoComplete="email" /><div className="sm:col-span-2"><Field name="password" label="Create password" type="password" autoComplete="new-password" /></div></div><label className="flex cursor-pointer items-start gap-3 rounded-xl border border-[#d8e0da] bg-[#f8faf7] p-4 text-sm leading-6 text-[#526159]"><input name="acceptTerms" type="checkbox" required className="mt-1 size-4 shrink-0 rounded border-[#aebbb2] accent-[#1d6046]" /><span>I agree to the <a href="/terms" target="_blank" className="font-bold text-[#277052] underline-offset-2 hover:underline">Terms and Conditions</a> and acknowledge the <a href="/privacy" target="_blank" className="font-bold text-[#277052] underline-offset-2 hover:underline">Privacy Policy</a>.</span></label><TurnstileWidget action="register" />{error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}<button disabled={busy} className="min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white transition hover:-translate-y-0.5 hover:bg-[#1d513d] disabled:translate-y-0 disabled:opacity-50">{busy ? "Creating school…" : "Register school"}</button></form>;
}

export function PlatformLoginForm() {
  const router = useRouter(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const response = await fetch("/api/auth/platform-login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: data.get("email"), password: data.get("password") }) }); setBusy(false); if (!response.ok) { setError("Platform operator credentials are invalid or the account is not authorised."); return; } router.replace("/portal"); router.refresh(); }
  return <form onSubmit={submit} className="space-y-4"><Field name="email" label="Operator email" type="email" /><Field name="password" label="Password" type="password" />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Signing in…" : "Sign in to platform"}</button></form>;
}

export function ForgotPasswordForm() {
  const router = useRouter(); const [email, setEmail] = useState(""); const [codeRequested, setCodeRequested] = useState(false); const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function requestCode(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const requestedEmail = String(data.get("email") ?? "").trim(); const response = await fetch("/api/auth/forgot-password", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: requestedEmail }) }); setBusy(false); if (!response.ok) { const body = await response.json().catch(() => ({})) as { code?: string; message?: string }; setError(response.status === 404 && body.code === "account_email_not_found" ? "No active GiddyEdu account exists for this email address." : response.status === 429 ? "Too many attempts. Please wait before trying again." : body.message ?? "The request could not be completed. Please try again."); return; } setEmail(requestedEmail); setCodeRequested(true); setMessage("A six-digit verification code has been sent to your email address. It expires in 10 minutes."); }
  async function resetWithCode(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); if (data.get("newPassword") !== data.get("confirmPassword")) { setBusy(false); setError("The passwords do not match."); return; } const response = await fetch("/api/auth/reset-password/code", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, code: data.get("code"), newPassword: data.get("newPassword") }) }); setBusy(false); if (!response.ok) { setError("The code is invalid or expired, or the password does not meet the security requirements."); return; } router.replace("/login?passwordReset=success"); }
  if (!codeRequested) return <form onSubmit={requestCode} className="space-y-5"><Field name="email" label="Account email" type="email" autoComplete="email" placeholder="you@school.edu" />{error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white transition hover:bg-[#1d513d] disabled:opacity-50">{busy ? "Checking account…" : "Send verification code"}</button><p className="text-center text-sm text-[#66736c]"><a href="/login" className="font-bold text-[#277052]">Return to sign in</a></p></form>;
  return <form onSubmit={resetWithCode} className="space-y-5"><p role="status" className="rounded-xl border border-[#cfe2d5] bg-[#edf5ef] p-4 text-sm leading-6 text-[#24553f]">{message}</p><OtpInput name="code" label="Verification code" autoFocus /><Field name="newPassword" label="New password" type="password" autoComplete="new-password" /><Field name="confirmPassword" label="Confirm new password" type="password" autoComplete="new-password" />{error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<p className="text-xs leading-5 text-[#748078]">Use at least 12 characters including uppercase, lowercase, a number and a symbol. A code is invalidated after five failed attempts.</p><button disabled={busy} className="min-h-14 w-full rounded-full bg-[#12372a] px-5 font-black text-white transition hover:bg-[#1d513d] disabled:opacity-50">{busy ? "Updating password…" : "Set new password"}</button><button type="button" onClick={() => { setCodeRequested(false); setMessage(""); setError(""); }} className="w-full text-sm font-bold text-[#277052]">Use a different email or resend</button></form>;
}

export function ResetPasswordForm({ email, token }: { email: string; token: string }) {
  const router = useRouter(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const response = await fetch("/api/auth/reset-password", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, token, newPassword: data.get("newPassword") }) }); setBusy(false); if (!response.ok) { setError("The reset link is invalid or the password does not meet security requirements."); return; } router.replace("/login?passwordReset=success"); }
  if (!email || !token) return <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">This password-reset link is incomplete.</p>;
  return <form onSubmit={submit} className="space-y-4"><Field name="newPassword" label="New password" type="password" />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Updating…" : "Set password"}</button></form>;
}

const inputClassName = "mt-2 min-h-13 w-full rounded-xl border border-[#cbd5ce] bg-[#fbfaf6] px-4 text-sm outline-none transition placeholder:text-[#98a29c] focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10";
function Field({ name, label, type = "text", required = true, autoComplete, placeholder, inputMode, pattern, maxLength }: { name: string; label: string; type?: string; required?: boolean; autoComplete?: string; placeholder?: string; inputMode?: "numeric"; pattern?: string; maxLength?: number }) { return <label className="block text-sm font-black text-[#29372f]">{label}<input name={name} type={type} required={required} autoComplete={autoComplete} placeholder={placeholder} inputMode={inputMode} pattern={pattern} maxLength={maxLength} className={inputClassName} /></label>; }
