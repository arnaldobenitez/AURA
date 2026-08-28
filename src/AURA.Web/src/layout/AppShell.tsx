import type { ReactNode } from "react";
import type { StoredSession } from "../lib/auth";
import { Sidebar } from "./Sidebar";

interface AppShellProps {
  children: ReactNode;
  onNavigate?: (path: string) => void;
  session?: StoredSession | null;
  onLogout?: () => void;
}

export function AppShell({ children, onNavigate, session, onLogout }: AppShellProps) {
  return (
    <div className="app-shell">
      <Sidebar onNavigate={onNavigate} />
      <div className="app-shell__content">
        {session ? (
          <div className="session-bar">
            <span>{session.userName}</span>
            <button type="button" className="session-bar__logout" onClick={onLogout}>
              Cerrar sesión
            </button>
          </div>
        ) : null}
        {children}
      </div>
    </div>
  );
}
