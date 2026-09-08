import Link from "next/link";
import { PlatformLoginForm } from "@/components/auth-form";

export default function PlatformLoginPage() {
  return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6 py-12"><section className="w-full max-w-md rounded-3xl bg-white p-8 shadow-xl"><Link href="/" className="text-sm font-bold text-emerald-800">← GiddyEdu</Link><h1 className="mt-6 text-3xl font-black">Platform operations</h1><p className="mb-7 mt-2 text-slate-600">Restricted to authorised GiddyEdu platform operators. No school or campus is required.</p><PlatformLoginForm /><p className="mt-6 text-sm text-slate-600"><Link className="font-bold text-emerald-800" href="/forgot-password">Set or reset platform password</Link></p><p className="mt-3 text-sm text-slate-600"><Link className="font-bold text-emerald-800" href="/login">Return to school sign-in</Link></p></section></main>;
}
