export interface NavItem {
  id: string;
  label: string;
}

// Add new sections here as they're built; Sidebar renders them in order.
// The first item is treated as the active one until real routing exists.
export const navItems: NavItem[] = [{ id: "project-control", label: "Control de proyectos IA" }];
