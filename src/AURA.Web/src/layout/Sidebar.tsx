import { PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { useState } from "react";
import { navItems } from "./navigation";

export function Sidebar({ onNavigate }: { onNavigate?: (path: string) => void }) {
  const [isCollapsed, setIsCollapsed] = useState(false);

  return (
    <nav
      className={`app-sidebar${isCollapsed ? " app-sidebar--collapsed" : ""}`}
      aria-label="Menú principal"
    >
      <div className="app-sidebar__header">
        <div className="app-sidebar__brand">
          <span className="app-sidebar__brand-mark app-sidebar__brand-mark--full">AURA</span>
          <span className="app-sidebar__brand-mark app-sidebar__brand-mark--compact" aria-hidden="true">A</span>
        </div>
        <button
          type="button"
          className="app-sidebar__toggle"
          onClick={() => setIsCollapsed((collapsed) => !collapsed)}
          aria-label={isCollapsed ? "Expandir menú" : "Contraer menú"}
          aria-expanded={!isCollapsed}
          aria-controls="primary-navigation"
          title={isCollapsed ? "Expandir menú" : "Contraer menú"}
        >
          {isCollapsed ? <PanelLeftOpen size={16} aria-hidden="true" /> : <PanelLeftClose size={16} aria-hidden="true" />}
        </button>
      </div>
      <ul id="primary-navigation" className="app-sidebar__nav">
        {navItems.map((item, index) => {
          const isActive = index === 0;
          const Icon = item.icon;
          return (
            <li key={item.id}>
              <a
                href="/"
                className={`app-sidebar__link${isActive ? " app-sidebar__link--active" : ""}`}
                aria-current={isActive ? "page" : undefined}
                title={isCollapsed ? item.label : undefined}
                onClick={(event) => {
                  if (!onNavigate) {
                    return;
                  }
                  event.preventDefault();
                  onNavigate("/");
                }}
              >
                <Icon className="app-sidebar__link-icon" size={16} aria-hidden="true" />
                <span className="app-sidebar__link-label">{item.label}</span>
              </a>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
