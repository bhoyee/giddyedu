import { StaffDetailWorkspace } from "@/components/staff-detail-workspace";

export default async function StaffDetailPage({params}:{params:Promise<{id:string}>}){
  const {id}=await params;
  return <StaffDetailWorkspace staffId={id}/>;
}
