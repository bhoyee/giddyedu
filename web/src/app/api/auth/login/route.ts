import { NextRequest, NextResponse } from "next/server";
import { apiUrl, hasTrustedOrigin, parseApiResponse, setAuthenticationCookies, type TokenResponse } from "@/lib/server-api";

export async function POST(request: NextRequest) {
  if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const payload = await request.json();
  const upstream = await fetch(apiUrl("/api/v1/auth/login"), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload), cache: "no-store" });
  const body = await parseApiResponse(upstream);
  if (!upstream.ok) return NextResponse.json(body ?? { message: "Sign in failed." }, { status: upstream.status });
  const response = NextResponse.json({ authenticated: true });
  setAuthenticationCookies(response, body as TokenResponse, payload.rememberMe === true);
  return response;
}
