import { useEffect, useState } from "react";
import { fetchHealth } from "../../lib/health";

type Status = "checking" | "available" | "unavailable";

const statusLabels: Record<Status, string> = {
  checking: "Comprobando API…",
  available: "API disponible",
  unavailable: "API no disponible",
};

export function HealthIndicator() {
  const [status, setStatus] = useState<Status>("checking");

  useEffect(() => {
    const controller = new AbortController();

    async function checkHealth() {
      try {
        await fetchHealth(controller.signal);
        setStatus("available");
      } catch {
        setStatus("unavailable");
      }
    }

    void checkHealth();

    return () => controller.abort();
  }, []);

  return (
    <span className={`pc-health-indicator pc-health-indicator--${status}`} title={statusLabels[status]}>
      <span className="pc-health-indicator__dot" aria-hidden="true" />
      <span className="pc-health-indicator__label">{statusLabels[status]}</span>
    </span>
  );
}
