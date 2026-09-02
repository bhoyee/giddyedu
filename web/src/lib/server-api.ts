import { NextResponse } from "next/server";

const apiBaseUrl = process.env.API_INTERNAL_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
const secureCookie = process.env.AUTH_COOKIE_SECURE ? process.env.AUTH_COOKIE_SECURE === "true" : process.env.NODE_ENV === "production";

export type TokenResponse = { accessToken: string; refreshToken: string; expiresAtUtc: string };

export function apiUrl(path: string) {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return `${apiBaseUrl.replace(/\/$/, "")}${normalized}`;
}

export function setAuthenticationCookies(response: NextResponse, tokens: TokenResponse) {
  const expires = new Date(tokens.expiresAtUtc);
  response.cookies.set("giddyedu_access", tokens.accessToken, { httpOnly: true, secure: secureCookie, sameSite: "lax", path: "/", expires });
  response.cookies.set("giddyedu_refresh", tokens.refreshToken, { httpOnly: true, secure: secureCookie, sameSite: "strict", path: "/", maxAge: 60 * 60 * 24 * 14 });
}

export function clearAuthenticationCookies(response: NextResponse) {
  response.cookies.set("giddyedu_access", "", { httpOnly: true, secure: secureCookie, sameSite: "lax", path: "/", maxAge: 0 });
  response.cookies.set("giddyedu_refresh", "", { httpOnly: true, secure: secureCookie, sameSite: "strict", path: "/", maxAge: 0 });
}

export async function parseApiResponse(response: Response) {
  const text = await response.text();
  return text ? JSON.parse(text) as unknown : null;
}

export async function refreshAuthentication(refreshToken: string) {
  const response = await fetch(apiUrl("/api/v1/auth/refresh"), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken }), cache: "no-store" });
  if (!response.ok) return null;
  return await parseApiResponse(response) as TokenResponse;
}

export function hasTrustedOrigin(request: Request) {
  const origin = request.headers.get("origin");
  const forwardedHost = request.headers.get("x-forwarded-host") ?? request.headers.get("host");
  const forwardedProtocol = request.headers.get("x-forwarded-proto") ?? new URL(request.url).protocol.replace(":", "");
  return Boolean(origin && forwardedHost && origin === `${forwardedProtocol}://${forwardedHost}`);
}
