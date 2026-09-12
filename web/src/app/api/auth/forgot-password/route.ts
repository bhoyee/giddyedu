import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse } from "@/lib/server-api";

export async function POST(request: NextRequest) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const upstream = await fetch(apiUrl("/api/v1/auth/forgot-password"), { method: "POST", headers: { "Content-Type": "application/json" }, body: await request.text(), cache: "no-store" });
  const body = await parseApiResponse(upstream);
  return body === null ? new NextResponse(null, { status: upstream.status }) : NextResponse.json(body, { status: upstream.status });
}
