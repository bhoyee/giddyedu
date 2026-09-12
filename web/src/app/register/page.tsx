import { RegisterForm } from "@/components/auth-form";
import { AuthShell } from "@/components/auth-shell";

export default function RegisterPage() { return <AuthShell eyebrow="School registration" title="Build your school’s workspace." introduction="Create the school, main campus and first trusted administrator to begin onboarding." imageSrc="/auth-school-registration.png" imageAlt="Two school leaders planning their school’s next chapter" panelEyebrow="A foundation that can grow" panelTitle="Bring your school’s next chapter into focus." panelDescription="Start with one connected campus, clear ownership and a connected foundation designed to grow with your community."><RegisterForm /></AuthShell>; }
