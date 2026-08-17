import type { LucideIcon } from "lucide-react";
import { LayoutDashboard } from "lucide-react";

export interface NavItem {
  id: string;
  label: string;
  icon: LucideIcon;
}

// Add new sections here as they're built; Sidebar renders them in order.
// The first item is treated as the active one until real routing exists.
export const navItems: NavItem[] = [
  { id: "project-control", label: "Control de proyectos IA", icon: LayoutDashboard },
];
