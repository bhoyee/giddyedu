import { StudentEditWorkspace } from "@/components/student-edit-workspace";

export default async function StudentEditPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <StudentEditWorkspace studentId={id} />;
}
