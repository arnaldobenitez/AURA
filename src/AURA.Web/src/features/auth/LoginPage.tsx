import { useState } from "react";
import type { FormEvent } from "react";
import { LoginError, login, type StoredSession } from "../../lib/auth";

interface LoginPageProps {
  onLoggedIn: (session: StoredSession) => void;
}

export function LoginPage({ onLoggedIn }: LoginPageProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const session = await login(email.trim(), password);
      onLoggedIn(session);
    } catch (err) {
      setError(err instanceof LoginError ? err.message : "No se pudo iniciar sesión. Intentá de nuevo.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={(event) => void handleSubmit(event)}>
        <div className="login-card__brand">AURA</div>
        <h1 className="login-card__title">Iniciar sesión</h1>

        <div className="pc-field">
          <label className="pc-label" htmlFor="login-email">
            Email
          </label>
          <input
            id="login-email"
            className="pc-input"
            type="email"
            autoComplete="username"
            required
            autoFocus
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={submitting}
          />
        </div>

        <div className="pc-field">
          <label className="pc-label" htmlFor="login-password">
            Contraseña
          </label>
          <input
            id="login-password"
            className="pc-input"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            disabled={submitting}
          />
        </div>

        {error ? (
          <p className="pc-form-error" role="alert">
            {error}
          </p>
        ) : null}

        <button type="submit" className="pc-refresh-button login-card__submit" disabled={submitting}>
          {submitting ? "Ingresando…" : "Ingresar"}
        </button>
      </form>
    </div>
  );
}
