import Link from "next/link";
import { FeatureCatalogue } from "@/components/feature-catalogue";
import { LandingHeader } from "@/components/landing-header";
import { featureGroups } from "@/lib/product-features";

export default function FeaturesPage() {
  return <main className="min-h-screen bg-[#fbfaf6] text-[#17221d]">
    <LandingHeader />
    <section className="bg-[#12372a] px-5 pb-24 pt-36 text-white sm:px-8 lg:px-12 lg:pb-32 lg:pt-44">
      <div className="mx-auto max-w-[86rem]"><p className="text-xs font-black uppercase tracking-[.22em] text-[#f4c95d]">The complete GiddyEdu vision</p><div className="mt-6 grid gap-8 lg:grid-cols-[1.15fr_.85fr] lg:items-end"><h1 className="max-w-4xl text-5xl font-black leading-[.92] tracking-[-.06em] sm:text-7xl lg:text-8xl">Everything your school needs to move forward.</h1><div className="lg:justify-self-end"><p className="max-w-lg text-lg leading-8 text-white/70">Explore connected capabilities spanning the learner journey, teaching, finance, people, campus operations and school intelligence.</p><Link href="/register" className="mt-8 inline-flex rounded-full bg-[#f4c95d] px-7 py-4 font-black text-[#12372a]">Register your school</Link></div></div></div>
    </section>
    <section className="mx-auto max-w-[90rem] px-5 py-20 sm:px-8 lg:px-12 lg:py-28">
      <FeatureCatalogue groups={featureGroups} />
    </section>
    <section className="px-5 pb-8 sm:px-8"><div className="mx-auto max-w-[86rem] rounded-[2.5rem] bg-[#e9efe9] px-6 py-16 text-center sm:px-12 lg:py-20"><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">One connected school system</p><h2 className="mx-auto mt-5 max-w-3xl text-4xl font-black leading-none tracking-[-.05em] sm:text-6xl">Build around your school. Grow without starting over.</h2><p className="mx-auto mt-6 max-w-2xl leading-7 text-[#66736c]">GiddyEdu’s suites share one identity, permission model and trusted school record, so capabilities work together as your needs grow.</p><Link href="/register" className="mt-9 inline-flex rounded-full bg-[#12372a] px-8 py-4 font-black text-white">Start with GiddyEdu</Link></div></section>
    <footer className="mx-auto flex max-w-[90rem] flex-col gap-4 px-5 py-10 text-sm text-[#68736d] sm:flex-row sm:items-center sm:justify-between sm:px-8 lg:px-12"><Link href="/" className="font-black text-[#12372a]">GiddyEdu</Link><p>Every suite. One school operating platform.</p><Link href="/login" className="font-bold text-[#12372a]">Sign in</Link></footer>
  </main>;
}
