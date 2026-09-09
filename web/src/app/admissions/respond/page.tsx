import { Suspense } from "react";
import { AdmissionOfferResponse } from "@/components/admission-offer-response";
export default function AdmissionResponsePage() { return <Suspense fallback={<main className="p-10">Loading offer…</main>}><AdmissionOfferResponse /></Suspense>; }
