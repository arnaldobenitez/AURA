import { useCallback, useEffect, useMemo, useState } from "react";
import {
  assignCredentialProject,
  createAgentCredential,
  getApiBaseUrl,
  listAdminProjects,
  listAgentCredentials,
  revokeAgentCredential,
  revokeCredentialProject,
  rotateAgentCredential,
  type AdminAgentCredential,
  type AdminProject,
  type ProvisionedCredential,
} from "../../lib/adminManagement";

interface AgentCredentialsPageProps {
  tenantId: number;
}

const dateLabel = (value: string | null) => value ? new Date(value).toLocaleString() : "—";

export function AgentCredentialsPage({ tenantId }: AgentCredentialsPageProps) {
  const [credentials, setCredentials] = useState<AdminAgentCredential[]>([]);
  const [projects, setProjects] = useState<AdminProject[]>([]);
  const [agentName, setAgentName] = useState("");
  const [selectedProjects, setSelectedProjects] = useState<number[]>([]);
  const [expiresAt, setExpiresAt] = useState("");
  const [provisioned, setProvisioned] = useState<ProvisionedCredential | null>(null);
  const [assignProjectId, setAssignProjectId] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const [credentialRows, projectRows] = await Promise.all([listAgentCredentials(tenantId), listAdminProjects(tenantId)]);
    setCredentials(credentialRows);
    setProjects(projectRows);
  }, [tenantId]);

  useEffect(() => {
    void reload().catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "No se pudieron cargar las credenciales."));
  }, [reload]);

  const apiBaseUrl = useMemo(getApiBaseUrl, []);

  const copy = async (name: string, value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(name);
      window.setTimeout(() => setCopied((current) => current === name ? null : current), 1800);
    } catch {
      setError("El navegador no permitió copiar. Seleccioná el valor y copialo manualmente.");
    }
  };

  const toggleProject = (projectId: number) => {
    setSelectedProjects((selected) => selected.includes(projectId) ? selected.filter((id) => id !== projectId) : [...selected, projectId]);
  };

  const create = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const result = await createAgentCredential({
        tenantId,
        agentName: agentName.trim(),
        actorType: "INGESTION",
        projectIds: selectedProjects,
        expiresAtUtc: expiresAt ? new Date(expiresAt).toISOString() : null,
      });
      setProvisioned(result);
      setAgentName("");
      setSelectedProjects([]);
      setExpiresAt("");
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo crear la credencial.");
    } finally {
      setBusy(false);
    }
  };

  const rotate = async (credential: AdminAgentCredential) => {
    if (!window.confirm(`Rotar la credencial ${credential.credentialId}? El secreto actual quedará revocado.`)) return;
    setError(null);
    setNotice(null);
    try {
      setProvisioned(await rotateAgentCredential(tenantId, credential.credentialId));
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo rotar la credencial.");
    }
  };

  const revoke = async (credential: AdminAgentCredential) => {
    if (!window.confirm(`Revocar la credencial ${credential.credentialId}? La revocación bloquea nuevos tokens y eventos inmediatamente.`)) return;
    setError(null);
    setNotice(null);
    try {
      await revokeAgentCredential(tenantId, credential.credentialId);
      setNotice("Credencial revocada.");
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo revocar la credencial.");
    }
  };

  const assign = async (credentialId: number) => {
    const projectId = Number(assignProjectId[credentialId]);
    if (!Number.isInteger(projectId) || projectId <= 0) return;
    setError(null);
    try {
      await assignCredentialProject(tenantId, credentialId, projectId);
      setNotice("Proyecto asociado; el alcance actualizado aplica también a tokens existentes.");
      setAssignProjectId((current) => ({ ...current, [credentialId]: "" }));
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo asociar el proyecto.");
    }
  };

  const unassign = async (credential: AdminAgentCredential, projectId: number) => {
    setError(null);
    try {
      await revokeCredentialProject(tenantId, credential.credentialId, projectId);
      setNotice("Acceso al proyecto revocado inmediatamente.");
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo desasociar el proyecto.");
    }
  };

  const closeProvisioned = () => {
    setProvisioned(null);
    setCopied(null);
  };

  return (
    <main className="pc-page admin-page">
      <header className="pc-header">
        <div>
          <h1 className="pc-header__title">Administración · Credenciales de agentes</h1>
          <p className="admin-page__subtitle">Credenciales técnicas `INGESTION`, limitadas a proyectos asignados. Los secretos no se guardan en el navegador.</p>
        </div>
        <span className="pc-header__tenant">Tenant {tenantId}</span>
      </header>

      {error ? <div className="admin-alert admin-alert--error" role="alert">{error}</div> : null}
      {notice ? <div className="admin-alert admin-alert--success" role="status">{notice}</div> : null}
      {credentials.some((credential) => credential.status === "ACTIVE" && credential.scopeMode === "LEGACY_TENANT_WIDE") ? (
        <div className="admin-warning" role="note">Hay credenciales legacy con alcance a todo el tenant. Asigná un proyecto para restringirlas inmediatamente; para rotarlas primero deben tener al menos una asignación.</div>
      ) : null}

      {provisioned ? (
        <section className="admin-card admin-secret-card" aria-live="polite">
          <div className="admin-secret-card__header">
            <div><h2>Secreto de un solo uso</h2><p>Copialo ahora y guardalo en el secret manager del agente. AURA no podrá recuperarlo.</p></div>
            <button className="admin-button" type="button" onClick={closeProvisioned}>Cerrar y borrar de pantalla</button>
          </div>
          <div className="admin-warning" role="alert"><strong>Advertencia:</strong> el secreto no podrá volver a consultarse. Al cerrar esta pantalla se elimina de la memoria de la interfaz.</div>
          <div className="admin-copy-grid">
            <CopyValue label="AURA_INGESTION_CREDENTIAL_ID" value={String(provisioned.credentialId)} copied={copied} onCopy={copy} />
            <CopyValue label="AURA_INGESTION_SECRET" value={provisioned.secret} copied={copied} onCopy={copy} secret />
            <CopyValue label="AURA_TENANT_ID" value={String(provisioned.tenantId)} copied={copied} onCopy={copy} />
            <CopyValue label="AURA_API_BASE_URL" value={apiBaseUrl} copied={copied} onCopy={copy} />
            <CopyValue label="AURA_PROJECT_CODE" value={provisioned.projects.map((project) => project.projectCode).join(", ")} copied={copied} onCopy={copy} />
          </div>
          <p className="admin-page__subtitle">Agent: {provisioned.agentName} · ActorType: INGESTION · Expira: {dateLabel(provisioned.expiresAtUtc)}</p>
        </section>
      ) : null}

      <section className="admin-card">
        <h2>Crear credencial técnica</h2>
        <form className="admin-form" onSubmit={(event) => void create(event)}>
          <label className="admin-field">AgentName
            <input value={agentName} maxLength={100} required onChange={(event) => setAgentName(event.target.value)} disabled={busy} placeholder="SMT-DEVIN" />
          </label>
          <label className="admin-field">Tipo de actor
            <input value="INGESTION" readOnly aria-readonly="true" />
          </label>
          <fieldset className="admin-project-picker">
            <legend>Proyectos permitidos (seleccioná al menos uno)</legend>
            {projects.filter((project) => project.isActive).map((project) => (
              <label className="admin-checkbox" key={project.projectId}>
                <input type="checkbox" checked={selectedProjects.includes(project.projectId)} onChange={() => toggleProject(project.projectId)} disabled={busy} />
                <span><strong>{project.projectCode}</strong> — {project.projectName}</span>
              </label>
            ))}
            {projects.filter((project) => project.isActive).length === 0 ? <p>No hay proyectos activos para este tenant. Creá o activá un proyecto primero.</p> : null}
          </fieldset>
          <label className="admin-field">Expiración (opcional)
            <input type="datetime-local" value={expiresAt} onChange={(event) => setExpiresAt(event.target.value)} disabled={busy} />
          </label>
          <button className="pc-refresh-button" type="submit" disabled={busy || selectedProjects.length === 0}>{busy ? "Creando…" : "Crear credencial"}</button>
        </form>
      </section>

      <section className="admin-card">
        <h2>Credenciales registradas</h2>
        {credentials.length === 0 ? <p className="admin-empty">No hay credenciales para este tenant.</p> : (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>ID / agente</th><th>Tipo / alcance</th><th>Proyectos</th><th>Estado / fechas</th><th>Gestión</th></tr></thead>
              <tbody>{credentials.map((credential) => {
                const activeProjects = credential.projects.filter((project) => project.revokedAtUtc === null);
                const availableProjects = projects.filter((project) => project.isActive && project.projectCode !== null && !activeProjects.some((assigned) => assigned.projectId === project.projectId));
                return (
                  <tr key={credential.credentialId}>
                    <td><strong>{credential.credentialId}</strong><br />{credential.agentName}<br /><small>Rotada desde: {credential.rotatedFromCredentialId ?? "—"}</small></td>
                    <td>{credential.actorType}<br /><span className={`admin-status ${credential.scopeMode === "LEGACY_TENANT_WIDE" ? "admin-status--warning" : "admin-status--active"}`}>{credential.scopeMode}</span></td>
                    <td><ul className="admin-project-list">{credential.projects.map((project) => (
                      <li key={`${project.projectId}-${project.assignedAtUtc}`}>
                        {project.projectCode || project.projectId}{project.revokedAtUtc ? " (revocado)" : ""}
                        {project.revokedAtUtc === null ? <button className="admin-link-button" type="button" onClick={() => void unassign(credential, project.projectId)}>Quitar</button> : null}
                      </li>
                    ))}</ul>
                      {credential.actorType === "INGESTION" && credential.status === "ACTIVE" && availableProjects.length > 0 ? (
                        <div className="admin-assignment">
                          <select aria-label={`Proyecto para credencial ${credential.credentialId}`} value={assignProjectId[credential.credentialId] ?? ""} onChange={(event) => setAssignProjectId((current) => ({ ...current, [credential.credentialId]: event.target.value }))}>
                            <option value="">Agregar proyecto…</option>{availableProjects.map((project) => <option key={project.projectId} value={project.projectId}>{project.projectCode}</option>)}
                          </select>
                          <button className="admin-button" type="button" disabled={!assignProjectId[credential.credentialId]} onClick={() => void assign(credential.credentialId)}>Asignar</button>
                        </div>
                      ) : null}
                    </td>
                    <td><span className={`admin-status admin-status--${credential.status.toLowerCase()}`}>{credential.status}</span><br />Creada: {dateLabel(credential.createdAtUtc)}<br />Expira: {dateLabel(credential.expiresAtUtc)}<br />Revocada: {dateLabel(credential.revokedAtUtc)}<br />Último uso: {dateLabel(credential.lastUsedAtUtc)}</td>
                    <td><div className="admin-actions admin-actions--stack">
                      {credential.status === "ACTIVE" ? <><button className="admin-button" type="button" onClick={() => void rotate(credential)}>Rotar</button><button className="admin-button admin-button--danger" type="button" onClick={() => void revoke(credential)}>Revocar</button></> : null}
                    </div></td>
                  </tr>
                );
              })}</tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}

function CopyValue({ label, value, copied, onCopy, secret = false }: {
  label: string;
  value: string;
  copied: string | null;
  onCopy: (name: string, text: string) => void;
  secret?: boolean;
}) {
  const [revealed, setRevealed] = useState(false);
  return (
    <div className="admin-copy-value">
      <label>{label}</label>
      <div><input value={secret && !revealed ? "••••••••••••••••••••••••" : value} readOnly onFocus={(event) => event.currentTarget.select()} aria-label={label} />
        {secret ? <button className="admin-button" type="button" onClick={() => setRevealed((current) => !current)}>{revealed ? "Ocultar" : "Mostrar"}</button> : null}
        <button className="admin-button" type="button" onClick={() => onCopy(label, value)}>{copied === label ? "Copiado" : "Copiar"}</button>
      </div>
    </div>
  );
}
