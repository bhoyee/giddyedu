import { ResourcePage, type RecordActionConfig } from "@/components/resource-page";

const placementFields: NonNullable<RecordActionConfig["fields"]> = [{name:"academicYearId",label:"Academic year",type:"select",optionsEndpoint:"academics/structure",optionsCollectionKey:"academicYears"},{name:"classSectionId",label:"Class section",type:"select",optionsEndpoint:"academics/structure",optionsCollectionKey:"classSections"},{name:"enrolledOn",label:"Enrolment date",type:"date"}];
const progressionFields: NonNullable<RecordActionConfig["fields"]> = [{name:"type",label:"Transition",type:"select",numeric:true,options:[{value:"0",label:"Promote"},{value:"1",label:"Repeat class"},{value:"2",label:"Transfer"}]},{name:"academicYearId",label:"Target academic year",type:"select",optionsEndpoint:"academics/structure",optionsCollectionKey:"academicYears"},{name:"classSectionId",label:"Target class",type:"select",optionsEndpoint:"academics/structure",optionsCollectionKey:"classSections"},{name:"effectiveOn",label:"Effective date",type:"date"},{name:"reason",label:"Reason",required:false}];

export default function StudentsPage() {
  return <ResourcePage title="Students" description="Manage canonical learner records, enrolment placement, and guardian relationships." endpoint="students?page=1&pageSize=50" emptyMessage="No students have been enrolled." fields={[{key:"admissionNumber",label:"Admission no."},{key:"firstName",label:"First name"},{key:"lastName",label:"Last name"},{key:"dateOfBirth",label:"Date of birth"},{key:"status",label:"Status"}]} actions={[
    { endpoint:"", title:"Open", href:"/portal/students/{id}" },
    { endpoint:"account-invitations", title:"Invite student", payload:{targetType:2}, itemIdField:"targetId" },
    { endpoint:"students/{id}/enrollments", title:"Add enrolment", fields:placementFields },
    { endpoint:"students/{id}/re-enrolments", title:"Register returning student", fields:placementFields },
    { endpoint:"students/{id}/progressions", title:"Promote, repeat or transfer", fields:progressionFields },
    { endpoint:"students/{id}/complete-enrolment", title:"Complete current enrolment" },
    { endpoint:"students/{id}/graduate", title:"Graduate student" },
    { endpoint:"students/{id}/guardians", title:"Link guardian", fields:[{name:"guardianId",label:"Guardian",type:"select",optionsEndpoint:"guardians",optionsCollectionKey:"items"},{name:"relationship",label:"Relationship",type:"select",numeric:true,options:[{value:"0",label:"Mother"},{value:"1",label:"Father"},{value:"2",label:"Parent"},{value:"3",label:"Legal guardian"},{value:"4",label:"Relative"},{value:"5",label:"Sponsor"},{value:"6",label:"Other"}]},{name:"isPrimary",label:"Primary guardian",type:"select",options:[{value:"true",label:"Yes"},{value:"false",label:"No"}]},{name:"isEmergencyContact",label:"Emergency contact",type:"select",options:[{value:"true",label:"Yes"},{value:"false",label:"No"}]},{name:"mayCollect",label:"May collect",type:"select",options:[{value:"true",label:"Yes"},{value:"false",label:"No"}]}] },
    { endpoint:"documents/Student/{id}/uploads", title:"Upload document", documentUpload:true, fields:[{name:"category",label:"Category",type:"select",options:[{value:"identity",label:"Identity"},{value:"medical",label:"Medical"},{value:"photo",label:"Photograph"},{value:"other",label:"Other"}]},{name:"file",label:"PDF, JPEG, or PNG (maximum 10 MB)",type:"file"}] },
    { endpoint:"students/{id}/withdraw", title:"Withdraw student" }
  ]} />;
}
