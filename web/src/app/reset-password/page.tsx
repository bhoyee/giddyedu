import Link from "next/link";
import { ResetPasswordForm } from "@/components/auth-form";

export default async function ResetPasswordPage({ searchParams }: { searchParams: Promise<{ email?: string; token?: string }> }) {
  const query = await searchParams;
  return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6 py-12"><section className="w-full max-w-md rounded-3xl bg-white p-8 shadow-xl"><Link href="/login" className="text-sm font-bold text-emerald-800">← Sign in</Link><h1 className="mt-6 text-3xl font-black">Choose a new password</h1><p className="mb-7 mt-2 text-slate-600">Use at least 12 characters with uppercase, lowercase, number, and symbol.</p><ResetPasswordForm email={query.email ?? ""} token={query.token ?? ""} /></section></main>;
}
