import { useCallback, useEffect, useRef, useState } from "react";
import { fetchProjectHistory, type ProjectHistory } from "../../lib/projectControl";

const TENANT_ID = 1;

export function useProjectHistory(projectCode: string | null) {
  const [history, setHistory] = useState<ProjectHistory | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    if (!projectCode) {
      setHistory(null);
      setIsLoading(false);
      return;
    }

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    try {
      const data = await fetchProjectHistory(TENANT_ID, projectCode, controller.signal);
      setHistory(data);
      setError(null);
    } catch (err) {
      if (controller.signal.aborted) {
        return;
      }
      setError(err instanceof Error ? err.message : "No se pudo obtener el historial del proyecto.");
    } finally {
      if (!controller.signal.aborted) {
        setIsLoading(false);
      }
    }
  }, [projectCode]);

  useEffect(() => {
    void refresh();
    return () => abortRef.current?.abort();
  }, [refresh]);

  return { history, isLoading, error, refresh };
}
