export type FeatureGroup = {
  name: string;
  eyebrow: string;
  description: string;
  suites: string[];
};

export const featureGroups: FeatureGroup[] = [
  { name: "School administration", eyebrow: "Control & configuration", description: "Set up your organisation, campuses, services, access and commercial relationship from one place.", suites: ["School & SaaS Administration", "Academic Structure", "SaaS Subscription & Commercial Management"] },
  { name: "Student lifecycle", eyebrow: "From enquiry to alumni", description: "Build one continuous, dependable learner record through every stage of school life.", suites: ["Admissions & Enrolment", "Student Information System (SIS)", "Parent & Guardian Management", "Alumni Management"] },
  { name: "Teaching & learning", eyebrow: "Plan, teach & assess", description: "Give educators connected tools for curriculum delivery, learning activities and academic outcomes.", suites: ["Teaching, Lessons & Curriculum", "Assignments & Homework", "LMS — E-Learning", "Examination, Assessment & Gradebook", "CBT — Computer-Based Testing", "Report Cards, Certificates & Academic Documents"] },
  { name: "Daily school operations", eyebrow: "Every school day", description: "Coordinate schedules, presence, movement and the activities that keep the school day flowing.", suites: ["Timetable & Scheduling", "Student Attendance", "Events & School Calendar", "Leave, Permissions & Student Movement", "Clubs, Houses, Sports & Extracurricular Activities"] },
  { name: "Finance & commerce", eyebrow: "Money with clarity", description: "Manage fees, payments, accounts, purchasing and school commerce with stronger visibility.", suites: ["Fees & Billing", "Nigerian Online Payments", "Accounting & Financial Management", "Procurement & Vendor Management", "School Store"] },
  { name: "People & workforce", eyebrow: "Support your team", description: "Keep staff records, attendance, leave, time and payroll connected across the organisation.", suites: ["HR & Staff Management", "Staff Attendance, Leave & Timesheets", "Payroll"] },
  { name: "Community & care", eyebrow: "Connect and protect", description: "Build a responsive school community around communication, wellbeing and trusted support.", suites: ["Communication & Notification Hub", "School Chat & Collaboration", "Behaviour, Discipline & Student Welfare", "Health & Medical Records", "Surveys, Polls & Feedback", "Helpdesk, Complaints & Grievances"] },
  { name: "Campus services", eyebrow: "Beyond the classroom", description: "Run the physical resources and services families rely on throughout the school day.", suites: ["Library Management", "Inventory & Asset Management", "Transportation", "Hostel / Boarding Management", "Canteen / Meal Management", "Facilities & Maintenance", "Visitor, Gate & Security Management"] },
  { name: "Digital enterprise", eyebrow: "Work without silos", description: "Connect documents, workflows, websites and external services across the whole institution.", suites: ["Document Management", "Workflow & Approval Engine", "Website & CMS", "Integrations & API Platform"] },
  { name: "Intelligence & experience", eyebrow: "See, decide & act", description: "Turn connected school information into useful insight and role-specific digital experiences.", suites: ["Analytics & Reporting", "AI & Intelligent Automation", "Portals & Mobile Experience", "Security, Privacy & Compliance"] },
];

export const suiteCount = featureGroups.reduce((total, group) => total + group.suites.length, 0);
