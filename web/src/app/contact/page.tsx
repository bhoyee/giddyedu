import Link from "next/link";
import { ContactForm } from "@/components/contact-form";
import { LandingFooter } from "@/components/landing-footer";
import { LandingHeader } from "@/components/landing-header";
import { SocialLinks } from "@/components/social-links";

const contactCards = [
  { label: "Support hours", value: "Monday–Friday", detail: "08:00–18:00 WAT" },
  { label: "Email", value: "hello@giddyedu.com", detail: "General and sales enquiries", href: "mailto:hello@giddyedu.com" },
  { label: "Phone support", value: "Available by appointment", detail: "A verified public number will be added here" },
];

export default function ContactPage() {
  return <main className="min-h-screen bg-[#f5f3ec] text-[#17221d]">
    <LandingHeader />
    <header className="bg-[#12372a] px-5 pb-36 pt-36 text-white sm:px-8 lg:px-12"><div className="mx-auto max-w-6xl"><p className="text-xs font-black uppercase tracking-[.2em] text-[#f4c95d]">Contact GiddyEdu</p><div className="mt-5 grid gap-8 lg:grid-cols-[1.15fr_.85fr] lg:items-end"><h1 className="max-w-4xl text-5xl font-black leading-[.95] tracking-[-.055em] sm:text-7xl">Let’s talk about what your school needs.</h1><p className="max-w-xl text-lg leading-8 text-white/68">Speak with us about adopting GiddyEdu, getting support, building a partnership or telling a wider education story.</p></div></div></header>
    <section className="relative z-10 mx-auto -mt-20 max-w-6xl px-5 sm:px-8"><div className="grid gap-4 md:grid-cols-3">{contactCards.map((card) => <article key={card.label} className="rounded-2xl border border-[#dce3dd] bg-white p-6 shadow-[0_18px_50px_rgba(18,55,42,.09)]"><p className="text-xs font-black uppercase tracking-[.16em] text-[#527060]">{card.label}</p>{card.href ? <Link href={card.href} className="mt-4 block text-xl font-black tracking-[-.03em] text-[#12372a] hover:text-[#2f6d52]">{card.value}</Link> : <p className="mt-4 text-xl font-black tracking-[-.03em]">{card.value}</p>}<p className="mt-2 text-sm text-[#6b786f]">{card.detail}</p></article>)}</div></section>
    <section className="mx-auto grid max-w-6xl gap-14 px-5 py-20 sm:px-8 lg:grid-cols-[.72fr_1.28fr] lg:py-28"><div><p className="text-xs font-black uppercase tracking-[.2em] text-[#2f6d52]">Send an enquiry</p><h2 className="mt-4 text-4xl font-black leading-none tracking-[-.05em]">How can we help?</h2><p className="mt-5 max-w-md leading-7 text-[#647168]">Choose the subject that best matches your request. Required details help route the conversation when online delivery is connected.</p><div className="mt-10 border-t border-[#d4ddd6] pt-8"><p className="text-sm font-black">Follow GiddyEdu</p><p className="mt-2 text-sm leading-6 text-[#6b786f]">Official social profiles will be linked here as they become available.</p><div className="mt-5"><SocialLinks light /></div></div></div><ContactForm /></section>
    <LandingFooter />
  </main>;
}
