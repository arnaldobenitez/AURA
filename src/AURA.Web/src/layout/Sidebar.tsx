import { navItems } from "./navigation";

export function Sidebar() {
  return (
    <nav className="app-sidebar" aria-label="Menú principal">
      <div className="app-sidebar__brand">
        <span className="app-sidebar__brand-mark">AURA</span>
      </div>
      <ul className="app-sidebar__nav">
        {navItems.map((item, index) => {
          const isActive = index === 0;
          return (
            <li key={item.id}>
              <a
                href="#"
                className={`app-sidebar__link${isActive ? " app-sidebar__link--active" : ""}`}
                aria-current={isActive ? "page" : undefined}
              >
                {item.label}
              </a>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
