type BrandLogoProps = {
  className?: string;
  iconClassName?: string;
  wordmarkClassName?: string;
  tone?: "light" | "dark";
  showWordmark?: boolean;
};

export function BrandLogo({ className = "", iconClassName = "size-10", wordmarkClassName = "text-xl", tone = "light", showWordmark = true }: BrandLogoProps) {
  const colour = tone === "light" ? "text-[#f4c95d]" : "text-[#12372a]";

  return <span className={`inline-flex items-center gap-2.5 ${colour} ${className}`}>
    <svg aria-hidden="true" viewBox="0 0 48 48" className={`shrink-0 fill-none stroke-current ${iconClassName}`}>
      <path d="M38 14.5A17 17 0 1 0 38 34v-9H27" strokeWidth="7.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
    {showWordmark && <span className={`font-bold leading-none tracking-[-.065em] [font-family:'Trebuchet_MS','Avenir_Next',Arial,sans-serif] ${wordmarkClassName}`}>GiddyEdu</span>}
  </span>;
}
