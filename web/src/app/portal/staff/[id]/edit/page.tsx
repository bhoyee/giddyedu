import { StaffEditWorkspace } from "@/components/staff-edit-workspace";

export default async function StaffEditPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <StaffEditWorkspace staffId={id} />;
}
