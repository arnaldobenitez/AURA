import type { ProjectActorTypeCode, ProjectAgentCode } from "../../lib/projectControl";
import { actorTypeLabels, formatAgentLabel } from "./labels";

interface InterventionSummaryProps {
  actorType: ProjectActorTypeCode | null;
  actorName?: string | null;
  agent?: ProjectAgentCode | null;
  model?: string | null;
  environment?: string | null;
}

export function InterventionSummary({ actorType, actorName, agent, model, environment }: InterventionSummaryProps) {
  if (!actorType) {
    return <span className="pc-intervention pc-intervention--empty">—</span>;
  }

  const agentLabel = formatAgentLabel(agent);
  const who = agentLabel ?? actorName;
  const isHuman = actorType === "HUMAN";

  return (
    <span className="pc-intervention">
      <span className="pc-intervention__role">
        {actorTypeLabels[actorType]}
        {who && <> · {who}</>}
      </span>
      {/* Una intervencion no humana siempre identifica modelo y entorno, cada uno en su
          propia linea etiquetada - nunca combinados ni ocultos, y "Sin informar" en vez
          de fabricar un dato cuando no se conoce (ver correccion del circuito). */}
      {!isHuman && (
        <>
          <span className="pc-intervention__model">Modelo: {model ?? "Sin informar"}</span>
          <span className="pc-intervention__environment">Entorno: {environment ?? "Sin informar"}</span>
        </>
      )}
    </span>
  );
}
