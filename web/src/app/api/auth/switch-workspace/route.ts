import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse, setAuthenticationCookies, TokenResponse } from "@/lib/server-api";

export async function POST(request: NextRequest) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const cookieStore = await cookies();
  const accessToken = cookieStore.get("giddyedu_access")?.value;
  const refreshToken = cookieStore.get("giddyedu_refresh")?.value;
  if (!accessToken || !refreshToken) return NextResponse.json({ message: "Your session has expired." }, { status: 401 });
  const selection = await request.json() as { tenantId?: string; campusId?: string | null };
  const upstream = await fetch(apiUrl("/api/v1/auth/switch-workspace"), { method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${accessToken}` }, body: JSON.stringify({ ...selection, refreshToken }), cache: "no-store" });
  const body = await parseApiResponse(upstream);
  if (!upstream.ok) return NextResponse.json(body ?? { message: "Workspace could not be changed." }, { status: upstream.status });
  const response = NextResponse.json({ switched: true });
  setAuthenticationCookies(response, body as TokenResponse, cookieStore.get("giddyedu_remember")?.value === "1");
  return response;
}
