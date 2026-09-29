import { useCallback, useEffect, useState } from "react";
import { LoginPage } from "./features/auth/LoginPage";
import { AdminProjectsPage } from "./features/admin/AdminProjectsPage";
import { AgentCredentialsPage } from "./features/admin/AgentCredentialsPage";
import { ProjectControlPage } from "./features/project-control/ProjectControlPage";
import { ProyectoDetalles } from "./features/project-details/ProyectoDetalles";
import { AppShell } from "./layout/AppShell";
import { checkAdminAccess } from "./lib/adminManagement";
import { SESSION_EXPIRED_EVENT, clearSession, loadSession, type StoredSession } from "./lib/auth";

const PROJECT_DETAILS_PREFIX = "/proyectos/";

function readLocation() {
  return window.location.pathname;
}

function App() {
  const [pathname, setPathname] = useState(readLocation);
  const [session, setSession] = useState<StoredSession | null>(loadSession);
  const [adminAccess, setAdminAccess] = useState<{ tenantId: number | null; isAdmin: boolean } | null>(null);

  useEffect(() => {
    const handlePopState = () => setPathname(readLocation());
    window.addEventListener("popstate", handlePopState);

    const handleSessionExpired = () => {
      setSession(null);
      setAdminAccess(null);
    };
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

  const accessToken = session?.accessToken;
  useEffect(() => {
    if (!accessToken) {
      setAdminAccess(null);
      return;
    }

    let cancelled = false;
    void checkAdminAccess()
      .then((result) => { if (!cancelled) setAdminAccess(result); })
      .catch(() => { if (!cancelled) setAdminAccess({ tenantId: 1, isAdmin: false }); });
    return () => { cancelled = true; };
  }, [accessToken]);

  useEffect(() => {
    if (pathname.startsWith("/admin/") && adminAccess?.isAdmin === false) navigate("/");
  }, [pathname, adminAccess, navigate]);

  if (!session) {
    return <LoginPage onLoggedIn={setSession} />;
  }

  const projectKey = pathname.startsWith(PROJECT_DETAILS_PREFIX)
    ? decodeURIComponent(pathname.slice(PROJECT_DETAILS_PREFIX.length))
    : null;
  const tenantId = adminAccess?.tenantId ?? 1;
  const waitingForAdminCheck = pathname.startsWith("/admin/") && adminAccess === null;

  return (
    <AppShell
      onNavigate={navigate}
      session={session}
      isAdmin={adminAccess?.isAdmin === true}
      activePath={pathname}
      onLogout={() => {
        clearSession();
        setSession(null);
        setAdminAccess(null);
      }}
    >
      {waitingForAdminCheck ? <main className="pc-page"><p role="status">Verificando permisos administrativos…</p></main> :
        pathname === "/admin/projects" && adminAccess?.isAdmin ? <AdminProjectsPage tenantId={tenantId} /> :
          pathname === "/admin/agent-credentials" && adminAccess?.isAdmin ? <AgentCredentialsPage tenantId={tenantId} /> :
            projectKey ? (
              <ProyectoDetalles projectKey={projectKey} onBack={() => navigate("/")} />
            ) : (
              <ProjectControlPage
                canManageProjects={adminAccess?.isAdmin === true}
                onProjectSelect={(project) =>
                  navigate(`${PROJECT_DETAILS_PREFIX}${encodeURIComponent(project.projectCode ?? String(project.projectId))}`)
                }
              />
            )}
    </AppShell>
  );
}

export default App;
