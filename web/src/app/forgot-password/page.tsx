import Link from "next/link";
import { ForgotPasswordForm } from "@/components/auth-form";

export default function ForgotPasswordPage() {
  return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6 py-12"><section className="w-full max-w-md rounded-3xl bg-white p-8 shadow-xl"><Link href="/login" className="text-sm font-bold text-emerald-800">← Sign in</Link><h1 className="mt-6 text-3xl font-black">Reset password</h1><p className="mb-7 mt-2 text-slate-600">Request a secure, single-use password reset link.</p><ForgotPasswordForm /></section></main>;
}
