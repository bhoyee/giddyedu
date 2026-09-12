import Link from "next/link";
import { LandingHeader } from "@/components/landing-header";
import { LandingFooter } from "@/components/landing-footer";

export type LegalSection = { title: string; paragraphs: string[] };

export function LegalPage({ title, introduction, sections }: { title: string; introduction: string; sections: LegalSection[] }) {
  return <main className="min-h-screen bg-[#fbfaf6] text-[#17221d]"><LandingHeader /><header className="bg-[#12372a] px-5 pb-20 pt-36 text-white sm:px-8 lg:px-12"><div className="mx-auto max-w-5xl"><p className="text-xs font-black uppercase tracking-[.2em] text-[#f4c95d]">GiddyEdu legal</p><h1 className="mt-5 text-5xl font-black tracking-[-.055em] sm:text-7xl">{title}</h1><p className="mt-6 max-w-3xl text-lg leading-8 text-white/68">{introduction}</p><p className="mt-6 text-sm font-semibold text-white/45">Last updated 11 September 2026</p></div></header><article className="mx-auto max-w-5xl px-5 py-16 sm:px-8 lg:py-24">{sections.map(section => <section key={section.title} className="grid gap-4 border-b border-[#dfe5df] py-9 first:pt-0 lg:grid-cols-[.65fr_1.35fr] lg:gap-12"><h2 className="text-xl font-black tracking-[-.03em]">{section.title}</h2><div className="space-y-4 text-base leading-7 text-[#5e6c64]">{section.paragraphs.map(paragraph => <p key={paragraph}>{paragraph}</p>)}</div></section>)}<div className="mt-12 flex flex-wrap gap-3"><Link href="/" className="rounded-full bg-[#12372a] px-6 py-3 font-bold text-white">Return home</Link><Link href="/register" className="rounded-full border border-[#bdc9c1] px-6 py-3 font-bold text-[#12372a]">Register your school</Link></div></article><LandingFooter /></main>;
}
