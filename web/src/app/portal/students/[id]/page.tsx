import { StudentDetailWorkspace } from "@/components/student-detail-workspace";

export default async function StudentDetailPage({params}:{params:Promise<{id:string}>}){
  const {id}=await params;
  return <StudentDetailWorkspace studentId={id}/>;
}
