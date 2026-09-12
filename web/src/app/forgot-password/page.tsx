import { ForgotPasswordForm } from "@/components/auth-form";
import { AuthShell } from "@/components/auth-shell";

export default function ForgotPasswordPage() {
  return <AuthShell eyebrow="Account recovery" title="Reset your password." introduction="We’ll send a six-digit verification code to the email address connected to your account." imageSrc="/auth-password-recovery.png" imageAlt="A school administrator securely checking a verification message" panelEyebrow="Secure account recovery" panelTitle="A clear way back into your workspace." panelDescription="Short-lived verification codes and careful session controls help keep school accounts protected."><ForgotPasswordForm /></AuthShell>;
}
