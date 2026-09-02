import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

type Tokens = { accessToken: string; refreshToken: string; expiresAtUtc: string };
export type AccessContext = { userId: string; tenantId: string; campusId: string | null; permissions: string[]; entitlements: Record<string, { enabled: boolean }> };
const tokenKey = 'giddyedu.family.tokens';
let webTokens: Tokens | null = null;

function apiUrl(path: string) {
  const origin = process.env.EXPO_PUBLIC_API_URL?.replace(/\/$/, '');
  if (!origin) throw new Error('EXPO_PUBLIC_API_URL is not configured.');
  return `${origin}${path}`;
}

async function readTokens() { if (Platform.OS === 'web') return webTokens; const stored = await SecureStore.getItemAsync(tokenKey); if (!stored) return null; try { return JSON.parse(stored) as Tokens; } catch { await SecureStore.deleteItemAsync(tokenKey); return null; } }
async function saveTokens(tokens: Tokens) { if (Platform.OS === 'web') webTokens = tokens; else await SecureStore.setItemAsync(tokenKey, JSON.stringify(tokens), { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY }); }
export async function signOut() { webTokens = null; if (Platform.OS !== 'web') await SecureStore.deleteItemAsync(tokenKey); }
export async function hasStoredSession() { return (await readTokens()) !== null; }

export async function signIn(email: string, password: string, tenantId: string, campusId?: string) {
  const response = await fetch(apiUrl('/api/v1/auth/login'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, tenantId, campusId: campusId || null }) });
  if (!response.ok) throw new Error(response.status === 401 ? 'Check your email, password, and confirmed account.' : 'Sign-in failed.');
  const tokens = await response.json() as Tokens; await saveTokens(tokens); return tokens;
}

export async function authenticatedFetch(path: string, init?: RequestInit) {
  let tokens = await readTokens(); if (!tokens) throw new Error('Authentication is required.');
  let response = await fetch(apiUrl(path), { ...init, headers: { ...init?.headers, Authorization: `Bearer ${tokens.accessToken}` } });
  if (response.status !== 401) return response;
  const refresh = await fetch(apiUrl('/api/v1/auth/refresh'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: tokens.refreshToken }) });
  if (!refresh.ok) { await signOut(); throw new Error('Your session has expired.'); }
  tokens = await refresh.json() as Tokens; await saveTokens(tokens);
  response = await fetch(apiUrl(path), { ...init, headers: { ...init?.headers, Authorization: `Bearer ${tokens.accessToken}` } }); return response;
}
export async function getAccessContext() { const response = await authenticatedFetch('/api/v1/access/me'); if (!response.ok) throw new Error('Your family access could not be loaded.'); return response.json() as Promise<AccessContext>; }
