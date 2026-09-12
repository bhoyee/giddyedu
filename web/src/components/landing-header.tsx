"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { BrandLogo } from "@/components/brand-logo";

const links = [
  { href: "/features", label: "Features" },
  { href: "/#everyone", label: "For everyone" },
  { href: "/#mobile", label: "Mobile apps" },
];

export function LandingHeader() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === "Escape") setOpen(false); };
    document.addEventListener("keydown", closeOnEscape);
    document.body.style.overflow = "hidden";
    return () => { document.removeEventListener("keydown", closeOnEscape); document.body.style.overflow = ""; };
  }, [open]);

  return <header className="absolute inset-x-0 top-0 z-40">
    <div className="mx-auto flex max-w-[90rem] items-center justify-between px-4 py-4 sm:px-6 sm:py-5 lg:px-12">
      <Link href="/" aria-label="GiddyEdu home" className="relative z-50 flex min-w-0 items-center gap-2.5 rounded-xl text-white transition hover:opacity-85 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#f4c95d] sm:gap-3">
        <BrandLogo iconClassName="size-11 sm:size-13" wordmarkClassName="text-2xl sm:text-[1.7rem]" />
      </Link>

      <nav aria-label="Main navigation" className="hidden items-center gap-2 text-sm font-semibold text-white/80 lg:flex">{links.map(link => <a key={link.href} href={link.href} className="rounded-full px-4 py-2.5 transition duration-200 hover:-translate-y-0.5 hover:bg-white/10 hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#f4c95d]">{link.label}</a>)}</nav>

      <div className="hidden items-center gap-2 md:flex sm:gap-3"><Link className="rounded-full px-4 py-2.5 text-sm font-bold text-white transition hover:bg-white/10 sm:px-5" href="/login">Sign in</Link><Link className="rounded-full bg-[#f4c95d] px-5 py-2.5 text-sm font-black text-[#12372a] shadow-lg shadow-black/10 transition hover:-translate-y-0.5 hover:bg-[#ffda72] sm:px-6" href="/register">Register school</Link></div>

      <button type="button" aria-expanded={open} aria-controls="mobile-navigation" aria-label={open ? "Close navigation" : "Open navigation"} onClick={() => setOpen(value => !value)} className="relative z-50 grid size-11 place-items-center rounded-full border border-white/25 bg-[#12372a]/70 text-white backdrop-blur-md transition hover:bg-[#12372a] md:hidden">
        <span className="relative block h-4 w-5"><span className={`absolute left-0 top-0.5 h-0.5 w-5 rounded bg-current transition ${open ? "translate-y-1.5 rotate-45" : ""}`} /><span className={`absolute left-0 top-[7px] h-0.5 w-5 rounded bg-current transition ${open ? "opacity-0" : ""}`} /><span className={`absolute bottom-0.5 left-0 h-0.5 w-5 rounded bg-current transition ${open ? "-translate-y-1.5 -rotate-45" : ""}`} /></span>
      </button>
    </div>

    <div id="mobile-navigation" className={`fixed inset-0 z-40 bg-[#0d3024] px-5 pb-8 pt-24 text-white transition duration-300 md:hidden ${open ? "visible opacity-100" : "invisible opacity-0"}`}>
      <nav aria-label="Mobile navigation" className="mx-auto flex h-full max-w-lg flex-col">
        <div className="border-t border-white/15">{links.map(link => <a key={link.href} href={link.href} onClick={() => setOpen(false)} className="block border-b border-white/15 px-3 py-5 text-2xl font-black tracking-[-.03em] transition duration-200 hover:bg-white/10 hover:pl-6 hover:text-[#f4c95d] focus-visible:bg-white/10 focus-visible:text-[#f4c95d] focus-visible:outline-none">{link.label}</a>)}</div>
        <div className="mt-auto grid gap-3 pt-8"><Link href="/register" onClick={() => setOpen(false)} className="flex min-h-14 items-center justify-center rounded-full bg-[#f4c95d] px-6 text-center font-black text-[#12372a]">Register your school</Link><Link href="/login" onClick={() => setOpen(false)} className="flex min-h-14 items-center justify-center rounded-full border border-white/25 px-6 text-center font-bold">Sign in</Link></div>
      </nav>
    </div>
  </header>;
}
