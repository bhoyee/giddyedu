import Link from "next/link";
import { BrandLogo } from "@/components/brand-logo";
import { CookieSettingsButton } from "@/components/cookie-settings-button";
import { SocialLinks } from "@/components/social-links";

const footerGroups = [
  { title: "Explore", links: [["Features", "/features"], ["Mobile apps", "/#mobile"], ["Documentation", "/documentation"]] },
  { title: "Company", links: [["About", "/about"], ["FAQs", "/faqs"], ["Contact", "/contact"]] },
  { title: "Connect", links: [["Partnerships", "/partnerships"], ["Careers", "/careers"], ["Register school", "/register"]] },
] as const;

export function LandingFooter() {
  return <footer className="overflow-hidden bg-[#0b3028] text-white">
    <div className="mx-auto max-w-[90rem] px-5 pb-7 pt-16 sm:px-8 lg:px-12 lg:pt-20">
      <div className="grid gap-12 border-b border-white/15 pb-14 lg:grid-cols-[.9fr_1.35fr] lg:gap-20">
        <div><Link href="/" aria-label="GiddyEdu home" className="inline-flex rounded-lg transition hover:opacity-80"><BrandLogo iconClassName="size-14" wordmarkClassName="text-3xl" /></Link><p className="mt-6 max-w-md text-sm leading-7 text-white/58">The connected school operating platform for administration, teaching, finance, people, families and the wider school day.</p><div className="mt-7"><SocialLinks /></div></div>
        <div className="grid grid-cols-2 gap-8 text-sm sm:grid-cols-3">{footerGroups.map((group) => <div key={group.title}><p className="font-black text-white">{group.title}</p><nav className="mt-5 grid gap-3 text-white/58">{group.links.map(([label, href]) => <Link key={label} className="transition hover:text-[#f4c95d]" href={href}>{label}</Link>)}</nav></div>)}</div>
      </div>
      <div className="overflow-hidden py-8 sm:py-10"><p aria-label="GiddyEdu" className="whitespace-nowrap font-serif text-[clamp(5rem,18vw,17rem)] leading-[.72] tracking-[-.075em] text-white/20">GiddyEdu</p></div>
      <div className="flex flex-col gap-4 border-t border-white/15 pt-6 text-xs text-white/45 sm:flex-row sm:items-center sm:justify-between"><p>© 2026 GiddyEdu. All rights reserved.</p><nav aria-label="Legal" className="flex flex-wrap gap-x-6 gap-y-2"><Link className="transition hover:text-white" href="/privacy">Privacy Policy</Link><Link className="transition hover:text-white" href="/terms">Terms & Conditions</Link><Link className="transition hover:text-white" href="/cookies">Cookie Policy</Link><CookieSettingsButton className="transition hover:text-white" /></nav></div>
    </div>
  </footer>;
}
