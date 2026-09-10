import Image from "next/image";
import Link from "next/link";
import { LandingHeader } from "@/components/landing-header";

const roles = ["School leaders", "Administrators", "Teachers", "Bursars", "Parents", "Students"];

const featureGlance = [
  { name: "Student records", tone: "bg-[#e5f0e8] text-[#1f6747]", path: "M16 20v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8M18 8h4M20 6v4" },
  { name: "Admissions", tone: "bg-[#e2edf7] text-[#226384]", path: "M4 3h16v18H4zM8 7h8M8 11h5M8 15h3M16 14v5M13.5 16.5h5" },
  { name: "Academics", tone: "bg-[#f8ead1] text-[#8b5b17]", path: "m3 7 9-4 9 4-9 4-9-4Zm3 2v6c3 3 9 3 12 0V9M21 7v6" },
  { name: "Attendance", tone: "bg-[#e4efee] text-[#24645f]", path: "M8 3v3M16 3v3M4 8h16v13H4zM4 11h16M8 16l2 2 5-5" },
  { name: "Timetable", tone: "bg-[#f3e5ea] text-[#84435c]", path: "M6 3v3M18 3v3M3 8h18v13H3zM7 12h3M14 12h3M7 16h3M14 16h3" },
  { name: "Assignments", tone: "bg-[#eee7f5] text-[#674589]", path: "M6 3h12v18H6zM9 3v4h6V3M9 11h6M9 15h4" },
  { name: "Exams & gradebook", tone: "bg-[#e7ecf5] text-[#4b6089]", path: "M5 3h14v18H5zM9 7h6M9 11h6M9 15h3M15 14l1 1 2-3" },
  { name: "Fees & payments", tone: "bg-[#f7e8d9] text-[#88512f]", path: "M3 6h18v13H3zM3 10h18M7 15h4M16 15h1" },
  { name: "Staff & HR", tone: "bg-[#e6f0e5] text-[#426b3c]", path: "M15 20v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M8 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8M17 11h5M19.5 8.5v5" },
  { name: "Parent portal", tone: "bg-[#f6ebce] text-[#87651e]", path: "M16 20v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8M17 8a3 3 0 1 0 0-6M18 14a4 4 0 0 1 4 4v2" },
  { name: "Communication", tone: "bg-[#e2eff2] text-[#286778]", path: "M21 15a4 4 0 0 1-4 4H8l-5 3v-7a4 4 0 0 1-1-3V7a4 4 0 0 1 4-4h11a4 4 0 0 1 4 4zM7 9h10M7 13h6" },
  { name: "Campus operations", tone: "bg-[#eee9df] text-[#655b49]", path: "M3 21h18M5 21V9l7-5 7 5v12M8 12h3v3H8zM14 12h2v6h-2" },
];

function ArrowIcon() {
  return <svg aria-hidden="true" viewBox="0 0 20 20" className="size-4 fill-none stroke-current stroke-2"><path d="M4 10h11M11 5l5 5-5 5" /></svg>;
}

function CheckIcon() {
  return <svg aria-hidden="true" viewBox="0 0 20 20" className="size-5 fill-none stroke-current stroke-2"><path d="m4 10 4 4 8-9" /></svg>;
}

export default function Home() {
  return <main className="overflow-hidden bg-[#fbfaf6] text-[#17221d]">
    <LandingHeader />

    <section className="relative min-h-[760px] bg-[#12372a] text-white lg:min-h-[820px]">
      <Image src="/giddyedu-school-operations-v2.png" alt="A GiddyEdu school administrator working while students and a teacher collaborate nearby" fill priority sizes="100vw" className="object-cover object-[68%_center] opacity-55 lg:object-center" />
      <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(10,42,31,.98)_0%,rgba(10,42,31,.91)_38%,rgba(10,42,31,.22)_74%,rgba(10,42,31,.12)_100%)]" />
      <div className="absolute inset-x-0 bottom-0 h-48 bg-gradient-to-t from-[#12372a] to-transparent" />
      <div className="relative mx-auto flex min-h-[760px] max-w-[90rem] items-center px-5 pb-24 pt-36 sm:px-8 lg:min-h-[820px] lg:px-12">
        <div className="max-w-3xl">
          <h1 className="text-[clamp(3rem,7vw,7.2rem)] font-black leading-[.9] tracking-[-.06em]">Run your school.<br /><span className="font-serif font-normal italic text-[#f4c95d]">Move learning forward.</span></h1>
          <p className="mt-8 max-w-xl text-lg leading-8 text-white/76 sm:text-xl">GiddyEdu brings admissions, people, academics and school operations together—so your team spends less time chasing records and more time supporting every learner.</p>
          <div className="mt-10 flex flex-col gap-3 sm:flex-row"><Link href="/register" className="inline-flex items-center justify-center gap-3 rounded-full bg-[#f4c95d] px-7 py-4 font-black text-[#12372a] shadow-xl shadow-black/20 transition hover:-translate-y-0.5 hover:bg-[#ffda72]">Start with your school <ArrowIcon /></Link><Link href="/features" className="inline-flex items-center justify-center rounded-full border border-white/30 bg-white/5 px-7 py-4 font-bold text-white backdrop-blur-md transition hover:bg-white/12">Explore all features</Link></div>
          <div className="mt-12 flex flex-wrap gap-x-7 gap-y-3 text-sm font-semibold text-white/70">{["Multi-campus ready", "Permission-aware", "Works across devices"].map(item => <span key={item} className="flex items-center gap-2"><span className="text-[#f4c95d]"><CheckIcon /></span>{item}</span>)}</div>
        </div>
      </div>
    </section>

    <section className="relative z-10 mx-auto -mt-14 max-w-[86rem] px-5 sm:px-8">
      <div className="grid overflow-hidden rounded-[2rem] border border-black/5 bg-white shadow-[0_30px_80px_rgba(18,55,42,.14)] md:grid-cols-[.9fr_1.1fr]">
        <div className="flex flex-col justify-between bg-[#f1eddf] p-8 sm:p-10 lg:p-14"><div><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">A clearer school day</p><h2 className="mt-4 text-3xl font-black leading-tight tracking-[-.04em] sm:text-4xl">Everything connected. Nothing buried.</h2></div><p className="mt-8 max-w-md leading-7 text-[#53625a]">From the first application to the first day in class, each action builds on the same trusted school record.</p></div>
        <div className="grid grid-cols-2 gap-px bg-slate-200 sm:grid-cols-4">{[["Applications", "Review & decide"], ["Students", "Enrol & support"], ["Staff", "Organise & empower"], ["Families", "Connect & inform"]].map(([title, detail]) => <div key={title} className="bg-white p-6 sm:px-5 sm:py-10 lg:p-8"><h3 className="mt-8 font-black">{title}</h3><p className="mt-2 text-sm leading-6 text-slate-500">{detail}</p></div>)}</div>
      </div>
    </section>

    <section id="features" className="mx-auto max-w-[90rem] px-5 py-28 sm:px-8 lg:px-12 lg:py-36">
      <div className="mx-auto max-w-4xl text-center"><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">A complete school operating system</p><h2 className="mt-5 text-4xl font-black leading-[.98] tracking-[-.055em] sm:text-6xl">The tools your school relies on, connected.</h2><p className="mx-auto mt-6 max-w-3xl text-lg leading-8 text-[#66736c]">Choose what your school needs and add more as you grow—without splitting your people, processes or records across different systems.</p></div>
      <div className="mt-16 grid grid-cols-2 gap-x-4 gap-y-10 sm:grid-cols-3 sm:gap-y-14 lg:grid-cols-6">{featureGlance.map(feature => <Link href="/features" key={feature.name} className="group flex min-w-0 flex-col items-center rounded-3xl px-2 py-3 text-center transition duration-300 hover:-translate-y-1 hover:bg-white hover:shadow-[0_18px_45px_rgba(18,55,42,.08)] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#2f6d52] sm:px-4 sm:py-5"><span className={`grid size-20 place-items-center rounded-[1.65rem] transition duration-300 group-hover:scale-105 group-hover:rounded-[2rem] sm:size-24 ${feature.tone}`}><svg aria-hidden="true" viewBox="0 0 24 24" className="size-10 fill-none stroke-current stroke-[1.55] sm:size-12" strokeLinecap="round" strokeLinejoin="round"><path d={feature.path} /></svg></span><h3 className="mt-5 text-sm font-black leading-tight tracking-[-.02em] sm:text-base">{feature.name}</h3></Link>)}</div>
      <div className="mt-16 text-center"><Link href="/features" className="inline-flex items-center gap-3 rounded-full bg-[#12372a] px-7 py-4 font-black text-white transition hover:-translate-y-0.5 hover:bg-[#1d513d] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#2f6d52]">Explore every feature <ArrowIcon /></Link></div>
    </section>

    <section id="everyone" className="bg-[#e9efe9] py-28 lg:py-36"><div className="mx-auto grid max-w-[90rem] gap-14 px-5 sm:px-8 lg:grid-cols-2 lg:items-center lg:px-12">
      <div className="relative overflow-hidden rounded-[2.5rem] bg-[#12372a] p-8 text-white sm:p-12"><div className="absolute -right-16 -top-16 size-56 rounded-full border-[40px] border-white/5" /><p className="relative text-xs font-black uppercase tracking-[.22em] text-[#f4c95d]">One school. Many perspectives.</p><div className="relative mt-10 grid grid-cols-2 gap-3 sm:grid-cols-3">{roles.map(role => <div key={role} className={`rounded-2xl border p-4 text-sm font-bold ${role === roles[0] ? "border-[#f4c95d]/60 bg-[#f4c95d] text-[#12372a]" : "border-white/15 bg-white/7"}`}>{role}</div>)}</div></div>
      <div className="lg:pl-10"><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">Made for everyone</p><h2 className="mt-5 text-4xl font-black leading-none tracking-[-.05em] sm:text-6xl">The right workspace for every role.</h2><p className="mt-7 max-w-xl text-lg leading-8 text-[#5d6a63]">Leaders see the whole school. Teachers stay focused on learning. Families stay connected. Each workspace adapts to responsibilities, permissions and the services your school enables.</p><Link href="/login" className="mt-9 inline-flex items-center gap-3 font-black text-[#174d38]">Sign in to your workspace <ArrowIcon /></Link></div>
    </div></section>

    <section id="trust" className="mx-auto grid max-w-[90rem] gap-14 px-5 py-28 sm:px-8 lg:grid-cols-[1fr_1.1fr] lg:items-center lg:px-12 lg:py-36">
      <div><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">Confidence built in</p><h2 className="mt-5 text-4xl font-black leading-none tracking-[-.05em] sm:text-6xl">Your community&apos;s data deserves care.</h2><p className="mt-7 max-w-xl text-lg leading-8 text-[#66736c]">GiddyEdu is designed around clear access, secure school boundaries and accountable actions—not added as an afterthought.</p></div>
      <div className="grid gap-4 sm:grid-cols-2">{[["School data stays separated", "Tenant-aware records and server-side controls help keep each school’s information within the right boundary."], ["Access follows responsibility", "Permissions and feature access are checked by the platform, not simply hidden in the interface."], ["Important actions are traceable", "Audit-ready foundations preserve accountability across sensitive school operations."], ["Ready wherever work happens", "Responsive web and installable PWA foundations support school teams across devices."]].map(([title, detail]) => <article key={title} className="rounded-3xl bg-[#f1eddf] p-7"><span className="grid size-10 place-items-center rounded-full bg-[#12372a] text-[#f4c95d]"><CheckIcon /></span><h3 className="mt-7 text-lg font-black">{title}</h3><p className="mt-3 text-sm leading-6 text-[#66736c]">{detail}</p></article>)}</div>
    </section>

    <section className="px-5 pb-8 sm:px-8"><div className="relative mx-auto max-w-[86rem] overflow-hidden rounded-[2.5rem] bg-[#12372a] px-6 py-20 text-center text-white sm:px-12 lg:py-28"><div className="absolute left-1/2 top-0 h-px w-2/3 -translate-x-1/2 bg-gradient-to-r from-transparent via-[#f4c95d] to-transparent" /><p className="text-xs font-black uppercase tracking-[.22em] text-[#f4c95d]">Bring your school together</p><h2 className="mx-auto mt-5 max-w-3xl text-4xl font-black leading-none tracking-[-.05em] sm:text-6xl">A better-run school starts with one clear system.</h2><p className="mx-auto mt-6 max-w-xl leading-7 text-white/65">Set up your school, invite your team and create a stronger operational foundation for every learner.</p><Link href="/register" className="mt-9 inline-flex items-center gap-3 rounded-full bg-[#f4c95d] px-8 py-4 font-black text-[#12372a] transition hover:-translate-y-0.5 hover:bg-[#ffda72]">Register your school <ArrowIcon /></Link></div></section>

    <footer className="mx-auto flex max-w-[90rem] flex-col gap-5 px-5 py-10 text-sm text-[#68746e] sm:flex-row sm:items-center sm:justify-between sm:px-8 lg:px-12"><div className="flex items-center gap-2 font-black text-[#12372a]"><span className="grid size-8 place-items-center rounded-lg bg-[#12372a] text-white">G</span>GiddyEdu</div><p>School operations, connected with care.</p><div className="flex gap-5"><Link href="/login">Sign in</Link><Link href="/platform-login">Platform access</Link></div></footer>
  </main>;
}
