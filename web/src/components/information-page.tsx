import Link from "next/link";
import { LandingHeader } from "@/components/landing-header";
import { LandingFooter } from "@/components/landing-footer";

export type InformationSection = { title: string; description: string };

export function InformationPage({ eyebrow, title, introduction, sections, actionLabel = "Register your school", actionHref = "/register" }: { eyebrow: string; title: string; introduction: string; sections: InformationSection[]; actionLabel?: string; actionHref?: string }) {
  return <main className="min-h-screen bg-[#fbfaf6] text-[#17221d]">
    <LandingHeader />
    <header className="bg-[#12372a] px-5 pb-20 pt-36 text-white sm:px-8 lg:px-12"><div className="mx-auto max-w-6xl"><p className="text-xs font-black uppercase tracking-[.2em] text-[#f4c95d]">{eyebrow}</p><h1 className="mt-5 max-w-4xl text-5xl font-black leading-[.95] tracking-[-.055em] sm:text-7xl">{title}</h1><p className="mt-7 max-w-3xl text-lg leading-8 text-white/68">{introduction}</p></div></header>
    <section className="mx-auto max-w-6xl px-5 py-16 sm:px-8 lg:py-24"><div className="grid border-t border-[#d7ded8] md:grid-cols-2">{sections.map((section) => <article key={section.title} className="border-b border-[#d7ded8] py-9 md:odd:pr-10 md:even:border-l md:even:pl-10"><h2 className="text-xl font-black tracking-[-.03em]">{section.title}</h2><p className="mt-3 max-w-xl leading-7 text-[#5e6c64]">{section.description}</p></article>)}</div><div className="mt-12 flex flex-wrap gap-3"><Link href={actionHref} className="rounded-full bg-[#12372a] px-6 py-3 font-bold text-white transition hover:bg-[#1d513d]">{actionLabel}</Link><Link href="/" className="rounded-full border border-[#bdc9c1] px-6 py-3 font-bold text-[#12372a] transition hover:bg-white">Return home</Link></div></section>
    <LandingFooter />
  </main>;
}
