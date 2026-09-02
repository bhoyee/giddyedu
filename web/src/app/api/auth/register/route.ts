import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse } from "@/lib/server-api";

export async function POST(request: NextRequest) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const payload = await request.json();
  const upstream = await fetch(apiUrl("/api/v1/auth/register"), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload), cache: "no-store" });
  const body = await parseApiResponse(upstream);
  return NextResponse.json(body, { status: upstream.status });
}
