import type { LucideIcon } from "lucide-react";
import { LayoutDashboard } from "lucide-react";

export interface NavItem {
  id: string;
  label: string;
  icon: LucideIcon;
}

export const navItems: NavItem[] = [
  { id: "project-control", label: "Control de proyectos IA", icon: LayoutDashboard },
];
