import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import { apiUrl, clearAuthenticationCookies, hasTrustedOrigin, parseApiResponse, refreshAuthentication, setAuthenticationCookies, type TokenResponse } from "@/lib/server-api";

type RouteContext = { params: Promise<{ path: string[] }> };
const supportedMethods = new Set(["GET", "POST", "PUT", "PATCH", "DELETE"]);

async function forward(request: NextRequest, context: RouteContext) {
  if (!supportedMethods.has(request.method)) return NextResponse.json({ message: "Method not allowed." }, { status: 405 });
  if (request.method !== "GET" && !hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 });
  const segments = (await context.params).path;
  if (segments.some(segment => !/^[a-zA-Z0-9._-]+$/.test(segment))) return NextResponse.json({ message: "Invalid API path." }, { status: 400 });
  const cookieStore = await cookies(); let accessToken = cookieStore.get("giddyedu_access")?.value; let refreshed: TokenResponse | null = null;
  const refreshToken = cookieStore.get("giddyedu_refresh")?.value;
  if (!accessToken && refreshToken) { refreshed = await refreshAuthentication(refreshToken); accessToken = refreshed?.accessToken; }
  if (!accessToken) return NextResponse.json({ message: "Authentication required." }, { status: 401 });
  const body = request.method === "GET" ? undefined : await request.text();
  const target = new URL(apiUrl(`/api/v1/${segments.join("/")}`)); target.search = request.nextUrl.search;
  const send = (token: string) => fetch(target, { method: request.method, headers: { Authorization: `Bearer ${token}`, "Content-Type": request.headers.get("content-type") ?? "application/json", "X-Correlation-ID": request.headers.get("x-correlation-id") ?? crypto.randomUUID() }, body, cache: "no-store" });
  let upstream = await send(accessToken);
  if (upstream.status === 401 && refreshToken && !refreshed) { refreshed = await refreshAuthentication(refreshToken); if (refreshed) upstream = await send(refreshed.accessToken); }
  const contentType = upstream.headers.get("content-type") ?? "";
  let response: NextResponse;
  if (contentType.includes("application/json") || contentType.includes("application/problem+json")) {
    const payload = await parseApiResponse(upstream); response = payload === null ? new NextResponse(null, { status: upstream.status }) : NextResponse.json(payload, { status: upstream.status });
  } else {
    response = new NextResponse(await upstream.arrayBuffer(), { status: upstream.status, headers: { "Content-Type": contentType || "application/octet-stream" } });
    const disposition = upstream.headers.get("content-disposition"); if (disposition) response.headers.set("Content-Disposition", disposition);
  }
  if (refreshed) setAuthenticationCookies(response, refreshed, cookieStore.get("giddyedu_remember")?.value === "1"); else if (upstream.status === 401) clearAuthenticationCookies(response);
  return response;
}

export const GET = forward; export const POST = forward; export const PUT = forward; export const PATCH = forward; export const DELETE = forward;
