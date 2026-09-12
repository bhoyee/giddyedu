import { LoginForm } from "@/components/auth-form";
import { AuthShell } from "@/components/auth-shell";

export default function LoginPage() {
  return <AuthShell eyebrow="School workspace sign in" title="Welcome back." introduction="Enter your email address and password. We’ll securely find the schools and campuses connected to your account." imageSrc="/auth-login.png" imageAlt="A teacher arriving at his modern school workspace" panelEyebrow="Back to your school day" panelTitle="Everything you need is ready when you are." panelDescription="Return to the classes, people and responsibilities connected to your secure school workspace."><LoginForm /></AuthShell>;
}
