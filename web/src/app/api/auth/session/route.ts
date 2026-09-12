import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { apiUrl, clearAuthenticationCookies, parseApiResponse, refreshAuthentication, setAuthenticationCookies } from "@/lib/server-api";

export async function GET() {
  const cookieStore = await cookies(); let token = cookieStore.get("giddyedu_access")?.value; let refreshed = null;
  if (!token) { const refreshToken = cookieStore.get("giddyedu_refresh")?.value; if (refreshToken) { refreshed = await refreshAuthentication(refreshToken); token = refreshed?.accessToken; } }
  if (!token) return NextResponse.json({ authenticated: false }, { status: 401 });
  let upstream = await fetch(apiUrl("/api/v1/access/me"), { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" });
  if (upstream.status === 401 && !refreshed) { const refreshToken = cookieStore.get("giddyedu_refresh")?.value; if (refreshToken) { refreshed = await refreshAuthentication(refreshToken); if (refreshed) upstream = await fetch(apiUrl("/api/v1/access/me"), { headers: { Authorization: `Bearer ${refreshed.accessToken}` }, cache: "no-store" }); } }
  if (!upstream.ok) { const response = NextResponse.json({ authenticated: false }, { status: upstream.status }); if (upstream.status === 401) clearAuthenticationCookies(response); return response; }
  const response = NextResponse.json({ authenticated: true, access: await parseApiResponse(upstream) }); if (refreshed) setAuthenticationCookies(response, refreshed, cookieStore.get("giddyedu_remember")?.value === "1"); return response;
}
