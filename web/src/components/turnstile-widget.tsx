"use client";

import Script from "next/script";

const siteKey = process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY;

export function TurnstileWidget({ action }: { action: string }) {
  if (!siteKey) return <p role="alert" className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-xs leading-5 text-amber-900">The security check is temporarily unavailable. Please try again later.</p>;

  return <>
    <Script src="https://challenges.cloudflare.com/turnstile/v0/api.js" strategy="afterInteractive" />
    <div className="cf-turnstile" data-sitekey={siteKey} data-theme="light" data-action={action} />
  </>;
}
