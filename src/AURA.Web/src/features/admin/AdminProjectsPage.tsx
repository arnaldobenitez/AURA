import { useCallback, useEffect, useState } from "react";
import {
  createAdminProject,
  listAdminProjects,
  setAdminProjectStatus,
  updateAdminProject,
  type AdminProject,
} from "../../lib/adminManagement";

interface AdminProjectsPageProps {
  tenantId: number;
}

const emptyForm = { code: "", name: "", description: "", externalKey: "" };

export function AdminProjectsPage({ tenantId }: AdminProjectsPageProps) {
  const [projects, setProjects] = useState<AdminProject[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setProjects(await listAdminProjects(tenantId));
  }, [tenantId]);

  useEffect(() => {
    void reload().catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "No se pudieron cargar los proyectos."));
  }, [reload]);

  const reset = () => {
    setForm(emptyForm);
    setEditingId(null);
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (editingId === null) {
        await createAdminProject({
          tenantId,
          projectCode: form.code.trim(),
          projectName: form.name.trim(),
          description: form.description.trim() || null,
          externalProjectKey: form.externalKey.trim() || null,
        });
        setNotice("Proyecto creado.");
      } else {
        const current = projects.find((project) => project.projectId === editingId);
        if (!current) return;
        await updateAdminProject({ ...current, projectCode: current.projectCode ?? (form.code.trim().toUpperCase() || null), projectName: form.name.trim(), description: form.description.trim() || null, externalProjectKey: form.externalKey.trim() || null });
        setNotice("Proyecto actualizado.");
      }
      reset();
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo guardar el proyecto.");
    } finally {
      setBusy(false);
    }
  };

  const edit = (project: AdminProject) => {
    setEditingId(project.projectId);
    setForm({ code: project.projectCode ?? "", name: project.projectName, description: project.description ?? "", externalKey: project.externalProjectKey ?? "" });
    setError(null);
    setNotice(null);
  };

  const changeStatus = async (project: AdminProject, action: "activate" | "pause" | "deactivate") => {
    setError(null);
    setNotice(null);
    try {
      await setAdminProjectStatus(project, action);
      setNotice(action === "deactivate" ? "Proyecto desactivado sin eliminar su historial." : action === "pause" ? "Proyecto pausado." : "Proyecto activado.");
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No se pudo cambiar el estado.");
    }
  };

  return (
    <main className="pc-page admin-page">
      <header className="pc-header">
        <div>
          <h1 className="pc-header__title">Administración · Proyectos</h1>
          <p className="admin-page__subtitle">Tenant {tenantId}. Los códigos son únicos por tenant e inmutables; los proyectos legacy sin código pueden recibir uno una sola vez.</p>
        </div>
        <span className="pc-header__tenant">Tenant {tenantId}</span>
      </header>

      {error ? <div className="admin-alert admin-alert--error" role="alert">{error}</div> : null}
      {notice ? <div className="admin-alert admin-alert--success" role="status">{notice}</div> : null}

      <section className="admin-card">
        <h2>{editingId === null ? "Crear proyecto" : `Editar proyecto ${form.code}`}</h2>
        <form className="admin-form" onSubmit={(event) => void submit(event)}>
          {editingId === null || projects.find((project) => project.projectId === editingId)?.projectCode === null ? (
            <label className="admin-field">{editingId === null ? "Código del proyecto" : "Asignar código al proyecto legacy"}
              <input value={form.code} maxLength={50} required={editingId === null} onChange={(event) => setForm({ ...form, code: event.target.value.toUpperCase() })} disabled={busy} />
            </label>
          ) : null}
          <label className="admin-field">Nombre
            <input value={form.name} maxLength={100} required onChange={(event) => setForm({ ...form, name: event.target.value })} disabled={busy} />
          </label>
          <label className="admin-field">ExternalProjectKey (opcional)
            <input value={form.externalKey} maxLength={200} onChange={(event) => setForm({ ...form, externalKey: event.target.value })} disabled={busy} />
          </label>
          <label className="admin-field">Descripción (opcional)
            <textarea value={form.description} maxLength={500} onChange={(event) => setForm({ ...form, description: event.target.value })} disabled={busy} />
          </label>
          <div className="admin-actions">
            <button className="pc-refresh-button" type="submit" disabled={busy}>{busy ? "Guardando…" : editingId === null ? "Crear proyecto" : "Guardar cambios"}</button>
            {editingId !== null ? <button className="pc-sound-toggle" type="button" onClick={reset} disabled={busy}>Cancelar edición</button> : null}
          </div>
        </form>
      </section>

      <section className="admin-card">
        <h2>Proyectos registrados</h2>
        {projects.length === 0 ? <p className="admin-empty">No hay proyectos registrados para este tenant.</p> : (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>Código</th><th>Nombre</th><th>Estado</th><th>External key</th><th>Modificado</th><th>Acciones</th></tr></thead>
              <tbody>{projects.map((project) => (
                <tr key={project.projectId}>
                  <td><strong>{project.projectCode ?? "[sin código]"}</strong></td>
                  <td>{project.projectName}</td>
                  <td><span className={`admin-status admin-status--${project.status.toLowerCase()}`}>{project.status}</span></td>
                  <td>{project.externalProjectKey ?? "—"}</td>
                  <td>{project.updatedAtUtc ? new Date(project.updatedAtUtc).toLocaleString() : "—"}</td>
                  <td><div className="admin-actions admin-actions--inline">
                    <button type="button" className="admin-button" onClick={() => edit(project)}>Editar</button>
                    {project.status === "ACTIVE" ? <button type="button" className="admin-button" onClick={() => void changeStatus(project, "pause")}>Pausar</button> : <button type="button" className="admin-button" onClick={() => void changeStatus(project, "activate")}>Activar</button>}
                    {project.status !== "ARCHIVED" ? <button type="button" className="admin-button admin-button--danger" onClick={() => void changeStatus(project, "deactivate")}>Desactivar</button> : null}
                  </div></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
