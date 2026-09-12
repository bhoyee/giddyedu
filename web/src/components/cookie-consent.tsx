"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

type ConsentPreferences = { analytics: boolean; marketing: boolean };
const consentCookieName = "giddyedu_cookie_consent";
const defaultPreferences: ConsentPreferences = { analytics: false, marketing: false };

function readConsent(): ConsentPreferences | null {
  const value = document.cookie.split("; ").find((entry) => entry.startsWith(`${consentCookieName}=`))?.split("=")[1];
  if (!value) return null;
  try {
    const parsed = JSON.parse(decodeURIComponent(value)) as Partial<ConsentPreferences>;
    return { analytics: parsed.analytics === true, marketing: parsed.marketing === true };
  } catch { return null; }
}

function saveConsent(preferences: ConsentPreferences) {
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${consentCookieName}=${encodeURIComponent(JSON.stringify(preferences))}; Max-Age=31536000; Path=/; SameSite=Lax${secure}`;
  window.dispatchEvent(new CustomEvent("giddyedu:cookie-consent-changed", { detail: preferences }));
}

export function CookieConsent() {
  const [visible, setVisible] = useState(false);
  const [customising, setCustomising] = useState(false);
  const [preferences, setPreferences] = useState<ConsentPreferences>(defaultPreferences);

  useEffect(() => {
    const openSettings = () => { setPreferences(readConsent() ?? defaultPreferences); setCustomising(true); setVisible(true); };
    window.addEventListener("giddyedu:open-cookie-settings", openSettings);
    const initialise = window.requestAnimationFrame(() => {
      const stored = readConsent();
      if (stored) setPreferences(stored); else setVisible(true);
    });
    return () => { window.cancelAnimationFrame(initialise); window.removeEventListener("giddyedu:open-cookie-settings", openSettings); };
  }, []);

  const choose = (next: ConsentPreferences) => { saveConsent(next); setPreferences(next); setVisible(false); setCustomising(false); };
  if (!visible) return null;

  return <div className="fixed inset-0 z-[100] flex items-end bg-[#071c16]/35 p-3 backdrop-blur-[2px] sm:p-5" role="presentation">
    <section role="dialog" aria-modal="true" aria-labelledby="cookie-consent-title" className="mx-auto w-full max-w-5xl rounded-[1.75rem] border border-[#dbe2dc] bg-[#fbfaf6] p-6 text-[#17221d] shadow-[0_30px_90px_rgba(7,28,22,.3)] sm:p-8">
      <div className="grid gap-7 lg:grid-cols-[1fr_auto] lg:items-end">
        <div><p className="text-xs font-black uppercase tracking-[.18em] text-[#2f6d52]">Your privacy</p><h2 id="cookie-consent-title" className="mt-2 text-2xl font-black tracking-[-.04em]">Cookies, with a clear choice.</h2><p className="mt-3 max-w-3xl text-sm leading-6 text-[#5e6c64]">We use essential cookies to keep GiddyEdu secure and working. With your permission, optional analytics cookies may help us understand site use and marketing cookies may support relevant communications. You can change your choice at any time. Read our <Link href="/cookies" className="font-bold text-[#1f6747] underline underline-offset-2">Cookie Policy</Link>.</p></div>
        {!customising && <div className="flex flex-col-reverse gap-2 sm:flex-row lg:justify-end"><button type="button" onClick={() => choose(defaultPreferences)} className="rounded-full border border-[#aebcb3] px-5 py-3 text-sm font-bold transition hover:bg-white">Reject optional</button><button type="button" onClick={() => setCustomising(true)} className="rounded-full border border-[#aebcb3] px-5 py-3 text-sm font-bold transition hover:bg-white">Manage choices</button><button type="button" onClick={() => choose({ analytics: true, marketing: true })} className="rounded-full bg-[#12372a] px-5 py-3 text-sm font-black text-white transition hover:bg-[#1d513d]">Accept all</button></div>}
      </div>
      {customising && <div className="mt-7 border-t border-[#dbe2dc] pt-6"><div className="grid gap-3 md:grid-cols-3"><ConsentOption title="Essential" description="Required for security, sessions and core site operation." checked disabled onChange={() => undefined} /><ConsentOption title="Analytics" description="Helps measure site use and improve journeys." checked={preferences.analytics} onChange={(analytics) => setPreferences((current) => ({ ...current, analytics }))} /><ConsentOption title="Marketing" description="Supports relevant campaign measurement and communications." checked={preferences.marketing} onChange={(marketing) => setPreferences((current) => ({ ...current, marketing }))} /></div><div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><button type="button" onClick={() => choose(defaultPreferences)} className="rounded-full border border-[#aebcb3] px-5 py-3 text-sm font-bold">Reject optional</button><button type="button" onClick={() => choose(preferences)} className="rounded-full bg-[#12372a] px-6 py-3 text-sm font-black text-white">Save preferences</button></div></div>}
    </section>
  </div>;
}

function ConsentOption({ title, description, checked, disabled = false, onChange }: { title: string; description: string; checked: boolean; disabled?: boolean; onChange: (checked: boolean) => void }) {
  return <label className="flex cursor-pointer items-start justify-between gap-4 rounded-2xl border border-[#dbe2dc] bg-white p-4"><span><span className="block text-sm font-black">{title}</span><span className="mt-1 block text-xs leading-5 text-[#66736c]">{description}</span></span><input type="checkbox" className="mt-1 size-5 accent-[#12372a]" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /></label>;
}
