import { GuardianProfileWorkspace } from "@/components/guardian-profile-workspace";

export default async function GuardianDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <GuardianProfileWorkspace guardianId={id} />;
}
