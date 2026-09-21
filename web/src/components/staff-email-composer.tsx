"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import dynamic from "next/dynamic";
import DOMPurify from "dompurify";
import type { Jodit } from "jodit/esm/jodit";
import "jodit/es2021/jodit.min.css";

const JoditEditor = dynamic(() => import("jodit-react"), { ssr: false, loading: () => <div className="grid min-h-64 place-items-center text-sm text-slate-500">Loading editor…</div> });

type Recipient = { name: string; email: string };
const menuLabels: Record<string, string[]> = {
  File: ["Preview email", "Close composer"],
  Edit: ["Undo", "Redo", "Select all"],
  View: ["Toggle source view", "Preview email"],
  Insert: ["Attach files", "Horizontal rule", "2 × 2 table"],
  Format: ["Bold", "Italic", "Underline", "Clear formatting"],
  Tools: ["Select all", "Toggle source view"],
  Table: ["Insert 2 × 2 table", "Insert 3 × 3 table"],
  Help: ["Editor help"],
};

export function StaffEmailComposer({ recipients, skippedCount, onClose, audience = "staff members", contextLabel = "Staff directory" }: { recipients: Recipient[]; skippedCount: number; onClose: () => void; audience?: string; contextLabel?: string }) {
  const [step, setStep] = useState<"compose" | "preview">("compose");
  const [subject, setSubject] = useState("");
  const [previewText, setPreviewText] = useState("");
  const [messageHtml, setMessageHtml] = useState("");
  const [error, setError] = useState("");
  const [activeMenu, setActiveMenu] = useState<string | null>(null);
  const [attachments, setAttachments] = useState<File[]>([]);
  const [showHelp, setShowHelp] = useState(false);
  const editor = useRef<Jodit | null>(null);
  const fileInput = useRef<HTMLInputElement | null>(null);
  const editorConfig = useMemo(() => ({
    height: 350,
    toolbarAdaptive: false,
    toolbarButtonSize: "small" as const,
    buttons: ["undo", "redo", "bold", "italic", "underline", "strikethrough", "eraser", "font", "fontsize", "paragraph", "brush", "ul", "ol", "outdent", "indent", "left", "center", "right", "justify", "link", "table", "hr", "symbol", "source", "fullsize"],
    disablePlugins: ["filebrowser", "image", "video", "iframe"],
    uploader: { insertImageAsBase64URI: false },
    placeholder: `Write your message to the selected ${audience}…`,
    zIndex: 190,
    showCharsCounter: true,
    showWordsCounter: true,
  }), [audience]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === "Escape") { if (activeMenu) setActiveMenu(null); else if (showHelp) setShowHelp(false); else onClose(); } };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [activeMenu, showHelp, onClose]);

  function runEditorCommand(command: string) {
    editor.current?.execCommand(command);
    setActiveMenu(null);
  }

  function insertTable(size: number) {
    const rows = Array.from({ length: size }, () => `<tr>${"<td>&nbsp;</td>".repeat(size)}</tr>`).join("");
    editor.current?.s.insertHTML(`<table><tbody>${rows}</tbody></table>`);
    setActiveMenu(null);
  }

  function addAttachments(files: FileList | null) {
    if (!files) return;
    const nextFiles = [...attachments, ...Array.from(files)];
    if (nextFiles.some(file => !/\.(pdf|doc|docx|jpe?g|png)$/i.test(file.name))) {
      setError("Attach PDF, Word, JPG or PNG files only.");
      return;
    }
    if (nextFiles.length > 5 || nextFiles.some(file => file.size > 10 * 1024 * 1024)) {
      setError("Choose up to five files, each no larger than 10 MB.");
      return;
    }
    setAttachments(nextFiles);
    setError("");
    if (fileInput.current) fileInput.current.value = "";
  }

  function runMenuAction(menu: string, label: string) {
    if (label === "Preview email") next();
    else if (label === "Close composer") onClose();
    else if (label === "Attach files") fileInput.current?.click();
    else if (label === "Toggle source view") editor.current?.toggleMode();
    else if (label === "Horizontal rule") editor.current?.s.insertHTML("<hr>");
    else if (label === "2 × 2 table" || label === "Insert 2 × 2 table") insertTable(2);
    else if (label === "Insert 3 × 3 table") insertTable(3);
    else if (label === "Editor help") setShowHelp(true);
    else if (menu === "Edit" || menu === "Format" || menu === "Tools") runEditorCommand(label === "Clear formatting" ? "removeFormat" : label.toLowerCase().replaceAll(" ", ""));
    setActiveMenu(null);
  }

  function next() {
    const message = new DOMParser().parseFromString(messageHtml, "text/html").body.textContent?.trim() ?? "";
    if (!subject.trim() || !message || message.length > 10000) {
      setError(message.length > 10000 ? "Keep the message under 10,000 characters." : "Add a subject and message before previewing this email.");
      return;
    }
    setError("");
    setStep("preview");
  }

  const safePreview = step === "preview" ? DOMPurify.sanitize(messageHtml, {
    ALLOWED_TAGS: ["p", "br", "b", "strong", "em", "i", "u", "s", "ul", "ol", "li", "blockquote", "h1", "h2", "h3", "h4", "table", "thead", "tbody", "tr", "th", "td", "a", "span", "div", "hr"],
    ALLOWED_ATTR: ["href", "target", "rel", "colspan", "rowspan"],
  }) : "";

  return createPortal(
    <div className="fixed inset-0 z-[180] grid place-items-center bg-slate-950/65 p-2 backdrop-blur-sm sm:p-5" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
      <div role="dialog" aria-modal="true" aria-labelledby="staff-email-title" className="flex max-h-[min(92vh,900px)] w-full max-w-4xl flex-col overflow-hidden rounded-[1.5rem] bg-white shadow-2xl">
        <header className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-5 sm:px-7">
          <div><p className="text-[11px] font-black uppercase tracking-[.16em] text-[#28654a]">{contextLabel} · Email</p><h2 id="staff-email-title" className="mt-1 text-2xl font-black tracking-tight text-slate-950">Compose email</h2><p className="mt-1 text-sm text-slate-500">Prepare a message for the selected {audience}, then review it before leaving this screen.</p></div>
          <button type="button" onClick={onClose} aria-label="Close email composer" className="grid size-10 shrink-0 place-items-center rounded-xl bg-slate-100 text-2xl text-slate-600 transition hover:bg-red-50 hover:text-red-700">×</button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7">
          <div className="mb-5 flex items-center gap-3 text-xs font-bold"><span className={`grid size-7 place-items-center rounded-full ${step === "compose" ? "bg-[#12372a] text-white" : "bg-emerald-100 text-emerald-800"}`}>1</span><span className="text-slate-700">Compose</span><span className="h-px w-8 bg-slate-200"/><span className={`grid size-7 place-items-center rounded-full ${step === "preview" ? "bg-[#12372a] text-white" : "bg-slate-100 text-slate-500"}`}>2</span><span className="text-slate-700">Preview</span></div>
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4"><p className="text-[11px] font-black uppercase tracking-[.12em] text-slate-500">Recipients · {recipients.length}</p><div className="mt-2 flex max-h-24 flex-wrap gap-1.5 overflow-y-auto">{recipients.map(recipient => <span key={recipient.email} title={recipient.email} className="rounded-full border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-700">{recipient.name}</span>)}</div>{skippedCount > 0 && <p className="mt-2 text-xs text-amber-800">{skippedCount} selected recipient{skippedCount === 1 ? " has" : "s have"} no email address and {skippedCount === 1 ? "is" : "are"} excluded.</p>}</div>

          {step === "compose" ? <div className="mt-5 space-y-4">
            <label className="block text-sm font-bold text-slate-800">Subject <span className="text-red-600">*</span><input value={subject} onChange={event => { setSubject(event.target.value); setError(""); }} maxLength={160} placeholder="Enter a clear subject" className="mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm font-normal text-slate-900 outline-none focus:border-[#28654a] focus:ring-2 focus:ring-emerald-100"/></label>
            <label className="block text-sm font-bold text-slate-800">Preview text <span className="font-normal text-slate-400">(optional)</span><input value={previewText} onChange={event => setPreviewText(event.target.value)} maxLength={200} placeholder="A short summary shown beneath the subject in some inboxes" className="mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm font-normal text-slate-900 outline-none focus:border-[#28654a] focus:ring-2 focus:ring-emerald-100"/></label>
          </div> : <div className="mt-5 rounded-t-xl border border-b-0 border-slate-200 bg-slate-50 px-5 py-4"><p className="text-lg font-bold text-slate-950">{subject.trim()}</p>{previewText.trim() && <p className="mt-1 text-sm text-slate-500">{previewText.trim()}</p>}</div>}
          {step === "compose" ? <div className="staff-compose-editor mt-4 rounded-xl border border-slate-200 bg-white p-1 shadow-sm">
            <p className="px-2 py-2 text-sm font-bold text-slate-800">Message <span className="text-red-600">*</span></p>
            <div className="flex flex-wrap items-center gap-0.5 border-y border-slate-200 bg-slate-50 px-2 py-1" onMouseLeave={() => setActiveMenu(null)}>
              {Object.entries(menuLabels).map(([menu, items]) => <div key={menu} className="relative">
                <button type="button" aria-expanded={activeMenu === menu} onClick={() => setActiveMenu(activeMenu === menu ? null : menu)} className={`rounded-md px-2.5 py-1.5 text-xs font-semibold transition hover:bg-slate-200 ${activeMenu === menu ? "bg-slate-200 text-slate-950" : "text-slate-700"}`}>{menu}</button>
                {activeMenu === menu && <div className="absolute left-0 top-full z-50 min-w-48 rounded-lg border border-slate-200 bg-white py-1 shadow-xl" role="menu">{items.map(label => <button key={label} type="button" role="menuitem" onMouseDown={event => event.preventDefault()} onClick={() => runMenuAction(menu, label)} className="block w-full px-3 py-2 text-left text-xs text-slate-800 hover:bg-slate-100">{label}</button>)}</div>}
              </div>)}
            </div>
            <JoditEditor editorRef={instance => { editor.current = instance as Jodit; }} value={messageHtml} config={editorConfig} onChange={value => { setMessageHtml(value); setError(""); }}/>
            <div className="flex flex-wrap items-center gap-2 px-2 py-3">
              <input ref={fileInput} type="file" multiple className="hidden" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" onChange={event => addAttachments(event.target.files)} />
              <button type="button" onClick={() => fileInput.current?.click()} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-bold text-slate-700 hover:border-slate-500 hover:bg-slate-50">📎 Attach files</button>
              <span className="text-xs text-slate-500">PDF, Word or image · up to 5 files · 10 MB each</span>
            </div>
          </div> : <div className="staff-email-preview min-h-64 rounded-b-xl border border-slate-200 bg-white px-5 py-6 text-sm leading-7 text-slate-800" dangerouslySetInnerHTML={{ __html: safePreview }} />}
          {attachments.length > 0 && <div className="mt-3 rounded-xl border border-slate-200 p-3"><p className="text-xs font-bold text-slate-700">Attachments selected locally</p><div className="mt-2 flex flex-wrap gap-2">{attachments.map((file, index) => <span key={`${file.name}-${index}`} className="inline-flex items-center gap-2 rounded-lg bg-slate-100 px-2 py-1 text-xs text-slate-700">{file.name} <span className="text-slate-500">({(file.size / 1024 / 1024).toFixed(1)} MB)</span><button type="button" aria-label={`Remove ${file.name}`} onClick={() => setAttachments(current => current.filter((_, itemIndex) => itemIndex !== index))} className="rounded px-1 font-bold text-red-700 hover:bg-red-100">×</button></span>)}</div></div>}
          {showHelp && <div className="mt-3 rounded-xl border border-blue-200 bg-blue-50 p-4 text-xs leading-6 text-slate-700"><div className="flex items-center justify-between"><strong>Editor help</strong><button type="button" onClick={() => setShowHelp(false)} className="font-bold text-red-700">Close</button></div><p>Use the menus or toolbar to format text, insert tables and links, and switch to HTML source view. Ctrl+Z undoes your last edit; Ctrl+Y redoes it. Attachments are selected locally until sending is implemented.</p></div>}
          {error && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-700">{error}</p>}
          <p className="mt-5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-xs leading-5 text-amber-900">Preview only: this email and its attachments are not sent, uploaded or saved. Draft storage requires the messaging workflow.</p>
        </div>

        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:px-7"><span className="text-xs text-slate-500">{step === "compose" ? "Step 1 of 2 · Compose" : "Step 2 of 2 · Review"}</span><div className="flex gap-2"><button type="button" onClick={step === "preview" ? () => setStep("compose") : onClose} className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 transition hover:bg-red-100">{step === "preview" ? "Back to edit" : "Close"}</button>{step === "compose" && <button type="button" onClick={next} className="rounded-xl bg-[#12372a] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-[#1d513d]">Next · Preview</button>}</div></footer>
      </div>
    </div>, document.body
  );
}
