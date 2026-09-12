import { ConfirmEmailForm } from "@/components/confirm-email-form";
import { AuthShell } from "@/components/auth-shell";

export default async function ConfirmEmailPage({ searchParams }: { searchParams: Promise<{ email?: string; token?: string; sent?: string }> }) {
  const query = await searchParams;
  return <AuthShell eyebrow="Email verification" title="Confirm your email." introduction="Complete this security step to activate your account and school workspace." imageSrc="/auth-school-registration.png" imageAlt="School leaders preparing their GiddyEdu workspace" panelEyebrow="One final security step" panelTitle="Your school workspace is nearly ready." panelDescription="Verification protects your school, its people and the information entrusted to GiddyEdu."><ConfirmEmailForm email={query.email ?? ""} token={query.token ?? ""} codeAlreadySent={query.sent === "1"} /></AuthShell>;
}
