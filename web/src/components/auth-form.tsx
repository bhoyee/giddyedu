"use client";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

export function LoginForm() {
  const router = useRouter(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const response = await fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: data.get("email"), password: data.get("password"), tenantId: data.get("tenantId"), campusId: data.get("campusId") || null }) }); setBusy(false); if (!response.ok) { setError(response.status === 401 ? "Email, password, tenant, or email confirmation is invalid." : "Sign in could not be completed."); return; } router.replace("/portal"); router.refresh(); }
  return <form onSubmit={submit} className="space-y-4"><Field name="email" label="Email" type="email" /><Field name="password" label="Password" type="password" /><Field name="tenantId" label="School tenant ID" /><Field name="campusId" label="Campus ID (optional)" required={false} />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Signing in…" : "Sign in securely"}</button></form>;
}

export function RegisterForm() {
  const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); setMessage(""); const data = Object.fromEntries(new FormData(event.currentTarget)); const response = await fetch("/api/auth/register", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data) }); const body = await response.json().catch(() => ({})); setBusy(false); if (!response.ok) { setError("Registration could not be completed. Check the entered values and try again."); return; } setMessage(`School created. Save tenant ID ${body.tenantId} and confirm the email before signing in.`); event.currentTarget.reset(); }
  return <form onSubmit={submit} className="space-y-4"><Field name="schoolName" label="School name" /><Field name="schoolSlug" label="School URL name" /><Field name="campusName" label="Main campus" /><Field name="displayName" label="Administrator name" /><Field name="email" label="Email" type="email" /><Field name="password" label="Password" type="password" />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Creating school…" : "Register school"}</button></form>;
}

export function PlatformLoginForm() {
  const router = useRouter(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const response = await fetch("/api/auth/platform-login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: data.get("email"), password: data.get("password") }) }); setBusy(false); if (!response.ok) { setError("Platform operator credentials are invalid or the account is not authorised."); return; } router.replace("/portal"); router.refresh(); }
  return <form onSubmit={submit} className="space-y-4"><Field name="email" label="Operator email" type="email" /><Field name="password" label="Password" type="password" />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Signing in…" : "Sign in to platform"}</button></form>;
}

export function ForgotPasswordForm() {
  const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); const data = new FormData(event.currentTarget); await fetch("/api/auth/forgot-password", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: data.get("email") }) }); setBusy(false); setMessage("If the account is eligible, a password-reset email has been sent."); }
  return <form onSubmit={submit} className="space-y-4"><Field name="email" label="Account email" type="email" />{message && <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Requesting…" : "Send reset link"}</button></form>;
}

export function ResetPasswordForm({ email, token }: { email: string; token: string }) {
  const router = useRouter(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(""); const data = new FormData(event.currentTarget); const response = await fetch("/api/auth/reset-password", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, token, newPassword: data.get("newPassword") }) }); setBusy(false); if (!response.ok) { setError("The reset link is invalid or the password does not meet security requirements."); return; } router.replace("/platform-login"); }
  if (!email || !token) return <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">This password-reset link is incomplete.</p>;
  return <form onSubmit={submit} className="space-y-4"><Field name="newPassword" label="New password" type="password" />{error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Updating…" : "Set password"}</button></form>;
}

function Field({ name, label, type = "text", required = true }: { name: string; label: string; type?: string; required?: boolean }) { return <label className="block text-sm font-semibold text-slate-700">{label}<input name={name} type={type} required={required} className="mt-1 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" /></label>; }
