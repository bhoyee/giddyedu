import type { Metadata, Viewport } from "next";
import "./globals.css";
import { ServiceWorkerRegistration } from "@/components/service-worker-registration";
import { CookieConsent } from "@/components/cookie-consent";

export const metadata: Metadata = {
  title: "GiddyEdu — School Operating Platform",
  description: "GiddyEdu brings school administration, academics, communication, and operations into one secure platform.",
  applicationName: "GiddyEdu",
  manifest: "/manifest.webmanifest",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "GiddyEdu" },
  icons: {
    icon: [{ url: "/icons/giddyedu.svg", type: "image/svg+xml", sizes: "any" }],
    apple: [{ url: "/icons/giddyedu-apple-180.png", type: "image/png", sizes: "180x180" }],
  },
};

export const viewport: Viewport = { themeColor: "#12372a", colorScheme: "light" };

export default function RootLayout({ children }: LayoutProps<"/">) {
  return <html lang="en" className="h-full antialiased"><body className="flex min-h-full flex-col">{children}<ServiceWorkerRegistration /><CookieConsent /></body></html>;
}
