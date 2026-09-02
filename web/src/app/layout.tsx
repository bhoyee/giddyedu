import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = { title: "GiddyEdu — School Operating Platform", description: "GiddyEdu brings school administration, academics, communication, and operations into one secure platform." };
export default function RootLayout({ children }: LayoutProps<"/">) { return <html lang="en" className="h-full antialiased"><body className="flex min-h-full flex-col">{children}</body></html>; }
