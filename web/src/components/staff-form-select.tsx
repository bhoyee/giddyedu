"use client";

import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";

type StaffFormSelectProps = {
  name: string;
  title: string;
  options: string[][];
  optional?: boolean;
  value?: string;
  onChange?: (value: string) => void;
  disabled?: boolean;
  note?: string;
};

export function StaffFormSelect({ name, title, options, optional = false, value, onChange, disabled = false, note }: StaffFormSelectProps) {
  const [localValue, setLocalValue] = useState("");
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [position, setPosition] = useState({ top: 0, left: 0, width: 0, maxHeight: 240 });
  const buttonRef = useRef<HTMLButtonElement>(null);
  const popupRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const listId = useId();
  const selectedValue = value ?? localValue;
  const selectedLabel = options.find(([optionValue]) => optionValue === selectedValue)?.[1];
  const visibleOptions = options.filter(([, optionLabel]) => optionLabel.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()));

  useEffect(() => {
    if (!open) return;
    const updatePosition = () => {
      const rect = buttonRef.current?.getBoundingClientRect();
      if (!rect) return;
      setPosition({ top: rect.bottom + 6, left: rect.left, width: rect.width, maxHeight: Math.max(100, window.innerHeight - rect.bottom - 18) });
    };
    const closeOutside = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!buttonRef.current?.contains(target) && !popupRef.current?.contains(target)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") { setOpen(false); buttonRef.current?.focus(); }
    };
    updatePosition();
    const frame = window.requestAnimationFrame(() => {
      updatePosition();
      if (options.length > 8) searchRef.current?.focus();
      else popupRef.current?.querySelector<HTMLButtonElement>('[role="option"]')?.focus();
    });
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    document.addEventListener("pointerdown", closeOutside);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
      document.removeEventListener("pointerdown", closeOutside);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open, options.length]);

  function choose(nextValue: string) {
    if (value === undefined) setLocalValue(nextValue);
    onChange?.(nextValue);
    setOpen(false);
    setSearch("");
    buttonRef.current?.focus();
  }

  function toggle() {
    if (disabled) return;
    if (!open) buttonRef.current?.scrollIntoView({ block: "center", behavior: "instant" });
    setSearch("");
    setOpen(current => !current);
  }

  return <div className="block text-xs font-bold text-slate-700">
    <span id={`${listId}-label`}>{title}{!optional && <span className="text-red-600"> *</span>}</span>
    <input type="hidden" name={name} value={selectedValue} />
    <button ref={buttonRef} type="button" disabled={disabled} aria-labelledby={`${listId}-label`} aria-haspopup="listbox" aria-expanded={open} aria-controls={open ? listId : undefined}
      onClick={toggle} onKeyDown={event => { if (event.key === "ArrowDown" || event.key === "ArrowUp") { event.preventDefault(); if (!open) toggle(); } }}
      className="mt-1.5 flex w-full items-center justify-between rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-left text-sm font-normal text-slate-900 outline-none transition hover:border-slate-400 focus:border-[var(--tenant-primary,#28654a)] focus:ring-4 focus:ring-emerald-950/5 disabled:bg-slate-100 disabled:text-slate-400">
      <span className={selectedLabel ? "truncate" : "truncate text-slate-400"}>{selectedLabel ?? (disabled ? "Select a state first" : "Select…")}</span>
      <svg aria-hidden="true" viewBox="0 0 20 20" className="ml-2 size-4 shrink-0 fill-none stroke-current"><path d="m5 7 5 5 5-5" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </button>
    {note && <span className="mt-1 block font-normal text-slate-500">{note}</span>}
    {open && createPortal(<div ref={popupRef} style={{ top: position.top, left: position.left, width: position.width, maxHeight: position.maxHeight }} className="fixed z-[200] flex flex-col overflow-hidden rounded-xl border border-slate-200 bg-white p-1.5 shadow-[0_18px_45px_rgba(15,35,27,.2)]">
      {options.length > 8 && <input ref={searchRef} value={search} onChange={event => setSearch(event.target.value)} onKeyDown={event => { if (event.key === "Enter" && visibleOptions.length === 1) { event.preventDefault(); choose(visibleOptions[0][0]); } else if (event.key === "ArrowDown") { event.preventDefault(); popupRef.current?.querySelector<HTMLButtonElement>('[role="option"]')?.focus(); } }} placeholder={`Search ${title.toLocaleLowerCase()}`} aria-label={`Search ${title.toLocaleLowerCase()}`} className="mb-1 rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-[var(--tenant-primary,#28654a)]" />}
      <div id={listId} role="listbox" aria-labelledby={`${listId}-label`} onKeyDown={event => {
        if (event.key !== "ArrowDown" && event.key !== "ArrowUp" && event.key !== "Home" && event.key !== "End") return;
        event.preventDefault();
        const choices = [...event.currentTarget.querySelectorAll<HTMLButtonElement>('[role="option"]')];
        const current = choices.indexOf(document.activeElement as HTMLButtonElement);
        const next = event.key === "Home" ? 0 : event.key === "End" ? choices.length - 1 : event.key === "ArrowDown" ? Math.min(current + 1, choices.length - 1) : Math.max(current - 1, 0);
        choices[next]?.focus();
      }} className="min-h-0 overflow-y-auto">
        {optional && <button type="button" role="option" aria-selected={!selectedValue} onClick={() => choose("")} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-slate-500 hover:bg-slate-100">No selection</button>}
        {visibleOptions.map(([optionValue, optionLabel]) => <button key={optionValue} type="button" role="option" aria-selected={optionValue === selectedValue} onClick={() => choose(optionValue)} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-slate-800 hover:bg-slate-100 aria-selected:bg-[var(--tenant-primary-soft,#e7f2eb)] aria-selected:font-semibold">{optionLabel}</button>)}
        {visibleOptions.length === 0 && <p className="px-3 py-2 text-sm font-normal text-slate-500">No matches found.</p>}
      </div>
    </div>, document.body)}
  </div>;
}
