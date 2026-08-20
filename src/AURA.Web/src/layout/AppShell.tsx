import type { ReactNode } from "react";
import { Sidebar } from "./Sidebar";

interface AppShellProps {
  children: ReactNode;
  onNavigate?: (path: string) => void;
}

export function AppShell({ children, onNavigate }: AppShellProps) {
  return (
    <div className="app-shell">
      <Sidebar onNavigate={onNavigate} />
      <div className="app-shell__content">{children}</div>
    </div>
  );
}
