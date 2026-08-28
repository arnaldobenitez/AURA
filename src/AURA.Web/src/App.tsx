import { useCallback, useEffect, useState } from "react";
import { LoginPage } from "./features/auth/LoginPage";
import { ProjectControlPage } from "./features/project-control/ProjectControlPage";
import { ProyectoDetalles } from "./features/project-details/ProyectoDetalles";
import { AppShell } from "./layout/AppShell";
import { SESSION_EXPIRED_EVENT, clearSession, loadSession, type StoredSession } from "./lib/auth";

const PROJECT_DETAILS_PREFIX = "/proyectos/";

function readLocation() {
  return window.location.pathname;
}

function App() {
  const [pathname, setPathname] = useState(readLocation);
  const [session, setSession] = useState<StoredSession | null>(loadSession);

  useEffect(() => {
    const handlePopState = () => setPathname(readLocation());
    window.addEventListener("popstate", handlePopState);

    const handleSessionExpired = () => setSession(null);
    window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);

    return () => {
      window.removeEventListener("popstate", handlePopState);
      window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
    };
  }, []);

  const navigate = useCallback((nextPath: string) => {
    if (window.location.pathname !== nextPath) {
      window.history.pushState({}, "", nextPath);
    }
    setPathname(nextPath);
    window.scrollTo({ top: 0, behavior: "instant" });
  }, []);

  if (!session) {
    return <LoginPage onLoggedIn={setSession} />;
  }

  const projectKey = pathname.startsWith(PROJECT_DETAILS_PREFIX)
    ? decodeURIComponent(pathname.slice(PROJECT_DETAILS_PREFIX.length))
    : null;

  return (
    <AppShell
      onNavigate={navigate}
      session={session}
      onLogout={() => {
        clearSession();
        setSession(null);
      }}
    >
      {projectKey ? (
        <ProyectoDetalles projectKey={projectKey} onBack={() => navigate("/")} />
      ) : (
        <ProjectControlPage
          onProjectSelect={(project) =>
            navigate(`${PROJECT_DETAILS_PREFIX}${encodeURIComponent(project.projectCode ?? String(project.projectId))}`)
          }
        />
      )}
    </AppShell>
  );
}

export default App;
