export type AlertKind = "inicio" | "finalizado" | "humano" | "notificacion";

const SOUND_FILES: Record<AlertKind, string> = {
  inicio: "/sonidos/inicio.mp3",
  finalizado: "/sonidos/finalizado.mp3",
  humano: "/sonidos/Humano.mp3",
  notificacion: "/sonidos/notificacion.mp3",
};

export class AudioBlockedError extends Error {
  readonly kind: AlertKind;

  constructor(kind: AlertKind, cause: unknown) {
    super(`El navegador bloqueo la reproduccion del sonido "${kind}".`, { cause });
    this.kind = kind;
  }
}

const audioElements = new Map<AlertKind, HTMLAudioElement>();

function getAudio(kind: AlertKind): HTMLAudioElement {
  let audio = audioElements.get(kind);
  if (!audio) {
    audio = new Audio(SOUND_FILES[kind]);
    audio.preload = "auto";
    audioElements.set(kind, audio);
  }
  return audio;
}

function playSound(kind: AlertKind): Promise<void> {
  const audio = getAudio(kind);
  try {
    audio.currentTime = 0;
  } catch {
    // El audio puede no tener metadata cargada todavia; no es fatal.
  }

  return new Promise((resolve, reject) => {
    const cleanup = () => {
      audio.removeEventListener("ended", onEnded);
      audio.removeEventListener("error", onError);
    };
    const onEnded = () => {
      cleanup();
      resolve();
    };
    const onError = () => {
      cleanup();
      reject(new Error(`No se pudo reproducir el sonido "${kind}".`));
    };

    audio.addEventListener("ended", onEnded);
    audio.addEventListener("error", onError);
    audio.play().catch((err: unknown) => {
      cleanup();
      reject(err);
    });
  });
}

function speak(text: string): Promise<void> {
  if (!("speechSynthesis" in window) || !text) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "es-AR";
    // Un error de TTS no debe trabar la cola de alertas: el sonido ya se escucho.
    utterance.onend = () => resolve();
    utterance.onerror = () => resolve();
    window.speechSynthesis.speak(utterance);
  });
}

export interface AlertPayload {
  kind: AlertKind;
  projectName: string;
  summary: string | null;
}

async function runAlert(payload: AlertPayload, enabled: boolean): Promise<void> {
  if (!enabled) {
    return;
  }

  try {
    await playSound(payload.kind);
  } catch (err) {
    throw new AudioBlockedError(payload.kind, err);
  }

  const text = `${payload.projectName}: ${payload.summary?.trim() || "sin avance informado"}`;
  await speak(text);
}

let tail: Promise<void> = Promise.resolve();

/** Encola una alerta (sonido + TTS). Se reproducen en orden, nunca superpuestas. */
export function enqueueAlert(payload: AlertPayload, enabled: boolean): Promise<void> {
  const result = tail.then(
    () => runAlert(payload, enabled),
    () => runAlert(payload, enabled),
  );
  // La cola interna siempre sigue adelante, aunque esta alerta particular falle;
  // el llamador se entera del fallo por la promesa que devolvemos.
  tail = result.catch(() => undefined);
  return result;
}

/** Debe llamarse dentro de un gesto del usuario (click) para destrabar el autoplay. */
export function primeAudioPlayback(): void {
  for (const kind of Object.keys(SOUND_FILES) as AlertKind[]) {
    const audio = getAudio(kind);
    audio.muted = true;
    audio
      .play()
      .then(() => {
        audio.pause();
        audio.currentTime = 0;
      })
      .catch(() => {
        // Si ni siquiera el intento "primed" funciona, se reintentara en el proximo click.
      })
      .finally(() => {
        audio.muted = false;
      });
  }

  if ("speechSynthesis" in window) {
    window.speechSynthesis.resume();
  }
}
