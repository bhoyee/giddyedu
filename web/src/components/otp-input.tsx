"use client";

import { useRef, useState } from "react";

export function OtpInput({ name, label, autoFocus = false }: { name: string; label: string; autoFocus?: boolean }) {
  const [digits, setDigits] = useState<string[]>(Array(6).fill(""));
  const inputs = useRef<Array<HTMLInputElement | null>>([]);

  function fill(start: number, raw: string) {
    const numbers = raw.replace(/\D/g, "").slice(0, 6 - start);
    if (!numbers) return;
    const next = [...digits];
    numbers.split("").forEach((digit, offset) => { next[start + offset] = digit; });
    setDigits(next);
    inputs.current[Math.min(start + numbers.length, 5)]?.focus();
  }

  return <fieldset>
    <legend className="text-sm font-black text-[#29372f]">{label}</legend>
    <div className="mt-2 grid grid-cols-6 gap-2" onPaste={(event) => { event.preventDefault(); fill(0, event.clipboardData.getData("text")); }}>
      {digits.map((digit, index) => <input
        key={index}
        ref={(element) => { inputs.current[index] = element; }}
        value={digit}
        required
        autoFocus={autoFocus && index === 0}
        inputMode="numeric"
        autoComplete={index === 0 ? "one-time-code" : "off"}
        pattern="[0-9]"
        maxLength={1}
        aria-label={`${label}, digit ${index + 1} of 6`}
        onChange={(event) => {
          const numbers = event.target.value.replace(/\D/g, "");
          if (numbers.length > 1) { fill(index, numbers); return; }
          const next = [...digits]; next[index] = numbers; setDigits(next);
          if (numbers && index < 5) inputs.current[index + 1]?.focus();
        }}
        onKeyDown={(event) => {
          if (event.key === "Backspace" && !digits[index] && index > 0) inputs.current[index - 1]?.focus();
          if (event.key === "ArrowLeft" && index > 0) { event.preventDefault(); inputs.current[index - 1]?.focus(); }
          if (event.key === "ArrowRight" && index < 5) { event.preventDefault(); inputs.current[index + 1]?.focus(); }
        }}
        className="aspect-square min-w-0 rounded-xl border border-[#cbd5ce] bg-[#fbfaf6] text-center font-mono text-xl font-black outline-none transition focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10 sm:text-2xl"
      />)}
    </div>
    <input type="hidden" name={name} value={digits.join("")} />
  </fieldset>;
}
