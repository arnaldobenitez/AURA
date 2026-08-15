import { HealthIndicator } from "./HealthIndicator";
import { ProjectGrid } from "./ProjectGrid";
import { SummaryCards } from "./SummaryCards";
import { useProjectControlBoard } from "./useProjectControlBoard";

const lastUpdatedFormatter = new Intl.DateTimeFormat("es-AR", { dateStyle: "medium", timeStyle: "medium" });

export function ProjectControlPage() {
  const { board, isLoading, error, lastUpdated, refresh } = useProjectControlBoard();

  const isInitialLoading = board === null && isLoading;

  return (
    <div className="pc-page">
      <header className="pc-header">
        <div className="pc-header__identity">
          <h1 className="pc-header__title">Control de proyectos IA</h1>
        </div>
        <div className="pc-header__meta">
          <span className="pc-header__tenant">Tenant 1</span>
          <HealthIndicator />
          <button type="button" className="pc-refresh-button" onClick={() => void refresh()} disabled={isLoading}>
            {isLoading ? "Actualizando…" : "Actualizar"}
          </button>
          <span className="pc-header__updated">
            {lastUpdated ? `Última actualización: ${lastUpdatedFormatter.format(lastUpdated)}` : "Sin actualizar todavía"}
          </span>
        </div>
      </header>

      <main className="pc-main">
        {error && (
          <p className="pc-error-banner" role="alert">
            {error} Mostrando los últimos datos disponibles.
          </p>
        )}

        {isInitialLoading && <p className="pc-loading">Cargando proyectos…</p>}

        {!isInitialLoading && board && (
          <>
            <SummaryCards projects={board.projects} />
            <ProjectGrid projects={board.projects} />
          </>
        )}
      </main>
    </div>
  );
}
