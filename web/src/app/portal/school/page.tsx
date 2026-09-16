import { SchoolOnboarding } from "@/components/school-onboarding";

export default async function SchoolPage({ searchParams }: { searchParams: Promise<{ section?: string }> }) {
  const { section } = await searchParams;
  return <SchoolOnboarding initialStep={section === "campuses" ? 2 : section === "branding" ? 1 : 0} />;
}
