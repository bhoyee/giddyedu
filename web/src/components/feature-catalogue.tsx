"use client";

import { useMemo, useState } from "react";
import type { FeatureGroup } from "@/lib/product-features";

export function FeatureCatalogue({ groups }: { groups: FeatureGroup[] }) {
  const [query, setQuery] = useState("");
  const visibleGroups = useMemo(() => {
    const term = query.trim().toLocaleLowerCase();
    if (!term) return groups;
    return groups.map(group => ({ ...group, suites: group.suites.filter(suite => `${group.name} ${group.eyebrow} ${suite}`.toLocaleLowerCase().includes(term)) })).filter(group => group.suites.length > 0);
  }, [groups, query]);

  return <>
    <div className="mb-10 flex flex-col gap-4 rounded-3xl bg-[#e9efe9] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6"><div><label htmlFor="feature-search" className="font-black">Find a capability</label><p className="mt-1 text-sm text-[#66736c]">Search across every GiddyEdu suite.</p></div><div className="relative w-full sm:max-w-md"><svg aria-hidden="true" viewBox="0 0 20 20" className="pointer-events-none absolute left-4 top-1/2 size-5 -translate-y-1/2 fill-none stroke-[#5f6c65] stroke-2"><circle cx="9" cy="9" r="5.5" /><path d="m13 13 4 4" /></svg><input id="feature-search" type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Try fees, attendance or transport" className="min-h-12 w-full rounded-full border border-[#cbd6ce] bg-white py-3 pl-12 pr-5 text-base outline-none transition placeholder:text-[#89938e] focus:border-[#2f6d52] focus:ring-4 focus:ring-[#2f6d52]/10" /></div></div>
    {visibleGroups.length > 0 ? <div className="grid gap-5 lg:grid-cols-2">{visibleGroups.map(group => {
      const originalIndex = groups.findIndex(item => item.name === group.name);
      return <article key={group.name} id={`group-${originalIndex + 1}`} className="scroll-mt-6 rounded-[2rem] border border-[#dfe4df] bg-white p-7 sm:p-9"><div><p className="text-xs font-black uppercase tracking-[.18em] text-[#2f6d52]">{group.eyebrow}</p><h2 className="mt-3 text-3xl font-black tracking-[-.04em]">{group.name}</h2></div><p className="mt-5 max-w-xl leading-7 text-[#66736c]">{group.description}</p><ul className="mt-8 grid gap-2">{group.suites.map(suite => <li key={suite} className="rounded-xl bg-[#f7f7f2] px-4 py-3.5 font-bold">{suite}</li>)}</ul></article>;
    })}</div> : <div className="rounded-[2rem] border border-dashed border-[#bdc9c1] py-20 text-center"><h2 className="text-2xl font-black">No matching capability</h2><p className="mt-2 text-[#66736c]">Try a broader search such as student, finance or staff.</p><button type="button" onClick={() => setQuery("")} className="mt-6 rounded-full bg-[#12372a] px-6 py-3 font-bold text-white">Clear search</button></div>}
  </>;
}
