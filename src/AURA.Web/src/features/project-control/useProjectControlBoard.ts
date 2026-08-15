import { useCallback, useEffect, useRef, useState } from "react";
import { fetchProjectControlBoard, type ProjectControlBoard } from "../../lib/projectControl";

const REFRESH_INTERVAL_MS = 30_000;
const TENANT_ID = 1;

export function useProjectControlBoard() {
  const [board, setBoard] = useState<ProjectControlBoard | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    try {
      const data = await fetchProjectControlBoard(TENANT_ID, controller.signal);
      setBoard(data);
      setError(null);
      setLastUpdated(new Date());
    } catch (err) {
      if (controller.signal.aborted) {
        return;
      }
      // Keep the last valid board on screen; only surface the error banner.
      setError(err instanceof Error ? err.message : "No se pudo actualizar el tablero.");
    } finally {
      if (!controller.signal.aborted) {
        setIsLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void refresh();

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void refresh();
      }
    }, REFRESH_INTERVAL_MS);

    return () => {
      window.clearInterval(intervalId);
      abortRef.current?.abort();
    };
  }, [refresh]);

  return { board, isLoading, error, lastUpdated, refresh };
}
