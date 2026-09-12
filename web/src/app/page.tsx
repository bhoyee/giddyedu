import Image from "next/image";
import Link from "next/link";
import { LandingHeader } from "@/components/landing-header";
import { LandingFooter } from "@/components/landing-footer";

const roles = [
  { name: "School leaders", detail: "See enrolment, people, operations and performance across the whole school." },
  { name: "Administrators", detail: "Keep daily records, processes and communication moving from one workspace." },
  { name: "Teachers", detail: "Stay close to classes, attendance, learning activities and every student’s progress." },
  { name: "Bursars", detail: "Bring fees, payments, balances and financial reporting into a clearer view." },
  { name: "Parents & guardians", detail: "Follow each child’s school life, payments and updates from one secure account." },
  { name: "Students", detail: "Reach learning, assignments, timetables, results and school services with less friction." },
];

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

    <section className="relative z-10 mx-auto -mt-14 max-w-[86rem] px-4 sm:px-8">
      <div className="grid gap-10 rounded-[2rem] border border-[#e1e6e2] bg-white px-6 py-10 shadow-[0_28px_75px_rgba(18,55,42,.14)] sm:px-10 sm:py-12 lg:grid-cols-[1.05fr_1.95fr] lg:items-center lg:gap-16 lg:px-12 lg:py-14">
        <div>
          <p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">One source of truth</p>
          <h2 className="mt-4 max-w-md text-3xl font-black leading-[1.04] tracking-[-.045em] sm:text-4xl">Your whole school, finally on the same page.</h2>
        </div>
        <div className="grid gap-7 sm:grid-cols-3 sm:gap-0">
          {[ ["Connected by design", "Admissions, student records and academics build on the same trusted information."], ["Clear by responsibility", "Each person gets a focused view shaped around the work they are trusted to do."], ["Ready to grow", "Add campuses, people and capabilities without rebuilding your school’s digital foundation."] ].map(([title, detail], index) => <div key={title} className={`group relative sm:px-7 ${index === 0 ? "sm:pl-0" : "sm:border-l sm:border-[#dfe5df]"} ${index === 2 ? "sm:pr-0" : ""}`}>
            <span className="mb-5 block h-1 w-9 rounded-full bg-[#f4c95d] transition-all duration-300 group-hover:w-14 group-hover:bg-[#2f6d52]" />
            <h3 className="text-base font-black tracking-[-.02em]">{title}</h3>
            <p className="mt-2 text-sm leading-6 text-[#66736c]">{detail}</p>
          </div>)}
        </div>
      </div>
    </section>

    <section id="features" className="mx-auto max-w-[90rem] px-5 py-28 sm:px-8 lg:px-12 lg:py-36">
      <div className="mx-auto max-w-4xl text-center"><p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">A complete school operating system</p><h2 className="mt-5 text-4xl font-black leading-[.98] tracking-[-.055em] sm:text-6xl">The tools your school relies on, connected.</h2><p className="mx-auto mt-6 max-w-3xl text-lg leading-8 text-[#66736c]">Choose what your school needs and add more as you grow—without splitting your people, processes or records across different systems.</p></div>
      <div className="mt-16 grid grid-cols-2 gap-x-4 gap-y-10 sm:grid-cols-3 sm:gap-y-14 lg:grid-cols-6">{featureGlance.map(feature => <Link href="/features" key={feature.name} className="group flex min-w-0 flex-col items-center rounded-3xl px-2 py-3 text-center transition duration-300 hover:-translate-y-1 hover:bg-white hover:shadow-[0_18px_45px_rgba(18,55,42,.08)] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#2f6d52] sm:px-4 sm:py-5"><span className={`grid size-20 place-items-center rounded-[1.65rem] transition duration-300 group-hover:scale-105 group-hover:rounded-[2rem] sm:size-24 ${feature.tone}`}><svg aria-hidden="true" viewBox="0 0 24 24" className="size-10 fill-none stroke-current stroke-[1.55] sm:size-12" strokeLinecap="round" strokeLinejoin="round"><path d={feature.path} /></svg></span><h3 className="mt-5 text-sm font-black leading-tight tracking-[-.02em] sm:text-base">{feature.name}</h3></Link>)}</div>
      <div className="mt-16 text-center"><Link href="/features" className="inline-flex items-center gap-3 rounded-full bg-[#12372a] px-7 py-4 font-black text-white transition hover:-translate-y-0.5 hover:bg-[#1d513d] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#2f6d52]">Explore every feature <ArrowIcon /></Link></div>
    </section>

    <section id="mobile" className="scroll-mt-0 overflow-hidden bg-[#0e3327] text-white">
      <div className="mx-auto grid max-w-[90rem] gap-16 px-5 py-24 sm:px-8 lg:grid-cols-[.9fr_1.1fr] lg:items-center lg:px-12 lg:py-32">
        <div>
          <p className="text-xs font-black uppercase tracking-[.22em] text-[#f4c95d]">Beyond the school office</p>
          <h2 className="mt-5 max-w-2xl text-4xl font-black leading-[.98] tracking-[-.055em] sm:text-6xl">School keeps moving. GiddyEdu moves with it.</h2>
          <p className="mt-7 max-w-xl text-lg leading-8 text-white/68">Give families and staff secure access wherever the school day happens—with experiences designed for mobile devices, uneven connectivity and less expensive data use.</p>
          <div className="mt-10 grid gap-x-8 gap-y-7 sm:grid-cols-2">
            {[ ["Universal Family App", "Parents and students can move between linked children and schools from one account."], ["Universal Staff App", "Teachers and staff get permission-aware tools shaped around their school responsibilities."], ["Offline-friendly work", "Supported workflows can continue through connection drops and synchronise when access returns."], ["Low-bandwidth by design", "Lean mobile experiences reduce unnecessary data use on slower or costly connections."], ["Installable web app", "Add GiddyEdu to a device home screen for fast access without a traditional app install."], ["School identity included", "Branding and enabled services adapt to the school and membership currently selected."] ].map(([title, detail]) => <div key={title} className="border-l border-white/15 pl-5 transition duration-300 hover:border-[#f4c95d]"><h3 className="font-black text-white">{title}</h3><p className="mt-2 text-sm leading-6 text-white/58">{detail}</p></div>)}
          </div>
        </div>

        <div className="relative mx-auto w-full max-w-2xl lg:justify-self-end">
          <div aria-hidden="true" className="absolute left-1/2 top-1/2 size-[30rem] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#2d7658]/40 blur-3xl" />
          <Image src="/giddyedu-mobile-apps.png" alt="GiddyEdu Family and Staff applications displayed on two smartphones" width={1280} height={1280} sizes="(max-width: 1024px) 90vw, 50vw" className="relative mx-auto h-auto w-full max-w-[620px] drop-shadow-[0_35px_45px_rgba(0,0,0,.28)]" />
          <div className="relative mx-auto mt-5 flex max-w-md flex-col justify-center gap-3 sm:flex-row">
            <div className="flex min-h-14 flex-1 items-center gap-3 rounded-xl border border-white/15 bg-white/10 px-4 py-2.5 backdrop-blur transition hover:bg-white/15"><svg aria-hidden="true" viewBox="0 0 24 24" className="size-7 shrink-0 fill-white"><path d="M17.05 20.28c-.98.95-2.05.8-3.08.35-1.09-.46-2.09-.48-3.24 0-1.44.62-2.2.44-3.06-.35C2.79 15.25 3.51 7.59 9.05 7.31c1.26-.07 2.14.69 2.88.69.7 0 2.01-.86 3.39-.73 1.2.05 2.29.48 3.13 1.29-2.72 1.63-2.07 5.22.42 6.22-.5 1.31-1.14 2.61-1.82 3.5ZM12.03 7.25C11.88 5.3 13.48 3.7 15.3 3.54c.25 2.25-2.04 3.93-3.27 3.71Z" /></svg><span><span className="block text-[9px] font-bold uppercase tracking-wider text-white/55">Designed for</span><span className="block text-sm font-black">iPhone & iPad</span></span></div>
            <div className="flex min-h-14 flex-1 items-center gap-3 rounded-xl border border-white/15 bg-white/10 px-4 py-2.5 backdrop-blur transition hover:bg-white/15"><svg aria-hidden="true" viewBox="0 0 32 36" className="size-7 shrink-0"><path fill="#00d7fe" d="M1.8 1.3 18.7 18 1.8 34.7A3.4 3.4 0 0 1 0 31.6V4.4c0-1.3.7-2.4 1.8-3.1Z"/><path fill="#ffce00" d="m23.9 12.9-5.2 5.1 5.2 5.1 6.1-3.5c2-1.1 2-2.9 0-4l-6.1-2.7Z"/><path fill="#00f076" d="M1.8 1.3c.8-.5 1.8-.4 2.8.1l19.3 11.5-5.2 5.1L1.8 1.3Z"/><path fill="#f63448" d="m1.8 34.7 16.9-16.7 5.2 5.1L4.6 34.6c-1 .6-2 .6-2.8.1Z"/></svg><span><span className="block text-[9px] font-bold uppercase tracking-wider text-white/55">Designed for</span><span className="block text-sm font-black">Android devices</span></span></div>
          </div>
          <p className="relative mt-4 text-center text-xs font-semibold text-white/50">Plus an installable web app for fast access from any supported device.</p>
        </div>
      </div>
    </section>

    <section id="everyone" className="bg-[#f2f0e8] py-24 lg:py-32">
      <div className="mx-auto grid max-w-[90rem] gap-14 px-5 sm:px-8 lg:grid-cols-[.78fr_1.22fr] lg:gap-24 lg:px-12">
        <div className="lg:sticky lg:top-10 lg:self-start">
          <p className="text-xs font-black uppercase tracking-[.22em] text-[#2f6d52]">One school, thoughtfully connected</p>
          <h2 className="mt-5 max-w-xl text-4xl font-black leading-[.98] tracking-[-.055em] sm:text-6xl">Different responsibilities. One shared direction.</h2>
          <p className="mt-7 max-w-lg text-lg leading-8 text-[#5d6a63]">GiddyEdu gives every member of your community a purposeful place to work, learn and stay informed—while your school keeps control of access and information.</p>
          <Link href="/login" className="mt-9 inline-flex items-center gap-3 rounded-full bg-[#12372a] px-7 py-4 font-black text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#1d513d] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#2f6d52]">Find your workspace <ArrowIcon /></Link>
        </div>
        <div className="border-t border-[#cfd7d1]">{roles.map(role => <div key={role.name} className="group grid gap-3 border-b border-[#cfd7d1] py-7 transition duration-300 hover:border-[#819b8d] sm:grid-cols-[.68fr_1.32fr] sm:gap-8 sm:py-8">
          <h3 className="text-xl font-black tracking-[-.035em] transition duration-300 group-hover:translate-x-2 group-hover:text-[#1f6747] sm:text-2xl">{role.name}</h3>
          <div className="flex items-start gap-5"><p className="max-w-lg text-sm leading-6 text-[#66736c] sm:text-base sm:leading-7">{role.detail}</p><span className="ml-auto mt-1 grid size-8 shrink-0 place-items-center rounded-full border border-[#bdc9c1] text-[#486457] opacity-0 transition duration-300 group-hover:translate-x-1 group-hover:border-[#2f6d52] group-hover:bg-[#12372a] group-hover:text-white group-hover:opacity-100"><ArrowIcon /></span></div>
        </div>)}</div>
      </div>
    </section>

    <LandingFooter />
  </main>;
}
