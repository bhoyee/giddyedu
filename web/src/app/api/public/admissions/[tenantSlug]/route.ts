import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse } from "@/lib/server-api";

export async function POST(request: NextRequest, { params }: { params: Promise<{ tenantSlug: string }> }) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const { tenantSlug } = await params;
  if (!/^[a-z0-9-]{3,100}$/.test(tenantSlug)) return NextResponse.json({ message: "Invalid school." }, { status: 400 });
  const upstream = await fetch(apiUrl(`/api/v1/public/admissions/${tenantSlug}/applications`), { method: "POST", headers: { "Content-Type": "application/json" }, body: await request.text(), cache: "no-store" });
  return NextResponse.json(await parseApiResponse(upstream), { status: upstream.status });
}
