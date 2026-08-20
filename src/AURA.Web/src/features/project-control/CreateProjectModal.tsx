import { useEffect, useRef, useState } from "react";
import {
  CreateProjectApiError,
  createProject,
  type ProjectStatusCode,
} from "../../lib/projectControl";
import { projectStatusLabels } from "./labels";

interface CreateProjectModalProps {
  open: boolean;
  tenantId: number;
  onClose: () => void;
  onCreated: (projectId: number) => void;
}

const CODE_MAX_LENGTH = 50;
const NAME_MAX_LENGTH = 100;
const DESCRIPTION_MAX_LENGTH = 500;
const WORKING_PATH_MAX_LENGTH = 1000;
const REPOSITORY_MAX_LENGTH = 2000;

const STATUS_OPTIONS: ProjectStatusCode[] = ["DRAFT", "ACTIVE", "PAUSED", "BLOCKED", "COMPLETED", "ARCHIVED"];

interface FormState {
  code: string;
  name: string;
  description: string;
  workingPath: string;
  repository: string;
  status: ProjectStatusCode;
  humanResponsibleId: string;
}

const emptyForm: FormState = {
  code: "",
  name: "",
  description: "",
  workingPath: "",
  repository: "",
  status: "ACTIVE",
  humanResponsibleId: "",
};

function validateClientSide(form: FormState): string[] {
  const errors: string[] = [];

  if (!form.code.trim()) {
    errors.push("El código es obligatorio.");
  } else if (form.code.trim().length > CODE_MAX_LENGTH) {
    errors.push(`El código no puede superar ${CODE_MAX_LENGTH} caracteres.`);
  }

  if (!form.name.trim()) {
    errors.push("El nombre es obligatorio.");
  } else if (form.name.trim().length > NAME_MAX_LENGTH) {
    errors.push(`El nombre no puede superar ${NAME_MAX_LENGTH} caracteres.`);
  }

  if (form.description.length > DESCRIPTION_MAX_LENGTH) {
    errors.push(`La descripción no puede superar ${DESCRIPTION_MAX_LENGTH} caracteres.`);
  }

  if (form.workingPath.length > WORKING_PATH_MAX_LENGTH) {
    errors.push(`La ruta de trabajo no puede superar ${WORKING_PATH_MAX_LENGTH} caracteres.`);
  }

  if (form.repository.length > REPOSITORY_MAX_LENGTH) {
    errors.push(`El repositorio no puede superar ${REPOSITORY_MAX_LENGTH} caracteres.`);
  }

  if (form.humanResponsibleId.trim()) {
    const parsed = Number(form.humanResponsibleId);
    if (!Number.isInteger(parsed) || parsed <= 0) {
      errors.push("El humano responsable debe ser un número entero mayor a cero.");
    }
  }

  return errors;
}

export function CreateProjectModal({ open, tenantId, onClose, onCreated }: CreateProjectModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [errors, setErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      setForm(emptyForm);
      setErrors([]);
      setSaving(false);
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  const updateField = <K extends keyof FormState>(field: K, value: FormState[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (saving) {
      return;
    }

    const clientErrors = validateClientSide(form);
    if (clientErrors.length > 0) {
      setErrors(clientErrors);
      return;
    }

    setSaving(true);
    setErrors([]);

    try {
      const response = await createProject({
        tenantId,
        projectCode: form.code.trim(),
        projectName: form.name.trim(),
        description: form.description.trim() || null,
        workingPath: form.workingPath.trim() || null,
        repository: form.repository.trim() || null,
        status: form.status,
        humanResponsibleId: form.humanResponsibleId.trim() ? Number(form.humanResponsibleId) : null,
      });

      // CREATED y ALREADY_EXISTS se tratan igual del lado del usuario: el proyecto
      // existe y hay que mostrarlo, sea que se acabe de crear o ya existiera.
      onCreated(response.projectId!);
    } catch (err) {
      if (err instanceof CreateProjectApiError) {
        if (err.response.status === "VALIDATION_FAILED") {
          setErrors(err.response.errors);
        } else {
          setErrors([`Ya existe un proyecto con el código "${form.code.trim().toUpperCase()}" pero con otro nombre.`]);
        }
      } else {
        setErrors([err instanceof Error ? err.message : "No se pudo crear el proyecto."]);
      }
      setSaving(false);
    }
  };

  return (
    <dialog ref={dialogRef} className="pc-modal" onCancel={onClose} onClose={onClose}>
      <form className="pc-form" onSubmit={(event) => void handleSubmit(event)}>
        <h2 className="pc-modal__title">Nuevo proyecto</h2>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-code">
            Código
          </label>
          <input
            id="pc-new-project-code"
            className="pc-input"
            type="text"
            value={form.code}
            maxLength={CODE_MAX_LENGTH}
            onChange={(event) => updateField("code", event.target.value)}
            disabled={saving}
            autoFocus
            required
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-name">
            Nombre
          </label>
          <input
            id="pc-new-project-name"
            className="pc-input"
            type="text"
            value={form.name}
            maxLength={NAME_MAX_LENGTH}
            onChange={(event) => updateField("name", event.target.value)}
            disabled={saving}
            required
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-human-responsible">
            Humano responsable (opcional, ID)
          </label>
          <input
            id="pc-new-project-human-responsible"
            className="pc-input"
            type="number"
            min={1}
            step={1}
            value={form.humanResponsibleId}
            onChange={(event) => updateField("humanResponsibleId", event.target.value)}
            disabled={saving}
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-description">
            Descripción (opcional)
          </label>
          <textarea
            id="pc-new-project-description"
            className="pc-input pc-input--textarea"
            value={form.description}
            maxLength={DESCRIPTION_MAX_LENGTH}
            onChange={(event) => updateField("description", event.target.value)}
            disabled={saving}
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-path">
            Ruta de trabajo (opcional)
          </label>
          <input
            id="pc-new-project-path"
            className="pc-input"
            type="text"
            value={form.workingPath}
            maxLength={WORKING_PATH_MAX_LENGTH}
            onChange={(event) => updateField("workingPath", event.target.value)}
            disabled={saving}
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-repository">
            Repositorio (opcional)
          </label>
          <input
            id="pc-new-project-repository"
            className="pc-input"
            type="text"
            value={form.repository}
            maxLength={REPOSITORY_MAX_LENGTH}
            onChange={(event) => updateField("repository", event.target.value)}
            disabled={saving}
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="pc-new-project-status">
            Estado
          </label>
          <select
            id="pc-new-project-status"
            className="pc-input"
            value={form.status}
            onChange={(event) => updateField("status", event.target.value as ProjectStatusCode)}
            disabled={saving}
          >
            {STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {projectStatusLabels[status]}
              </option>
            ))}
          </select>
        </div>

        {errors.length > 0 && (
          <ul className="pc-form-error" role="alert">
            {errors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        )}

        <div className="pc-modal__actions">
          <button type="button" className="pc-sound-toggle" onClick={onClose} disabled={saving}>
            Cancelar
          </button>
          <button type="submit" className="pc-refresh-button" disabled={saving}>
            {saving ? "Creando…" : "Crear proyecto"}
          </button>
        </div>
      </form>
    </dialog>
  );
}
