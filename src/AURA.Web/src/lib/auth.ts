const STORAGE_KEY = "aura.auth";
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export interface StoredSession {
  accessToken: string;
  userName: string;
}

export function loadSession(): StoredSession | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const session = JSON.parse(raw) as StoredSession;
    if (!session.accessToken) {
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

function saveSession(session: StoredSession) {
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession() {
  window.localStorage.removeItem(STORAGE_KEY);
}

export class LoginError extends Error {}

export async function login(email: string, password: string): Promise<StoredSession> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    throw new LoginError(
      response.status === 401 ? "Email o contraseña incorrectos." : `No se pudo iniciar sesión (HTTP ${response.status}).`,
    );
  }

  const body = (await response.json()) as { accessToken: string; expiresAtUtc: string; userName: string };
  const session: StoredSession = { accessToken: body.accessToken, userName: body.userName };
  saveSession(session);
  return session;
}

export const SESSION_EXPIRED_EVENT = "aura:session-expired";

/**
 * fetch wrapper that attaches the stored Bearer token and treats a 401 as "session no longer
 * valid" - clears it and broadcasts a window event so the app can show the login screen again,
 * instead of surfacing a generic error deep in some data hook.
 */
export async function authFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const session = loadSession();
  const headers = new Headers(init.headers);
  if (session) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  const response = await fetch(input, { ...init, headers });

  if (response.status === 401) {
    clearSession();
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
  }

  return response;
}
