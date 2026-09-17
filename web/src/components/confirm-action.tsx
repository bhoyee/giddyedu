"use client";

import { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";

type ConfirmationOptions = {
  title: string;
  message: string;
  confirmLabel: string;
  confirmText?: string;
};

export function confirmAction(options: ConfirmationOptions): Promise<boolean> {
  return new Promise(resolve => {
    const host = document.createElement("div");
    document.body.appendChild(host);
    const root = createRoot(host);
    let settled = false;
    const finish = (confirmed: boolean) => {
      if (settled) return;
      settled = true;
      queueMicrotask(() => { root.unmount(); host.remove(); });
      resolve(confirmed);
    };
    root.render(<ConfirmationDialog {...options} onFinish={finish} />);
  });
}

function ConfirmationDialog({ title, message, confirmLabel, confirmText, onFinish }: ConfirmationOptions & { onFinish: (confirmed: boolean) => void }) {
  const [entry, setEntry] = useState("");
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    cancelRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onFinish(false);
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [onFinish]);

  return <div className="fixed inset-0 z-[250] grid place-items-center bg-slate-950/60 p-4 backdrop-blur-sm" onMouseDown={event => { if (event.target === event.currentTarget) onFinish(false); }}>
    <div role="alertdialog" aria-modal="true" aria-labelledby="confirm-action-title" aria-describedby="confirm-action-message" className="w-full max-w-md rounded-[1.5rem] border border-slate-200 bg-white p-6 shadow-2xl sm:p-7">
      <div className="grid size-11 place-items-center rounded-xl bg-red-50 text-xl font-black text-red-700" aria-hidden="true">!</div>
      <h2 id="confirm-action-title" className="mt-4 text-xl font-black text-slate-950">{title}</h2>
      <p id="confirm-action-message" className="mt-2 text-sm leading-6 text-slate-600">{message}</p>
      {confirmText && <label className="mt-5 block text-sm font-bold text-slate-700">Type <strong className="text-red-700">{confirmText}</strong> to confirm<input autoComplete="off" value={entry} onChange={event => setEntry(event.target.value)} className="mt-2 w-full rounded-xl border border-slate-300 px-4 py-3 font-mono text-sm outline-none focus:border-red-500 focus:ring-4 focus:ring-red-100" /></label>}
      <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><button ref={cancelRef} type="button" onClick={() => onFinish(false)} className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 hover:bg-red-100">Cancel</button><button type="button" disabled={Boolean(confirmText && entry !== confirmText)} onClick={() => onFinish(true)} className="rounded-xl bg-red-700 px-4 py-2.5 text-sm font-bold text-white hover:bg-red-800 disabled:cursor-not-allowed disabled:opacity-40">{confirmLabel}</button></div>
    </div>
  </div>;
}
