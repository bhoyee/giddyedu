"use client";

import Link from "next/link";
import { useState } from "react";

export function ConfirmEmailForm({ email, token }: { email: string; token: string }) {
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState("");
  const linkIsComplete = Boolean(email && token);

  async function confirm() {
    setBusy(true);
    setError("");
    try {
      const response = await fetch("/api/auth/confirm-email", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, token }),
      });
      if (!response.ok) {
        setError("This confirmation link is invalid or has expired.");
        return;
      }
      setConfirmed(true);
    } catch {
      setError("Email confirmation is temporarily unavailable. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  if (!linkIsComplete) return <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">The confirmation link is incomplete.</p>;
  if (confirmed) return <div className="rounded-xl bg-emerald-50 p-5 text-emerald-900"><p>Your email address is confirmed.</p><Link href="/login" className="mt-4 inline-block font-bold underline">Continue to sign in</Link></div>;

  return <div><p className="break-words text-sm text-slate-600">Confirm the GiddyEdu account registered for <strong>{email}</strong>.</p>{error && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}<button type="button" disabled={busy} onClick={() => void confirm()} className="mt-6 w-full rounded-xl bg-[#12372a] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? "Confirming..." : "Confirm email"}</button></div>;
}
