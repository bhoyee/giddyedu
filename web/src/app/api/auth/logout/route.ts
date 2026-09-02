import { NextResponse } from "next/server";
import { clearAuthenticationCookies, hasTrustedOrigin } from "@/lib/server-api";

export async function POST(request: Request) { if (!hasTrustedOrigin(request)) return NextResponse.json({ message: "Untrusted request origin." }, { status: 403 }); const response = NextResponse.json({ authenticated: false }); clearAuthenticationCookies(response); return response; }
