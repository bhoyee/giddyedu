"use client";

import { useEffect, useState } from "react";

export type AppToastDetail = { title?: string; message: string; tone?: "success" | "error" };

export function notify(detail: AppToastDetail) {
  window.dispatchEvent(new CustomEvent<AppToastDetail>("giddyedu:notify", { detail }));
}

export function AppToast() {
  const [toast, setToast] = useState<AppToastDetail | null>(null);

  useEffect(() => {
    const show = (event: Event) => setToast((event as CustomEvent<AppToastDetail>).detail);
    window.addEventListener("giddyedu:notify", show);
    return () => window.removeEventListener("giddyedu:notify", show);
  }, []);

  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(null), 4500);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  if (!toast) return null;
  const failed = toast.tone === "error";
  return <div role={failed ? "alert" : "status"} aria-live={failed ? "assertive" : "polite"} className={`fixed right-4 top-20 z-[150] flex w-[calc(100%-2rem)] max-w-sm items-start gap-3 rounded-2xl border bg-white p-4 shadow-[0_20px_60px_rgba(15,45,31,.22)] sm:right-6 ${failed ? "border-red-200 text-red-900" : "border-emerald-200 text-emerald-900"}`}>
    <span className={`grid size-8 shrink-0 place-items-center rounded-full text-base font-black ${failed ? "bg-red-100" : "bg-emerald-100"}`}>{failed ? "!" : "✓"}</span>
    <div className="min-w-0 flex-1"><p className="text-sm font-black">{toast.title ?? (failed ? "Action unsuccessful" : "Changes saved")}</p><p className="mt-0.5 text-xs leading-5 text-slate-600">{toast.message}</p></div>
    <button type="button" onClick={() => setToast(null)} aria-label="Close notification" className="grid size-8 shrink-0 place-items-center rounded-full text-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">×</button>
  </div>;
}
