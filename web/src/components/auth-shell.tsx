import Image from "next/image";
import Link from "next/link";
import { ReactNode } from "react";
import { BrandLogo } from "@/components/brand-logo";

export function AuthShell({ eyebrow, title, introduction, imageSrc, imageAlt, panelEyebrow, panelTitle, panelDescription, children }: { eyebrow: string; title: string; introduction: string; imageSrc: string; imageAlt: string; panelEyebrow: string; panelTitle: string; panelDescription: string; children: ReactNode }) {
  return <main className="min-h-screen bg-[#edf0ea] p-0 lg:p-5">
    <div className="mx-auto grid min-h-screen max-w-[100rem] overflow-hidden bg-white shadow-[0_35px_100px_rgba(18,55,42,.15)] lg:min-h-[calc(100vh-2.5rem)] lg:grid-cols-[.94fr_1.06fr] lg:rounded-[2rem]">
      <section className="relative hidden overflow-hidden bg-[#12372a] text-white lg:block"><Image src={imageSrc} alt={imageAlt} fill priority sizes="55vw" className="object-cover object-center" /><div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(9,37,28,.16),rgba(9,37,28,.92))]" /><div className="relative flex h-full flex-col justify-between p-12 xl:p-16"><Link href="/" aria-label="GiddyEdu home" className="self-start"><BrandLogo iconClassName="size-14" wordmarkClassName="text-3xl" /></Link><div className="max-w-xl"><p className="text-xs font-black uppercase tracking-[.22em] text-[#f4c95d]">{panelEyebrow}</p><p className="mt-5 text-4xl font-black leading-[1.02] tracking-[-.05em] xl:text-5xl">{panelTitle}</p><p className="mt-5 max-w-lg leading-7 text-white/68">{panelDescription}</p></div></div></section>
      <section className="flex items-center px-5 py-10 sm:px-10 lg:px-14 xl:px-20"><div className="mx-auto w-full max-w-lg"><Link href="/" aria-label="GiddyEdu home" className="mb-8 inline-flex lg:hidden"><BrandLogo tone="dark" iconClassName="size-12" wordmarkClassName="text-2xl" /></Link><div><Link href="/" className="group inline-flex items-center gap-2 rounded-full border border-[#d8e0da] bg-[#f8faf7] px-4 py-2 text-sm font-bold text-[#405148] transition hover:border-[#99b2a2] hover:bg-white hover:text-[#12372a]"><span aria-hidden="true" className="transition group-hover:-translate-x-0.5">←</span> Back to home</Link></div><p className="mt-8 text-xs font-black uppercase tracking-[.2em] text-[#2f6d52]">{eyebrow}</p><h1 className="mt-4 text-4xl font-black leading-none tracking-[-.055em] sm:text-5xl">{title}</h1><p className="mt-4 leading-7 text-[#66736c]">{introduction}</p><div className="mt-9">{children}</div></div></section>
    </div>
  </main>;
}
