import Link from "next/link";
import { ConfirmEmailForm } from "@/components/confirm-email-form";

export default async function ConfirmEmailPage({ searchParams }: { searchParams: Promise<{ email?: string; token?: string }> }) {
  const query = await searchParams;
  return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6 py-12"><section className="w-full max-w-md rounded-3xl bg-white p-8 shadow-xl"><Link href="/" className="text-sm font-bold text-emerald-800">&larr; GiddyEdu</Link><h1 className="mt-6 text-3xl font-black">Confirm your email</h1><p className="mb-7 mt-2 text-slate-600">Finish setting up your secure school account.</p><ConfirmEmailForm email={query.email ?? ""} token={query.token ?? ""} /></section></main>;
}
