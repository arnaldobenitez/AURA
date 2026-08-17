import { useEffect, useRef, useState } from "react";
import type { ProjectControlBoard, ProjectControlProject } from "../../lib/projectControl";
import { AudioBlockedError, enqueueAlert, primeAudioPlayback, type AlertKind } from "./alertSounds";

const SOUND_ENABLED_STORAGE_KEY = "aura.projectControl.soundEnabled";

interface ProjectSnapshot {
  lastItemId: number | null;
  currentControlType: string | null;
}

function readStoredSoundEnabled(): boolean {
  if (typeof window === "undefined") {
    return true;
  }
  const stored = window.localStorage.getItem(SOUND_ENABLED_STORAGE_KEY);
  return stored === null ? true : stored === "true";
}

function classifyAlert(
  previous: ProjectSnapshot | undefined,
  current: ProjectControlProject,
): AlertKind | null {
  if (!previous) {
    // Primer snapshot conocido de este proyecto (carga inicial): no suena nada.
    return null;
  }

  const controlBecameHuman =
    previous.currentControlType !== "HUMAN" && current.currentControlType === "HUMAN";
  if (controlBecameHuman) {
    return "humano";
  }

  const itemChanged = current.lastItemId !== null && current.lastItemId !== previous.lastItemId;
  if (!itemChanged) {
    return null;
  }

  if (current.lastEventType === "CLAIM") {
    return "inicio";
  }
  if (current.lastEventType === "COMPLETION") {
    return "finalizado";
  }
  return "notificacion";
}

export function useProjectControlAlerts(board: ProjectControlBoard | null) {
  const [soundEnabled, setSoundEnabledState] = useState(readStoredSoundEnabled);
  const [needsAudioUnlock, setNeedsAudioUnlock] = useState(false);
  const snapshotsRef = useRef<Map<number, ProjectSnapshot> | null>(null);

  const setSoundEnabled = (value: boolean) => {
    setSoundEnabledState(value);
    window.localStorage.setItem(SOUND_ENABLED_STORAGE_KEY, String(value));
  };

  const unlockAudio = () => {
    primeAudioPlayback();
    setNeedsAudioUnlock(false);
  };

  useEffect(() => {
    if (!board) {
      return;
    }

    const previousSnapshots = snapshotsRef.current;
    const isBaseline = previousSnapshots === null;
    const nextSnapshots = new Map<number, ProjectSnapshot>();

    for (const project of board.projects) {
      const previous = previousSnapshots?.get(project.projectId);
      nextSnapshots.set(project.projectId, {
        lastItemId: project.lastItemId,
        currentControlType: project.currentControlType,
      });

      if (isBaseline) {
        continue;
      }

      const kind = classifyAlert(previous, project);
      if (!kind) {
        continue;
      }

      enqueueAlert(
        { kind, projectName: project.projectName, summary: project.summary },
        soundEnabled,
      ).catch((err: unknown) => {
        if (err instanceof AudioBlockedError) {
          setNeedsAudioUnlock(true);
        }
      });
    }

    snapshotsRef.current = nextSnapshots;
  }, [board, soundEnabled]);

  return { soundEnabled, setSoundEnabled, needsAudioUnlock, unlockAudio };
}
