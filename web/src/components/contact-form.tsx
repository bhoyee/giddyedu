"use client";

import Script from "next/script";
import { FormEvent, useState } from "react";

const turnstileSiteKey = process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY;

export function ContactForm() {
  const [notice, setNotice] = useState("");

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice("This enquiry has not been sent. Online delivery will be enabled when the contact service is connected.");
  }

  return <>
    {turnstileSiteKey && <Script src="https://challenges.cloudflare.com/turnstile/v0/api.js" strategy="afterInteractive" />}
    <form onSubmit={submit} className="rounded-[2rem] border border-[#dce3dd] bg-white p-6 shadow-[0_24px_70px_rgba(18,55,42,.1)] sm:p-9">
      <div className="grid gap-6 sm:grid-cols-2">
        <Field label="Full name" name="fullName" autoComplete="name" />
        <Field label="Email address" name="email" type="email" autoComplete="email" />
        <label className="sm:col-span-2"><span className="text-sm font-black text-[#26352d]">Subject</span><select name="subject" required defaultValue="" className="mt-2 min-h-13 w-full rounded-xl border border-[#cbd5ce] bg-[#fbfaf6] px-4 text-sm outline-none transition focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10"><option value="" disabled>Select an enquiry type</option><option>Sales enquiry</option><option>Technical support</option><option>Partnership opportunity</option><option>Press and media</option><option>Other</option></select></label>
        <label className="sm:col-span-2"><span className="text-sm font-black text-[#26352d]">Message</span><textarea name="message" required minLength={20} rows={7} placeholder="Tell us how we can help" className="mt-2 w-full resize-y rounded-xl border border-[#cbd5ce] bg-[#fbfaf6] px-4 py-3 text-sm leading-6 outline-none transition placeholder:text-[#8b978f] focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10" /></label>
      </div>
      <div className="mt-6">{turnstileSiteKey ? <div className="cf-turnstile" data-sitekey={turnstileSiteKey} data-theme="light" data-action="contact" /> : <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-xs leading-5 text-amber-900">Cloudflare Turnstile is ready to activate when <code>NEXT_PUBLIC_TURNSTILE_SITE_KEY</code> is configured.</div>}</div>
      {notice && <p role="status" className="mt-5 rounded-xl bg-[#edf4ef] px-4 py-3 text-sm font-semibold text-[#24553f]">{notice}</p>}
      <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between"><p className="max-w-md text-xs leading-5 text-[#748078]">All fields are required. Do not include passwords, payment details or sensitive student information.</p><button type="submit" className="min-h-13 rounded-full bg-[#12372a] px-7 text-sm font-black text-white transition hover:-translate-y-0.5 hover:bg-[#1d513d]">Submit enquiry</button></div>
    </form>
  </>;
}

function Field({ label, name, type = "text", autoComplete }: { label: string; name: string; type?: string; autoComplete?: string }) {
  return <label><span className="text-sm font-black text-[#26352d]">{label}</span><input name={name} type={type} required autoComplete={autoComplete} className="mt-2 min-h-13 w-full rounded-xl border border-[#cbd5ce] bg-[#fbfaf6] px-4 text-sm outline-none transition focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10" /></label>;
}
