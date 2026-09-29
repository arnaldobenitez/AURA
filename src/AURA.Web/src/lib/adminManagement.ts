import { authFetch } from "./auth";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export interface AdminProject {
  tenantId: number;
  projectId: number;
  projectCode: string | null;
  projectName: string;
  description: string | null;
  externalProjectKey: string | null;
  status: string;
  isActive: boolean;
  createdAtUtc: string | null;
  updatedAtUtc: string | null;
  createdByUserId: number | null;
  updatedByUserId: number | null;
}

export interface AdminCredentialProject {
  projectId: number;
  projectCode: string;
  projectName: string;
  isActive: boolean;
  assignedAtUtc: string;
  revokedAtUtc: string | null;
}

export interface AdminAgentCredential {
  tenantId: number;
  credentialId: number;
  agentName: string;
  actorType: string;
  status: string;
  scopeMode: string;
  createdAtUtc: string;
  expiresAtUtc: string | null;
  revokedAtUtc: string | null;
  lastUsedAtUtc: string | null;
  rotatedFromCredentialId: number | null;
  projects: AdminCredentialProject[];
}

export interface ProvisionedCredential {
  tenantId: number;
  credentialId: number;
  agentName: string;
  actorType: string;
  secret: string;
  createdAtUtc: string;
  expiresAtUtc: string | null;
  projects: AdminCredentialProject[];
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { errors?: string[]; detail?: string; title?: string } | null;
    const detail = body?.errors?.join(" ") ?? body?.detail ?? body?.title;
    throw new Error(detail ?? `La operación administrativa falló (HTTP ${response.status}).`);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

const json = (method: string, body?: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: body === undefined ? undefined : JSON.stringify(body),
});

export async function checkAdminAccess(): Promise<{ tenantId: number | null; isAdmin: boolean }> {
  return await request("/api/admin/authorization");
}

export async function listAdminProjects(tenantId: number): Promise<AdminProject[]> {
  return await request(`/api/admin/projects?tenantId=${tenantId}`);
}

export async function createAdminProject(input: {
  tenantId: number;
  projectCode: string;
  projectName: string;
  description: string | null;
  externalProjectKey: string | null;
}): Promise<AdminProject> {
  return await request("/api/admin/projects", json("POST", input));
}

export async function updateAdminProject(project: AdminProject): Promise<AdminProject> {
  return await request(`/api/admin/projects/${project.projectId}`, json("PUT", {
    tenantId: project.tenantId,
    projectCode: project.projectCode,
    projectName: project.projectName,
    description: project.description,
    externalProjectKey: project.externalProjectKey,
  }));
}

export async function setAdminProjectStatus(project: AdminProject, action: "activate" | "pause" | "deactivate"): Promise<AdminProject> {
  return await request(`/api/admin/projects/${project.projectId}/${action}?tenantId=${project.tenantId}`, json("POST"));
}

export async function listAgentCredentials(tenantId: number): Promise<AdminAgentCredential[]> {
  return await request(`/api/admin/agent-credentials?tenantId=${tenantId}`);
}

export async function createAgentCredential(input: {
  tenantId: number;
  agentName: string;
  actorType: "INGESTION";
  projectIds: number[];
  expiresAtUtc: string | null;
}): Promise<ProvisionedCredential> {
  return await request("/api/admin/agent-credentials", json("POST", { ...input, linkedActorId: null }));
}

export async function rotateAgentCredential(tenantId: number, credentialId: number): Promise<ProvisionedCredential> {
  return await request(`/api/admin/agent-credentials/${credentialId}/rotate?tenantId=${tenantId}`, json("POST"));
}

export async function revokeAgentCredential(tenantId: number, credentialId: number): Promise<void> {
  await request(`/api/admin/agent-credentials/${credentialId}/revoke?tenantId=${tenantId}`, json("POST"));
}

export async function assignCredentialProject(tenantId: number, credentialId: number, projectId: number): Promise<void> {
  await request(`/api/admin/agent-credentials/${credentialId}/projects`, json("POST", { tenantId, projectId }));
}

export async function revokeCredentialProject(tenantId: number, credentialId: number, projectId: number): Promise<void> {
  await request(`/api/admin/agent-credentials/${credentialId}/projects/${projectId}?tenantId=${tenantId}`, { method: "DELETE" });
}

export function getApiBaseUrl(): string {
  return apiBaseUrl || window.location.origin;
}
