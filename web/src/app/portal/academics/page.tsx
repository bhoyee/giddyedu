import { Suspense } from "react";
import { AcademicStructureWorkspace } from "@/components/academic-structure-workspace";

export default function AcademicsPage() { return <Suspense fallback={<div className="p-6 text-sm text-slate-500">Loading academic structure…</div>}><AcademicStructureWorkspace /></Suspense>; }
