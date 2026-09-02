import Link from "next/link";
import { AcceptInvitationForm } from "@/components/accept-invitation-form";

export default async function AcceptInvitationPage({searchParams}:{searchParams:Promise<{token?:string}>}) {
  const {token=""}=await searchParams;
  return <main className="grid min-h-screen place-items-center bg-[#f6f4ee] px-6 py-12"><section className="w-full max-w-lg rounded-3xl bg-white p-8 shadow-xl"><Link href="/" className="text-sm font-bold text-emerald-800">← GiddyEdu</Link><h1 className="mt-6 text-3xl font-black">Join your school workspace</h1><p className="mb-7 mt-2 text-slate-600">Accept the invitation sent to your verified email address.</p><AcceptInvitationForm token={token}/></section></main>;
}
