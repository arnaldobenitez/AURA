import { FolderKanban, KeyRound, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { useState } from "react";
import { navItems } from "./navigation";

interface SidebarProps {
  onNavigate?: (path: string) => void;
  isAdmin?: boolean;
  activePath?: string;
}

export function Sidebar({ onNavigate, isAdmin = false, activePath = "/" }: SidebarProps) {
  const [isCollapsed, setIsCollapsed] = useState(false);

  const navigate = (event: React.MouseEvent<HTMLAnchorElement>, path: string) => {
    if (!onNavigate) return;
    event.preventDefault();
    onNavigate(path);
  };

  return (
    <nav className={`app-sidebar${isCollapsed ? " app-sidebar--collapsed" : ""}`} aria-label="Menú principal">
      <div className="app-sidebar__header">
        <div className="app-sidebar__brand">
          <span className="app-sidebar__brand-mark app-sidebar__brand-mark--full">AURA</span>
          <span className="app-sidebar__brand-mark app-sidebar__brand-mark--compact" aria-hidden="true">A</span>
        </div>
        <button type="button" className="app-sidebar__toggle" onClick={() => setIsCollapsed((collapsed) => !collapsed)} aria-label={isCollapsed ? "Expandir menú" : "Contraer menú"} aria-expanded={!isCollapsed} aria-controls="primary-navigation" title={isCollapsed ? "Expandir menú" : "Contraer menú"}>
          {isCollapsed ? <PanelLeftOpen size={16} aria-hidden="true" /> : <PanelLeftClose size={16} aria-hidden="true" />}
        </button>
      </div>
      <ul id="primary-navigation" className="app-sidebar__nav">
        {navItems.map((item) => {
          const Icon = item.icon;
          const active = item.id === "project-control" && !activePath.startsWith("/admin/");
          return <li key={item.id}><a href="/" className={`app-sidebar__link${active ? " app-sidebar__link--active" : ""}`} aria-current={active ? "page" : undefined} title={isCollapsed ? item.label : undefined} onClick={(event) => navigate(event, "/")}>
            <Icon className="app-sidebar__link-icon" size={16} aria-hidden="true" /><span className="app-sidebar__link-label">{item.label}</span>
          </a></li>;
        })}
        {isAdmin ? <>
          <li className="admin-sidebar-section" aria-hidden="true"><span>Administración</span></li>
          <li><a href="/admin/projects" className={`app-sidebar__link${activePath === "/admin/projects" ? " app-sidebar__link--active" : ""}`} aria-current={activePath === "/admin/projects" ? "page" : undefined} title={isCollapsed ? "Proyectos" : undefined} onClick={(event) => navigate(event, "/admin/projects")}>
            <FolderKanban className="app-sidebar__link-icon" size={16} aria-hidden="true" /><span className="app-sidebar__link-label">Proyectos</span>
          </a></li>
          <li><a href="/admin/agent-credentials" className={`app-sidebar__link${activePath === "/admin/agent-credentials" ? " app-sidebar__link--active" : ""}`} aria-current={activePath === "/admin/agent-credentials" ? "page" : undefined} title={isCollapsed ? "Credenciales de agentes" : undefined} onClick={(event) => navigate(event, "/admin/agent-credentials")}>
            <KeyRound className="app-sidebar__link-icon" size={16} aria-hidden="true" /><span className="app-sidebar__link-label">Credenciales de agentes</span>
          </a></li>
        </> : null}
      </ul>
    </nav>
  );
}
