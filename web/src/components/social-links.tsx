const socialIcons = [
  ["LinkedIn", <path key="linkedin" d="M6.5 8.2H3V19h3.5V8.2ZM4.8 3A2 2 0 1 0 4.8 7a2 2 0 0 0 0-4ZM19.5 12.8c0-3.3-1.8-4.9-4.2-4.9-1.9 0-2.8 1.1-3.3 1.8V8.2H8.5V19H12v-5.4c0-1.4.3-2.8 2-2.8 1.7 0 1.8 1.6 1.8 2.9V19h3.5l.2-6.2Z" />],
  ["Instagram", <><rect key="instagram-frame" x="3" y="3" width="18" height="18" rx="5" /><circle key="instagram-lens" cx="12" cy="12" r="4" /><path key="instagram-dot" d="M17.4 6.6h.01" /></>],
  ["X", <path key="x" d="M4 4l11.8 16H20L8.2 4H4Zm1 16L19 4M5.2 4l13.6 16" />],
  ["Facebook", <path key="facebook" d="M14 8h4V3h-4c-3.3 0-6 2.7-6 6v3H4v5h4v5h5v-5h4l1-5h-5V9c0-.6.4-1 1-1Z" />],
] as const;

export function SocialLinks({ light = false }: { light?: boolean }) {
  return <div className="flex items-center gap-3" aria-label="GiddyEdu social channels">{socialIcons.map(([name, icon]) => <span key={name} title={`${name} profile coming soon`} aria-label={`GiddyEdu on ${name}; official profile coming soon`} className={`grid size-11 place-items-center rounded-full border transition ${light ? "border-[#cad5ce] text-[#3e5e4f] hover:border-[#12372a] hover:bg-[#12372a] hover:text-white" : "border-white/15 text-white/60 hover:border-[#f4c95d]/70 hover:text-[#f4c95d]"}`}><svg aria-hidden="true" viewBox="0 0 24 24" className="size-[1.1rem] fill-none stroke-current stroke-[1.7]" strokeLinecap="round" strokeLinejoin="round">{icon}</svg></span>)}</div>;
}
