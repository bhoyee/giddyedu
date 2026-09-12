import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse } from "@/lib/server-api";

export async function POST(request: NextRequest) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const upstream = await fetch(apiUrl("/api/v1/auth/workspaces"), { method: "POST", headers: { "Content-Type": "application/json" }, body: await request.text(), cache: "no-store" });
  const body = await parseApiResponse(upstream);
  return NextResponse.json(body ?? { message: "Workspace discovery failed." }, { status: upstream.status });
}
