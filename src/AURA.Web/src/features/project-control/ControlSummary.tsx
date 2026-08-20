import type { ProjectActorTypeCode, ProjectAgentCode } from "../../lib/projectControl";
import { ActorBadge } from "./ActorBadge";
import { formatAgentLabel } from "./labels";

interface ControlSummaryProps {
  controlType: ProjectActorTypeCode | null;
  controlName: string | null;
  controlAgent?: ProjectAgentCode | null;
  controlModel?: string | null;
  controlEnvironment?: string | null;
}

export function ControlSummary({
  controlType,
  controlName,
  controlAgent,
  controlModel,
  controlEnvironment,
}: ControlSummaryProps) {
  if (!controlType) {
    return <span className="pc-actor pc-actor--empty">—</span>;
  }

  if (controlType === "HUMAN") {
    return <ActorBadge type={controlType} name={controlName} emphasize />;
  }

  // Ejecucion IA activa: agente, modelo y entorno siempre en su propia linea etiquetada,
  // nunca combinados ni ocultos (misma regla que InterventionSummary).
  return (
    <span className="pc-control pc-control--execution">
      <span className="pc-control__label">Ejecución IA</span>
      <span className="pc-control__agent">Agente: {formatAgentLabel(controlAgent) ?? "Sin informar"}</span>
      <span className="pc-control__model">Modelo: {controlModel ?? "Sin informar"}</span>
      <span className="pc-control__environment">Entorno: {controlEnvironment ?? "Sin informar"}</span>
    </span>
  );
}
